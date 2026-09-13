"""Temporary native launch/callback check for the isolated FNV launcher only."""
import ctypes
from ctypes import wintypes
import json
from pathlib import Path
import mobase
from PyQt6.QtCore import QTimer, Qt
from PyQt6.QtGui import QIcon
from PyQt6.QtWidgets import QApplication, QComboBox, QPushButton


class LaunchCallbacks(mobase.IPluginTool):
    def init(self, organizer):
        self.root = Path(__file__).parent.parent
        if self.root.name != 'mo2-fnv-host': return False
        self.artifacts = self.root.parent
        self.report = self.artifacts / 'native-launch-callbacks.json'
        self.events = {}
        organizer.onAboutToRun(self.about)
        organizer.onFinishedRun(self.finished)
        organizer.onUserInterfaceInitialized(self.ready)
        return True

    def ready(self, window):
        self.window = window
        self.selector = window.findChild(QComboBox, 'executablesListBox')
        self.original_selection = self.selector.currentText()

    def save(self): self.report.write_text(json.dumps(self.events))

    def about(self, binary, *args):
        if Path(binary).name.casefold() == 'falloutnvlauncher.exe':
            self.binary = str(binary)
            mode = (self.artifacts / 'native-launch-mode.txt').read_text().strip()
            self.events = {'mode': mode, 'startedName': Path(binary).name,
                           'mainSuppressed': self.window.testAttribute(Qt.WidgetAttribute.WA_DontShowOnScreen)}
            self.save()
            QTimer.singleShot(5000, self.close_launcher)
            if mode == 'unlock': QTimer.singleShot(1000, self.unlock)
        return True

    def finished(self, binary, exit_code):
        if self.events and 'finishedName' not in self.events:
            self.events.update(finishedName=Path(binary).name if binary else '', exitCode=int(exit_code))
            self.save()
            QTimer.singleShot(0, lambda: self.selector.setCurrentText(self.original_selection))

    def unlock(self):
        buttons = [w for w in QApplication.allWidgets() if isinstance(w, QPushButton) and w.text() == 'Unlock' and w.isEnabled()]
        self.events['unlockButtons'] = len(buttons)
        if len(buttons) == 1:
            self.events['unlocked'] = True
            buttons[0].click()
        self.save()

    def close_launcher(self):
        user = ctypes.windll.user32
        kernel = ctypes.windll.kernel32
        kernel.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
        kernel.OpenProcess.restype = wintypes.HANDLE
        kernel.QueryFullProcessImageNameW.argtypes = [wintypes.HANDLE, wintypes.DWORD, wintypes.LPWSTR, ctypes.POINTER(wintypes.DWORD)]
        kernel.CloseHandle.argtypes = [wintypes.HANDLE]
        user.GetWindowThreadProcessId.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.DWORD)]
        user.PostMessageW.argtypes = [wintypes.HWND, wintypes.UINT, wintypes.WPARAM, wintypes.LPARAM]
        closed = []
        expected = self.binary.replace('/', '\\').casefold()
        @ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
        def visit(hwnd, _):
            pid = wintypes.DWORD()
            user.GetWindowThreadProcessId(hwnd, ctypes.byref(pid))
            handle = kernel.OpenProcess(0x1000, False, pid.value)
            if not handle: return True
            try:
                path = ctypes.create_unicode_buffer(4096)
                size = wintypes.DWORD(4096)
                if kernel.QueryFullProcessImageNameW(handle, 0, path, ctypes.byref(size)) and path.value.casefold() == expected:
                    title = ctypes.create_unicode_buffer(512)
                    user.GetWindowTextW(hwnd, title, 512)
                    if title.value == 'Fallout: New Vegas':
                        # Bethesda's custom menu ignores WM_CLOSE. Click its Exit
                        # item in client coordinates, only in our own launcher.
                        rect = wintypes.RECT()
                        user.GetClientRect(hwnd, ctypes.byref(rect))
                        point = int(rect.right * .935) | (int(rect.bottom * .755) << 16)
                        user.PostMessageW(hwnd, 0x201, 1, point)
                        user.PostMessageW(hwnd, 0x202, 0, point)
                        closed.append(pid.value)
            finally: kernel.CloseHandle(handle)
            return True
        user.EnumWindows(visit, 0)
        self.events['closeRequested'] = bool(closed)
        self.save()

    def name(self): return 'Frontend native launch callback verification'
    def localizedName(self): return self.name()
    def author(self): return 'MO2 frontend verification'
    def description(self): return 'Temporary original launcher callback and unlock check.'
    def version(self): return mobase.VersionInfo(1, 0, 0)
    def settings(self): return []
    def displayName(self): return self.name()
    def tooltip(self): return self.description()
    def icon(self): return QIcon()
    def setParentWidget(self, parent): pass
    def display(self): pass


def createPlugin(): return LaunchCallbacks()
