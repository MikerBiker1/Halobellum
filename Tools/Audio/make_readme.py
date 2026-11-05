# make_readme.py
# Writes the owner's list of audio slots, Assets/_Project/Art/Imported/Audio/README.txt, from the generated files, so that it can't drift
# from them. A test checks that it names every slot.
# Usage: python make_readme.py. Depends on: the files in GeneratedAudio.

import glob
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
FOLDER = os.path.join(HERE, "..", "..", "Assets", "_Project", "Audio", "Generated", "Resources", "GeneratedAudio")
TARGET = os.path.join(HERE, "..", "..", "Assets", "_Project", "Art", "Imported", "Audio", "README.txt")

WHAT = {
    "music_mainmenu": "The main menu's music, also the draft screen (no side chosen yet). The first thing heard.",
    "music_deployment": "Each round's cards and deployment. Several songs: music_deployment_1 and so on. One is picked at random.",
    "music_combat": "Each battle. Several songs: music_combat_1 and so on. One is picked at random.",
    "music_combat_flood_present": "A battle with Flood on either side: joins the combat songs, and plays more often than any of them.",
    "stinger_match_victory": "A round won, when its window opens.",
    "stinger_match_defeat": "A round lost, when its window opens. Several sounds: stinger_match_defeat_1 and so on. One is picked at random.",
    "stinger_match_defeat_flood_present": "A round lost against Flood: joins the other sounds.",
    "stinger_game_victory": "The whole game won.",
    "stinger_game_defeat_unsc": "The whole game lost, playing UNSC.",
    "stinger_game_defeat_covenant": "The whole game lost, playing Covenant.",
    "stinger_game_defeat_flood": "The whole game lost, playing Flood.",
    "stinger_game_defeat_flood_present": "The whole game lost against Flood, whoever you played.",
    "sfx_fire_bullet": "Rifles, SMGs, pistols. Plays very often: keep it short and not too bright.",
    "sfx_fire_bullet_flood": "A rifle in Flood hands (Scavenged Rifle).",
    "sfx_fire_heavybullet": "Machine guns, chainguns, autocannons, turrets, shotguns.",
    "sfx_fire_cannon": "Tank and other cannons.",
    "sfx_fire_gauss": "Gauss guns and the sniper rifle: one hard shot.",
    "sfx_fire_rocket": "Rocket launchers and missiles, the launch.",
    "sfx_fire_bomb": "Bombs, the release.",
    "sfx_fire_flame": "Flamethrowers.",
    "sfx_fire_plasma": "Plasma rifles, pistols, turrets.",
    "sfx_fire_plasmaheavy": "Fuel rod guns, plasma mortars, and plasma shots that fly and burst (Marauder).",
    "sfx_fire_needle": "Needle weapons.",
    "sfx_fire_laser": "The Spartan Laser and the Hunter Captain's Siphon Laser.",
    "sfx_fire_beam": "The Scarab's beam: 2 s of charging, then 3 s of firing, 5.5 s in all.",
    "sfx_fire_melee": "Melee with no faction of its own.",
    "sfx_fire_melee_covenant": "Covenant melee (energy sword, talons).",
    "sfx_fire_melee_flood": "Flood melee (claws, tendrils, bites).",
    "sfx_fire_bio": "Flood spat bone, bone shards and spores.",
    "sfx_impact_flesh": "A hit on something unarmored.",
    "sfx_impact_armor": "A hit on armor.",
    "sfx_impact_shield": "A hit on an energy shield.",
    "sfx_impact_block": "A shot stopped by a held shield.",
    "sfx_impact_wet": "A hit on the Flood.",
    "sfx_explosion_small": "Rockets, shells and fuel rods landing.",
    "sfx_explosion_medium": "Medium vehicles and walkers going up.",
    "sfx_explosion_large": "The biggest units going up.",
    "sfx_death_infantry": "A UNSC soldier falling.",
    "sfx_death_infantry_covenant": "A Covenant soldier falling.",
    "sfx_death_flood": "A Flood form dying.",
    "sfx_death_vehicle": "A light vehicle going up.",
    "sfx_death_air": "An aircraft hit, falling and crashing.",
    "sfx_flood_burst": "A carrier form going off, a Flood unit bursting, or Bone Shards landing.",
    "sfx_infection_start": "A dying unit being infected.",
    "sfx_infection_rise": "An infected corpse rising.",
    "sfx_pelican_dive": "The Infected Pelican starting its dive.",
    "sfx_pelican_crash": "The Infected Pelican hitting the ground; also the biggest Flood units dying.",
    "sfx_drop_in": "A squad falling into place: it lands 0.6 s in.",
    "sfx_spell_mac": "MAC Strike.",
    "sfx_spell_nuke": "HAVOK Nuke.",
    "sfx_spell_glassing": "Glassing Beam (one sound for its whole 3 s).",
    "sfx_spell_missiles": "Archer Missile Barrage (one sound at its start).",
    "sfx_spell_plasma": "Plasma Bombardment.",
    "sfx_spell_storm_tick": "Each hit of the Plasma Storm.",
    "sfx_spell_spore": "Each hit of Blight Rain, and Spore Burst.",
    "sfx_spell_wrath": "Gravemind's Wrath.",
    "sfx_spell_buff_unsc": "Battlefield Rally.",
    "sfx_spell_buff_covenant": "Prophet's Blessing.",
    "sfx_spell_buff_flood": "Hive Frenzy and Biomass Shell.",
    "sfx_spell_shield": "Bubble Shield and Energy Shield Dome.",
    "sfx_spell_drop_pods": "ODST Drop Pods.",
    "sfx_spell_drop_air": "Banshee Ambush.",
    "sfx_spell_drop_flood": "Dispersal Pods.",
    "sfx_spell_emp": "Electromagnetic Pulse and Nerve Spores.",
    "sfx_spell_smoke": "Smoke Bomb.",
    "sfx_hum_engine": "A loop: the engines of all UNSC ground vehicles, as loud as how many are alive.",
    "sfx_hum_hover": "A loop: what flies or floats (aircraft, Covenant vehicles). Loops must close without a click.",
}
UI = {
    "click": "any button; quieter, a squad selected or turned", "hover": "the mouse going over a button", "buy": "a squad bought", "sell": "a squad sold; quieter, a spell taken back", "unlock": "a unit unlocked or a tech bought",
    "error": "something that can't be done, or a click on a greyed-out button", "cardpick": "a card taken", "skip": "the cards skipped; quieter, a squad moved", "spellplaced": "a spell or an item placed",
    "levelup": "a squad leveled up", "battlestart": "the battle starts", "roundwon": "round won", "roundlost": "round lost", "matchwon": "match won (a short hit: the stinger follows)", "matchlost": "match lost",
}


