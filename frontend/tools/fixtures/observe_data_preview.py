"""Run under the target host's Wine prefix; observe only its Preview window.

Arguments: exact ModOrganizer.exe path, request marker, new JSON report,
optional exact dialog title (default Preview). An explicit title also creates a
report-stem.ready marker. Never reads editor contents or closes unrelated dialogs.
"""
import ctypes
from ctypes import wintypes
import json
from pathlib import Path
import sys
import time

user = ctypes.windll.user32
kernel = ctypes.windll.kernel32
kernel.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
kernel.OpenProcess.restype = wintypes.HANDLE
kernel.QueryFullProcessImageNameW.argtypes = [wintypes.HANDLE, wintypes.DWORD, wintypes.LPWSTR, ctypes.POINTER(wintypes.DWORD)]
kernel.CloseHandle.argtypes = [wintypes.HANDLE]
user.GetWindowThreadProcessId.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.DWORD)]
user.GetWindowTextW.argtypes = [wintypes.HWND, wintypes.LPWSTR, ctypes.c_int]
user.IsWindowVisible.argtypes = [wintypes.HWND]
user.IsWindow.argtypes = [wintypes.HWND]
user.PostMessageW.argtypes = [wintypes.HWND, wintypes.UINT, wintypes.WPARAM, wintypes.LPARAM]

executable, marker, destination = sys.argv[1:4]
window_title = sys.argv[4] if len(sys.argv) > 4 else "Preview"
executable = executable.replace('/', '\\').casefold()
marker, destination = Path(marker), Path(destination)
if marker.exists() or destination.exists():
    raise RuntimeError('Observer requires new marker and report paths')

def previews(title_filter=window_title):
    matches = []
    @ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
    def visit(window, _):
        title = ctypes.create_unicode_buffer(1024)
        user.GetWindowTextW(window, title, 1024)
        if (title_filter is not None and title.value != title_filter) or not user.IsWindowVisible(window):
            return True
        pid = wintypes.DWORD()
        user.GetWindowThreadProcessId(window, ctypes.byref(pid))
        handle = kernel.OpenProcess(0x1000, False, pid.value)
        if not handle:
            return True
        try:
            path = ctypes.create_unicode_buffer(4096)
            size = wintypes.DWORD(4096)
            if kernel.QueryFullProcessImageNameW(handle, 0, path, ctypes.byref(size)) and path.value.casefold() == executable:
                matches.append(window)
        finally:
            kernel.CloseHandle(handle)
        return True
    user.EnumWindows(visit, 0)
    return matches

if previews():
    raise RuntimeError('Close the existing ' + window_title + ' before starting this check')
if len(sys.argv) > 4:
    destination.with_suffix('.ready').write_text('ready')
deadline = time.monotonic() + 120
while not marker.exists():
    if time.monotonic() > deadline:
        raise RuntimeError('Frontend did not request a preview')
    time.sleep(.05)
while True:
    windows = previews()
    if len(windows) == 1:
        window = windows[0]
        only_dialog_visible = len(previews(None)) == 1
        # Allow the dialog to paint before closing it. This proves visibility,
        # not correct DDS rendering or OpenGL validity (separate checks).
        time.sleep(1)
        user.PostMessageW(window, 0x10, 0, 0)
        close_deadline = time.monotonic() + 10
        while user.IsWindow(window) and user.IsWindowVisible(window):
            if time.monotonic() > close_deadline:
                raise RuntimeError('Preview did not close')
            time.sleep(.05)
        temp = destination.with_suffix('.tmp')
        temp.write_text(json.dumps({'visible': True, 'closed': True, 'title': window_title,
                                    'mainWindowHidden': only_dialog_visible and not previews(None)}))
        temp.replace(destination)
        break
    if len(windows) > 1:
        raise RuntimeError('Ambiguous native preview windows')
    if time.monotonic() > deadline:
        raise RuntimeError('No visible native Preview window appeared')
    time.sleep(.05)
