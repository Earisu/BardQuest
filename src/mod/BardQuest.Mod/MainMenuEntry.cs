using System.Reflection;

using TMPro;

using UnityEngine;
using UnityEngine.Events;

using YARG.Localization;
using YARG.Menu.Main;
using YARG.Menu.Navigation;

namespace BardQuest.Mod;

internal static class MainMenuEntry
{
    private const string EntryName = "BardQuestEntry";
    private const string QuickplayKey = "Menu.Main.Options.Quickplay";
    private const string Label = "BARDQUEST";
    private static readonly Color BardQuestColor = new Color32(0x8A, 0x63, 0xD2, 0xFF); // BardQuest accent

    // Deliberate accessibility bypass: YARG exposes no public API for menu injection, so this mod
    // reads/writes a handful of its private fields (_localizationKey, _navigatables, _defaultColors,
    // _selectedVisual, _onClick) via reflection. Read-only or same-process UI wiring, no untrusted
    // input crosses this boundary — reviewed and accepted, not a code-fixable finding.
    private static readonly BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Ensure(BardQuestManager manager, MainMenu mainMenu)
    {
        try
        {
            NavigatableButton template = FindQuickplayButton(mainMenu);
            if (template == null) { ModLog.Warn("Quickplay template not found; entry skipped."); return; }

            Transform container = template.transform.parent;
            if (container.Find(EntryName) != null)
            {
                return; // idempotent (OnEnable fires repeatedly)
            }

            GameObject clone = UnityEngine.Object.Instantiate(template.gameObject, container);
            clone.name = EntryName;
            clone.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1);

            StripLocalization(clone);
            SetLabelAndColor(clone);
            FixSelectedVisual(clone);

            NavigatableButton button = clone.GetComponent<NavigatableButton>();
            DisablePersistentClicks(button);
            button.SetOnClickEvent(manager.OpenCanvas);

            NavigationGroup navGroup = container.GetComponent<NavigationGroup>();
            if (navGroup != null)
            {
                navGroup.AddNavigatable(button);
                // AddNavigatable appends to NavigationGroup's ordered _navigatables list, so nav order
                // would not match visual order. Re-place our entry immediately after Quickplay.
                FieldInfo navListField = typeof(NavigationGroup).GetField("_navigatables", Priv);
                if (navListField?.GetValue(navGroup) is List<NavigatableBehaviour> navList)
                {
                    _ = navList.Remove(button);
                    int quickplayIndex = navList.IndexOf(template);
                    if (quickplayIndex >= 0)
                    {
                        navList.Insert(quickplayIndex + 1, button);
                    }
                    else
                    {
                        navList.Add(button);
                    }
                }
            }
            ModLog.Info("Menu entry added.");
        }
        catch (Exception ex) { ModLog.Error("Menu entry injection failed: " + ex); }
    }

    private static NavigatableButton FindQuickplayButton(MainMenu mainMenu)
    {
        FieldInfo keyField = typeof(LocalizeText).GetField("_localizationKey", Priv);
        foreach (LocalizeText loc in mainMenu.GetComponentsInChildren<LocalizeText>(true))
        {
            if ((keyField?.GetValue(loc) as string) == QuickplayKey)
            {
                return loc.GetComponentInParent<NavigatableButton>();
            }
        }
        return null;
    }

    private static void StripLocalization(GameObject clone)
    {
        foreach (LocalizeText loc in clone.GetComponentsInChildren<LocalizeText>(true))
        {
            UnityEngine.Object.DestroyImmediate(loc);
        }
    }

    private static void SetLabelAndColor(GameObject clone)
    {
        foreach (TMP_Text text in clone.GetComponentsInChildren<TMP_Text>(true))
        {
            text.text = Label;
            text.color = BardQuestColor;
        }
        // Overwrite the colorizer's captured defaults so the deselected color is BardQuest's.
        foreach (NavigationTextColorizer colorizer in clone.GetComponentsInChildren<NavigationTextColorizer>(true))
        {
            if (typeof(NavigationTextColorizer).GetField("_defaultColors", Priv)?.GetValue(colorizer) is Color[] defaults)
            {
                for (int i = 0; i < defaults.Length; i++)
                {
                    defaults[i] = BardQuestColor;
                }
            }
        }
    }

    private static void FixSelectedVisual(GameObject clone)
    {
        NavigatableBehaviour beh = clone.GetComponent<NavigatableBehaviour>();
        var selected = typeof(NavigatableBehaviour).GetField("_selectedVisual", Priv)?.GetValue(beh) as GameObject;
        selected?.SetActive(false);
    }

    private static void DisablePersistentClicks(NavigatableButton button)
    {
        if (typeof(NavigatableButton).GetField("_onClick", Priv)?.GetValue(button) is not UnityEventBase onClick)
        {
            return;
        }

        for (int i = 0; i < onClick.GetPersistentEventCount(); i++)
        {
            onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
        }
    }
}
