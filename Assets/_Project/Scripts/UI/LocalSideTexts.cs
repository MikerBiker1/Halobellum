// LocalSideTexts.cs
// The words of the result and match-over screens, from the local side's point of view (M11): "you" and "yours" are the side this screen plays
// (MatchController.LocalTeam: Team A in single-player, Team B for a LAN guest), "the enemy" is the other side. With local = Team A every text is
// exactly what the screens said before. Plain functions, so that tests can check them without a scene.
// hostResult (a LAN guest whose own battle ended differently, so the host's result was used): base damage, bases and income are the
// host's, but the survivors and kills are still this game's own battle, so the texts never mix the two without saying so.
// Depends on: Halobellum.Economy (Match, RoundSummary), Halobellum.Combat (BattleResult), Halobellum.Core (Team).

using System.Collections.Generic;
using System.Linq;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Economy;

namespace Halobellum.UI
{
    public static class LocalSideTexts
    {
        /// <summary>The round's big word: won, lost or drawn, for the local side.</summary>
        public static string RoundWord(Team? winner, Team local) => winner == null ? "MATCH DRAWN" : winner == local ? "MATCH WON" : "MATCH LOST";

        /// <summary>Both armies fell in this round's battle.</summary>
        public static bool BothFell(RoundSummary s) => s.Battle.TeamA.Survivors == 0 && s.Battle.TeamB.Survivors == 0;

        /// <summary>The line under the round's big word. With hostResult it never reads this game's own battle ("both armies fell").</summary>
        public static string RoundLine(RoundSummary s, Team local, bool hostResult = false) => !hostResult && BothFell(s)
            ? $"Round {s.Round}: both armies fell, and neither base was hit"
            : $"Round {s.Round}: you hit the enemy base for {s.BaseDamageBy(local)}, it hit yours for {s.BaseDamageBy(local.Opponent())}";

        public static string BaseDamageLine(RoundSummary s, Team local) =>
            $"Base damage: you dealt {s.BaseDamageBy(local)}, the enemy dealt {s.BaseDamageBy(local.Opponent())}";

        /// <summary>A side's base health after the round.</summary>
        public static int BaseHealthOf(RoundSummary s, Team team) => team == Team.A ? s.BaseHealthA : s.BaseHealthB;

        public static string BasesLine(RoundSummary s, Team local) => $"Bases: yours {BaseHealthOf(s, local)}, enemy {BaseHealthOf(s, local.Opponent())}";

        public static string SurvivorsLine(RoundSummary s, Team local, bool hostResult = false)
        {
            TeamResult own = s.Battle.For(local), other = s.Battle.For(local.Opponent());
            return (hostResult ? "In this game's battle, survivors: yours" : "Survivors: yours") + $" {own.Survivors} of {own.Deployed}, enemy {other.Survivors} of {other.Deployed}" + (s.Battle.TimedOut ? "  (time limit)" : "");
        }

        /// <summary>The note a LAN guest's result window shows when its own battle ended differently and the host's result is used.</summary>
        public const string HostResultNote = "Your game's battle ended differently; the host's result is used.";

        /// <summary>A heading over this game's own battle's lists (top damage, kills); with hostResult it says they are this game's.</summary>
        public static string BattleHeading(string heading, bool hostResult) => hostResult ? heading + " (this game's battle)" : heading;

        /// <summary>"Your" for the local side, "Enemy" for the other (top damage dealers, what killed what).</summary>
        public static string SideWord(Team team, Team local) => team == local ? "Your" : "Enemy";

        /// <summary>The local side's squads ready to level up, or null when there are none.</summary>
        public static string ReadyToLevelLine(RoundSummary s, Team local)
        {
            List<string> ready = s.SquadsReadyToLevel(local);
            return ready.Count == 0 ? null : "Ready to level up: " + string.Join(", ", ready.Distinct()) + " (select it next round)";
        }

        /// <summary>The result window's last line: a tie, and the local side's income.</summary>
        public static string ResultFooter(RoundSummary s, Team local) => (s.Tie ? "Exact tie: both bases are restored. " : "") + $"Income: +{s.IncomeFor(local)} credits for next round.";

        /// <summary>The local side won the match.</summary>
        public static bool WonGame(Match m, Team local) => m.Winner == local;

        public static string GameWord(Match m, Team local) => WonGame(m, local) ? "VICTORY" : "DEFEAT";

        public static string GameLine(Match m, Team local) => WonGame(m, local)
            ? $"You won the game in round {m.Round}: the enemy base ended at {m.Get(local.Opponent()).BaseHealth}"
            : $"You lost the game in round {m.Round}: your base ended at {m.Get(local).BaseHealth}";
    }
}
