"""Browse the merged game Data directory through MO2's virtual directory API."""
from pathlib import PureWindowsPath


def read_directory(organizer, directory):
    if not isinstance(directory, str):
        raise ValueError('Choose a relative Data directory')
    path = PureWindowsPath(directory)
    if path.drive or path.root or '..' in path.parts or '\x00' in directory:
        raise ValueError('Choose a relative Data directory')
    directory = '/'.join(path.parts)
    rows = [{'name': name, 'directory': True, 'origins': [], 'archive': ''}
            for name in organizer.listDirectories(directory)]
    for info in organizer.findFileInfos(directory, lambda _: True):
        rows.append({'name': PureWindowsPath(info.filePath).name,
                     'directory': False, 'origins': list(info.origins),
                     'archive': info.archive})
    return {'directory': directory, 'entries': sorted(rows, key=lambda row: (not row['directory'], row['name'].casefold()))}
