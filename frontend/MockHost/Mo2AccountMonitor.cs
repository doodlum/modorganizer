namespace Mo2.Frontend;

// Called on the UI thread. Reads are serialized, and invalidated results never
// overwrite a newer login or a changed instance catalog.
internal sealed class Mo2AccountMonitor(Func<Task<Mo2LoginResult>> read, Action<Mo2LoginResult> apply) : IDisposable
{
    private long _version;
    private bool _reading, _pending, _loggingIn, _disposed;

    public bool BeginLogin()
    {
        if (_disposed || _loggingIn) return false;
        _loggingIn = true;
        _version++;
        return true;
    }

    public async Task EndLogin(Mo2LoginResult? result)
    {
        _loggingIn = false;
        if (_disposed) return;
        if (result is not null) apply(result);
        await Refresh();
    }

    public async Task Refresh()
    {
        if (_disposed) return;
        _version++;
        _pending = true;
        if (_reading || _loggingIn) return;
        _reading = true;
        try {
            while (_pending && !_disposed && !_loggingIn) {
                _pending = false;
                var version = _version;
                Mo2LoginResult result;
                try { result = await read(); }
                catch { result = new(0, 0, null, ["Nexus account status could not be confirmed."]); }
                if (!_disposed && !_loggingIn && version == _version) apply(result);
            }
        } finally { _reading = false; }
    }

    public void Dispose() { _disposed = true; _version++; }
}
