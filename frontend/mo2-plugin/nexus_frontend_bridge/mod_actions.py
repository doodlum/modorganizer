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


def filter_tree(tree, user_role):
    """Serialize FilterList::CriteriaItem IDs/types without changing its state."""
    def read(item):
        return {'id': int(item.data(0, user_role)),
                'type': int(item.data(0, user_role + 1)),
                'name': item.text(1),
                'children': [read(item.child(i)) for i in range(item.childCount())]}
    return [read(tree.topLevelItem(i)) for i in range(tree.topLevelItemCount())]


def category_names(model, row, column, grouping_role):
    """Read the native category list without splitting its primary display text."""
    if column is None:
        return []
    value = model.index(row, column).data(grouping_role)
    if value is None:
        return []
    if not isinstance(value, (list, tuple)) or any(not isinstance(name, str) for name in value):
        raise ValueError('MO2 returned an invalid category membership list')
    return list(value)


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

    def _select_rows(self, view_name, model_name, all_names, names, subject):
        """Select rows in one of MO2's own list views and return it with the lead.

        Its context menus act on the view's selection, so a menu action is reached by
        selecting through the view's proxy chain exactly as a click would.
        """
        from PyQt6.QtCore import QAbstractProxyModel, QItemSelectionModel, Qt
        from PyQt6.QtWidgets import QTreeView
        if not isinstance(names, list) or not names or any(not isinstance(n, str) or n not in all_names for n in names):
            raise ValueError(subject + ' no longer exists; refresh the list')
        if not self.window.isEnabled(): raise ValueError('Close MO2’s current dialog first')
        view = self.window.findChild(QTreeView, view_name)
        if view is None or not view.isEnabled(): raise ValueError('MO2 ' + subject.lower() + ' list is unavailable')
        model = view.model()
        while isinstance(model, QAbstractProxyModel): model = model.sourceModel()
        if model is None or model.metaObject().className() != model_name:
            raise ValueError('Unsupported MO2 ' + subject.lower() + ' list model')
        chain = []
        proxy = view.model()
        while isinstance(proxy, QAbstractProxyModel):
            chain.append(proxy); proxy = proxy.sourceModel()
        # The mod list's rows are its own mod order and it numbers them in this role;
        # the plugin list's rows are the load order and are named in the first column,
        # so each list is resolved the way it reports itself.
        rows = {name: model.index(row, 0) for row, name in enumerate(all_names)} if model_name == 'ModList' else {
            str(model.index(row, 0).data(Qt.ItemDataRole.DisplayRole)): model.index(row, 0)
            for row in range(model.rowCount())}
        return ModActions._select_indexes(view, chain, rows, names, subject, numbered=model_name == 'ModList')

    @staticmethod
    def _select_indexes(view, chain, rows, names, subject, numbered=False):
        """Select the rows named, given where each one sits in the list's own model.

        A row reached by name need not be a top-level one: MO2 keeps each archive
        under the mod that supplies it. The branch holding a row is opened and the row
        scrolled to if the view is not already drawing it, because the menu is opened
        at the rectangle the view draws that row at — a row with no rectangle would
        open the list's menu on nothing.
        """
        from PyQt6.QtCore import QItemSelectionModel, Qt
        lead = None
        view.selectionModel().clear()
        for name in names:
            if name not in rows: raise ValueError(subject + ' no longer exists; refresh the list')
            index = rows[name]
            if not index.isValid() or (numbered and index.data(int(Qt.ItemDataRole.UserRole) + 1) != index.row()):
                raise ValueError('MO2 ' + subject.lower() + ' row changed; refresh the list')
            mapped = index
            for step in reversed(chain):
                mapped = step.mapFromSource(mapped)
                if not mapped.isValid(): raise ValueError('MO2 is not showing this ' + subject.lower() + '; clear its filters first')
            branch = mapped.parent()
            while branch.isValid():
                view.expand(branch)
                branch = branch.parent()
            view.selectionModel().select(mapped, QItemSelectionModel.SelectionFlag.Select | QItemSelectionModel.SelectionFlag.Rows)
            if lead is None: lead = mapped
        view.selectionModel().setCurrentIndex(lead, QItemSelectionModel.SelectionFlag.NoUpdate)
        if view.visualRect(lead).isEmpty(): view.scrollTo(lead)
        return view, lead

    def _select_mods(self, names):
        return self._select_rows('modList', 'ModList', list(self.organizer.modList().allMods()), names, 'Mod')

    def _select_plugins(self, names):
        return self._select_rows('espList', 'PluginList', list(self.organizer.pluginList().pluginNames()), names, 'Plugin')

    @staticmethod
    def _submenu(action):
        """The menu an entry opens onto, filled the way showing it would fill it.

        Its category menus are push buttons in a widget action rather than ordinary
        submenus (ModListContextMenu::addMenuAsPushButton), so both shapes are read.
        MO2's All Mods menu builds itself on aboutToShow and is empty until then, so
        that signal is emitted before it is read or searched.
        """
        from PyQt6.QtWidgets import QPushButton, QWidgetAction
        child, text = action.menu(), action.text()
        if isinstance(action, QWidgetAction) and child is None:
            button = action.defaultWidget()
            if isinstance(button, QPushButton):
                text, child = button.text(), button.menu()
        if child is not None and not child.actions():
            child.aboutToShow.emit()
        return text.replace('&', ''), child

    @staticmethod
    def _menu_entries(menu):
        """MO2's menu as plain data: what it holds, in the order it holds it."""
        entries = []
        for action in menu.actions():
            if action.isSeparator():
                entries.append({'separator': True})
                continue
            text, child = ModActions._submenu(action)
            entry = {'text': text, 'enabled': action.isEnabled(),
                     'checkable': action.isCheckable(), 'checked': action.isChecked()}
            if child is not None:
                entry['items'] = ModActions._menu_entries(child)
            entries.append(entry)
        return entries

    def plugin_menu(self, names, path=None):
        """The same for MO2's plugin list, which carries its own menu."""
        return self._list_menu(self._select_plugins, names, path)

    def mod_menu(self, names, path=None):
        """Read, or trigger one entry of, MO2's own mod context menu.

        With no path this reports what MO2 would show for the selection, which is
        what the frontend's own menu is checked against. With one it triggers MO2's
        action of that name — so the wording, the conditions and the work itself are
        all MO2's, and nothing about what those actions do is reimplemented here.
        """
        return self._list_menu(self._select_mods, names, path)

    # MO2's other four lists, by the name each is built under in mainwindow.ui and
    # the model behind it. Every one of them answers customContextMenuRequested the
    # same way the mod and plugin lists do — downloadView through DownloadListView,
    # dataTree through FileTree, bsaList through the main window, savegameList
    # through SavesTab — so the same capture reads what MO2 would have shown.
    # Both tree widgets keep their rows in QTreeWidget's own model.
    FILE_LISTS = {
        'downloads': ('downloadView', 'DownloadList', 'Download'),
        'data': ('dataTree', 'FileTreeModel', 'Data file'),
        'archives': ('bsaList', 'QTreeModel', 'Archive'),
        'saves': ('savegameList', 'QTreeModel', 'Save'),
    }

    def activate_data_file(self, name):
        """Use FileTree::activate, including MO2's preview preference/fallback."""
        if not isinstance(name, str) or not name:
            raise ValueError('Choose a Data file to activate')
        self._restore_to = None
        try:
            view, index = self._select_listed('data', [name])
            view.activated.emit(index)
            return {'activated': name}
        finally:
            ModActions._restore_tab(self.window, self._restore_to)
            self._restore_to = None

    def file_menu(self, view, names, path=None):
        """Read, or trigger one entry of, the menu MO2 puts on one of its file lists.

        The mod and plugin menus were the only ones the frontend could compare against
        MO2, so the other four were checked against entries written down in the check
        instead — which cannot notice MO2 gaining an entry, nor this frontend offering
        one MO2 does not.
        """
        if view not in ModActions.FILE_LISTS:
            raise ValueError('Unknown MO2 list')
        self._restore_to = None
        try:
            return self._list_menu(lambda wanted: self._select_listed(view, wanted), names, path)
        finally:
            ModActions._restore_tab(self.window, self._restore_to)
            self._restore_to = None


    def _show_owning_tab(self, view):
        """Bring the tab holding a view to the front, and say what was there before.

        MO2 fills bsaList and savegameList when their tab is shown and not before, so
        both answered with nothing at all while the frontend was driving them: their
        menus cannot be read off an empty list. downloadView and dataTree are already
        populated, and switching to their tab costs nothing.
        """
        from PyQt6.QtWidgets import QApplication, QTabWidget
        tabs = self.window.findChild(QTabWidget, 'tabWidget')
        if tabs is None: return None
        page = view
        while page is not None and tabs.indexOf(page) < 0:
            page = page.parentWidget()
        if page is None: return None
        previous = tabs.currentWidget()
        if page is not previous:
            tabs.setCurrentWidget(page)
            QApplication.processEvents()
        return previous

    @staticmethod
    def _restore_tab(window, previous):
        from PyQt6.QtWidgets import QApplication, QTabWidget
        if previous is None: return
        tabs = window.findChild(QTabWidget, 'tabWidget')
        if tabs is not None and tabs.currentWidget() is not previous:
            tabs.setCurrentWidget(previous)
            QApplication.processEvents()

    @staticmethod
    def _listed_rows(model, view):
        """Each row of one of those lists, under the name the frontend knows it by.

        Two of the four do not name a row by the first column of a top-level row, and
        a comparison that asks MO2 about a row it does not hold compares two different
        menus — or none at all.

        MO2's archive list is a tree of mods with their archives beneath, so the rows
        the Archives page lists are its children (MainWindow::refreshBSAList builds the
        mod as the top-level item and adds each archive under it). And MO2 labels a
        save with a composite caption — character, slot and place — which two saves can
        share, and this profile's two do; the file sits in the second column
        (SavesTab::refreshSaveList writes both), which is what the Saves page names a
        row by and what MO2's own save actions match on.
        """
        from PyQt6.QtCore import Qt
        rows = {}
        if view == 'archives':
            for owner in range(model.rowCount()):
                parent = model.index(owner, 0)
                for child in range(model.rowCount(parent)):
                    index = model.index(child, 0, parent)
                    rows.setdefault(str(index.data(Qt.ItemDataRole.DisplayRole)), index)
        elif view == 'saves':
            for row in range(model.rowCount()):
                rows.setdefault(str(model.index(row, 1).data(Qt.ItemDataRole.DisplayRole)), model.index(row, 0))
        else:
            for row in range(model.rowCount()):
                index = model.index(row, 0)
                rows.setdefault(str(index.data(Qt.ItemDataRole.DisplayRole)), index)
        return rows

    def _list_model(self, view, subject):
        """The view for one of those lists with its own model, its tab brought forward."""
        from PyQt6.QtCore import QAbstractProxyModel
        from PyQt6.QtWidgets import QTreeView
        view_name, model_name = ModActions.FILE_LISTS[view][0], ModActions.FILE_LISTS[view][1]
        found = self.window.findChild(QTreeView, view_name)
        if found is None: raise ValueError('MO2 ' + subject.lower() + ' list is unavailable')
        # Left showing: the menu is opened on this view afterwards, and a view on a
        # hidden tab has no row rectangle to open it at. The caller puts the tab back.
        previous = self._show_owning_tab(found)
        try:
            model, chain = found.model(), []
            while isinstance(model, QAbstractProxyModel):
                chain.append(model); model = model.sourceModel()
            if model is None: raise ValueError('MO2 ' + subject.lower() + ' list has no model')
            # Held to the model MO2 is expected to be using rather than to whatever it
            # turns out to have: passing the model's own name back would agree with
            # anything, including a list this was never written for.
            name = model.metaObject().className()
            if name != model_name:
                raise ValueError('Unsupported MO2 ' + subject.lower() + ' list model: ' + name)
        except Exception:
            # The tab was brought forward to read this list; it goes back whether or
            # not the list turned out to be the one expected.
            ModActions._restore_tab(self.window, previous)
            raise
        return found, model, chain, previous

    @staticmethod
    def _data_rows(model, names):
        """Resolve Data-relative paths without traversing unrelated game folders.

        FileTreeModel populates children lazily. A basename lookup at the root
        can select a different file when the frontend is inside a subfolder.
        Keep the complete path as the key expected by _select_indexes.
        """
        from PyQt6.QtCore import QModelIndex, Qt
        rows = {}
        for name in names:
            parts = name.replace('\\', '/').split('/')
            if any(part in ('', '.', '..') or ':' in part for part in parts):
                raise ValueError('Invalid Data-relative path')
            parent = QModelIndex()
            for part in parts:
                if model.canFetchMore(parent):
                    model.fetchMore(parent)
                matches = []
                for row in range(model.rowCount(parent)):
                    index = model.index(row, 0, parent)
                    if str(index.data(Qt.ItemDataRole.DisplayRole)).casefold() == part.casefold():
                        matches.append(index)
                if len(matches) != 1:
                    raise ValueError('Data file no longer exists or is ambiguous; refresh the list')
                parent = matches[0]
            rows[name] = parent
        return rows

    def _select_listed(self, view, names):
        """Select by the name the frontend's own page gives that row."""
        subject = ModActions.FILE_LISTS[view][2]
        found, model, chain, previous = self._list_model(view, subject)
        self._restore_to = previous
        if view == 'data' and names == []:
            # Qt's Data menu exposes whole-tree actions even without a selection.
            from PyQt6.QtCore import QModelIndex, QItemSelectionModel
            selection = found.selectionModel()
            selection.clear()
            empty = QModelIndex()
            selection.setCurrentIndex(empty, QItemSelectionModel.SelectionFlag.NoUpdate)
            return found, empty
        if not isinstance(names, list) or not names or any(not isinstance(n, str) for n in names):
            raise ValueError(subject + ' no longer exists; refresh the list')
        rows = ModActions._data_rows(model, names) if view == 'data' else ModActions._listed_rows(model, view)
        return ModActions._select_indexes(found, chain, rows, names, subject)

    def file_list_rows(self, view):
        """What MO2 is showing in one of those lists, so a row can be asked for by name."""
        if view not in ModActions.FILE_LISTS: raise ValueError('Unknown MO2 list')
        subject = ModActions.FILE_LISTS[view][2]
        previous = None
        try:
            _, model, _, previous = self._list_model(view, subject)
            return {'rows': list(ModActions._listed_rows(model, view)),
                    'model': model.metaObject().className()}
        finally:
            ModActions._restore_tab(self.window, previous)

    def _list_menu(self, select, names, path=None):
        from PyQt6.QtCore import QEvent, QObject, QPoint, QTimer, Qt
        from PyQt6.QtWidgets import QApplication, QMenu
        # One entry, or a choice of spellings for the same entry: MO2 renames several
        # of its own depending on where the menu was opened and whether a filter is
        # on, so the frontend offers every name MO2 uses for one action and the one
        # MO2 actually built is the one taken.
        if path is not None and isinstance(path, list) and path and all(isinstance(step, str) for step in path):
            path = [path]
        if path is not None and (not isinstance(path, list) or not path or any(
                not isinstance(option, list) or not option or any(not isinstance(step, str) for step in option)
                for option in path)):
            raise ValueError('Choose a menu entry to trigger')
        view, lead = select(names)
        read, errors = [], []

        def find(entries_menu, steps):
            for action in entries_menu.actions():
                if action.isSeparator(): continue
                text, child = ModActions._submenu(action)
                if text != steps[0]: continue
                if len(steps) == 1: return action
                if child is not None: return find(child, steps[1:])
            return None

        class Capture(QObject):
            armed = True
            def eventFilter(inner, watched, event):
                if not inner.armed or not isinstance(watched, QMenu) or event.type() != QEvent.Type.Polish:
                    return False
                inner.armed = False
                watched.setAttribute(Qt.WidgetAttribute.WA_DontShowOnScreen, True)
                def run():
                    try:
                        if path is None:
                            read.append(ModActions._menu_entries(watched))
                            return
                        found = [action for action in (find(watched, option) for option in path)
                                 if action is not None and action.isEnabled()]
                        if not found:
                            errors.append('MO2 does not offer ' + ' → '.join(path[0]) + ' for this selection')
                            return
                        read.append(True)
                        found[0].trigger()
                    except Exception as error:
                        errors.append(str(error))
                    finally:
                        watched.close()
                QTimer.singleShot(0, run)
                return False

        app = QApplication.instance(); capture = Capture(); app.installEventFilter(capture)
        visible = self.window.isVisible()
        try:
            rect = view.visualRect(lead)
            view.customContextMenuRequested.emit(rect.center() if rect.isValid() else QPoint(0, 0))
        finally:
            app.removeEventFilter(capture)
            if not visible: self.window.hide()
        if errors: raise ValueError(errors[0])
        if not read: raise ValueError('MO2 did not open that list’s menu')
        return {'entries': read[0]} if path is None else {'triggered': path[0]}

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
                # GroupingRole is Qt::UserRole and carries every category name,
                # unlike DisplayRole, which contains only the primary category.
                'categories': category_names(model, row, columns.get('Category'), Qt.ItemDataRole.UserRole),
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
                # What MO2's own mod menu branches on, so the frontend can offer the
                # entries MO2 would offer rather than a fixed list. Read off the mod
                # itself; an MO2 that does not expose one leaves it out.
                **mod_menu_state(mods.getMod(name)),
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

    def filter_snapshot(self):
        from PyQt6.QtCore import Qt
        from PyQt6.QtWidgets import QTreeWidget
        tree = self.window.findChild(QTreeWidget, 'filters')
        if tree is None:
            raise ValueError('MO2 category filter tree is unavailable')
        return filter_tree(tree, int(Qt.ItemDataRole.UserRole))

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


