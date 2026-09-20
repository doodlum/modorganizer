using Avalonia.Controls;
using Avalonia.Input;

namespace Mo2.Frontend;

// MO2 cycles inactive -> included -> inverted, and reverses on right-click or
// Shift+Space. Avalonia's nullable checked state supplies the same three glyphs.
internal sealed class Mo2CategoryToggle : CheckBox
{
    protected override Type StyleKeyOverride => typeof(CheckBox);
    private bool reverseSpace;
    public Mo2CategoryToggle() { IsThreeState = true; LostFocus += (_, _) => reverseSpace = false; }
    private void Reverse() => IsChecked = IsChecked switch { false => null, null => true, true => false };
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Space && e.KeyModifiers.HasFlag(KeyModifiers.Shift)) {
            reverseSpace = true; Reverse(); e.Handled = true; return;
        }
        base.OnKeyDown(e);
    }
    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.Key == Key.Space && reverseSpace) { reverseSpace = false; e.Handled = true; return; }
        base.OnKeyUp(e);
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed) {
            Focus(); Reverse(); e.Handled = true; return;
        }
        base.OnPointerPressed(e);
    }
}
