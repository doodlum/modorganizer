"""Short-lived, owner-scoped native save previews; no changes to game extensions."""
import time


class SavePreviewLease:
    def __init__(self, create, profile, now=lambda: time.time() * 1000):
        self.create, self.profile, self.now = create, profile, now
        self.owner = self.filename = self.target = None
        self.deadline = 0
        self.dismiss = None

    def close(self):
        dismiss, self.dismiss = self.dismiss, None
        self.owner = self.filename = self.target = None
        self.deadline = 0
        if dismiss is not None:
            dismiss()

    def tick(self):
        if self.owner is not None and (self.now() >= self.deadline or self.profile() != self.target):
            self.close()

    def request(self, owner, filename, deadline, position=None):
        if not isinstance(owner, str) or not owner or len(owner) > 128:
            raise ValueError('Invalid save preview owner')
        self.tick()
        if filename is None:
            if owner == self.owner:
                self.close()
            return {'visible': False}
        if not isinstance(filename, str) or not filename:
            raise ValueError('Invalid save preview filename')
        if type(deadline) is not int or not self.now() < deadline <= self.now() + 2000:
            # A queued hover must not open after the pointer has moved away.
            return {'visible': False}
        if position is not None and (not isinstance(position, list) or len(position) != 2 or
                                     any(type(value) is not int for value in position)):
            raise ValueError("Invalid save preview position")
        target = self.profile()
        if (owner, filename, target) != (self.owner, self.filename, self.target):
            self.close()
            dismiss = self.create(filename, position)
            if self.now() >= deadline or self.profile() != target:
                if dismiss is not None:
                    dismiss()
                return {'visible': False}
            self.owner, self.filename, self.target = owner, filename, target
            self.dismiss = dismiss
        self.deadline = deadline
        return {'visible': self.dismiss is not None}


def create_preview(organizer, window, filename, position=None):
    import mobase
    from PyQt6.QtCore import QPoint, Qt
    from PyQt6.QtGui import QCursor, QGuiApplication
    from .saves import current_saves_directory
    if not window.isEnabled():
        return None
    feature = organizer.gameFeatures().gameFeature(mobase.SaveGameInfo)
    if feature is None:
        return None
    directory = current_saves_directory(organizer)
    saves = [save for save in organizer.managedGame().listSaves(directory)
             if directory.relativeFilePath(save.getFilepath()) == filename]
    if len(saves) != 1:
        return None
    widget = feature.getSaveGameWidget(window)
    if widget is None:
        return None
    qt = getattr(widget, '_widget', widget)
    if callable(qt):
        qt = qt()
    try:
        # Tool windows stay out of the taskbar. No focus/input is taken from the
        # frontend, and no MO2 main window needs to be shown to display this widget.
        qt.setObjectName("NexusFrontendSavePreview")
        qt.setWindowTitle("Save preview")
        qt.setWindowFlags(Qt.WindowType.Tool | Qt.WindowType.FramelessWindowHint |
                          Qt.WindowType.WindowStaysOnTopHint | Qt.WindowType.WindowDoesNotAcceptFocus |
                          Qt.WindowType.WindowTransparentForInput)
        qt.setAttribute(Qt.WidgetAttribute.WA_ShowWithoutActivating, True)
        qt.setAttribute(Qt.WidgetAttribute.WA_TransparentForMouseEvents, True)
        widget.setSave(saves[0])
        qt.adjustSize()
        # Wine may not receive cursor updates over the Linux frontend window.
        pos = QPoint(*position) if position is not None else QCursor.pos()
        screen = QGuiApplication.screenAt(pos) or QGuiApplication.primaryScreen()
        area = screen.availableGeometry()
        x = pos.x() + 5 if pos.x() + qt.width() + 5 <= area.right() else pos.x() - qt.width() - 2
        y = pos.y() + 20 if pos.y() + qt.height() + 20 <= area.bottom() else pos.y() - qt.height() - 10
        qt.move(max(area.left(), x), max(area.top(), y))
        qt.show()
    except Exception:
        qt.hide()
        qt.deleteLater()
        raise

    # Retain the MOBase wrapper as well as its underlying QWidget until dismissal.
    def dismiss(retained=widget):
        qt.hide()
        qt.deleteLater()
    return dismiss
