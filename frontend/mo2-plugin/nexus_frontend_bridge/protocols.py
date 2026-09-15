"""Keep hidden hosts from competing with the frontend's desktop URL handler."""
from pathlib import Path


def quiet_registration(application_directory, global_directory, make_settings):
    # Match nxmhandler's own global-versus-portable storage selection. Newer
    # versions migrate nxmhandler.ini to downloadhandler.ini. Do not create an
    # empty modern file that would prevent migration of existing handler records.
    global_directory = Path(global_directory)
    directory = global_directory if global_directory.is_dir() else Path(application_directory)
    paths = [directory / 'nxmhandler.ini']
    if (directory / 'downloadhandler.ini').exists():
        paths.append(directory / 'downloadhandler.ini')
    for path in paths:
        settings = make_settings(str(path))
        settings.setValue('noregister', True)
        settings.sync()
