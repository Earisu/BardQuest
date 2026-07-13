// src/mod/BardQuest.Mod/UI/PlaceholderScreen.cs  (temporary — removed in Task 7)
extern alias yargpkg;

using UnityEngine.UIElements;

using YARG.Menu.Navigation;

using MenuAction = yargpkg::YARG.Core.Input.MenuAction;

namespace BardQuest.Mod.UI;

public sealed class PlaceholderScreen(BardQuestCanvas canvas) : IScreen
{
    public VisualElement Root { get; } = new Label("BARDQUEST — press Back")
    {
        style = { color = (UnityEngine.Color)BardTheme.Parchment, fontSize = 40, marginTop = 60, marginLeft = 60 },
    };

    public NavigationScheme BuildScheme() => new(
    [
        new(MenuAction.Red, "Menu.Common.Back", canvas.Pop),
    ], false);
}
