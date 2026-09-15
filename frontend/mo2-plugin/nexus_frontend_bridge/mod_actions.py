"""Use the original mod model's confirmed uninstall and profile cleanup."""


def conflict_neighbors(name, origins):
    """Split MO2's winner-first, ascending-alternatives origin sequence.

    OrganizerCore::getFileOrigins preserves the native alternatives vector;
    AdvancedConflictsTab uses that vector for before/after relationships.
    Do not sort by mod priority: native precedence also accounts for archives.
    """
    if len(origins) < 2 or name not in origins:
        return set(), set()
    if origins[0] == name:
        return set(), set(origins[1:]) - {name}
    position = origins.index(name, 1)
    return ({origins[0]} | set(origins[position + 1:])) - {name}, set(origins[1:position]) - {name}


def plain_text(value):
    """Strip the markup MO2 puts in its tooltips, on a document of its own.

    The callers already hold a QTextDocument they are reading other text out
    of, and setHtml would replace it mid-read.
    """
    from PyQt6.QtGui import QTextDocument
    document = QTextDocument()
    document.setHtml(str(value or ''))
    return document.toPlainText()


def background_color(model, row, column):
    """The colour the user gave a mod, as MO2 reports it.

    ModList::data returns it as the row's background: the whole row for a
    separator, and the Notes cell for an ordinary mod. Read from the model rather
    than from ModInfo::color so an MO2 that decides a row's colour differently is
    still read the way it draws.
    """
    from PyQt6.QtCore import Qt
    from PyQt6.QtGui import QBrush, QColor
    value = model.index(row, column).data(Qt.ItemDataRole.BackgroundRole)
    if isinstance(value, QBrush):
        value = value.color()
    if not isinstance(value, QColor) or not value.isValid():
        return ''
    return value.name()


def model_columns(model):
    """Map a list model's header text to its column index.

    Read by name rather than by a fixed index because MO2's own column order is
    not fixed: this tree's ModList gained Author and Uploader after the columns
    beyond Category, so a build that hardcodes the index reads whatever now
    occupies it. Reading 'Version' at index 7 returned the Nexus ID column, and
    'Priority' at 9 returned Version, which is exactly what this avoids.
    """
    from PyQt6.QtCore import Qt
    headers = {}
    for column in range(model.columnCount()):
        header = model.headerData(column, Qt.Orientation.Horizontal, Qt.ItemDataRole.DisplayRole)
        if header:
            headers.setdefault(str(header), column)
    return headers


