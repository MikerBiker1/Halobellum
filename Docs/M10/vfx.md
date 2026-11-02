# M10: effects and explosions

Everything here is looks only. The simulation is read, never changed, and the battle's own random numbers are not touched. Everything is original and made in code: textures, chunk meshes and shaders. Nothing was downloaded.

## How it is built

All the code is in `Assets/_Project/Scripts/Combat/Presentation/Effects/`, with `CombatEffectsView.cs` next to it as the part that listens to the battle. Shaders are in `Assets/_Project/Resources/Effects/`, so that builds include them.

- **EffectKit**: one shared ParticleSystem per kind of particle: glow, streak, bolt, sparks, fire, flame, smoke, dust, spores, shock ring, ground light, scorch mark, solid chunks, heat shimmer, slash. An effect is a few `Emit` calls on these, so no objects are made during battle, and each kind is one draw call however many effects use it. Particles run at the battle's speed: they stop when it is paused and run four times as fast at 4x.
- **Redrawn each frame**: shells in flight ("heads"), the burning spot of a beam ("ground heads"), the puffs of a nuke's cloud ("cloud puffs"), and glowing lines (beams, lasers, lightning, a pool of 64 LineRenderers).
- **EffectRecipes**: how a muzzle flash, an impact, a blast, a fireball or a death is put together from those. A waiting list on battle time holds what comes a moment later, such as a bullet's impact once it has arrived.
- **WeaponLooks**: a table of colours and sizes per weapon kind, the kind of death per unit, the look per spell. Plain data, tested.
- **EffectBudget**: what may still start. See "Budget" below.
- **DeathEffects, SpellEffects, CameraShake, EffectsPostProcess, EffectsGallery**: as named. **MushroomCloud** is the shape of the nuke's cloud.

## What each weapon kind looks like

Colour says who fired: UNSC warm yellow, Covenant blue, with green fuel rods and pink needles, Flood a sick green. If both sides could fire the same colours (a mirror match, or the same unit type on both sides in the sandbox), the second side's shots are pushed towards red.

| Kind | Look | Who |
|---|---|---|
| Bullet, heavy bullet | Flash, hot streak; sparks on armour, dust on anything else, a blue flash on a shield that is up. A shotgun has a wide flash, smoke at the muzzle and a thick streak | Marine, Warthog, turrets, Spartan CQB |
| Cannon | Hard flash, smoke and sparks at the muzzle, a long thin nearly white tracer with hardly any glow and a thin trail, a blast as wide as its splash | Scorpion, Elephant |
| Gauss | A bright straight line from muzzle to target at once, white core; an impact where it hits. A sniper's is thinner, in its side's tracer colour | Gauss Warthog, Cobra, Gremlin, Spartan Sniper |
| Rocket | A short flame with a wide glow at the head, a thick pale smoke trail that grows with the camera's distance, a blast | Wolverine, Mantis, Hornet, Archer (in an arc) |
| Bomb | A short fat falling shape with a slow pulsing glow, redder than a rocket's, or a green ball for a fuel rod bomb. Flood bombs are a sick green and burst in gore and spores. The Methane Wagon's burning keg is an orange ball dripping embers, with a fire blast | Hornet, Banshee, Infested Hornet, Infested Banshee, Methane Wagon |
| Plasma | Fat glowing bolt, splash of droplets | Grunt, Jackal, Elite, Ghost; green on Flood craft |
| Heavy plasma | Big glowing ball that drips light, plasma blast | Fuel Rod Grunt (green), Wraith mortar (blue), Hunter (green), Blisterback's missiles and Marauder's cannon (blue) |
| Needle | Thin pink streak, pink shards on impact | Vampire |
| Laser | One straight red line with a white-hot core; burning spot and sparks | Spartan |
| Flame | A real cone of fire, small at the muzzle and growing to the end of its range, sparks, heat shimmer | Flamethrower Trooper |
| Melee | A quick crescent at the point of contact; a ground slam adds a dust ring as wide as its splash | Zealot, Flood forms, Abomination |
| Bio | Spat bone with a spore at the muzzle; spores on impact; lobbed shards burst in gore and spores | Swarm Form, Seeder, Pure Form Ranged |
| Beam | Charging: thin wavering line, light gathering at the muzzle, the target spot growing. Firing: a thick glowing beam with a white core on a burning spot that throws sparks, fire and smoke, and scorches the ground as it moves | Scarab |

Weapons the simulation hits with at once (many of them) still appear to fly for a moment, and their impact comes when they arrive. The damage has by then already happened, by about a tenth of a second. A hit is never shown without its damage.

