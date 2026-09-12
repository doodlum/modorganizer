"""Adapt MO2 2.5.2's own profile controls; no replacement profile file writer."""
from pathlib import Path
from PyQt6.QtCore import QEventLoop, QTimer
from PyQt6.QtGui import QAction
from PyQt6.QtWidgets import QComboBox, QDialog, QListWidget, QPushButton


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

    def manage_profile(self, name, operation):
        buttons = {'copy': 'copyProfileButton', 'remove': 'removeProfileButton'}
        if operation not in buttons:
            raise ValueError('Unknown profile operation')
        if not isinstance(name, str) or not any(item['name'] == name for item in self.snapshot()):
            raise ValueError('The MO2 profile no longer exists')
        if operation == 'remove' and name == self.organizer.profileName():
            raise ValueError('Select a different profile in MO2 before deleting its active profile')
        failures = []
        existing = set(self.window.findChildren(QDialog))
        timer = QTimer()
        timer.setSingleShot(True)
        def activate_operation():
            dialog = next((child for child in self.window.findChildren(QDialog)
                           if child not in existing and child.objectName() == 'ProfilesDialog'), None)
            try:
                if dialog is None:
                    raise ValueError('This MO2 version does not expose its profile manager dialog')
                profiles = dialog.findChild(QListWidget, 'profilesList')
                button = dialog.findChild(QPushButton, buttons[operation])
                if profiles is None or button is None:
                    raise ValueError('This MO2 version does not expose the expected profile controls')
                item = next((profiles.item(i) for i in range(profiles.count())
                             if profiles.item(i).text() == name), None)
                if item is None:
                    raise ValueError('The MO2 profile no longer exists')
                profiles.setCurrentItem(item)
                if not button.isEnabled():
                    raise ValueError('MO2 does not allow this profile operation')
                # Original slots provide the name prompt, confirmation, copy rules,
                # save handling and removal. Never bypass their dialogs or write files.
                button.click()
            except Exception as error:
                failures.append(str(error))
                if dialog is not None:
                    dialog.reject()
        timer.timeout.connect(activate_operation)
        timer.start(0)
        try:
            self.manage()
        finally:
            timer.stop()
        if failures:
            raise ValueError(failures[0])

    def manage(self):
        if self.refreshing or not self.selector().isEnabled():
            raise ValueError('MO2 is busy; wait before managing profiles')
        action = self.window.findChild(QAction, 'actionAdd_Profile')
        if action is None or not action.isEnabled():
            raise ValueError('MO2 profile manager is unavailable')
        self.window.show()
        self.window.raise_()
        action.trigger()
