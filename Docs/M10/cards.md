# Cards with unit icons and borders (M10)

*The Blisterback and Methane Wagon named below were removed later (D56). This is kept as it was written.*

The owner's words: "Cards should have icons with the units, different borders of cards depending on spell, econ, buff, reinforcement factions."

## What you see

**Starting draft.** Up to five cards (it lays out 2 to 5). Each card has its faction's border, the faction's name and emblem, the sub-faction on a big banner across the card, and its two free squads with big pictures.

**Card offers.** Every card has a border and a coloured ribbon that say what kind it is, at a glance, by colour and by shape:

| Kind | Colour | Shape |
| --- | --- | --- |
| Offensive spell | red-hot on charred iron | saw teeth all round, spikes at the corners, cracks with heat in them |
| Defensive spell | blue | smooth and rounded, two rings with light between them, a small shield in each corner |
| Economy | green and gold | gold coins along the edges, a big coin in each corner |
| Army buff | orchid | a crown of points on top, chevrons rising up the sides, swept-up top corners with a star |
| Item | brass | a machine part: notched cut corners, hex bolts along the edges, a hex nut in each corner |
| Training | teal | a stitched uniform patch, with three rank chevrons in each corner |
| Reinforcements, UNSC | steel and yellow | cut corners, plates and rivets, hazard stripes |
| Reinforcements, Covenant | violet | sweeping fins at two corners, lines of blue light |
| Reinforcements, Flood | sick green and flesh | a lumpy rim that grows over the card, veins and pustules |

A spell or a buff has a large emblem; a reinforcement card has the unit's picture, with its level and number of squads on it. The card under the mouse glows in its kind's colour.

**Small pictures.** The shop and unlock rows, the sandbox's unit list and the squad window also show the unit's picture.

## Unit pictures

`UnitIcons.For(unit, faction)` gives the picture of any unit, and never fails. It looks up `Resources/UnitIcons/<unit name>.png` by name.

The **Unit Icon Renderer** makes those files (Halobellum > Art > Render Unit Icons, or `-executeMethod Halobellum.EditorTools.UnitIconRenderer.RenderAllFromCommandLine`). **Run it again whenever a unit gets a real model, or when new units are added.** It renders each real model and baked body from a 3/4 front view, and writes a designed fallback for each unit that is still a placeholder. `UnitIcons/index.txt` lists how each picture was made.

A unit added since the tool last ran still gets its fallback, put together at run time from the same parts, so nothing breaks while new units come in.

Three things keep the pictures readable at the size of a shop row, about 40 pixels:

- A baked body wears its unit's `bakedTint`, as in battle, so an Infested Marine doesn't look like a Marine.
- A tall thin figure (narrower than half its height: the soldiers) is framed from the waist up, and fades out at the bottom instead of being cut off.
- A picture darker than a common level is brightened to it. The curve lifts shadows and midtones and can't blow out highlights.

Units with the same body and tint have the same picture, as they look the same in battle: Spartan Sniper and Fireteam, Spartan Grenadier and Heavy Weapons. A test checks that units sharing a body but not a tint have different pictures.

**The fallback** is a dark shape of the unit's kind of body with an edge of light, on a glow in the faction's colour, with its role's emblem in a badge. The shape comes from its stats (`UnitIcons.BodyShapeFor`): trooper, heavy trooper, shield trooper, hulk, speeder, tank, walker, flyer, pod, Flood form, Flood beast or Flood swarm. Units whose stats hide their shape are named there, such as the Goliath (a hulk) and the Reaver (a walker). Two borrow the closest shape there is: the Skitterer uses the cluster of small crawling bodies, and the Methane Wagon the launcher pod.

39 units fall back today, because they have no model yet. `UnitIcons/index.txt` lists them.

## The art

All of it is original and drawn by code, nothing downloaded: `Tools/UI/make_card_art.py` (numpy and Pillow). Run it again after changing it. Unity makes the .meta files; `CardArtImport` sets the import settings.