class ModActions:
    def __init__(self, organizer, window):
        self.organizer = organizer
        self.window = window

    def selection_links(self, names):
        """Read native conflict models; resolve names through the virtual filesystem."""
        from PyQt6.QtCore import QAbstractProxyModel, QEvent, QObject, QTimer, Qt
        from PyQt6.QtWidgets import QApplication, QDialog, QTreeView
        mods = self.organizer.modList()
        if not isinstance(names, list) or any(not isinstance(n, str) or n not in mods.allMods() for n in names):
            raise ValueError('Selected mods changed; refresh the mod list')
        if not self.window.isEnabled(): raise ValueError('MO2 is busy')
        selected = set(names)
        plugins = [p for p in self.organizer.pluginList().pluginNames()
                   if selected.intersection(self.organizer.getFileOrigins(p))]
        winners, losers, errors = set(), set(), []
        # Only conflicting mods need their original conflict model populated.
        conflicting = {m['name'] for m in self.snapshot() if m['conflicts'] and not m['overwrite']}
        for name in selected.intersection(conflicting):
            captured = []
            class Capture(QObject):
                def eventFilter(inner, watched, event):
                    if isinstance(watched, QDialog) and watched.objectName() == 'ModInfoDialog' and event.type() == QEvent.Type.Polish:
                        watched.setAttribute(Qt.WidgetAttribute.WA_DontShowOnScreen, True)
                        def read():
                            try:
                                for tree_name in ('overwriteTree', 'overwrittenTree'):
                                    tree = watched.findChild(QTreeView, tree_name)
                                    if tree is None: raise ValueError('Native conflict list is unavailable')
                                    model = tree.model()
                                    while isinstance(model, QAbstractProxyModel): model = model.sourceModel()
                                    for row in range(model.rowCount()):
                                        path = str(model.index(row, 0).data() or '').lstrip('/\\')
                                        origins = list(self.organizer.getFileOrigins(path))
                                        above, below = conflict_neighbors(name, origins)
                                        winners.update(above)
                                        losers.update(below)
                                captured.append(True)
                            except Exception as error: errors.append(str(error))
                            finally: watched.reject()
                        QTimer.singleShot(0, read)
                    return False
            app = QApplication.instance(); capture = Capture(); app.installEventFilter(capture)
            visible = self.window.isVisible()
            try: self.details(name)
            finally:
                app.removeEventFilter(capture)
                if not visible: self.window.hide()
            if errors: raise ValueError(errors[0])
            if not captured: raise ValueError('Native conflict details did not complete')
        return {'plugins': plugins, 'winningMods': sorted(winners - selected), 'losingMods': sorted(losers - selected)}

    def create_separator(self, name=None, collection_name=None):
        from PyQt6.QtCore import QAbstractProxyModel, QCoreApplication, QEvent, QObject, QPoint, QTimer, Qt
        from PyQt6.QtWidgets import QApplication, QInputDialog, QMenu, QTreeView
        if not self.window.isEnabled(): raise ValueError('MO2 is busy')
        view = self.window.findChild(QTreeView, 'modList')
        if view is None: raise ValueError('MO2 mod list is unavailable')
        if collection_name is not None:
            if not isinstance(collection_name, str) or not collection_name.strip() or len(collection_name) > 120 or any(ord(c) < 32 or c in '<>:"/\\|?*' for c in collection_name) or collection_name.endswith(('.', ' ')):
                raise ValueError('Choose a valid collection name (up to 120 characters)')
            existing = {n.casefold() for n in self.organizer.modList().allMods()}
            if collection_name.casefold() in existing or (collection_name + '_separator').casefold() in existing:
                raise ValueError('A mod or collection with this name already exists')
        # Invoke the native global menu: it owns name validation and creation.
        invoked, errors = [], []
        expected = QCoreApplication.translate('ModListGlobalContextMenu', 'Create separator').replace('&', '')
        class Capture(QObject):
            armed = True
            entered = False
            def eventFilter(inner, watched, event):
                if collection_name is not None and not inner.entered and isinstance(watched, QInputDialog) and event.type() == QEvent.Type.Polish:
                    inner.entered = True
                    watched.setAttribute(Qt.WidgetAttribute.WA_DontShowOnScreen, True)
                    def accept_name():
                        watched.setTextValue(collection_name)
                        watched.accept()
                    QTimer.singleShot(0, accept_name)
                if inner.armed and isinstance(watched, QMenu) and event.type() == QEvent.Type.Polish:
                    inner.armed = False
                    watched.setAttribute(Qt.WidgetAttribute.WA_DontShowOnScreen, True)
                    def run():
                        actions = [a for a in watched.actions() if a.text().replace('&', '') == expected and a.isEnabled()]
                        watched.close()
                        if len(actions) != 1: errors.append('Native separator action is unavailable'); return
                        invoked.append(True); actions[0].trigger()
                    QTimer.singleShot(0, run)
                return False
        mods = self.organizer.modList()
        if name is not None and name not in mods.allMods(): raise ValueError('Selected mod no longer exists')
        priority = mods.priority(name) if name is not None else -1
        before = set(mods.allMods())
        app = QApplication.instance(); capture = Capture(); app.installEventFilter(capture)
        visible = self.window.isVisible()
        try:
            if collection_name is None: self.window.show()
            view.customContextMenuRequested.emit(QPoint(-1, -1))
        finally:
            app.removeEventFilter(capture)
            if not visible: self.window.hide()
        if errors: raise ValueError(errors[0])
        if not invoked: raise ValueError('MO2 did not open its separator menu')
        created = [n for n in mods.allMods() if n not in before and n.endswith('_separator')]
        if len(created) == 1 and priority >= 0: mods.setPriority(created[0], priority)
        return {'created': created}

    def manage_executables(self):
        from PyQt6.QtGui import QAction
        action = self.window.findChild(QAction, 'actionModify_Executables')
        if not self.window.isEnabled() or action is None or not action.isEnabled():
            raise ValueError('MO2 executable settings are unavailable')
        action.trigger()
        return {'opened': True}

    # MO2's own main-window entries that the frontend has no page of its own for.
    # Allowlisted by name rather than taking one from the request: this triggers
    # real menu actions in the host, and the set it may reach has to be a decision
    # made here, not by whatever asked.
    ORIGINAL_ACTIONS = {
        'settings': 'actionSettings',
        'notifications': 'actionNotifications',
        'endorse': 'actionEndorseMO',
        'nexus': 'actionNexus',
        'help': 'actionHelp',
        'update': 'actionUpdate',
    }

    def open_original(self, name):
        from PyQt6.QtGui import QAction
        if name not in self.ORIGINAL_ACTIONS:
            raise ValueError('Unsupported MO2 window action')
        action = self.window.findChild(QAction, self.ORIGINAL_ACTIONS[name])
        if not self.window.isEnabled() or action is None or not action.isEnabled():
            raise ValueError('That MO2 window action is unavailable; close its current dialog first')
        action.trigger()
        return {'opened': True}

    def order_backup(self, target, operation):
        from PyQt6.QtWidgets import QPushButton
        buttons = {('mods', 'backup'): 'saveModsButton', ('mods', 'restore'): 'restoreModsButton',
                   ('plugins', 'backup'): 'saveButton', ('plugins', 'restore'): 'restoreButton'}
        if not isinstance(target, str) or not isinstance(operation, str) or (target, operation) not in buttons:
            raise ValueError('Choose mod-list or plugin-order backup or restore')
        button = self.window.findChild(QPushButton, buttons[target, operation])
        if not self.window.isEnabled() or button is None or not button.isEnabled():
            raise ValueError('MO2 backup or restore is unavailable; close its current dialog first')
        # Native slots flush current state, keep the original retention policy,
        # and own the restore picker, cancellation, writes and refresh.
        button.click()
        return {'opened': True}

    def can_sort_plugins(self):
        from PyQt6.QtWidgets import QPushButton
        button = self.window.findChild(QPushButton, 'sortButton')
        return self.window.isEnabled() and button is not None and button.isEnabled()

    def sort_unavailable_reason(self):
        from PyQt6.QtWidgets import QPushButton
        if not self.window.isEnabled():
            return 'MO2 is busy; close its current dialog first.'
        button = self.window.findChild(QPushButton, 'sortButton')
        if button is None:
            return 'This MO2 version does not expose its plugin sort control.'
        if button.isEnabled():
            return ''
        game = self.organizer.managedGame()
        if (game.name() == 'Fallout NV Support Plugin' and
                not self.organizer.pluginSetting(game.name(), 'enable_loot_sorting')):
            return ('LOOT sorting is disabled by the Fallout NV Support Plugin. '
                    'Its default follows the FNV modding community recommendation. '
                    'To opt in, open MO2 Settings > Plugins > Fallout NV Support Plugin '
                    'and enable enable_loot_sorting. Manual plugin ordering remains available.')
        return button.toolTip() or 'Plugin sorting is disabled in MO2.'

    def sort_plugins(self):
        from PyQt6.QtWidgets import QPushButton
        if not self.can_sort_plugins():
            raise ValueError('MO2 plugin sorting is unavailable or busy')
        # The original slot owns LOOT, its progress/confirmation dialogs,
        # masterlist updates and writing the resulting plugin order.
        self.window.findChild(QPushButton, 'sortButton').click()
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
        tools = list(leaves(menu, []))
        settings = self.window.findChild(QAction, 'actionSettings')
        if settings is not None and settings.isVisible():
            tools.append((['MO2', 'Settings'], settings))
        return tools

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
        columns = model_columns(model)
        def cell(row, header, role=Qt.ItemDataRole.DisplayRole):
            column = columns.get(header)
            return '' if column is None else str(model.index(row, column).data(role) or '')
        notes_column = columns.get('Notes')
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
                'priorityText': cell(row, 'Priority'),
                'overwrite': name in overwrite_names,
                'nexusId': mods.getMod(name).nexusId(),
                'version': cell(row, 'Version'),
                # MO2 tracks the newest known version on the mod itself, not in the
                # list model. Older builds may not expose it, so failure is not fatal.
                'newestVersion': newest_version(mods.getMod(name)),
                'category': cell(row, 'Category'),
                'separator': mods.getMod(name).isSeparator(),
                'conflicts': plain(cell(row, 'Conflicts', Qt.ItemDataRole.ToolTipRole)),
                'flags': plain(cell(row, 'Flags', Qt.ItemDataRole.ToolTipRole)),
                # The rest of MO2's own columns, so the frontend can show the list it
                # shows rather than a subset of it. Any MO2 that lacks one sends "".
                'content': plain(cell(row, 'Content', Qt.ItemDataRole.ToolTipRole)),
                'author': cell(row, 'Author'),
                'uploader': cell(row, 'Uploader'),
                'sourceGame': cell(row, 'Source Game'),
                'installTime': cell(row, 'Installation'),
                'notes': plain(cell(row, 'Notes', Qt.ItemDataRole.ToolTipRole)) or cell(row, 'Notes'),
                'color': background_color(model, row, 0),
                'notesColor': '' if notes_column is None else background_color(model, row, notes_column),
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
        columns = model_columns(model)
        def cell(row, header, role=Qt.ItemDataRole.DisplayRole):
            column = columns.get(header)
            return '' if column is None else str(model.index(row, column).data(role) or '')
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
                'locked': ':/MO/gui/locked' in (index.data(int(Qt.ItemDataRole.UserRole) + 1) or []),
                'modIndex': cell(row, 'Mod Index'),
                # The rest of MO2's own plugin columns, for the same reason as the
                # mod list above.
                'pluginFlags': plain_text(cell(row, 'Flags', Qt.ItemDataRole.ToolTipRole)),
                'priorityText': cell(row, 'Priority'),
                'formVersion': cell(row, 'Form Version'),
                'headerVersion': cell(row, 'Header Version'),
                'author': cell(row, 'Author'),
                'description': cell(row, 'Description'),
                'canToggle': bool(flags & Qt.ItemFlag.ItemIsUserCheckable),
                'canMove': bool(flags & Qt.ItemFlag.ItemIsDragEnabled),
            })
        if len(result) != len(expected):
            raise ValueError('MO2 plugin list changed; refresh before reading details')
        return result

    def set_plugin_active(self, name, enabled):
        from PyQt6.QtCore import QAbstractProxyModel, Qt
        from PyQt6.QtWidgets import QTreeView
        if not isinstance(name, str) or type(enabled) is not bool:
            raise ValueError('Select a plugin and its requested activation state')
        if not self.window.isEnabled(): raise ValueError('Close MO2’s current dialog first')
        tree = self.window.findChild(QTreeView, 'espList')
        if tree is None: raise ValueError('MO2 plugin list is unavailable')
        model = tree.model()
        while isinstance(model, QAbstractProxyModel): model = model.sourceModel()
        if model is None or model.metaObject().className() != 'PluginList':
            raise ValueError('Unsupported MO2 plugin list model')
        matches = [model.index(row, 0) for row in range(model.rowCount())
                   if model.index(row, 0).data(Qt.ItemDataRole.DisplayRole) == name]
        if len(matches) != 1: raise ValueError('Plugin no longer exists')
        index = matches[0]
        if not model.flags(index) & Qt.ItemFlag.ItemIsUserCheckable:
            raise ValueError('MO2 does not allow changing this plugin’s activation')
        # IPluginList.setState only changes the in-memory flag. The native
        # checkbox path also writes the lists, refreshes indexes and notifies
        # game extensions and missing-master diagnostics.
        state = Qt.CheckState.Checked if enabled else Qt.CheckState.Unchecked
        if not model.setData(index, state.value, Qt.ItemDataRole.CheckStateRole):
            raise ValueError('MO2 rejected the plugin activation change')

    def set_plugin_locked(self, name, locked):
        from PyQt6.QtCore import QCoreApplication, QEvent, QItemSelectionModel, QObject, QTimer, Qt
        from PyQt6.QtWidgets import QApplication, QLineEdit, QMenu, QTabWidget, QTreeView, QWidget
        if not isinstance(name, str) or type(locked) is not bool:
            raise ValueError('Select a plugin and its requested lock state')
        if not self.window.isEnabled(): raise ValueError('Close MO2’s current dialog first')
        rows = [p for p in self.plugin_snapshot() if p['name'] == name]
        if len(rows) != 1: raise ValueError('Plugin no longer exists')
        if rows[0]['locked'] == locked: return {'locked': locked}
        tree = self.window.findChild(QTreeView, 'espList')
        tabs = self.window.findChild(QTabWidget, 'tabWidget')
        page = self.window.findChild(QWidget, 'espTab')
        search = self.window.findChild(QLineEdit, 'espFilterEdit')
        if tree is None or tabs is None or page is None: raise ValueError('MO2 plugin controls are unavailable')
        previous = tabs.currentIndex(); old_search = search.text() if search else None
        selected = [str(i.data()) for i in tree.selectionModel().selectedRows()]
        scroll = tree.verticalScrollBar().value()
        expected = QCoreApplication.translate('PluginListContextMenu', 'Lock load order' if locked else 'Unlock load order')
        failures, invoked = [], []
        class Capture(QObject):
            armed = True
            def eventFilter(inner, watched, event):
                if inner.armed and isinstance(watched, QMenu) and event.type() == QEvent.Type.Polish:
                    inner.armed = False
                    watched.setAttribute(Qt.WidgetAttribute.WA_DontShowOnScreen, True)
                    def run():
                        actions = [a for a in watched.actions() if a.text().replace('&', '') == expected.replace('&', '') and a.isEnabled()]
                        watched.close()
                        if len(actions) != 1:
                            failures.append('MO2 does not allow changing this plugin lock'); return
                        invoked.append(True); actions[0].trigger()
                    QTimer.singleShot(0, run)
                return False
        app = QApplication.instance(); capture = Capture(); app.installEventFilter(capture)
        try:
            if search: search.clear()
            tabs.setCurrentWidget(page)
            model = tree.model()
            matches = [model.index(r, 0) for r in range(model.rowCount()) if model.index(r, 0).data() == name]
            if len(matches) != 1: raise ValueError('MO2 cannot resolve the selected plugin row')
            tree.selectionModel().clearSelection()
            tree.setCurrentIndex(matches[0]); tree.scrollTo(matches[0]); tree.doItemsLayout()
            tree.customContextMenuRequested.emit(tree.visualRect(matches[0]).center())
        finally:
            app.removeEventFilter(capture)
            if search is not None: search.setText(old_search)
            tabs.setCurrentIndex(previous)
            tree.selectionModel().clearSelection()
            model = tree.model()
            for r in range(model.rowCount()):
                index = model.index(r, 0)
                if str(index.data()) in selected: tree.selectionModel().select(index, QItemSelectionModel.SelectionFlag.Select | QItemSelectionModel.SelectionFlag.Rows)
            tree.verticalScrollBar().setValue(scroll)
        if failures: raise ValueError(failures[0])
        if not invoked or next((p['locked'] for p in self.plugin_snapshot() if p['name'] == name), None) != locked:
            raise ValueError('MO2 did not apply the requested plugin lock')
        return {'locked': locked}

    def connect_nexus_account(self, key, expected):
        """Validate and apply a key through the native connection UI, without mapping it."""
        from PyQt6.QtCore import QCoreApplication, QElapsedTimer, QEvent, QObject, QTimer, Qt
        from PyQt6.QtGui import QAction
        from PyQt6.QtWidgets import QApplication, QAbstractButton, QDialog, QLabel, QListWidget, QPlainTextEdit, QPushButton
        action = self.window.findChild(QAction, 'actionSettings')
        if not self.window.isEnabled() or action is None or not action.isEnabled():
            raise ValueError('MO2 is busy; wait before connecting its account')
        results, failures = [], []
        success = QCoreApplication.translate('NexusConnectionUI', 'Linked with Nexus successfully.')
        restart_title = QCoreApplication.translate('MainWindow', 'Restart Mod Organizer')
        continue_text = QCoreApplication.translate('MainWindow', 'Continue').replace('&', '')
        class Capture(QObject):
            entered = False
            settings = None
            def eventFilter(inner, watched, event):
                if not isinstance(watched, QDialog) or event.type() != QEvent.Type.Polish: return False
                if watched.objectName() == 'TaskDialog' and watched.windowTitle() == restart_title and results:
                    watched.setAttribute(Qt.WidgetAttribute.WA_DontShowOnScreen, True)
                    def continue_without_restart():
                        buttons = [b for b in watched.findChildren(QAbstractButton) if b.text().replace('&', '') == continue_text]
                        if len(buttons) == 1: buttons[0].click()
                        else:
                            failures.append('Unable to finish the native account confirmation')
                            watched.reject()
                    QTimer.singleShot(0, continue_without_restart)
                elif watched.objectName() == 'NexusManualKeyDialog'  and inner.settings is not None and not inner.entered:
                    inner.entered = True
                    watched.setAttribute(Qt.WidgetAttribute.WA_DontShowOnScreen, True)
                    def fill():
                        field = watched.findChild(QPlainTextEdit, 'key')
                        if field is None:
                            failures.append('This MO2 version does not expose manual account login')
                            watched.reject(); inner.settings.reject(); return
                        field.setPlainText(key)
                        watched.accept()
                        field.clear()
                    QTimer.singleShot(0, fill)
                elif watched.objectName() == 'SettingsDialog' and inner.settings is None:
                    inner.settings = watched
                    watched.setAttribute(Qt.WidgetAttribute.WA_DontShowOnScreen, True)
                    def begin():
                        manual = watched.findChild(QPushButton, 'nexusManualKey')
                        disconnect = watched.findChild(QPushButton, 'nexusDisconnect')
                        log = watched.findChild(QListWidget, 'nexusLog')
                        user = watched.findChild(QLabel, 'nexusUserID')
                        if any(x is None for x in (manual, disconnect, log, user)) or not (manual.isEnabled() or disconnect.isEnabled()):
                            failures.append('Native MO2 account login is unavailable or busy'); watched.reject(); return
                        elapsed = QElapsedTimer(); elapsed.start()
                        timer = QTimer(watched); timer.setInterval(100)
                        def poll():
                            # Compare a known success marker; never return or log native log text.
                            linked = any(log.item(i).text() == success for i in range(log.count()))
                            identity = user.text().strip()
                            if linked and identity.isdecimal() and int(identity) == expected['userId'] and disconnect.isEnabled():
                                results.append({'connected': True, 'account': expected})
                                timer.stop(); watched.reject()
                            elif elapsed.elapsed() > 20000 or failures:
                                failures.append('MO2 did not finish connecting the requested Nexus account')
                                # Native manual action doubles as cancel while validation runs.
                                if inner.entered and manual.isEnabled() and not disconnect.isEnabled(): manual.click()
                                timer.stop(); watched.reject()
                        timer.timeout.connect(poll); timer.start()
                        # A connected account disables the manual button, but its native slot
                        # accepts a replacement and preserves the old credential on failure.
                        manual.clicked.emit()
                        if not inner.entered:
                            failures.append('MO2 did not open its manual account dialog')
                    QTimer.singleShot(0, begin)
                return False
        app = QApplication.instance(); capture = Capture(); app.installEventFilter(capture)
        try: action.trigger()
        finally:
            app.removeEventFilter(capture)
        if failures or len(results) != 1:
            raise ValueError(failures[0] if failures else 'Native MO2 account login did not complete')
        return results[0]

    def nexus_account_state(self, disconnect_account=False):
        """Read the original client's account identity without exposing its settings."""
        from PyQt6.QtCore import QCoreApplication, QElapsedTimer, QEvent, QObject, QTimer, Qt
        from PyQt6.QtGui import QAction
        from PyQt6.QtWidgets import QApplication, QAbstractButton, QDialog, QLabel, QPushButton
        action = self.window.findChild(QAction, 'actionSettings')
        if not self.window.isEnabled() or action is None or not action.isEnabled():
            raise ValueError('MO2 is busy; wait before reading its account state')
        results, failures = [], []
        restart_title = QCoreApplication.translate('MainWindow', 'Restart Mod Organizer')
        continue_text = QCoreApplication.translate('MainWindow', 'Continue').replace('&', '')
        class Capture(QObject):
            def eventFilter(inner, watched, event):
                if not isinstance(watched, QDialog) or event.type() != QEvent.Type.Polish: return False
                if watched.objectName() == 'TaskDialog' and watched.windowTitle() == restart_title and results and disconnect_account:
                    watched.setAttribute(Qt.WidgetAttribute.WA_DontShowOnScreen, True)
                    def finish():
                        buttons = [b for b in watched.findChildren(QAbstractButton) if b.text().replace('&', '') == continue_text]
                        if len(buttons) == 1: buttons[0].click()
                        else:
                            failures.append('Unable to finish the native account confirmation')
                            watched.reject()
                    QTimer.singleShot(0, finish)
                elif watched.objectName() == 'SettingsDialog':
                    watched.setAttribute(Qt.WidgetAttribute.WA_DontShowOnScreen, True)
                    elapsed = QElapsedTimer(); elapsed.start()
                    timer = QTimer(watched); timer.setInterval(50)
                    requested = False
                    def read():
                        nonlocal requested
                        try:
                            user = watched.findChild(QLabel, 'nexusUserID')
                            name = watched.findChild(QLabel, 'nexusName')
                            disconnect = watched.findChild(QPushButton, 'nexusDisconnect')
                            if user is None or name is None or disconnect is None:
                                raise ValueError('This MO2 version does not expose its account state')
                            if disconnect_account and not requested:
                                requested = True
                                if disconnect.isEnabled():
                                    disconnect.click()
                                    # Native identity labels update via a queued signal.
                                    return
                            identity = user.text().strip()
                            connected = identity.isdecimal() and int(identity) > 0 and disconnect.isEnabled()
                            if disconnect_account and (connected or disconnect.isEnabled()):
                                if elapsed.elapsed() < 3000: return
                                raise ValueError('MO2 did not finish disconnecting its Nexus account')
                            results.append({'connected': connected, 'userId': int(identity) if connected else None,
                                            'name': name.text() if connected else None})
                        except Exception as error: failures.append(str(error))
                        if results or failures:
                            timer.stop(); watched.reject()
                    timer.timeout.connect(read); timer.start()
                return False
        app = QApplication.instance(); capture = Capture(); app.installEventFilter(capture)
        try: action.trigger()
        finally: app.removeEventFilter(capture)
        if failures: raise ValueError(failures[0])
        if len(results) != 1: raise ValueError('MO2 account state did not complete')
        return results[0]

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

    def edit_categories(self):
        """Press MO2's own Edit... beside its filter list.

        That button opens MO2's category editor, which owns the category
        definitions the frontend only reads. MO2 is shown while its dialog is up,
        the way opening a mod's details already does, and the call returns when
        the dialog closes so the frontend can re-read what changed.
        """
        from PyQt6.QtCore import QEventLoop
        from PyQt6.QtWidgets import QDialog, QPushButton
        button = self.window.findChild(QPushButton, 'filtersEdit')
        if button is None or not self.window.isEnabled() or not button.isEnabled():
            raise ValueError('MO2 cannot edit its categories right now')
        visible = self.window.isVisible()
        self.window.show()
        self.window.raise_()
        try:
            button.click()
            for dialog in self.window.findChildren(QDialog):
                if not dialog.isVisible():
                    continue
                loop = QEventLoop()
                dialog.finished.connect(loop.quit)
                if dialog.isVisible():
                    loop.exec()
        finally:
            if not visible: self.window.hide()
        return {'edited': True}

    def mod_path(self, name):
        """Where MO2 keeps a mod, for the frontend's own Open in Explorer.

        MO2's context menu opens the folder through Windows; the frontend is
        running outside the prefix and opens it with the desktop it is on, so it
        asks MO2 where the folder is rather than asking MO2 to open it.
        """
        mods = self.organizer.modList()
        if not isinstance(name, str) or name not in mods.allMods():
            raise ValueError('Mod no longer exists')
        return {'name': name, 'path': mods.getMod(name).absolutePath()}

    def rename_mod(self, name, new_name):
        """Rename a mod through the original list model's own editor.

        The same route as renaming a separator: MO2 owns the folder rename, the
        meta update and the modRenamed notifications every profile listens to.
        """
        from PyQt6.QtCore import QAbstractProxyModel, Qt
        from PyQt6.QtWidgets import QTreeView
        mods = self.organizer.modList()
        names = list(mods.allMods())
        if not isinstance(name, str) or name not in names:
            raise ValueError('Mod no longer exists')
        if mods.getMod(name).isSeparator():
            return self.rename_separator(name, new_name)
        if not self.window.isEnabled(): raise ValueError('MO2 is busy')
        if (not isinstance(new_name, str) or not new_name.strip() or len(new_name) > 120
                or any(ord(c) < 32 or c in '<>:"/\\|?*' for c in new_name) or new_name.endswith(('.', ' '))):
            raise ValueError('Choose a valid mod name (up to 120 characters)')
        if new_name.casefold() in {n.casefold() for n in names if n != name}:
            raise ValueError('A mod with this name already exists')
        view = self.window.findChild(QTreeView, 'modList')
        if view is None or not view.isEnabled(): raise ValueError('MO2 mod list is unavailable')
        model = view.model()
        while isinstance(model, QAbstractProxyModel): model = model.sourceModel()
        if model is None or model.metaObject().className() != 'ModList':
            raise ValueError('Unsupported MO2 mod list model')
        row = names.index(name)
        index = model.index(row, 0)
        if not index.isValid() or index.data(int(Qt.ItemDataRole.UserRole) + 1) != row:
            raise ValueError('MO2 mod row changed; refresh before renaming')
        view.selectionModel().clear()
        accepted = model.setData(index, new_name, Qt.ItemDataRole.EditRole)
        if not accepted or new_name not in mods.allMods():
            raise ValueError('MO2 did not rename the mod')
        return {'renamed': True, 'oldName': name, 'name': new_name}

    def rename_separator(self, name, collection_name):
        from PyQt6.QtCore import QAbstractProxyModel, Qt
        from PyQt6.QtWidgets import QTreeView
        mods = self.organizer.modList()
        names = list(mods.allMods())
        if not isinstance(name, str) or name not in names or not mods.getMod(name).isSeparator():
            raise ValueError('Collection separator no longer exists')
        if not self.window.isEnabled(): raise ValueError('MO2 is busy')
        if not isinstance(collection_name, str) or not collection_name.strip() or len(collection_name) > 120 or any(ord(c) < 32 or c in '<>:"/\\|?*' for c in collection_name) or collection_name.endswith(('.', ' ')):
            raise ValueError('Choose a valid collection name (up to 120 characters)')
        new_name = collection_name + '_separator'
        existing = {n.casefold() for n in names if n != name}
        if collection_name.casefold() in existing or new_name.casefold() in existing:
            raise ValueError('A mod or collection with this name already exists')
        view = self.window.findChild(QTreeView, 'modList')
        if view is None or not view.isEnabled(): raise ValueError('MO2 mod list is unavailable')
        model = view.model()
        while isinstance(model, QAbstractProxyModel): model = model.sourceModel()
        if model is None or model.metaObject().className() != 'ModList':
            raise ValueError('Unsupported MO2 mod list model')
        row = names.index(name)
        index = model.index(row, 0)
        if not index.isValid() or index.data(int(Qt.ItemDataRole.UserRole) + 1) != row:
            raise ValueError('MO2 mod row changed; refresh before renaming')
        # The original inline editor owns suffix handling, directory rename,
        # and modRenamed notifications that update every native profile.
        view.selectionModel().clear()
        accepted = model.setData(index, collection_name, Qt.ItemDataRole.EditRole)
        if not accepted or new_name not in mods.allMods():
            raise ValueError('MO2 did not rename the collection')
        return {'renamed': True, 'oldName': name, 'name': new_name}

    def set_mod_color(self, name, color):
        """Give a mod or separator the colour MO2 draws its row in.

        Through MO2's own "Select Color..." action rather than by writing the
        colour into meta.ini: that action is what remembers the last colour
        chosen, writes the file the way MO2 reads it back, and refreshes the
        list. The colour dialog it opens is answered here, off screen, the same
        way the separator creation above answers MO2's name prompt.

        A colour of None takes MO2's "Reset Color", which is the action its own
        menu offers on a mod that has one.
        """
        from PyQt6.QtCore import QAbstractProxyModel, QCoreApplication, QEvent, QObject, QItemSelectionModel, QPoint, QTimer, Qt
        from PyQt6.QtGui import QColor
        from PyQt6.QtWidgets import QApplication, QColorDialog, QMenu, QTreeView
        mods = self.organizer.modList()
        names = list(mods.allMods())
        if not isinstance(name, str) or name not in names:
            raise ValueError('Mod no longer exists')
        chosen = None
        if color is not None:
            if not isinstance(color, str) or not QColor(color).isValid():
                raise ValueError('Choose a valid colour')
            chosen = QColor(color)
        if not self.window.isEnabled(): raise ValueError('MO2 is busy')
        view = self.window.findChild(QTreeView, 'modList')
        if view is None or not view.isEnabled(): raise ValueError('MO2 mod list is unavailable')
        model = view.model()
        while isinstance(model, QAbstractProxyModel): model = model.sourceModel()
        if model is None or model.metaObject().className() != 'ModList':
            raise ValueError('Unsupported MO2 mod list model')
        row = names.index(name)
        index = model.index(row, 0)
        if not index.isValid() or index.data(int(Qt.ItemDataRole.UserRole) + 1) != row:
            raise ValueError('MO2 mod row changed; refresh before colouring')
        # The menu acts on the view's selection, so the row is selected through the
        # view's own proxy chain rather than on the source model.
        mapped = index
        proxy = view.model()
        chain = []
        while isinstance(proxy, QAbstractProxyModel):
            chain.append(proxy); proxy = proxy.sourceModel()
        for step in reversed(chain):
            mapped = step.mapFromSource(mapped)
            if not mapped.isValid(): raise ValueError('MO2 is not showing this mod; clear its filters first')
        view.selectionModel().select(mapped, QItemSelectionModel.SelectionFlag.ClearAndSelect | QItemSelectionModel.SelectionFlag.Rows)
        view.selectionModel().setCurrentIndex(mapped, QItemSelectionModel.SelectionFlag.NoUpdate)
        wanted = QCoreApplication.translate('ModListContextMenu', 'Select Color...' if chosen is not None else 'Reset Color').replace('&', '')
        invoked, errors = [], []

        class Capture(QObject):
            armed = True
            answered = False
            def eventFilter(inner, watched, event):
                if chosen is not None and not inner.answered and isinstance(watched, QColorDialog) and event.type() == QEvent.Type.Polish:
                    inner.answered = True
                    watched.setAttribute(Qt.WidgetAttribute.WA_DontShowOnScreen, True)
                    def answer():
                        watched.setCurrentColor(chosen)
                        watched.accept()
                    QTimer.singleShot(0, answer)
                if inner.armed and isinstance(watched, QMenu) and event.type() == QEvent.Type.Polish:
                    inner.armed = False
                    watched.setAttribute(Qt.WidgetAttribute.WA_DontShowOnScreen, True)
                    def run():
                        actions = [a for a in watched.actions() if a.text().replace('&', '') == wanted and a.isEnabled()]
                        watched.close()
                        if len(actions) != 1:
                            errors.append('MO2 does not offer that colour action for this mod')
                            return
                        invoked.append(True)
                        actions[0].trigger()
                    QTimer.singleShot(0, run)
                return False

        app = QApplication.instance(); capture = Capture(); app.installEventFilter(capture)
        visible = self.window.isVisible()
        try:
            rect = view.visualRect(mapped)
            view.customContextMenuRequested.emit(rect.center() if rect.isValid() else QPoint(0, 0))
        finally:
            app.removeEventFilter(capture)
            if not visible: self.window.hide()
        if errors: raise ValueError(errors[0])
        if not invoked: raise ValueError('MO2 did not open its mod menu')
        return {'name': name, 'color': background_color(model, row, 0)}

    def remove_separator(self, name):
        mods = self.organizer.modList()
        if not isinstance(name, str) or name not in mods.allMods() or not mods.getMod(name).isSeparator():
            raise ValueError('Separator no longer exists')
        return self.remove_confirmed(name)

    def remove_confirmed(self, name):
        from PyQt6.QtCore import QCoreApplication, QEvent, QObject, QTimer, Qt
        from PyQt6.QtWidgets import QApplication, QMessageBox
        mods = self.organizer.modList()
        if not isinstance(name, str) or name not in mods.allMods() :
            raise ValueError('Mod no longer exists')
        if not self.window.isEnabled(): raise ValueError('MO2 is busy')
        title = QCoreApplication.translate('ModList', 'Confirm')
        display_name = next(m['displayName'] for m in self.snapshot() if m['name'] == name)
        messages = {QCoreApplication.translate('ModList', text).replace('%1', display_name) for text in (
            'Are you sure you want to remove "%1"?', "Are you sure you want to remove '%1'?")}
        captured = []
        class Capture(QObject):
            def eventFilter(inner, watched, event):
                if isinstance(watched, QMessageBox) and event.type() == QEvent.Type.Polish and watched.windowTitle() == title and watched.text() in messages:
                    watched.setAttribute(Qt.WidgetAttribute.WA_DontShowOnScreen, True)
                    def confirm():
                        # The frontend already confirmed removal of this specific
                        # mod. Never acknowledge other native dialogs.
                        captured.append(True)
                        watched.done(int(QMessageBox.StandardButton.Yes))
                    QTimer.singleShot(0, confirm)
                return False
        app = QApplication.instance(); capture = Capture(); app.installEventFilter(capture)
        try: result = self.remove(name)
        finally: app.removeEventFilter(capture)
        if not captured: raise ValueError('MO2 did not present the expected mod confirmation')
        return result

    def remove(self, name):
        from PyQt6.QtCore import QAbstractProxyModel, Qt
        from PyQt6.QtWidgets import QTreeView

        mods = self.organizer.modList()
        names = list(mods.allMods())
        if not isinstance(name, str) or name not in names:
            raise ValueError('Mod no longer exists')
        state = mods.state(name)
        if int(getattr(state, 'value', state)) & 4 and not mods.getMod(name).isSeparator():
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
        # A separator may still be selected after native creation. Clear the
        # view selection before removal so its delayed marker refresh cannot
        # retain the removed ModInfo index.
        view.selectionModel().clear()
        model.removeRow(row)
        return {'removed': name not in mods.allMods(), 'modName': name}


def newest_version(mod):
    """Newest version MO2 knows about, or '' when it has none or cannot report one."""
    try:
        value = mod.newestVersion()
    except Exception:
        return ''
    if value is None:
        return ''
    text = getattr(value, 'displayString', None)
    return str(text() if callable(text) else value)
