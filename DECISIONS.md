# Decisions and proposals

Agents record here any design question, interpretation, or proposed change that needs the human's approval (Bible Section 1). Nothing here changes the design until the human approves it; approved items move into the Bible's decision log.

| # | Date | Status | Topic | Question or proposal |
| --- | --- | --- | --- | --- |
| D1 | 2026-10-31 | Needs approval | Anti-armor tag | The Bible lists "Anti-armor" as a row of the damage table. Implemented as: a weapon with the anti-armor tag uses the anti-armor row **instead of** its damage type row (a ballistic anti-armor gun does 1.5x to Heavy, not 0.25 x 1.5). Alternative: multiply both rows together. One flag in `DamageCalculator.ArmorMultiplier` to change. |
| D2 | 2026-10-31 | Needs approval | Damage to energy shields | Not specified. Implemented as: the shield is an extra pool absorbed before health, and the hit uses the unit's **base armor type** multiplier for both. Alternatives: shields take a flat 1.0x, or shields get their own row in the damage table (for example Energy strong vs shields, as in Halo). |
| D3 | 2026-10-31 | Needs approval | Shield regen rate | The Bible says shields regenerate after 5 seconds without damage but gives no rate. Added a per-unit data field `shieldRegenPerSecond` (the 5 second delay is in `CombatRules.asset`). Values to be set when shielded units (Spartan, Elite, Zealot) are built in M4. |
| D4 | 2026-10-31 | Needs approval | Arena size | Placeholder `120 m wide x 160 m long` (each half 80 m deep) in `Data/CombatRules.asset`. Chosen so a range-12 Marine line and long-range artillery both fit; please compare with Mechabellum in playtests (M2/M3). |