- `Resources/CardArt/Frames`: a 512 x 512 border and a ribbon per kind.
- `Resources/CardArt/Emblems`: white emblems (tinted when drawn): one per spell, one per spell effect (a new spell falls back on its effect's), one per kind of card, one per faction, and large versions of the item and card icons of `Art/UI/Items`. The faction emblems are plain abstract marks made for this game, on purpose nothing like any existing insignia. The owner can replace them.
- `Resources/CardArt/Layers`: the body shapes and role emblems of the fallback.

**How a border stays crisp.** It is a nine-slice with a difference: the corners are fixed, and each edge *repeats* its middle part a whole number of times instead of stretching it, so rivets, teeth and growths keep their shape at any card size. So everything on an edge in the generator repeats every 192 pixels (a test checks that it joins up), and the big shapes are in the corners. Pieces are placed on whole screen pixels, because the immediate-mode GUI otherwise leaves hairlines between them.

## Layout

`CardLayout.Arrange` fits 1 to 5 cards across any screen. A test checks every count at 16:9, 4:3, 5:4, 21:9 and a tiny window. Text shrinks to fit its place on a narrow card, down to a size that is still legible. It never breaks a word in the middle if a smaller size would avoid it. Narrow draft cards put each squad's text under its picture; wide ones put it beside.

The sub-faction text on a draft card is measured for the whole row (`CardScreens.MeasureDraftRow`): every card has the same size of text and the same height for it, so nothing jumps between neighbours and the squads line up. It takes up to 32% of the card's free height, from sizes 16 and 15 down to 13.

## Looking at it

`Halobellum.exe -screen-fullscreen 0 -screen-width 1600 -screen-height 900 -cardsDemo <folder>` takes a picture of every card screen for all three factions, at several window sizes: the draft with 2 to 5 cards, every kind of card, reinforcements, the hover glow, the shop and squad window, and the sandbox's list. It logs which screens have text that did not fit, and which have text that fits only at the smallest size (`Card demo finished: ...`). Its fullest draft uses the 5 real sub-factions with the most to say. It builds offers and sets the phase directly, which never happens in a real match.

`Tools/UI/icon_sheet.py` makes a contact sheet of the unit pictures. `make_card_art.py --sheet out.png` makes one of the frames, with edges tiled as in the game.

## Code

- `UI/UnitIcons.cs`: the lookup and the fallback.
- `UI/CardArt.cs`: frames, ribbons, emblems, drawing.
- `UI/CardLayout.cs`: where the cards go (plain arithmetic).
- `UI/CardScreens.cs`: draws one card, and the text that fits.
- `UI/CardScreensDemo.cs`: the screenshots.
- `UI/MatchHud.cs`: the draft and card screens now only lay out and call these. `DrawGeneralCard`, `DrawReinforcementCard`, `CardKindText` and `CardKindColor` moved to `CardScreens`.
- `Editor/UnitIconRenderer.cs`, `Editor/CardArtImport.cs`: the tool and the import settings.
- `Tests/EditMode/Unity/CardArtTests.cs`.

The simulation was not touched.

## When this is merged with the sub-factions work

Both branches change `MatchHud.DrawDraft` and the shop rows. Keep this branch's `DrawDraft`, and then:

- In `DrawDraft`, pass the sub-faction's text: `CardScreens.DrawDraftCard(row.CardAt(i), m.PlayerDraft[i], m.PlayerDraft[i].SubFaction.Description)`. It has a parameter ready for it. (This branch can't use that field yet: the sub-factions branch adds it.) The card already lists the exclusive units and marks an exclusive squad.
- In `DrawShop`, keep the picture and the moved text from this branch, and add the sub-factions branch's `player.Label` and ", exclusive".
- Then run Render Unit Icons, and `-cardsDemo` to look at the result. New units with no model get their fallback at run time until then.
