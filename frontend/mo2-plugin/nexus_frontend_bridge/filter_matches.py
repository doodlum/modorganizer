"""Evaluate native criteria synchronously, restoring the host's filter/view state."""


def read_matches(window, organizer, criteria):
    from PyQt6.QtCore import QAbstractProxyModel, QEvent, QItemSelectionModel, QModelIndex, Qt
    from PyQt6.QtGui import QKeyEvent
    from PyQt6.QtWidgets import QApplication, QComboBox, QLineEdit, QRadioButton, QTreeView, QTreeWidget
    import time

    if not window.isEnabled():
        raise ValueError('Close the native MO2 dialog before reading filter matches')
    if not isinstance(criteria, list) or len(criteria) > 128:
        raise ValueError('Expected up to 128 native filter criteria')
    tree = window.findChild(QTreeWidget, 'filters')
    view = window.findChild(QTreeView, 'modList')
    search = window.findChild(QLineEdit, 'modFilterEdit')
    mode_and = window.findChild(QRadioButton, 'filtersAnd')
    mode_or = window.findChild(QRadioButton, 'filtersOr')
    separators = window.findChild(QComboBox, 'filtersSeparators')
    if any(x is None for x in (tree, view, search, mode_and, mode_or, separators)):
        raise ValueError('MO2 native filter controls are unavailable')
    role = int(Qt.ItemDataRole.UserRole)
    items = []
    def walk(item):
        items.append(item)
        for i in range(item.childCount()): walk(item.child(i))
    for i in range(tree.topLevelItemCount()): walk(tree.topLevelItem(i))
    by_key = {(int(x.data(0, role + 1)), int(x.data(0, role))): x for x in items}
    requested = []
    for criterion in criteria:
        if not isinstance(criterion, dict) or set(criterion) != {'type', 'id'} or type(criterion.get('type')) is not int or type(criterion.get('id')) is not int:
            raise ValueError('Native filter criteria require integer type and ID')
        key = (criterion['type'], criterion['id'])
        if key not in by_key or key in requested:
            raise ValueError('Native filter criterion is missing or duplicated; refresh the list')
        requested.append(key)
    if not requested: return {'criteria': [], 'restored': True, 'elapsedMs': 0}

    chain = []
    model = view.model()
    while isinstance(model, QAbstractProxyModel):
        chain.append(model); model = model.sourceModel()
    names = list(organizer.modList().allMods())
    if model.rowCount() != len(names):
        raise ValueError('Native mod rows changed; refresh before matching filters')
    for row in range(len(names)):
        if model.index(row, 0).data(role + 1) != row:
            raise ValueError('Native mod row identity is unavailable')
    def mapped(row):
        index = model.index(row, 0)
        for proxy in reversed(chain): index = proxy.mapFromSource(index)
        return index
    def base_row(index):
        for proxy in chain: index = proxy.mapToSource(index)
        return index.row() if index.isValid() else -1
    selection = view.selectionModel()
    selected = [base_row(index) for index in selection.selectedRows()]
    current = base_row(selection.currentIndex())
    states = [int(x.data(0, role + 2)) for x in items]
    current_filter = tree.currentItem()
    search_state = (search.text(), search.cursorPosition(), search.selectionStart(), len(search.selectedText()))
    modes = (mode_and.isChecked(), mode_or.isChecked(), separators.currentIndex())
    bars = [view.verticalScrollBar(), view.horizontalScrollBar(), tree.verticalScrollBar(), tree.horizontalScrollBar()]
    offsets = [x.value() for x in bars]
    updates = window.updatesEnabled()
    blocked = selection.signalsBlocked()
    sentinel = tree.topLevelItem(0)
    sentinel_index = items.index(sentinel)
    def apply(values):
        # StateRole alone changes the glyph but does not emit criteriaChanged.
        # A real native Space event cycles one sentinel into its desired state
        # and runs FilterList::checkCriteria for the whole list.
        for item, value in zip(items, values): item.setData(0, role + 2, value)
        sentinel.setData(0, role + 2, (values[sentinel_index] - 1) % 3)
        tree.setCurrentItem(sentinel)
        QApplication.sendEvent(tree, QKeyEvent(QEvent.Type.KeyPress, Qt.Key.Key_Space, Qt.KeyboardModifier.NoModifier))
    result = []
    started = time.monotonic()
    window.setUpdatesEnabled(False); selection.blockSignals(True)
    try:
        search.clear(); mode_and.setChecked(True); separators.setCurrentIndex(0)
        for key in requested:
            values = [0] * len(items)
            values[items.index(by_key[key])] = 1
            apply(values)
            result.append({'type': key[0], 'id': key[1],
                           'names': [name for row, name in enumerate(names) if mapped(row).isValid()]})
    finally:
        try:
            apply(states)
            mode_and.setChecked(modes[0]); mode_or.setChecked(modes[1]); separators.setCurrentIndex(modes[2])
            search.setText(search_state[0])
            if search_state[2] >= 0:
                if search_state[1] == search_state[2]: search.setSelection(search_state[2] + search_state[3], -search_state[3])
                else: search.setSelection(search_state[2], search_state[3])
            else: search.setCursorPosition(search_state[1])
            tree.setCurrentItem(current_filter)
            selection.clearSelection()
            for row in selected:
                index = mapped(row)
                if index.isValid(): selection.select(index, QItemSelectionModel.SelectionFlag.Select | QItemSelectionModel.SelectionFlag.Rows)
            selection.setCurrentIndex(mapped(current) if current >= 0 else QModelIndex(), QItemSelectionModel.SelectionFlag.NoUpdate)
            view.doItemsLayout(); tree.doItemsLayout()
            for bar, offset in zip(bars, offsets): bar.setValue(offset)
        finally:
            selection.blockSignals(blocked); window.setUpdatesEnabled(updates)
        restored = ([int(x.data(0, role + 2)) for x in items] == states and
                    (mode_and.isChecked(), mode_or.isChecked(), separators.currentIndex()) == modes and
                    (search.text(), search.cursorPosition(), search.selectionStart(), len(search.selectedText())) == search_state and
                    [x.value() for x in bars] == offsets and tree.currentItem() == current_filter and
                    sorted(base_row(x) for x in selection.selectedRows()) == sorted(selected) and
                    base_row(selection.currentIndex()) == current)
        if not restored:
            raise ValueError('MO2 filter query did not restore the original view state')

    return {'criteria': result, 'restored': True, 'elapsedMs': round((time.monotonic() - started) * 1000, 2)}
