"""MO2-owned operations; no frontend state store and no direct profile-file writes."""
import json
import os
import time
from pathlib import Path
import uuid


def number(value):
    return int(getattr(value, 'value', value))


class Bridge:
    def __init__(self, organizer, directory, plugin_states, credentials=None, downloads=None, profiles=None, executables=None, mod_actions=None):
        self.mod_actions = mod_actions
        self._save_preview = None
        self.logs_path = None
        self.interface = None
        self.executables = executables
        self.profiles = profiles
        self.downloads = downloads
        self._polling = False
        # How often the host looks for frontend requests. A flat 100ms tick charged
        # every action up to that much before MO2 even saw it, which the frontend
        # then paid again waiting for the reply. Ticking quickly for a short while
        # after each request keeps a burst of actions responsive without leaving the
        # host reading its request folder 60 times a second while nothing is going on.
        self.idle_interval = 100
        self.busy_interval = 16
        self.busy_for = 0.6
        self.interval = self.idle_interval
        self._busy_until = 0.0
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
            'executableIcons': self.executables.icons() if self.executables is not None else {},
            'canPreviewSaves': True,
            'canSortPlugins': getattr(self.mod_actions, 'can_sort_plugins', lambda: False)(),
            'sortPluginsUnavailableReason': getattr(self.mod_actions, 'sort_unavailable_reason', lambda: 'Plugin sorting is unavailable in this MO2 host.')(),
            'selectedExecutable': self.executables.selector().currentText() if self.executables is not None else '',
            'executables': self.executables.snapshot() if self.executables is not None else [],
            # The ones MO2 keeps on its own toolbar, which is the same pinning the
            # frontend draws beside its Run button and had been keeping separately.
            'pinnedExecutables': self.executables.pinned() if self.executables is not None else [],
            'profiles': self.profiles.snapshot() if self.profiles is not None else [],
            'nexusGame': self.downloads.game_domain() if self.downloads is not None else None,
            'downloads': self.downloads.snapshot() if self.downloads is not None else [],
            # What this MO2 build can actually be asked to do with a download, so the
            # frontend offers no entry MO2 has no action behind.
            'downloadActions': self.downloads.available_actions() if self.downloads is not None else [],
            'profile': {'name': organizer.profileName(), 'path': organizer.profilePath()},
            'instance': {'name': organizer.instanceName() if hasattr(organizer, 'instanceName') else None, 'basePath': organizer.basePath(),
                         'modsPath': organizer.modsPath(), 'downloadsPath': organizer.downloadsPath(), 'logsPath': self.logs_path,
                         'uiVisible': self.interface.is_visible() if self.interface is not None else None},
            'modFilters': self.mod_actions.filter_snapshot() if self.mod_actions is not None and hasattr(self.mod_actions, 'filter_snapshot') else [],
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
        if self._save_preview is not None and isinstance(action, str) and action not in ('snapshot', 'savePreview') and not action.startswith('read'):
            self._save_preview.close()
        if action == 'readNativeNexusAccount':
            if self.mod_actions is None: raise ValueError('MO2 account state is unavailable')
            return self.mod_actions.nexus_account_state()
        if action == 'disconnectNexusAccount':
            if self.credentials is None or self.mod_actions is None:
                raise ValueError('MO2 account integration is unavailable')
            result = self.mod_actions.nexus_account_state(disconnect_account=True)
            if result.get('connected') is not False or self.credentials._read_key() is not None:
                raise ValueError('MO2 account disconnection was not confirmed')
            self.credentials._restart_required = False
            return {'disconnected': True}
        if action == 'connectNexusAccount':
            if self.credentials is None or self.mod_actions is None:
                raise ValueError('MO2 account integration is unavailable')
            filename = request.get('path')
            if not isinstance(filename, str): raise ValueError('A Nexus API key file path is required')
            key = self.credentials._key_from_file(filename)
            account = self.credentials._validate_key(key)
            result = self.mod_actions.connect_nexus_account(key, account)
            if self.credentials._read_key() != key:
                raise ValueError('MO2 did not persist the validated Nexus credential')
            self.credentials._restart_required = False
            return result
        if action == 'readNexusAccount':
            if self.credentials is None: raise ValueError('Nexus account integration is unavailable')
            return self.credentials.account_status()
        if action in ('importNexusKey', 'importValidatedNexusKey'):
            filename = request.get('path')
            if self.credentials is None or not isinstance(filename, str):
                raise ValueError('Nexus credential import is unavailable or missing a file path')
            return self.credentials.import_validated_file(filename) if action == 'importValidatedNexusKey' else self.credentials.import_file(filename)
        if action == 'snapshot':
            return self.snapshot()
        if request.get('profilePath') != self.organizer.profilePath():
            raise ValueError('Active MO2 profile changed; refresh before editing')
        if self.profiles is not None and self.profiles.refreshing:
            raise ValueError('MO2 is refreshing the selected profile')
        if action == 'readModFilterMatches':
            if self.mod_actions is None: raise ValueError('MO2 filter integration is unavailable')
            from .filter_matches import read_matches
            return read_matches(self.mod_actions.window, self.organizer, request.get('criteria'))
        if action == 'setUiVisible':
            if self.interface is None or type(request.get('visible')) is not bool:
                raise ValueError('MO2 interface is unavailable or visibility is not a boolean')
            self.interface.set_visible(request['visible'])
            return self.snapshot()
        if action == 'sortPlugins':
            if self.mod_actions is None: raise ValueError('MO2 plugin sorting is unavailable')
            return self.mod_actions.sort_plugins()
        if action == 'orderBackup':
            if self.mod_actions is None: raise ValueError('MO2 backup integration is unavailable')
            return self.mod_actions.order_backup(request.get('list'), request.get('operation'))
        if action == 'refreshHost':
            if self.mod_actions is None: raise ValueError('MO2 refresh is unavailable')
            return self.mod_actions.refresh_host()
        if action == 'listOrderBackups':
            if self.mod_actions is None: raise ValueError('MO2 backup integration is unavailable')
            return self.mod_actions.list_order_backups(request.get('list'))
        if action == 'restoreOrderBackup':
            if self.mod_actions is None: raise ValueError('MO2 backup integration is unavailable')
            return self.mod_actions.restore_order_backup(request.get('list'), request.get('backup'))
        if action == 'setPluginLocked':
            if self.mod_actions is None: raise ValueError('MO2 plugin locking is unavailable')
            return self.mod_actions.set_plugin_locked(request.get('name'), request.get('locked'))
        if action == 'manageExecutables':
            if self.mod_actions is None: raise ValueError('MO2 tools are unavailable')
            return self.mod_actions.manage_executables()
        if action == 'openOriginal':
            if self.mod_actions is None: raise ValueError('MO2 window actions are unavailable')
            return self.mod_actions.open_original(request.get('name'))
        if action in ('listTools', 'runTool'):
            if self.mod_actions is None:
                raise ValueError('MO2 tools are unavailable')
            return self.mod_actions.list_tools() if action == 'listTools' else self.mod_actions.run_tool(request.get('tool'))
        if action in ('readOverwrite', 'overwriteAction'):
            if self.mod_actions is None: raise ValueError('MO2 Overwrite is unavailable')
            from .overwrite import Overwrite
            overwrite = Overwrite(self.organizer, self.mod_actions.window)
            return overwrite.read() if action == 'readOverwrite' else overwrite.action(request.get('operation'))
        if action == 'savePreview':
            if self.mod_actions is None: raise ValueError('MO2 save integration is unavailable')
            if self._save_preview is None:
                from .save_preview import SavePreviewLease, create_preview
                self._save_preview = SavePreviewLease(
                    lambda filename, position: create_preview(self.organizer, self.mod_actions.window, filename, position),
                    self.organizer.profilePath)
            return self._save_preview.request(request.get('owner'), request.get('file'), request.get('expires'), request.get('position'))
        if action in ('readSaves', 'saveAction'):
            if self.mod_actions is None: raise ValueError('MO2 save integration is unavailable')
            from .saves import read_saves, save_action, preview_save_details, current_saves_directory
            if action == "saveAction" and request.get("operation") == "details": return preview_save_details(self.organizer, self.mod_actions.window, request.get("file"))
            if action != 'readSaves': return save_action(self.mod_actions.window, request.get('file'), request.get('operation'))
            result = read_saves(self.mod_actions.window)
            # Where MO2 is reading them from, so the frontend's own Open in Explorer
            # can show a save on this desktop. A game plugin that cannot say is not a
            # failure to list the saves.
            try:
                result['directory'] = current_saves_directory(self.organizer).absolutePath()
            except Exception:
                result['directory'] = ''
            return result
        if action == 'readDataDirectory':
            from .data_files import read_directory
            return read_directory(self.organizer, request.get('directory', ''))
        if action in ('readArchives', 'previewArchive', 'extractArchive', 'previewDataFile', 'dataFileAction', 'setArchiveManaged'):
            if self.mod_actions is None: raise ValueError('MO2 archive integration is unavailable')
            from .archives import Archives
            archives = Archives(self.organizer, self.mod_actions.window)
            if action == 'dataFileAction': return archives.preview_file(request.get('directory'), request.get('name'), request.get('origins'), request.get('operation'))
            if action == 'previewDataFile': return archives.preview_file(request.get('directory'), request.get('name'), request.get('origins'))
            if action == 'setArchiveManaged': return archives.set_managed(request.get('name'), request.get('mod'), request.get('enabled'))
            if action == 'extractArchive': return archives.extract(request.get('name'), request.get('mod'))
            return archives.read() if action == 'readArchives' else archives.preview(request.get('name'))
        if action in ('readModMenu', 'modMenuAction', 'readPluginMenu', 'pluginMenuAction'):
            if self.mod_actions is None: raise ValueError('MO2 mod actions are unavailable')
            reading = action.startswith('read')
            menu = self.mod_actions.plugin_menu if 'Plugin' in action else self.mod_actions.mod_menu
            if reading or 'Plugin' in action:
                return menu(request.get('names'), None if reading else request.get('path'))
            # A mod action may ask something before it acts; the answer comes from the
            # frontend, which asked the user for it in a window they can actually see.
            return menu(request.get('names'), request.get('path'), request.get('answer'))
        if action == 'activateDataFile':
            if self.mod_actions is None: raise ValueError('MO2 Data activation is unavailable')
            return self.mod_actions.activate_data_file(request.get('name'))
        if action in ('readFileMenu', 'fileMenuAction', 'readFileRows'):
            if self.mod_actions is None: raise ValueError('MO2 mod actions are unavailable')
            if action == 'readFileRows': return self.mod_actions.file_list_rows(request.get('view'))
            return self.mod_actions.file_menu(request.get('view'), request.get('names'),
                                              None if action == 'readFileMenu' else request.get('path'))
        if action in ('selectionLinks', 'createSeparator'):
            if self.mod_actions is None: raise ValueError('MO2 mod integration is unavailable')
            return self.mod_actions.selection_links(request.get('names')) if action == 'selectionLinks' else self.mod_actions.create_separator(request.get('name'), request.get('collectionName'))
        if action == 'healthCheck':
            if self.mod_actions is None:
                raise ValueError('MO2 health checks are unavailable')
            result = self.mod_actions.health_check()
            if self.downloads is not None:
                result['problems'] = result['problems'] + self.downloads.warnings()
            return result
        if action == 'launch':
            if self.executables is None:
                raise ValueError('MO2 launch integration is unavailable')
            return self.executables.launch(request.get('name'))
        if action == 'shortcutMenu':
            if self.executables is None:
                raise ValueError('MO2 launch integration is unavailable')
            return self.executables.shortcut_menu(request.get('name'), request.get('entry'))
        if action == 'manageNexusAccount':
            if self.mod_actions is None:
                raise ValueError('MO2 settings integration is unavailable')
            return self.mod_actions.nexus_settings()
        if action == 'showModDetails':
            if self.mod_actions is None:
                raise ValueError('MO2 mod management is unavailable')
            return self.mod_actions.details(request.get('name'))
        if action == 'renameSeparator':
            if self.mod_actions is None: raise ValueError('MO2 collection management is unavailable')
            return self.mod_actions.rename_separator(request.get('name'), request.get('collectionName'))
        if action == 'editCategories':
            if self.mod_actions is None: raise ValueError('MO2 mod management is unavailable')
            return self.mod_actions.edit_categories()
        if action in ('modPath', 'renameMod'):
            if self.mod_actions is None: raise ValueError('MO2 mod management is unavailable')
            return self.mod_actions.mod_path(request.get('name')) if action == 'modPath' \
                else self.mod_actions.rename_mod(request.get('name'), request.get('newName'))
        if action in ('setModCategories', 'setModNotes'):
            if self.mod_actions is None: raise ValueError('MO2 mod management is unavailable')
            return self.mod_actions.set_mod_categories(request.get('name'), request.get('categories')) \
                if action == 'setModCategories' else self.mod_actions.set_mod_notes(request.get('name'), request.get('notes'))
        if action == 'setModColor':
            if self.mod_actions is None: raise ValueError('MO2 mod management is unavailable')
            return self.mod_actions.set_mod_color(request.get('name'), request.get('color'))
        if action == 'removeSeparator':
            if self.mod_actions is None: raise ValueError('MO2 collection management is unavailable')
            return self.mod_actions.remove_separator(request.get('name'))
        if action == 'removeMod':
            if self.mod_actions is None:
                raise ValueError('MO2 mod management is unavailable')
            if request.get('confirmed') is True:
                return self.mod_actions.remove_confirmed(request.get('name'))
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
        if action == 'queryDownloadMetadata':
            if self.downloads is None:
                raise ValueError('Host downloads integration is unavailable')
            return self.downloads.query_metadata()
        if action == 'refreshDownloads':
            if self.downloads is None:
                raise ValueError('Host downloads integration is unavailable')
            return self.downloads.refresh()
        if action == 'controlDownload':
            if self.downloads is None:
                raise ValueError('Host downloads integration is unavailable')
            return self.downloads.control(request.get('path'), request.get('operation'))
        if action == 'controlDownloadList':
            if self.downloads is None:
                raise ValueError('Host downloads integration is unavailable')
            return self.downloads.control_list(request.get('operation'))
        if action in ('startNexusDownload', 'startNxmDownload', 'installArchive'):

            if self.downloads is None:
                raise ValueError('Host downloads integration is unavailable')
            if action == 'startNxmDownload':
                return self.downloads.start_nxm(request.get('url'))
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
                if self.mod_actions is not None:
                    self.mod_actions.set_plugin_active(name, enabled)
                else:
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
        if self._save_preview is not None:
            self._save_preview.tick()
        # Installer dialogs run nested Qt loops. A second timer tick must never
        # replay the in-flight request or run another operation within a dialog.
        if self._polling:
            return
        self._polling = True
        try:
            handled = self._poll_requests()
        finally:
            self._polling = False
        now = time.monotonic()
        if handled:
            self._busy_until = now + self.busy_for
        self.interval = self.busy_interval if now < self._busy_until else self.idle_interval
        return self.interval

    def _poll_requests(self):
        # Invoked by a QTimer on the host UI thread; do not call MO2 from workers.
        # Reports whether it did anything, which is what decides how soon the host
        # looks again.
        handled = False
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
            handled = True
        return handled
