# Turns a tournament's results file (Program.cs) into the text report. A win rate from n matches is good to about 1 / sqrt(n) either way (95% of the time):
# with 100 matches, about 10 points; with 700, about 4. Differences smaller than that may be luck, and the report says how big they must be.
# Usage: python report.py <results.jsonl> <report.txt> [title]
import collections, json, math, sys

results_path, report_path = sys.argv[1], sys.argv[2]
title = sys.argv[3] if len(sys.argv) > 3 else "Hard AI tournament"
matches = [json.loads(line) for line in open(results_path, encoding="utf-8") if line.strip()]
out = []


def line(text=""):
    out.append(text)


def pct(wins, games):
    return f"{100 * wins / games:.0f}%" if games else "-"


def margin(games):
    return 100 * 1.96 * 0.5 / math.sqrt(games) if games else 0


def heading(text):
    line()
    line(text)
    line("=" * len(text))


def sides(m):
    """Each sub-faction's side of the match: (sub, faction, side data, won?), in the order a, b."""
    a_side = m["sideA"] if m["aOnTeam"] == 1 else m["sideB"]
    b_side = m["sideB"] if m["aOnTeam"] == 1 else m["sideA"]
    return [(m["a"], m["fa"], a_side, m["winner"] == "a"), (m["b"], m["fb"], b_side, m["winner"] == "b")]


subs = sorted({m["a"] for m in matches} | {m["b"] for m in matches})
faction_of = {}
for m in matches:
    faction_of[m["a"]], faction_of[m["b"]] = m["fa"], m["fb"]
others = [m for m in matches if m["a"] != m["b"]]
mirrors = [m for m in matches if m["a"] == m["b"]]
unfinished = sum(1 for m in matches if m["winner"] == "none")

line(title)
line("=" * len(title))
line(f"{len(matches)} matches between two Hard AIs: {len(others)} between different sub-factions, {len(mirrors)} of a sub-faction against itself.")
line("Whole matches under the game's real rules, with towers, cards, techs and spells. Half of each pairing is played with the sides swapped.")
line("Each side is dealt a random starting card for its sub-faction (250 to 350, with base health to match).")
line(f"Matches with no winner after 60 rounds: {unfinished}.")
line("This measures two Hard AIs, not good players: a unit the AI uses badly looks weak here. How close numbers must be to mean nothing is given in each table.")

# ------------------------------------------------------------------ overall
heading("Win rate by sub-faction (against the other sub-factions)")
wins, games, rounds = collections.Counter(), collections.Counter(), collections.Counter()
for m in others:
    for sub, _, _, won in sides(m):
        games[sub] += 1
        wins[sub] += won
        rounds[sub] += m["rounds"]
ranked = sorted(subs, key=lambda s: -wins[s] / max(1, games[s]))
if others:
    line(f"Good to about {margin(games[subs[0]]):.0f} points either way.")
for sub in ranked:
    line(f"  {sub:<20} {faction_of[sub]:<9} {pct(wins[sub], games[sub]):>4}   {wins[sub]:>3} of {games[sub]:<4} {rounds[sub] / max(1, games[sub]):.1f} rounds on average")

heading("Win rate by faction (against the other factions)")
fwins, fgames = collections.Counter(), collections.Counter()
for m in others:
    if m["fa"] == m["fb"]:
        continue
    for _, faction, _, won in sides(m):
        fgames[faction] += 1
        fwins[faction] += won
for faction in sorted(fgames, key=lambda f: -fwins[f] / fgames[f]):
    line(f"  {faction:<9} {pct(fwins[faction], fgames[faction]):>4}   {fwins[faction]} of {fgames[faction]}")

heading("Each sub-faction against each other (the row's win rate)")
pair_wins, pair_games = collections.Counter(), collections.Counter()
for m in matches:
    if m["winner"] == "none":
        continue
    pair_games[(m["a"], m["b"])] += 1
    pair_games[(m["b"], m["a"])] += 1
    pair_wins[(m["a"], m["b"])] += m["winner"] == "a"
    pair_wins[(m["b"], m["a"])] += m["winner"] == "b"
one = next(iter(pair_games.values()), 0)
line(f"Good to about {margin(one):.0f} points either way. A sub-faction against itself is shown as -.")
short = {s: s[:9] for s in ranked}
line(" " * 22 + "".join(f"{short[s]:>11}" for s in ranked))
for row in ranked:
    cells = "".join(f"{'-' if row == col else pct(pair_wins[(row, col)], pair_games[(row, col)]):>11}" for col in ranked)
    line(f"  {row:<20}{cells}")

