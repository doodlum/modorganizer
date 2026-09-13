"""Use the host's configured executables and original process runner/USVFS."""
from PyQt6.QtCore import QEventLoop, QMetaObject, QTimer, Qt
from PyQt6.QtWidgets import QComboBox, QPushButton


class Executables:
    def __init__(self, organizer, window):
        self.organizer = organizer
        self.window = window
        self._run = None
        organizer.onFinishedRun(self._finished)

    def _finished(self, binary, exit_code):
        state = self._run
        if state is None:
            return
        state['results'].append((str(binary), int(exit_code)))
        state['refreshes'] += 1
        def refreshed():
            state['refreshes'] -= 1
            if state['refreshes'] == 0 and state['loop'] is not None:
                state['loop'].quit()
        # Original Run refreshes asynchronously. Wait before publishing its models.
        self.organizer.onNextRefresh(refreshed, True)

    def selector(self):
        selector = self.window.findChild(QComboBox, 'executablesListBox')
        if selector is None:
            raise ValueError('MO2 executable selector is unavailable')
        return selector

    def snapshot(self):
        selector = self.selector()
        return [selector.itemText(index) for index in range(1, selector.count())]

    def icons(self):
        from .icons import icon_png
        selector = self.selector()
        return {selector.itemText(index): icon_png(selector.itemIcon(index)) for index in range(1, selector.count())}

    def launch(self, name):
        if not isinstance(name, str) or name not in self.snapshot():
            raise ValueError('Choose an executable configured in MO2')
        selector = self.selector()
        button = self.window.findChild(QPushButton, 'startButton')
        if self._run is not None or not selector.isEnabled() or button is None or not button.isEnabled():
            raise ValueError('MO2 is busy')
        state = {'results': [], 'refreshes': 0, 'loop': None}
        self._run = state
        try:
            selector.setCurrentIndex(selector.findText(name))
            # Keep the configured native runner through start, wait and afterRun.
            # The separate startApplication/waitForApplication API loses the
            # executable metadata before notifying onFinishedRun extensions.
            QMetaObject.invokeMethod(self.window, 'on_startButton_clicked', Qt.ConnectionType.DirectConnection)
            if state['refreshes']:
                loop = QEventLoop()
                state['loop'] = loop
                timer = QTimer()
                timer.setSingleShot(True)
                timer.timeout.connect(loop.quit)
                timer.start(30000)
                loop.exec()
                timer.stop()
                if state['refreshes']:
                    raise ValueError('MO2 is still refreshing after the run; wait and refresh before editing')
            results = state['results']
            # Native force-unlock can return STILL_ACTIVE (259) or an unset DWORD.
            # Neither establishes completion. Multiple callbacks
            # can belong to extension-started tools, so do not guess a game result.
            known = len(results) == 1 and bool(results[0][0]) and results[0][1] not in (-1, 0xffffffff, 259)
            return {'completed': known, 'exitCode': results[0][1] if known else None}
        finally:
            self._run = None
