"""Read the original Saves tab, retaining native save-path and game-parser rules."""


def read_saves(window):
    from PyQt6.QtCore import QSignalBlocker
    from PyQt6.QtWidgets import QTabWidget, QTreeWidget, QWidget
    tabs = window.findChild(QTabWidget, 'tabWidget')
    page = window.findChild(QWidget, 'savesTab')
    tree = window.findChild(QTreeWidget, 'savegameList')
    if tabs is None or page is None or tree is None or tabs.indexOf(page) < 0:
        raise ValueError('This MO2 game plugin does not provide a Saves tab')
    previous = tabs.currentIndex()
    selected = {item.text(1) for item in tree.selectedItems()}
    scroll = tree.verticalScrollBar().value()
    try:
        if tabs.currentWidget() == page: tabs.currentChanged.emit(previous)
        else: tabs.setCurrentWidget(page)
        return {'saves': [{'name': tree.topLevelItem(i).text(0),
                           'file': tree.topLevelItem(i).text(1)}
                          for i in range(tree.topLevelItemCount())]}
    finally:
        # Restoring the Plugins tab normally reloads its list from disk. During
        # an automatic Saves refresh that can discard a just-changed activation
        # before MO2's delayed list writer has flushed it. Restore presentation
        # only; the requested Saves refresh above still uses MO2's original slot.
        with QSignalBlocker(tabs):
            tabs.setCurrentIndex(previous)
        for i in range(tree.topLevelItemCount()):
            item = tree.topLevelItem(i)
            item.setSelected(item.text(1) in selected)
        tree.verticalScrollBar().setValue(scroll)


def save_action(window, filename, operation):
    from PyQt6.QtCore import QCoreApplication, QEvent, QObject, QTimer, Qt
    from PyQt6.QtWidgets import QApplication, QMenu, QTabWidget, QTreeWidget, QWidget
    if operation not in ('repair', 'delete'):
        raise ValueError('Unknown save action')
    if not window.isEnabled(): raise ValueError('Close MO2’s current dialog first')
    if not isinstance(filename, str) or sum(row['file'] == filename for row in read_saves(window)['saves']) != 1:
        raise ValueError('Save no longer exists; refresh Saves before continuing')
    tabs = window.findChild(QTabWidget, 'tabWidget')
    page = window.findChild(QWidget, 'savesTab')
    tree = window.findChild(QTreeWidget, 'savegameList')
    previous = tabs.currentIndex(); visible = window.isVisible()
    selected = {item.text(1) for item in tree.selectedItems()}
    scroll = tree.verticalScrollBar().value()
    expected = (QCoreApplication.translate('SavesTab', 'Delete %n save(s)', None, 1)
                if operation == 'delete' else QCoreApplication.translate('SavesTab', 'Fix enabled mods...')).replace('&', '')
    invoked, failures = [], []
    class Capture(QObject):
        armed = True
        def eventFilter(self, watched, event):
            if self.armed and isinstance(watched, QMenu) and event.type() == QEvent.Type.Polish:
                self.armed = False
                watched.setAttribute(Qt.WidgetAttribute.WA_DontShowOnScreen, True)
                def run():
                    actions = [action for action in watched.actions() if action.text().replace('&', '') == expected and action.isEnabled()]
                    watched.close()
                    if len(actions) != 1:
                        failures.append('MO2 reports no repairable missing mods for this save.' if operation == 'repair' else 'MO2 save deletion is unavailable')
                        return
                    invoked.append(True); actions[0].trigger()
                QTimer.singleShot(0, run)
            return False
    app = QApplication.instance(); capture = Capture(); app.installEventFilter(capture)
    try:
        window.show()  # Hidden-host suppression keeps the main window off screen.
        tabs.setCurrentWidget(page)
        matches = [tree.topLevelItem(i) for i in range(tree.topLevelItemCount()) if tree.topLevelItem(i).text(1) == filename]
        if len(matches) != 1: raise ValueError('Save changed; refresh Saves before continuing')
        tree.clearSelection(); tree.setCurrentItem(matches[0]); matches[0].setSelected(True)
        tree.scrollToItem(matches[0]); tree.doItemsLayout()
        tree.customContextMenuRequested.emit(tree.visualItemRect(matches[0]).center())
    finally:
        app.removeEventFilter(capture)
        tabs.setCurrentIndex(previous)
        tree.clearSelection()
        for i in range(tree.topLevelItemCount()):
            item = tree.topLevelItem(i); item.setSelected(item.text(1) in selected)
        tree.verticalScrollBar().setValue(scroll)
        if not visible: window.hide()
    if failures: raise ValueError(failures[0])
    if not invoked: raise ValueError('MO2 did not open the save action')
    return {'opened': True}


