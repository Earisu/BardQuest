using System;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using YARG.Menu.Main;
using YARG.Menu.Navigation;

namespace BardQuest.Mod
{
    internal static class MainMenuEntry
    {
        private const string EntryName = "BardQuestEntry";
        private const string QuickplayKey = "Menu.Main.Options.Quickplay";
        private const string Label = "BARDQUEST";
        private static readonly Color BardQuestColor = new Color32(0x8A, 0x63, 0xD2, 0xFF); // BardQuest accent

        private static readonly BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;

        public static void Ensure(BardQuestManager manager, MainMenu mainMenu)
        {
            try
            {
                var template = FindQuickplayButton(mainMenu);
                if (template == null) { ModLog.Warn("Quickplay template not found; entry skipped."); return; }

                var container = template.transform.parent;
                if (container.Find(EntryName) != null) return; // idempotent (OnEnable fires repeatedly)

                var clone = UnityEngine.Object.Instantiate(template.gameObject, container);
                clone.name = EntryName;
                clone.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1);

                StripLocalization(clone);
                SetLabelAndColor(clone);
                FixSelectedVisual(clone);

                var button = clone.GetComponent<NavigatableButton>();
                DisablePersistentClicks(button);
                button.SetOnClickEvent(() => manager.OpenCanvas());

                container.GetComponent<NavigationGroup>()?.AddNavigatable(button);
                ModLog.Info("Menu entry added.");
            }
            catch (Exception ex) { ModLog.Error("Menu entry injection failed: " + ex); }
        }

        private static NavigatableButton FindQuickplayButton(MainMenu mainMenu)
        {
            var keyField = typeof(YARG.Localization.LocalizeText).GetField("_localizationKey", Priv);
            foreach (var loc in mainMenu.GetComponentsInChildren<YARG.Localization.LocalizeText>(true))
            {
                if ((keyField?.GetValue(loc) as string) == QuickplayKey)
                    return loc.GetComponentInParent<NavigatableButton>();
            }
            return null;
        }

        private static void StripLocalization(GameObject clone)
        {
            foreach (var loc in clone.GetComponentsInChildren<YARG.Localization.LocalizeText>(true))
                UnityEngine.Object.DestroyImmediate(loc);
        }

        private static void SetLabelAndColor(GameObject clone)
        {
            foreach (var text in clone.GetComponentsInChildren<TMP_Text>(true))
            {
                text.text = Label;
                text.color = BardQuestColor;
            }
            // Overwrite the colorizer's captured defaults so the deselected color is BardQuest's.
            foreach (var colorizer in clone.GetComponentsInChildren<NavigationTextColorizer>(true))
            {
                var texts = typeof(NavigationTextColorizer).GetField("_texts", Priv)?.GetValue(colorizer) as TMP_Text[];
                var defaults = typeof(NavigationTextColorizer).GetField("_defaultColors", Priv)?.GetValue(colorizer) as Color[];
                if (defaults != null)
                    for (int i = 0; i < defaults.Length; i++) defaults[i] = BardQuestColor;
            }
        }

        private static void FixSelectedVisual(GameObject clone)
        {
            var beh = clone.GetComponent<NavigatableBehaviour>();
            var selected = typeof(NavigatableBehaviour).GetField("_selectedVisual", Priv)?.GetValue(beh) as GameObject;
            if (selected != null) selected.SetActive(false);
        }

        private static void DisablePersistentClicks(NavigatableButton button)
        {
            var onClick = typeof(NavigatableButton).GetField("_onClick", Priv)?.GetValue(button) as UnityEventBase;
            if (onClick == null) return;
            for (int i = 0; i < onClick.GetPersistentEventCount(); i++)
                onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
        }
    }
}
