// BattleClock.cs
// Turns real frame time into fixed simulation ticks for the battle scene. Each frame it adds the
// frame's time (times the battle speed: 0 = paused, 1x, 2x, 4x) and says how many whole ticks to
// run. The simulation itself only ever moves in whole fixed ticks, so the battle plays out the same
// at any frame rate or speed, exactly like the headless runner.
// If the PC cannot keep up, at most MaxTicksPerFrame ticks run per frame and the rest is dropped
// (the battle slows down instead of freezing).
// Alpha says how far the next tick is, from 0 to 1, so visuals can move smoothly between ticks.
// Depends on: nothing (plain C#).

using System;

namespace Halobellum.Combat
{
    public sealed class BattleClock
    {
        private readonly float _tickSeconds;
        private float _accumulator;

        public BattleClock(float tickSeconds, int maxTicksPerFrame)
        {
            if (tickSeconds <= 0f) throw new ArgumentException("Tick length must be above 0.", nameof(tickSeconds));
            _tickSeconds = tickSeconds;
            MaxTicksPerFrame = Math.Max(1, maxTicksPerFrame);
        }

        /// <summary>Battle speed multiplier. 0 pauses the battle.</summary>
        public float Speed { get; set; } = 1f;
        public int MaxTicksPerFrame { get; }
        public bool IsPaused => Speed <= 0f;

        /// <summary>Progress toward the next tick, from 0 to 1.</summary>
        public float Alpha => Math.Clamp(_accumulator / _tickSeconds, 0f, 1f);

        /// <summary>Adds one frame's real time and returns how many ticks to simulate this frame.</summary>
        public int Advance(float frameSeconds)
        {
            if (IsPaused || frameSeconds <= 0f) return 0;

            _accumulator += frameSeconds * Speed;
            int ticks = (int)(_accumulator / _tickSeconds);
            if (ticks > MaxTicksPerFrame)
            {
                ticks = MaxTicksPerFrame;
                _accumulator = 0f; // Too far behind: drop the backlog rather than trying to catch up.
            }
            else
            {
                _accumulator -= ticks * _tickSeconds;
            }
            return ticks;
        }
    }
}
