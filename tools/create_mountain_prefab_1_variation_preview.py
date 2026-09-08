from __future__ import annotations

import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


PROJECT_ROOT = Path(__file__).resolve().parents[1]
THEME_ROOT = PROJECT_ROOT / (
    "addons/beep_game_builder_cs/generated/mountains/low_poly_sandstone/"
    "authored_prefabs/modular_themes"
)
CATALOG_PATH = THEME_ROOT / "modular_mountain_theme_catalog.json"
OUTPUT_PATH = THEME_ROOT / "mountain_prefab_1_all_variations_preview.png"
BACKGROUND = (19, 32, 39, 255)
PANEL = (23, 39, 47, 255)


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
    catalog = json.loads(CATALOG_PATH.read_text(encoding="utf-8"))
    themes = catalog["themes"]
    canvas = Image.new("RGBA", (2000, 1280), BACKGROUND)
    draw = ImageDraw.Draw(canvas)
    title_font = font(42)
    style_font = font(20)
    label_font = font(25)
    draw.text((50, 32), "MOUNTAIN PREFAB 1 - ALL MATERIAL VARIATIONS", fill="white", font=title_font)

    cell_width = 480
    cell_height = 560
    image_width = 440
    image_height = 470
    for index, theme in enumerate(themes):
        row = index // 4
        column = index % 4
        x = 40 + column * 490
        y = 100 + row * 575
        draw.rectangle((x, y, x + cell_width, y + cell_height), fill=PANEL)
        preview_path = PROJECT_ROOT / theme["three_level_preview"]
        preview = fit(Image.open(preview_path).convert("RGBA"), image_width, image_height)
        image_x = x + (cell_width - preview.width) // 2
        image_y = y + 66 + (image_height - preview.height) // 2
        canvas.alpha_composite(preview, (image_x, image_y))
        draw.text(
            (x + 18, y + 10),
            theme["style_label"].upper(),
            fill=(158, 184, 193, 255),
            font=style_font,
        )
        draw.text(
            (x + 18, y + 34),
            theme["label"],
            fill=(232, 240, 243, 255),
            font=label_font,
        )

    canvas.convert("RGB").save(OUTPUT_PATH, quality=95)
    print(OUTPUT_PATH)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
