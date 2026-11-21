// TopSquadGlowSceneTests.cs
// Plays the real Match scene for the glow under the top squads: after a battle, the player's squad and the enemy's squad that dealt
// the most damage glow (blue and red); both glows stay on the same squads through the next deployment (the enemy's on its grey
// ghost, D25), and both go the moment the next battle starts.
// Depends on: Halobellum.Presentation, Halobellum.Economy, Halobellum.Core, NUnit, Unity Test Framework.

using System.Collections;
using System.Linq;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Halobellum.Tests
{
    public class TopSquadGlowSceneTests
    {
        private MatchController _match;
        private BattleController _battle;

        private IEnumerator LoadMatch(int seed = 3)
        {
            yield return SceneManager.LoadSceneAsync("Match", LoadSceneMode.Single);
            yield return null;
            _match = Object.FindAnyObjectByType<MatchController>();
            _battle = Object.FindAnyObjectByType<BattleController>();
            Assert.IsNotNull(_match, "The Match scene has no MatchController.");
            _match.NewMatch(seed);
            yield return null;
        }

        private IEnumerator PlayRound()
        {
            _battle.Speed = 16f;
            _match.Ready();
            float deadline = Time.realtimeSinceStartup + 120f;
            while (_match.Match.Phase == MatchPhase.Battle && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreNotEqual(MatchPhase.Battle, _match.Match.Phase, "The battle did not finish in time.");
        }

        /// <summary>Leaves the result screen and skips the card offer if there is one.</summary>
        private void ContinueAndSkipCards()
        {
            _match.Continue();
            if (_match.Match.Phase == MatchPhase.Cards) _match.SkipCards();
        }

        private static bool AnyAlive(BattleSimulation sim, int squadId) => squadId >= 0 && sim.Units.Any(u => u.IsAlive && u.SquadId == squadId);

        [UnityTest]
        public IEnumerator TheTopSquads_Glow_StayThroughTheNextDeployment_AndResetWhenTheNextBattleStarts()
        {
            yield return LoadMatch();
            _match.ChooseDraft(0);
            yield return null;
            yield return PlayRound();
            yield return null;
            Match m = _match.Match;
            Assert.AreEqual(MatchPhase.Result, m.Phase);

            TopSquadGlow glow = _battle.GetComponent<TopSquadGlow>();
            Assert.IsNotNull(glow, "The Match scene adds the glow.");

            BattleStats last = m.BattleHistory[m.BattleHistory.Count - 1];
            bool blueDealt = last.Lines.Any(l => l.Team == Team.A && l.Kind == BattleStatsKind.Squad && l.DamageDealt > 0f);
            bool redDealt = last.Lines.Any(l => l.Team == Team.B && l.Kind == BattleStatsKind.Squad && l.DamageDealt > 0f);
            Assume.That(blueDealt || redDealt, "Nobody dealt damage in this battle.");
            if (blueDealt)
            {
                Assert.GreaterOrEqual(glow.BlueSquadId, 0, "The player's top squad glows blue on the result screen.");
                Assert.IsTrue(m.Player.Squads.Any(s => s.Id == glow.BluePlacedId), "It is remembered as one of the player's squads.");
            }
            if (redDealt)
            {
                Assert.GreaterOrEqual(glow.RedSquadId, 0, "The enemy's top squad glows red on the result screen.");
                Assert.IsTrue(m.SeenArmy(Team.B).Contains(glow.RedGhost), "It is remembered as the enemy's squad as seen at the battle start.");
            }
            if (AnyAlive(_battle.Simulation, glow.BlueSquadId) || AnyAlive(_battle.Simulation, glow.RedSquadId))
                Assert.Greater(glow.Shown, 0, "A leader with bodies standing glows under them.");

            int bluePlaced = glow.BluePlacedId;
            SeenSquad ghost = glow.RedGhost;
            ContinueAndSkipCards();
            yield return null;
            Assert.AreEqual(MatchPhase.Deployment, m.Phase);
            BattleSimulation preview = _battle.Simulation;
            Assert.AreNotSame(m.CurrentBattle, preview);
            if (bluePlaced >= 0)
            {
                int index = m.Player.Squads.FindIndex(s => s.Id == bluePlaced);
                Assert.GreaterOrEqual(index, 0);
                Assert.AreEqual(preview.Squads[index].Id, glow.BlueSquadId, "The same squad of the player's still glows blue in deployment.");
                Assert.AreEqual(bluePlaced, glow.BluePlacedId);
            }
            if (ghost != null)
            {
                int index = m.Player.Squads.Count + m.SeenArmy(Team.B).ToList().IndexOf(ghost);
                Assert.AreEqual(preview.Squads[index].Id, glow.RedSquadId, "The enemy's grey ghost of that squad glows red in deployment.");
                Assert.AreEqual(Team.B, preview.Squads[index].Team);
            }
            if (bluePlaced >= 0 || ghost != null) Assert.Greater(glow.Shown, 0, "The glows show in deployment.");

            // The next battle starts (paused, so nothing can land yet): last battle's glows go.
            _battle.Speed = 0f;
            _match.Ready();
            yield return null;
            Assert.AreEqual(MatchPhase.Battle, m.Phase);
            Assert.AreEqual(0, _battle.Simulation.Tick);
            Assert.AreEqual(-1, glow.BlueSquadId);
            Assert.AreEqual(-1, glow.RedSquadId);
            Assert.AreEqual(-1, glow.BluePlacedId);
            Assert.IsNull(glow.RedGhost);
            Assert.AreEqual(0, glow.Shown);
            _battle.Speed = 16f;
        }
    }
}
