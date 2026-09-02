from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
import re
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Iterable

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFile, ImageFont


ImageFile.LOAD_TRUNCATED_IMAGES = True

DEFAULT_SOURCE = Path(
    r"C:\Users\f_ald\source\repos\The-Tech-Idea\Art\TopDownTileSets\lowPoly_tiles"
)
DEFAULT_OUTPUT = Path(
    r"C:\Users\f_ald\source\repos\The-Tech-Idea\Beep.Godot"
    r"\addons\beep_game_builder_cs\generated\mountains"
    r"\low_poly_sandstone\source_catalog"
)

PREVIEW_SIZE = (300, 248)
COMPARISON_CELL = (720, 340)
OVERVIEW_CELL = (360, 330)


@dataclass(frozen=True)
class FamilyDefinition:
    key: str
    prefix: str
    display_name: str
    intended_role: str
    classification: str


@dataclass(frozen=True)
class SemanticDefinition:
    role: str
    classification: str
    material: str
    atlas_stage: str
    notes: tuple[str, ...] = ()


@dataclass
class CatalogEntry:
    source_id: str
    source_file: str
    source_path: str
    source_sha256: str
    source_mode: str
    source_width: int
    source_height: int
    source_has_meaningful_alpha: bool
    cleanup_method: str
    crop_box: list[int]
    cleaned_file: str
    cleaned_width: int
    cleaned_height: int
    opaque_coverage: float
    family: str
    family_display_name: str
    intended_role: str
    classification: str
    material: str
    atlas_stage: str
    direction: str
    review_status: str
    notes: list[str]


FAMILIES = (
    FamilyDefinition(
        "broad_stepped",
        "LP-BSM",
        "Broad Stepped Mountain",
        "macro_shape_reference",
        "reference_only",
    ),
    FamilyDefinition(
        "double_height_wall",
        "LP-DHC",
        "Double-Height Cliff",
        "cliff_wall_candidate",
        "candidate",
    ),
    FamilyDefinition(
        "east_west_ridge",
        "LP-EWR",
        "East-West Ridge",
        "ridge_surface_candidate",
        "candidate",
    ),
    FamilyDefinition(
        "isolated_plateau",
        "LP-ISP",
        "Isolated Plateau",
        "plateau_edge_corner_candidate",
        "candidate",
    ),
    FamilyDefinition(
        "plateau_other",
        "LP-PLT",
        "Additional Plateau",
        "plateau_candidate",
        "candidate",
    ),
)


