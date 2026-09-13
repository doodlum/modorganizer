"""Read MO2's generated files and invoke its original Overwrite menu actions."""
import os
from pathlib import Path


class Overwrite:
    def __init__(self, organizer, window):
        self.organizer, self.window = organizer, window

    def read(self):
        root = Path(self.organizer.overwritePath())
        files = []
        if root.exists():
            for directory, dirs, names in os.walk(root, followlinks=False):
                dirs[:] = sorted(d for d in dirs if not (Path(directory) / d).is_symlink())
                for name in sorted(names):
                    path = Path(directory) / name
                    try:
                        if path.is_symlink(): continue
                        size = path.stat().st_size
                    except FileNotFoundError:
                        continue  # A running tool can replace its output while we read.
                    files.append({'path': path.relative_to(root).as_posix(), 'bytes': size})
        return {'path': str(root), 'files': files}

    def action(self, operation):
        from PyQt6.QtCore import QAbstractProxyModel, QCoreApplication, QEvent, QObject, QTimer, Qt, QItemSelectionModel, QPersistentModelIndex, QModelIndex
        from PyQt6.QtWidgets import QApplication, QMenu, QTreeView
        labels = {'create': 'Create Mod...', 'move': 'Move content to Mod...',
                  'sync': 'Sync to Mods...', 'clear': 'Clear Overwrite...', 'folder': 'Open in Explorer'}
        if operation not in labels: raise ValueError('Unknown Overwrite action')
        if not self.window.isEnabled(): raise ValueError('MO2 is busy; close its current dialog first')
        mods = self.organizer.modList()
        root = os.path.normcase(os.path.normpath(self.organizer.overwritePath()))
        names = list(mods.allMods())
        matches = [name for name in names if os.path.normcase(os.path.normpath(mods.getMod(name).absolutePath())) == root]
        if len(matches) != 1: raise ValueError('MO2 Overwrite entry is unavailable')
        view = self.window.findChild(QTreeView, 'modList')
        if view is None or not view.isEnabled(): raise ValueError('MO2 mod list is unavailable')
        model, proxies = view.model(), []
        while isinstance(model, QAbstractProxyModel):
            proxies.append(model); model = model.sourceModel()
        if model is None or model.metaObject().className() != 'ModList': raise ValueError('Unsupported MO2 mod list model')
        index = model.index(names.index(matches[0]), 0)
        for proxy in reversed(proxies): index = proxy.mapFromSource(index)
        if not index.isValid(): raise ValueError('Clear the original MO2 filter to access Overwrite')
        selection = view.selectionModel()
        previous = [QPersistentModelIndex(x) for x in selection.selectedRows()]
        scroll = view.verticalScrollBar().value()
        was_visible = self.window.isVisible()
        expected = QCoreApplication.translate('ModListContextMenu', labels[operation]).replace('&', '')
        invoked, failures = [], []
        class Capture(QObject):
            def eventFilter(self, watched, event):
                if isinstance(watched, QMenu) and watched.parent() == view and event.type() == QEvent.Type.Polish:
                    watched.setAttribute(Qt.WidgetAttribute.WA_DontShowOnScreen, True)
                    def run():
                        actions = [a for a in watched.actions() if a.text().replace('&', '') == expected and a.isEnabled()]
                        watched.close()
                        if len(actions) != 1:
                            failures.append('This action is unavailable; Overwrite may be empty. Refresh and try again.')
                            return
                        invoked.append(operation)
                        actions[0].trigger()  # Keep native prompts, conflict handling and refresh.
                    QTimer.singleShot(0, run)
                return False
        app = QApplication.instance(); capture = Capture(); app.installEventFilter(capture)
        try:
            self.window.show()  # Hidden-host WA_DontShowOnScreen still prevents mapping.
            selection.select(index, QItemSelectionModel.SelectionFlag.ClearAndSelect | QItemSelectionModel.SelectionFlag.Rows)
            view.scrollTo(index)
            rect = view.visualRect(index)
            if rect.isEmpty(): raise ValueError('MO2 could not locate the Overwrite row')
            view.customContextMenuRequested.emit(rect.center())
        finally:
            app.removeEventFilter(capture)
            selection.clearSelection()
            for old in previous:
                if old.isValid(): selection.select(QModelIndex(old), QItemSelectionModel.SelectionFlag.Select | QItemSelectionModel.SelectionFlag.Rows)
            view.verticalScrollBar().setValue(scroll)
            if not was_visible: self.window.hide()
        if failures: raise ValueError(failures[0])
        if not invoked: raise ValueError('MO2 did not open the Overwrite menu')
        return {'requested': operation}
