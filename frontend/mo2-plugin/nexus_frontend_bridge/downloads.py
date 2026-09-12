"""MO2's download directory plus its existing downloader/installer entry points."""
import configparser
from pathlib import Path


class Downloads:
    def __init__(self, organizer):
        self.organizer = organizer

    def game_domain(self):
        game = self.organizer.managedGame()
        return game.gameNexusName() or game.gameShortName()

    def snapshot(self):
        directory = Path(self.organizer.downloadsPath())
        result = []
        if not directory.is_dir():
            return result
        for path in sorted(directory.iterdir(), key=lambda item: item.name.casefold()):
            if not path.is_file() or path.suffix.casefold() == '.meta':
                continue
            meta = configparser.ConfigParser(interpolation=None, strict=False)
            try:
                meta.read(str(path) + '.meta', encoding='utf-8-sig')
                values = meta['General'] if meta.has_section('General') else {}
            except (configparser.Error, UnicodeError):
                values = {}
            partial = path.name.casefold().endswith(('.unfinished', '.part'))
            result.append({'name': path.name, 'path': str(path), 'bytes': path.stat().st_size,
                           'partial': partial, 'installed': values.get('installed', 'false') == 'true',
                           'hidden': values.get('removed', 'false') == 'true',
                           'paused': values.get('paused', 'false') == 'true'})
        return result

    def start_nexus(self, mod_id, file_id, game):
        if not isinstance(game, str) or game.casefold() != self.game_domain().casefold():
            raise ValueError("The Nexus file belongs to a different game")
        if any(type(value) is not int or value <= 0 for value in (mod_id, file_id)):
            raise ValueError('Positive Nexus mod and file IDs are required')
        identifier = self.organizer.downloadManager().startDownloadNexusFile(mod_id, file_id)
        if identifier < 0:
            raise ValueError('MO2 did not accept the Nexus download')
        return {'downloadId': identifier, 'queued': True}

    def install(self, filename):
        if not isinstance(filename, str) or not Path(filename).is_file():
            raise ValueError('The mod archive does not exist')
        if filename.casefold().endswith(('.unfinished', '.part', '.meta')):
            raise ValueError('The selected file is not a completed archive')
        # MO2 chooses its existing installer plugin and owns any Qt dialogs.
        installed = self.organizer.installMod(filename)
        return {'installed': installed is not None, 'modName': installed.name() if installed is not None else None}
