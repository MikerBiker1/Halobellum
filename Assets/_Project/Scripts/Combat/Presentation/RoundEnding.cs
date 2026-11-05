// RoundEnding.cs
// The end of a round, once the fight is over and the last shot has landed: a node of light leaves every surviving unit and flies to the enemy's base health, slowly at first and
// then fast. Each one that arrives takes its unit's damage off that health, with a hit and a fading number that grow with the unit. A second after the last hit, the round's window opens.
// This only shows what the battle already decided. MatchController runs it and MatchHud draws it. Plain C#, so that it can be tested without a scene.
// Depends on: Halobellum.Combat (BattleSimulation, SimUnit), Halobellum.Core (Team), UnityEngine (Vector3, Mathf).

using System;
using System.Collections.Generic;
using System.Globalization;
using Halobellum.Combat;
using Halobellum.Core;
using UnityEngine;

namespace Halobellum.Presentation
{
    public sealed class RoundEnding
    {
        /// <summary>How long a node takes to reach the base, with the biggest taking longer than the smallest.</summary>
        public const float NodeSeconds = 0.9f, BigNodeExtraSeconds = 0.3f;
        /// <summary>Nodes leave within this time of each other, so that a crowd arrives as a run of hits.</summary>
        public const float MostStagger = 0.7f;
        /// <summary>From the last hit to the window.</summary>
        public const float PauseSeconds = 1f;
        public const float NumberSeconds = 1.1f;
        /// <summary>How long the base's bar shakes and flashes after a hit.</summary>
        public const float KickSeconds = 0.35f;
        /// <summary>The biggest unit's damage to a base, which makes the biggest node.</summary>
        private const float BiggestDamage = 250f;

        public sealed class Node
        {
            public Vector3 From;
            public Team Source;
            public float Damage;
            /// <summary>0 for the smallest unit, 1 for the biggest.</summary>
            public float Size;
            public float Starts;
            public float Seconds;
            public bool Landed;
            /// <summary>The team whose base it flies to.</summary>
            public Team Target => Source == Team.A ? Team.B : Team.A;
        }

        public sealed class Number
        {
            public Team Target;
            public float Size;
            public string Text;
            public float Born;
            /// <summary>-1 to 1: where along the bar it comes out.</summary>
            public float Along;
        }

        private readonly List<Node> _nodes = new List<Node>();
        private readonly List<Number> _numbers = new List<Number>();
        private readonly float[] _landed = new float[2];
        private readonly float[] _kick = new float[2];
        private readonly float[] _kickAt = { -10f, -10f };
        private readonly System.Random _random;
        private float _lastHit;
        private int _left;

        public float Clock { get; private set; }
        public IReadOnlyList<Node> Nodes => _nodes;
        public IReadOnlyList<Number> Numbers => _numbers;
        /// <summary>Raised as a node reaches a base.</summary>
        public event Action<Node> Hit;

        /// <summary>True once every node has landed and the pause after them has passed.</summary>
        public bool IsDone => _left == 0 && Clock >= _lastHit + PauseSeconds;

        /// <param name="placeOf">Where a unit is in the world.</param>
        public RoundEnding(BattleSimulation battle, Func<SimUnit, Vector3> placeOf, int seed = 0)
        {
            _random = new System.Random(seed);
            IReadOnlyList<SimUnit> units = battle.Units;
            for (int i = 0; i < units.Count; i++)
            {
                SimUnit unit = units[i];
                if (!unit.IsAlive || unit.Stats.IsTower || unit.Stats.BaseDamage <= 0f) continue; // The same survivors that the round's result counts.
                float size = SizeOf(unit.Stats.BaseDamage);
                _nodes.Add(new Node
                {
                    From = placeOf(unit),
                    Source = unit.Team,
                    Damage = unit.Stats.BaseDamage,
                    Size = size,
                    Starts = (float)_random.NextDouble() * MostStagger,
                    Seconds = NodeSeconds + BigNodeExtraSeconds * size,
                });
            }
            _left = _nodes.Count;
        }

        /// <summary>From a unit's damage to a base to 0 to 1. By its logarithm, as they run from about 1 for a Marine to 250: a tenth of the biggest is still a good hit.</summary>
        public static float SizeOf(float damage) => Mathf.Clamp01(Mathf.Log(Mathf.Max(1f, damage)) / Mathf.Log(BiggestDamage));

        /// <summary>How far along its way a node is, for 0 to 1 of its time: slow to start, then gathering speed fast.</summary>
        public static float Ease(float time)
        {
            time = Mathf.Clamp01(time);
            return time * time * time * time;
        }

        /// <summary>The damage shown as a number: whole, or with one decimal if it has one.</summary>
        public static string HitText(float damage)
        {
            float rounded = Mathf.Round(damage);
            return "-" + (Mathf.Abs(damage - rounded) < 0.05f ? rounded.ToString("0", CultureInfo.InvariantCulture) : damage.ToString("0.0", CultureInfo.InvariantCulture));
        }

        /// <summary>How much of a team's base has been hit so far, rounded as the round's result rounds it.</summary>
        public int HitsTaken(Team team) => (int)MathF.Round(_landed[team == Team.A ? 0 : 1]);

        /// <summary>0 to 1: how hard this team's bar is shaking and flashing right now.</summary>
        public float Kick(Team team)
        {
            int index = team == Team.A ? 0 : 1;
            float left = 1f - (Clock - _kickAt[index]) / KickSeconds;
            return left <= 0f ? 0f : left * _kick[index];
        }

        public void Tick(float seconds)
        {
            Clock += seconds;
            if (_left > 0)
            {
                for (int i = 0; i < _nodes.Count; i++)
                {
                    Node node = _nodes[i];
                    if (!node.Landed && Clock >= node.Starts + node.Seconds) Land(node);
                }
            }
            _numbers.RemoveAll(number => Clock - number.Born >= NumberSeconds);
        }

        private void Land(Node node)
        {
            node.Landed = true;
            _left--;
            _lastHit = Mathf.Max(_lastHit, Clock);
            int index = node.Target == Team.A ? 0 : 1;
            _landed[index] += node.Damage;
            _kick[index] = Mathf.Max(Kick(node.Target), 0.3f + 0.7f * node.Size);
            _kickAt[index] = Clock;
            _numbers.Add(new Number
            {
                Target = node.Target,
                Size = node.Size,
                Text = HitText(node.Damage),
                Born = Clock,
                Along = (float)_random.NextDouble() * 2f - 1f,
            });
            Hit?.Invoke(node);
        }
    }
}
