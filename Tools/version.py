# version.py
# The game's version, MAJOR.MINOR.PATCH, shown in small print on the main menu (D100, D102). It lives in Unity's Player Settings
# (ProjectSettings/ProjectSettings.asset, bundleVersion). The owner's rule (2026-11-07):
#   PATCH  +1 for a very small change: a fix, a tweak, a number, a text, a sound swapped;
#   MINOR  +1 for a substantial change (PATCH back to 0): a new feature, unit, animation, model, screen or system;
#   MAJOR  only the owner changes it.
# Bump once per change pushed to main, in the same commit, by the biggest part of it.
#   python Tools/version.py            prints the version
#   python Tools/version.py patch      0.378.0 -> 0.378.1
#   python Tools/version.py minor      0.378.4 -> 0.379.0
# Each version gets its entry in the wiki's changelog (Tools/Wiki/content/42_changelog.md, newest first) in the same commit; a bump
# reminds you while the entry is missing. Then rebuild the wiki (python Tools/Wiki/build_wiki.py).
# Needs only the Python standard library.

import os
import re
import sys

PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "ProjectSettings", "ProjectSettings.asset")
CHANGELOG = os.path.join(os.path.dirname(os.path.abspath(__file__)), "Wiki", "content", "42_changelog.md")
LINE = re.compile(r"^(  bundleVersion: )(\d+)\.(\d+)\.(\d+)(\r?)$", re.M)


def main():
    text = open(PATH, encoding="utf-8", newline="").read()
    match = LINE.search(text)
    if not match:
        raise SystemExit("No bundleVersion of the form MAJOR.MINOR.PATCH in " + PATH)
    major, minor, patch = int(match.group(2)), int(match.group(3)), int(match.group(4))
    what = sys.argv[1] if len(sys.argv) > 1 else None
    if what == "patch":
        patch += 1
    elif what == "minor":
        minor, patch = minor + 1, 0
    elif what is not None:
        raise SystemExit("Use patch or minor. MAJOR is the owner's alone.")
    version = f"{major}.{minor}.{patch}"
    if what is not None:
        text = text[:match.start()] + f"{match.group(1)}{version}{match.group(5)}" + text[match.end():]
        open(PATH, "w", encoding="utf-8", newline="").write(text)
    print(version)
    if what is not None and os.path.exists(CHANGELOG) and f"## {version} " not in open(CHANGELOG, encoding="utf-8").read():
        print(f"Add a '## {version} · <date>' entry at the top of Tools/Wiki/content/42_changelog.md, then rebuild the wiki.", file=sys.stderr)


if __name__ == "__main__":
    main()
