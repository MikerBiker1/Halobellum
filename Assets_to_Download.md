# Assets to download

Everything in the game that is still a stand-in, for you to replace. Tick things off as you get them. Nothing here is required for the game to run.

- **Sound and music:** put the file in `Assets/_Project/Art/Imported/Audio/Resources/AudioOverride/`, named after its slot, such as `music_unsc_battle.ogg`. No code changes are needed, and the game uses it from its next start. Use `.ogg` or `.wav` for music, since `.mp3` usually doesn't loop cleanly.
- **Several files for one slot:** name them `slot_1`, `slot_2` and so on, up to 12. One is picked each time.
- **Models:** tell me where you put them and I'll fit them to their units.
- Please leave a `SOURCE.txt` note saying where each came from, in `Art/Imported/Audio` for sound (not in `AudioOverride`).

## Music (10 tracks)

Music loops, so a track should end where it starts.

| Done | Slot | When it plays |
| --- | --- | --- |
| [ ] | `music_mainmenu` | Main menu and the draft screen. The first thing heard. |
| [ ] | `music_unsc_deployment` | UNSC: placing the army. |
| [ ] | `music_unsc_battle` | UNSC: the battle. |
| [ ] | `music_unsc_menu` | UNSC: cards, round result, match over. |
| [ ] | `music_covenant_deployment` | Covenant: placing the army. |
| [ ] | `music_covenant_battle` | Covenant: the battle. |
| [ ] | `music_covenant_menu` | Covenant: cards, round result, match over. |
| [ ] | `music_flood_deployment` | Flood: placing the army. |
| [ ] | `music_flood_battle` | Flood: the battle. |
| [ ] | `music_flood_menu` | Flood: cards, round result, match over. |

## Short end-of-match pieces (6)

| Done | Slot | When it plays |
| --- | --- | --- |
| [ ] | `stinger_unsc_victory` | UNSC: match won. |
| [ ] | `stinger_unsc_defeat` | UNSC: match lost. |
| [ ] | `stinger_covenant_victory` | Covenant: match won. |
| [ ] | `stinger_covenant_defeat` | Covenant: match lost. |
| [ ] | `stinger_flood_victory` | Flood: match won. |
| [ ] | `stinger_flood_defeat` | Flood: match lost. |

## Unit sound effects

Sounds go by kind of weapon, not by unit, so one file covers every unit in its row.

### Weapons firing (17)

| Done | Slot | Used by |
| --- | --- | --- |
| [ ] | `sfx_fire_bullet` | Rifles, SMGs, pistols: Marines and others. It plays very often, so keep it short and not too bright. |
| [ ] | `sfx_fire_heavybullet` | Machine guns, chainguns, autocannons, turrets, shotguns: Warthog, Mantis, Spartan CQB. |
| [ ] | `sfx_fire_bullet_flood` | A rifle in Flood hands: Infested Marine. |
| [ ] | `sfx_fire_gauss` | Gauss guns and the sniper rifle: Gauss Warthog, Spartan Sniper. |
| [ ] | `sfx_fire_cannon` | Tank and other cannons: Scorpion, Elephant, Rhino, Grizzly. |
| [ ] | `sfx_fire_rocket` | Rockets and missiles, the launch: Hornet, Wolverine, Archer Pod. |
| [ ] | `sfx_fire_bomb` | Bombs, the release: Banshee, Hornet, Longsword. |
| [ ] | `sfx_fire_flame` | Flamethrowers. |
| [ ] | `sfx_fire_laser` | Spartan Laser, Hunter Captain's Siphon Laser. |
| [ ] | `sfx_fire_plasma` | Plasma rifles, pistols and turrets: Grunts, Jackals, Elites. |
| [ ] | `sfx_fire_plasmaheavy` | Fuel rod guns, plasma mortars, and plasma shots that fly and burst: Fuel Rod Grunt, Wraith, Hunter, Marauder. |
| [ ] | `sfx_fire_needle` | Needle weapons: Vampire, Heavy Grunts. |
| [ ] | `sfx_fire_beam` | The Scarab's beam: 2 s of charging, then 3 s of firing, 5.5 s in all. |
| [ ] | `sfx_fire_bio` | Flood spat bone, bone shards and spores. |
| [ ] | `sfx_fire_melee` | Melee with no faction of its own. |
| [ ] | `sfx_fire_melee_covenant` | Covenant melee: energy sword, talons. |
| [ ] | `sfx_fire_melee_flood` | Flood melee: claws, tendrils, bites. |

### Hits, deaths and engines (16)