# ------------------------------------------------------------------ fairness of the sides and the cards
heading("Does the side matter?")
team_a = sum(1 for m in matches if m["winnerTeam"] == "A")
team_b = sum(1 for m in matches if m["winnerTeam"] == "B")
line(f"All matches: Team A wins {team_a}, Team B wins {team_b}: {pct(team_a, team_a + team_b)} for Team A (good to about {margin(team_a + team_b):.0f} points; it should be 50%).")
ma = sum(1 for m in mirrors if m["winnerTeam"] == "A")
line(f"Sub-faction against itself: Team A wins {ma} of {len(mirrors)}: {pct(ma, len(mirrors))} (good to about {margin(len(mirrors)):.0f} points).")

heading("Starting cards: does the base health make up for the value?")
vw, vg = collections.Counter(), collections.Counter()
diff_w, diff_g = collections.Counter(), collections.Counter()
for m in matches:
    if m["winner"] == "none":
        continue
    a, b = sides(m)[0], sides(m)[1]
    for me, you in ((a, b), (b, a)):
        vg[me[2]["value"]] += 1
        vw[me[2]["value"]] += me[3]
        d = me[2]["value"] - you[2]["value"]
        diff_g[d] += 1
        diff_w[d] += me[3]
for value in sorted(vg):
    line(f"  Card worth {value}: wins {pct(vw[value], vg[value]):>4} of {vg[value]} (good to about {margin(vg[value]):.0f} points)")
line("  By how much dearer a side's card is than the other's:")
for d in sorted(diff_g):
    line(f"    {d:+5} credits: wins {pct(diff_w[d], diff_g[d]):>4} of {diff_g[d]}")

heading("Opening squads (at least 40 matches)")
ow, og = collections.Counter(), collections.Counter()
for m in matches:
    if m["winner"] == "none":
        continue
    for sub, _, side, won in sides(m):
        key = (sub, side["opening"])
        og[key] += 1
        ow[key] += won
rows = [(ow[k] / og[k], k) for k in og if og[k] >= 40]
for rate, (sub, opening) in sorted(rows, reverse=True)[:12]:
    line(f"  {100 * rate:3.0f}%  {sub}: {opening} ({og[(sub, opening)]})")
line("  ...")
for rate, (sub, opening) in sorted(rows, reverse=True)[-12:]:
    line(f"  {100 * rate:3.0f}%  {sub}: {opening} ({og[(sub, opening)]})")

heading("How matches go")
all_rounds = sorted(m["rounds"] for m in matches)
line(f"Rounds: shortest {all_rounds[0]}, median {all_rounds[len(all_rounds) // 2]}, longest {all_rounds[-1]}.")
histogram = collections.Counter(all_rounds)
line("  " + "  ".join(f"{r}: {histogram[r]}" for r in sorted(histogram)))
first = [m for m in matches if m["firstBattle"] != 0 and m["winner"] != "none"]
firsts = sum(1 for m in first if m["winnerTeam"] == ("A" if m["firstBattle"] == 1 else "B"))
line(f"The side that does more base damage in round 1 goes on to win {pct(firsts, len(first))} of matches ({firsts} of {len(first)}).")
margins = [max(s[2]["end"] for s in sides(m)) for m in matches if m["winner"] != "none"]
line(f"The winner's base at the end: {sum(margins) / len(margins):.0f} on average.")

# ------------------------------------------------------------------ towers and what the AI buys
heading("Towers")
falls = levels = sides_count = research = 0
level_counts = collections.Counter()
fewer_w = fewer_g = 0
for m in matches:
    a, b = sides(m)
    for me, you in ((a, b), (b, a)):
        my_falls = sum(t[1] for t in me[2]["towers"])
        your_falls = sum(t[1] for t in you[2]["towers"])
        sides_count += 1
        falls += my_falls / max(1, m["rounds"])
        research += me[2]["research"]
        for t in me[2]["towers"]:
            level_counts[t[0]] += 1
        if my_falls < your_falls:
            fewer_g += 1
            fewer_w += me[3]
