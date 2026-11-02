// VoiceLimiter.cs
// Keeps 200 rifles from deafening the player or costing much: each group of sounds has a most it may play at once and a
// shortest gap between two starts. A shot asked for over that limit is simply not played. Plain C#, so it can be tested.
// Depends on: nothing.

using System;

namespace Halobellum.Presentation
{
    public sealed class VoiceLimiter
    {
        private readonly int[] _max;
        private readonly float[] _gap;
        private readonly int[] _active;
        private readonly float[] _lastStart;

        public VoiceLimiter(int groups)
        {
            _max = new int[groups];
            _gap = new float[groups];
            _active = new int[groups];
            _lastStart = new float[groups];
            for (int i = 0; i < groups; i++)
            {
                _max[i] = int.MaxValue;
                _lastStart[i] = float.NegativeInfinity;
            }
        }

        public void SetLimit(int group, int maxVoices, float minGapSeconds)
        {
            _max[group] = Math.Max(1, maxVoices);
            _gap[group] = Math.Max(0f, minGapSeconds);
        }

        public int ActiveIn(int group) => _active[group];

        /// <summary>True if a sound of this group may start now; it then counts as playing until Release.</summary>
        public bool TryStart(int group, float now)
        {
            if (_active[group] >= _max[group] || now - _lastStart[group] < _gap[group]) return false;
            _active[group]++;
            _lastStart[group] = now;
            return true;
        }

        public void Release(int group)
        {
            if (_active[group] > 0) _active[group]--;
        }

        public void Clear()
        {
            for (int i = 0; i < _active.Length; i++)
            {
                _active[i] = 0;
                _lastStart[i] = float.NegativeInfinity;
            }
        }
    }
}
