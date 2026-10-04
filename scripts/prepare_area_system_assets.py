#!/usr/bin/env python3
"""Prepare the Phase 3 C2 Asset Importer source tree from the two authoritative archives.

Usage:
  python3 scripts/prepare_area_system_assets.py --industrial CS2_AS_IndustrialHeadquarters.zip \
      --park CS2_AS_ParkSet_HQ_Gate.zip --out <directory>
  python3 scripts/prepare_area_system_assets.py --self-test

The archives are verified by SHA-256 (docs/content/area-system-built-in-assets-contract.md). Every authored file and folder
is renamed to its exact AS_ identifier and the Park Gate's shared-texture settings.json is rewritten to the renamed Park
Headquarters textures, so the atlas is shared rather than duplicated. Nothing else is changed: meshes and textures are
copied byte-for-byte. The output is the Asset Importer "Project Root"; import it as one batch.
"""
import argparse
import hashlib
import json
import os
import sys
import tempfile
import zipfile

PACKAGE_ID = "AS_AreaSystemAssets"
SUPERSEDED = "CS2_AS_ParkHeadquarters.zip"
ARCHIVES = {
    "industrial": ("CS2_AS_IndustrialHeadquarters.zip", "30b4138e2ce7b8c57a89ec783a57f5bf3ee4a233f2ce8beff01203c0ca14b1a3"),
    "park": ("CS2_AS_ParkSet_HQ_Gate.zip", "b4e0d3c249552c3b8922000c93a53b7a17198745e1748bd1ef445e1e4852d175"),
}
# (archive key, source folder inside the archive, source asset name, AS_ identifier, output folder)
ASSETS = [
    ("industrial", "ProjectFiles/IndustrialAreaHQ01_5x4/", "IndustrialAreaHQ01_5x4", "AS_IndustrialHeadquarters", "AS_IndustrialHeadquarters"),
    ("park", "ProjectFiles/ParkSet/ParkAreaHQ01_4x3/", "ParkAreaHQ01_4x3", "AS_ParkHeadquarters", "AS_ParkSet/AS_ParkHeadquarters"),
    ("park", "ProjectFiles/ParkSet/ParkGate01_2x1/", "ParkGate01_2x1", "AS_ParkGate", "AS_ParkSet/AS_ParkGate"),
]
RENAMES = {source: target for _, _, source, target, _ in ASSETS}


