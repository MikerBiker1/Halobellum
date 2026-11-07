HALOBELLUM: AUDIO SLOTS

To use your own music or sound, put the file in this folder:

    Assets/_Project/Art/Imported/Audio/Resources/AudioOverride/

named after the slot below, for example music_combat.ogg. That is all: the game uses your file in place of the
generated one the next time it starts, and takes it back if you remove it. Nothing else needs setting: music is
streamed and compressed, effects kept ready in memory (Scripts/Editor/AudioImportRules.cs).

- File types: .ogg, .wav, .mp3, .aif. Use .ogg or .wav for music: an .mp3 usually does not loop without a gap.
- Several files for one slot: name them slot_1, slot_2, slot_3 (up to 12), for example music_combat_1.ogg,
  music_combat_2.ogg. One is picked each time. They also replace all of a generated slot's variants.
- Music loops, so a track should end where it starts. It resumes where it was at the next round.
- Each thing you drop in should have a SOURCE.txt note saying where it came from (Bible Section 12). A plain text file
  here in Art/Imported/Audio is fine; don't put it in AudioOverride.
- The music and the end-of-round and end-of-game pieces are the owner's own (see SOURCE.txt), and so are the sound effects in SOURCE_SFX.txt.
- All the generated sounds are original placeholders made by Tools/Audio. They are meant to be replaced as you like.

Volumes in the game: PlayerPrefs Volume.Master, Volume.Music, Volume.Sfx, Volume.Ui.

MUSIC
  music_combat                     Each battle. Several songs: music_combat_1 and so on. One is picked at random.
  music_combat_flood_present       A battle with Flood on either side: joins the combat songs, and plays more often than any of them.
  music_deployment                 Each round's cards and deployment. Several songs: music_deployment_1 and so on. One is picked at random.
  music_mainmenu                   The main menu's music, also the draft screen (no side chosen yet). The first thing heard.

STINGERS
  stinger_game_defeat_covenant     The whole game lost, playing Covenant.
  stinger_game_defeat_flood        The whole game lost, playing Flood.
  stinger_game_defeat_flood_present The whole game lost against Flood, whoever you played.
  stinger_game_defeat_unsc         The whole game lost, playing UNSC.
  stinger_game_victory             The whole game won.
  stinger_match_defeat             A round lost, when its window opens. Several sounds: stinger_match_defeat_1 and so on. One is picked at random.
  stinger_match_defeat_flood_present A round lost against Flood: joins the other sounds.
  stinger_match_draw               A round drawn (neither side won it), when its window opens. No generated sound: silent until a file is in.
  stinger_match_victory            A round won, when its window opens.