def save_directory(profile, game, supports_local, read_ini, make_dir):
    # Keep this aligned with SavesTab::currentSavesDir and Profile::savePath.
    if profile.localSavesEnabled():
        return make_dir(make_dir(profile.absolutePath()).absoluteFilePath('saves'))
    inis = game.iniFiles()
    if inis and supports_local:
        override = read_ini(profile.absoluteIniFilePath(inis[0]))
        if override:
            return make_dir(game.documentsDirectory().absoluteFilePath(override))
    return game.savesDirectory()


def current_saves_directory(organizer):
    """The folder MO2's own Saves tab is reading, by MO2's own rules.

    The saves a profile lists are named relative to it, so the frontend is told
    where they are and shows one on this desktop rather than asking MO2 to open a
    folder inside its Windows prefix.
    """
    import ctypes
    import mobase
    from PyQt6.QtCore import QDir
    def read_ini(path):
        reader = ctypes.windll.kernel32.GetPrivateProfileStringW
        reader.argtypes = [ctypes.c_wchar_p, ctypes.c_wchar_p, ctypes.c_wchar_p, ctypes.c_wchar_p, ctypes.c_uint32, ctypes.c_wchar_p]
        reader.restype = ctypes.c_uint32
        value = ctypes.create_unicode_buffer(260)
        reader('General', 'SLocalSavePath', '', value, len(value), path)
        return value.value
    return save_directory(organizer.profile(), organizer.managedGame(),
        organizer.gameFeatures().gameFeature(mobase.LocalSavegames) is not None, read_ini, QDir)


def preview_save_details(organizer, window, filename):
    import mobase
    from PyQt6.QtCore import Qt
    from PyQt6.QtWidgets import QDialog, QDialogButtonBox, QScrollArea, QVBoxLayout
    if not window.isEnabled(): raise ValueError('Close MO2’s current dialog first')
    if not isinstance(filename, str) or sum(row['file'] == filename for row in read_saves(window)['saves']) != 1:
        raise ValueError('Save no longer exists; refresh Saves before continuing')
    feature = organizer.gameFeatures().gameFeature(mobase.SaveGameInfo)
    if feature is None: raise ValueError('This game extension does not provide save details')
    directory = current_saves_directory(organizer)
    matches = [save for save in organizer.managedGame().listSaves(directory)
               if directory.relativeFilePath(save.getFilepath()) == filename]
    if len(matches) != 1: raise ValueError('Save changed; refresh Saves before viewing details')
    dialog = QDialog(window); dialog.setWindowTitle('Save details')
    try:
        layout = QVBoxLayout(dialog)
        scroll = QScrollArea(dialog); scroll.setWidgetResizable(True)
        widget = feature.getSaveGameWidget(scroll)
        if widget is None: raise ValueError('This game extension does not provide a save details widget')
        widget.setWindowFlags(Qt.WindowType.Widget)
        widget.setSave(matches[0])
        # plugin_python's ISaveGameInfoWidget delegates QWidget through _widget.
        # Pass that SIP object to Qt containers, retaining the MOBase wrapper alive.
        qt_widget = getattr(widget, '_widget', widget)
        if callable(qt_widget): qt_widget = qt_widget()
        scroll.setWidget(qt_widget); layout.addWidget(scroll)
        buttons = QDialogButtonBox(QDialogButtonBox.StandardButton.Close, dialog)
        buttons.rejected.connect(dialog.reject); layout.addWidget(buttons)
        area = dialog.screen().availableGeometry()
        hint = widget.sizeHint()
        dialog.resize(min(max(hint.width() + 48, 440), area.width() - 40), min(max(hint.height() + 96, 400), area.height() - 60))
        dialog.exec()
    finally:
        dialog.deleteLater()
    return {'opened': True}
