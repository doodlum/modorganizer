using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace Mo2.Frontend;

// Reuses each list's existing filter field and callback; presentation and keyboard
// behavior stay the same on both pages without a second search/filter pipeline.
internal sealed class Mo2ToolbarSearch : Grid
{
    private readonly Control _field;
    private readonly TextBox _box;
    private readonly Button _button;
    private readonly string _label;

    internal Mo2ToolbarSearch(Control page, string name, Control field, TextBox box, string label)
    {
        Name = name;
        ColumnDefinitions = new ColumnDefinitions("Auto,*");
        VerticalAlignment = VerticalAlignment.Center;
        _field = field; _box = box; _label = label;
        Avalonia.Automation.AutomationProperties.SetName(box, label);
        box.Watermark = "Search";
        _button = Mo2QtWidgets.Icon(name + "Button", "Search (Ctrl+F)", "mdi-magnify", Toggle);
        Children.Add(_button);
        field.Width = double.NaN;
        field.MaxWidth = 188;
        field.Margin = new Thickness(4, 0, 0, 0);
        field.IsVisible = !string.IsNullOrEmpty(box.Text);
        SetColumn(field, 1); Children.Add(field);
        box.TextChanged += (_, _) => UpdatePresentation();
        UpdatePresentation();
        // The upstream Mods view also owns an unparented search control. Handle
        // the shortcut before it reaches that view so focus goes to the visible one.
        AttachedToVisualTree += (_, _) => page.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        DetachedFromVisualTree += (_, _) => page.RemoveHandler(KeyDownEvent, OnKeyDown);
    }

    private void UpdatePresentation()
    {
        var hasQuery = !string.IsNullOrEmpty(_box.Text);
        if (hasQuery) _button.Foreground = Brushes.DarkOrange;
        else _button.ClearValue(Button.ForegroundProperty);
        Avalonia.Automation.AutomationProperties.SetName(_button, _label);
        var state = _field.IsVisible ? "Expanded" : "Collapsed";
        Avalonia.Automation.AutomationProperties.SetHelpText(_button,
            state + ". Ctrl+F toggles search. Escape clears the query." + (hasQuery ? " Active query: " + _box.Text : ""));
        ToolTip.SetTip(_button, _label + " (Ctrl+F)" + (hasQuery ? ": " + _box.Text : ""));
    }

    private void Toggle()
    {
        if (_field.IsVisible && _field.IsKeyboardFocusWithin) _button.Focus();
        _field.IsVisible = !_field.IsVisible;
        if (_field.IsVisible) _box.Focus();
        UpdatePresentation();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control)) {
            Toggle(); e.Handled = true;
        } else if (e.Key == Key.Escape && _field.IsVisible) {
            _box.Text = "";
            if (_field.IsKeyboardFocusWithin) _button.Focus();
            _field.IsVisible = false;
            UpdatePresentation();
            e.Handled = true;
        }
    }
}
