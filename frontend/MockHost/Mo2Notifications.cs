using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using NexusMods.App.UI.Controls;
using NexusMods.UI.Sdk.Icons;

namespace Mo2.Frontend;

internal sealed class Mo2NotificationState
{
    internal record Warning(string Title, string Details);
    private readonly Dictionary<Mo2ProfileTarget, HashSet<Warning>> _read = new();
    private readonly Dictionary<Mo2ProfileTarget, HashSet<Warning>> _known = new();
    internal Mo2ProfileTarget Target { get; private set; }
    internal Warning[] Warnings { get; private set; } = [];
    internal int Unread => Warnings.Count(w => !_read.GetValueOrDefault(Target, []).Contains(w));
    internal int Update(Mo2ProfileTarget target, IEnumerable<Warning> warnings) {
        Target = target; Warnings = warnings.Distinct().ToArray();
        var current = Warnings.ToHashSet();
        var known = _known.GetValueOrDefault(target, []);
        var added = current.Except(known).Count();
        _known[target] = current;
        if (_read.TryGetValue(target, out var read)) read.IntersectWith(current);
        return added;
    }
    internal void Select(Mo2ProfileTarget target) { Target = target; Warnings = _known.GetValueOrDefault(target, []).ToArray(); }
    internal void MarkRead() => _read[Target] = Warnings.ToHashSet();
}

internal sealed class Mo2Notifications : IDisposable
{
    private readonly Mo2LiveWorkspace _shell;
    private readonly Mo2NotificationState _state = new();
    private readonly StackPanel _items = new() { Spacing = 8 };
    private readonly TextBlock _badge = new() { FontSize = 10, Foreground = Brushes.Black, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Border _badgeBorder;
    private readonly TextBlock _status = new() { Opacity = .65, TextWrapping = TextWrapping.Wrap };
    private readonly Flyout _flyout;
    private readonly ScrollViewer _scroll;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(10) };
    private WindowNotificationManager? _toasts;
    private bool _reading, _disposed;
    public Button Button { get; }
    public Mo2Notifications(Mo2LiveWorkspace shell)
    {
        _shell = shell;
        var icon = new UnifiedIcon { Value = new ProjektankerIcon("mdi-bell-outline"), Size = 20 };
        _badgeBorder = new Border { Background = Brush.Parse("#FB923C"), CornerRadius = new CornerRadius(8), MinWidth = 14, Height = 14,
            Padding = new Thickness(2,0), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Child = _badge, IsVisible = false };
        var buttonContent = new Grid(); buttonContent.Children.Add(icon); buttonContent.Children.Add(_badgeBorder);
        Button = new Button { Name = "Mo2NotificationsButton", Width = 36, Height = 32, Padding = new Thickness(4), Background = Brushes.Transparent, Content = buttonContent };
        Avalonia.Automation.AutomationProperties.SetName(Button, "Notifications");
        var heading = new DockPanel { Margin = new Thickness(0,0,0,8) };
        var read = new Button { Content = "Mark all read", FontSize = 12, Padding = new Thickness(6,3) };
        read.Click += (_,_) => { _state.MarkRead(); Render(); };
        DockPanel.SetDock(read, Dock.Right); heading.Children.Add(read);
        heading.Children.Add(new TextBlock { Text = "Notifications", FontSize = 18, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        var content = new DockPanel { Width = 360 };
        DockPanel.SetDock(heading, Dock.Top); content.Children.Add(heading);
        DockPanel.SetDock(_status, Dock.Top); content.Children.Add(_status);
        _scroll = new ScrollViewer { Content = _items, MaxHeight = 380, Margin = new Thickness(0,8,0,0) }; content.Children.Add(_scroll);
        _flyout = new Flyout { Content = content, Placement = PlacementMode.BottomEdgeAlignedRight };
        Button.Flyout = _flyout;
        _flyout.Opening += (_,_) => {
            if (TopLevel.GetTopLevel(Button) is { } window) {
                content.Width = Math.Max(200, Math.Min(360, window.Bounds.Width - 48));
                _scroll.MaxHeight = Math.Max(100, Math.Min(380, window.Bounds.Height - 150));
            }
            Render(); _ = Refresh();
        };
        shell.Profile.Changed += Changed;
        _timer.Tick += (_,_) => _ = Refresh();
        _timer.Start(); Changed();
    }
    private void Changed() {
        if (_state.Target != _shell.Profile.CurrentTarget) {
            _state.Select(_shell.Profile.CurrentTarget); _status.Text = "Checking MO2 warnings…"; Render(); _ = Refresh();
        }
    }
    private async Task Refresh() {
        if (_disposed || _reading || !_shell.Profile.CanChangeOriginalUi || _shell.Profile.ProfilePath.Length == 0) return;
        _reading = true; var target = _shell.Profile.CurrentTarget;
        try {
            var reports = await _shell.Profile.ReadHealth();
            if (_disposed || target != _shell.Profile.CurrentTarget) return;
            var added = _state.Update(target, reports.Select(p => new Mo2NotificationState.Warning(p.Title, p.Details)));
            _status.Text = reports.Length == 0 ? "No MO2 warnings for this profile." : $"{_state.Warnings.Length} MO2 warning{(_state.Warnings.Length == 1 ? "" : "s")} · {_shell.Profile.GameName}";
            Render();
            if (added > 0 && TopLevel.GetTopLevel(Button) is Window window) {
                _toasts ??= new WindowNotificationManager(window) { Position = NotificationPosition.BottomRight, MaxItems = 2 };
                _toasts.Show(new Notification("MO2 warnings", $"{added} new warning{(added == 1 ? "" : "s")}. Open Notifications for details.", NotificationType.Warning, TimeSpan.FromSeconds(6), onClick: () => _flyout.ShowAt(Button)));
            }
        } catch (Exception) {
            if (!_disposed && target == _shell.Profile.CurrentTarget) { _status.Text = "MO2 warnings could not be refreshed. Retrying automatically."; Render(); }
        } finally {
            _reading = false;
            if (!_disposed && target != _shell.Profile.CurrentTarget) _ = Refresh();
        }
    }
    private void Render() {
        _badge.Text = _state.Unread > 9 ? "9+" : _state.Unread.ToString(); _badgeBorder.IsVisible = _state.Unread > 0;
        ToolTip.SetTip(Button, $"Notifications · {_state.Warnings.Length} MO2 warnings, {_state.Unread} unread");
        _items.Children.Clear();
        foreach (var warning in _state.Warnings) {
            var target = _state.Target;
            var body = new StackPanel { Spacing = 8 };
            body.Children.Add(new TextBlock { Text = warning.Details, TextWrapping = TextWrapping.Wrap, Opacity = .8 });
            var details = new Button { Content = "View Health Check", HorizontalAlignment = HorizontalAlignment.Left };
            details.Click += (_,_) => { if (target == _shell.Profile.CurrentTarget) { _state.MarkRead(); _flyout.Hide(); Render(); _shell.OpenHealth(); } };
            body.Children.Add(details);
            var title = new TextBlock { Text = warning.Title, TextWrapping = TextWrapping.Wrap, MaxWidth = 280, FontWeight = FontWeight.SemiBold };
            _items.Children.Add(new Border { Background = Brush.Parse("#29292E"), CornerRadius = new CornerRadius(6), Padding = new Thickness(8),
                Child = new Expander { Header = title, Content = body } });
        }
    }
    public void Dispose() { _disposed = true; _timer.Stop(); _shell.Profile.Changed -= Changed; }
}