class PrepareError(Exception):
    pass


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as stream:
        for block in iter(lambda: stream.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


def renamed(file_name, source, target):
    if not file_name.startswith(source + "_") and not file_name.startswith(source + "."):
        raise PrepareError(f"unexpected file name {file_name!r} in {source}")
    return target + file_name[len(source):]


def rewrite_settings(data):
    settings = json.loads(data.decode("utf-8"))
    shared = settings.get("sharedAssets")
    if not isinstance(shared, dict) or not shared:
        raise PrepareError("ParkGate settings.json has no sharedAssets")
    rewritten = {}
    for key, value in shared.items():
        new_key = renamed(key, "ParkGate01_2x1", "AS_ParkGate")
        folder, _, name = value.rpartition("/")
        if folder != "../ParkAreaHQ01_4x3":
            raise PrepareError(f"shared texture {value!r} does not point at the Park Headquarters folder")
        rewritten[new_key] = "../AS_ParkHeadquarters/" + renamed(name, "ParkAreaHQ01_4x3", "AS_ParkHeadquarters")
    settings["sharedAssets"] = rewritten
    return (json.dumps(settings, indent=2, ensure_ascii=False) + "\n").encode("utf-8")


def prepare(archives, out_dir, check_hashes=True):
    for key, path in archives.items():
        expected_name, expected_hash = ARCHIVES[key]
        if os.path.basename(path) == SUPERSEDED:
            raise PrepareError(f"{SUPERSEDED} is superseded and is not a C2 source")
        if os.path.basename(path) != expected_name:
            raise PrepareError(f"{key} archive must be {expected_name}, got {os.path.basename(path)}")
        if check_hashes and sha256(path) != expected_hash:
            raise PrepareError(f"{expected_name} SHA-256 does not match the contract")
    root = os.path.join(out_dir, PACKAGE_ID)
    if os.path.exists(root):
        raise PrepareError(f"{root} already exists; choose an empty output directory")
    manifest = {"package": PACKAGE_ID, "archives": {ARCHIVES[k][0]: ARCHIVES[k][1] for k in archives}, "files": {}}
    for key, folder, source, target, out_folder in ASSETS:
        with zipfile.ZipFile(archives[key]) as archive:
            members = [m for m in archive.namelist() if m.startswith(folder) and not m.endswith("/")]
            if not any(m.endswith(source + ".fbx") for m in members):
                raise PrepareError(f"{source}.fbx missing from {ARCHIVES[key][0]}")
            for member in sorted(members):
                name = member[len(folder):]
                if "/" in name:
                    raise PrepareError(f"unexpected nested entry {member!r}")
                data = archive.read(member)
                if name == "settings.json":
                    if target != "AS_ParkGate":
                        raise PrepareError(f"unexpected settings.json in {source}")
                    data, out_name = rewrite_settings(data), name
                else:
                    out_name = renamed(name, source, target)
                relative = f"{out_folder}/{out_name}"
                destination = os.path.join(root, *relative.split("/"))
                os.makedirs(os.path.dirname(destination), exist_ok=True)
                with open(destination, "wb") as stream:
                    stream.write(data)
                manifest["files"][relative] = hashlib.sha256(data).hexdigest()
    with open(os.path.join(root, "manifest.json"), "w", encoding="utf-8") as stream:
        json.dump(manifest, stream, indent=2, sort_keys=True)
        stream.write("\n")
    return root, manifest


def self_test():
    with tempfile.TemporaryDirectory() as work:
        industrial = os.path.join(work, ARCHIVES["industrial"][0])
        park = os.path.join(work, ARCHIVES["park"][0])
        with zipfile.ZipFile(industrial, "w") as z:
            for suffix in (".fbx", "_LOD1.fbx", "_Win.fbx", "_BaseColor.png"):
                z.writestr("ProjectFiles/IndustrialAreaHQ01_5x4/IndustrialAreaHQ01_5x4" + suffix, suffix)
            z.writestr("README_ja.md", "readme")
        gate = {"sharedAssets": {"ParkGate01_2x1_BaseColor.png": "../ParkAreaHQ01_4x3/ParkAreaHQ01_4x3_BaseColor.png"}}
        with zipfile.ZipFile(park, "w") as z:
            for suffix in (".fbx", "_LOD2.fbx", "_BaseColor.png"):
                z.writestr("ProjectFiles/ParkSet/ParkAreaHQ01_4x3/ParkAreaHQ01_4x3" + suffix, suffix)
            for suffix in (".fbx", "_LOD1.fbx"):
                z.writestr("ProjectFiles/ParkSet/ParkGate01_2x1/ParkGate01_2x1" + suffix, suffix)
            z.writestr("ProjectFiles/ParkSet/ParkGate01_2x1/settings.json", json.dumps(gate))
        root, manifest = prepare({"industrial": industrial, "park": park}, os.path.join(work, "out"), check_hashes=False)
        files = sorted(manifest["files"])
        assert files == sorted([
            "AS_IndustrialHeadquarters/AS_IndustrialHeadquarters.fbx", "AS_IndustrialHeadquarters/AS_IndustrialHeadquarters_LOD1.fbx",
            "AS_IndustrialHeadquarters/AS_IndustrialHeadquarters_Win.fbx", "AS_IndustrialHeadquarters/AS_IndustrialHeadquarters_BaseColor.png",
            "AS_ParkSet/AS_ParkHeadquarters/AS_ParkHeadquarters.fbx", "AS_ParkSet/AS_ParkHeadquarters/AS_ParkHeadquarters_LOD2.fbx",
            "AS_ParkSet/AS_ParkHeadquarters/AS_ParkHeadquarters_BaseColor.png", "AS_ParkSet/AS_ParkGate/AS_ParkGate.fbx",
            "AS_ParkSet/AS_ParkGate/AS_ParkGate_LOD1.fbx", "AS_ParkSet/AS_ParkGate/settings.json"]), files
        with open(os.path.join(root, "AS_ParkSet", "AS_ParkGate", "settings.json"), encoding="utf-8") as stream:
            assert json.load(stream) == {"sharedAssets": {"AS_ParkGate_BaseColor.png": "../AS_ParkHeadquarters/AS_ParkHeadquarters_BaseColor.png"}}
        with open(os.path.join(root, "AS_IndustrialHeadquarters", "AS_IndustrialHeadquarters_LOD1.fbx"), "rb") as stream:
            assert stream.read() == b"_LOD1.fbx", "content must be copied byte-for-byte"
        superseded = os.path.join(work, SUPERSEDED)
        os.rename(park, superseded)
        try:
            prepare({"industrial": industrial, "park": superseded}, os.path.join(work, "out2"), check_hashes=False)
            raise AssertionError("superseded archive accepted")
        except PrepareError as error:
            assert "superseded" in str(error)
        try:
            renamed("ParkGate01_2x1Extra.fbx", "ParkGate01_2x1", "AS_ParkGate")
            raise AssertionError("look-alike file name accepted")
        except PrepareError:
            pass
    print("prepare_area_system_assets self-test passed.")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--industrial")
    parser.add_argument("--park")
    parser.add_argument("--out")
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    if args.self_test:
        self_test()
        return 0
    if not (args.industrial and args.park and args.out):
        parser.error("--industrial, --park and --out are required")
    try:
        root, manifest = prepare({"industrial": args.industrial, "park": args.park}, args.out)
    except PrepareError as error:
        print(f"error: {error}", file=sys.stderr)
        return 1
    print(f"Prepared {len(manifest['files'])} files in {root}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
