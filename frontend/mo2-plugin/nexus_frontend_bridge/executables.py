"""Use the host's configured executables and original process runner/USVFS."""
from PyQt6.QtWidgets import QComboBox


class Executables:
    def __init__(self, organizer, window):
        self.organizer = organizer
        self.window = window

    def selector(self):
        selector = self.window.findChild(QComboBox, 'executablesListBox')
        if selector is None:
            raise ValueError('MO2 executable selector is unavailable')
        return selector

    def snapshot(self):
        selector = self.selector()
        return [selector.itemText(index) for index in range(1, selector.count())]

    def launch(self, name):
        if not isinstance(name, str) or name not in self.snapshot():
            raise ValueError('Choose an executable configured in MO2')
        if not self.selector().isEnabled():
            raise ValueError('MO2 is busy')
        # Passing the configured title preserves arguments, Steam ID, overwrite
        # and injection settings. MO2 owns process setup and plugin callbacks.
        handle = self.organizer.startApplication(name)
        if not handle:
            raise ValueError('MO2 did not start the executable')
        # This also closes the process handle and refreshes MO2 when it exits.
        # The bridge's nested-poll guard prevents edits while the host is locked.
        completed, exit_code = self.organizer.waitForApplication(handle)
        return {'completed': completed, 'exitCode': exit_code}