BATTLE SOUNDS
  sfx_death_air                    An aircraft hit, falling and crashing.
  sfx_death_flood                  A Flood form dying.
  sfx_death_infantry               A UNSC soldier falling.
  sfx_death_infantry_covenant      A Covenant soldier falling.
  sfx_death_vehicle                A light vehicle going up.
  sfx_drop_in                      A squad falling into place: it lands 0.6 s in.
  sfx_explosion_large              The biggest units going up.
  sfx_explosion_medium             Medium vehicles and walkers going up.
  sfx_explosion_small              Rockets, shells and fuel rods landing.
  sfx_fire_beam                    The Scarab's beam: 2 s of charging, then 3 s of firing, 5.5 s in all.
  sfx_fire_bio                     Flood spat bone, bone shards and spores.
  sfx_fire_bomb                    Bombs, the release.
  sfx_fire_bullet                  Rifles, SMGs, pistols. Plays very often: keep it short and not too bright.
  sfx_fire_bullet_flood            A rifle in Flood hands (Scavenged Rifle).
  sfx_fire_cannon                  Tank and other cannons.
  sfx_fire_flame                   Flamethrowers.
  sfx_fire_gauss                   Gauss guns and the sniper rifle: one hard shot.
  sfx_fire_heavybullet             Machine guns, chainguns, autocannons, turrets, shotguns, where they have no file of their own (sfx_fire_m60, sfx_fire_autocannon, sfx_fire_shotgun).
  sfx_fire_laser                   The Spartan Laser and the Hunter Captain's Siphon Laser.
  sfx_fire_melee                   Melee with no faction of its own.
  sfx_fire_melee_covenant          Covenant melee (energy sword, talons).
  sfx_fire_melee_flood             Flood melee (claws, tendrils, bites).
  sfx_fire_needle                  Needle weapons.
  sfx_fire_plasma                  Plasma rifles, pistols, turrets.
  sfx_fire_plasmaheavy             Heavy plasma: plasma shots that fly and burst (Marauder), and fuel rod guns and mortars where they have no file of their own.
  sfx_fire_rocket                  Rocket launchers and missiles, the launch.
  sfx_flood_burst                  A carrier form going off, a Flood unit bursting, or Bone Shards landing.
  sfx_hum_engine                   A loop: the engines of all UNSC ground vehicles, as loud as how many are alive.
  sfx_hum_hover                    A loop: what flies or floats (aircraft, Covenant vehicles). Loops must close without a click.
  sfx_infection_rise               An infected corpse rising.
  sfx_infection_start              A dying unit being infected.
  sfx_pelican_crash                The Infected Pelican hitting the ground (sfx_death_air_unsc once it is in); also the biggest Flood units dying.
  sfx_pelican_dive                 The Infected Pelican starting its dive.
  sfx_spell_buff_covenant          Prophet's Blessing.
  sfx_spell_buff_flood             Hive Frenzy and Biomass Shell.
  sfx_spell_buff_unsc              Battlefield Rally.
  sfx_spell_drop_air               Banshee Ambush.
  sfx_spell_drop_flood             Dispersal Pods.
  sfx_spell_drop_pods              ODST Drop Pods.
  sfx_spell_emp                    Electromagnetic Pulse and Nerve Spores.
  sfx_spell_glassing               Glassing Beam: one sound for its whole 3 s. The owner's file builds up for 1.85 s before the beam strikes, so it starts that long ahead, and fades as the beam goes.
  sfx_spell_mac                    MAC Strike.
  sfx_spell_missiles               Archer Missile Barrage (one sound at its start).
  sfx_spell_nuke                   HAVOK Nuke.
  sfx_spell_plasma                 Plasma Bombardment.
  sfx_spell_shield                 Bubble Shield and Energy Shield Dome.
  sfx_spell_smoke                  Smoke Bomb.
  sfx_spell_spore                  Each hit of Blight Rain, and Spore Burst.
  sfx_spell_storm_tick             Each hit of the Plasma Storm.
  sfx_spell_wrath                  Gravemind's Wrath.

UI SOUNDS (each faction has its own set)
  any button; quieter, a squad selected or turned:
    ui_unsc_click  ui_covenant_click  ui_flood_click
  the mouse going over a button:
    ui_unsc_hover  ui_covenant_hover  ui_flood_hover
  a squad bought:
    ui_unsc_buy  ui_covenant_buy  ui_flood_buy
  a squad sold; quieter, a spell taken back:
    ui_unsc_sell  ui_covenant_sell  ui_flood_sell
  a unit unlocked or a tech bought:
    ui_unsc_unlock  ui_covenant_unlock  ui_flood_unlock
  something that can't be done, or a click on a greyed-out button:
    ui_unsc_error  ui_covenant_error  ui_flood_error
  a card taken:
    ui_unsc_cardpick  ui_covenant_cardpick  ui_flood_cardpick
  the cards skipped; quieter, a squad moved:
    ui_unsc_skip  ui_covenant_skip  ui_flood_skip
  a spell or an item placed:
    ui_unsc_spellplaced  ui_covenant_spellplaced  ui_flood_spellplaced
  a squad leveled up:
    ui_unsc_levelup  ui_covenant_levelup  ui_flood_levelup
  the battle starts:
    ui_unsc_battlestart  ui_covenant_battlestart  ui_flood_battlestart
  round won:
    ui_unsc_roundwon  ui_covenant_roundwon  ui_flood_roundwon
  round lost:
    ui_unsc_roundlost  ui_covenant_roundlost  ui_flood_roundlost
  match won (a short hit: the stinger follows):
    ui_unsc_matchwon  ui_covenant_matchwon  ui_flood_matchwon
  match lost:
    ui_unsc_matchlost  ui_covenant_matchlost  ui_flood_matchlost

