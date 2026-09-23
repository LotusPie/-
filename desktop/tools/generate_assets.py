import pathlib
import struct
import zlib

ROOT = pathlib.Path(__file__).resolve().parents[1] / "src" / "LyricsTranslator.App" / "Assets"
ROOT.mkdir(parents=True, exist_ok=True)

BG = (0x1B, 0x1F, 0x2A, 255)
ACCENT = (0x2E, 0xC4, 0xB6, 255)
WHITE = (255, 255, 255, 255)
INK = (0x10, 0x20, 0x27, 255)


def png(width: int, height: int, pixel_at) -> bytes:
    raw = bytearray()
    for y in range(height):
        raw.append(0)
        for x in range(width):
            raw.extend(pixel_at(x, y, width, height))
    def chunk(tag: bytes, data: bytes) -> bytes:
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)
    ihdr = struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr) + chunk(b"IDAT", zlib.compress(bytes(raw), 9)) + chunk(b"IEND", b"")


def lerp(a, b, t):
    return int(a + (b - a) * t)


def draw_mark(x, y, w, h):
    # Dark rounded-ish card with three lyric lines and a teal disc.
    nx = x / (w - 1)
    ny = y / (h - 1)
    px, py = x + 0.5, y + 0.5
    cx, cy = w / 2, h / 2
    r = min(w, h) * 0.42
    dx, dy = px - cx, py - cy
    if dx * dx + dy * dy > r * r:
        return (0, 0, 0, 0) if w <= 48 else BG

    # inner fill
    color = BG
    # teal disc on the left-ish
    disc_cx, disc_cy = cx - r * 0.15, cy
    disc_r = r * 0.55
    ddx, ddy = px - disc_cx, py - disc_cy
    if ddx * ddx + ddy * ddy <= disc_r * disc_r:
        color = ACCENT
        # inner hole to look like a record
        if ddx * ddx + ddy * ddy <= (disc_r * 0.18) ** 2:
            color = INK
    # three bars on the right (lyrics)
    bar_left = cx + r * 0.05
    if px >= bar_left:
        for i, by in enumerate((-0.28, 0.0, 0.28)):
            bar_cy = cy + r * by
            if abs(py - bar_cy) <= max(1.2, h * 0.035) and px <= cx + r * 0.75:
                color = WHITE if color == BG else WHITE
    return color


def write_png(name: str, w: int, h: int, fn=None):
    fn = fn or (lambda x, y, ww, hh: draw_mark(x, y, ww, hh) if ww == hh else splash_pixel(x, y, ww, hh))
    (ROOT / name).write_bytes(png(w, h, fn))


def splash_pixel(x, y, w, h):
    # Wide banner: background + centered mark.
    if y < 8 or y > h - 9 or x < 8 or x > w - 9:
        return BG
    side = min(h - 40, 180)
    ox = (w - side) // 2
    oy = (h - side) // 2
    if ox <= x < ox + side and oy <= y < oy + side:
        return draw_mark(x - ox, y - oy, side, side)
    return BG


def ico_from_pngs(pngs: list[bytes]) -> bytes:
    count = len(pngs)
    offset = 6 + 16 * count
    entries = b""
    payload = b""
    for data in pngs:
        # decode IHDR for size
        w, h = struct.unpack(">II", data[16:24])
        entries += struct.pack("<BBBBHHII", w if w < 256 else 0, h if h < 256 else 0, 0, 0, 1, 32, len(data), offset)
        payload += data
        offset += len(data)
    return struct.pack("<HHH", 0, 1, count) + entries + payload


write_png("StoreLogo.png", 50, 50)
write_png("Square44x44Logo.png", 44, 44)
write_png("Square150x150Logo.png", 150, 150)
write_png("Wide310x150Logo.png", 310, 150, splash_pixel)
write_png("SplashScreen.png", 620, 300, splash_pixel)
write_png("LockScreenLogo.png", 24, 24)

sizes = [16, 32, 48, 256]
pngs = [png(s, s, draw_mark) for s in sizes]
(ROOT / "app.ico").write_bytes(ico_from_pngs(pngs))
print("wrote", ROOT)
