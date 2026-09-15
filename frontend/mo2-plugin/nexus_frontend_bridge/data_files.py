"""Browse the merged game Data directory through MO2's virtual directory API."""
import os
from pathlib import PureWindowsPath


def _stat(info):
    """Size and modified time of a file MO2 reports, where they can be read.

    MO2 gives the virtual path; a file provided from inside an archive has no
    file of its own to stat, and a path that has since gone leaves the two
    fields empty rather than failing the whole listing.
    """
    try:
        if info.archive:
            return '', ''
        stat = os.stat(info.filePath)
        return str(stat.st_size), str(int(stat.st_mtime))
    except (OSError, ValueError, AttributeError):
        return '', ''


def read_directory(organizer, directory):
    if not isinstance(directory, str):
        raise ValueError('Choose a relative Data directory')
    path = PureWindowsPath(directory)
    if path.drive or path.root or '..' in path.parts or '\x00' in directory:
        raise ValueError('Choose a relative Data directory')
    directory = '/'.join(path.parts)
    rows = [{'name': name, 'directory': True, 'origins': [], 'archive': '', 'size': '', 'modified': ''}
            for name in organizer.listDirectories(directory)]
    for info in organizer.findFileInfos(directory, lambda _: True):
        size, modified = _stat(info)
        rows.append({'name': PureWindowsPath(info.filePath).name,
                     'directory': False, 'origins': list(info.origins),
                     'archive': info.archive, 'size': size, 'modified': modified})
    return {'directory': directory, 'entries': sorted(rows, key=lambda row: (not row['directory'], row['name'].casefold()))}