SEMANTICS: dict[str, SemanticDefinition] = {
    "LP-BSM-01": SemanticDefinition("macro_mountain_reference", "reference_only", "sandstone", "reference"),
    "LP-BSM-02": SemanticDefinition("macro_mesa_reference", "reference_only", "sandstone", "reference"),
    "LP-BSM-03": SemanticDefinition("macro_peak_reference", "reference_only", "sandstone", "reference"),
    "LP-BSM-04": SemanticDefinition("boulder_cluster_large", "prop_candidate", "grey_rock", "prop"),
    "LP-BSM-05": SemanticDefinition("boulder_cluster_medium", "prop_candidate", "grey_rock", "prop"),
    "LP-BSM-06": SemanticDefinition("boulder_cluster_tall", "prop_candidate", "grey_rock", "prop"),
    "LP-BSM-07": SemanticDefinition("rock_spire", "prop_candidate", "grey_rock", "prop"),
    "LP-BSM-08": SemanticDefinition("pine_tree_tall", "prop_candidate", "vegetation", "prop"),
    "LP-BSM-09": SemanticDefinition("pine_tree_slender", "prop_candidate", "vegetation", "prop"),
    "LP-BSM-10": SemanticDefinition("pine_tree_broad", "prop_candidate", "vegetation", "prop"),
    "LP-BSM-11": SemanticDefinition("shrub_large", "prop_candidate", "vegetation", "prop"),
    "LP-BSM-12": SemanticDefinition("deciduous_tree", "prop_candidate", "vegetation", "prop"),
    "LP-PLT-03": SemanticDefinition("isolated_plateau_low", "terrain_candidate", "sandstone", "surface"),
    "LP-DHC-01": SemanticDefinition("straight_cliff_wall_high_a", "terrain_candidate", "sandstone", "wall"),
    "LP-DHC-02": SemanticDefinition("straight_cliff_wall_low_diagonal", "terrain_candidate", "sandstone", "wall"),
    "LP-DHC-03": SemanticDefinition("ascending_ramp_straight_a", "transition_candidate", "sandstone", "transition"),
    "LP-DHC-04": SemanticDefinition("ascending_ramp_curved_a", "transition_candidate", "sandstone", "transition"),
    "LP-DHC-05": SemanticDefinition("cave_entrance", "prop_candidate", "sandstone", "prop"),
    "LP-DHC-06": SemanticDefinition("waterfall_tall", "prop_candidate", "water", "prop"),
    "LP-DHC-07": SemanticDefinition("snow_surface_patch", "excluded_variant", "snow", "excluded"),
    "LP-DHC-08": SemanticDefinition("dead_tree", "prop_candidate", "vegetation", "prop"),
    "LP-DHC-09": SemanticDefinition("boulder_cluster_sandstone_a", "prop_candidate", "sandstone", "prop"),
    "LP-DHC-10": SemanticDefinition("cliff_wall_with_ledge", "terrain_candidate", "sandstone", "wall"),
    "LP-DHC-11": SemanticDefinition("boulder_cluster_sandstone_b", "prop_candidate", "sandstone", "prop"),
    "LP-DHC-12": SemanticDefinition("ascending_ramp_straight_b", "transition_candidate", "sandstone", "transition"),
    "LP-DHC-13": SemanticDefinition("cliff_pillar_repeatable", "terrain_candidate", "sandstone", "wall"),
    "LP-DHC-14": SemanticDefinition("snow_plateau", "excluded_variant", "sandstone_snow", "excluded"),
    "LP-DHC-15": SemanticDefinition("straight_cliff_wall_high_b", "terrain_candidate", "sandstone", "wall"),
    "LP-EWR-01": SemanticDefinition("plateau_low_front_corner_a", "terrain_candidate", "sandstone", "surface"),
    "LP-EWR-02": SemanticDefinition("plateau_with_back_wall", "terrain_candidate", "sandstone", "surface"),
    "LP-EWR-03": SemanticDefinition("plateau_low_front_corner_b", "terrain_candidate", "sandstone", "surface"),
    "LP-EWR-04": SemanticDefinition("plateau_front_notch_low", "terrain_candidate", "sandstone", "surface"),
    "LP-ISP-01": SemanticDefinition("plateau_low_isolated_a", "terrain_candidate", "sandstone", "surface"),
    "LP-ISP-02": SemanticDefinition("plateau_high_isolated", "terrain_candidate", "sandstone", "surface"),
    "LP-ISP-03": SemanticDefinition("plateau_raised_corner", "terrain_candidate", "sandstone", "surface"),
    "LP-ISP-04": SemanticDefinition("plateau_front_notch_high_a", "terrain_candidate", "sandstone", "surface"),
    "LP-ISP-05": SemanticDefinition("plateau_low_isolated_b", "terrain_candidate", "sandstone", "surface"),
    "LP-ISP-06": SemanticDefinition("plateau_with_back_wall_left", "terrain_candidate", "sandstone", "surface"),
    "LP-ISP-07": SemanticDefinition("plateau_with_back_wall_right", "terrain_candidate", "sandstone", "surface"),
    "LP-ISP-08": SemanticDefinition("plateau_front_notch_high_b", "terrain_candidate", "sandstone", "surface"),
    "LP-ISP-09": SemanticDefinition("plateau_irregular_isolated", "terrain_candidate", "sandstone", "surface"),
    "LP-ISP-10": SemanticDefinition("none", "rejected", "invalid", "rejected"),
}


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def natural_key(path: Path) -> list[object]:
    return [int(part) if part.isdigit() else part.casefold() for part in re.split(r"(\d+)", path.name)]


