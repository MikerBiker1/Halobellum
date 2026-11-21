---
title: Modding unit stats
group: Reference
icon: list
order: 43
blurb: Change any unit's numbers with the JSON files in the game folder's UnitStats folder.
---

## The UnitStats folder

Next to the game's program there is a folder called **`UnitStats`** with **11 JSON files**, one per faction and sub-faction:

| File | Units in it |
| --- | --- |
| `UNSC.json`, `Covenant.json`, `Flood.json` | The faction's base roster, plus every unit that only appears through spells, spawns, death bursts, drops or seeds |
| `UNSC_Noble.json`, `UNSC_ArmoredDivision.json`, `UNSC_AerospaceCommand.json` | The UNSC sub-factions' exclusive units |
| `Covenant_Banished.json`, `Covenant_LekgoloColony.json`, `Covenant_UnggoyRebellion.json` | The Covenant sub-factions' exclusive units |
| `Flood_UNSCbased.json`, `Flood_Covenantbased.json` | The Flood sub-factions' exclusive units |

Each unit appears once, in one file. Edit a number, save, and start the game again: **the game reads the files at every launch**.

## What you can change

- **Per unit:** cost, unlock cost, squad size, tier, base damage, health, armor type, shield and shield regeneration, move speed, turn time, target filter, lifesteal.
- **Per weapon:** damage per shot, time between shots, range and minimum range, projectile speed, splash, bonus against air, damage type, anti-armor, whether it can be blocked; and, where the weapon already has them, cone, beam, seed, hit-slow, wind-up and stagger values.
- **Per tech:** cost, its multipliers and bonuses.
- **Sections, where the unit already has them:** defense arc, death burst, spawning, mid-battle drops, aura, leap.
- Not in the files: roles, whether a unit flies, its size, Flood flags, which unit it spawns or drops, tech texts and the art.

The weapon and tech lists must keep the same count and names in the same order. Keys that start with `_` are ignored, so you can use them for notes.

## When a file has a mistake

- Any mistake in any file (bad JSON, an unknown unit or field, a wrong type, a value out of its limits, a change the battle would refuse, a squad too big for the deployment zone) means **every unit uses the game's own values**.
- The main menu then shows a panel, **"Unit stats files have mistakes"**, listing them as `<file> line L: ...`, and the menu's corner reads "Unit stats: defaults (files have mistakes)". Every error is also written to Player.log.
- **Your files are never changed.** Fix the line and start the game again.
- With good edits the menu reads "Unit stats: modded (N changes)".

## Good to know

- The unit pages in this manual show the **default** values; the files override them.
- **Each new game build overwrites the files.** Keep a copy of your edits somewhere else.
- **Delete a file to get it back:** a missing file is written again, with the defaults, at the next launch.
- The AI judges a unit by its **default** reputation (its measured counter table), but pays your modded prices.
- Modded stats apply in every mode, the Sandbox included. A match played with them notes `"unit_stats":"modded:N"` in its battle log; a replay always uses the defaults.
- The Unity editor, the tests and the balance tournament never read the files.
