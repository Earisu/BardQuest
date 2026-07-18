using UnityEngine.UIElements;

using YARG.Menu.Navigation;

namespace BardQuest.Mod.UI;

// A BardQuest UI screen: a UITK subtree plus the navigation scheme that drives it (Up/Down/Confirm/Back).
public interface IScreen
{
    VisualElement Root { get; }

    string Title { get; }

    // True for screens that use the shared app header (logo + title + back button). The Hub returns false —
    // it renders its own top band (small logo + journey path) instead, so the canvas hides the app header
    // while the Hub is on top.
    bool ShowsAppHeader { get; }

    NavigationScheme BuildScheme();

    // Called by the canvas whenever this screen leaves the stack — via the Red/Back nav action OR a bulk
    // teardown. Screens release here anything that would outlive them (e.g. a running song preview that would
    // otherwise loop over the main menu).
    void OnPop();
}
