"""Package Lua, JSON and theme PNGs without Unity or executing gameplay."""
import argparse
import hashlib
import json
import re
import shutil
import time
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
LUA = ROOT / "Assets/StreamingAssets/LoopSort/lua"
RESOURCES = ROOT / "Assets/LoopSort/Resources/LoopSort"


def describe(path, relative):
    data = path.read_bytes()
    return {"path": relative, "size": len(data), "sha256": hashlib.sha256(data).hexdigest()}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--embedded-only", action="store_true")
    parser.add_argument("--version-code", type=int, default=int(time.time()))
    parser.add_argument("--channel", help="Defaults to HotUpdateConfig.json channel")
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()
    config = json.loads((RESOURCES / "HotUpdateConfig.json").read_text("utf-8-sig"))
    channel = args.channel or config.get("channel") or "production"
    binding = json.loads((RESOURCES / "BindingId.json").read_text("utf-8-sig"))["id"]
    settings = (ROOT / "ProjectSettings/ProjectSettings.asset").read_text("utf-8-sig")
    match = re.search(r"^\s*bundleVersion:\s*(.+?)\s*$", settings, re.MULTILINE)
    if not match:
        raise RuntimeError("Cannot read the player version")
    manifest = {"schema": 1, "bridgeApi": 1, "bindingId": binding,
                "version": "embedded" if args.embedded_only else datetime.now(timezone.utc).strftime("%Y.%m.%d.%H%M%S"),
                "versionCode": 0 if args.embedded_only else args.version_code,
                "binaryVersion": match.group(1), "channel": channel, "platform": "any"}
    sources = {"lua/" + path.relative_to(LUA).as_posix(): path for path in LUA.rglob("*.lua")}
    if not sources:
        raise RuntimeError("No Lua modules found")
    if args.embedded_only:
        output = LUA.parent
    else:
        if args.version_code < 1:
            raise ValueError("Version code must be positive")
        output = (args.out or ROOT / "Builds/HotUpdate/any" / str(args.version_code)).resolve()
        if output.exists():
            raise FileExistsError("Choose a new version/output directory: " + str(output))
        output.mkdir(parents=True)
        for path in RESOURCES.rglob("*.json"):
            relative = path.relative_to(RESOURCES).as_posix()
            if relative in ("BindingId.json", "HotUpdateConfig.json", "Data/ui-skin.json"):
                continue
            sources["data/" + (relative[5:] if relative.startswith("Data/") else relative)] = path
        for folder in ("Theme", "Portraits", "Textures"):
            for path in (RESOURCES / folder).rglob("*.png"):
                sources["images/" + path.relative_to(RESOURCES).as_posix()] = path
        for relative, source in sources.items():
            destination = output / relative
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, destination)
    manifest["files"] = [describe(source, relative) for relative, source in sorted(sources.items())]
    manifest_path = output / "manifest.json"
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"manifest": str(manifest_path), "files": len(manifest["files"]),
                      "payloadBytes": sum(item["size"] for item in manifest["files"]), "bindingId": binding}, ensure_ascii=False))


if __name__ == "__main__":
    main()