line(f"A side loses {falls / sides_count:.2f} towers per round, of its 2.")
line("Tower levels at the end of a match: " + ", ".join(f"level {k}: {level_counts[k]}" for k in sorted(level_counts)))
line(f"Research steps bought per match, by a side: {research / sides_count:.2f}.")
line(f"The side whose towers fell less often wins {pct(fewer_w, fewer_g)} of the matches where they differ ({fewer_w} of {fewer_g}).")
credits_left = [s[2]["credits"] for m in matches for s in sides(m)]
line(f"Credits unspent when the match ends: {sum(credits_left) / len(credits_left):.0f} on average.")

heading("Units")
line("Fielded: the share of a side's matches, among sub-factions that can buy it, in which it ever fought.")
line("Win when fielded: the side's win rate in those matches. Edge: that, minus the same sub-faction's win rate without it. High edge can also mean the side was ahead, as winners last longer and can afford more.")
line("Damage per 100 credits: damage dealt per round fought per 100 credits of squad. Kills are bodies, per round fought per squad.")
access_games = collections.Counter()
games_by_sub = collections.Counter()
wins_by_sub = collections.Counter()
for m in matches:
    if m["winner"] == "none":
        continue
    for sub, _, _, won in sides(m):
        games_by_sub[sub] += 1
        wins_by_sub[sub] += won
unit = collections.defaultdict(lambda: dict(field=0, wins=0, sr=0, credits=0, damage=0, kills=0, squads=0, levels=0, by=collections.Counter(), bw=collections.Counter()))
for m in matches:
    if m["winner"] == "none":
        continue
    for sub, _, side, won in sides(m):
        for name, (sr, cr, dmg, kills, end_squads, end_levels) in side["units"].items():
            u = unit[name]
            if sr == 0:
                continue  # Dropped by something else, never bought.
            u["field"] += 1
            u["wins"] += won
            u["sr"] += sr
            u["credits"] += cr
            u["damage"] += dmg
            u["kills"] += kills
            u["squads"] += end_squads
            u["levels"] += end_levels
            u["by"][sub] += 1
            u["bw"][sub] += won
rows = []
for name, u in unit.items():
    if u["field"] == 0:
        continue
    can = sum(games_by_sub[s] for s in u["by"])
    edge_sum = weight = 0
    for s, n in u["by"].items():
        without_games = games_by_sub[s] - n
        without_wins = wins_by_sub[s] - u["bw"][s]
        if without_games > 0:
            edge_sum += n * (u["bw"][s] / n - without_wins / without_games)
            weight += n
    edge = 100 * edge_sum / weight if weight else None
    rows.append((name, u["field"] / can, u["wins"] / u["field"], edge, 100 * u["damage"] / max(1, u["credits"]), u["kills"] / max(1, u["sr"]), u["sr"] / u["field"], u["field"]))
line()
line(f"  {'Unit':<26}{'Fielded':>8}{'Win':>6}{'Edge':>7}{'Dmg/100':>9}{'Kills':>7}{'Rounds':>8}{'Matches':>9}")
for name, fielded, win, edge, damage, kills, squad_rounds, count in sorted(rows, key=lambda r: -r[1]):
    shown = f"{edge:+.0f}" if edge is not None else "-"
    line(f"  {name:<26}{100 * fielded:>7.0f}%{100 * win:>5.0f}%{shown:>7}{damage:>9.0f}{kills:>7.1f}{squad_rounds:>8.1f}{count:>9}")
never = None

heading("Cards the AI takes")
line("Edge: the side's win rate with the card, minus without it, for the same faction's matches. Late cards mostly go to sides that survive, so compare them with care.")
card_taken, card_wins = collections.Counter(), collections.Counter()
faction_games, faction_wins = collections.Counter(), collections.Counter()
for m in matches:
    if m["winner"] == "none":
        continue
    for sub, faction, side, won in sides(m):
        faction_games[faction] += 1
        faction_wins[faction] += won
        for name in set(side["cards"]):
            card_taken[(faction, name)] += 1
            card_wins[(faction, name)] += won
for faction in sorted(faction_games):
    line()
    line(f"  {faction}")
    keys = [k for k in card_taken if k[0] == faction]
    for key in sorted(keys, key=lambda k: -card_taken[k])[:24]:
        w, n = card_wins[key], card_taken[key]
        rest = faction_games[faction] - n
        without = (faction_wins[faction] - w) / rest if rest else 0
        line(f"    {key[1]:<34} taken in {100 * n / faction_games[faction]:>3.0f}% of matches   win {100 * w / n:>3.0f}%   edge {100 * (w / n - without):+.0f}")

open(report_path, "w", encoding="utf-8", newline="\n").write("\n".join(out) + "\n")
print("\n".join(out))
