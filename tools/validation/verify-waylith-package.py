#!/usr/bin/env python3
"""Static RimWorld 1.7 / Waylith mod packaging checks, no game launch."""
from pathlib import Path
import hashlib
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]

def ensure(ok, message):
    if not ok:
        raise RuntimeError(message)

def digest(path):
    return hashlib.sha256(path.read_bytes()).digest()

about = ET.parse(ROOT / "About" / "About.xml").getroot()
versions = {x.text for x in about.findall("./supportedVersions/li")}
ensure({"1.6", "1.7"} <= versions, "Both RimWorld versions must be supported")

folders = ET.parse(ROOT / "LoadFolders.xml").getroot()
for version in ("1.6", "1.7"):
    paths = {x.text for x in folders.findall(f"./v{version}/li")}
    ensure("/" in paths and version in paths, f"Bad load folders for {version}")
    ensure(("1.7" if version == "1.6" else "1.6") not in paths, "Cross-version assembly loading")

for folder in ("Defs", "Textures"):
    source = ROOT / "1.6" / folder
    dest = ROOT / "1.7" / folder
    source_files = {p.relative_to(source) for p in source.rglob("*") if p.is_file()}
    dest_files = {p.relative_to(dest) for p in dest.rglob("*") if p.is_file()}
    ensure(source_files == dest_files, f"Missing or extra {folder} files in 1.7")
    for f in source_files:
        ensure(digest(source / f) == digest(dest / f), f"1.7 {folder} out of sync: {f}")

binary = ROOT / "1.7" / "Assemblies" / "DeepRim.Vertical.dll"
ensure(binary.is_file() and binary.stat().st_size > 1024, "No 1.7 compiled assembly")
ensure(binary.open("rb").read(2) == b"MZ", "1.7 compiled assembly is not a PE DLL")
source = (ROOT / "Source" / "VerticalMaps" / "VerticalMapComponent.cs").read_text(encoding="utf-8")
ensure("GUI.Window(" not in source and "GUI.DragWindow(" not in source, "Legacy Unity IMGUI window still used")
project = (ROOT / "DeepRim.Vertical.1.7.csproj").read_text(encoding="utf-8")
ensure("<TargetFramework>net10.0</TargetFramework>" in project, "Incorrect framework for 1.7")
print("PASS: RimWorld 1.7 version routing, assets, metadata, DLL, navigator API and SDK target")
