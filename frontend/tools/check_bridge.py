#!/usr/bin/env python3
"""Contract tests with a fake MO2 API. These do not prove MO2 runtime compatibility."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
import uuid

source = Path(__file__).resolve().parents[1] / 'mo2-plugin/nexus_frontend_bridge/core.py'
spec = importlib.util.spec_from_file_location('bridge_core', source)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class ListApi:
    def __init__(self): self.active = False; self.calls = 0
    def allMods(self): return ['Test Mod']
    def allModsByProfilePriority(self): return self.allMods()
    def displayName(self, name): return name
    def state(self, name): return 2 if self.active else 1
    def priority(self, name): return 0
    def setActive(self, name, active): self.active = active; self.calls += 1; return True
    def setPriority(self, name, priority): self.calls += 1; return True
    def pluginNames(self): return ['Test.esp']
    def setState(self, name, state): self.active = state == 2; self.calls += 1
    def loadOrder(self, name): return 0
    def masters(self, name): return ['FalloutNV.esm']
    def origin(self, name): return 'Test Mod'


class Organizer:
    def __init__(self): self.mods = ListApi(); self.plugins = ListApi(); self.current = 'Z:/profiles/Test'
    def modList(self): return self.mods
    def pluginList(self): return self.plugins
    def profilePath(self): return self.current
    def profileName(self): return 'Test'
    def instanceName(self): return 'FNV Test'
    def basePath(self): return 'Z:/instance'
    def modsPath(self): return 'Z:/instance/mods'
    def downloadsPath(self): return 'Z:/instance/downloads'


class ContractTests(unittest.TestCase):
    def test_download_history_is_included_with_native_health_reports(self):
        from types import SimpleNamespace
        with tempfile.TemporaryDirectory() as directory:
            native = {'problems': [{'title': 'Native warning', 'details': 'Native details'}]}
            downloads = SimpleNamespace(warnings=lambda: [{'title': 'Download interrupted', 'details': 'archive.zip'}])
            bridge = module.Bridge(Organizer(), directory, {True: 2, False: 1}, downloads=downloads,
                                  mod_actions=SimpleNamespace(health_check=lambda: dict(native)))
            result = bridge.execute({'protocol': 1, 'session': bridge.session, 'action': 'healthCheck',
                                     'profilePath': 'Z:/profiles/Test'})
            self.assertEqual([x['title'] for x in result['problems']], ['Native warning', 'Download interrupted'])
            self.assertEqual(len(native['problems']), 1)
    def test_plugin_activation_uses_native_checkbox_dispatch(self):
        from types import SimpleNamespace
        with tempfile.TemporaryDirectory() as directory:
            calls = []
            organizer = Organizer()
            actions = SimpleNamespace(set_plugin_active=lambda name, enabled: calls.append((name, enabled)))
            bridge = module.Bridge(organizer, directory, {True: 2, False: 1}, mod_actions=actions)
            bridge.snapshot = lambda: {'native': True}
            request = {'protocol': 1, 'session': bridge.session, 'action': 'setPluginActive',
                       'profilePath': 'Z:/profiles/Test', 'name': 'Test.esp', 'enabled': False}
            self.assertEqual(bridge.execute(request), {'native': True})
            self.assertEqual(calls, [('Test.esp', False)])
            self.assertEqual(organizer.plugins.calls, 0)
            request['profilePath'] = 'stale'
            with self.assertRaises(ValueError): bridge.execute(request)
            self.assertEqual(len(calls), 1)

    def test_read_only_tabs_do_not_reload_previous_plugin_tab(self):
        import sys
        from types import SimpleNamespace
        from unittest.mock import patch
        spec = importlib.util.spec_from_file_location('saves', source.with_name('saves.py'))
        saves = importlib.util.module_from_spec(spec); spec.loader.exec_module(saves)
        page = object()
        class Tabs:
            current = 0
            blocked = False
            events = []
            def currentIndex(self): return self.current
            def currentWidget(self): return page if self.current == 1 else None
            def indexOf(self, widget): return 1
            def setCurrentWidget(self, widget): self.setCurrentIndex(1)
            def setCurrentIndex(self, index):
                self.current = index
                if not self.blocked: self.events.append(index)
        tabs = Tabs()
        class Blocker:
            def __init__(self, obj): self.obj = obj
            def __enter__(self): self.previous = self.obj.blocked; self.obj.blocked = True
            def __exit__(self, *args): self.obj.blocked = self.previous
        scroll = SimpleNamespace(value=lambda: 7, setValue=lambda value: None)
        tree = SimpleNamespace(selectedItems=lambda: [], verticalScrollBar=lambda: scroll, topLevelItemCount=lambda: 0)
        window = SimpleNamespace(findChild=lambda kind, name: {'tabWidget': tabs, 'savesTab': page, 'savegameList': tree, 'bsaTab': page, 'bsaList': tree}[name])
        with patch.dict(sys.modules, {'PyQt6.QtCore': SimpleNamespace(QSignalBlocker=Blocker, Qt=SimpleNamespace()),
                                     'PyQt6.QtWidgets': SimpleNamespace(QTabWidget=object, QTreeWidget=object, QWidget=object)}):
            self.assertEqual(saves.read_saves(window), {'saves': []})
            spec = importlib.util.spec_from_file_location('archives', source.with_name('archives.py'))
            archives = importlib.util.module_from_spec(spec); spec.loader.exec_module(archives)
            self.assertEqual(archives.Archives(None, window).read(), {'archives': []})
        self.assertEqual(tabs.events, [1, 1])  # Both requested reads run; Plugins reload must not.
        self.assertEqual(tabs.current, 0)
        self.assertFalse(tabs.blocked)

    def test_save_details_directory_matches_native_profile_rules(self):
        from types import SimpleNamespace
        spec = importlib.util.spec_from_file_location('saves', source.with_name('saves.py'))
        saves = importlib.util.module_from_spec(spec); spec.loader.exec_module(saves)
        class Dir:
            def __init__(self, path): self.path = path
            def absoluteFilePath(self, name): return self.path + '/' + name
        profile = SimpleNamespace(localSavesEnabled=lambda: False, absolutePath=lambda: 'profile', absoluteIniFilePath=lambda name: 'resolved/' + name)
        game = SimpleNamespace(iniFiles=lambda: ['game.ini'], documentsDirectory=lambda: Dir('documents'), savesDirectory=lambda: Dir('default'))
        reads = []
        def read_ini(path): reads.append(path); return 'custom-saves'
        self.assertEqual(saves.save_directory(profile, game, True, read_ini, Dir).path, 'documents/custom-saves')
        self.assertEqual(reads, ['resolved/game.ini'])
        self.assertEqual(saves.save_directory(profile, game, True, lambda _: '', Dir).path, 'default')
        self.assertEqual(saves.save_directory(profile, game, False, read_ini, Dir).path, 'default')
        game.iniFiles = lambda: []
        self.assertEqual(saves.save_directory(profile, game, True, read_ini, Dir).path, 'default')
        profile.localSavesEnabled = lambda: True
        self.assertEqual(saves.save_directory(profile, game, True, read_ini, Dir).path, 'profile/saves')
        self.assertEqual(reads, ['resolved/game.ini'])

    def test_data_preview_rechecks_source_before_dispatch(self):
        import sys, types
        package = types.ModuleType('preview_test'); package.__path__ = [str(source.parent)]
        sys.modules['preview_test'] = package
        try:
            import importlib
            archives = importlib.import_module('preview_test.archives')
            class Api:
                def listDirectories(self, path): return []
                def findFileInfos(self, path, predicate):
                    return [types.SimpleNamespace(filePath=r'Z:\mods\Winner\file.txt', origins=['Winner'], archive='')]
            preview = archives.Archives(Api(), None)
            calls = []
            preview._preview = lambda parts, operation='preview': calls.append((parts, operation))
            preview.preview_file('menus/prefabs', 'file.txt', ['Winner'])
            self.assertEqual(calls, [(['menus', 'prefabs', 'file.txt'], 'preview')])
            for name, origins in [('file.txt', ['Old Winner']), ('missing.txt', ['Winner'])]:
                with self.assertRaises(ValueError): preview.preview_file('menus', name, origins)
            self.assertEqual(len(calls), 1)
            with self.assertRaises(ValueError): preview.preview_file('menus', 'file.txt', ['Winner'], 'delete')
            preview.organizer.findFileInfos = lambda path, predicate: [types.SimpleNamespace(filePath='file.txt', origins=['Winner'], archive='assets.bsa')]
            with self.assertRaises(ValueError): preview.preview_file('menus', 'file.txt', ['Winner'], 'hide')
            self.assertEqual(len(calls), 1)
            with self.assertRaises(ValueError): preview.preview_file('menus', 'file.txt', ['Winner'], 'reveal')
            preview.organizer.findFileInfos = lambda path, predicate: [types.SimpleNamespace(filePath='file.txt', origins=['Winner'], archive='')]
            with self.assertRaises(ValueError): preview.preview_file('menus', 'file.txt', ['Old Winner'], 'reveal')
            preview.preview_file('menus', 'file.txt', ['Winner'], 'reveal')
            self.assertEqual(calls[-1], (['menus', 'file.txt'], 'reveal'))
            self.assertEqual(len(calls), 2)
            resolved = []
            def resolve(path): resolved.append(path); return 'Z:/mods/Winner/file.txt'
            preview.organizer.resolvePath = resolve
            self.assertEqual(preview.preview_file('menus', 'file.txt', ['Winner'], 'revealPath'), {'path': 'Z:/mods/Winner/file.txt'})
            self.assertEqual(resolved, ['menus/file.txt'])
            with self.assertRaises(ValueError): preview.preview_file('menus', 'file.txt', ['Old Winner'], 'revealPath')
            self.assertEqual(len(resolved), 1)
            preview.organizer.findFileInfos = lambda path, predicate: [types.SimpleNamespace(filePath='file.txt', origins=['Winner'], archive='assets.bsa')]
            with self.assertRaises(ValueError): preview.preview_file('menus', 'file.txt', ['Winner'], 'revealPath')
            self.assertEqual(len(resolved), 1)
        finally:
            for name in list(sys.modules):
                if name == 'preview_test' or name.startswith('preview_test.'): del sys.modules[name]

    def test_data_directory_uses_native_origins_and_rejects_escape(self):
        from types import SimpleNamespace
        spec = importlib.util.spec_from_file_location('data_files', source.with_name('data_files.py'))
        data = importlib.util.module_from_spec(spec); spec.loader.exec_module(data)
        calls = []
        class Api:
            def listDirectories(self, path):
                calls.append(path); return ['textures']
            def findFileInfos(self, path, predicate):
                info = SimpleNamespace(filePath=r'Z:\mods\Winner\test.txt', origins=['Winner', 'Loser'], archive='assets.bsa')
                self.assertion = predicate(info)
                return [info]
        result = data.read_directory(Api(), 'meshes\\actors')
        self.assertEqual(calls, ['meshes/actors'])
        self.assertEqual(result['entries'][0]['name'], 'textures')
        self.assertEqual(result['entries'][1], {'name': 'test.txt', 'directory': False, 'origins': ['Winner', 'Loser'], 'archive': 'assets.bsa'})
        for path in ['../mods', '/absolute', 'C:relative', 'C:/absolute', 'foo/../bar', None, 'bad\0path']:
            with self.assertRaises(ValueError): data.read_directory(Api(), path)

    def test_hidden_protocol_prompt_preserves_handler_migration(self):
        spec = importlib.util.spec_from_file_location('protocols', source.with_name('protocols.py'))
        protocols = importlib.util.module_from_spec(spec); spec.loader.exec_module(protocols)
        app = Path(self.temp.name) / 'portable'; app.mkdir()
        global_dir = Path(self.temp.name) / 'global'
        calls = []
        class Settings:
            def __init__(self, path): self.path = Path(path)
            def setValue(self, key, value): calls.append((self.path, key, value))
            def sync(self): pass
        protocols.quiet_registration(app, global_dir, Settings)
        self.assertEqual(calls, [(app / 'nxmhandler.ini', 'noregister', True)])
        global_dir.mkdir(); (global_dir / 'downloadhandler.ini').write_text('[General]\n')
        calls.clear(); protocols.quiet_registration(app, global_dir, Settings)
        self.assertEqual(calls, [(global_dir / name, 'noregister', True)
                                for name in ('nxmhandler.ini', 'downloadhandler.ini')])

    def test_conflict_neighbors_use_all_native_alternatives(self):
        spec = importlib.util.spec_from_file_location('mod_actions', source.with_name('mod_actions.py'))
        actions = importlib.util.module_from_spec(spec); spec.loader.exec_module(actions)
        # Native ordering is winner first, then alternatives weakest to strongest.
        origins = ['Winner', 'Low, with comma', 'Middle', 'High']
        expected = {
            'Winner': (set(), {'Low, with comma', 'Middle', 'High'}),
            'Low, with comma': ({'Middle', 'High', 'Winner'}, set()),
            'Middle': ({'High', 'Winner'}, {'Low, with comma'}),
            'High': ({'Winner'}, {'Low, with comma', 'Middle'}),
        }
        for name, neighbors in expected.items():
            with self.subTest(name=name): self.assertEqual(actions.conflict_neighbors(name, origins), neighbors)
        self.assertEqual(actions.conflict_neighbors('Absent', origins), (set(), set()))
        self.assertEqual(actions.conflict_neighbors('Only', ['Only']), (set(), set()))
        self.assertEqual(actions.conflict_neighbors('Winner', ['Winner', 'Winner', 'Other']), (set(), {'Other'}))

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.organizer = Organizer()
        self.bridge = module.Bridge(self.organizer, self.temp.name, {True: 2, False: 1})
    def request(self, **fields):
        return dict(protocol=1, session=self.bridge.session, profilePath=self.organizer.current, **fields)
    def test_plugin_sort_dispatch_and_stale_guards(self):
        from types import SimpleNamespace
        calls = []
        self.bridge.mod_actions = SimpleNamespace(sort_plugins=lambda: calls.append('sort') or {'opened': True})
        request = self.request(action='sortPlugins')
        for change in ({'profilePath': 'Z:/profiles/Other'}, {'session': str(uuid.uuid4())}):
            with self.assertRaises(ValueError): self.bridge.execute(dict(request, **change))
        self.assertEqual(calls, [])
        self.assertEqual(self.bridge.execute(request), {'opened': True})
        self.assertEqual(calls, ['sort'])
        self.bridge.mod_actions = None
        with self.assertRaisesRegex(ValueError, 'unavailable'): self.bridge.execute(request)

    def test_order_backup_rejects_stale_targets_before_native_dispatch(self):
        from types import SimpleNamespace
        calls = []
        self.bridge.mod_actions = SimpleNamespace(order_backup=lambda target, operation: calls.append((target, operation)) or {'opened': True})
        for target in ('mods', 'plugins'):
            for operation in ('backup', 'restore'):
                request = self.request(action='orderBackup', list=target, operation=operation)
                count = len(calls)
                for change in ({'profilePath': 'Z:/profiles/Other'}, {'session': str(uuid.uuid4())}):
                    with self.assertRaises(ValueError): self.bridge.execute(dict(request, **change))
                self.assertEqual(len(calls), count)
                self.assertEqual(self.bridge.execute(request), {'opened': True})
        self.assertEqual(calls, [('mods', 'backup'), ('mods', 'restore'), ('plugins', 'backup'), ('plugins', 'restore')])
        self.bridge.mod_actions = None
        with self.assertRaisesRegex(ValueError, 'unavailable'): self.bridge.execute(request)

    def test_plugin_lock_rejects_stale_targets_before_native_dispatch(self):
        from types import SimpleNamespace
        calls = []
        self.bridge.mod_actions = SimpleNamespace(set_plugin_locked=lambda name, locked: calls.append((name, locked)) or {'locked': locked})
        for locked in (True, False):
            request = self.request(action='setPluginLocked', name='Test.esp', locked=locked)
            count = len(calls)
            for change in ({'profilePath': 'Z:/profiles/Other'}, {'session': str(uuid.uuid4())}):
                with self.assertRaises(ValueError): self.bridge.execute(dict(request, **change))
            self.assertEqual(len(calls), count)
            self.assertEqual(self.bridge.execute(request), {'locked': locked})
        self.assertEqual(calls, [('Test.esp', True), ('Test.esp', False)])

    def test_archive_actions_reject_stale_targets_before_opening_native_ui(self):
        # No Qt/archive adapter is loaded: each guard must run before dispatch.
        for action in ('readArchives', 'previewArchive', 'extractArchive'):
            with self.subTest(action=action):
                request = self.request(action=action, name='Update.bsa', mod='<Unmanaged>')
                request['profilePath'] = 'Z:/profiles/Other'
                with self.assertRaisesRegex(ValueError, 'profile changed'):
                    self.bridge.execute(request)
                request['profilePath'] = self.organizer.current
                request['session'] = str(uuid.uuid4())
                with self.assertRaisesRegex(ValueError, 'session'):
                    self.bridge.execute(request)
    def test_overwrite_listing_does_not_follow_links_or_read_contents(self):
        spec = importlib.util.spec_from_file_location('overwrite', source.with_name('overwrite.py'))
        overwrite = importlib.util.module_from_spec(spec); spec.loader.exec_module(overwrite)
        root = Path(self.temp.name) / 'overwrite'; root.mkdir()
        (root / 'nested').mkdir(); (root / 'nested' / 'generated.txt').write_bytes(b'abc')
        outside = Path(self.temp.name) / 'outside'; outside.mkdir()
        (outside / 'private.txt').write_bytes(b'private')
        (root / 'linked-dir').symlink_to(outside, target_is_directory=True)
        (root / 'linked-file').symlink_to(outside / 'private.txt')
        self.organizer.overwritePath = lambda: str(root)
        result = overwrite.Overwrite(self.organizer, None).read()
        self.assertEqual(result['files'], [{'path': 'nested/generated.txt', 'bytes': 3}])
        self.assertEqual((outside / 'private.txt').read_bytes(), b'private')

    def test_tools_use_original_actions_and_reject_stale_profiles(self):
        class Tools:
            def __init__(self): self.calls = []
            def list_tools(self): return {'tools': [{'id': ['INI Editor'], 'enabled': True}]}
            def run_tool(self, tool): self.calls.append(tool); return {'opened': True}
            def manage_executables(self): self.calls.append('settings'); return {'opened': True}
        native = Tools(); self.bridge.mod_actions = native
        self.assertEqual(self.bridge.execute(self.request(action='listTools'))['tools'][0]['id'], ['INI Editor'])
        for action in ('listTools', 'runTool', 'manageExecutables'):
            request = self.request(action=action, tool=['INI Editor']); request['profilePath'] = 'other'
            with self.assertRaises(ValueError): self.bridge.execute(request)
        self.assertEqual(native.calls, [])
        self.bridge.execute(self.request(action='runTool', tool=['INI Editor']))
        self.bridge.execute(self.request(action='manageExecutables'))
        self.assertEqual(native.calls, [['INI Editor'], 'settings'])

    def test_snapshot_reports_host_order_state_and_origin(self):
        result = self.bridge.execute(self.request(action='snapshot'))
        self.assertEqual(result['plugins'][0]['masters'], ['FalloutNV.esm'])
        self.assertEqual(result['mods'][0]['state'], 1)
    def test_log_directory_is_host_metadata_not_an_assumed_install_path(self):
        self.assertIsNone(self.bridge.execute(self.request(action='snapshot'))['instance']['logsPath'])
        self.bridge.logs_path = 'Z:/custom MO2 data/logs'
        self.assertEqual(self.bridge.execute(self.request(action='snapshot'))['instance']['logsPath'], 'Z:/custom MO2 data/logs')
    def test_original_ui_requires_ready_host_boolean_and_current_profile(self):
        with self.assertRaises(ValueError):
            self.bridge.execute(self.request(action='setUiVisible', visible=True))
        class Interface:
            visible = False
            def is_visible(self): return self.visible
            def set_visible(self, value): self.visible = value
        self.bridge.interface = Interface()
        for invalid in (1, 'true', None):
            with self.assertRaises(ValueError):
                self.bridge.execute(self.request(action='setUiVisible', visible=invalid))
        request = self.request(action='setUiVisible', visible=True)
        request['profilePath'] = 'another profile'
        with self.assertRaises(ValueError): self.bridge.execute(request)
        self.assertFalse(self.bridge.interface.visible)
        for visible in (True, False):
            result = self.bridge.execute(self.request(action='setUiVisible', visible=visible))
            self.assertEqual(result['instance']['uiVisible'], visible)
        self.assertEqual(self.organizer.mods.calls + self.organizer.plugins.calls, 0)
    def test_native_mod_details_are_forwarded_without_recomputing_conflicts(self):
        class ModActions:
            def snapshot(self): return [{'name': 'Overwrite', 'state': 4, 'priority': 1,
                'priorityText': '', 'overwrite': True, 'conflicts': 'Native archive conflict', 'flags': 'Native status'}]
            def plugin_snapshot(self): return [{'name': 'Base.esm', 'canToggle': False, 'canMove': False, 'modIndex': '00', 'diagnostics': 'Native missing master warning', 'hasWarning': True}]
            def nexus_settings(self): return {'opened': True, 'tab': 'nexusTab'}
            def details(self, name): return {'opened': True, 'modName': name}
        self.bridge.mod_actions = ModActions()
        result = self.bridge.execute(self.request(action='snapshot'))
        self.assertEqual(result['mods'], self.bridge.mod_actions.snapshot())
        self.assertEqual(result['plugins'], self.bridge.mod_actions.plugin_snapshot())
        self.assertEqual(self.bridge.execute(self.request(action='manageNexusAccount')), {'opened': True, 'tab': 'nexusTab'})
        request = self.request(action='showModDetails', name='Overwrite')
        self.assertEqual(self.bridge.execute(request), {'opened': True, 'modName': 'Overwrite'})
        self.organizer.current = 'Z:/profiles/Other'
        with self.assertRaises(ValueError): self.bridge.execute(request)
    def test_mutations_use_host_and_return_actual_state(self):
        result = self.bridge.execute(self.request(action='setModActive', name='Test Mod', enabled=True))
        self.assertEqual(self.organizer.mods.calls, 1)
        self.assertEqual(result['mods'][0]['state'], 2)
        self.bridge.execute(self.request(action='setPluginActive', name='Test.esp', enabled=True))
        self.assertTrue(self.organizer.plugins.active)
    def test_older_host_without_instance_name(self):
        class OlderOrganizer:
            def __getattr__(self, name):
                if name == 'instanceName': raise AttributeError(name)
                return getattr(self.organizer, name)
        older = OlderOrganizer()
        older.organizer = self.organizer
        self.bridge.organizer = older
        result = self.bridge.execute(self.request(action='snapshot'))
        self.assertIsNone(result['instance']['name'])
        self.assertEqual(result['profile']['name'], 'Test')
    def test_stale_profile_or_session_rejected(self):
        request = self.request(action='setModActive', name='Test Mod', enabled=True)
        self.organizer.current = 'Z:/profiles/Other'
        with self.assertRaises(ValueError): self.bridge.execute(request)
        request['session'] = str(uuid.uuid4())
        with self.assertRaises(ValueError): self.bridge.execute(request)
        self.assertEqual(self.organizer.mods.calls, 0)
    def test_invalid_value_rejected(self):
        with self.assertRaises(ValueError): self.bridge.execute(self.request(action='setModActive', name='Test Mod', enabled='false'))
        with self.assertRaises(ValueError): self.bridge.execute(self.request(action='setPluginPriority', name='Test.esp', priority=-1))
    def test_separator_removal_is_guarded_by_profile_and_session(self):
        calls = []
        class Actions:
            def remove_separator(inner, name): calls.append(name); return {'removed': True}
        self.bridge.mod_actions = Actions()
        request = self.request(action='removeSeparator', name='Fixture_separator')
        request['profilePath'] = 'wrong-profile'
        with self.assertRaisesRegex(ValueError, 'profile changed'): self.bridge.execute(request)
        self.assertEqual(calls, [])
        request['profilePath'] = self.organizer.current
        self.assertTrue(self.bridge.execute(request)['removed'])
        request['session'] = str(uuid.uuid4())
        with self.assertRaisesRegex(ValueError, 'session'): self.bridge.execute(request)
        self.assertEqual(calls, ['Fixture_separator'])

    def test_separator_rename_forwards_names_and_rejects_stale_targets(self):
        calls = []
        class Actions:
            def rename_separator(inner, name, title):
                calls.append((name, title))
                return {'renamed': True, 'oldName': name, 'name': title + '_separator'}
        self.bridge.mod_actions = Actions()
        request = self.request(action='renameSeparator', name='Old_separator', collectionName='New')
        request['profilePath'] = 'wrong-profile'
        with self.assertRaisesRegex(ValueError, 'profile changed'): self.bridge.execute(request)
        self.assertEqual(calls, [])
        request['profilePath'] = self.organizer.current
        self.assertEqual(self.bridge.execute(request), {'renamed': True, 'oldName': 'Old_separator', 'name': 'New_separator'})
        request['session'] = str(uuid.uuid4())
        with self.assertRaisesRegex(ValueError, 'session'): self.bridge.execute(request)
        self.assertEqual(calls, [('Old_separator', 'New')])

    def test_native_account_connection_validates_before_applying_and_checks_persistence(self):
        calls = []
        class Credentials:
            _restart_required = True
            stored = 'fixture-key'
            def _key_from_file(inner, path): calls.append('read'); return 'fixture-key'
            def _validate_key(inner, key): calls.append('validate'); return {'userId': 1, 'name': 'Fixture'}
            def _read_key(inner): calls.append('stored'); return inner.stored
        class Actions:
            def connect_nexus_account(inner, key, account):
                calls.append('native'); return {'connected': True, 'account': account}
        self.bridge.credentials = Credentials(); self.bridge.mod_actions = Actions()
        request = self.request(action='connectNexusAccount', path='fixture.txt')
        request.pop('profilePath')  # account identity is global to the instance
        self.assertTrue(self.bridge.execute(request)['connected'])
        self.assertEqual(calls, ['read', 'validate', 'native', 'stored'])
        self.assertFalse(self.bridge.credentials._restart_required)
        self.bridge.credentials.stored = 'different-fixture-key'
        with self.assertRaisesRegex(ValueError, 'persist'): self.bridge.execute(request)
        calls.clear(); request['session'] = str(uuid.uuid4())
        with self.assertRaisesRegex(ValueError, 'session'): self.bridge.execute(request)
        self.assertEqual(calls, [])

    def test_native_logout_checks_runtime_and_saved_credential(self):
        calls = []
        class Credentials:
            stored = None
            def _read_key(inner): return inner.stored
        class Actions:
            connected = False
            def nexus_account_state(inner, disconnect_account=False):
                calls.append(disconnect_account)
                return {'connected': inner.connected}
        self.bridge.credentials = Credentials(); self.bridge.mod_actions = Actions()
        request = self.request(action='disconnectNexusAccount')
        request.pop('profilePath')
        self.assertEqual(self.bridge.execute(request), {'disconnected': True})
        self.assertEqual(calls, [True])
        self.bridge.credentials.stored = 'fixture-only'
        with self.assertRaisesRegex(ValueError, 'not confirmed'): self.bridge.execute(request)
        self.bridge.credentials.stored = None; self.bridge.mod_actions.connected = True
        with self.assertRaisesRegex(ValueError, 'not confirmed'): self.bridge.execute(request)
        calls.clear(); request['session'] = str(uuid.uuid4())
        with self.assertRaisesRegex(ValueError, 'session'): self.bridge.execute(request)
        self.assertEqual(calls, [])

    def test_credential_import_passes_only_path_and_checks_session(self):
        class Credentials:
            calls = []
            def import_file(self, path):
                self.calls.append(path)
                return {'stored': True, 'restartRequired': True}
        credentials = Credentials()
        self.bridge.credentials = credentials
        request = self.request(action='importNexusKey', path='Z:/private/key.txt')
        result = self.bridge.execute(request)
        self.assertEqual(credentials.calls, ['Z:/private/key.txt'])
        self.assertEqual(result, {'stored': True, 'restartRequired': True})
        request['session'] = str(uuid.uuid4())
        with self.assertRaises(ValueError): self.bridge.execute(request)
        self.assertEqual(len(credentials.calls), 1)
    def test_profiles_are_host_owned_and_refresh_blocks_edits(self):
        organizer = self.organizer
        class Profiles:
            refreshing = False
            def snapshot(self): return [{'name': organizer.profileName(), 'path': organizer.profilePath()}]
            def select(self, name): organizer.current = 'Z:/profiles/' + name
            def manage(self): pass
        self.bridge.profiles = Profiles()
        result = self.bridge.execute(self.request(action='selectProfile', name='Clone'))
        self.assertEqual(result['profile']['path'], 'Z:/profiles/Clone')
        self.bridge.profiles.refreshing = True
        with self.assertRaises(ValueError): self.bridge.execute(self.request(action='snapshot'))
        with self.assertRaises(ValueError): self.bridge.execute(self.request(action='setModActive', name='Test Mod', enabled=True))
        self.assertEqual(organizer.mods.calls, 0)
    def test_profile_card_action_preserves_active_profile_and_rejects_stale_request(self):
        organizer = self.organizer
        class Profiles:
            refreshing = False
            calls = []
            def snapshot(self): return [{'name': 'Other', 'path': 'Z:/profiles/Other'}]
            def manage_profile(self, name, operation): self.calls.append((name, operation))
        profiles = Profiles()
        self.bridge.profiles = profiles
        request = self.request(action='manageProfile', name='Other', operation='copy')
        result = self.bridge.execute(request)
        self.assertEqual(profiles.calls, [('Other', 'copy')])
        self.assertEqual(result['profile']['path'], 'Z:/profiles/Test')
        self.bridge.execute(self.request(action='manageProfile', name='Other', operation='rename'))
        self.assertEqual(profiles.calls[-1], ('Other', 'rename'))
        self.bridge.execute(self.request(action='manageProfile', name='Other', operation='create'))
        self.assertEqual(profiles.calls[-1], ('Other', 'create'))
        organizer.current = 'Z:/profiles/Changed'
        with self.assertRaises(ValueError): self.bridge.execute(request)
        self.assertEqual(len(profiles.calls), 3)
    def test_download_control_uses_host_and_rejects_stale_profile(self):
        class Downloads:
            calls = []
            def control(self, path, operation):
                self.calls.append((path, operation))
                return {'requested': operation}
        self.bridge.downloads = Downloads()
        request = self.request(action='controlDownload', path='Z:/downloads/Test.zip.unfinished', operation='pause')
        self.assertEqual(self.bridge.execute(request), {'requested': 'pause'})
        self.organizer.current = 'Z:/profiles/Other'
        with self.assertRaises(ValueError): self.bridge.execute(request)
        self.assertEqual(self.bridge.downloads.calls, [('Z:/downloads/Test.zip.unfinished', 'pause')])
    def test_launch_rejects_stale_profile_before_starting_host_process(self):
        class Executables:
            calls = []
            def snapshot(self): return ['NVSE']
            def launch(self, name):
                self.calls.append(name)
                return {'completed': True, 'exitCode': 0}
        self.bridge.executables = Executables()
        request = self.request(action='launch', name='NVSE')
        self.organizer.current = 'Z:/profiles/Other'
        with self.assertRaises(ValueError): self.bridge.execute(request)
        self.assertEqual(self.bridge.executables.calls, [])
        result = self.bridge.execute(self.request(action='launch', name='NVSE'))
        self.assertEqual(result['exitCode'], 0)
        self.assertEqual(self.bridge.executables.calls, ['NVSE'])
    def test_uninstall_reports_cancel_and_rejects_stale_profile(self):
        class ModActions:
            calls = []
            def remove(self, name):
                self.calls.append(name)
                return {'removed': False, 'modName': name}
        self.bridge.mod_actions = ModActions()
        request = self.request(action='removeMod', name='Test Mod')
        self.assertFalse(self.bridge.execute(request)['removed'])
        self.organizer.current = 'Z:/profiles/Other'
        with self.assertRaises(ValueError): self.bridge.execute(request)
        self.assertEqual(self.bridge.mod_actions.calls, ['Test Mod'])
    def test_confirmed_uninstall_requires_exact_flag_and_current_profile(self):
        calls = []
        class ModActions:
            def remove(self, name): calls.append(('native-dialog', name)); return {'removed': False}
            def remove_confirmed(self, name): calls.append(('frontend-confirmed', name)); return {'removed': True}
        self.bridge.mod_actions = ModActions()
        self.bridge.execute(self.request(action='removeMod', name='Fixture', confirmed='true'))
        self.bridge.execute(self.request(action='removeMod', name='Fixture', confirmed=True))
        self.assertEqual(calls, [('native-dialog', 'Fixture'), ('frontend-confirmed', 'Fixture')])
        request = self.request(action='removeMod', name='Fixture', confirmed=True)
        self.organizer.current = 'Z:/profiles/Other'
        with self.assertRaises(ValueError): self.bridge.execute(request)
        self.assertEqual(len(calls), 2)
    def test_health_checks_use_native_adapter_and_reject_stale_profile(self):
        class ModActions:
            calls = 0
            def health_check(self):
                self.calls += 1
                return {'problems': [{'title': 'MO2 diagnostic', 'details': 'Native description'}]}
        request = self.request(action='healthCheck')
        with self.assertRaises(ValueError): self.bridge.execute(request)
        self.bridge.mod_actions = ModActions()
        self.organizer.current = 'Z:/profiles/Other'
        with self.assertRaises(ValueError): self.bridge.execute(request)
        self.assertEqual(self.bridge.mod_actions.calls, 0)
        result = self.bridge.execute(self.request(action='healthCheck'))
        self.assertEqual(result['problems'][0]['title'], 'MO2 diagnostic')
        self.assertEqual(self.bridge.mod_actions.calls, 1)

    def test_nested_dialog_poll_does_not_replay_request(self):
        calls = []
        bridge = self.bridge
        class Credentials:
            def import_file(self, path):
                calls.append(path)
                bridge.poll()
                return {'stored': True}
        bridge.credentials = Credentials()
        identifier = str(uuid.uuid4())
        path = Path(self.temp.name) / 'requests' / (identifier + '.json')
        path.write_text(json.dumps(self.request(action='importNexusKey', path='private.txt')))
        bridge.poll()
        self.assertEqual(calls, ['private.txt'])
        self.assertFalse(bridge._polling)
    def test_mailbox_replay_does_not_repeat_mutation(self):
        identifier = str(uuid.uuid4())
        path = Path(self.temp.name) / 'requests' / (identifier + '.json')
        body = self.request(action='setModActive', name='Test Mod', enabled=True)
        path.write_text(json.dumps(body)); self.bridge.poll()
        self.assertTrue(json.loads((Path(self.temp.name) / 'responses' / path.name).read_text())['ok'])
        path.write_text(json.dumps(body)); self.bridge.poll()
        self.assertEqual(self.organizer.mods.calls, 1)


if __name__ == '__main__': unittest.main()
