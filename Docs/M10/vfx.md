# M10: effects and explosions

Everything here is looks only. The simulation is read, never changed, and the battle's own random numbers are not touched. Everything is original and made in code: textures, chunk meshes and shaders. Nothing was downloaded.

## How it is built

All the code is in `Assets/_Project/Scripts/Combat/Presentation/Effects/`, with `CombatEffectsView.cs` next to it as the part that listens to the battle. Shaders are in `Assets/_Project/Resources/Effects/`, so that builds include them.

- **EffectKit**: one shared ParticleSystem per kind of particle: glow, streak, bolt, sparks, fire, flame, smoke, dust, spores, shock ring, ground light, scorch mark, solid chunks, heat shimmer, slash. An effect is a few `Emit` calls on these, so no objects are made during battle, and each kind is one draw call however many effects use it. Particles run at the battle's speed: they stop when it is paused and run four times as fast at 4x.
- **Redrawn each frame**: shells in flight ("heads"), the burning spot of a beam ("ground heads"), and glowing lines (beams, lasers, lightning, a pool of 64 LineRenderers).
- **EffectRecipes**: how a muzzle flash, an impact, a blast, a fireball or a death is put together from those. A waiting list on battle time holds what comes a moment later, such as a bullet's impact once it has arrived.
- **WeaponLooks**: a table of colours and sizes per weapon kind, the kind of death per unit, the look per spell. Plain data, tested.
- **EffectBudget**: what may still start. See "Budget" below.
- **DeathEffects, SpellEffects, CameraShake, EffectsPostProcess, EffectsGallery**: as named.

## What each weapon kind looks like

Colour says who fired: UNSC warm yellow, Covenant blue, with green fuel rods and pink needles, Flood a sick green. If both sides could fire the same colours (a mirror match, or the same unit type on both sides in the sandbox), the second side's shots are pushed towards red.

| Kind | Look | Who |
|---|---|---|
| Bullet, heavy bullet | Flash, hot streak; sparks on armour, dust on anything else, a blue flash on a shield that is up | Marine, Warthog, turrets |
| Cannon | Hard flash, smoke and sparks at the muzzle, a shell with a thin trail, a blast as wide as its splash | Scorpion, Elephant |
| Gauss | A bright straight line from muzzle to target at once, white core | Gauss Warthog |
| Rocket | Flame at the head, a thick smoke trail, a blast | Wolverine, Mantis, Hornet, Archer (in an arc) |
| Bomb | Falling streak, or a green ball for a fuel rod bomb | Hornet, Banshee |
| Plasma | Fat glowing bolt, splash of droplets | Grunt, Jackal, Elite, Ghost; green on Flood craft |
| Heavy plasma | Big glowing ball that drips light, plasma blast | Fuel Rod Grunt (green), Wraith mortar (blue), Hunter (green) |
| Needle | Thin pink streak, pink shards on impact | Vampire |
| Laser | One straight red line with a white-hot core; burning spot and sparks | Spartan |
| Flame | A real cone of fire, small at the muzzle and growing to the end of its range, sparks, heat shimmer | Flamethrower Trooper |
| Melee | A quick crescent at the point of contact; a ground slam adds a dust ring as wide as its splash | Zealot, Flood forms, Abomination |
| Bio | Spat bone with a spore at the muzzle; spores on impact; lobbed shards burst in gore and spores | Swarm Form, Seeder, Pure Form Ranged |
| Beam | Charging: thin wavering line, light gathering at the muzzle, the target spot growing. Firing: a thick glowing beam with a white core on a burning spot that throws sparks, fire and smoke, and scorches the ground as it moves | Scarab |

Weapons the simulation hits with at once (many of them) still appear to fly for a moment, and their impact comes when they arrive. The damage has by then already happened, by about a tenth of a second. A hit is never shown without its damage.

## Deaths

