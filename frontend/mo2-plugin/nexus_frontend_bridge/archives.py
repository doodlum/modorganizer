"""MO2's archive list and original file-preview extension, without another parser."""


class Archives:
    def __init__(self, organizer, window):
        self.organizer, self.window = organizer, window

    def read(self):
        from PyQt6.QtCore import QSignalBlocker, Qt
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
            # Restore the host's presentation without reloading a different
            # native tab as a side effect of an automatic Archives read.
            with QSignalBlocker(tabs):
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

    def set_managed(self, name, mod, enabled):
        """Tick or untick an archive in MO2's own list.

        MO2 writes the profile's archive file whenever an item in that list
        changes (MainWindow::on_bsaList_itemChanged), so setting the check state
        on the item is the same thing as clicking it in MO2 — the ordering, the
        file and the refresh are MO2's.
        """
        from PyQt6.QtCore import Qt
        from PyQt6.QtWidgets import QTreeWidget
        if not isinstance(name, str) or not isinstance(mod, str) or not isinstance(enabled, bool):
            raise ValueError('An archive, its mod and the wanted state are required')
        if not self.window.isEnabled(): raise ValueError('MO2 is busy')
        # Reading also populates the list, which MO2 fills only when its own tab
        # is visited; without it there is nothing to tick.
        self.read()
        tree = self.window.findChild(QTreeWidget, 'bsaList')
        if tree is None: raise ValueError('MO2 archive list is unavailable')
        for row in range(tree.topLevelItemCount()):
            group = tree.topLevelItem(row)
            if group.text(0) != mod: continue
            for index in range(group.childCount()):
                item = group.child(index)
                if item.text(0) != name: continue
                if item.isDisabled() or not bool(item.flags() & Qt.ItemFlag.ItemIsUserCheckable):
                    raise ValueError('MO2 does not manage this archive')
                item.setCheckState(0, Qt.CheckState.Checked if enabled else Qt.CheckState.Unchecked)
                return self.read()
        raise ValueError('MO2 no longer lists this archive; refresh first')

    def extract(self, name, mod):
        from PyQt6.QtCore import QCoreApplication, QEvent, QObject, QTimer, Qt
        from PyQt6.QtWidgets import QApplication, QMenu, QTabWidget, QTreeWidget, QWidget
        if not self.window.isEnabled(): raise ValueError('Close MO2’s current dialog first')
        if not isinstance(name, str) or not isinstance(mod, str): raise ValueError('Select an archive and its source mod')
        rows = self.read()['archives']
        if sum(x['name'] == name and x['mod'] == mod for x in rows) != 1:
            raise ValueError('Archive source changed; refresh before extracting')
        tabs = self.window.findChild(QTabWidget, 'tabWidget')
        page = self.window.findChild(QWidget, 'bsaTab')
        tree = self.window.findChild(QTreeWidget, 'bsaList')
        previous = tabs.currentIndex(); visible = self.window.isVisible()
        selected = [(i.parent().text(0), i.text(0)) for i in tree.selectedItems() if i.parent() is not None]
        scroll = tree.verticalScrollBar().value()
        expected = QCoreApplication.translate('MainWindow', 'Extract...').replace('&', '')
        invoked, failures = [], []
        class Capture(QObject):
            armed = True
            def eventFilter(inner, watched, event):
                if inner.armed and isinstance(watched, QMenu) and event.type() == QEvent.Type.Polish:
                    inner.armed = False
                    watched.setAttribute(Qt.WidgetAttribute.WA_DontShowOnScreen, True)
                    def run():
                        actions = [a for a in watched.actions() if a.text().replace('&', '') == expected and a.isEnabled()]
                        watched.close()
                        if len(actions) != 1:
                            failures.append('This MO2 version does not expose archive extraction'); return
                        invoked.append(True); actions[0].trigger()
                    QTimer.singleShot(0, run)
                return False
        app = QApplication.instance(); capture = Capture(); app.installEventFilter(capture)
        try:
            self.window.show()
            tabs.setCurrentWidget(page)
            matches = [tree.topLevelItem(r).child(c) for r in range(tree.topLevelItemCount())
                       for c in range(tree.topLevelItem(r).childCount())
                       if tree.topLevelItem(r).text(0) == mod and tree.topLevelItem(r).child(c).text(0) == name]
            if len(matches) != 1: raise ValueError('Native archive row is unavailable')
            item = matches[0]
            tree.expandItem(item.parent()); tree.setCurrentItem(item); tree.scrollToItem(item)
            tree.doItemsLayout()
            tree.customContextMenuRequested.emit(tree.visualItemRect(item).center())
        finally:
            app.removeEventFilter(capture)
            tree.clearSelection()
            for r in range(tree.topLevelItemCount()):
                group = tree.topLevelItem(r)
                for c in range(group.childCount()):
                    child = group.child(c)
                    if (group.text(0), child.text(0)) in selected: child.setSelected(True)
            tree.verticalScrollBar().setValue(scroll)
            tabs.setCurrentIndex(previous)
            if not visible: self.window.hide()
        if failures: raise ValueError(failures[0])
        if not invoked: raise ValueError('MO2 did not open its extraction action')
        # MO2 owns the picker, progress, errors and cancellation. Opening this
        # action is not proof that the user chose a destination or extracted files.
        return {'opened': True}

    def preview(self, name):
        if not isinstance(name, str) or not any(x['name'] == name for x in self.read()['archives']):
            raise ValueError('Archive no longer exists in the selected MO2 profile')
        return self._preview([name])

    def preview_file(self, directory, name, origins, operation="preview"):
        if operation not in ("preview", "hide", "unhide", "reveal", "revealPath"):
            raise ValueError("Unknown Data file action")
        from .data_files import read_directory
        rows = read_directory(self.organizer, directory)['entries']
        matches = [row for row in rows if not row['directory'] and row['name'] == name and row['origins'] == origins]
        if len(matches) != 1:
            raise ValueError('File source changed; refresh Data before previewing')
        from pathlib import PureWindowsPath
        if operation != "preview" and matches[0]["archive"]:
            raise ValueError("Files inside archives cannot be hidden or revealed individually")
        parts = [*PureWindowsPath(directory).parts, name]
        if operation == "revealPath":
            path = self.organizer.resolvePath('/'.join(parts))
            if not path: raise ValueError('MO2 could not resolve this file’s location')
            return {'path': path}
        if operation == "preview": return self._preview(parts)
        if operation == "reveal": return self._preview(parts, operation)
        result = self._preview(parts, operation)
        # Native renaming returns before the asynchronous directory rebuild.
        # Observe it before allowing the frontend to reload the affected folder.
        from PyQt6.QtCore import QEventLoop, QTimer
        loop = QEventLoop(); poll = QTimer(); deadline = QTimer(); deadline.setSingleShot(True)
        def updated():
            if read_directory(self.organizer, directory)['entries'] != rows: loop.quit()
        poll.timeout.connect(updated); deadline.timeout.connect(loop.quit)
        poll.start(100); deadline.start(5000); loop.exec(); poll.stop(); deadline.stop()
        return result

    def _preview(self, parts, operation="preview"):
        from PyQt6.QtCore import QCoreApplication, QEvent, QEventLoop, QItemSelectionModel, QModelIndex, QObject, QPersistentModelIndex, QTimer, Qt
        from PyQt6.QtWidgets import QApplication, QCheckBox, QLineEdit, QMenu, QTabWidget, QTreeView, QWidget
        if not self.window.isEnabled(): raise ValueError('Close MO2’s current dialog first')
        tabs = self.window.findChild(QTabWidget, 'tabWidget')
        page = self.window.findChild(QWidget, 'dataTab')
        tree = self.window.findChild(QTreeView, 'dataTree')
        if tabs is None or page is None or tree is None: raise ValueError('MO2 Data browser is unavailable')
        original_tab = tabs.currentIndex()
        search = self.window.findChild(QLineEdit, 'dataTabFilter')
        conflict_filter = self.window.findChild(QCheckBox, 'dataTabShowOnlyConflicts')
        hidden_filter = self.window.findChild(QCheckBox, 'dataTabShowHiddenFiles')
        old_hidden = hidden_filter.isChecked() if hidden_filter else None
        old_search = search.text() if search else None
        old_conflicts = conflict_filter.isChecked() if conflict_filter else None
        visible = self.window.isVisible()
        previous_rows = [QPersistentModelIndex(x) for x in tree.selectionModel().selectedRows()]
        old_scroll = tree.verticalScrollBar().value()
        failures, invoked = [], []
        expanded = []
        expected = QCoreApplication.translate('FileTree', {'preview': '&Preview', 'hide': '&Hide', 'unhide': '&Un-Hide', 'reveal': 'Reveal in E&xplorer'}[operation]).replace('&', '')
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
                            failures.append('No enabled MO2 preview extension supports this file.' if operation == 'preview' else 'MO2 does not allow this file action.')
                            return
                        invoked.append(True); actions[0].trigger()
                    QTimer.singleShot(0, run)
                return False
        app = QApplication.instance(); capture = Capture(); app.installEventFilter(capture)
        try:
            self.window.show()  # Hidden-host suppression remains in force.
            if search: search.clear()
            if conflict_filter: conflict_filter.setChecked(False)
            if hidden_filter: hidden_filter.setChecked(True)
            tabs.setCurrentWidget(page)
            loop = QEventLoop(); timer = QTimer(); deadline = QTimer(); deadline.setSingleShot(True)
            found = []
            def locate():
                model = tree.model()
                parent = QModelIndex()
                for depth, part in enumerate(parts):
                    if model.canFetchMore(parent): model.fetchMore(parent)
                    matches = [model.index(row, 0, parent) for row in range(model.rowCount(parent))
                               if str(model.index(row, 0, parent).data()).casefold() == part.casefold()]
                    if len(matches) != 1: return
                    parent = matches[0]
                    if depth < len(parts) - 1 and not tree.isExpanded(parent):
                        expanded.append(QPersistentModelIndex(parent)); tree.expand(parent)
                found.append(parent); loop.quit()
            timer.timeout.connect(locate); deadline.timeout.connect(loop.quit)
            timer.start(100); deadline.start(10000); loop.exec(); timer.stop(); deadline.stop()
            if not found: raise ValueError('MO2 Data browser could not resolve this file')
            index = found[0]; tree.setCurrentIndex(index); tree.scrollTo(index)
            capture.armed = True
            tree.customContextMenuRequested.emit(tree.visualRect(index).center())
        finally:
            app.removeEventFilter(capture)
            if hidden_filter is not None: hidden_filter.setChecked(old_hidden)
            if search is not None: search.setText(old_search)
            if conflict_filter is not None: conflict_filter.setChecked(old_conflicts)
            tabs.setCurrentIndex(original_tab)
            tree.selectionModel().clearSelection()
            for old in previous_rows:
                if old.isValid(): tree.selectionModel().select(QModelIndex(old), QItemSelectionModel.SelectionFlag.Select | QItemSelectionModel.SelectionFlag.Rows)
            for opened in reversed(expanded):
                if opened.isValid(): tree.collapse(QModelIndex(opened))
            tree.verticalScrollBar().setValue(old_scroll)
            if not visible: self.window.hide()
        if failures: raise ValueError(failures[0])
        if not invoked: raise ValueError('MO2 did not open its file preview action')
        return {'opened': True}