| Done | Slot | When it plays |
| --- | --- | --- |
| [ ] | `sfx_impact_armor` | A hit on armor. |
| [ ] | `sfx_impact_flesh` | A hit on something unarmored. |
| [ ] | `sfx_impact_shield` | A hit on an energy shield. |
| [ ] | `sfx_impact_block` | A shot stopped by a held shield. |
| [ ] | `sfx_impact_wet` | A hit on the Flood. |
| [ ] | `sfx_explosion_small` | Rockets, shells and fuel rods landing. |
| [ ] | `sfx_explosion_medium` | Medium vehicles and walkers going up. |
| [ ] | `sfx_explosion_large` | The biggest units going up, and towers. |
| [ ] | `sfx_death_infantry` | A UNSC soldier falling. |
| [ ] | `sfx_death_infantry_covenant` | A Covenant soldier falling. |
| [ ] | `sfx_death_flood` | A Flood form dying. |
| [ ] | `sfx_death_vehicle` | A light vehicle going up. |
| [ ] | `sfx_death_air` | An aircraft hit, falling and crashing. |
| [ ] | `sfx_hum_engine` | A loop: the engines of all UNSC ground vehicles. It must close without a click. |
| [ ] | `sfx_hum_hover` | A loop: what flies or floats (aircraft, Covenant vehicles). It must close without a click. |
| [ ] | `sfx_drop_in` | A squad falling into place: it lands 0.6 s in. |

### Flood specials (5)

| Done | Slot | When it plays |
| --- | --- | --- |
| [ ] | `sfx_flood_burst` | A Carrier Form going off, a Flood unit bursting, Bone Shards landing. |
| [ ] | `sfx_infection_start` | A dying unit being infected. |
| [ ] | `sfx_infection_rise` | An infected corpse rising. |
| [ ] | `sfx_pelican_dive` | The Infected Pelican starting its dive. |
| [ ] | `sfx_pelican_crash` | The Infected Pelican hitting the ground, and the biggest Flood units dying. |

Spells and interface sounds have slots too, listed in `Assets/_Project/Art/Imported/Audio/README.txt`. I left them out here because you asked for music and unit sounds.

## Models left to find

These units are placeholder shapes, or borrow another unit's body. Weapons on a model are welcome, because shots leave from its muzzles.

### Your Spartan download

| Done | What | For |
| --- | --- | --- |
| [ ] | Base Spartan | Spartan, and the five Noble units, which use the Marine's body for now. |
| [ ] | Spartan Laser | Spartan. |
| [ ] | SRS99 sniper rifle | Spartan Sniper. |
| [ ] | M247H chaingun and Hydra rocket pod | Spartan Heavy Weapons. |
| [ ] | M319 grenade launcher | Spartan Grenadier. |
| [ ] | Shotgun | Spartan CQB. |
| [ ] | BR85 battle rifle and M41 rocket launcher | Spartan Fireteam. |

### UNSC

| Done | Unit | Notes |
| --- | --- | --- |
| [ ] | Flamethrower Trooper | A soldier with a flamethrower. |
| [ ] | Wolverine | Anti-air vehicle. |
| [ ] | Archer Missile Pod | |
| [ ] | Mantis | Walker. |
| [ ] | ODST | Only lands from the drop pod spell. |
| [ ] | Gremlin | Armored Division. |
| [ ] | Cobra | Armored Division. |
| [ ] | Rhino | Armored Division. |
| [ ] | Sparrowhawk | Aerospace Command. |
| [ ] | Vulture | Aerospace Command. |
| [ ] | Longsword | Aerospace Command. |
| [ ] | Condor Gunship | Aerospace Command. It is a grey Pelican for now. |

### Covenant

| Done | Unit | Notes |
| --- | --- | --- |
| [ ] | Elite | Infested Elite can reuse it, tinted. |
| [ ] | Zealot | An Elite with a sword could do. |
| [ ] | Ghost | Infested Ghost can reuse it, tinted. |
| [ ] | Jackal | Two units, with a rifle and with a shield. |
| [ ] | Hunter | Hunter Pair, and Hunter Captain can reuse it. |
| [ ] | Vampire | Aircraft. |
| [ ] | Marauder | Banished. |
| [ ] | Reaver | Banished. |
| [ ] | Brute | Brute Grenadier, Banished. |
| [ ] | Goliath | Lekgolo Colony. |
| [ ] | Skitterer | Lekgolo Colony. |
| [ ] | Grunt Goblin | Unggoy Rebellion: a Grunt in a mech. |

### Flood

| Done | Unit | Notes |
| --- | --- | --- |
| [ ] | Flood Abomination | Their only Titan, very tall and large. |
| [ ] | Combat Form | |
| [ ] | Seeder | |
| [ ] | Swarm Form | Flies. |
| [ ] | Juggernaut | You may remove it if you can't find one. |
| [ ] | Spawner | You may remove it if you can't find one. |

### Towers (new)

| Done | What | Notes |
| --- | --- | --- |
| [ ] | Command Center | Stands on each side's left. For now it is a block with a mast and a dish, built from plain shapes. |
| [ ] | Research Center | Stands on each side's right. For now it is a lab with a glowing dome. |

Only about 6 m of ground is free for each tower, though it can be as tall as you like.
