# bring_in_sfx.py
# Copies the owner's sound effects from Art_Source/SFX into the game's AudioOverride folder under the names of their slots, and writes
# down where each file came from (Art/Imported/Audio/SOURCE_SFX.txt). Run again when the owner changes or adds a file. Takes the source
# folder as an argument, as worktrees don't have their own Art_Source:
#   python Tools/Audio/bring_in_sfx.py E:/AI/Halobellum/Art_Source/SFX
# A UI sound goes in once, as ui_<sound>: every faction's set uses it (AudioLibrary). The game evens out how loud each file is at run
# time (AudioLibrary.Loudness), so files go in as they are.

import os
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
AUDIO = os.path.join(HERE, "..", "..", "Assets", "_Project", "Art", "Imported", "Audio")
OVERRIDE = os.path.join(AUDIO, "Resources", "AudioOverride")

# The owner's file -> the slot it fills, and what for.
SLOTS = [
    ("assault_rifle_fire_brown2_1.wav", "sfx_fire_assaultrifle", "Marines' MA5 and the Scavenged Rifle (Infested Marine, Combat Form)"),
    ("fire[smg_new01].wav", "sfx_fire_smg", "ODSTs' M7 SMG, looped while they fire"),
    ("fire[m60_ster1].wav", "sfx_fire_m60", "every machine gun and chaingun: the Warthog's (and the Infested Warthog's), the coaxial guns, the Mantis, the Vulture, the Elephant's turrets, the Spartan's chaingun, looped while they fire"),
    ("ghost_fire[plasma_ghost].wav", "sfx_fire_ghost", "the Ghost's plasma bolts (and the Infested Ghost's), looped while it fires"),
    ("flamethrower_fire_FT_active.wav", "sfx_fire_flame", "flamethrowers, looped while they fire"),
    ("plas_rifle_fire[plas_rif_fire].wav", "sfx_fire_plasma", "plasma rifles, pistols and turrets"),
    ("banshee_fire[plasmarifle1].wav", "sfx_fire_banshee", "the Banshee's plasma cannons (and the Infested Banshee's)"),
    ("banshee_cannon_fire[banshee_cannon__fire].wav", "sfx_fire_banshee_bomb", "the Banshee's fuel rod bomb, fired"),
    ("banshee_cannon_expl[banshee_cannon_explo2].wav", "sfx_explosion_banshee", "the Banshee's fuel rod bomb, landing"),
    ("fire[needlefire11].wav", "sfx_fire_needle", "needle weapons"),
    ("fire[rocket16].wav", "sfx_fire_rocket", "rocket launchers and missiles"),
    ("scorpion_cannon_fire[cannon_fire].wav", "sfx_fire_cannon", "tank cannons"),
    ("fire[grifle03].wav", "sfx_fire_gauss", "gauss guns and railguns (Gauss Warthog, Cobra, Gremlin)"),
    ("Sniper fire[ruger24].wav", "sfx_fire_sniper", "the Spartan Sniper's rifle"),
    ("fire[shotgun2].wav", "sfx_fire_shotgun", "the Spartan CQB's shotgun"),
    ("brute_shot_fire[brute_fire1].wav", "sfx_fire_bruteshot", "the Brute Grenadier's spike grenade launcher"),
    ("Fuelrod_fire_grunt.wav", "sfx_fire_fuelrod", "fuel rod guns (Fuel Rod Grunt, Grunt Goblin, Goliath)"),
    ("Hunter_fuel_rod_gun_fire_fuelrod_fire.wav", "sfx_fire_hunter", "the Hunters' assault cannon"),
    ("fuel_rod_gun_explosion_fuelrod_explo1.wav", "sfx_explosion_fuelrod_1", "a fuel rod landing"),
    ("fuel_rod_gun_explosion_fuelrod_explo2.wav", "sfx_explosion_fuelrod_2", "a fuel rod landing"),
    ("wraith_fire_mortar[wraith_fire].wav", "sfx_fire_mortar", "the Wraith's mortar (and the Infested Wraith's), fired"),
    ("Wraith airborn projectile loop.wav", "sfx_flight_mortar", "a Wraith's mortar shell in the air, looped while it flies"),
    ("sword_impact_character[sword_hit_char2].wav", "sfx_fire_sword", "the Zealot's energy sword"),
    ("sword_ready[energy_ready].wav", "sfx_sword_ready", "every Zealot as a battle starts"),
    ("Beam Charge scarab.wav", "sfx_beam_charge", "the Scarab's beam charging"),
    ("in[scarab_fire_in].wav", "sfx_beam_in", "the Scarab's beam starting to fire"),
    ("loop[scarab_fire_loop].wav", "sfx_beam_loop", "the Scarab's beam firing, looped"),
    ("out[scarab_fire_out].wav", "sfx_beam_out", "the Scarab's beam stopping"),
    ("Scarab walking.mp3", "sfx_walk_scarab", "a Scarab walking, looped"),
    ("Scarab dying.mp3", "sfx_death_scarab", "a Scarab dying"),
    ("frag_grenade_expl_frag2.wav", "sfx_explosion_small", "rockets, shells and grenades landing"),
    ("med_explosion_15.wav", "sfx_explosion_medium", "medium vehicles and walkers going up"),
    ("large_explosion_1.wav", "sfx_explosion_large_1", "the biggest units going up"),
    ("large_explosion_12.wav", "sfx_explosion_large_2", "the biggest units going up"),
    ("UNSC air unit hitting the ground.wav", "sfx_death_air_unsc", "a UNSC aircraft crashing, and the Infected Pelican's crash dive"),
    ("Infested pelican dive.wav", "sfx_pelican_dive", "the Infected Pelican starting its dive"),
    ("combatform_body_destroyed[combat_body_destroyed3].wav", "sfx_death_flood", "a Flood form dying"),
    ("infectionform_die2.wav", "sfx_death_infectionform", "an Infection Form popping"),
    ("flood carrier explosion.mp3", "sfx_flood_burst", "a Carrier Form going off, and other Flood bursts"),
    ("flood_howls[floodhowl_1].wav", "sfx_infection_rise_1", "a unit rising as Flood"),
    ("flood_howls[floodhowl_2].wav", "sfx_infection_rise_2", "a unit rising as Flood"),
    ("flood_howls[floodhowl_3].wav", "sfx_infection_rise_3", "a unit rising as Flood"),
    ("flood_howls[floodhowl_4].wav", "sfx_infection_rise_4", "a unit rising as Flood"),
    ("Seeder1.wav", "sfx_call_seeder_1", "a Seeder, every 3 to 6 s"),
    ("Seeder2.wav", "sfx_call_seeder_2", "a Seeder, every 3 to 6 s"),
    ("Seeder3.wav", "sfx_call_seeder_3", "a Seeder, every 3 to 6 s"),
    ("Swarm form1.wav", "sfx_call_swarm_1", "a Swarm Form, every 3 to 6 s"),
    ("Swarm form2.wav", "sfx_call_swarm_2", "a Swarm Form, every 3 to 6 s"),
    ("Swarm form3.wav.wav", "sfx_call_swarm_3", "a Swarm Form, every 3 to 6 s"),
    ("shield_regen.wav", "sfx_shield_regen", "an energy shield starting to recharge"),
    ("UI buttons.wav", "ui_click", "any button, every faction"),
    ("Failure to place unit.wav", "ui_error", "something that can't be done, every faction"),
    ("Card chosen.wav", "ui_cardpick", "a card taken, every faction"),
    ("Unit bought (fall to grid).wav", "ui_buy", "a squad bought, every faction"),
    ("Unit unlocked.wav", "ui_unlock", "a unit unlocked or a tech bought, every faction"),
    # The owner's second batch (D71).
    ("Autocannon.mp3", "sfx_fire_autocannon", "autocannons (Pelican, Sparrowhawk, Condor, Chopper), looped while they fire"),
    ("fire_BR85.wav", "sfx_fire_br85", "the BR85 battle rifle (Spartan Fireteam)"),
    ("phantom_turret_fire[phantom_fire].wav", "sfx_fire_plasmaheavy", "heavy plasma: the Marauder's plasma cannon, and any heavy plasma shot with no file of its own"),
    ("phantom_turret_fire[phantom_fire].wav", "sfx_fire_plasmaturret", "the Phantoms' and the Scarab's plasma turrets (the same file, in a voice group of its own)"),
    ("beam_rifle_fire[cov_snip_shot9].wav", "sfx_fire_beamrifle", "the Beam Rifle (Jackal)"),
    ("Glassing beam.mp3", "sfx_spell_glassing", "Glassing Beam, once for the whole beam, started so that the beam strikes with it"),
    ("Small&medium light nodes.wav", "sfx_node_small", "a small or medium node of light hitting a base at the end of a round"),
    ("big light node.wav", "sfx_node_big", "a big node of light hitting a base at the end of a round"),
    ("UNSC air unit hitting the ground.wav", "sfx_pelican_crash", "the same crash, as asked: the biggest Flood units dying"),
]