def mod_menu_state(mod):
    """The conditions MO2's own mod menu branches on, read off the mod.

    MO2 decides what a mod's menu holds from these (modlistcontextmenu.cpp): a mod
    it knows on Nexus gets the two links, the update actions and the endorsement
    entries; one installed for another game gets Mark as converted; one it could
    not read gets Ignore missing data. Anything an MO2 build does not expose is
    left out rather than guessed at, and the frontend leaves that entry out too.
    """
    def read(call, default=None):
        try:
            value = call()
        except Exception:
            return default
        return value if value is not None else default
    def name_of(value, default=''):
        if value is None: return default
        return str(getattr(value, 'name', value)).rsplit('.', 1)[-1]
    url = read(mod.url, '') or ''
    return {
        'foreign': bool(read(mod.isForeign, False)),
        # ENDORSED_TRUE / ENDORSED_FALSE / ENDORSED_NEVER / ENDORSED_UNKNOWN, and
        # TRACKED_TRUE / TRACKED_FALSE / TRACKED_UNKNOWN, under MO2's own names.
        'endorsed': name_of(read(mod.endorsedState)),
        'tracked': name_of(read(mod.trackedState)),
        'ignoredVersion': str(read(mod.ignoredVersion, '') or ''),
        'validated': bool(read(mod.validated, True)),
        'converted': bool(read(mod.converted, True)),
        'url': url,
    }


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
