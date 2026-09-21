"""Download wiki images into remote_assets following the YAML entry hierarchy."""
import concurrent.futures
import io
from pathlib import Path
import urllib.request

from PIL import Image
import yaml

ROOT = Path(__file__).resolve().parents[1]
DESTINATION = ROOT / "remote_assets"


def entries(nodes, parents=()):
    for node in nodes:
        path = (*parents, node["id"])
        if any(part in ("", ".", "..") or any(c in part for c in '/\\:') for part in path):
            raise ValueError(f"Invalid entry path: {path}")
        if node.get("image"):
            yield path, node["image"]
        yield from entries(node.get("children", []), path)


def download(entry):
    path, url = entry
    target = DESTINATION.joinpath(*path).with_suffix(".png")
    if target.is_file():
        with Image.open(target) as image:
            image.verify()
        return f"OK {target.relative_to(ROOT)}"
    for attempt in range(2):
        try:
            request = urllib.request.Request(url, headers={"User-Agent": "MoonDex-AssetDownload/1.0"})
            with urllib.request.urlopen(request, timeout=20) as response:
                data = response.read()
            with Image.open(io.BytesIO(data)) as image:
                image.verify()
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
            return f"OK {target.relative_to(ROOT)}"
        except Exception as error:
            if attempt == 1:
                return f"FAILED {'/'.join(path)}: {error}"


if __name__ == "__main__":
    document = yaml.safe_load((ROOT / "Assets/data/database.yaml").read_text(encoding="utf-8"))
    pending = list(entries(document["items"]))
    failures = []
    with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:
        for result in pool.map(download, pending):
            print(result, flush=True)
            if result.startswith("FAILED"):
                failures.append(result)
    print(f"Downloaded/verified {len(pending) - len(failures)}/{len(pending)} images.", flush=True)
    raise SystemExit(bool(failures))