- **Infantry** fall with a puff of dust. Baked bodies keep their own death animation; others tip over, lie a moment and sink.
- **Vehicles** blow up: fireball, smoke, sparks, flying chunks that bounce on the ground, a shock ring and a scorch mark. The body stays as a **blackened wreck** that burns and smokes for 14 seconds of battle time, then sinks away. This is a change of behaviour: a dead vehicle used to vanish at once. `BattleSceneTests.FullBattleInTheScene_MatchesTheHeadlessRunner` asserted that views are hidden the moment a battle ends, so it now waits for wrecks to finish. It still requires that every dead unit's view goes.
- **Titans** (Scarab, Elephant) go up in a chain of blasts over two seconds, and burn for 24 seconds.
- **Air units** blow up in the air, rain chunks and fall burning to the ground, where they crash with a fireball and a scorch mark.
- **Flood** burst in gore and spores, leaving a dark splatter. The Abomination comes apart in several heaves.
- **Infected Pelican**: fire and smoke all the way down its dive. On impact, the biggest thing a unit can do on the field: a white flash, a fireball far wider than a titan's, two rings (the first shows the real reach of its burst), a big spore cloud, a rain of chunks, the blast rolling on for a moment, and the strongest camera shake. Spells can be bigger still: the nuke is.

## Spells

Each looks like nothing else. Gameplay is untouched. They run from `SpellEffects`, so they also show in tests and the sandbox tool; `SpellView` still draws the outlines and the ring pulses of buffs and EMPs, and no longer draws its flat flash for damage spells.

- **MAC Strike**: one white-blue line from orbit, a hard flash, a crown of thrown earth, no fire.
- **HAVOK Nuke**: a red blinking designator in the last seconds; then a white flash over half the field, a fireball, three rings, a running wave of dust, and a mushroom cloud that keeps rising for four seconds.
- **Plasma Bombardment**: a violet column and a splash of plasma, with violet light left on the ground.
- **Plasma Storm**: a dark violet cloud overhead, bolts and lightning coming down for as long as it lasts.
- **Glassing Beam**: a line that thickens in the last seconds, then a thick blue-white beam from orbit; the ground goes molten and glows for six seconds more.
- **Missile Barrage**: missiles coming in on smoke trails, each with its own blast.
- **Gravemind's Wrath, Spore Burst**: the ground bursts open in flesh and spores with green spouts; Wrath stirs the ground with spores before it goes off.
- **Blight Rain**: a sick cloud and green rain, splats where it lands.
- **EMP**: blue rings and lightning. **Nerve Spores**: a wave of spores. **Buffs**: a ring and rising motes. **Smoke**: a real cloud over its area for as long as it hangs. **Drops**: a streak and a ring of dust.

A spell with an unknown name goes by what it does, so a new spell looks sensible without any work.

## Other

- **Dust**: vehicles kick up dust while moving, infantry a little now and then. Each body is looked at every seven frames.
- **Bloom**: `EffectsPostProcess` turns on the camera's post-processing and adds a global bloom with a threshold above white. Only effects draw above white, so units and the ground are untouched. Its priority is low, so another volume that sets bloom wins.
- **Camera shake**: small, on big blasts, scaled to the camera's distance. **The K key turns it off**, and it is remembered between runs. Any settings screen can use `CameraShake.ShakeEnabled`. It runs after `BattleCameraController` and only adds an offset, so that script is unchanged.
- Small things are drawn a little bigger the further the camera is (`EffectKit.ReadScale`), so that a tracer stays visible from the game camera without looking like a searchlight close up.

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
- `-sandboxDemo <folder> -effectsBench`: a battle of about 200 bodies with effects off, on at 1x and on at 4x. Frame times go to `bench.txt`.

## Shared files touched

- `UnitViewManager.cs`: two small methods, `ShownPosition` and `KeepCorpse`.
- `SpellView.cs`: the flat flash for damage spells removed.
- `SandboxAutoplay.cs`: the flags above.
- `Halobellum.Presentation.asmdef`: references to URP, for the bloom.
- `BattleSceneTests.cs`: see "Deaths".