def family_for(path: Path) -> FamilyDefinition:
    name = path.name.casefold()
    if name.startswith("broad stepped mountain"):
        return FAMILIES[0]
    if name.startswith("double-height sandstone cliff module"):
        return FAMILIES[1]
    if name.startswith("east-west sandstone ridge tile"):
        return FAMILIES[2]
    if name.startswith("isolated four_sided sandstone cliff tiles"):
        return FAMILIES[3]
    return FAMILIES[4]


def sequence_number(path: Path, fallback: int) -> int:
    numbers = re.findall(r"(\d+)", path.stem)
    return int(numbers[-1]) if numbers else fallback


def semantic_for(source_id: str, family: FamilyDefinition) -> SemanticDefinition:
    return SEMANTICS.get(
        source_id,
        SemanticDefinition(
            family.intended_role,
            family.classification,
            "unverified",
            "unverified",
            ("semantic role requires manual classification",),
        ),
    )


def meaningful_alpha(alpha: np.ndarray) -> bool:
    transparent_fraction = float(np.mean(alpha < 250))
    return transparent_fraction > 0.005 and int(alpha.min()) < 32


def smoothstep(low: float, high: float, values: np.ndarray) -> np.ndarray:
    if high <= low:
        raise ValueError("smoothstep high must be greater than low")
    scaled = np.clip((values - low) / (high - low), 0.0, 1.0)
    return scaled * scaled * (3.0 - 2.0 * scaled)


def fill_internal_holes(mask: np.ndarray) -> np.ndarray:
    padded = cv2.copyMakeBorder(mask, 1, 1, 1, 1, cv2.BORDER_CONSTANT, value=0)
    flood = padded.copy()
    cv2.floodFill(flood, None, (0, 0), 255)
    holes = cv2.bitwise_not(flood)[1:-1, 1:-1]
    return cv2.bitwise_or(mask, holes)


def select_foreground_components(seed_mask: np.ndarray) -> np.ndarray:
    count, labels, stats, _ = cv2.connectedComponentsWithStats(seed_mask, connectivity=8)
    if count <= 1:
        return seed_mask

    areas = stats[1:, cv2.CC_STAT_AREA]
    largest = int(areas.max(initial=0))
    minimum_area = max(96, int(largest * 0.0015))
    height, width = seed_mask.shape
    keep = np.zeros(count, dtype=np.uint8)

    for label in range(1, count):
        x, y, component_width, component_height, area = stats[label]
        touches_border = x == 0 or y == 0 or x + component_width >= width or y + component_height >= height
        if area >= minimum_area and not touches_border:
            keep[label] = 255

    selected = keep[labels]
    if not np.any(selected):
        largest_label = 1 + int(np.argmax(areas))
        selected = np.where(labels == largest_label, 255, 0).astype(np.uint8)
    return selected.astype(np.uint8)


