"""Use the original mod model's confirmed uninstall and profile cleanup."""


class ModActions:
    def __init__(self, organizer, window):
        self.organizer = organizer
        self.window = window

    def manage_executables(self):
        from PyQt6.QtGui import QAction
        action = self.window.findChild(QAction, 'actionModify_Executables')
        if not self.window.isEnabled() or action is None or not action.isEnabled():
            raise ValueError('MO2 executable settings are unavailable')
        action.trigger()
        return {'opened': True}

    def _tools(self):
        from PyQt6.QtGui import QAction
        if not self.window.isEnabled():
            raise ValueError('MO2 is busy; wait before opening tools')
        action = self.window.findChild(QAction, 'actionTool')
        if action is None or action.menu() is None:
            raise ValueError('MO2 tools menu is unavailable')
        menu = action.menu()
        menu.aboutToShow.emit()
        def leaves(current, path):
            for item in current.actions():
                if item.isSeparator() or not item.isVisible(): continue
                name = item.text().replace('&', '').strip()
                if not name: continue
                key = path + [name]
                if item.menu() is not None:
                    yield from leaves(item.menu(), key)
                else:
                    yield key, item
        return list(leaves(menu, []))

    def list_tools(self):
        from .icons import icon_png
        return {'tools': [{'id': path, 'name': path[-1], 'group': ' / '.join(path[:-1]),
                           'icon': icon_png(action.icon()), 'description': action.toolTip().replace('&', ''), 'enabled': action.isEnabled()}
                          for path, action in self._tools()]}

    def run_tool(self, identifier):
        if not isinstance(identifier, list) or not all(isinstance(x, str) for x in identifier):
            raise ValueError('A tool identifier is required')
        matches = [action for path, action in self._tools() if path == identifier]
        if len(matches) != 1 or not matches[0].isEnabled():
            raise ValueError('This MO2 tool is unavailable; refresh the tools page')
        matches[0].trigger()
        return {'opened': True}

    def health_check(self):
        from PyQt6.QtCore import QObject, QEvent, QMetaObject, QTimer, Qt
        from PyQt6.QtGui import QTextDocument
        from PyQt6.QtWidgets import QApplication, QDialog, QTreeWidget
        if not self.window.isEnabled():
            raise ValueError('MO2 is busy; wait before checking health')
        captured, failures = [], []
        class Capture(QObject):
            def eventFilter(inner, watched, event):
                if (isinstance(watched, QDialog) and watched.objectName() == 'ProblemsDialog'
                        and event.type() == QEvent.Type.Polish):
                    # Polish occurs before QWidget maps its native window.
                    watched.setAttribute(Qt.WidgetAttribute.WA_DontShowOnScreen, True)
                    def read():
                        try:
                            tree = watched.findChild(QTreeWidget, 'problemsWidget')
                            if tree is None: raise ValueError('MO2 diagnostics tree is unavailable')
                            entries = []
                            for row in range(tree.topLevelItemCount()):
                                item = tree.topLevelItem(row)
                                description = item.data(0, Qt.ItemDataRole.UserRole)
                                # MO2's empty placeholder has an empty description and italic title.
                                if not description and item.font(0).italic(): continue
                                document = QTextDocument(); document.setHtml(str(description or ''))
                                entries.append({'title': item.text(0), 'details': document.toPlainText()})
                            captured.append(entries)
                        except Exception as error:
                            failures.append(str(error))
                        finally:
                            watched.reject()
                    QTimer.singleShot(0, read)
                return False
        app = QApplication.instance()
        capture = Capture()
        app.installEventFilter(capture)
        try:
            # Invoke the native slot even when the last cached notification count
            # was zero; it refreshes diagnostics before constructing the dialog.
            QMetaObject.invokeMethod(self.window, 'on_actionNotifications_triggered', Qt.ConnectionType.DirectConnection)
        finally:
            app.removeEventFilter(capture)
        if failures: raise ValueError(failures[0])
        if len(captured) != 1: raise ValueError('MO2 diagnostics did not complete')
        return {'problems': captured[0]}

    def snapshot(self):
        import os
        from PyQt6.QtCore import QAbstractProxyModel, Qt
        from PyQt6.QtGui import QTextDocument
        from PyQt6.QtWidgets import QTreeView

        mods = self.organizer.modList()
        names = list(mods.allMods())
        ordered = list(mods.allModsByProfilePriority())
        overwrite = os.path.normcase(os.path.normpath(self.organizer.overwritePath()))
        overwrite_names = {name for name in names if
                           os.path.normcase(os.path.normpath(mods.getMod(name).absolutePath())) == overwrite}
        ordered.extend(name for name in names if name in overwrite_names and name not in ordered)
        view = self.window.findChild(QTreeView, 'modList')
        if view is None:
            raise ValueError('MO2 mod list is unavailable')
        model = view.model()
        while isinstance(model, QAbstractProxyModel):
            model = model.sourceModel()
        if model is None or model.metaObject().className() != 'ModList':
            raise ValueError('Unsupported MO2 mod list model')
        document = QTextDocument()
        def plain(value):
            document.setHtml(str(value or ''))
            return document.toPlainText()
        result = []
        rows = {name: index for index, name in enumerate(names)}
        for name in ordered:
            row = rows[name]
            index = model.index(row, 0)
            if not index.isValid() or index.data(int(Qt.ItemDataRole.UserRole) + 1) != row:
                raise ValueError('MO2 mod row changed; refresh before reading details')
            state = mods.state(name)
            result.append({
                'name': name, 'displayName': str(index.data(Qt.ItemDataRole.DisplayRole)),
                'state': int(getattr(state, 'value', state)), 'priority': mods.priority(name),
                'priorityText': str(model.index(row, 9).data(Qt.ItemDataRole.DisplayRole) or ''),
                'overwrite': name in overwrite_names,
                'conflicts': plain(model.index(row, 1).data(Qt.ItemDataRole.ToolTipRole)),
                'flags': plain(model.index(row, 2).data(Qt.ItemDataRole.ToolTipRole)),
            })
        return result

    def plugin_snapshot(self):
        from PyQt6.QtCore import QAbstractProxyModel, Qt
        from PyQt6.QtGui import QTextDocument
        from PyQt6.QtWidgets import QTreeView
        view = self.window.findChild(QTreeView, 'espList')
        if view is None:
            raise ValueError('MO2 plugin list is unavailable')
        model = view.model()
        while isinstance(model, QAbstractProxyModel):
            model = model.sourceModel()
        if model is None or model.metaObject().className() != 'PluginList':
            raise ValueError('Unsupported MO2 plugin list model')
        plugins = self.organizer.pluginList()
        expected = set(plugins.pluginNames())
        document = QTextDocument()
        result = []
        for row in range(model.rowCount()):
            index = model.index(row, 0)
            name = index.data(Qt.ItemDataRole.DisplayRole)
            if name not in expected:
                raise ValueError('MO2 plugin row changed; refresh before reading details')
            document.setHtml(str(index.data(Qt.ItemDataRole.ToolTipRole) or ''))
            flags = model.flags(index)
            state = plugins.state(name)
            result.append({
                'name': name, 'state': int(getattr(state, 'value', state)),
                'priority': plugins.priority(name), 'loadOrder': plugins.loadOrder(name),
                'masters': list(plugins.masters(name)), 'origin': plugins.origin(name),
                'diagnostics': document.toPlainText(),
                'hasWarning': ':/MO/gui/warning' in (index.data(int(Qt.ItemDataRole.UserRole) + 1) or []),
                'modIndex': str(model.index(row, 3).data(Qt.ItemDataRole.DisplayRole) or ''),
                'canToggle': bool(flags & Qt.ItemFlag.ItemIsUserCheckable),
                'canMove': bool(flags & Qt.ItemFlag.ItemIsDragEnabled),
            })
        if len(result) != len(expected):
            raise ValueError('MO2 plugin list changed; refresh before reading details')
        return result

    def nexus_settings(self):
        from PyQt6.QtCore import QTimer
        from PyQt6.QtGui import QAction
        from PyQt6.QtWidgets import QDialog, QTabWidget, QWidget
        action = self.window.findChild(QAction, 'actionSettings')
        if action is None or not action.isEnabled():
            raise ValueError('MO2 settings are unavailable')
        selected = []
        errors = []
        timer = QTimer()
        timer.setSingleShot(True)
        def select_nexus():
            dialog = next((d for d in self.window.findChildren(QDialog)
                           if d.objectName() == 'SettingsDialog' and d.isVisible()), None)
            try:
                if dialog is None:
                    raise ValueError('MO2 settings dialog did not open')
                tabs = dialog.findChild(QTabWidget, 'tabWidget')
                page = dialog.findChild(QWidget, 'nexusTab')
                if tabs is None or page is None or tabs.indexOf(page) < 0:
                    raise ValueError('This MO2 version does not expose its Nexus settings tab')
                tabs.setCurrentWidget(page)
                selected.append(tabs.currentWidget() == page)
            except Exception as error:
                errors.append(str(error))
                if dialog is not None: dialog.reject()
        timer.timeout.connect(select_nexus)
        self.window.show(); self.window.raise_(); timer.start(0)
        try:
            action.trigger()
        finally:
            timer.stop()
        if errors: raise ValueError(errors[0])
        if selected != [True]: raise ValueError('MO2 did not display Nexus settings')
        return {'opened': True, 'tab': 'nexusTab'}

    def details(self, name):
        from PyQt6.QtCore import QAbstractProxyModel, QEventLoop, QTimer, Qt
        from PyQt6.QtWidgets import QApplication, QDialog, QTreeView
        names = list(self.organizer.modList().allMods())
        if not isinstance(name, str) or name not in names:
            raise ValueError('Mod no longer exists')
        view = self.window.findChild(QTreeView, 'modList')
        if view is None or not view.isEnabled():
            raise ValueError('MO2 mod list is unavailable')
        if QApplication.queryKeyboardModifiers() != Qt.KeyboardModifier.NoModifier:
            raise ValueError('Release modifier keys before opening MO2 details')
        model = view.model()
        proxies = []
        while isinstance(model, QAbstractProxyModel):
            proxies.append(model)
            model = model.sourceModel()
        if model is None or model.metaObject().className() != 'ModList':
            raise ValueError('Unsupported MO2 mod list model')
        row = names.index(name)
        index = model.index(row, 1)  # MO2's own conflict column opens its Conflicts tab.
        if not index.isValid() or index.data(int(Qt.ItemDataRole.UserRole) + 1) != row:
            raise ValueError('MO2 mod row changed; refresh before opening details')
        for proxy in reversed(proxies):
            index = proxy.mapFromSource(index)
        if not index.isValid():
            raise ValueError('Clear the original MO2 mod-list filter to open this mod’s details')
        shown = []
        def dialogs():
            return [dialog for dialog in self.window.findChildren(QDialog)
                    if dialog.isVisible() and dialog.objectName() in ('ModInfoDialog', '__overwriteDialog')]
        timer = QTimer()
        timer.setSingleShot(True)
        timer.timeout.connect(lambda: shown.extend(dialogs()))
        self.window.show()
        self.window.raise_()
        timer.start(0)
        try:
            view.doubleClicked.emit(index)
            # Regular details are modal. Overwrite uses MO2's modeless file dialog.
            for dialog in dialogs():
                shown.append(dialog)
                loop = QEventLoop()
                dialog.finished.connect(loop.quit)
                if dialog.isVisible():
                    loop.exec()
        finally:
            timer.stop()
        if not shown:
            raise ValueError('MO2 did not open a mod detail dialog; wait and try again')
        return {'opened': True, 'modName': name}

    def remove(self, name):
        from PyQt6.QtCore import QAbstractProxyModel, Qt
        from PyQt6.QtWidgets import QTreeView

        mods = self.organizer.modList()
        names = list(mods.allMods())
        if not isinstance(name, str) or name not in names:
            raise ValueError('Mod no longer exists')
        state = mods.state(name)
        if int(getattr(state, 'value', state)) & 4:
            raise ValueError('Essential content cannot be uninstalled')
        view = self.window.findChild(QTreeView, 'modList')
        if view is None or not view.isEnabled():
            raise ValueError('MO2 mod list is unavailable')
        model = view.model()
        while isinstance(model, QAbstractProxyModel):
            model = model.sourceModel()
        if model is None or model.metaObject().className() != 'ModList':
            raise ValueError('Unsupported MO2 mod list model')
        # IModList.allMods and ModList source rows share ModInfo's order.
        # Resolve immediately before removal; proxy sorting is irrelevant.
        row = names.index(name)
        index = model.index(row, 0)
        if not index.isValid() or index.data(int(Qt.ItemDataRole.UserRole) + 1) != row:
            raise ValueError('MO2 mod row changed; refresh before uninstalling')
        # This is the original single-mod removal path, including MO2's Yes/No
        # dialog, profile cleanup, origin removal and download notification.
        # removeRow returns true even after Cancel, so inspect actual state.
        model.removeRow(row)
        return {'removed': name not in mods.allMods(), 'modName': name}
