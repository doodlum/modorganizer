"""Read only the game INI paths supplied by the isolated MO2 verifier."""
import ctypes
from ctypes import wintypes
import hashlib
import json
from pathlib import Path
import sys

request = json.loads(Path(sys.argv[1]).read_text())
kernel = ctypes.windll.kernel32
kernel.GetModuleHandleW.argtypes = [wintypes.LPCWSTR]
kernel.GetModuleHandleW.restype = wintypes.HMODULE
result = {
    'usvfsLoaded': bool(kernel.GetModuleHandleW('usvfs_x64.dll') or kernel.GetModuleHandleW('usvfs_x86.dll')),
    'files': [{'name': item['name'], 'sha256': hashlib.sha256(Path(item['target']).read_bytes()).hexdigest()
               if Path(item['target']).is_file() else None} for item in request['files']],
}
Path(request['output']).write_text(json.dumps(result))