Shots in flight are blended between the simulation's last two ticks, as bodies are, so a 45 m/s rocket that the simulation moves 1.5 m at a time moves a little every frame on the screen. Like a body, it is shown up to a tick behind the simulation. Trails are laid by distance along that path, not once per tick or per frame, so they look the same at any frame rate and battle speed. Where a shot lands is where the simulation last had it, as before.

## Deaths

- **Infantry** fall with a puff of dust. Baked bodies keep their own death animation; others tip over, lie a moment and sink.
- **Vehicles** blow up: fireball, smoke, sparks, flying chunks that bounce on the ground, a shock ring and a scorch mark. The body stays as a **blackened wreck** that burns and smokes for 14 seconds of battle time, then sinks away. This is a change of behaviour: a dead vehicle used to vanish at once. `BattleSceneTests.FullBattleInTheScene_MatchesTheHeadlessRunner` asserted that views are hidden the moment a battle ends, so it now waits for wrecks to finish. It still requires that every dead unit's view goes.
- **Titans** (Scarab, Elephant) go up in a chain of blasts over two seconds, and burn for 24 seconds.
- **Air units** blow up in the air, rain chunks and fall burning to the ground, where they crash with a fireball and a scorch mark.
- **Flood** burst in gore and spores, leaving a dark splatter. The Abomination comes apart in several heaves.
- **Infected Pelican**: fire and smoke all the way down its dive. On impact, a blast about as big as a titan's: a white flash, a fireball a little wider than what it damages, two rings (the first shows the real reach of its burst, 7 m), a spore cloud, a rain of chunks, and the blast rolling on for a moment. The owner asked for it to be at least 50% smaller after playing, so everything it draws was halved from what it was (a flash and a ground glow 36 m across, not 71 m; 196 particles at once, not 402), its shake went from 1 to 0.7 of the strongest, which is half as much movement, and the fire left burning on the spot is half as wide. The burst's reach and damage in the simulation are not touched. The crash's sound is a fixed cue that doesn't scale with the blast and stays as it was.

## Spells

Each looks like nothing else. Gameplay is untouched. They run from `SpellEffects`, so they also show in tests and the sandbox tool; `SpellView` still draws the outlines and the ring pulses of buffs and EMPs, and no longer draws its flat flash for damage spells.

- **MAC Strike**: one white-blue line from orbit, a hard flash, a crown of thrown earth, no fire.
- **HAVOK Nuke**: a red blinking designator in the last seconds; then a white flash over half the field, three rings, a running wave of dust, and a mushroom cloud: a stem on a skirt of dust and a wide cap rolling over itself, up within two seconds as a boiling ball of fire, glowing in its folds at three, a dark cloud at five, thinning away from then, gone at 8.6 seconds of battle time. Its shape is worked out from its age for every puff each frame (`MushroomCloud`): 137 puffs of smoke and 58 licks of fire, in two layers of 320 and 140, so two nukes at once fit. It freezes and speeds up with the battle. The camera looks down at 55 degrees, so the cap is narrower north to south than east to west (a round one would cover its own stem); the camera never goes round to see that. A puff the camera is inside fades, so zooming in doesn't fill the screen with smoke.
- **Plasma Bombardment**: a violet column and a splash of plasma, with violet light left on the ground.
- **Plasma Storm**: a dark violet cloud overhead, bolts and lightning coming down for as long as it lasts.
- **Glassing Beam**: a line that thickens in the last seconds, then a thick blue-white beam from orbit; the ground goes molten and glows for six seconds more.
- **Missile Barrage**: missiles coming in on smoke trails, each with its own blast.
- **Gravemind's Wrath, Spore Burst**: the ground bursts open in flesh and spores with green spouts; Wrath stirs the ground with spores before it goes off.
- **Blight Rain**: a sick cloud and green rain, splats where it lands.
- **EMP**: blue rings and lightning. **Nerve Spores**: a wave of spores. **Buffs**: a ring and rising motes. **Smoke**: a real cloud over its area for as long as it hangs. **Drops**: a streak and a ring of dust.

A spell with an unknown name goes by what it does, so a new spell looks sensible without any work.

## Other

