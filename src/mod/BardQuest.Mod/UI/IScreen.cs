using UnityEngine.UIElements;

using YARG.Menu.Navigation;

namespace BardQuest.Mod.UI;

// A BardQuest UI screen: a UITK subtree plus the navigation scheme that drives it (Up/Down/Confirm/Back).
public interface IScreen
{
    VisualElement Root { get; }

    string Title { get; }

    NavigationScheme BuildScheme();
}
