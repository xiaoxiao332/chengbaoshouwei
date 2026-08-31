from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image


def _axis_scores(image: Image.Image, vertical: bool) -> list[float]:
    gray = image.convert("L")
    width, height = gray.size
    pixels = list(gray.get_flattened_data())

    if vertical:
        scores = [0.0] * width
        for y in range(height):
            row = y * width
            for x in range(width):
                scores[x] += pixels[row + x]
        return [score / height for score in scores]

    scores = [0.0] * height
    for y in range(height):
        row = y * width
        scores[y] = sum(pixels[row : row + width]) / width
    return scores


def _smooth(scores: list[float], radius: int = 2) -> list[float]:
    result: list[float] = []
    for index in range(len(scores)):
        start = max(0, index - radius)
        end = min(len(scores), index + radius + 1)
        result.append(sum(scores[start:end]) / (end - start))
    return result


def _find_boundaries(
    scores: list[float], cell_count: int, separator: str
) -> list[int]:
    length = len(scores)
    nominal = length / cell_count
    if separator == "uniform":
        return [round(index * nominal) for index in range(cell_count + 1)]
    search_radius = max(4, round(nominal * 0.22))
    smoothed = _smooth(scores)
    boundaries = [0]

    for index in range(1, cell_count):
        expected = round(index * nominal)
        start = max(boundaries[-1] + round(nominal * 0.55), expected - search_radius)
        end = min(length - 1, expected + search_radius)
        candidates = range(start, end + 1)
        boundary = (
            max(candidates, key=smoothed.__getitem__)
            if separator == "light"
            else min(candidates, key=smoothed.__getitem__)
        )
        boundaries.append(boundary)

    boundaries.append(length)
    return boundaries


def prepare(
    source_path: Path,
    output_path: Path,
    columns: int,
    rows: int,
    tile_size: int,
    inset: int,
    separator: str,
) -> tuple[list[int], list[int]]:
    with Image.open(source_path) as opened:
        source = opened.convert("RGBA")

    x_bounds = _find_boundaries(
        _axis_scores(source, vertical=True), columns, separator
    )
    y_bounds = _find_boundaries(
        _axis_scores(source, vertical=False), rows, separator
    )
    atlas = Image.new("RGBA", (columns * tile_size, rows * tile_size))

    for row in range(rows):
        for column in range(columns):
            left = min(x_bounds[column] + inset, x_bounds[column + 1] - 1)
            top = min(y_bounds[row] + inset, y_bounds[row + 1] - 1)
            right = max(left + 1, x_bounds[column + 1] - inset)
            bottom = max(top + 1, y_bounds[row + 1] - inset)
            tile = source.crop((left, top, right, bottom)).resize(
                (tile_size, tile_size), Image.Resampling.LANCZOS
            )
            atlas.paste(tile, (column * tile_size, row * tile_size))

    output_path.parent.mkdir(parents=True, exist_ok=True)
    atlas.save(output_path)
    return x_bounds, y_bounds


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Remove generated atlas grid lines and create an exact-size tile sheet."
    )
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--columns", type=int, required=True)
    parser.add_argument("--rows", type=int, required=True)
    parser.add_argument("--tile-size", type=int, default=32)
    parser.add_argument("--inset", type=int, default=4)
    parser.add_argument(
        "--separator",
        choices=("dark", "light", "uniform"),
        default="dark",
        help="Divider detection mode; use uniform for a geometrically regular sheet.",
    )
    args = parser.parse_args()

    x_bounds, y_bounds = prepare(
        args.source,
        args.output,
        args.columns,
        args.rows,
        args.tile_size,
        args.inset,
        args.separator,
    )
    print(f"x boundaries: {x_bounds}")
    print(f"y boundaries: {y_bounds}")
    print(f"saved: {args.output}")


if __name__ == "__main__":
    main()
