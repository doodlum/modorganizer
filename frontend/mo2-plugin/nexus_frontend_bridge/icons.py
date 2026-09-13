"""Small, cached copies of the icons already supplied by MO2 and its extensions."""
_cache = {}


def icon_png(icon):
    if icon.isNull(): return ''
    key = icon.cacheKey()
    if key not in _cache:
        from PyQt6.QtCore import QBuffer, QByteArray, QIODevice
        data = QByteArray(); buffer = QBuffer(data)
        buffer.open(QIODevice.OpenModeFlag.WriteOnly)
        icon.pixmap(48, 48).save(buffer, 'PNG')
        _cache[key] = bytes(data.toBase64()).decode('ascii')
        buffer.close()
    return _cache[key]
