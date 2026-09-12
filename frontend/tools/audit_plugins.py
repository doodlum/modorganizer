#!/usr/bin/env python3
"""Read-only inventory of an MO2 plugin tree; does not import or execute plugins."""
import argparse
import ast
import json
from pathlib import Path

WINDOWS_MODULES = {'winreg', '_winreg', 'win32api', 'win32con', 'win32com', 'win32gui', 'win32process', 'pythoncom', 'msvcrt', 'ctypes.wintypes'}
SKIP = {'libs', '__pycache__', '.git', 'node_modules'}


def inspect(path, root):
    entry = {'path': str(path.relative_to(root)), 'runtime_verified': False}
    if path.suffix.lower() == '.py':
        entry['format'] = 'python-source'
        try:
            tree = ast.parse(path.read_bytes(), filename=str(path))
            imports = set()
            calls = set()
            for node in ast.walk(tree):
                if isinstance(node, ast.Import):
                    imports.update(alias.name for alias in node.names)
                elif isinstance(node, ast.ImportFrom) and node.module:
                    imports.add(node.module)
                    imports.update(node.module + '.' + alias.name for alias in node.names)
                elif isinstance(node, ast.Call):
                    calls.add(ast.unparse(node.func))
            entry['imports'] = sorted(imports)
            entry['windows_indicators'] = sorted(
                {name for name in imports if any(name == module or name.startswith(module + '.') for module in WINDOWS_MODULES)}
                | {call for call in calls if call in {'os.startfile', 'ctypes.WinDLL', 'ctypes.windll.LoadLibrary'}})
            entry['qt_ui_imports'] = sorted(name for name in imports if name.startswith(('PyQt', 'PySide')))
            entry['mobase_imports'] = sorted(name for name in imports if name == 'mobase' or name.startswith('mobase.'))
        except (SyntaxError, UnicodeError) as error:
            entry['parse_error'] = str(error)
    else:
        with path.open('rb') as source:
            magic = source.read(4)
        entry['format'] = 'windows-pe' if magic[:2] == b'MZ' else 'linux-elf' if magic == b'\x7fELF' else 'unknown-binary'
    return entry


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('plugin_directory', type=Path)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    root = args.plugin_directory.resolve(strict=True)
    entries = [inspect(path, root) for path in sorted(root.rglob('*'))
               if path.is_file() and not any(part in SKIP for part in path.relative_to(root).parts)
               and path.suffix.lower() in {'.py', '.dll', '.pyd', '.so'}]
    result = {'schema_version': 1, 'root': str(root),
              'limitations': 'Static inventory only. No absence of indicators proves compatibility. Bundled libs are excluded; dynamic imports and platform assumptions need runtime tests.',
              'entries': entries}
    args.output.write_text(json.dumps(result, indent=2) + '\n')
    for kind in sorted({entry['format'] for entry in entries}):
        print(f"{kind}: {sum(entry['format'] == kind for entry in entries)}")
    print(f"Python files with explicit Windows indicators: {sum(bool(entry.get('windows_indicators')) for entry in entries)}")
    print(f"Inventory: {args.output}")


if __name__ == '__main__':
    main()
