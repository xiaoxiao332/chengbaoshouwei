from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image


def parse_size(value: str) -> tuple[int, int]:
    width_text, height_text = value.lower().split("x", maxsplit=1)
    width = int(width_text)
    height = int(height_text)
    if width <= 0 or height <= 0:
        raise argparse.ArgumentTypeError("size must be positive")
    return width, height


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Trim and pad a transparent generated image for Unity import."
    )
    parser.add_argument("--input", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--size", required=True, type=parse_size)
    parser.add_argument("--padding", type=float, default=0.08)
    parser.add_argument("--anchor", choices=("center", "bottom"), default="bottom")
    args = parser.parse_args()

    if not 0 <= args.padding < 0.5:
        raise ValueError("padding must be in [0, 0.5)")

    image = Image.open(args.input).convert("RGBA")
    alpha = image.getchannel("A")
    bounds = alpha.getbbox()
    if bounds is None:
        raise ValueError(f"No opaque pixels found in {args.input}")

    cropped = image.crop(bounds)
    target_width, target_height = args.size
    usable_width = max(1, round(target_width * (1 - 2 * args.padding)))
    usable_height = max(1, round(target_height * (1 - 2 * args.padding)))
    scale = min(usable_width / cropped.width, usable_height / cropped.height)
    resized_width = max(1, round(cropped.width * scale))
    resized_height = max(1, round(cropped.height * scale))
    resized = cropped.resize((resized_width, resized_height), Image.Resampling.LANCZOS)

    canvas = Image.new("RGBA", (target_width, target_height), (0, 0, 0, 0))
    if args.anchor == "center":
        offset_y = (target_height - resized_height) // 2
    else:
        offset_y = target_height - resized_height - round(target_height * args.padding)
    offset = ((target_width - resized_width) // 2, offset_y)
    canvas.alpha_composite(resized, offset)

    args.output.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(args.output, optimize=True)

    corner_alpha = [canvas.getpixel(point)[3] for point in ((0, 0), (target_width - 1, 0), (0, target_height - 1), (target_width - 1, target_height - 1))]
    if any(corner_alpha):
        raise ValueError(f"Expected transparent corners in {args.output}")

    alpha_histogram = canvas.getchannel("A").histogram()
    alpha_pixels = sum(alpha_histogram[9:])
    coverage = alpha_pixels / (target_width * target_height)
    print(f"prepared={args.output}; size={target_width}x{target_height}; alpha_coverage={coverage:.3f}")


if __name__ == "__main__":
    main()