def remove_baked_pale_background(rgba: np.ndarray) -> np.ndarray:
    rgb = rgba[:, :, :3].astype(np.float32) / 255.0
    red, green, blue = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
    maximum = rgb.max(axis=2)
    minimum = rgb.min(axis=2)
    saturation = np.divide(maximum - minimum, np.maximum(maximum, 1.0e-5))
    luminance = red * 0.2126 + green * 0.7152 + blue * 0.0722
    warmth = red - blue

    saturation_confidence = smoothstep(0.025, 0.12, saturation)
    darkness_confidence = 1.0 - smoothstep(0.52, 0.79, luminance)
    warmth_confidence = smoothstep(0.018, 0.11, warmth)
    confidence = np.maximum.reduce((saturation_confidence, darkness_confidence, warmth_confidence))

    seed = np.where(confidence >= 0.16, 255, 0).astype(np.uint8)
    seed = cv2.morphologyEx(seed, cv2.MORPH_CLOSE, np.ones((5, 5), np.uint8), iterations=2)
    selected = select_foreground_components(seed)
    selected = cv2.morphologyEx(selected, cv2.MORPH_CLOSE, np.ones((7, 7), np.uint8), iterations=2)
    selected = fill_internal_holes(selected)

    feathered = cv2.GaussianBlur(selected, (0, 0), sigmaX=1.1, sigmaY=1.1)
    solid = cv2.erode(selected, np.ones((3, 3), np.uint8), iterations=1)
    feathered[solid > 0] = 255

    cleaned = rgba.copy()
    cleaned[:, :, 3] = feathered
    cleaned[cleaned[:, :, 3] == 0, :3] = 0
    return cleaned


def trim_to_alpha(image: Image.Image, padding: int = 8) -> tuple[Image.Image, list[int]]:
    alpha = np.asarray(image.getchannel("A"))
    ys, xs = np.nonzero(alpha > 8)
    if len(xs) == 0:
        return image.copy(), [0, 0, image.width, image.height]

    left = max(0, int(xs.min()) - padding)
    top = max(0, int(ys.min()) - padding)
    right = min(image.width, int(xs.max()) + padding + 1)
    bottom = min(image.height, int(ys.max()) + padding + 1)
    return image.crop((left, top, right, bottom)), [left, top, right, bottom]


def clean_source(path: Path, preserve_pale_material: bool = False) -> tuple[Image.Image, str, bool, list[int], str]:
    with Image.open(path) as source:
        source.load()
        source_mode = source.mode
        rgba_image = source.convert("RGBA")

    rgba = np.asarray(rgba_image).copy()
    has_alpha = meaningful_alpha(rgba[:, :, 3])
    if has_alpha and preserve_pale_material:
        cleaned_array = rgba
        method = "preserved_source_alpha"
    elif has_alpha:
        segmented = remove_baked_pale_background(rgba)
        segmented[:, :, 3] = np.minimum(segmented[:, :, 3], rgba[:, :, 3])
        cleaned_array = segmented
        method = "refined_source_alpha"
    else:
        cleaned_array = remove_baked_pale_background(rgba)
        method = "segmented_baked_pale_background"

    cleaned = Image.fromarray(cleaned_array, mode="RGBA")
    trimmed, crop_box = trim_to_alpha(cleaned)
    trimmed_array = np.asarray(trimmed).copy()
    trimmed_alpha = trimmed_array[:, :, 3]
    trimmed_alpha[trimmed_alpha <= 3] = 0
    trimmed_alpha[trimmed_alpha >= 252] = 255
    trimmed_array[trimmed_alpha == 0, :3] = 0
    finalized = Image.fromarray(trimmed_array, mode="RGBA")
    return finalized, source_mode, has_alpha, crop_box, method


def font(size: int, bold: bool = False) -> ImageFont.ImageFont:
    candidates = (
        Path(r"C:\Windows\Fonts\segoeuib.ttf") if bold else Path(r"C:\Windows\Fonts\segoeui.ttf"),
        Path(r"C:\Windows\Fonts\arialbd.ttf") if bold else Path(r"C:\Windows\Fonts\arial.ttf"),
    )
    for candidate in candidates:
        if candidate.exists():
            return ImageFont.truetype(str(candidate), size=size)
    return ImageFont.load_default()


