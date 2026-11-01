# Things the human needs to do

Exact steps for anything an agent cannot do (editor clicks, importing assets, decisions). Newest at the bottom. Tick items when done.

## 2026-10-31: check M0 and M1 (a milestone is only done once you confirm it)

- [x] **Let Unity compile.** Click into the open Unity editor and wait for the spinner in the bottom-right corner to stop. The Console (Window > General > Console) should show no red errors.
- [x] **Run the tests.** Window > General > Test Runner > **EditMode** tab > **Run All**. Expected: 113 tests, all green.
- [x] **Run the headless battle.** Menu bar: **Halobellum > Headless Runner > Run Placeholder Round Robin**. The report appears in the Console (click the log line to see all of it), and a CSV file is written to `E:\AI\Halobellum\BalanceReports\`.
- [ ] **Optional: try a custom matchup.** **Halobellum > Headless Runner > Open Runner Window**. Under Army A > Squads press **+**, drag a unit asset from `Assets/_Project/Data/Units/Placeholder` into the slot and set the squad count; do the same for Army B; press **Run**.
- [x] **Review DECISIONS.md D1 to D9.** These are rules the Bible leaves open, which I filled in so the simulation could run. For each one, reply "approve", or say what you want instead.
- [x] **Optional (D4, arena size):** if you want the arena to match Mechabellum, show me the Mechabellum folder or tell me the size, and I will propose a value.
- [x] **Confirm M0 and M1** so M2 can start in a later session.

## 2026-10-31: after your D1 to D9 review

- [x] **D10 (square size):** D4 is in squares, so I set one square to 2 m (details in DECISIONS.md). Reply "approve", or give another size. Changing it later only means editing `squareSize` in `Assets/_Project/Data/CombatRules.asset`.
- [ ] **Optional re-check:** in Unity, Test Runner > EditMode > Run All should now show 123 tests, all green.

## 2026-10-31: check M2 (battle in the scene)

- [ ] **Watch the battle.** In Unity open `Assets/_Project/Scenes/Battle.unity` (Project window, double-click) and press **Play**. 300 blue placeholder units fight 300 red ones.
- [x] **Try the controls.** Pan: W A S D / arrow keys, or hold the middle mouse button and drag. Zoom: mouse wheel. Speed: the buttons at the top, or Space (pause), 1 (1x), 2 (2x), 3 (4x). Left-click a unit to open its squad's stats window (bottom-left); right-click or Escape closes it.
- [ ] **Compare with the headless runner.** When the battle ends, the Console shows `Battle finished: seed 1000, 974 ticks ...`. Then use **Halobellum > Headless Runner > Run Selected Battle Setup**: it prints `Headless result ...` with the same line.
- [ ] **Optional: frame rate on another PC.** I measured on your PC (RX 7800 XT, i7-12700KF), which is above mid-range. The FPS counter is in the top-right corner while playing. To test a built game, ask me for the build steps.
- [x] **Answer D11 and D12** in DECISIONS.md.
- [x] **Answer D13** (how turning works) in DECISIONS.md.
- [x] **Answer D14** (formation cohesion details) in DECISIONS.md.
- [x] **Answer D15** (acquisition range details) in DECISIONS.md.
- [x] **Answer D16** (formation rows) in DECISIONS.md.
- [x] **Confirm M2** so M3 can start.
- [ ] **Optional cleanup:** `E:\AI\_hbci` is my throwaway copy of the project for batch tests and builds (it needs a short path). You can delete it any time; I will recreate it when needed.

## 2026-10-31: check M3 (round loop)

- [x] **Play a match.** Open `Assets/_Project/Scenes/Match.unity` and press **Play**. Pick a draft card (Standard UNSC or Covenant; both use the placeholder units until M4).
- [x] **Deploy.** In the shop (right), press **Buy**, move the mouse over your (blue) zone: a green box shows where the squad will stand (red = blocked). Left-click to place, **R** to turn it, right-click to cancel. Click one of your squads to select it: **Move (M)**, **Turn (R)** and **Sell (Delete)** appear at the bottom (move and turn only for squads bought this round). Try **Unlock 50** on the heavy tank, then buy it.
- [x] **Check the limits:** only 2 new squads per round, 1 unlock per round, credits and next income in the top bar, no timer.
- [x] **Ready:** the AI's army appears and the battle plays (speed buttons at the top). After it, the result screen shows base damage both ways, income, survivors, top damage dealers and what killed what. **Continue** goes to the next round.
- [x] **Win and lose:** keep buying to destroy the AI's base (10 to 15 rounds with placeholder units), or sell everything and press Ready repeatedly to lose. **New match** starts over.
- [x] **Answer D17 to D20** in DECISIONS.md (income timing, Tier 1 unlocks, draft squads and selling, exact tie, temporary content).
- [x] **Re-check deployment after your D17 to D19 changes.** Start a match: you have 200 credits, and your 2 free squads stand in the middle of your zone. Press **Buy**: the squad falls from the sky to the middle of your zone and is selected when it lands; press **M** (or Move) to put it somewhere else, R to turn it. Select a free draft squad: Sell shows half its cost.
- [x] **Confirm M3** so M4 (slice rosters and sandbox) can start.

## 2026-10-31: check M4 (slice rosters and sandbox)

- [x] **Play a match with the real units.** Open `Assets/_Project/Scenes/Match.unity` and press **Play**. The draft cards now offer real Tier 1 squads (Marines, Flamethrower Troopers, Warthogs; Grunts, Fuel Rod Grunts, Jackals, Ghosts), and the shop lists all 12 units of your faction with their unlock prices.
- [x] **Try the sandbox.** On the draft screen press **Sandbox: test any two armies** (or open `Assets/_Project/Scenes/Sandbox.unity`). Choose **Blue** or **Red**, press **Place** next to a unit, and left-click in that side's zone (it keeps placing that unit until you right-click). Click a squad to **Move (M)**, **Turn (R)** or **Remove (Del)** it. **Copy to red** mirrors your army, the top bar shows both armies' cost. Press **Run battle**, then **Reset** to put both armies back and try again. **Back to match** returns to the draft.
- [x] **Placeholder units in the sandbox.** At the bottom of the sandbox's unit list, the 3 placeholder test units have their own "Placeholder" group. They never appear in a match.
- [ ] **Optional: run the counter web yourself.** Menu **Halobellum > Headless Runner > Run Counter Web** (takes several minutes; a progress bar shows). The report appears in the Console; the full win-rate matrix goes to `BalanceReports/`.
- [x] **Answer D21 to D24** in DECISIONS.md (first-pass stats, how the abilities work, counter web method and the 55-70% vs over-70% question, placeholder units).
- [x] **D23: pick the counter threshold.** My suggestion is in DECISIONS.md: a counter must win at least 9 of 10 equal-value battles (90%).
- [x] **Confirm M4** so M5 (AI opponent) can start.

## 2026-10-31 (night): check M5 (AI), M6 (leveling and techs) and the faction UI

- [x] **Pick a difficulty and play.** Open `Assets/_Project/Scenes/Match.unity`, press **Play**. On the draft screen choose **Easy**, **Normal** or **Hard** (remembered for next time), then a card. The top bar shows "UNSC vs Covenant / Normal AI".
- [x] **Watch the AI.** It now buys and places an army each round (chaff and tanks in front, ranged behind, flankers on the sides) and answers what you fielded last battle. It never sees what you buy this round.
- [x] **Level a squad up.** After a battle, the result screen lists your squads that are ready. Next round, click one: the stats window (bottom left) shows its level, XP bar and **Level up (price)**. A level-2 squad has double health and damage.
- [x] **Research a tech.** In the same window, **Buy** a tech: it applies to every squad of that type for the rest of the match. Try the Pelican's **Troop Bay** (it then drops 6 Marines 10 seconds into each battle) or the Flamethrower's **Wide Nozzle**.
- [ ] **Sandbox: levels and techs.** In the sandbox, click a squad: **Level +** / **Level -** and **Add** / **Remove** techs freely, per side.
- [x] **Look at the faction styles.** Play once as UNSC (steel panels with cut corners, amber capital titles) and once as Covenant (violet rounded panels with a glow, cyan titles). The draft screen and the sandbox stay neutral. Colours, fonts and shapes are in `Data/UI/*_Theme.asset` if you want to tweak them.
- [x] **Optional: real fonts.** (Done another way on 2026-11-01: free OFL fonts are now in `Art/Imported/Fonts`.) If you have Halo-style fonts you are allowed to use, put the font files in `Assets/_Project/Art/Imported/Fonts` (with a `SOURCE.txt`) and drag them into the theme's **Font Asset** slot.
- [x] **Answer D25 to D28** in DECISIONS.md (how the AI decides, leveling details, the tech catalogue, the faction styles).
- [x] **Confirm M4, M5 and M6.**

## 2026-11-01: check your D25, D26 and D28 changes

- [x] **Enemy ghosts (D25).** Play a match and finish round 1. In round 2's deployment, the enemy's army from the last battle stands in its zone in grey. Click a grey squad: the stats window shows it as "enemy, last battle" with its level and techs. What the AI buys this round stays hidden until you press Ready.
- [x] **Level-up arrow (D26).** When a squad's XP bar is full (now twice its price), a yellow arrow above it pulses bright, dim, bright. It goes away once you level the squad up. Size, colours and speed are on the Battle object's **Unit View Manager** (Ghosts and level-up arrows).
- [x] **Faction UI (D28).** Play once as UNSC and once as Covenant and tell me what to change: colours, how strong the hull texture is, fonts, anything. Screenshots are in `Docs/D28_*.png`. Quick tweaks without me: `Assets/_Project/Data/UI/UNSC_Theme.asset` and `Covenant_Theme.asset` (e.g. **Panel Detail Tint** alpha for the hull texture's strength, **Font Scale**, colours).
- [ ] **Optional: reference art.** If you want the textures closer to a particular look, put reference images (screenshots of Halo menus, ship close-ups) in a folder and tell me where; I redraw the textures from them.
- [x] **Confirm M4, M5 and M6** (still open from last night).

## 2026-11-01 (afternoon): check the income and crowd changes

- [x] **Income (D29).** Start a match: 200 credits. After round 1 the top bar's "+... next round" shows 400 and you receive 400; then 600, 800 ... up to 1600.
- [x] **Chaff makes way (D30).** In the sandbox (or a match), put a squad of Marines or Grunts in front of a Warthog or Ghost and run the battle: the chaff steps aside and the vehicle drives through instead of getting stuck at an angle.
- [x] **Front lines edge forward (D30).** Put two squads of the same unit one behind the other (e.g. 2 Marine squads) against an enemy: when the front squad starts shooting, it slowly moves up while firing, so the squad behind gets into range instead of jiggling. Tell me if it should stop sooner or later (now: at 25% of its range from the target), or move faster (now: half speed).
- [x] **Re-check after the second D30 change.** A Warthog or Ghost behind chaff should now drive straight at its target while the chaff splits to both sides (no turning left and right). Faster infantry behind a Scorpion or Titan go around it; the tank keeps its line.
- [ ] **Answer D30** in DECISIONS.md (the details of how crowds behave).

## 2026-11-01 (evening): check M7 (cards)

- [ ] **Card screen.** Play a match (`Assets/_Project/Scenes/Match.unity`, Play). After round 1, rounds 2 and 3 offer general cards: **Take** one (price on the button) or **Skip** (+50). Round 4 offers reinforcements (Skip gives +100), then it alternates.
- [ ] **Place a spell.** In deployment, the **Spells** panel (left) lists your spells. Press **Place**: the area follows the mouse, red for offensive spells (anywhere), blue for defensive ones (only your side), grey where it can't go. Left-click to place, right-click to cancel; **Move** and **X** change your mind. Press Ready and watch it go off.
- [ ] **Recharge.** The panel shows "Recharging: N round(s)" after use (MAC Strike: usable every other round).
- [ ] **Reinforcements.** Take a reinforcement card: its squads appear in the middle of your zone (move them like bought squads); you can still buy your 2 squads.
- [ ] **Try the big ones.** HAVOK Nuke / Glassing Beam (one copy, 4-round recharge), ODST Drop Pods (UNSC: 6 ODSTs fall from the sky where you placed them).
- [x] **Answer D31 and D32** in DECISIONS.md (spell numbers, card rule details).
- [ ] **Confirm M7.**

## 2026-11-01 (night): check your D31 and D32 additions

- [ ] **Items.** Take an item card. It appears in the **Inventory** (mid left). Drag it: an empty hexagon appears over every squad without an item. Drop it on a hexagon: the squad now shows the item, and the stats window lists it. Dropping it elsewhere cancels.
- [ ] **Deployment Module.** Put one on a squad from an earlier round: Move and Turn work on it in every round.
- [ ] **Intensive Training.** Take the card. Its icon is at the top left. Select a squad, click the icon, **Confirm**: the XP bar fills, and the icon shows its cooldown.
- [ ] **Smoke and EMP.** Place a Smoke Bomb in front of the enemy (see the two-click placement below); it stays for the whole round. Place an EMP on shielded units. Both can catch your own units.
- [ ] **Friendly fire.** Drop a strike on your own squad: it takes the damage.
- [ ] **ODSTs (UNSC).** 12 troopers land: SMGs on infantry, rockets on vehicles.
- [ ] **Banshee Ambush (Covenant).** 2 Banshee squads drop in from above.
- [ ] **From round 5 and 7.** The extra deployment slot from round 5; the HAVOK Nuke, Glassing Beam, powerful buffs and Super Supply from round 7. The nuke lands 6 seconds in, so watch your troops walk out of (or into) it.
- [ ] **Shop.** Every unit shows its price, even before it's unlocked.
- [x] **Answer D33** in DECISIONS.md.
- [ ] **Sandbox items (D34).** In the Sandbox, the **Items** panel is at the top left. Drag an item onto the hexagon over a squad of either side. Select the squad: the stats window lists the item and has **Remove**. Run a battle to compare.
- [ ] **Two Intensive Trainings.** Take the card twice: two icons at the top left, each with its own cooldown.

## 2026-11-01 (play test changes): check D35

- [ ] **Dragging items.** Drag an item from the Inventory onto a hexagon (match and Sandbox). Dropping elsewhere cancels.
- [ ] **Smoke placement.** Press Place on a Smoke Bomb, click where one end goes, move the mouse (the rectangle swings around), click to confirm. Right-click goes back a step.
- [ ] **Shield bar.** Spartans, Elites and Zealots show a blue bar over their health bar once hit or when selected.
- [ ] **Speed (D36).** The trial is gone: units move at half their old speed and the Warthog, Ghost and Gauss Warthog are 1 m/s faster than that. Projectiles, rate of fire and the round clock are back to normal at 1x.
- [ ] **Reinforcement cards** no longer mention the budget. **Glassing Beam** hits air.

## 2026-11-01 (new weapon mechanics): check D37

- [ ] **Wraith and Archer Missile Pod.** In the Sandbox, watch the shots arc up and land. Moving units should mostly still get hit, and a fast vehicle that turns should sometimes dodge.
- [ ] **Scarab.** Watch for a thin line and a growing spot (2 s), then the thick beam (3 s). Kill its target during the beam: the beam should stay on and drift slowly to the next target. Placeholder art only.
- [ ] **Numbers I chose:** the 3 s rest between beams and the 2 m/s drift. Tell me if either feels wrong.

