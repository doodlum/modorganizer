"""MO2-owned operations; no frontend state store and no direct profile-file writes."""
import json
import os
from pathlib import Path
import uuid


def number(value):
    return int(getattr(value, 'value', value))


class Bridge:
    def __init__(self, organizer, directory, plugin_states, credentials=None, downloads=None, profiles=None, executables=None, mod_actions=None):
        self.mod_actions = mod_actions
        self.executables = executables
        self.profiles = profiles
        self.downloads = downloads
        self._polling = False
        self.credentials = credentials
        self.organizer = organizer
        self.directory = Path(directory)
        self.states = plugin_states
        self.session = str(uuid.uuid4())
        for name in ('requests', 'responses'):
            (self.directory / name).mkdir(parents=True, exist_ok=True)
        self.write(self.directory / 'endpoint.json', {'protocol': 1, 'session': self.session, 'pid': os.getpid()})

    @staticmethod
    def write(path, value):
        temporary = path.with_suffix(path.suffix + '.tmp')
        temporary.write_text(json.dumps(value, ensure_ascii=False), encoding='utf-8')
        os.replace(temporary, path)

    def snapshot(self):
        if self.profiles is not None and self.profiles.refreshing:
            raise ValueError('MO2 is refreshing the selected profile')
        organizer = self.organizer
        mods = organizer.modList()
        plugins = organizer.pluginList()
        return {
            'executables': self.executables.snapshot() if self.executables is not None else [],
            'profiles': self.profiles.snapshot() if self.profiles is not None else [],
            'nexusGame': self.downloads.game_domain() if self.downloads is not None else None,
            'downloads': self.downloads.snapshot() if self.downloads is not None else [],
            'profile': {'name': organizer.profileName(), 'path': organizer.profilePath()},
            'instance': {'name': organizer.instanceName() if hasattr(organizer, 'instanceName') else None, 'basePath': organizer.basePath(),
                         'modsPath': organizer.modsPath(), 'downloadsPath': organizer.downloadsPath()},
            'mods': self.mod_actions.snapshot() if self.mod_actions is not None else [{'name': name, 'displayName': mods.displayName(name), 'state': number(mods.state(name)),
                      'priority': mods.priority(name)} for name in mods.allModsByProfilePriority()],
            'plugins': self.mod_actions.plugin_snapshot() if self.mod_actions is not None else [{'name': name, 'state': number(plugins.state(name)), 'priority': plugins.priority(name),
                         'loadOrder': plugins.loadOrder(name), 'masters': list(plugins.masters(name)),
                         'origin': plugins.origin(name)} for name in plugins.pluginNames()],
        }

    def execute(self, request):
        if request.get('protocol') != 1 or request.get('session') != self.session:
            raise ValueError('Bridge session changed; reconnect before issuing commands')
        action = request.get('action')
        if action == 'importNexusKey':
            filename = request.get('path')
            if self.credentials is None or not isinstance(filename, str):
                raise ValueError('Nexus credential import is unavailable or missing a file path')
            return self.credentials.import_file(filename)
        if action == 'snapshot':
            return self.snapshot()
        if request.get('profilePath') != self.organizer.profilePath():
            raise ValueError('Active MO2 profile changed; refresh before editing')
        if self.profiles is not None and self.profiles.refreshing:
            raise ValueError('MO2 is refreshing the selected profile')
        if action == 'launch':
            if self.executables is None:
                raise ValueError('MO2 launch integration is unavailable')
            return self.executables.launch(request.get('name'))
        if action == 'showModDetails':
            if self.mod_actions is None:
                raise ValueError('MO2 mod management is unavailable')
            return self.mod_actions.details(request.get('name'))
        if action == 'removeMod':
            if self.mod_actions is None:
                raise ValueError('MO2 mod management is unavailable')
            return self.mod_actions.remove(request.get('name'))
        if action in ('selectProfile', 'manageProfiles', 'manageProfile'):
            if self.profiles is None:
                raise ValueError('MO2 profile integration is unavailable')
            if action == 'selectProfile':
                name = request.get('name')
                if not isinstance(name, str): raise ValueError('A profile name is required')
                self.profiles.select(name)
            elif action == 'manageProfile':
                self.profiles.manage_profile(request.get('name'), request.get('operation'))
            else:
                self.profiles.manage()
            return self.snapshot()
        if action == 'controlDownload':
            if self.downloads is None:
                raise ValueError('Host downloads integration is unavailable')
            return self.downloads.control(request.get('path'), request.get('operation'))
        if action in ('startNexusDownload', 'installArchive'):

            if self.downloads is None:
                raise ValueError('Host downloads integration is unavailable')
            if action == 'startNexusDownload':
                return self.downloads.start_nexus(request.get('modId'), request.get('fileId'), request.get('game'))
            return self.downloads.install(request.get('path'))
        name = request.get('name')
        if not isinstance(name, str):
            raise ValueError('A mod or plugin name is required')
        if action in ('setModActive', 'setModPriority'):
            target = self.organizer.modList()
            if name not in target.allMods():
                raise ValueError('Mod no longer exists')
            if action == 'setModActive':
                enabled = request.get('enabled')
                if type(enabled) is not bool:
                    raise ValueError('enabled must be a boolean')
                accepted = target.setActive(name, enabled)
            else:
                priority = request.get('priority')
                if type(priority) is not int or priority < 0 or priority >= len(target.allMods()):
                    raise ValueError('Invalid mod priority')
                accepted = target.setPriority(name, priority)
            if not accepted:
                raise ValueError('MO2 rejected the mod change')
        elif action in ('setPluginActive', 'setPluginPriority'):
            target = self.organizer.pluginList()
            if name not in target.pluginNames():
                raise ValueError('Plugin no longer exists')
            if action == 'setPluginActive':
                enabled = request.get('enabled')
                if type(enabled) is not bool:
                    raise ValueError('enabled must be a boolean')
                target.setState(name, self.states[enabled])
            else:
                priority = request.get('priority')
                if type(priority) is not int or priority < 0 or priority >= len(target.pluginNames()):
                    raise ValueError('Invalid plugin priority')
                if not target.setPriority(name, priority):
                    raise ValueError('MO2 rejected the plugin priority')
        else:
            raise ValueError('Unsupported bridge action')
        # Report authoritative post-operation state; the host may enforce rules.
        return self.snapshot()

    def poll(self):
        # Installer dialogs run nested Qt loops. A second timer tick must never
        # replay the in-flight request or run another operation within a dialog.
        if self._polling:
            return
        self._polling = True
        try:
            self._poll_requests()
        finally:
            self._polling = False

    def _poll_requests(self):
        # Invoked by a QTimer on the host UI thread; do not call MO2 from workers.
        for path in sorted((self.directory / 'requests').glob('*.json'))[:8]:
            try:
                identifier = str(uuid.UUID(path.stem))
            except ValueError:
                continue
            response = self.directory / 'responses' / (identifier + '.json')
            if response.exists():
                path.unlink(missing_ok=True)
                continue
            try:
                if path.stat().st_size > 131072:
                    raise ValueError('Request exceeds size limit')
                request = json.loads(path.read_text(encoding='utf-8'))
                result = {'id': identifier, 'session': self.session, 'ok': True, 'result': self.execute(request)}
            except Exception as error:
                result = {'id': identifier, 'session': self.session, 'ok': False, 'error': str(error)}
            self.write(response, result)
            path.unlink(missing_ok=True)
