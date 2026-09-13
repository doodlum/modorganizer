"""MO2's archive list and original file-preview extension, without another parser."""


class Archives:
    def __init__(self, organizer, window):
        self.organizer, self.window = organizer, window

    def read(self):
        from PyQt6.QtCore import Qt
        from PyQt6.QtWidgets import QTabWidget, QTreeWidget, QWidget
        tree = self.window.findChild(QTreeWidget, 'bsaList')
        if tree is None: raise ValueError('MO2 archive list is unavailable')
        tabs = self.window.findChild(QTabWidget, 'tabWidget')
        page = self.window.findChild(QWidget, 'bsaTab')
        if tabs is None or page is None or tabs.indexOf(page) < 0:
            raise ValueError('This MO2 game plugin does not provide an archive list')
        # MO2 populates this list lazily when its Archives tab is visited.
        previous = tabs.currentIndex()
        try:
            if tabs.currentWidget() == page: tabs.currentChanged.emit(previous)
            else: tabs.setCurrentWidget(page)
        finally:
            tabs.setCurrentIndex(previous)
        rows = []
        for row in range(tree.topLevelItemCount()):
            group = tree.topLevelItem(row)
            for index in range(group.childCount()):
                item = group.child(index)
                rows.append({'name': item.text(0), 'mod': group.text(0),
                             'active': item.checkState(0) == Qt.CheckState.Checked,
                             'canToggle': not item.isDisabled() and bool(item.flags() & Qt.ItemFlag.ItemIsUserCheckable)})
        return {'archives': rows}

    def preview(self, name):
        from PyQt6.QtCore import QCoreApplication, QEvent, QEventLoop, QItemSelectionModel, QModelIndex, QObject, QPersistentModelIndex, QTimer, Qt
        from PyQt6.QtWidgets import QApplication, QCheckBox, QLineEdit, QMenu, QTabWidget, QTreeView, QWidget
        if not isinstance(name, str) or not any(x['name'] == name for x in self.read()['archives']):
            raise ValueError('Archive no longer exists in the selected MO2 profile')
        if not self.window.isEnabled(): raise ValueError('Close MO2’s current dialog first')
        tabs = self.window.findChild(QTabWidget, 'tabWidget')
        page = self.window.findChild(QWidget, 'dataTab')
        tree = self.window.findChild(QTreeView, 'dataTree')
        if tabs is None or page is None or tree is None: raise ValueError('MO2 Data browser is unavailable')
        original_tab = tabs.currentIndex()
        search = self.window.findChild(QLineEdit, 'dataTabFilter')
        conflict_filter = self.window.findChild(QCheckBox, 'dataTabShowOnlyConflicts')
        old_search = search.text() if search else None
        old_conflicts = conflict_filter.isChecked() if conflict_filter else None
        visible = self.window.isVisible()
        previous_rows = [QPersistentModelIndex(x) for x in tree.selectionModel().selectedRows()]
        old_scroll = tree.verticalScrollBar().value()
        failures, invoked = [], []
        expected = QCoreApplication.translate('FileTree', '&Preview').replace('&', '')
        class Capture(QObject):
            armed = False
            def eventFilter(self, watched, event):
                if self.armed and isinstance(watched, QMenu) and event.type() == QEvent.Type.Polish:
                    self.armed = False
                    watched.setAttribute(Qt.WidgetAttribute.WA_DontShowOnScreen, True)
                    def run():
                        actions = [a for a in watched.actions() if a.text().replace('&', '') == expected and a.isEnabled()]
                        watched.close()
                        if len(actions) != 1:
                            failures.append('Enable a BSA/BA2 preview extension in MO2 to browse this archive.')
                            return
                        invoked.append(True); actions[0].trigger()
                    QTimer.singleShot(0, run)
                return False
        app = QApplication.instance(); capture = Capture(); app.installEventFilter(capture)
        try:
            self.window.show()  # Hidden-host suppression remains in force.
            if search: search.clear()
            if conflict_filter: conflict_filter.setChecked(False)
            tabs.setCurrentWidget(page)
            loop = QEventLoop(); timer = QTimer(); deadline = QTimer(); deadline.setSingleShot(True)
            found = []
            def locate():
                model = tree.model()
                if model.canFetchMore(QModelIndex()): model.fetchMore(QModelIndex())
                for row in range(model.rowCount()):
                    index = model.index(row, 0)
                    if str(index.data()).casefold() == name.casefold():
                        found.append(index); loop.quit(); return
            timer.timeout.connect(locate); deadline.timeout.connect(loop.quit)
            timer.start(100); deadline.start(10000); loop.exec(); timer.stop(); deadline.stop()
            if not found: raise ValueError('MO2 Data browser could not resolve this archive')
            index = found[0]; tree.setCurrentIndex(index); tree.scrollTo(index)
            capture.armed = True
            tree.customContextMenuRequested.emit(tree.visualRect(index).center())
        finally:
            app.removeEventFilter(capture)
            if search is not None: search.setText(old_search)
            if conflict_filter is not None: conflict_filter.setChecked(old_conflicts)
            tabs.setCurrentIndex(original_tab)
            tree.selectionModel().clearSelection()
            for old in previous_rows:
                if old.isValid(): tree.selectionModel().select(QModelIndex(old), QItemSelectionModel.SelectionFlag.Select | QItemSelectionModel.SelectionFlag.Rows)
            tree.verticalScrollBar().setValue(old_scroll)
            if not visible: self.window.hide()
        if failures: raise ValueError(failures[0])
        if not invoked: raise ValueError('MO2 did not open its archive preview action')
        return {'opened': True}