- **Dust**: vehicles kick up dust while moving, infantry a little now and then. Each body is looked at once per 7/60 of a second of battle time, so the amount doesn't change with the frame rate or the speed of the battle.
- **Bloom**: `EffectsPostProcess` turns on the camera's post-processing and adds a global bloom with a threshold above white. Only effects draw above white, so units and the ground are untouched. Its priority is low, so another volume that sets bloom wins.
- **Camera shake**: small, on big blasts, scaled to the camera's distance. **The K key turns it off**, and it is remembered between runs. Any settings screen can use `CameraShake.ShakeEnabled`. It runs after `BattleCameraController` and only adds an offset, so that script is unchanged.
- Small things are drawn a little bigger the further the camera is (`EffectKit.ReadScale`), so that a tracer stays visible from the game camera without looking like a searchlight close up.
- **Anti-aliasing**: 4x MSAA on the High quality level (`Assets/Settings/PC_RPAsset.asset`). Low stays off. It is what stops thin glowing seams and veins breaking into dashes at the game camera, and it smooths fences and thin Flood forms. See "Seeing and measuring it" for its cost.

## Budget

Layers have fixed sizes, about 12,000 particles in all. Importance decides what may still start:

- Small things (flashes, bullet sparks, trail puffs, dust) stop when their layer is 60% full, or after 260 in one frame.
- Medium ones stop at 85%. Big ones stop at 97%. Huge ones (spells, the crash) stop only when the layer is full.
- When frames take over 13 ms, the limits for small and medium fall, down to 12% and 40% at 22 ms, and they recover slowly.

Live things are capped too: 48 corpses, 28 burning spots, 700 waiting effects, 64 lines, 16 beams, 24 running spells. Past the corpse cap the oldest wreck goes first.

## Seeing and measuring it

`SandboxAutoplay` runs the tools in a built game:

- `-sandboxDemo <folder> -effects`: pictures of every weapon kind firing, every kind of death, the crash and every spell, each from the game camera and close by.
- `-sandboxDemo <folder> -gallery`: the same, after the real models.
- `-sandboxDemo <folder> -effects -galleryOnly <text>`: only the pictures whose names contain that text, such as `w_Railgun`, `d_Scorpion`, `crash` or `s_`.
- `-sandboxDemo <folder> -effectsBench`: a battle of about 200 bodies with effects off, on at 1x and on at 4x. Frame times go to `bench.txt`. Add `-msaa 1` or `-msaa 4` to measure one setting of anti-aliasing against another on one build; it lasts for that run and nothing is saved.
- The gallery writes the frame times after each spell goes off to the log (`Effects gallery: ... after it went off`).

What 4x MSAA cost on this PC (Radeon RX 7800 XT), on one build, runs one after another, average frame in the bench's battle at 1x and at 4x. Other programs were using the PC during these runs, so the runs differ among themselves by more than the setting does:

| Window | No MSAA, 1x | 4x MSAA, 1x | No MSAA, 4x | 4x MSAA, 4x |
|---|---|---|---|---|
| 1600 x 900, first pair | 1.98 ms | 1.83 ms | 2.07 ms | 2.23 ms |
| 1600 x 900, second pair | 1.99 ms | 1.64 ms | 2.25 ms | 1.87 ms |
| 2560 x 1200 | 2.19 ms | 2.13 ms | 2.45 ms | 8.39 ms (see below) |

The 8.39 ms is one 23 seconds of one run: the same run's 90 seconds at 1x were normal, and the same setting and speed at 1600 x 900 were not slow. It looks like something else on the PC, but that isn't proved. So the cost is too small to measure here, about 0.2 ms at most; it has not been measured on a weaker graphics card. Soft smoke against the ground, the heat shimmer, the deployment squares and screenshots were all looked at in stills with it on, and no fault shows.

## Garbage during battle

Effects, blobs and sound make none per shot or per frame. What the battle still makes comes from the HUD, about 1.2 KB a frame, and all of that is inside Unity: in Unity 6 every piece of text IMGUI draws costs about 85 bytes, even the same text in the same place (measured: GUI.Label, GUI.Button and GUIStyle.Draw alike, nothing for textures, GUI.color or our own HUD helpers). The battle's screen draws about 14 pieces of text. Going further means a battle HUD that isn't IMGUI.

So, for what is ours: no OnGUI script of the battle has the layout pass on (`useGUILayout = false`: it made new layout groups for each script every frame), HUD text comes from `HudLine`, which builds a string only when its numbers change, the stats window works out its text a few times a second, and panels keep their arrays.

## Shared files touched

- `UnitViewManager.cs`: two small methods, `ShownPosition` and `KeepCorpse`.
- `SpellView.cs`: the flat flash for damage spells removed.
- `SandboxAutoplay.cs`: the flags above.
- `Halobellum.Presentation.asmdef`: references to URP, for the bloom.
- `BattleSceneTests.cs`: see "Deaths".
