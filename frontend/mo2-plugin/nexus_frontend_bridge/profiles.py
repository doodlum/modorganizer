"""Adapt MO2 2.5.2's own profile controls; no replacement profile file writer."""
from pathlib import Path
from PyQt6.QtCore import QEventLoop, QTimer
from PyQt6.QtGui import QAction
from PyQt6.QtWidgets import QComboBox


class Profiles:
    def __init__(self, organizer, window):
        self.organizer = organizer
        self.window = window
        self.refreshing = False

    def selector(self):
        selector = self.window.findChild(QComboBox, 'profileBox')
        if selector is None:
            raise ValueError('This MO2 version does not expose the expected profile selector')
        return selector

    def snapshot(self):
        selector = self.selector()
        parent = Path(self.organizer.profilePath()).parent
        # Index zero is MO2's <Manage...> action, not a profile.
        return [{'name': selector.itemText(index), 'path': str(parent / selector.itemText(index))}
                for index in range(1, selector.count())]

    def select(self, name):
        selector = self.selector()
        if not selector.isEnabled() or self.refreshing:
            raise ValueError('MO2 is busy; wait before switching profiles')
        index = selector.findText(name)
        if index <= 0:
            raise ValueError('The MO2 profile no longer exists')
        if self.organizer.profileName() == name:
            return
        loop = QEventLoop()
        self.refreshing = True
        def refreshed():
            self.refreshing = False
            loop.quit()
        self.organizer.onNextRefresh(refreshed, False)
        selector.setCurrentIndex(index)
        if self.refreshing:
            timer = QTimer()
            timer.setSingleShot(True)
            timer.timeout.connect(loop.quit)
            timer.start(30000)
            loop.exec()
            timer.stop()
        if self.refreshing:
            raise ValueError('MO2 is still refreshing the profile; wait and refresh before editing')
        if self.organizer.profileName() != name:
            raise ValueError('MO2 did not select the requested profile')

    def manage(self):
        if self.refreshing or not self.selector().isEnabled():
            raise ValueError('MO2 is busy; wait before managing profiles')
        action = self.window.findChild(QAction, 'actionAdd_Profile')
        if action is None or not action.isEnabled():
            raise ValueError('MO2 profile manager is unavailable')
        self.window.show()
        self.window.raise_()
        action.trigger()
