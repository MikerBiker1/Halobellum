HALOBELLUM: AUDIO SLOTS

To use your own music or sound, put the file in this folder:

    Assets/_Project/Art/Imported/Audio/Resources/AudioOverride/

named after the slot below, for example music_unsc_battle.ogg. That is all: the game uses your file in place of the
generated one the next time it starts, and takes it back if you remove it. Nothing else needs setting: music is
streamed and compressed, effects kept ready in memory (Scripts/Editor/AudioImportRules.cs).

- File types: .ogg, .wav, .mp3, .aif. Use .ogg or .wav for music: an .mp3 usually does not loop without a gap.
- Several files for one slot: name them slot_1, slot_2, slot_3 (up to 12), for example music_unsc_battle_1.ogg,
  music_unsc_battle_2.ogg. One is picked each time. They also replace all of a generated slot's variants.
- Music loops, so a track should end where it starts. It resumes where it was at the next round.
- Each thing you drop in should have a SOURCE.txt note saying where it came from (Bible Section 12). A plain text file
  here in Art/Imported/Audio is fine; don't put it in AudioOverride.
- All the generated sounds and music are original placeholders made by Tools/Audio. They are meant to be replaced as you like.

Volumes in the game: PlayerPrefs Volume.Master, Volume.Music, Volume.Sfx, Volume.Ui.

MUSIC
  music_covenant_battle            Covenant: the battle.
  music_covenant_deployment        Covenant: deployment.
  music_covenant_menu              Covenant: the screens between battles.
  music_flood_battle               Flood: the battle.
  music_flood_deployment           Flood: deployment.
  music_flood_menu                 Flood: the screens between battles.
  music_mainmenu                   The main menu's music, also the draft screen (no side chosen yet). The first thing heard.
  music_unsc_battle                UNSC: the battle.
  music_unsc_deployment            UNSC: deployment, while placing the army.
  music_unsc_menu                  UNSC: the screens between battles (cards, round result, match over).

STINGERS
  stinger_covenant_defeat          Covenant: match lost.
  stinger_covenant_victory         Covenant: match won.
  stinger_flood_defeat             Flood: match lost.
  stinger_flood_victory            Flood: match won.
  stinger_unsc_defeat              UNSC: when the match is lost.
  stinger_unsc_victory             UNSC: a short piece when the match is won (the music ducks under it).

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
  sfx_fire_gauss                   Gauss guns.
  sfx_fire_heavybullet             Machine guns, chainguns, autocannons, turrets.
  sfx_fire_laser                   The Spartan Laser.
  sfx_fire_melee                   Melee with no faction of its own.
  sfx_fire_melee_covenant          Covenant melee (energy sword, talons).
  sfx_fire_melee_flood             Flood melee (claws, tendrils, bites).
  sfx_fire_needle                  Needle weapons.
  sfx_fire_plasma                  Plasma rifles, pistols, turrets.
  sfx_fire_plasmaheavy             Fuel rod guns and plasma mortars.
  sfx_fire_rocket                  Rocket launchers and missiles, the launch.
  sfx_flood_burst                  A carrier form going off, or a Flood unit bursting.
  sfx_hum_engine                   A loop: the engines of all UNSC ground vehicles, as loud as how many are alive.
  sfx_hum_hover                    A loop: what flies or floats (aircraft, Covenant vehicles). Loops must close without a click.
  sfx_impact_armor                 A hit on armor.
  sfx_impact_block                 A shot stopped by a held shield.
  sfx_impact_flesh                 A hit on something unarmored.
  sfx_impact_shield                A hit on an energy shield.
  sfx_impact_wet                   A hit on the Flood.
  sfx_infection_rise               An infected corpse rising.
  sfx_infection_start              A dying unit being infected.
  sfx_pelican_crash                The Infected Pelican hitting the ground; also the biggest Flood units dying.
  sfx_pelican_dive                 The Infected Pelican starting its dive.
  sfx_spell_buff_covenant          Prophet's Blessing.
  sfx_spell_buff_flood             Hive Frenzy and Biomass Shell.
  sfx_spell_buff_unsc              Battlefield Rally.
  sfx_spell_drop_air               Banshee Ambush.
  sfx_spell_drop_flood             Dispersal Pods.
  sfx_spell_drop_pods              ODST Drop Pods.
  sfx_spell_emp                    Electromagnetic Pulse and Nerve Spores.
  sfx_spell_glassing               Glassing Beam (one sound for its whole 3 s).
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
  any button:
    ui_unsc_click  ui_covenant_click  ui_flood_click
  the mouse going over a shop or card button:
    ui_unsc_hover  ui_covenant_hover  ui_flood_hover
  a squad bought:
    ui_unsc_buy  ui_covenant_buy  ui_flood_buy
  a squad sold:
    ui_unsc_sell  ui_covenant_sell  ui_flood_sell
  a unit unlocked or a tech bought:
    ui_unsc_unlock  ui_covenant_unlock  ui_flood_unlock
  something that can't be done, or a click on a greyed-out button:
    ui_unsc_error  ui_covenant_error  ui_flood_error
  a card taken:
    ui_unsc_cardpick  ui_covenant_cardpick  ui_flood_cardpick
  the cards skipped:
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
