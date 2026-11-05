# Assets to download

Everything in the game that is still a stand-in, for you to replace. Tick things off as you get them. Nothing here is required for the game to run.

- **Sound and music:** put the file in `Assets/_Project/Art/Imported/Audio/Resources/AudioOverride/`, named after its slot, such as `music_combat_5.ogg`. No code changes are needed, and the game uses it from its next start. Use `.ogg` or `.wav` for music, since `.mp3` usually doesn't loop cleanly.
- **Several files for one slot:** name them `slot_1`, `slot_2` and so on, up to 12. One is picked each time.
- **Models:** tell me where you put them and I'll fit them to their units.
- **Ground textures:** see their section at the end.
- Please leave a `SOURCE.txt` note saying where each came from, in `Art/Imported/Audio` for sound (not in `AudioOverride`).

## Music and end-of-round pieces

All in, from your `Art_Source/Music` (D67). To change one, replace the file there and tell me, or put a new one in `AudioOverride` under its slot's name. To add a song to a pool, name it the next number up, such as `music_combat_5`.

| Done | Slot | When it plays |
| --- | --- | --- |
| [x] | `music_mainmenu` | Main menu and the draft screen. |
| [x] | `music_deployment_1` to `_8` | Each round's cards and deployment: one song, picked at random. |
| [x] | `music_combat_1` to `_4` | Each battle: one song, picked at random. |
| [x] | `music_combat_flood_present` | Joins the battle songs when Flood is on either side, and plays more often than any of them. |
| [x] | `stinger_match_victory` | A round won, when its window opens. |
| [x] | `stinger_match_defeat_1` to `_5` | A round lost: one at random. |
| [x] | `stinger_match_defeat_flood_present` | Joins those when the enemy is Flood. |
| [x] | `stinger_game_victory` | The whole game won. |
| [x] | `stinger_game_defeat_unsc`, `_covenant`, `_flood` | The whole game lost, by the faction you played. |
| [x] | `stinger_game_defeat_flood_present` | The whole game lost against Flood, whoever you played. |

### Still wanted

| Done | What | For | Stand-in for now |
| --- | --- | --- | --- |
| [ ] | A round drawn | When neither side wins a round. | Silence |
| [ ] | Node of light hitting a base, small and big | The end of a round. Send me the files and I'll fit them. | The battle's own hit and explosion sounds, by size |
| [ ] | Node of light taking off | The end of a round, optional. | Silence |

## Unit sound effects

Your 59 files are in (D70): see `Assets/_Project/Art/Imported/Audio/SOURCE_SFX.txt` for which slot each fills. The list below is the older plan by kind; a row whose slot you filled can be ticked.

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
| [ ] | `sfx_fire_bomb` | Bombs, the release: the Banshee's fuel rod bomb (every 2.5 s, at ground targets), Hornet, Longsword. |
| [ ] | `sfx_fire_flame` | Flamethrowers. |
| [ ] | `sfx_fire_laser` | Spartan Laser, Hunter Captain's Siphon Laser. |
| [ ] | `sfx_fire_plasma` | Plasma rifles, pistols and turrets: Grunts, Jackals, Elites, and the Banshee's plasma cannons (fast, at air and ground). |
| [ ] | `sfx_fire_plasmaheavy` | Fuel rod guns, plasma mortars, and plasma shots that fly and burst: Fuel Rod Grunt, Wraith, Hunter, Marauder. |
| [ ] | `sfx_fire_needle` | Needle weapons: Vampire, Heavy Grunts. |
| [ ] | `sfx_fire_beam` | The Scarab's beam: 2 s of charging, then 3 s of firing, 5.5 s in all. |
| [ ] | `sfx_fire_bio` | Flood spat bone, bone shards and spores. |
| [ ] | `sfx_fire_melee` | Melee with no faction of its own. |
| [ ] | `sfx_fire_melee_covenant` | Covenant melee: energy sword, talons. |
| [ ] | `sfx_fire_melee_flood` | Flood melee: claws, tendrils, bites. |

### Hits, deaths and engines (17)

| Done | Slot | When it plays |
| --- | --- | --- |
| [ ] | `sfx_death_lekgolo` | A Hunter or Goliath dying: the gore death. Until you add it, they keep the Covenant soldier's death sound. |
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

### Spartan weapons

The Spartans have your model now, each with a placeholder weapon from the game's own set. Real ones would replace these.

