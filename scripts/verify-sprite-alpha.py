from collections import deque
from pathlib import Path
import sys

from PIL import Image


def component_count(image: Image.Image) -> int:
    alpha = image.getchannel("A")
    width, height = image.size
    remaining = {(x, y) for y in range(height) for x in range(width) if alpha.getpixel((x, y)) == 255}
    count = 0
    while remaining:
        count += 1
        queue = deque([remaining.pop()])
        while queue:
            x, y = queue.popleft()
            for ny in range(max(0, y - 1), min(height, y + 2)):
                for nx in range(max(0, x - 1), min(width, x + 2)):
                    if (nx, ny) in remaining:
                        remaining.remove((nx, ny))
                        queue.append((nx, ny))
    return count


asset_dir = Path(__file__).resolve().parents[1] / "src" / "QuotaPet.App" / "Assets"
expected_paths = [
    asset_dir / f"pet-{state}-{index}.png"
    for state in ("swim", "rest")
    for index in range(1, 5)
]
existing_paths = [path for path in expected_paths if path.exists()]
failures = []

if not existing_paths:
    print("No custom sprite frames found; QuotaPet will use the built-in placeholder pet.")
    sys.exit(0)

missing_paths = [path for path in expected_paths if not path.exists()]
if missing_paths:
    failures.append(
        "missing frames: " + ", ".join(path.name for path in missing_paths)
    )

for path in sorted(existing_paths):
    image = Image.open(path).convert("RGBA")
    if image.size != (112, 112):
        failures.append(f"{path.name}: expected 112x112, found {image.width}x{image.height}")
        continue
    alpha_values = set(image.getchannel("A").getdata())
    if not alpha_values.issubset({0, 255}):
        failures.append(f"{path.name}: contains partial alpha values")
        continue
    components = component_count(image)
    if components != 1:
        failures.append(f"{path.name}: expected 1 visible component, found {components}")

if failures:
    print("Sprite alpha verification failed:")
    print("\n".join(f"- {failure}" for failure in failures))
    sys.exit(1)

print("Verified 8 custom sprite frames: 112x112, binary alpha, one visible component per frame.")
