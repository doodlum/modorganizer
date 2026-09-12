"""Use the original mod model's confirmed uninstall and profile cleanup."""


class ModActions:
    def __init__(self, organizer, window):
        self.organizer = organizer
        self.window = window

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
