# bring_in_music.py
# Copies the owner's music from Art_Source/Music into the game's AudioOverride folder under the names of its slots, and writes down
# where each file came from. Run again when the owner changes a file. Takes the source folder as an argument, as worktrees don't have their own Art_Source.
#   python Tools/Audio/bring_in_music.py E:/AI/Halobellum/Art_Source/Music

import os
import shutil
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
AUDIO = os.path.join(HERE, "..", "..", "Assets", "_Project", "Art", "Imported", "Audio")
OVERRIDE = os.path.join(AUDIO, "Resources", "AudioOverride")

SLOTS = {"mainmenu": "music_mainmenu"}
SLOTS.update({f"Deployment{i}": f"music_deployment_{i}" for i in range(1, 9)})
SLOTS.update({f"Combat{i}": f"music_combat_{i}" for i in range(1, 5)})
SLOTS.update(
    {
        "Combat_flood_present": "music_combat_flood_present",
        "Victory_match": "stinger_match_victory",
        "draw": "stinger_match_draw",
        "Defeat_match": "stinger_match_defeat_1",
        "Defeat_match2": "stinger_match_defeat_2",
        "Defeat_match3": "stinger_match_defeat_3",
        "Defeat_match4": "stinger_match_defeat_4",
        "Defeat_match5": "stinger_match_defeat_5",
        "defeat_match_flood_present": "stinger_match_defeat_flood_present",
        "Victory_game": "stinger_game_victory",
        "Defeat_game_UNSC": "stinger_game_defeat_unsc",
        "Defeat_game_Covenant": "stinger_game_defeat_covenant",
        "Defeat__game_flood": "stinger_game_defeat_flood",
        "Defeat_game_flood_present": "stinger_game_defeat_flood_present",
    }
)

BITRATES = [0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320]
RATES = [44100, 48000, 32000]


def seconds(path):
    """The length of an MPEG-1 Layer III file, by walking its frames."""
    data = open(path, "rb").read()
    at = 0
    if data[:3] == b"ID3":
        size = data[6:10]
        at = 10 + ((size[0] << 21) | (size[1] << 14) | (size[2] << 7) | size[3])
    total = 0.0
    while at + 4 <= len(data):
        head = struct.unpack(">I", data[at : at + 4])[0]
        if head >> 21 != 0x7FF or (head >> 19) & 3 != 3 or (head >> 17) & 3 != 1:
            at += 1
            continue
        bitrate = (head >> 12) & 15
        rate = (head >> 10) & 3
        if bitrate == 0 or bitrate == 15 or rate == 3:
            at += 1
            continue
        total += 1152 / RATES[rate]
        at += 144000 * BITRATES[bitrate] // RATES[rate] + ((head >> 9) & 1)
    return total


def main():
    source = sys.argv[1]
    os.makedirs(OVERRIDE, exist_ok=True)
    found = {name[:-4]: name for name in os.listdir(source) if name.lower().endswith(".mp3")}
    missing = sorted(set(SLOTS) - set(found))
    extra = sorted(set(found) - set(SLOTS))
    if missing or extra:
        sys.exit(f"Files missing {missing}, files with no slot {extra}")
    lines = ["The owner's music, from their Art_Source/Music, brought in by Tools/Audio/bring_in_music.py.", "File, the slot it fills, and its length:", ""]
    for name, slot in SLOTS.items():
        path = os.path.join(source, found[name])
        shutil.copyfile(path, os.path.join(OVERRIDE, slot + ".mp3"))
        length = seconds(path)
        lines.append(f"{found[name]} -> {slot}.mp3 ({int(length) // 60}:{int(length) % 60:02})")
        print(lines[-1])
    with open(os.path.join(AUDIO, "SOURCE.txt"), "w", encoding="utf-8", newline="\n") as out:
        out.write("\n".join(lines) + "\n")


main()