# Files not used yet, and why.
UNUSED = [
    ("plasma_grenade_plasmagrenexpl_plasmagx1.wav", "no use for it yet, as the owner said"),
    ("magnum_fire[handgun2].wav", "no unit has a magnum"),
    ("fire[plasmarifle1].wav", "the same file as banshee_fire[plasmarifle1].wav, which the Banshee uses"),
]


def main():
    if len(sys.argv) < 2:
        print(__doc__ if __doc__ else "usage: bring_in_sfx.py <Art_Source/SFX folder>")
        sys.exit(1)
    source = sys.argv[1]
    os.makedirs(OVERRIDE, exist_ok=True)
    lines = [
        "The owner's sound effects, from their Art_Source/SFX, brought in by Tools/Audio/bring_in_sfx.py.",
        "File, the slot it fills, and what for. The game evens out how loud each one is (AudioLibrary).",
        "",
    ]
    missing = []
    for name, slot, what in SLOTS:
        path = os.path.join(source, name)
        if not os.path.exists(path):
            missing.append(name)
            continue
        ext = os.path.splitext(name)[1].lower()
        target = os.path.join(OVERRIDE, slot + ext)
        for other in (".wav", ".mp3", ".ogg", ".aif"):
            stale = os.path.join(OVERRIDE, slot + other)
            if other != ext and os.path.exists(stale):
                os.remove(stale)
                if os.path.exists(stale + ".meta"):
                    os.remove(stale + ".meta")
        shutil.copyfile(path, target)
        lines.append(f"{name} -> {slot}{ext}: {what}")
    lines.append("")
    lines.append("Not used:")
    for name, why in UNUSED:
        lines.append(f"{name}: {why}")
    known = {n for n, _, _ in SLOTS} | {n for n, _ in UNUSED}
    extra = sorted(f for f in os.listdir(source) if f not in known)
    for name in extra:
        lines.append(f"{name}: new, not placed yet")
    with open(os.path.join(AUDIO, "SOURCE_SFX.txt"), "w", encoding="utf-8", newline="\n") as out:
        out.write("\n".join(lines) + "\n")
    print(f"{len(SLOTS) - len(missing)} files in, {len(missing)} missing, {len(extra)} not placed")
    for name in missing:
        print("missing:", name)
    for name in extra:
        print("not placed:", name)


if __name__ == "__main__":
    main()
