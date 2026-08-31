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
        description="Center-crop and resize generated background art for Unity."
    )
    parser.add_argument("--input", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--size", required=True, type=parse_size)
    args = parser.parse_args()

    image = Image.open(args.input).convert("RGB")
    target_width, target_height = args.size
    target_ratio = target_width / target_height
    image_ratio = image.width / image.height

    if image_ratio > target_ratio:
        crop_width = round(image.height * target_ratio)
        left = (image.width - crop_width) // 2
        image = image.crop((left, 0, left + crop_width, image.height))
    elif image_ratio < target_ratio:
        crop_height = round(image.width / target_ratio)
        top = (image.height - crop_height) // 2
        image = image.crop((0, top, image.width, top + crop_height))

    image = image.resize((target_width, target_height), Image.Resampling.LANCZOS)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    image.save(args.output, optimize=True)
    print(f"prepared={args.output}; size={target_width}x{target_height}; mode={image.mode}")


if __name__ == "__main__":
    main()
