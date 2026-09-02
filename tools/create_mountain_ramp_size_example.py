from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


PROJECT_ROOT = Path(__file__).resolve().parents[1]
THEME_DIR = PROJECT_ROOT / (
    "addons/beep_game_builder_cs/generated/mountains/low_poly_sandstone/"
    "authored_prefabs/modular_themes/meadow_hill"
)
OUTPUT = THEME_DIR / "ramp_size_validation_example.png"
BACKGROUND = (19, 35, 43, 255)


def font(size: int) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    path = Path("C:/Windows/Fonts/arial.ttf")
    return ImageFont.truetype(path, size) if path.exists() else ImageFont.load_default()


def fit(image: Image.Image, width: int, height: int) -> Image.Image:
    scale = min(width / image.width, height / image.height)
    return image.resize(
        (round(image.width * scale), round(image.height * scale)),
        Image.Resampling.LANCZOS,
    )


def main() -> int:
    canvas = Image.new("RGBA", (1900, 1250), BACKGROUND)
    draw = ImageDraw.Draw(canvas)
    title_font = font(32)
    label_font = font(25)
    detail_font = font(21)

    mountain = Image.open(THEME_DIR / "assembled_with_level_ramps_preview.png").convert("RGBA")
    mountain = fit(mountain, 1100, 1130)
    canvas.alpha_composite(mountain, (40, 80))

    draw.text((55, 24), "ASSEMBLED THREE-LEVEL EXAMPLE", fill="white", font=title_font)
    draw.text((1190, 24), "RAMP SIZE CLASSES", fill="white", font=title_font)

    ramps = [
        ("LEFT / ALL LEVELS", "ramp_left.png"),
        ("FRONT / ALL LEVELS", "ramp_front.png"),
        ("RIGHT / ALL LEVELS", "ramp_right.png"),
    ]
    y_positions = [105, 535, 875]
    for (label, filename), y in zip(ramps, y_positions):
        ramp = Image.open(THEME_DIR / filename).convert("RGBA")
        canvas.alpha_composite(ramp, (1220, y + 46))
        draw.text((1190, y), label, fill=(238, 244, 246, 255), font=label_font)
        dimensions = f"{ramp.width} x {ramp.height} px"
        draw.text((1640, y + 8), dimensions, fill=(164, 188, 196, 255), font=detail_font)

    draw.text(
        (55, 1190),
        "Plate widths: base 1024 px   middle 810 px   top 619 px",
        fill=(202, 220, 225, 255),
        font=detail_font,
    )
    canvas.convert("RGB").save(OUTPUT, quality=95)
    print(OUTPUT)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
