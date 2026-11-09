---
title: About this manual
group: Reference
icon: book
order: 41
blurb: Where the numbers come from, and how to rebuild the manual after the game changes.
---

## Where the numbers come from

Nothing in the unit, tech, spell, item or tower pages is typed by hand. A builder script reads the game's own data files and writes this manual from them:

- **Units, techs, spells, items and factions** come from the data assets the game loads (`Assets/_Project/Data`).
- **Rules** (income, base health, the damage table, towers, flanking and the rest) come from the combat and match rules assets.
- **Good against · bad against** and the counter web come from the measured matchup table that the Hard AI uses.
- **Pictures** are the unit icons the game draws on its cards, rendered from the real models, and the card emblems.
- **The written pages** (like this one) are Markdown files in `Tools/Wiki/content`.

Figures marked *DPS* are worked out from the data: damage per shot ÷ seconds between shots, times the bodies in a squad, then times the [damage table](#/page/combat) for each armor. They leave out splash, misses, travel time and movement, so treat them as a guide.

## Rebuilding

After any change to units, techs, spells, cards, rules or these pages, rebuild from the repository root:

```
python Tools/Wiki/build_wiki.py
```

Then open `Wiki/index.html` in a browser. It needs no server and no internet connection, though it uses web fonts when it can reach them.

## Keyboard

- **Ctrl K** or **/** opens search, from any page.
- **↑ ↓** move through results, **Enter** opens one, **Esc** closes search.

## A fan project

Halobellum is a private, non-commercial fan project. This manual stays private with it.
