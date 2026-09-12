"""Normal MO2 Python tool extension hosting the alternate frontend bridge."""
from pathlib import Path
import os
import mobase
from PyQt6.QtCore import QTimer, Qt
from PyQt6.QtGui import QIcon
from PyQt6.QtWidgets import QMessageBox
from .core import Bridge
from .credentials import NexusCredentials
from .downloads import Downloads
from .profiles import Profiles
from .executables import Executables
from .mod_actions import ModActions


class NexusFrontendBridge(mobase.IPluginTool):
    def __init__(self):
        super().__init__()
        self.parent = None
        self.timer = None
        self.bridge = None

    def init(self, organizer):
        states = mobase.PluginState
        active = getattr(states, 'ACTIVE', None)
        inactive = getattr(states, 'INACTIVE', None)
        if active is None:
            active = states.active
        if inactive is None:
            inactive = states.inactive
        self.bridge = Bridge(organizer, Path(organizer.pluginDataPath()) / 'frontend-bridge', {True: active, False: inactive}, NexusCredentials(), Downloads(organizer))
        self.timer = QTimer()
        self.timer.timeout.connect(self.bridge.poll)
        # Profile and plugin APIs are only safe once the host has finished setup.
        # Startup dialogs run nested event loops, so starting the timer in init
        # could otherwise expose an incompletely initialized OrganizerCore.
        def ready(window):
            if os.environ.get('MO2_FRONTEND_HOST') == '1':
                # Keep the original models and extension APIs alive without a
                # second main UI. Explicit installer/tool dialogs remain windows.
                window.setAttribute(Qt.WidgetAttribute.WA_DontShowOnScreen, True)
                window.hide()
            self.bridge.mod_actions = ModActions(organizer, window)
            self.bridge.downloads.window = window
            self.bridge.profiles = Profiles(organizer, window)
            self.bridge.executables = Executables(organizer, window)
            self.timer.start(100)
        organizer.onUserInterfaceInitialized(ready)
        return True

    def name(self): return 'Nexus Frontend Bridge'
    def localizedName(self): return self.name()
    def author(self): return 'Mod Organizer contributors'
    def description(self): return 'Connects the alternate frontend to the existing MO2 mod and plugin APIs.'
    def version(self): return mobase.VersionInfo(0, 1, 0)
    def settings(self): return []
    def displayName(self): return self.name()
    def tooltip(self): return 'Show the local frontend bridge endpoint'
    def icon(self): return QIcon()
    def setParentWidget(self, parent): self.parent = parent
    def display(self): QMessageBox.information(self.parent, self.name(), str(self.bridge.directory))


def createPlugin(): return NexusFrontendBridge()
