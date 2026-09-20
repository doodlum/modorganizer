#!/usr/bin/env python3
"""Data menu path contracts; fake Qt model, not native runtime acceptance."""
import importlib.util
from pathlib import Path
import sys
from types import ModuleType, SimpleNamespace
import unittest
from unittest.mock import patch

source = Path(__file__).resolve().parents[1] / 'mo2-plugin/nexus_frontend_bridge/mod_actions.py'
spec = importlib.util.spec_from_file_location('data_mod_actions', source)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class Index:
    def __init__(self, name='', children=None, loaded=True):
        self.name, self.children, self.loaded = name, children or [], loaded

    def data(self, role):
        return self.name


class Model:
    def __init__(self):
        self.root_file = Index('tool.exe')
        self.nested_file = Index('tool.exe')
        self.folder = Index('Tools', [self.nested_file], False)
        self.unrelated = Index('Textures', [Index('large.dds')], False)
        self.root = Index(children=[self.root_file, self.folder, self.unrelated])
        self.fetched = []

    def node(self, index):
        return index if index.name else self.root

    def canFetchMore(self, index):
        return not self.node(index).loaded

    def fetchMore(self, index):
        self.fetched.append(index.name)
        self.node(index).loaded = True

    def rowCount(self, index):
        node = self.node(index)
        return len(node.children) if node.loaded else 0

    def index(self, row, column, parent):
        return self.node(parent).children[row]


class DataPaths(unittest.TestCase):
    def setUp(self):
        qt = ModuleType('PyQt6.QtCore')
        qt.QModelIndex = Index
        qt.Qt = SimpleNamespace(ItemDataRole=SimpleNamespace(DisplayRole=0))
        self.modules = patch.dict(sys.modules, {'PyQt6': ModuleType('PyQt6'), 'PyQt6.QtCore': qt})
        self.modules.start()
        self.addCleanup(self.modules.stop)
        self.model = Model()

    def resolve(self, *paths):
        return module.ModActions._data_rows(self.model, paths)

    def test_same_basename_resolves_selected_folder(self):
        rows = self.resolve('Tools/tool.exe', 'tool.exe')
        self.assertIs(rows['Tools/tool.exe'], self.model.nested_file)
        self.assertIs(rows['tool.exe'], self.model.root_file)
        self.assertEqual(self.model.fetched, ['Tools'])
        self.assertFalse(self.model.unrelated.loaded)

    def test_windows_case_and_separators(self):
        self.assertIs(self.resolve('tools\\TOOL.EXE')['tools\\TOOL.EXE'], self.model.nested_file)

    def test_menu_selection_uses_full_path_resolver(self):
        actions = module.ModActions.__new__(module.ModActions)
        actions._list_model = lambda view, subject: ('view', self.model, [], 'previous-tab')
        with patch.object(module.ModActions, '_select_indexes',
                          side_effect=lambda view, chain, rows, names, subject: rows[names[0]]):
            self.assertIs(actions._select_listed('data', ['Tools/tool.exe']), self.model.nested_file)
        self.assertEqual(actions._restore_to, 'previous-tab')

    def test_activation_emits_native_signal_and_restores_tab(self):
        actions = module.ModActions.__new__(module.ModActions)
        actions.window = object()
        calls = []
        view = SimpleNamespace(activated=SimpleNamespace(emit=lambda index: calls.append(('activate', index))))
        def select(page, names):
            calls.append((page, names)); actions._restore_to = 'previous-tab'
            return view, self.model.nested_file
        actions._select_listed = select
        with patch.object(module.ModActions, '_restore_tab', side_effect=lambda window, tab: calls.append(('restore', tab))):
            self.assertEqual(actions.activate_data_file('Tools/tool.exe'), {'activated': 'Tools/tool.exe'})
        self.assertEqual(calls, [('data', ['Tools/tool.exe']), ('activate', self.model.nested_file), ('restore', 'previous-tab')])
        self.assertIsNone(actions._restore_to)
        for name in ('', None, []):
            with self.assertRaises(ValueError): actions.activate_data_file(name)

    def test_failed_activation_still_restores_tab(self):
        actions = module.ModActions.__new__(module.ModActions)
        actions.window = object()
        def select(page, names):
            actions._restore_to = 'previous-tab'
            raise ValueError('stale file')
        actions._select_listed = select
        with patch.object(module.ModActions, '_restore_tab') as restore:
            with self.assertRaises(ValueError): actions.activate_data_file('Tools/missing.exe')
            restore.assert_called_once_with(actions.window, 'previous-tab')
        self.assertIsNone(actions._restore_to)

    def test_empty_data_selection_clears_native_selection(self):
        from PyQt6 import QtCore
        QtCore.QItemSelectionModel = SimpleNamespace(SelectionFlag=SimpleNamespace(NoUpdate=0))
        calls = []
        selection = SimpleNamespace(clear=lambda: calls.append('clear'),
            setCurrentIndex=lambda index, flags: calls.append(('current', index.name)))
        view = SimpleNamespace(selectionModel=lambda: selection)
        actions = module.ModActions.__new__(module.ModActions)
        actions._list_model = lambda page, subject: (view, self.model, [], 'previous-tab')
        returned, lead = actions._select_listed('data', [])
        self.assertIs(returned, view)
        self.assertEqual(lead.name, '')
        self.assertEqual(calls, ['clear', ('current', '')])
        self.assertEqual(self.model.fetched, [])
        with self.assertRaises(ValueError):
            actions._select_listed('archives', [])

    def test_missing_nested_file_never_falls_back_to_root(self):
        self.model.folder.children = []
        with self.assertRaises(ValueError):
            self.resolve('Tools/tool.exe')

    def test_ambiguous_case_fails_closed(self):
        self.model.folder.children.append(Index('TOOL.EXE'))
        with self.assertRaises(ValueError):
            self.resolve('Tools/tool.exe')

    def test_unsafe_paths_rejected_before_loading(self):
        for path in ('/tool.exe', '../tool.exe', 'Tools/../tool.exe', 'C:/tool.exe', 'Tools//tool.exe', ''):
            with self.subTest(path=path), self.assertRaises(ValueError):
                self.resolve(path)
        self.assertEqual(self.model.fetched, [])


if __name__ == '__main__':
    unittest.main()
