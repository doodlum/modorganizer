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
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.organizer = Organizer()
        self.bridge = module.Bridge(self.organizer, self.temp.name, {True: 2, False: 1})
    def request(self, **fields):
        return dict(protocol=1, session=self.bridge.session, profilePath=self.organizer.current, **fields)
    def test_snapshot_reports_host_order_state_and_origin(self):
        result = self.bridge.execute(self.request(action='snapshot'))
        self.assertEqual(result['plugins'][0]['masters'], ['FalloutNV.esm'])
        self.assertEqual(result['mods'][0]['state'], 1)
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