def checkerboard(size: tuple[int, int], tile: int = 16) -> Image.Image:
    width, height = size
    yy, xx = np.indices((height, width))
    pattern = ((xx // tile + yy // tile) % 2).astype(np.uint8)
    dark = np.array([31, 39, 43], dtype=np.uint8)
    light = np.array([49, 58, 62], dtype=np.uint8)
    rgb = np.where(pattern[:, :, None] == 0, dark, light)
    return Image.fromarray(rgb, mode="RGB").convert("RGBA")


def invalid_placeholder(size: tuple[int, int] = PREVIEW_SIZE) -> Image.Image:
    canvas = checkerboard(size)
    draw = ImageDraw.Draw(canvas)
    margin = 24
    draw.rectangle(
        (margin, margin, size[0] - margin, size[1] - margin),
        fill=(53, 29, 30, 230),
        outline=(225, 83, 83, 255),
        width=3,
    )
    draw.line((margin + 22, margin + 22, size[0] - margin - 22, size[1] - margin - 22), fill=(225, 83, 83, 255), width=5)
    draw.line((size[0] - margin - 22, margin + 22, margin + 22, size[1] - margin - 22), fill=(225, 83, 83, 255), width=5)
    draw.text((margin + 40, size[1] // 2 - 10), "INVALID SOURCE", fill=(245, 216, 216, 255), font=font(14, True))
    return canvas


def contain(image: Image.Image, size: tuple[int, int], allow_upscale: bool = False) -> Image.Image:
    width, height = size
    scale = min(width / image.width, height / image.height)
    if not allow_upscale:
        scale = min(scale, 1.0)
    target = (max(1, round(image.width * scale)), max(1, round(image.height * scale)))
    return image.resize(target, Image.Resampling.LANCZOS)


def paste_center(canvas: Image.Image, image: Image.Image, box: tuple[int, int, int, int]) -> None:
    left, top, right, bottom = box
    fitted = contain(image, (right - left, bottom - top), allow_upscale=False)
    x = left + (right - left - fitted.width) // 2
    y = top + (bottom - top - fitted.height) // 2
    canvas.alpha_composite(fitted, (x, y))


def original_preview(path: Path) -> Image.Image:
    try:
        with Image.open(path) as image:
            image.load()
            return image.convert("RGBA")
    except Exception:
        return invalid_placeholder()


def entry_color(classification: str) -> tuple[int, int, int, int]:
    if classification == "terrain_candidate":
        return (84, 193, 138, 255)
    if classification == "transition_candidate":
        return (77, 184, 211, 255)
    if classification == "prop_candidate":
        return (132, 151, 224, 255)
    if classification == "reference_only":
        return (224, 168, 75, 255)
    if classification == "excluded_variant":
        return (170, 126, 202, 255)
    return (224, 90, 90, 255)


def create_overview(entries: list[CatalogEntry], cleaned: dict[str, Image.Image], output: Path) -> None:
    columns = 4
    rows = math.ceil(len(entries) / columns)
    width = columns * OVERVIEW_CELL[0]
    height = rows * OVERVIEW_CELL[1] + 56
    sheet = Image.new("RGBA", (width, height), (18, 24, 27, 255))
    draw = ImageDraw.Draw(sheet)
    draw.text((18, 13), "LOW-POLY SANDSTONE SOURCE CATALOG", fill=(235, 239, 238), font=font(24, True))
    draw.text((18, 39), "Green terrain  Cyan transition  Blue prop  Amber reference  Purple excluded", fill=(164, 176, 177), font=font(14))

    for index, entry in enumerate(entries):
        column = index % columns
        row = index // columns
        x = column * OVERVIEW_CELL[0]
        y = 56 + row * OVERVIEW_CELL[1]
        accent = entry_color(entry.classification)
        draw.rectangle((x + 8, y + 8, x + OVERVIEW_CELL[0] - 8, y + OVERVIEW_CELL[1] - 8), fill=(24, 31, 34), outline=accent, width=2)
        preview = checkerboard(PREVIEW_SIZE)
        paste_center(preview, cleaned[entry.source_id], (6, 6, PREVIEW_SIZE[0] - 6, PREVIEW_SIZE[1] - 6))
        sheet.alpha_composite(preview, (x + 30, y + 48))
        draw.text((x + 20, y + 17), entry.source_id, fill=accent, font=font(16, True))
        draw.text((x + 96, y + 18), entry.intended_role[:35], fill=(226, 232, 231), font=font(13))
        dimensions = f"{entry.cleaned_width}x{entry.cleaned_height}  {entry.cleanup_method}"
        draw.text((x + 20, y + 303), dimensions, fill=(142, 156, 158), font=font(11))

    sheet.convert("RGB").save(output, quality=95)


def create_family_sheets(entries: list[CatalogEntry], cleaned: dict[str, Image.Image], review_dir: Path) -> list[str]:
    written: list[str] = []
    for family in FAMILIES:
        family_entries = [entry for entry in entries if entry.family == family.key]
        if not family_entries:
            continue
        columns = 4
        rows = math.ceil(len(family_entries) / columns)
        width = columns * OVERVIEW_CELL[0]
        height = rows * OVERVIEW_CELL[1] + 54
        sheet = Image.new("RGBA", (width, height), (18, 24, 27, 255))
        draw = ImageDraw.Draw(sheet)
        draw.text((18, 13), family.display_name.upper(), fill=(235, 239, 238), font=font(24, True))
        for index, entry in enumerate(family_entries):
            column = index % columns
            row = index // columns
            x = column * OVERVIEW_CELL[0]
            y = 54 + row * OVERVIEW_CELL[1]
            accent = entry_color(entry.classification)
            draw.rectangle((x + 8, y + 8, x + OVERVIEW_CELL[0] - 8, y + OVERVIEW_CELL[1] - 8), fill=(24, 31, 34), outline=accent, width=2)
            preview = checkerboard(PREVIEW_SIZE)
            paste_center(preview, cleaned[entry.source_id], (6, 6, PREVIEW_SIZE[0] - 6, PREVIEW_SIZE[1] - 6))
            sheet.alpha_composite(preview, (x + 30, y + 48))
            draw.text((x + 20, y + 17), entry.source_id, fill=accent, font=font(16, True))
            draw.text((x + 118, y + 18), entry.intended_role[:31], fill=(214, 222, 221), font=font(12))
            draw.text((x + 20, y + 303), Path(entry.source_file).stem[:46], fill=(142, 156, 158), font=font(11))
        filename = f"family_{family.key}.png"
        sheet.convert("RGB").save(review_dir / filename, quality=95)
        written.append(filename)
    return written


def create_comparison_pages(
    entries: list[CatalogEntry],
    cleaned: dict[str, Image.Image],
    source_dir: Path,
    review_dir: Path,
) -> list[str]:
    per_page = 6
    written: list[str] = []
    title = font(20, True)
    label = font(14, True)
    small = font(12)

    for page_index in range(math.ceil(len(entries) / per_page)):
        page_entries = entries[page_index * per_page : (page_index + 1) * per_page]
        width = COMPARISON_CELL[0]
        height = 58 + len(page_entries) * COMPARISON_CELL[1]
        sheet = Image.new("RGBA", (width, height), (18, 24, 27, 255))
        draw = ImageDraw.Draw(sheet)
        draw.text((16, 12), f"CLEANUP COMPARISON {page_index + 1}", fill=(235, 239, 238), font=title)
        draw.text((16, 38), "Original framing (left) and cleaned alpha sprite (right)", fill=(154, 166, 168), font=small)

        for row, entry in enumerate(page_entries):
            y = 58 + row * COMPARISON_CELL[1]
            draw.rectangle((8, y + 6, width - 8, y + COMPARISON_CELL[1] - 6), fill=(24, 31, 34), outline=(65, 78, 82), width=1)
            draw.text((18, y + 13), entry.source_id, fill=entry_color(entry.classification), font=label)
            draw.text((108, y + 14), Path(entry.source_file).stem[:70], fill=(214, 222, 221), font=small)

            left_panel = checkerboard((330, 280))
            right_panel = checkerboard((330, 280))
            paste_center(left_panel, original_preview(source_dir / entry.source_file), (8, 8, 322, 272))
            paste_center(right_panel, cleaned[entry.source_id], (8, 8, 322, 272))
            sheet.alpha_composite(left_panel, (18, y + 44))
            sheet.alpha_composite(right_panel, (372, y + 44))
            draw.text((20, y + 315), "ORIGINAL", fill=(142, 156, 158), font=small)
            draw.text((374, y + 315), f"CLEANED  {entry.cleaned_width}x{entry.cleaned_height}", fill=(142, 156, 158), font=small)

        filename = f"cleanup_comparison_{page_index + 1:02d}.png"
        sheet.convert("RGB").save(review_dir / filename, quality=95)
        written.append(filename)
    return written


def write_csv(entries: Iterable[CatalogEntry], path: Path) -> None:
    rows = [asdict(entry) for entry in entries]
    if not rows:
        return
    with path.open("w", newline="", encoding="utf-8") as stream:
        writer = csv.DictWriter(stream, fieldnames=list(rows[0].keys()))
        writer.writeheader()
        for row in rows:
            row["notes"] = " | ".join(row["notes"])
            writer.writerow(row)


def build_catalog(source_dir: Path, output_dir: Path) -> dict[str, object]:
    if not source_dir.is_dir():
        raise FileNotFoundError(f"Source directory does not exist: {source_dir}")

    source_paths = sorted(source_dir.glob("*.png"), key=natural_key)
    if not source_paths:
        raise RuntimeError(f"No PNG files found in: {source_dir}")

    cleaned_dir = output_dir / "cleaned"
    review_dir = output_dir / "review"
    cleaned_dir.mkdir(parents=True, exist_ok=True)
    review_dir.mkdir(parents=True, exist_ok=True)

    hashes_before = {path.name: sha256(path) for path in source_paths}
    family_counters: dict[str, int] = {}
    entries: list[CatalogEntry] = []
    cleaned_images: dict[str, Image.Image] = {}

    for fallback_index, source_path in enumerate(source_paths, start=1):
        family = family_for(source_path)
        family_counters[family.key] = family_counters.get(family.key, 0) + 1
        source_number = sequence_number(source_path, family_counters[family.key])
        source_id = f"{family.prefix}-{source_number:02d}"
        semantic = semantic_for(source_id, family)

        cleaned_filename = f"{source_id.lower()}_{family.key}.png"
        cleaned_relative_file = f"cleaned/{cleaned_filename}"
        notes: list[str] = []
        classification = semantic.classification
        intended_role = semantic.role
        review_status = "needs_visual_review"
        notes.extend(semantic.notes)

        try:
            with Image.open(source_path) as source:
                source.load()
                source_width, source_height = source.size

            cleaned, source_mode, has_alpha, crop_box, cleanup_method = clean_source(
                source_path,
                preserve_pale_material="snow" in semantic.material,
            )
            cleaned_path = cleaned_dir / cleaned_filename
            cleaned.save(cleaned_path, format="PNG", optimize=True)
            cleaned_images[source_id] = cleaned

            alpha = np.asarray(cleaned.getchannel("A"))
            coverage = float(np.mean(alpha > 16))
            if not has_alpha:
                notes.append("baked pale background segmented")
            if semantic.classification == "reference_only":
                notes.append("do not use as a repeatable terrain tile")
            if family.key == "east_west_ridge" and semantic.atlas_stage == "surface":
                notes.append("directional counterpart still required")
        except Exception as error:
            source_width = 0
            source_height = 0
            source_mode = "invalid"
            has_alpha = False
            crop_box = [0, 0, 0, 0]
            cleanup_method = "invalid_source"
            cleaned_relative_file = ""
            coverage = 0.0
            classification = "rejected"
            intended_role = "none"
            review_status = "rejected_invalid_source"
            notes.append(f"unreadable image: {type(error).__name__}: {error}")
            cleaned_images[source_id] = invalid_placeholder()

        entries.append(
            CatalogEntry(
                source_id=source_id,
                source_file=source_path.name,
                source_path=str(source_path),
                source_sha256=hashes_before[source_path.name],
                source_mode=source_mode,
                source_width=source_width,
                source_height=source_height,
                source_has_meaningful_alpha=has_alpha,
                cleanup_method=cleanup_method,
                crop_box=crop_box,
                cleaned_file=cleaned_relative_file,
                cleaned_width=cleaned_images[source_id].width if cleaned_relative_file else 0,
                cleaned_height=cleaned_images[source_id].height if cleaned_relative_file else 0,
                opaque_coverage=round(coverage, 5),
                family=family.key,
                family_display_name=family.display_name,
                intended_role=intended_role,
                classification=classification,
                material=semantic.material,
                atlas_stage=semantic.atlas_stage,
                direction="unverified",
                review_status=review_status,
                notes=notes,
            )
        )

    overview_name = "source_catalog_overview.png"
    create_overview(entries, cleaned_images, review_dir / overview_name)
    family_sheets = create_family_sheets(entries, cleaned_images, review_dir)
    comparison_pages = create_comparison_pages(entries, cleaned_images, source_dir, review_dir)

    hashes_after = {path.name: sha256(path) for path in source_paths}
    unchanged = hashes_before == hashes_after
    if not unchanged:
        changed = [name for name in hashes_before if hashes_before[name] != hashes_after.get(name)]
        raise RuntimeError(f"Source files changed during catalog build: {changed}")

    family_summary = {
        family.key: sum(1 for entry in entries if entry.family == family.key)
        for family in FAMILIES
    }
    manifest: dict[str, object] = {
        "schema_version": 1,
        "pack_id": "low_poly_sandstone",
        "source_directory": str(source_dir),
        "output_directory": str(output_dir),
        "source_count": len(entries),
        "usable_source_count": sum(1 for entry in entries if entry.classification != "rejected"),
        "rejected_source_count": sum(1 for entry in entries if entry.classification == "rejected"),
        "source_files_unchanged": unchanged,
        "family_counts": family_summary,
        "classification_counts": {
            classification: sum(1 for entry in entries if entry.classification == classification)
            for classification in sorted({entry.classification for entry in entries})
        },
        "atlas_stage_counts": {
            stage: sum(1 for entry in entries if entry.atlas_stage == stage)
            for stage in sorted({entry.atlas_stage for entry in entries})
        },
        "projection_status": "unverified",
        "catalog_status": "needs_visual_review",
        "review_artifacts": {
            "overview": f"review/{overview_name}",
            "family_sheets": [f"review/{name}" for name in family_sheets],
            "cleanup_comparisons": [f"review/{name}" for name in comparison_pages],
        },
        "entries": [asdict(entry) for entry in entries],
    }

    with (output_dir / "source_catalog.json").open("w", encoding="utf-8") as stream:
        json.dump(manifest, stream, indent=2, ensure_ascii=True)
        stream.write("\n")
    write_csv(entries, output_dir / "source_catalog.csv")
    with (output_dir / "source_hashes.json").open("w", encoding="utf-8") as stream:
        json.dump(hashes_after, stream, indent=2, ensure_ascii=True)
        stream.write("\n")

    return manifest


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build a clean low-poly mountain source catalog.")
    parser.add_argument("--source", type=Path, default=DEFAULT_SOURCE)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    manifest = build_catalog(args.source.resolve(), args.output.resolve())
    print(f"Built {manifest['source_count']} cleaned source sprites")
    print(f"Output: {manifest['output_directory']}")
    print(f"Sources unchanged: {manifest['source_files_unchanged']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
