using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls;

namespace Mo2.Frontend;

// MO2's run row is measured with this frontend's sidebar open, because MO2 has no
// closed one.
//
// Collapsing the sidebar is this frontend's own addition: it moves the whole run
// row — the executables box, Run and the shortcut menu — out of the window and
// into a flyout behind two icons. That preference is remembered between runs, so a
// check that happened to start after someone had collapsed it reported MO2's
// startGroup, executablesListBox, startButton and linkButton all missing, which is
// true of the window as it stood and says nothing about whether the frontend draws
// them.
//
// A check that wants to compare against MO2 opens the sidebar first and puts it
// back as it found it.
internal static class Mo2SidebarState
{
    internal sealed class Held(StandardButton? toggle, bool restore) : IDisposable
    {
        internal bool WasCollapsed { get; } = restore;
        public void Dispose()
        {
            if (restore && toggle is not null) toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }
    }

    internal static async Task<Held> Open(Window window, Mo2LiveWorkspace live)
    {
        var toggle = window.GetVisualDescendants().OfType<StandardButton>().FirstOrDefault(x => x.Name == "ToggleSidebarButton");
        // Collapsed is read off what it did to the run row rather than off the
        // preference file, which is what the window is actually showing. Not off
        // IsEffectivelyVisible: a control moved into a closed flyout is in no visual
        // tree at all, and reports itself visible right up until it is asked for its
        // bounds, which are nothing.
        // Opened, not merely found collapsed: with no toggle to press nothing was
        // opened, and saying it had been would have explained away a run row that is
        // genuinely missing.
        var opened = toggle is not null && live.LaunchPanel is { } panel &&
            (panel.GetVisualRoot() is null || panel.Bounds.Width <= 0);
        if (opened) {
            toggle!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Task.Delay(600);
            window.UpdateLayout();
        }
        return new Held(toggle, opened);
    }
}
