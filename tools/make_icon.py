"""Regenerates the app icons:
  src/TaskWidget/Assets/app.ico                          - PC widget (exe and window)
  android/app/src/main/res/mipmap-*/ic_launcher.png      - Android, rounded square (Android 7 and older launchers)
  android/app/src/main/res/mipmap-*/ic_launcher_foreground.png
                                                         - Android 8+ adaptive icon: the check on transparent;
                                                           the teal background is a colour in values/themes.xml
"""
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
RES = ROOT / "android" / "app" / "src" / "main" / "res"
TEAL = (13, 125, 114, 255)
WHITE = (255, 255, 255, 255)
S = 1024  # draw large, downscale for smooth edges

DENSITIES = {"mdpi": 1, "hdpi": 1.5, "xhdpi": 2, "xxhdpi": 3, "xxxhdpi": 4}


def check(d, scale=1.0):
    # check mark centred in the canvas, `scale` shrinks it towards the centre
    pts = [(300, 530), (450, 680), (730, 380)]
    pts = [(S / 2 + (x - S / 2) * scale, S / 2 + (y - S / 2) * scale) for x, y in pts]
    w = 96 * scale
    d.line(pts, fill=WHITE, width=round(w), joint="curve")
    for x, y in (pts[0], pts[-1]):
        d.ellipse((x - w / 2, y - w / 2, x + w / 2, y + w / 2), fill=WHITE)


rounded = Image.new("RGBA", (S, S), (0, 0, 0, 0))
d = ImageDraw.Draw(rounded)
d.rounded_rectangle((64, 64, S - 64, S - 64), radius=210, fill=TEAL)
check(d)

foreground = Image.new("RGBA", (S, S), (0, 0, 0, 0))
check(ImageDraw.Draw(foreground), scale=0.6)  # inside the 66% safe zone of adaptive icons

rounded.save(ROOT / "src" / "TaskWidget" / "Assets" / "app.ico",
             sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])

for name, k in DENSITIES.items():
    folder = RES / f"mipmap-{name}"
    folder.mkdir(parents=True, exist_ok=True)
    rounded.resize((round(48 * k),) * 2, Image.LANCZOS).save(folder / "ic_launcher.png", optimize=True)
    foreground.resize((round(108 * k),) * 2, Image.LANCZOS).save(folder / "ic_launcher_foreground.png", optimize=True)

print("icons written")
