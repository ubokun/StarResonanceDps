# This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.
from pathlib import Path

from PIL import Image


SOURCE_FILES = {
    32: Path("ApplicationIcon_32x32.png"),
    48: Path("ApplicationIcon_48x48.png"),
    64: Path("ApplicationIcon_64x64.png"),
    256: Path("ApplicationIcon.png"),
}

OUTPUT_FILE = Path("ApplicationIcon.ico")

# ウィンドウ左上のヘッダーに表示の大きさ(22)そのままで出す画像。実行時に縮めさせない
HEADER_ICON_FILE = Path("ApplicationIcon_22x22.png")
HEADER_ICON_SIZE = 22
HEADER_ICON_SOURCE_SIZE = 48

TARGET_SIZES = (16, 20, 24, 32, 40, 48, 64, 256)

SOURCE_SIZE_FOR_TARGET = {
    16: 32,
    20: 32,
    24: 32,
    32: 32,
    40: 48,
    48: 48,
    64: 64,
    256: 256,
}


def load_source(path: Path, expected_size: int) -> Image.Image:
    if not path.is_file():
        raise FileNotFoundError(f"入力画像が見つかりません: {path}")

    with Image.open(path) as image:
        image.load()

        expected = (expected_size, expected_size)
        if image.size != expected:
            raise ValueError(
                f"{path} のサイズが不正です。"
                f" 期待値: {expected[0]}x{expected[1]}"
                f" / 実際: {image.width}x{image.height}"
            )

        return image.convert("RGBA")


def resize_rgba(image: Image.Image, size: int) -> Image.Image:
    if image.size == (size, size):
        return image.copy()

    premultiplied = image.convert("RGBa")
    resized = premultiplied.resize(
        (size, size),
        Image.Resampling.LANCZOS,
    )
    return resized.convert("RGBA")


def main() -> None:
    source_images = {
        size: load_source(path, size)
        for size, path in SOURCE_FILES.items()
    }

    frames: dict[int, Image.Image] = {}

    for target_size in TARGET_SIZES:
        source_size = SOURCE_SIZE_FOR_TARGET[target_size]
        frames[target_size] = resize_rgba(
            source_images[source_size],
            target_size,
        )

    # 256pxを主フレームにし、他サイズを追加
    primary = frames[256]
    additional_frames = [
        frames[size]
        for size in TARGET_SIZES
        if size != 256
    ]

    primary.save(
        OUTPUT_FILE,
        format="ICO",
        sizes=[(size, size) for size in TARGET_SIZES],
        append_images=additional_frames,
        bitmap_format="png",
    )

    with Image.open(OUTPUT_FILE) as icon:
        actual_sizes = sorted(icon.ico.sizes())

    expected_sizes = sorted((size, size) for size in TARGET_SIZES)

    if actual_sizes != expected_sizes:
        raise RuntimeError(
            "ICOのフレーム構成が期待値と一致しません。\n"
            f"期待値: {expected_sizes}\n"
            f"実際: {actual_sizes}"
        )

    print(f"生成完了: {OUTPUT_FILE.resolve()}")
    print("収録サイズ:")
    for width, height in actual_sizes:
        print(f"  {width}x{height}")

    header_icon = resize_rgba(
        source_images[HEADER_ICON_SOURCE_SIZE],
        HEADER_ICON_SIZE,
    )
    header_icon.save(HEADER_ICON_FILE, format="PNG")
    print(f"生成完了: {HEADER_ICON_FILE.resolve()}")


if __name__ == "__main__":
    main()