UI SOUNDS BEFORE A SIDE IS CHOSEN (main menu, draft)
  ui_neutral_cardpick              
  ui_neutral_click                 
  ui_neutral_error                 
  ui_neutral_hover                 

ONE WEAPON'S OR UNIT'S OWN (no generated sound: until a file is in, the weapon's kind is heard; see AudioCatalog.OwnFire)
  sfx_fire_assaultrifle            Marines' MA5 and the Scavenged Rifle (Infested Marine, Combat Form).
  sfx_fire_smg                     ODSTs' M7 SMG. Loops while they fire, so the file should loop.
  sfx_fire_m60                     Every machine gun and chaingun: the Warthog's (and the Infested Warthog's), the Scorpion's and the Grizzly's
                                   coaxial guns, the Mantis, the Vulture, the Elephant's turrets, the Spartan's chaingun. Loops while they fire.
  sfx_fire_autocannon              Autocannons: Pelican, Sparrowhawk, Condor, Chopper. Loops while they fire.
  sfx_fire_br85                    The BR85 battle rifle (Spartan Fireteam).
  sfx_fire_beamrifle               The Beam Rifle (Jackal).
  sfx_fire_plasmaturret            The Phantoms' and the Scarab's plasma turrets (the same file as sfx_fire_plasmaheavy, in a voice group of its own).
  sfx_fire_ghost                   The Ghost's plasma bolts, and the Infested Ghost's. Loops while it fires.
  sfx_fire_banshee                 The Banshee's plasma cannons, and the Infested Banshee's.
  sfx_fire_banshee_bomb            The Banshee's fuel rod bomb, fired.
  sfx_explosion_banshee            The Banshee's fuel rod bomb, landing.
  sfx_fire_fuelrod                 Fuel rod guns: Fuel Rod Grunt, Grunt Goblin, Goliath.
  sfx_fire_hunter                  The Hunters' assault cannon.
  sfx_explosion_fuelrod            A fuel rod or a Hunter's shot landing.
  sfx_fire_mortar                  The Wraith's mortar, and the Infested Wraith's.
  sfx_flight_mortar                A Wraith's shell in the air, looped until it lands.
  sfx_fire_bruteshot               The Brute Grenadier's spike grenade launcher.
  sfx_fire_shotgun                 The Spartan CQB's shotgun.
  sfx_fire_sniper                  The Spartan Sniper's rifle.
  sfx_fire_spartanlaser            The Spartan Laser: its charge and shot, played as its 2.5 s wind-up starts. The shot should be 2.5 s in.
  sfx_fire_sword                   The Zealot's energy sword.
  sfx_sword_ready                  Every Zealot, as a battle starts.
  sfx_beam_charge                  The Scarab's beam charging (2 s), heard to its end. With it in, the beam plays in this order, not sfx_fire_beam:
  sfx_beam_in                      the beam starting to fire,
  sfx_beam_loop                    the beam firing, looped after the start,
  sfx_beam_out                     the beam stopping.
  sfx_walk_scarab                  A Scarab walking, looped.
  sfx_death_scarab                 A Scarab dying.
  sfx_death_air_unsc               A UNSC aircraft crashing, and the Infected Pelican's crash dive.
  sfx_death_infectionform          An Infection Form popping.
  sfx_call_seeder                  A Seeder, 3 to 6 s after its last call. Several: sfx_call_seeder_1 and so on.
  sfx_call_swarm                   A Swarm Form, 3 to 6 s after its last call. Several: sfx_call_swarm_1 and so on.
  sfx_shield_regen                 An energy shield starting to recharge (one at a time).
  sfx_death_lekgolo                A Hunter's or a Goliath's death.
  sfx_node_small                   A small or medium node of light hitting a base at the end of a round (until it is in: the battle's hits).
  sfx_node_big                     A big node of light (about 60 damage to a base and up, and every Titan's).

UI SOUNDS FOR EVERY FACTION
  A UI file named without its faction, such as ui_click or ui_error, serves every faction's set (and the neutral one) that has no
  file of its own here.

LOUDNESS
  Files can come as loud as they were made: the game measures each one and plays it at its slot's own level, a little under the music.
