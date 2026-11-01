# Daily report: 2026-11-01 (evening), M8: Flood

Today's earlier report (M7, D33 to D37) is in this file's Git history. You confirmed M7. **M8 is built**: the Flood are playable in matches and the Sandbox, for you and the AI. All tests pass. It is waiting for your play test and confirmation.

## What M8 adds
- **Packs (your idea):** Infection Forms come as 50 forms in 10 packs of 5. A pack moves as one body and shows and deals less as its forms die. A bullet hurts one form; splash, flame, beams and spells hurt all of them.
- **Infection:** an Unarmored unit killed within 2 m of Infection Forms turns green, gets up 2 s later at 60% health and charges its old friends.
- **11 units:** Infection Forms and Carrier Form (Tier 1); Combat Form, Stalker Form, Pure Form Ranged and Swarm Form (Tier 2); Pure Form Tank, Seeder, Infected Pelican and Infected Phantom (Tier 3); Flood Abomination (Tier 4).
- **Choices I made (D38):** the Swarm Form fills the open anti-air slot. The Seeder plants a seed that eats its host, and a host that dies seeded rises whatever its armor. The Abomination speeds up and heals nearby Flood and keeps releasing forms.
- **8 Flood spells,** three of which raise the Unarmored units they kill. Items, economy and buff cards are shared.
- **AI:** Normal and Hard avoid Unarmored squads once they have seen your Flood. Easy doesn't care.
- **Look:** a Flood UI theme with its own textures and title font. Units are placeholder art.

## How powerful infection is (measured)
One battle per line, about 1800 credits per side, Infection Forms only:

| Against | Bodies that rose | Left at the end |
| --- | --- | --- |
| Marines | 317 of 324 (98%) | 89 of 180 packs, 255 risen Marines |
| Grunts | 331 of 360 (92%) | 99 of 180 packs, 286 risen Grunts |
| Jackals (Plasma Rifle) | 43 of 96 (45%) | 129 of 180 packs, 43 risen Jackals |
| Elites | 6 of 18 (33%) | 150 of 180 packs, 6 risen Elites |
| Flamethrower Troopers | 0 of 72 | All Forms dead, all 72 Troopers alive |
| Scorpions | 0 of 6 | All Forms dead, all 6 Scorpions alive |

In a mixed army it is much smaller: in the Sandbox demo, 4 squads of Forms against 4 of Marines plus a Scorpion and a Spartan got only 7 bodies up.

## Balance (first pass)
- **Infection Forms at equal cost:** beat Marines, Grunts, Jackals and Elites. Lose to Flamethrowers, Fuel Rod Grunts, Scorpions, Wraiths, Mantises and Elephants.
- **Counter web:** 35 of 35 units have a counter winning at least 90%.
- **For the balance pass:** the Scarab beats 33 of 34. Warthog and Ghost each beat only 1 of the 11 Flood units. Infection Forms also beat Spartans and Warthogs. 49 of 595 pairs reach the 90 s limit (it was 4 of 276).

## Decisions needing your approval
**D38** in `DECISIONS.md` lists everything I decided on your carte blanche. Change anything you like.

## Test results
- **Unity:** EditMode **408 / 408**, PlayMode **25 / 25**. **.NET harness:** **360 / 360**.
- **New tests:** packs, infection rules (armor, shields, Spartans, range, no second infection), the Seeder, death bursts, the crash dive, spawning, the aura, the Flood data, the AI's reaction, and a scene test in which a Marine really rises.
- **Built game:** 3 rounds of autoplay as UNSC, Covenant and Flood, plus the Sandbox demo, with no errors.
- **Not tested automatically:** how it feels. The balance numbers come from armies of a single unit type, so mixed armies may play differently.
- **Small note:** at the bottom of a long stats window, the last tech's description can touch the frame.

## Blockers
None.
