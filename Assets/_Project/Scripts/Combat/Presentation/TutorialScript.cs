// TutorialScript.cs
// The tutorial's steps (D77), in order: what each says, what it points at and what moves it on. The lessons are in Docs/Tutorial/TUTORIAL_PLAN.md.
// Targets are read by TutorialHud: "key:..." is a HUD button by its key, "area:..." an area MatchHud names, "squad" the player's squads.
// Depends on: TutorialDirector, TutorialStep, TutorialSetup, MatchController, Halobellum.Economy, Halobellum.Core.

using System.Collections.Generic;
using Halobellum.Core;
using Halobellum.Economy;

namespace Halobellum.Presentation
{
    public static class TutorialScript
    {
        public static List<TutorialStep> Build(TutorialDirector t, MatchController match)
        {
            Match M() => match.Match;
            PlayerState Me() => match.Match.Player;
            bool Phase(MatchPhase phase) => match.Match != null && match.Match.Phase == phase;
            bool HasUnit(string name) => Me().Squads.Exists(s => s.Type.Name == name);
            bool Unlocked(string name) => Me().Roster.Exists(u => u.Name == name && Me().Unlocked.Contains(u));
            bool TowerOpen(TowerKind kind) => match.Selection != null && match.Selection.SelectedTower is (Team team, TowerKind k) && team == Team.A && k == kind;
            string shop = TutorialSetup.ShopUnit;

            return new List<TutorialStep>
            {
                // ---------------------------------------------------------------- round 1: the draft and deployment
                new TutorialStep
                {
                    Title = "Welcome to Halobellum",
                    Text = "Each round you buy squads and place them on your side of the field. Then the battle plays itself.\n" +
                           "Your squads that survive send nodes of light into the enemy base. The first base to fall loses the match.",
                },
                new TutorialStep
                {
                    Title = "Your starting card",
                    Text = "A starting card gives you a faction, a sub-faction and two free squads. In a match you choose from several: " +
                           "a cheaper card gives your base more health. Here there is one: UNSC Noble, with two squads of Marines.",
                    Target = "area:draft", Doing = "Press Choose on the card.",
                    Done = () => !Phase(MatchPhase.Draft),
                },
                new TutorialStep
                {
                    Title = "The camera",
                    Text = "Move the camera with W, A, S, D or the edges of the screen. Zoom with the mouse wheel. " +
                           "Turn and tilt it with the right mouse button held down.",
                },
                new TutorialStep
                {
                    Title = "The top bar",
                    Text = "Your credits, and what you will earn next round. Squads bought this round: you can buy 2 a round. " +
                           "Unlocks this round: 1. And both bases' health.",
                    Target = "area:topbar",
                },
                new TutorialStep
                {
                    Title = "The shop: unlocking",
                    Text = "Before you can buy a unit you unlock it, once, for the whole match: free for the first tier, credits for the others. " +
                           "Unlock the " + shop + ": its fire burns through infantry.",
                    Target = "key:Shop/Unlock/" + shop, Doing = "Press Unlock beside the " + shop + ".",
                    Done = () => Unlocked(shop),
                },
                new TutorialStep
                {
                    Title = "The shop: buying",
                    Text = "Now buy it. A new squad lands in the middle of your zone.",
                    Target = "key:Shop/Buy/" + shop, Doing = "Press Buy beside the " + shop + ".",
                    Done = () => HasUnit(shop),
                },
                new TutorialStep
                {
                    Title = "Selecting a squad",
                    Text = "Click one of your squads on the field.",
                    Target = "squad", Doing = "Click a squad.",
                    Done = () => match.SelectedPlacedSquad != null,
                },
                new TutorialStep
                {
                    Title = "Squad stats",
                    Text = "The window at the bottom left: health, armor, shield, speed, what it can hit, and each weapon's damage, " +
                           "rate of fire, range and damage type. Below them, its level, its experience and its techs.",
                    Target = "area:stats",
                },
                new TutorialStep
                {
                    Title = "Stats before buying",
                    Text = "Click a unit's name in the shop to see its stats before you buy it.",
                    Target = "area:shop", Doing = "Click a unit's name in the shop.",
                    Done = () => t.Saw("ShopUnit"),
                },
                new TutorialStep
                {
                    Title = "Moving a squad",
                    Text = "Select a squad and press M (or Move), then click where it goes. R turns it, Del (or Sell) sells it: " +
                           "for its full price until it has fought.",
                    Target = "squad", Doing = "Move a squad.",
                    Done = () => t.Did("PlaceAt"),
                },
                new TutorialStep
                {
                    Title = "Undo",
                    Text = "Changed your mind? Undo (or Ctrl+Z) takes back this round's actions one at a time, back to the round's card.",
                    Target = "key:Undo", Doing = "Press Undo.",
                    Done = () => t.Did("Undo"),
                },
                new TutorialStep
                {
                    Title = "The Command Center",
                    Text = "Your two towers stand at the back of your zone. Open the Command Center with the button above it.",
                    Target = "key:Tower/Command Center", Doing = "Open the Command Center.",
                    Done = () => TowerOpen(TowerKind.CommandCenter),
                },
                new TutorialStep
                {
                    Title = "What the Command Center does",
                    Text = "If it falls in a battle, your whole army is weakened for a while: level it up for more health and a longer shockwave " +
                           "against them. It sells, once a round: an extra squad slot, an oil zone (a spell you place once), Rapid Resupply " +
                           "(credits now, fewer next round) and Elite Recruitment (this round's new squads start at level 2).",
                    Target = "area:tower",
                },
                new TutorialStep
                {
                    Title = "The Research Center",
                    Text = "Now open the Research Center.",
                    Target = "key:Tower/Research Center", Doing = "Open the Research Center.",
                    Done = () => TowerOpen(TowerKind.ResearchCenter),
                },
                new TutorialStep
                {
                    Title = "What the Research Center does",
                    Text = "Research gives your whole army more health or more damage, step by step, for the rest of the match. " +
                           "It falls and weakens your army as the Command Center does.",
                    Target = "area:tower",
                },
                new TutorialStep
                {
                    Title = "Ready",
                    Text = "When your army is placed, press Ready and the battle begins.",
                    Target = "key:Ready", Doing = "Press Ready.", ReadyAllowed = true,
                    Done = () => !Phase(MatchPhase.Deployment) && !Phase(MatchPhase.Draft),
                },

                // ---------------------------------------------------------------- round 1: the battle
                new TutorialStep
                {
                    Title = "The battle",
                    Text = "It fights itself. Each unit goes for the nearest enemy it can hit. Space pauses it; 1, 2 and 3 play it at 1x, 2x and 4x. " +
                           "Watch your towers: if one falls, its side is weakened for a while.",
                    Doing = "Watch the battle to its end.",
                    Done = () => Phase(MatchPhase.Result) || Phase(MatchPhase.MatchOver) || M().Round >= 2,
                },
                new TutorialStep
                {
                    Title = "The round's result",
                    Text = "What each side's survivors did to the other's base, the income of the next round, and who did the most damage. " +
                           "Every squad comes back for the next round, at full health and where you placed it.",
                    Target = "key:Continue", Doing = "Press Continue.",
                    Done = () => M().Round >= 2 || Phase(MatchPhase.MatchOver),
                },

                // ---------------------------------------------------------------- round 2: cards, spells, levels
                new TutorialStep
                {
                    Title = "Cards",
                    Text = "From round 2, each round starts with 3 cards: spells, items, economy, buffs or training, and from round 4 " +
                           "reinforcements every other round. Skipping them pays a bonus instead. Take the spell.",
                    Target = "area:card0", Doing = "Take a card.",
                    Done = () => t.Did("ChooseCard") || t.Did("SkipCards") || !Phase(MatchPhase.Cards),
                },
                new TutorialStep
                {
                    Title = "Spells",
                    Text = "Your spells are on the left. Press one, then click the field: it is cast when the battle starts. " +
                           "Offensive spells go anywhere, defensive ones on your side. Each recharges for a few rounds.",
                    Target = "area:spells", Doing = "Press the spell, then click the enemy's side of the field.",
                    Done = () => t.Did("PlaceSpellAt") || t.Did("PlaceLineTowards") || Me().Spells.Count == 0,
                },
                new TutorialStep
                {
                    Title = "Levels",
                    Text = "Kills give a squad experience. When its bar is full it can level up, for half its price: more health and damage. " +
                           "An arrow over a squad shows it is ready. Level all levels every ready squad of a type at once.",
                    Target = "area:stats",
                },
                new TutorialStep
                {
                    Title = "Techs",
                    Text = "Under a squad's level are its techs: upgrades you buy once for every squad of that type, such as more armor or range.",
                    Target = "area:stats",
                },
                new TutorialStep
                {
                    Title = "Next battle",
                    Text = "Buy what you can afford, and press Ready.",
                    Target = "key:Ready", Doing = "Press Ready, and Continue after the battle.", ReadyAllowed = true,
                    Done = () => M().Round >= 3 || Phase(MatchPhase.MatchOver),
                },

                // ---------------------------------------------------------------- round 3: items, flank zones
                new TutorialStep
                {
                    Title = "Items",
                    Text = "Items go on a squad and help every body in it, for the whole match. Take the item.",
                    Target = "area:card0", Doing = "Take a card.",
                    Done = () => t.Did("ChooseCard") || t.Did("SkipCards") || !Phase(MatchPhase.Cards),
                },
                new TutorialStep
                {
                    Title = "Placing an item",
                    Text = "Drag the item from your inventory onto the hexagon over a squad. A squad carries one item.",
                    Target = "area:inventory", Doing = "Drag the item onto a squad's hexagon.",
                    Done = () => t.Did("PlaceItemOn") || Me().Inventory.Count == 0,
                },
                new TutorialStep
                {
                    Title = "Flank zones",
                    Text = "Some units, such as the Pelican and the gunships, can be placed in the zones beside the enemy's area. " +
                           "They arrive a little late, but behind the enemy's lines.",
                },
                new TutorialStep
                {
                    Title = "Finish them",
                    Text = "Keep buying, levelling and placing, and press Ready until the enemy base falls.",
                    Target = "key:Ready", Doing = "Win the match.", ReadyAllowed = true,
                    Done = () => Phase(MatchPhase.MatchOver),
                },

                // ---------------------------------------------------------------- the end
                new TutorialStep
                {
                    Title = "Battle stats",
                    Text = "Battle stats shows every battle of the match: the field from above as it began, and each squad's damage dealt, " +
                           "taken and stopped by armor and shields.",
                    Target = "key:Battle stats", Doing = "Open Battle stats.", ReadyAllowed = true,
                    Done = () => t.Saw("BattleStats"),
                },
                new TutorialStep
                {
                    Title = "That's the game",
                    Text = "You know the loop now. From the main menu: Faction mode for a full match, Unlock Skirmish where every unit can be " +
                           "unlocked, and the Sandbox to try any units against any others.",
                    ReadyAllowed = true,
                },
            };
        }
    }
}
