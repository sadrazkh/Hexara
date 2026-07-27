#!/usr/bin/env python3
"""تولید آیکون‌های PWA از روی نشان هگزارا.

نشان عمداً اینجا با کد کشیده می‌شود و از SVG رستر نمی‌شود: هیچ رسترکننده‌ی SVG
در ابزار پروژه نیست و اضافه‌کردن یکی فقط برای شش تصویر نمی‌ارزید. هندسه همان
هندسه‌ی ‎wwwroot/favicon.svg‎ است؛ اگر آن را عوض کردی، اینجا را هم عوض کن و
دوباره اجرا کن:

    python scripts/generate-icons.py

خروجی‌ها در ‎src/Hexara.Web/wwwroot/icons/‎ ساخته می‌شوند و در مخزن می‌مانند،
چون هیچ مرحله‌ی ساختِ خودکاری آن‌ها را تولید نمی‌کند.
"""

from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "src" / "Hexara.Web" / "wwwroot" / "icons"

# رنگ‌ها از خودِ نشان می‌آیند، نه از توکن‌های تم: تم عوض می‌شود، نشان نه.
BACKDROP = (11, 16, 32)
GRADIENT_FROM = (94, 231, 198)
GRADIENT_TO = (79, 156, 249)

# کیفیت لبه‌ها از بزرگ کشیدن و کوچک کردن می‌آید — PIL ضدلبه‌دندانه‌ی برداری ندارد.
SUPERSAMPLE = 4

# نقاط شش‌ضلعی در دستگاه ۰..۳۲ که ‎favicon.svg‎ هم از آن استفاده می‌کند.
OUTER = [(16, 4.5), (26.5, 10.6), (26.5, 22.8), (16, 28.9), (5.5, 22.8), (5.5, 10.6)]
INNER = [(16, 11.2), (21.5, 14.4), (21.5, 20.8), (16, 24), (10.5, 20.8), (10.5, 14.4)]


def gradient(size: int) -> Image.Image:
    """گرادیان خطی از گوشه‌ی بالا-چپ به پایین-راست، مثل ‎linearGradient‎ در SVG."""
    image = Image.new("RGB", (size, size))
    pixels = image.load()

    for y in range(size):
        for x in range(size):
            t = (x + y) / max(1, 2 * (size - 1))
            pixels[x, y] = tuple(
                round(a + (b - a) * t) for a, b in zip(GRADIENT_FROM, GRADIENT_TO)
            )

    return image


def polygon(points, size: int, scale: float, offset: float):
    """نقاط ۰..۳۲ را به مختصات تصویر می‌برد، با کوچک‌کردن و جابه‌جایی دلخواه."""
    span = size * scale / 32
    shift = size * offset
    return [(x * span + shift, y * span + shift) for x, y in points]


def draw_mark(size: int, *, rounded: bool, inset: float) -> Image.Image:
    """
    یک آیکون مربعی. ‎inset‎ نسبت حاشیه‌ی خالی دور نشان است — برای آیکون maskable
    لازم است، چون سیستم‌عامل گوشه‌ها را می‌برد و فقط دایره‌ی میانی امن است.
    """
    big = size * SUPERSAMPLE
    scale = 1 - 2 * inset

    canvas = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    backdrop = Image.new("RGBA", (big, big), BACKDROP + (255,))

    if rounded:
        corner = Image.new("L", (big, big), 0)
        ImageDraw.Draw(corner).rounded_rectangle(
            [0, 0, big - 1, big - 1], radius=big * 7 / 32, fill=255
        )
        backdrop.putalpha(corner)

    canvas.alpha_composite(backdrop)

    # نشان از دل یک گرادیان بریده می‌شود: خطِ بیرونی و شش‌ضلعیِ پرِ درونی.
    # خطِ بیرونی به‌صورت چندضلعیِ بسته کشیده می‌شود نه خطِ باز، وگرنه جای بسته‌شدنِ
    # مسیر (نوکِ بالا) یک بریدگی کوچک می‌ماند.
    mask = Image.new("L", (big, big), 0)
    pen = ImageDraw.Draw(mask)
    pen.polygon(
        polygon(OUTER, big, scale, inset),
        fill=0,
        outline=255,
        width=round(big * 2.2 * scale / 32),
    )
    pen.polygon(polygon(INNER, big, scale, inset), fill=255)

    tinted = gradient(big).convert("RGBA")
    tinted.putalpha(mask)
    canvas.alpha_composite(tinted)

    return canvas.resize((size, size), Image.LANCZOS)


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)

    # آیکون‌های معمولی گوشه‌ی گرد دارند چون خودشان همان شکل نهایی‌اند.
    for size in (192, 512):
        draw_mark(size, rounded=True, inset=0.0).save(OUT / f"icon-{size}.png")

    # maskable تمام‌پر است و سیستم‌عامل خودش می‌بُرد؛ نشان کوچک‌تر کشیده می‌شود
    # تا بعد از بریدن هم کامل بماند.
    for size in (192, 512):
        draw_mark(size, rounded=False, inset=0.18).save(OUT / f"maskable-{size}.png")

    # iOS گوشه‌ی گرد را خودش اضافه می‌کند و شفافیت را سیاه می‌کند.
    draw_mark(180, rounded=False, inset=0.06).convert("RGB").save(OUT / "apple-touch-icon.png")

    for path in sorted(OUT.iterdir()):
        print(f"{path.relative_to(ROOT)}  {path.stat().st_size:,} bytes")


if __name__ == "__main__":
    main()