def slots():
    names = sorted(os.path.splitext(os.path.basename(p))[0] for p in glob.glob(os.path.join(FOLDER, "*.wav")))
    return sorted({re.sub(r"_\d+$", "", n) for n in names} | {n for n in WHAT if n.startswith(("music_", "stinger_"))})  # The owner's music has no generated file.


def main():
    all_slots = slots()
    lines = [
        "HALOBELLUM: AUDIO SLOTS",
        "",
        "To use your own music or sound, put the file in this folder:",
        "",
        "    Assets/_Project/Art/Imported/Audio/Resources/AudioOverride/",
        "",
        "named after the slot below, for example music_combat.ogg. That is all: the game uses your file in place of the",
        "generated one the next time it starts, and takes it back if you remove it. Nothing else needs setting: music is",
        "streamed and compressed, effects kept ready in memory (Scripts/Editor/AudioImportRules.cs).",
        "",
        "- File types: .ogg, .wav, .mp3, .aif. Use .ogg or .wav for music: an .mp3 usually does not loop without a gap.",
        "- Several files for one slot: name them slot_1, slot_2, slot_3 (up to 12), for example music_combat_1.ogg,",
        "  music_combat_2.ogg. One is picked each time. They also replace all of a generated slot's variants.",
        "- Music loops, so a track should end where it starts. It resumes where it was at the next round.",
        "- Each thing you drop in should have a SOURCE.txt note saying where it came from (Bible Section 12). A plain text file",
        "  here in Art/Imported/Audio is fine; don't put it in AudioOverride.",
        "- The music and the end-of-round and end-of-game pieces are the owner's own (see SOURCE.txt).",
        "- All the generated sounds are original placeholders made by Tools/Audio. They are meant to be replaced as you like.",
        "",
        "Volumes in the game: PlayerPrefs Volume.Master, Volume.Music, Volume.Sfx, Volume.Ui.",
        "",
        "MUSIC",
    ]

    def section(prefix):
        for name in all_slots:
            if name.startswith(prefix):
                lines.append(f"  {name:32s} {WHAT.get(name, '')}")

    section("music_")
    lines += ["", "STINGERS"]
    section("stinger_")
    lines += ["", "BATTLE SOUNDS"]
    section("sfx_")
    lines += ["", "UI SOUNDS (each faction has its own set)"]
    for key, what in UI.items():
        names = [f"ui_{f}_{key}" for f in ("unsc", "covenant", "flood") if f"ui_{f}_{key}" in all_slots]
        lines.append(f"  {what}:")
        lines.append("    " + "  ".join(names))
    lines += ["", "UI SOUNDS BEFORE A SIDE IS CHOSEN (main menu, draft)"]
    section("ui_neutral_")
    missing = [n for n in all_slots if n.startswith("sfx_") and n not in WHAT]
    if missing:
        raise SystemExit("No description for: " + ", ".join(missing))
    os.makedirs(os.path.dirname(TARGET), exist_ok=True)
    with open(TARGET, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    print(len(all_slots), "slots listed")


if __name__ == "__main__":
    main()
