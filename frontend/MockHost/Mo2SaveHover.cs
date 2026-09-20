using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Mo2.Frontend;

// The game extension owns the Qt widget. Each hover gets a short native lease,
// renewed only while this frontend is active and the same row is under the pointer.
internal sealed class Mo2SaveHover : IDisposable
{
    private sealed record Preview(Mo2ProfileTarget Target, string File, string Owner, PixelPoint Position);
    private readonly Control _view;
    private readonly TreeDataGrid _table;
    private readonly Mo2LiveProfile _profile;
    private readonly Window? _window;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private Point _position;
    private bool _hasPosition, _disposed, _sending, _renew;
    private Preview? _desired, _sent;
    private string? _failedOwner;

    internal Mo2SaveHover(Control view, TreeDataGrid table, Mo2LiveProfile profile)
    {
        _view = view; _table = table; _profile = profile;
        _window = TopLevel.GetTopLevel(view) as Window;
        table.PointerMoved += Moved;
        table.PointerExited += Exited;
        if (_window is not null) _window.Deactivated += Deactivated;
        _timer.Tick += Tick;
        _timer.Start();
    }

    private void Moved(object? sender, PointerEventArgs args)
    { _position = args.GetPosition(_table); _hasPosition = true; Update(); }
    private void Exited(object? sender, PointerEventArgs args)
    { _hasPosition = false; Update(); }
    private void Deactivated(object? sender, EventArgs args)
    { _hasPosition = false; Update(); }
    private void Tick(object? sender, EventArgs args) => Update(renew: true);

    private static void Trace(string message) {
        if (Environment.GetEnvironmentVariable("MO2_VERIFY_SAVE_HOVER") is not null)
            Console.WriteLine("CHECK save hover: " + message);
    }

    private void Update(bool renew = false)
    {
        var before = _desired;
        string? file = null;
        if (!_disposed && _hasPosition && _window?.IsActive == true && _view.IsEffectivelyVisible &&
            _table.IsPointerOver && _profile.CanPreviewSaves && _profile.CanChangeOriginalUi &&
            _table.ContextMenu?.IsOpen != true) {
            var control = _table.InputHitTest(_position) as Control;
            var row = control as TreeDataGridRow ?? control?.GetVisualAncestors().OfType<TreeDataGridRow>().FirstOrDefault();
            if (row is { RowIndex: >= 0 } && row.RowIndex < (_table.Rows?.Count ?? 0) &&
                _table.Rows![row.RowIndex].Model is Mo2Save save) file = save.File;
        }
        if (file is null) _desired = null;
        else if (_desired?.File != file || _desired.Target != _profile.CurrentTarget)
            _desired = new(_profile.CurrentTarget, file, Guid.NewGuid().ToString(), _table.PointToScreen(_position));
        if (_desired != before) Trace($"desired={_desired?.Owner ?? "none"}; active={_window?.IsActive}; over={_table.IsPointerOver}; visible={_view.IsEffectivelyVisible}; position={_position}; ready={_profile.CanChangeOriginalUi}");
        _renew |= renew;
        _ = Pump();
    }

    private static Task<System.Text.Json.JsonElement> Send(Preview preview, bool show) =>
        new Mo2BridgeClient(preview.Target.Endpoint).SendAsync("savePreview", new() {
            ["profilePath"] = preview.Target.ProfilePath, ["owner"] = preview.Owner,
            ["file"] = show ? preview.File : null,
            ["expires"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 1500,
            ["position"] = new[] { preview.Position.X, preview.Position.Y },
        });

    private async Task Pump()
    {
        if (_sending) return;
        _sending = true;
        try {
            while (_renew || _sent != _desired) {
                _renew = false;
                if (_sent is { } previous && previous != _desired) {
                    _sent = null;
                    try { await Send(previous, false); }
                    catch (Exception error) { Console.Error.WriteLine("Save hover dismissal: " + error.Message); }
                }
                if (_desired is not { } next || next.Owner == _failedOwner) break;
                _sent = next;
                try {
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    var result = await Send(next, true);
                    Trace($"show owner={next.Owner}; visible={result.GetProperty("visible").GetBoolean()}; elapsedMs={watch.ElapsedMilliseconds}");
                    if (!result.GetProperty("visible").GetBoolean()) _failedOwner = next.Owner;
                } catch (Exception error) {
                    _failedOwner = next.Owner;
                    Console.Error.WriteLine("Save hover preview: " + error.Message);
                }
            }
        } finally { _sending = false; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _timer.Stop();
        _table.PointerMoved -= Moved; _table.PointerExited -= Exited;
        if (_window is not null) _window.Deactivated -= Deactivated;
        _desired = null; _renew = true; _ = Pump();
    }
}
