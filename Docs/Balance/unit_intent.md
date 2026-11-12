# Unit intent sheet (draft for the owner)

This sheet says what each of the 69 units is for in an army, and how strong it should be for its price. It is the yardstick for balancing unit by unit: every sub-faction now wins 47% to 53%, but the units inside them deal from 0.2 to 2.5 times their price's worth of damage.
How to use it: edit any cell. A "?" marks a question for the owner, or a place where the numbers and the intent disagree.
"Strength for its price" is the target the balance test will hold the unit to. It is "fair" unless the row says otherwise, and the notes explain every exception.
"Measured now" comes from the D136 Insane against Insane tournament: first the ratio (the unit's share of its side's damage divided by its share of its side's credits; 1.00 means exactly its price's worth), then how often the AI buys it in matches where it can, then its army slot test value.
The army slot test (`unit_balance.md`, D137) measures what a slot of the unit adds to the armies the AI really builds, at the levels it reaches, in points of margin over a slot of the army's own mix: 0 is as good as the army's average credit; about ±4 is chance. A shared unit has one value per sub-faction that fields it. The low ratio of chaff, blockers and transports is often by design; the slot test judges them by what they let the army win.

**What the targets mean**

- **fair**: deals about its price's worth of damage (ratio near 1.00).
- **carry**: a fragile unit far back. Dealing well above its price's worth is the point; it pays for it in health.
- **soak**: chaff. Its job is to take fire meant for others, so damage below fair is fine. Judge it by how much fire it takes ("soak": share of its side's damage taken over share of its credits).
- **front line**: a tank. Damage a little below fair is fine if it takes more than its share of fire.
- **with drops**: judged with what it drops, releases or raises. The ratio already counts that damage as its own (D74).
- **vs air**: anti-air, judged by what it does to aircraft. With no enemy air, it is not expected to pay off.
- **Titan**: fair, but it should lose to its counter at equal credits.

"Listed" in "Should lose to" is the counter that the counter-web test checks for that unit (`CounterWebAssetTests`).

## UNSC

### Base roster

| Unit | Price (unlock) | Role | Job | Should beat | Should lose to | Strength for its price | Measured now | Notes for the owner |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Marine | 100 (0) | Chaff | 18 cheap riflemen who take the fire meant for the units behind them. | Single-target DPS: snipers, Spartans, Zealots | Chaff clear (listed: Fuel Rod Grunt) | soak | 1.05; bought 94%; slot Aerospace +14, Armored +8, Noble +3 | The owner today: "marine should be worse." Now it does both jobs: a fair share of damage (1.36 in Aerospace Command, where it adds 13 points of wins) and 2.5 times its share of fire taken. ? Cut its damage rather than its health, so it still soaks? |
| Flamethrower Trooper | 150 (0) | Chaff clear | Short-range fire that burns crowds of infantry and Flood forms. | Chaff, Infection Forms, infantry | Tanks, flankers (listed: Scorpion) | fair? | 1.69; bought 83%; slot Aerospace -11, Armored -13, Noble -14 | ? Splash units top the game's list. Much of their damage goes into cheap chaff, which has a lot of health per credit, and its win edge is small (-9 to +6). Allow chaff clear above fair, or hold it to fair? |
| Warthog | 200 (0) | Flanker | Fast jeep with a machine gun that runs round the line to the back. | Artillery, back-line DPS | Chaff clear, tanks (listed: Flamethrower Trooper) | fair | 0.40; bought 67%; slot Aerospace -17, Armored -19, Noble -23 | ? Every flanker is under fair (0.31 to 0.95), and one on one they lose to the artillery and DPS they should beat. On flankers the owner said "Keep them like this for now" (item 52). Health 300 to 360 in D125. |
| Gauss Warthog | 250 (50) | Single-target DPS | Fast anti-armor gun on wheels that kills tanks from 22 m. | Tanks and heavy units (listed counter of the Scorpion, Mantis, Grizzly, Marauder, Goliath and more) | Chaff, flankers (listed: Marine) | fair | 1.67; bought 9%; slot Aerospace +2, Armored +1, Noble +9 | Strong when bought (its side wins 5 to 10 points more in Noble and Armored Division, about even in Aerospace Command) but rarely bought. Fine as a pure counter pick. |
| Scorpion | 300 (50) | Tank | Main battle tank: front-line health, a cannon and a machine gun. | Chaff clear, infantry | Single-target DPS (listed: Gauss Warthog) | front line | 0.98; bought 83%; slot Aerospace +4, Armored -2, Noble +6 | On target for damage. Takes only 0.83 of its share of fire: the chaff in front takes it. |
| Wolverine | 300 (50) | Anti-air | Rocket truck that shoots down aircraft from 24 m. | Aircraft | Ground units (listed: Warthog) | vs air | 1.16; bought 15%; slot Aerospace +5, Armored -2, Noble -5 | Bought only when the AI sees air, as meant. |
| Hornet | 300 (50) | Aerial | Pair of light gunships with rockets and bombs. | Ground armies without anti-air | Anti-air (listed: Wolverine) | fair | 1.03; bought 35%; slot Aerospace -20, Armored -14, Noble -15 | On target. It may not use the flank zones (D61: with it, Noble and Armored Division reached 70%). |
| Archer Missile Pod | 300 (50) | Artillery | Fixed missile battery that shells groups from 42 m. Cannot move. | Slow, grouped units; infantry | Flankers, air (listed: Hornet) | carry? | 0.87; bought 14%; slot Aerospace -11, Armored -21, Noble -15 | ? If artillery is a carry, as the owner said of the Pure Form Ranged, it is under target. It cannot move (D60). |
| Pelican | 300 (50) | Aerial | Dropship: lands 6 Marines 5 s in, then behind enemy lines, up to 3 drops. | Back-line units, through its Marines | Anti-air (listed: Wolverine) | with drops | 0.19; bought 22%; slot Aerospace -20, Armored -20, Noble -23 | Already counts its Marines' damage, and still about a fifth of its price's worth. The owner asked for 1,200 health "so it lives to land its Marines" (D73). D125 cut its gun: the counter web overrated it. ? Hold it to fair, or accept a unit the AI uses badly and a player uses better? The slot test runs without techs, so there it drops no one. |
| Spartan | 500 (250) | Single-target DPS | Super-soldier whose Spartan Laser kills tanks and Titans. | Tanks, Titans (listed counter of the Elephant, Scarab and Hunter Pair) | Chaff, flankers (listed: Marine) | fair | 1.52; bought 53%; slot Aerospace +13, Armored +15, Noble +12 | The owner today, on the Spartan (+10 win edge in Aerospace Command) against the Condor Gunship (-9), both 500: "a great example. 19 points of difference." Both should be fair: bring the Spartan down and the Condor up. |
| Mantis | 500 (250) | Tank | Heavy walker with a chaingun and rockets. | Chaff clear, infantry | Single-target DPS (listed: Gauss Warthog) | front line | 0.48; bought 29%; slot Aerospace -4, Armored -14, Noble -13 | ? Half its price's worth, and it takes only 0.84 of its share of fire despite 2,500 health. No recent change touched it. Raise it, or is it meant as a niche pick? |
| Elephant | 800 (350) | Titan | Mobile fortress: a cannon for armor, turrets for infantry. | Most things in a straight fight | Single-target DPS, big chaff swarms (listed: Spartan) | Titan | 0.87; bought 94%; slot Aerospace +2, Armored -2, Noble -4 | ? Almost every army takes one. Is a Titan meant to be in every army? Main cannon 220 to 300 in D125. |

### Noble

| Unit | Price (unlock) | Role | Job | Should beat | Should lose to | Strength for its price | Measured now | Notes for the owner |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Spartan Sniper | 400 (200) | Single-target DPS | Kills one armored target at a time from 42 m. | Tanks, Titans, heavy units | Crowds and anything that reaches it (listed: Marine) | carry? | 1.61; bought 72%; slot -1 | ? Fragile and far back, so a carry like the Pure Form Ranged? Taking it away cost Noble 13 points (D74). Damage 220 to 200 in D74. |
| Spartan Heavy Weapons | 400 (200) | Tank | A walking wall whose chaingun mows down infantry. | Infantry, chaff clear | Single-target DPS and anti-infantry fire once its shield is down (listed: Combat Form) | front line | 1.04; bought 96%; slot +8 | Noble's main unit (+17 win edge). The owner: it "does not shoot rockets" (D132); chaingun 18 at 20 m (D133). ? One on one it still wins 85% on average against the single-target DPS units that should counter it. |
| Spartan Grenadier | 400 (200) | Chaff clear | Rockets in a straight line: strong against crowds. | Chaff, infantry | Tanks, flankers (listed: Scorpion) | fair | 0.94; bought 13%; slot -0 | On target when bought, but rarely bought. |
| Spartan CQB | 300 (150) | Flanker | Fast Spartan with a shotgun; ground only, no leap. | Artillery and DPS, once it gets there | Chaff clear, tanks (listed: Scorpion); it can't shoot aircraft | fair | 0.50; bought 23%; slot -11 | The owner (D133): a shotgun can't shoot aircraft, so it was made "much stronger for it", and "if too strong, adjust until balanced". ? It is at half its worth: raise it. Its 300 price is under the Tier 3 band on purpose. |
| Spartan Fireteam | 500 (250) | Single-target DPS | Three Spartans with rifles and a rocket each: all-round. | No single strength | No single weakness (listed: Marine) | fair | 0.63; bought 12%; slot -13 | ? Weak and rarely bought. Health 420 to 500 in D125. |

### Armored Division

| Unit | Price (unlock) | Role | Job | Should beat | Should lose to | Strength for its price | Measured now | Notes for the owner |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Gremlin | 200 (0) | Flanker | Fast scout whose EMP slows vehicles. | Vehicles, artillery | Infantry, chaff clear (listed: Flamethrower Trooper) | fair | 0.72; bought 58%; slot -17 | Its slow helps the rest of the army, which the ratio does not show. ? Flankers are under fair (see Warthog). |
| Cobra | 300 (50) | Single-target DPS | Six-wheeled tank hunter with railguns, 26 m. | Tanks | Swarms, flankers that get under its guns (listed: Marine) | fair | 1.79; bought 33%; slot +10 | Railgun 190 to 175 when Armored Division felt high (D64). ? Above fair: trim it, or is it a carry since it outranges tanks? It has heavy armor, so it is not fragile. |
| Rhino | 400 (200) | Artillery | The longest gun in the game: lobbed shells from 50 m. | Slow, grouped units | Flankers, air, anything up close (listed: Hornet) | carry? | 1.43; bought 26%; slot -4 | Cannon 150 to 140 (D64). Fits a carry. |
| Grizzly | 500 (250) | Tank | The upgraded Scorpion: twin cannons and more armor. | Chaff clear, infantry | Single-target DPS (listed: Gauss Warthog) | front line | 0.87; bought 88%; slot +2 | At 450 the AI swapped its Scorpions for Grizzlies and lost them to Spartans, so it went back to 500 (D73). Item 119: "Balance left as it is." |

### Aerospace Command

| Unit | Price (unlock) | Role | Job | Should beat | Should lose to | Strength for its price | Measured now | Notes for the owner |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Sparrowhawk | 250 (50) | Aerial | Gunship that hunts vehicles; poor against infantry. | Vehicles; ground armies without anti-air | Anti-air (listed: Wolverine) | fair | 0.98; bought 38%; slot -17 | On target. |
| Longsword | 300 (50) | Aerial | Heavy bomber: it flies over its target, and can't fight aircraft. | Ground groups without anti-air | Anti-air (listed: Wolverine) | fair | 0.71; bought 77%; slot -19 | The owner played it: "fine as it is" (item 58, 2026-11-12). ? Under fair: raise it to fair, or keep it as the owner likes it? |
| Vulture | 400 (200) | Aerial | The heaviest gunship: heavy armor, slow, long-range missiles. | Ground armies without anti-air | Anti-air (listed: Wolverine) | fair | 0.44; bought 76%; slot -23 | The owner today: "Vulture should be better." Bought often for little (item 123 said the same). A tougher Vulture, tried with a tougher Abomination, once moved Aerospace by 10 points against the Flood (D74), so watch those matchups. |
| Condor Gunship | 500 (250) | Aerial | Gunship with twin autocannons and five light Spartan Lasers. | Ground armies without anti-air | Anti-air (listed: Wolverine) | fair | 0.91; bought 63%; slot -12 | The owner today on the Spartan (+10) against the Condor (-9): "a great example. 19 points of difference." Raise it toward fair. Its lasers were set low "so it isn't too strong" (D111). |

## Covenant

### Base roster

| Unit | Price (unlock) | Role | Job | Should beat | Should lose to | Strength for its price | Measured now | Notes for the owner |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Grunt | 100 (0) | Chaff | 20 Grunts with weak pistols: cheap bodies in front. | Single-target DPS | Chaff clear (listed: Flamethrower Trooper) | soak | 0.96; bought 56%; slot Banished +7, Lekgolo +11, Unggoy +12 | Takes 1.76 times its share of fire and also deals near fair damage, like the Marine. ? Same rule as the Marine? |
| Fuel Rod Grunt | 150 (0) | Chaff clear | Four Grunts with fuel rod guns: splash from 16 m. | Chaff, infantry | Tanks, flankers (listed: Scorpion) | fair? | 2.49; bought 77%; slot Banished +11, Lekgolo +10, Unggoy +13 | ? The highest ratio in the game, yet its win edge is near zero (-5 to +6). Low health and long range, like the Pure Form Ranged. A carry, or held to fair? See the Flamethrower Trooper. |
| Jackal (Beam Rifle) | 175 (0) | Single-target DPS | Four snipers: slow, hard shots from 24 m, little to stop a rush. | Tanks, heavy units, aircraft | Chaff, flankers (listed: Marine) | carry? | 1.26; bought 42%; slot Banished +6, Lekgolo +6, Unggoy +2 | The owner asked for snipers (D72); at 45 damage and 150 they carried Banished to 65%. Item 117: "Balance left as it is." Its worth is behind a screen: one on one it beats few tanks. |
| Jackal (Shield) | 150 (0) | Tank | Six Jackals whose front shields block 80% of bullets. | Light chaff, bullet-firing infantry | Single-target DPS (listed: Jackal (Beam Rifle)) | soak | 0.21; bought 45%; slot Banished -10, Lekgolo -12, Unggoy -12 | Low damage is expected from a blocker. But it takes only 0.46 of its share of fire after blocking about 55% of what is aimed at it. ? Is it doing its job, or should it be tougher? |
| Ghost | 200 (0) | Flanker | Fast hover bike that goes for the back line. | Artillery, back-line DPS | Chaff clear, tanks (listed: Flamethrower Trooper) | fair | 0.31; bought 28%; slot Banished -24, Lekgolo -17, Unggoy -24 | The weakest flanker. Item 123: still weak, left as it is because buffs made the AI overbuy it. ? See the Warthog. |
| Elite | 300 (50) | Single-target DPS | Three shielded Elites with plasma rifles. | Tanks, shielded units | Chaff, flankers (listed: Marine) | fair | 0.58; bought 32%; slot Banished -13, Lekgolo -5, Unggoy -12 | Item 123: still weak, left until the AI's table was settled. ? Raise it to fair now that balance goes by unit? |
| Wraith | 300 (50) | Artillery | Heavy hover tank with a lobbed plasma mortar, 38 m. | Slow, grouped units; infantry | Flankers, air (listed: Hornet) | carry? | 0.99; bought 55%; slot Banished -15, Lekgolo -16, Unggoy -25 | ? Tough for artillery (900 health, heavy armor). A carry like other artillery, or fair because it is armored? |
| Vampire | 300 (50) | Anti-air | Flying gunship that hunts aircraft. | Aircraft | Ground units that hit air (listed: Marine) | vs air | 0.72; bought 30%; slot Banished -15, Lekgolo -10, Unggoy -23 | It flies (D22). Judge it against air only. |
| Banshee | 300 (50) | Aerial | Pair of fast attack craft with plasma and fuel rod bombs. | Ground armies without anti-air | Anti-air (listed: Wolverine) | fair | 1.08; bought 75%; slot Banished -10, Lekgolo -12, Unggoy -15 | On target. |
| Covenant Phantom | 300 (50) | Aerial | Dropship: drops 7 Grunts 5 s in, then behind enemy lines, up to 3 drops. | Back-line units, through its Grunts | Anti-air (listed: Wolverine) | with drops | 0.20; bought 13%; slot Banished -26, Lekgolo -21, Unggoy -28 | Already counts its Grunts. Item 116: "drops not changed, balance is tight." ? As the Pelican: hold it to fair, or accept it? |
| Hunter Pair | 450 (225) | Tank | Two Hunters: a wide front shield, assault cannons, a shield bash. | Chaff clear, infantry | Single-target DPS (listed: Spartan); hits from behind | front line | 0.92; bought 68%; slot Banished +10, Lekgolo +8, Unggoy +12 | On target. Takes 1.30 times its share of fire, the best of the tanks. |
| Zealot | 400 (200) | Single-target DPS | Fast, shielded sword Elite that leaps on its target and cuts all around it. | Tanks, Titans, heavy units | Chaff, flankers (listed: Marine) | fair? | 0.69; bought 57%; slot Banished -1, Lekgolo -4, Unggoy +1 | In D65 the owner asked for an impact of 1.5. It reached 0.82 then; it is 0.69 now. ? Is 1.5 still the target (that would make it a carry), or is fair enough? |
| Scarab | 800 (350) | Titan | Walking fortress: a focus beam and two plasma turrets. | Most things in a straight fight | Single-target DPS, big chaff swarms (listed: Spartan) | Titan | 0.95; bought 92%; slot Banished -0, Lekgolo -2, Unggoy -6 | The owner kept its values (beam 30, health 8,500; D60). ? Almost every army takes one, as with the Elephant. |

### Banished

| Unit | Price (unlock) | Role | Job | Should beat | Should lose to | Strength for its price | Measured now | Notes for the owner |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Chopper | 200 (0) | Flanker | Rams the enemy with its front wheel, guns firing on the way. | Artillery, back line | Chaff clear, tanks (listed: Flamethrower Trooper) | fair | 0.56; bought 40%; slot -20 | ? Flankers are under fair (see Warthog). |
| Brute Grenadier | 150 (0) | Chaff clear | Tough infantry with lobbed spike grenades and a blade for close in. | Chaff, infantry | Tanks, flankers (listed: Scorpion) | fair? | 1.94; bought 79%; slot +6 | Grenades 34 to 31 (D125). ? See the Flamethrower Trooper: allow splash units above fair? |
| Marauder | 300 (50) | Tank | The Banished battle tank, with a plasma cannon. | Chaff clear, infantry | Single-target DPS (listed: Gauss Warthog) | front line | 0.84; bought 84%; slot -6 | The owner found it nearly unkillable after a level or two (item 121). The AI's squads end near level 1 to 2 (1.9 on average). The slot test fights at the levels units reach, so it sees a level or two, not more. |
| Reaver | 300 (50) | Anti-air | Ground anti-air walker with spike rockets; weak against vehicles. | Aircraft | Ground units, vehicles (listed: Marine) | vs air | 0.88; bought 16%; slot -11 | Loses to almost all ground units, as meant. |

### Lekgolo Colony

| Unit | Price (unlock) | Role | Job | Should beat | Should lose to | Strength for its price | Measured now | Notes for the owner |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Skitterer | 125 (0) | Flanker | Fast, light swarm of spider-like machines. | Artillery, back line | Chaff clear (listed: Flamethrower Trooper) | fair | 0.90; bought 62%; slot -4 | Price 150 to 125 (D116). Near target. |
| Goliath | 500 (250) | Tank | Two colonies in one suit: a fist slam and a fuel rod cannon. | Chaff clear, infantry | Single-target DPS (listed: Gauss Warthog) | front line | 0.59; bought 38%; slot -8 | ? Under fair, and it takes only 0.73 of its share of fire. Its base damage of 250 (a deliberate exception) does not show in the ratio. |
| Hunter Captain | 450 (225) | Single-target DPS | Hunter whose siphon laser heals it; friends within 12 m heal too. | Tanks, heavy units | Chaff, flankers (listed: Marine); a Titan one on one | fair | 0.77; bought 54%; slot +1 | The owner today: "Captain is 500c, it shouldn't beat a titan one on one." ? The data says 450 (and D134 says 450). One on one it beats the Elephant and the Abomination and loses to the Scarab. It is the only unit that beats the Abomination at equal credits (D134). Its healing does not show in the ratio. |

### Unggoy Rebellion

| Unit | Price (unlock) | Role | Job | Should beat | Should lose to | Strength for its price | Measured now | Notes for the owner |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Cannon Fodder | 100 (0) | Chaff | 30 Grunts who barely fight: there to be shot instead of something else. | Single-target DPS, by wasting its shots | Chaff clear (listed: Flamethrower Trooper) | soak | 0.47; bought 43%; slot +1 | ? Its only job is to be shot, but it takes less fire than a Grunt squad (1.18 against 1.76 times its share) and has less health per credit (600 against 800). More health? |
| Heavy Grunts | 200 (0) | Single-target DPS | Eight armored Grunts with needle rifles and a small shield. | Tanks, heavy units (by its role) | Chaff, flankers (listed: Marine) | fair | 0.92; bought 76%; slot +7 | ? One on one it beats chaff and flankers (its counters) and loses to tanks (its targets): it plays as anti-infantry. Change its role, or its gun? |
| Grunt Goblin | 400 (200) | Tank | Quick mech tank; friends within 14 m move 20% faster and heal. | Chaff clear, infantry | Single-target DPS (listed: Gauss Warthog) | front line | 0.77; bought 97%; slot -0 | Its aura does not show in the ratio. D74 called it "dead weight"; its health has gone from 1,250 to 1,750 since. |

## Flood

### Base roster

| Unit | Price (unlock) | Role | Job | Should beat | Should lose to | Strength for its price | Measured now | Notes for the owner |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Infection Forms | 100 (0) | Chaff | Ten fast packs that leap on infantry; the dead rise as Flood. | Infantry, single-target DPS | Chaff clear (listed: Flamethrower Trooper) | soak | 0.59; bought 54%; slot Covenant-based -12, UNSC-based -14 | Takes 7.1 times its share of fire: the biggest sponge in the game. The ratio counts the bodies it infects. On target. |
| Carrier Form | 150 (0) | Chaff clear | Walking bomb: bursts on contact or death and releases 12 Infection Forms. | Chaff, grouped infantry | Tanks, flankers (listed: Hunter Pair) | with drops | 0.50; bought 83%; slot Covenant-based -11, UNSC-based -12 | Already counts its forms (D74) and is still half its worth, though bought in 83%. ? Under target, or fine? Burst 80 to 110 (D74), health 140 to 170 (D125). |
| Combat Form | 200 (50) | Single-target DPS | Six clawed fighters that leap in; anti-armor claws and scavenged rifles. | Tanks, armored units | Chaff, flankers (listed: Marine) | fair | 0.46; bought 28%; slot Covenant-based -16, UNSC-based -15 | The owner today: "Combat form should not be so bad." It takes 1.9 times its share of fire, like chaff, and its side wins 38% when it is fielded. Raise it to fair. Better Combat Forms also lift the Infected Phantom and the Spawner. |
| Stalker Form | 250 (50) | Flanker | Very fast pure form that hunts the back line. | Artillery, back-line DPS | Chaff clear, tanks (listed: Flamethrower Trooper) | fair | rarely bought; slot Covenant-based -25, UNSC-based -19 | ? Fielded in 3 of 2,800 Flood matches; item 158 noted it too. One on one it wins 22% on average. It needs a look of its own. |
| Pure Form Ranged | 250 (50) | Artillery | Fragile form that throws bone shards from 34 m. | Slow, grouped units; infantry | Flankers, air (listed: Hornet) | carry | 2.47; bought 25%; slot Covenant-based -12, UNSC-based +7 | The owner today: "it actually makes sense that it has a super high ratio, it is a low health high range carry after all." Its shards do a quarter of their damage to aircraft ("a quarter stays", item 167). |
| Swarm Form | 250 (50) | Anti-air | Flying packs that spit bone at aircraft, and bite ground units when no air is near. | Aircraft | Ground units that hit air (listed: Marine) | vs air | 0.89; bought 35%; slot Covenant-based -5, UNSC-based -7 | It goes for air first, as the owner meant (D76). |
| Pure Form Tank | 400 (200) | Tank | Big armored form with a splash slam. | Chaff clear, infantry | Single-target DPS (listed: Gauss Warthog) | front line | 0.81; bought 50%; slot Covenant-based +4, UNSC-based -6 | D74: "dead weight" in UNSC-based Flood, and buffs made the AI overbuy it. Item 123: left as it is. A little under target now. |
| Seeder | 475 (200) | Single-target DPS | Flying form that seeds enemies; what dies seeded rises as the Flood's own unit. | Units the Flood can convert, and aircraft | Anti-air, chaff (listed: Marine) | with drops | 1.08; bought 45%; slot Covenant-based -9, UNSC-based -4 | Counts its seed damage and the units it raises. Set by the owner's picks in D127 (475, spores 45, seeds eat only hosts the Flood can convert, and aircraft). Item 126: "Balance left as it is." |
| Infected Pelican | 400 (200) | Aerial | One-shot dive bomber: crashes into the enemy, 1,200 blast, releases forms. | Grouped infantry, clusters | Anti-air (listed: Vampire) | with drops | rarely bought; slot Covenant-based -14, UNSC-based -11 | The owner today: its health and blast were raised so it reliably crashes on the enemy (health 900 to 1,350, blast 900 to 1,200; D134). ? Fielded in about 1% of Flood matches, too few to judge; the slot test should measure it. |
| Infected Phantom | 400 (200) | Aerial | Flood dropship: drops 5 Combat Forms after 8 s. | Ground armies, through its Combat Forms | Anti-air (listed: Swarm Form) | with drops | 0.63; bought 79%; slot Covenant-based -19, UNSC-based -8 | Counts its Combat Forms. ? Bought in 79% of matches for under two-thirds of its worth. A better Combat Form lifts it too. |
| Flood Abomination | 800 (350) | Titan | Huge melee Titan that keeps releasing Infection Forms and heals Flood near it. | Most things; crowds | Single-target DPS, big chaff swarms (listed: Hunter Captain, at 70%) | Titan | 0.96; bought 89%; slot Covenant-based +12, UNSC-based +9 | The owner liked its slam and whip numbers (item 63: "Good."). No unit but the Hunter Captain beats it at equal credits; the owner kept the Captain listed (D134). ? If the Captain should lose to Titans, what should counter the Abomination? |

### UNSC-based

| Unit | Price (unlock) | Role | Job | Should beat | Should lose to | Strength for its price | Measured now | Notes for the owner |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Infested Marine | 100 (0) | Chaff | 12 tough Flood Marines that leap in with a scythe limb. | Single-target DPS, infantry | Chaff clear (listed: Flamethrower Trooper) | soak | 1.08; bought 98%; slot -6 | Takes 2.5 times its share of fire and deals fair damage, like the Marine. ? If the Marine gets worse, the same rule here? Bought share is from the UNSC-based table; the game-wide table shows 49% (see the note at the end). |
| Infested Warthog | 200 (0) | Flanker | Drives straight into the enemy, gun firing; releases forms when it dies. | Artillery, back line | Chaff clear, tanks (listed: Flamethrower Trooper) | fair | 0.73; bought 66%; slot -15 | ? Flankers are under fair (see Warthog). Bought share from the UNSC-based table (game-wide table: 33%). |
| Infested Scorpion | 275 (50) | Tank | A tougher, shorter-ranged Scorpion with tentacles. | Chaff clear, infantry | Single-target DPS (listed: Gauss Warthog) | front line | 1.28; bought 40%; slot -2 | Price 250 to 275 (D112), cannon 80 to 72 (D125). ? Above fair, though it takes only 0.76 of its share of fire. |
| Infested Hornet | 275 (50) | Aerial | A barely flying Hornet, still bombing. | Ground armies without anti-air | Anti-air (listed: Wolverine) | fair | 1.46; bought 73%; slot -3 | Trimmed three times (D79, D112, D125). ? Still above fair. |
| Juggernaut | 400 (200) | Flanker | Tank-sized pure form that moves like a flanker and leaps on its prey. | Artillery, DPS | Chaff clear, tanks (listed: Scorpion) | fair | 0.79; bought 27%; slot -6 | A little under target. |

### Covenant-based

| Unit | Price (unlock) | Role | Job | Should beat | Should lose to | Strength for its price | Measured now | Notes for the owner |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Infested Ghost | 200 (0) | Flanker | A faster, lighter rammer than the Infested Warthog. | Artillery, back line | Chaff clear, tanks (listed: Flamethrower Trooper) | fair | 0.95; bought 80%; slot -16 | The best of the cheap flankers. Health 270 to 330 (D125). Bought share from the Covenant-based table (game-wide table: 40%). |
| Infested Elite | 250 (50) | Single-target DPS | Unshielded Flood Elite with a claw that opens armor. | Tanks, armored units | Chaff, flankers (listed: Marine) | fair | 0.60; bought 27%; slot -23 | ? Under fair; its side wins 40% when it is fielded (16 points less than without). |
| Infested Wraith | 250 (50) | Artillery | A tougher, shorter-ranged Wraith. | Slow, grouped units | Flankers, air (listed: Hornet) | carry? | 1.75; bought 68%; slot -14 | ? Tougher (1,000 health) and cheaper (250) than the Wraith (900, 300), with almost the same mortar. Yet its side wins 13 points less with it. A carry, or trim it toward the Wraith? |
| Infested Banshee | 250 (50) | Aerial | A barely flying Banshee, still bombing. | Ground armies without anti-air | Anti-air (listed: Wolverine) | fair | 1.84; bought 93%; slot -8 | ? The highest aircraft ratio, bought in nearly every Covenant-based match, and cheaper (250) than the Banshee (300) with almost the same weapons. Trim it? Bought share from the Covenant-based table (game-wide table: 47%). |
| Spawner | 400 (200) | Tank | Slow birthing form: releases Infection Forms and Combat Forms every 4 s. | Single-target DPS (its brood screens it) | Chaff clear (listed: Flamethrower Trooper), aircraft | with drops | 0.74; bought 94%; slot +11 | Counts its brood. "Kill it first" (D79, D116). The owner kept it as it is and asked for 3,600 health (D125). ? Bought in 94% of matches for three-quarters of its worth. Since D136 its brood also earns it XP. |

## A note on the measurements

- The game-wide table counts a Flood unit as "could be bought" in the other Flood sub-faction too, when a Seeder raises it there. That halves the bought share of the Infested Marine, Warthog, Ghost and Banshee, so this sheet uses each one's own sub-faction table. Their ratios are not affected.
- The Stalker Form (3 of 2,800 Flood matches) and the Infected Pelican (28 of 2,800, a ratio near 0.7 on that small sample) are left out of the game-wide table, which needs 100 matches.
- One-on-one results here come from the counter console's current table (`Tools/Data/cw/matrix.csv`). `counter_web.md` is older than D133, so it still shows the Spartan CQB at 400 and shooting aircraft.

## Questions for the owner

1. **Chaff and tanks:** should chaff be judged mainly by the fire it takes, and tanks by damage plus fire taken? Then the Marine (fair damage and 2.5 times its share of fire) is too strong, as you said, and Cannon Fodder, which takes less fire than a Grunt squad, is too weak.
2. **Carries:** you said the Pure Form Ranged's high ratio makes sense. Does the same go for other fragile, long-range units (Spartan Sniper 1.61, Rhino 1.43, Jackal (Beam Rifle) 1.26, Infested Wraith 1.75)? If so, the Archer Missile Pod (0.87) and the Wraith (0.99) are under target.
3. **Splash units:** the Fuel Rod Grunt (2.49), Brute Grenadier (1.94) and Flamethrower Trooper (1.69) top the list, but their sides win about as often with them as without. Much of their damage goes into cheap chaff. Hold them to fair, or let the slot test judge them by wins?
4. **Flankers:** every flanker is under fair (Ghost 0.31 to Infested Ghost 0.95), and the Stalker Form is almost never bought. Bring them all up to fair, or keep them as they are (item 52)?
5. **Transports:** the Pelican (0.19) and the Covenant Phantom (0.20) are at a fifth of their worth, even counting their drops. Hold them to fair, or accept units that the AI uses badly?
6. **Zealot:** in D65 you asked for an impact of 1.5; it is 0.69 now. Is 1.5 still the target?
7. **Hunter Captain:** you said 500 credits, but the game has it at 450. Which is right? And if it should lose to a Titan one on one, which unit should counter the Abomination? Today nothing else beats it at equal credits.
8. **Spartan and Condor Gunship:** both fair at 500, closing the 19-point gap from both sides? Or should most of it come from one of them?
9. **Titans:** the AI buys one in 89% to 94% of matches. Is a Titan meant to be in every army?
10. **Effects the ratio can't see:** auras (Grunt Goblin, Hunter Captain, Abomination), the Gremlin's slow and the Goliath's base damage. Leave these to the slot test, which measures wins rather than damage?
11. **The band:** should every unit end within about ±5 points of margin in the slot test (the test is good to about ±4), except where its row says otherwise (transports, anti-air, units you want stronger)?