| Done | What | For | Placeholder for now |
| --- | --- | --- | --- |
| [ ] | Spartan Laser | Spartan. | Rocket launcher |
| [ ] | SRS99 sniper rifle | Spartan Sniper. | Assault rifle, drawn long |
| [ ] | M247H chaingun and Hydra rocket pod | Spartan Heavy Weapons. | Rocket launcher |
| [ ] | M41 rocket launcher | Spartan Grenadier. | Rocket launcher, drawn short |
| [ ] | Shotgun | Spartan CQB. | SMG |
| [ ] | BR85 battle rifle and M41 rocket launcher | Spartan Fireteam. | Assault rifle |
| [ ] | Plasma rifle | Elite, and the Jackal (Plasma Rifle). | Plasma pistol, drawn big |
| [ ] | Spike grenade launcher | Brute Grenadier. | Rocket launcher |

### UNSC

| Done | Unit | Notes |
| --- | --- | --- |
| [ ] | Flamethrower Trooper | A soldier with a flamethrower. |
| [ ] | Gremlin | Armored Division. |

### Flood

| Done | Unit | Notes |
| --- | --- | --- |
| [ ] | Growths for Infested vehicles | The Flood's tentacles, to fit on an Infested Scorpion, Elephant, Hornet and so on (decision 37). One or a few pieces that can be placed on any vehicle. |
| [ ] | Hunter and Goliath gore death | Worms spilling apart instead of today's burning wreck (decision 31). A worm pile or a short animation. |

### Towers

| Done | What | Notes |
| --- | --- | --- |
| [ ] | Command Center | Stands on each side's left. For now it is a block with a mast and a dish, built from plain shapes. |
| [ ] | Research Center | Stands on each side's right. For now it is a lab with a glowing dome. |

Only about 6 m of ground is free for each tower, though it can be as tall as you like.

## Ground textures (11)

These are the game's own drawn tiles. To replace one, save yours over the file of the same name in `Assets/_Project/Resources/Terrain/`, as a PNG that tiles seamlessly. The game uses it from the next build, and no code changes are needed. Keep the same size if you can.

| Done | File | Where it shows | Size now |
| --- | --- | --- | --- |
| [ ] | `GroundUnscDirt.png` | UNSC: bare earth. | 1024 x 1024 |
| [ ] | `GroundUnscGrass.png` | UNSC: grass. | 1024 x 1024 |
| [ ] | `GroundUnscRock.png` | UNSC: rock. | 1024 x 1024 |
| [ ] | `GroundCovenantPlate.png` | Covenant: metal plates. | 1024 x 1024 |
| [ ] | `GroundCovenantRock.png` | Covenant: rock. | 1024 x 1024 |
| [ ] | `GroundCovenantSand.png` | Covenant: sand. | 1024 x 1024 |
| [ ] | `GroundFloodMass.png` | Flood: the main living mass. | 1024 x 1024 |
| [ ] | `GroundFloodRot.png` | Flood: rot. | 1024 x 1024 |
| [ ] | `GroundFloodSinew.png` | Flood: sinew. | 1024 x 1024 |
| [ ] | `GroundGrain.png` | All maps: a grey grain over concrete and metal. Keep it close to plain grey, as colour comes from elsewhere. | 1024 x 1024 |
| [ ] | `GroundMacro.png` | All maps: large soft patches, so that the tiling doesn't show. Keep it close to plain grey too. | 512 x 512 |

The colours of the sky, sun and haze for each faction are in `Presentation/Terrain/TerrainTheme.cs`, if new ground wants a different light.

## Models you already gave me

Fitted to their units, so there is nothing left to do here.

- **UNSC:** Spartan (and the 5 Noble Spartans, recoloured), Archer Missile Pod, Cobra, Rhino, Sparrowhawk, Vulture, Longsword (in place of the Shortsword), Wolverine, Mantis, ODST, Condor Gunship.
- **Covenant:** Elite, Zealot (the Elite in gold), Ghost, Hunter Pair, Hunter Captain, Grunt Goblin, Skitterer, both Jackals (the shield is your Halo 5 one), Vampire, Marauder, Reaver, Brute Grenadier (the red Brute of the six), Goliath.
- **Flood:** Flood Abomination, Swarm Form, Infested Elite and Infested Ghost (the Elite and Ghost, tinted), Infested Marine, Combat Form, Seeder, Juggernaut, Spawner.
