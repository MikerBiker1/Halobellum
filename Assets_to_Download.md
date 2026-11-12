# Assets to download

Everything in the game that is still a stand-in, for you to replace. I tick things off as you add them. Nothing here is required for the game to run.

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
| [x] | A round drawn | When neither side wins a round: `stinger_match_draw`. | Your `draw.mp3` (D71) |
| [x] | Node of light hitting a base, small and big | The end of a round: `sfx_node_small` below about 63 damage to a base, `sfx_node_big` from there and for every Titan. | Your `Small&medium light nodes` and `big light node` (D71) |
| [ ] | Node of light taking off | The end of a round, optional. | Silence |

## Sound effects

All your files from `Art_Source/SFX` are in: 55 of the first 59 (D70), and the 7 of the second batch (D71), the third batch (D91) and the Longsword and falling-strike sounds (D117), and the Banshee crash, turret explosion and Ghost knock-out (D120). `Assets/_Project/Art/Imported/Audio/SOURCE_SFX.txt` says which file fills which slot. To add or change one, put it in `Art_Source/SFX` and tell me, or run `Tools/Audio/bring_in_sfx.py` after adding its line there.

Not used: the plasma grenade (no use yet), the magnum (no unit carries one), and `fire[plasmarifle1]` (the same file as the Banshee's). The beam rifle now has a carrier, the Jackal (Beam Rifle).

### Weapons firing

A weapon with a sound of its own uses it; the others use their kind's sound.

| Done | Slot | Used by | Your file |
| --- | --- | --- | --- |
| [x] | `sfx_fire_assaultrifle` | Marines, Infested Marines, Combat Forms. | `assault_rifle_fire_brown2_1` |
| [x] | `sfx_fire_smg` | ODSTs. Once per shot, overlapping (D117). | `fire[smg_new01]` |
| [x] | `sfx_fire_m60` | Every machine gun and chaingun (D71): Warthog and Infested Warthog, Scorpion and Grizzly coaxial guns, Mantis, Spartan Heavy Weapons' M247H, Vulture, Elephant turrets, Marauder's Spike Turret. Loops while firing. | `fire[m60_ster1]` |
| [x] | `sfx_fire_autocannon` | Autocannons: Pelican, Sparrowhawk, Condor, Chopper. Loops while firing. | `Autocannon` (D71) |
| [x] | `sfx_fire_br85` | The BR85 (Spartan Fireteam). | `fire_BR85` (D71) |
| [x] | `sfx_fire_beamrifle` | The Jackal (Beam Rifle)'s Beam Rifle. | `beam_rifle_fire[cov_snip_shot9]` (D71) |
| [x] | `sfx_fire_spartanlaser` | The Spartan Laser: played as its 2.5 s wind-up starts, so the shot in the file lands with the shot. | `Spartan_laser_shoots_at_2` (D71) |
| [x] | `sfx_fire_plasmaheavy`, `sfx_fire_plasmaturret` | Heavy plasma: the Marauder's cannon; and the plasma turrets of both Phantoms and the Scarab. | `phantom_turret_fire[phantom_fire]` (D71) |
| [x] | `sfx_fire_ghost` | Ghost and Infested Ghost. Once per shot, overlapping (D117). | `ghost_fire[plasma_ghost]` |
| [x] | `sfx_fire_flame` | Flamethrowers. Loops while firing. | `flamethrower_fire_FT_active` |
| [x] | `sfx_fire_plasma` | Plasma rifles, pistols and turrets. | `plas_rifle_fire[plas_rif_fire]` |
| [x] | `sfx_fire_banshee` | Banshee and Infested Banshee plasma cannons. | `banshee_fire[plasmarifle1]` |
| [x] | `sfx_fire_banshee_bomb`, `sfx_explosion_banshee` | The Banshee's fuel rod bomb, fired and landing. | `banshee_cannon_fire`, `banshee_cannon_expl` |
| [x] | `sfx_fire_fuelrod`, `sfx_explosion_fuelrod` | Fuel Rod Grunt, Grunt Goblin, Goliath; their shots landing. | `Fuelrod_fire_grunt`, `fuel_rod_gun_explosion` 1 and 2 |
| [x] | `sfx_fire_hunter` | Hunters' assault cannon. | `Hunter_fuel_rod_gun_fire` |
| [x] | `sfx_fire_mortar`, `sfx_flight_mortar` | Wraith and Infested Wraith mortar, and the shell in the air. | `wraith_fire_mortar`, `Wraith airborn projectile loop` |
| [x] | `sfx_fire_bruteshot` | Brute Grenadier. | `brute_shot_fire` |
| [x] | `sfx_fire_shotgun` | Spartan CQB. | `fire[shotgun2]` |
| [x] | `sfx_fire_sniper` | Spartan Sniper. | `Sniper fire[ruger24]` |
| [x] | `sfx_fire_sword`, `sfx_sword_ready` | Zealots: the sword, and readying it as a battle starts. | `sword_impact_character`, `sword_ready` |
| [x] | `sfx_fire_needle` | Needle weapons. | `fire[needlefire11]` |
| [x] | `sfx_fire_rocket` | Rockets and missiles. | `fire[rocket16]` |
| [x] | `sfx_fire_cannon` | Tank cannons. | `scorpion_cannon_fire` |
| [x] | `sfx_fire_gauss` | Gauss Warthog, Cobra, Gremlin (my guess for this file). | `fire[grifle03]` |
| [x] | `sfx_beam_charge`, `_in`, `_loop`, `_out` | The Scarab's beam, in that order. | `Beam Charge scarab`, `in`, `loop`, `out` |
| [ ] | `sfx_fire_bullet` | Other rifles, SMGs and pistols, such as the Marines' and the placeholder rifles. | Generated stand-in |
| [ ] | `sfx_fire_heavybullet` | Only a heavy gun that is none of the above, if one comes. | Generated stand-in |
| [ ] | `sfx_fire_bomb` | Hornet and Longsword bombs. | Generated stand-in |
| [x] | `sfx_fire_siphon` | The Hunter Captain's 1 s siphon laser, as each beam starts (D91). | `Captain hunter cannon and other similar` |
| [x] | `sfx_fire_longsword` | The Longsword's carpet bombs going out (D117). | `Longsword_firing` |
| [x] | `sfx_falling_in` | Every UNSC rocket, missile and bomb in flight (D118), and the Archer Missile Barrage and the HAVOK Nuke (D117), heard coming down, ending as they land. | `rocket or bomb flying down` |
| [x] | `sfx_death_banshee` | The Banshee and the Infested Banshee shot down: 1.3 s of falling, then the crash as the wreck lands (D120). | `banshee-banshee-windup-and-explosion` |
| [x] | `sfx_explosion_turret` | Every machine whose death had a generated sound (Warthog, Ghost, Chopper, Cobra, Gremlin, Wolverine, Reaver, Grunt Goblin, Archer pod, the Phantoms, the Vampire), and the Ghost's blast after its drift (D120). | `turret_explosion` |
| [x] | `sfx_death_ghost_start` | A Ghost knocked out, as it starts drifting away; it blows up 2.1 s later (D120). | `ghost_initial_destroyed` |
| [ ] | `sfx_fire_laser` | Any other laser shot with no file of its own (none today). | Generated stand-in |
| [x] | `sfx_fire_hunterbash` | A Hunter's shield bash (D91). | `hunter_melee_hits` |
| [ ] | `sfx_fire_bio` | Flood spat bone, bone shards and spores. | Generated stand-in |
| [x] | `sfx_fire_melee_flood` | Flood blows landing, one of three at random (D91). | `flood melee impact` 1 to 3 |
| [ ] | `sfx_fire_melee`, `_covenant` | Other melee: blades, rams. | Generated stand-ins |

### Explosions, deaths and the Flood

| Done | Slot | When it plays | Your file |
| --- | --- | --- | --- |
| [x] | `sfx_explosion_small` | Rockets, shells and grenades landing. | `frag_grenade_expl_frag2` |
| [x] | `sfx_explosion_medium` | Medium vehicles and walkers going up. | `med_explosion_15` |
| [x] | `sfx_explosion_large` | The biggest units going up. | `large_explosion_1`, `_12` |
| [x] | `sfx_death_scarab`, `sfx_walk_scarab` | A Scarab dying, and walking. | `Scarab dying`, `Scarab walking` |
| [x] | `sfx_death_air_unsc` | A UNSC aircraft crashing, and the Infected Pelican's crash dive. | `UNSC air unit hitting the ground` |
| [x] | `sfx_pelican_dive` | The Infected Pelican starting its dive; any aircraft falling as it dies, until it hits the ground, where its crash is heard (D117). | `Infested pelican dive` |
| [x] | `sfx_death_flood` | A Flood form dying. | `combatform_body_destroyed` |
| [x] | `sfx_death_infectionform` | An Infection Form popping. | `infectionform_die2` |
| [x] | `sfx_flood_burst` | A Carrier Form going off, and other Flood bursts. | `flood carrier explosion` |
| [x] | `sfx_infection_rise` | A unit rising as Flood: one of four howls. | `flood_howls` 1 to 4 |
| [x] | `sfx_call_seeder`, `sfx_call_swarm` | Seeders and Swarm Forms, every 3 to 6 s. | `Seeder` 1 to 3, `Swarm form` 1 to 3 |
| [x] | `sfx_shield_regen` | An energy shield starting to recharge. | `shield_regen` |
| [ ] | `sfx_death_infantry` | A UNSC soldier falling. | Generated stand-in |
| [ ] | `sfx_death_infantry_covenant` | A Covenant soldier falling. | Generated stand-in |
| [ ] | `sfx_death_vehicle` | A light vehicle going up. | Generated stand-in |
| [ ] | `sfx_death_air` | A Covenant or Flood aircraft going down. | Generated stand-in |
| [x] | `sfx_death_lekgolo` | A Hunter, the Hunter Captain or the Goliath dying (D91). | `Hunter death` |
| [x] | `sfx_death_human`, `_spartan`, `_elite`, `_grunt`, `_jackal`, `_brute` | Each kind of soldier dying (D91); the generated falls stay for any other. | `human death (marine odst)`, `death spartan`, `Elite death`, `grunt death`, `Jackal death`, `Brute death` |
| [x] | `sfx_death_flood_small`, `_big`, `_huge` | The Flood dying, by size (D91). | `flood death small`, `big`, `huge` |
| [x] | `sfx_flood_leap` | A Flood form leaping (D91). | `flood leap` |
| [x] | `sfx_infectionform_leap` | Infection Forms leaping (D108). | `Infection form leap` |
| [x] | `sfx_hunter_charge` | A Hunter charging (D91). | `Hunter charge` |
| [x] | `sfx_pelican_crash` | The biggest Flood units dying: the same as the UNSC air crash, as you said. | `UNSC air unit hitting the ground` (D71) |
| [ ] | `sfx_infection_start` | A dying unit being infected. | Generated stand-in |
| [ ] | `sfx_hum_engine`, `sfx_hum_hover` | Loops: UNSC ground engines; what flies or floats. They must close without a click. | Generated stand-ins |
| [ ] | `sfx_drop_in` | A squad falling into place: it lands 0.6 s in. | Generated stand-in |
| — | `sfx_impact_armor`, `_flesh`, `_shield`, `_block`, `_wet` | Hits. Taken out (D82): hits have no sound. | Not wanted |

### Interface

A UI file named without a faction, such as `ui_click`, serves every faction.

| Done | Slot | When it plays | Your file |
| --- | --- | --- | --- |
| [x] | `ui_click` | Any button. | `UI buttons` |
| [x] | `ui_error` | Something that can't be done. | `Failure to place unit` |
| [x] | `ui_cardpick` | A card taken. | `Card chosen` |
| [x] | `ui_buy` | A squad bought. | `Unit bought (fall to grid)` |
| [x] | `ui_unlock` | A unit unlocked or a tech bought. | `Unit unlocked` |
| [x] | `ui_battlestart` | A round's battle starting, every faction. | Your `Round start.wav` (D78) |
| [x] | `ui_levelup`, `ui_spellplaced`, `ui_itemplaced` | A level-up; a spell placed; an item put in a hexagon (D91). | `Level up`, `Spell placed`, `Move item in Hexagon` |
| [ ] | `ui_hover`, `ui_sell`, `ui_skip`, `ui_roundwon`, `ui_roundlost`, `ui_matchwon`, `ui_matchlost` | The rest of the interface. Each can also have one per faction, such as `ui_unsc_hover`. | Generated stand-ins |

### Spells

| Done | Slot | When it plays | Your file |
| --- | --- | --- | --- |
| [x] | `sfx_spell_glassing` | The Glassing Beam: starts 1.85 s before the beam strikes, so its build-up leads into the strike, and fades as the beam goes. | `Glassing beam` (D71) |

The other 16 spell slots still have generated stand-ins, listed in `Assets/_Project/Art/Imported/Audio/README.txt`.

## Models left to find

These units are placeholder shapes, or borrow another unit's body. Weapons on a model are welcome, because shots leave from its muzzles.

### Spartan weapons

The Spartans have your model now, each with a placeholder weapon from the game's own set. Real ones would replace these.

| Done | What | For | Placeholder for now |
| --- | --- | --- | --- |
| [x] | Spartan Laser | Spartan. | Your `weaponshumanspartan_laser.glb`: the second laser, the green one with the small blue screen (D85) |
| [x] | SRS99 sniper rifle | Spartan Sniper. | Your `weaponshumansniper_rifle.glb`, the third (D86) |
| [x] | M247H machine gun | Spartan Heavy Weapons. | Your `Meshy_AI_UNSC_machinegun.glb`, whole; it has no rocket pod any more (D132) |
| [ ] | M41 rocket launcher | Spartan Grenadier. | Rocket launcher, drawn short |
| [x] | Shotgun | Spartan CQB. | Your `weaponshumanshotgun.glb`, the third, the darkest (D86) |
| [x] | BR85 battle rifle | Spartan Fireteam. | Your `br55_-_halo_battle_rifle.glb` (D131) |
| [ ] | M41 rocket launcher | Spartan Fireteam. | None: its rockets leave the rifle |
| [x] | Energy sword | Zealot. | Your twinblade, `Meshy_AI_Twinblade_of_the_Azur_1112012036_texture.glb`, its blades glowing (D131) |
| [x] | MA5B assault rifle | Marines, Infested Marines. | Your `ma5b_remaster.glb` (D131) |
| [x] | Plasma rifle | Elite. | Your `halo_-_plasma_rifle.glb` (D71) |
| [x] | Beam rifle | Jackal (Beam Rifle). | Your `halo_-_beam_rifle.glb` (D71) |
| [x] | Brute Shot (in place of the spike grenade launcher) | Brute Grenadier. | Your `weaponscovenantbrute_shot.glb`, the first (D86) |

### UNSC

| Done | Unit | Notes |
| --- | --- | --- |
| [x] | Flamethrower Trooper | Your `Meshy_AI_Flamethrower_trooper.glb`, rigged by Meshy, with clips made around its crouched pose (D71). |
| [x] | Gremlin | Your `Meshy_AI_Gremlin.glb` (D71). |

### Flood

| Done | Unit | Notes |
| --- | --- | --- |
| [x] | Growths for Infested vehicles | Your `Meshy_AI_growths.glb`: pods and bubbles in 3 to 5 clumps on each of the 8 infested vehicles (D71). A trial, to revert if you don't like it. |
| [ ] | Hunter and Goliath gore death | Worms spilling apart instead of today's burning wreck (decision 31). A worm pile or a short animation. |

### Towers

| Done | What | Notes |
| --- | --- | --- |
| [x] | Command Center | Your `Meshy_AI_command_center.glb`; its red trim glows in the owner's faction colour (D71). |
| [x] | Research Center | Your `Meshy_AI_Research center.glb`; its lights glow in the owner's faction colour (D71). |

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

- **UNSC:** Scorpion (your updated one, D71), Spartan (and the 5 Noble Spartans, recoloured), Archer Missile Pod, Cobra, Rhino, Sparrowhawk, Vulture, Longsword (in place of the Shortsword), Wolverine, Mantis, ODST, Condor Gunship.
- **Covenant:** Scarab (your updated one, the purple Scarab of `Scarab update.glb`, D84), Covenant Phantom (your Phantom, in Covenant purple), Elite, Zealot (the Elite in gold), Ghost, Hunter Pair, Hunter Captain, Grunt Goblin, Skitterer, both Jackals (the shield is your Halo 5 one), Vampire, Marauder, Reaver, Brute Grenadier (the red Brute of the six), Goliath.
- **Flood:** Flood Abomination, Swarm Form, Infested Elite and Infested Ghost (the Elite and Ghost, tinted), Infested Marine, Combat Form, Seeder, Juggernaut, Spawner.
- **Main menu:** the Covenant supercarrier (your `Meshy_AI_covenant_supercarrier.glb`, cut down to 12,000 triangles), glassing the land beyond the hills far off (D104).
