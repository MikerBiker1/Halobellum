// WalkerLegs.cs
// A big walker's legs, the Scarab's (D84). Each foot stays planted on the ground while it carries weight, then steps forward in its turn: four legs
// a quarter of a cycle apart (rear left, front left, rear right, front right), one foot in the air at a time. The body moves its weight away from the
// foot in the air: it sways onto the legs that carry it, sinks a little while a foot is up, and tilts down over the legs carrying it.
// The cycle runs on how far the feet's places travel over the ground (walking, turning, being shoved), never on the clock, so a faster or slower
// walk only steps faster or slower and no planted foot skates. Stopping, a foot in the air finishes its step; standing, a foot left far from its
// place steps back to it. A foot that would stretch its leg past its reach (a quick turn on the spot, faster than it can step) slides instead.
// Each leg bends at the hip and the knee, solved in the plane it already bends in as made. The body stands a little lower than the model as made,
// and each foot a little nearer the middle, so that a leg has room to stretch: the Scarab's legs are nearly straight in its file.
// The model's nodes (Tools/Art/model_tools.py): Leg_<n>_<phase %> (the upper part, at the hip), its child Knee_<n> (the lower part, at the knee),
// and Foot_<n>, an empty where that foot meets the ground in the model as made, read once.
// Looks only: it reads where the unit is and changes nothing in the battle. It runs on battle time, as ModelParts does.
// Depends on: UnityEngine.

using System.Collections.Generic;
using UnityEngine;

namespace Halobellum.Presentation
{
    public sealed class WalkerLegs
    {
        // Lengths are shares of the legs' mean reach (hip to foot, stretched straight: about 7 m on the Scarab), so a walker of any size walks alike.
        /// <summary>Ground covered in one whole cycle, by the body and by each foot (each foot steps once): about 3 m on the Scarab.</summary>
        public const float StrideShare = 0.43f;
        private const float LiftShare = 0.1f;
        private const float CrouchShare = 0.09f;
        private const float TuckShare = 0.11f;
        /// <summary>How far the body leans away from a lifted foot, as a share of that foot's distance from the middle.</summary>
        private const float ShiftShare = 0.055f;
        private const float DipShare = 0.012f;
        private const float IdleBobShare = 0.004f;
        private const float RollDegrees = 1.6f;
        private const float PitchDegrees = 1.1f;
        /// <summary>The share of a cycle a foot is in the air.</summary>
        public const float SwingShare = 0.25f;
        /// <summary>The weight starts to move this share of a cycle before the foot lifts.</summary>
        private const float WeightLead = 0.06f;
        /// <summary>The fastest it steps (a quick turn), and the slowest a step in the air goes (walking slowly, finishing a step, settling).</summary>
        private const float MostCyclesPerSecond = 0.7f;
        private const float LeastCyclesPerSecond = 0.2f;
        private const float Eps = 1e-4f;
        /// <summary>A planted foot this much of the stride from its place steps back to it once the walker stands.</summary>
        private const float SettleShare = 0.2f;
        /// <summary>A planted foot never stretches its leg past this share of its reach, nor strays further than this share of the stride from its place.</summary>
        private const float ReachLimit = 0.97f;
        private const float StrayLimit = 0.75f;
        private const float TravelSmoothSeconds = 0.3f;

        private sealed class Leg
        {
            public Transform Hip, Knee;
            public Quaternion HipRest, KneeRest;
            /// <summary>Hip to knee, knee to foot, the way the knee bends and the plane's normal, in the model's own axes as made.</summary>
            public Vector3 ThighRest, ShinRest, BendRest, NormalRest;
            public Vector3 FootInKnee;
            public float Upper, Lower;
            /// <summary>Where the foot stands, in the unit's own axes, on the ground.</summary>
            public Vector3 Home;
            public float Phase, Side, End;
            public Vector3 Planted, From, Foot, LastHome, Travel, Moved;
            public bool Swinging;
        }

        private readonly Transform _root, _body;
        private readonly Leg[] _legs;
        private readonly Vector3 _restPosition;
        private readonly Quaternion _restRotation;
        private readonly float _stride, _lift, _crouch, _dip, _idleBob, _pivotHeight, _phase;
        /// <summary>Where the walk is in its cycle, how far the cycle has run ahead of the ground covered, and how much the body walks (0 to 1).</summary>
        private float _cycle, _lead, _walk;
        private bool _planted;
        private Vector3 _lastRootPosition;
        private Quaternion _lastRootRotation;

        private WalkerLegs(Transform root, Transform body, Leg[] legs, float reach, float pivotHeight, float phase)
        {
            _root = root;
            _body = body;
            _legs = legs;
            _restPosition = body.localPosition;
            _restRotation = body.localRotation;
            _stride = StrideShare * reach;
            _lift = LiftShare * reach;
            _crouch = CrouchShare * reach;
            _dip = DipShare * reach;
            _idleBob = IdleBobShare * reach;
            _pivotHeight = pivotHeight;
            _phase = phase;
        }

        /// <summary>Ground covered in one whole cycle, in metres.</summary>
        public float Stride => _stride;
        public int LegCount => _legs.Length;
        /// <summary>Where each foot is wanted this frame, in the world.</summary>
        public Vector3 FootTarget(int leg) => _legs[leg].Foot;
        /// <summary>Where each foot's tip really is, after the legs are bent.</summary>
        public Vector3 FootTip(int leg) => _legs[leg].Knee.TransformPoint(_legs[leg].FootInKnee);
        public bool IsPlanted(int leg) => !_legs[leg].Swinging;
        public Transform HipOf(int leg) => _legs[leg].Hip;
        /// <summary>How stretched each leg is now, hip to foot over its whole length.</summary>
        public float Stretch(int leg) => Vector3.Distance(_legs[leg].Hip.position, _legs[leg].Foot) / (_legs[leg].Upper + _legs[leg].Lower);

        /// <summary>
        /// Reads the legs from a body as made: hips, the Knee_ under each and the Foot_ marks. Null without a whole leg. Call it before anything moves the body.
        /// </summary>
        public static WalkerLegs Create(Transform root, Transform body, List<Transform> hips, Dictionary<int, Transform> feet, float phase)
        {
            var legs = new List<Leg>();
            float reach = 0f, pivot = 0f;
            foreach (Transform hip in hips)
            {
                string[] words = hip.name.Split('_');
                if (words.Length < 2 || !int.TryParse(words[1], out int number)) continue;
                Transform knee = hip.Find("Knee_" + number);
                if (knee == null || !feet.TryGetValue(number, out Transform foot) || hip.parent == null) continue;
                int percent = number * 25;
                if (words.Length > 2) int.TryParse(words[2], out percent);

                Transform model = hip.parent;
                Vector3 h = model.InverseTransformPoint(hip.position), k = model.InverseTransformPoint(knee.position), f = model.InverseTransformPoint(foot.position);
                Vector3 along = (f - h).normalized;
                Vector3 bend = (k - h) - along * Vector3.Dot(k - h, along);
                if (bend.sqrMagnitude < 1e-10f) continue; // A straight leg says nothing about which way its knee bends.
                bend.Normalize();
                var leg = new Leg
                {
                    Hip = hip,
                    Knee = knee,
                    HipRest = hip.localRotation,
                    KneeRest = knee.localRotation,
                    ThighRest = k - h,
                    ShinRest = f - k,
                    BendRest = bend,
                    NormalRest = Vector3.Cross(along, bend),
                    FootInKnee = knee.InverseTransformPoint(foot.position),
                    Upper = Vector3.Distance(hip.position, knee.position),
                    Lower = Vector3.Distance(knee.position, foot.position),
                    Phase = Mathf.Repeat(percent / 100f, 1f),
                };
                Vector3 home = root.InverseTransformPoint(foot.position);
                Vector3 hipHere = root.InverseTransformPoint(hip.position);
                leg.Side = hipHere.x >= 0f ? 1f : -1f;
                leg.End = home.z >= 0f ? 1f : -1f;
                leg.Home = new Vector3(home.x, 0f, home.z);
                legs.Add(leg);
                reach += leg.Upper + leg.Lower;
                pivot += hipHere.y;
            }
            if (legs.Count == 0) return null;
            reach /= legs.Count;
            foreach (Leg leg in legs) leg.Home.z -= leg.End * TuckShare * reach;
            return new WalkerLegs(root, body, legs.ToArray(), reach, pivot / legs.Count, phase);
        }

        /// <summary>While the battle stands still (deployment): if the unit was moved, its feet go straight to their places.</summary>
        public void Hold(float time)
        {
            if (_planted && _root.position == _lastRootPosition && _root.rotation == _lastRootRotation) return;
            PlantAll();
            Pose(time);
        }

        /// <summary>One frame of walking. Delta is battle seconds.</summary>
        public void Animate(float delta, float time)
        {
            if (!_planted) PlantAll();
            float ground = _root.position.y;

            // How far the feet's places moved: the one that moved furthest sets the pace.
            float moved = 0f;
            bool jumped = false;
            foreach (Leg leg in _legs)
            {
                Vector3 home = HomeInWorld(leg, ground);
                leg.Moved = Flat(home - leg.LastHome);
                leg.LastHome = home;
                float distance = leg.Moved.magnitude;
                moved = Mathf.Max(moved, distance);
                if (distance > _stride * 2f) jumped = true;
                leg.From.y = ground;
                leg.Planted.y = ground;
            }
            if (jumped)
            {
                // Placed somewhere else at once (dropped in, or a new battle): no walk across the gap.
                PlantAll();
                Pose(time);
                return;
            }

            // The cycle follows the ground covered. A step in the air never goes slower than the least pace, and the cycle then waits for the ground
            // to catch up before the next foot lifts: walking slowly, it steps and pauses. Faster than it can step, the ground it can't keep up with is
            // let go (the feet slide).
            bool walking = moved > 1e-5f;
            float covered = Mathf.Min(moved / _stride, MostCyclesPerSecond * delta);
            float remaining = float.MaxValue;
            foreach (Leg leg in _legs)
            {
                float p = Mathf.Repeat(_cycle + leg.Phase, 1f);
                if (InAir(p)) remaining = Mathf.Min(remaining, SwingShare - p);
            }
            float advance;
            if (!walking) _lead = 0f;
            if (remaining < float.MaxValue) advance = Mathf.Min(Mathf.Max(covered - _lead, LeastCyclesPerSecond * delta), remaining);
            else if (walking) advance = Mathf.Max(0f, covered - _lead);
            else advance = Unsettled() ? LeastCyclesPerSecond * delta : 0f;
            if (walking) _lead = Mathf.Clamp(_lead + advance - covered, -SwingShare, SwingShare);

            float smooth = 1f - Mathf.Exp(-delta / TravelSmoothSeconds);
            foreach (Leg leg in _legs)
            {
                // Where a step lands: ahead of the foot's place by half of what the body covers while it is down.
                Vector3 perCycle = walking ? leg.Moved * (_stride / moved) : Vector3.zero;
                leg.Travel = Vector3.Lerp(leg.Travel, perCycle, smooth);
            }
            _cycle = Mathf.Repeat(_cycle + advance, 1f);
            _walk = Mathf.MoveTowards(_walk, walking || advance > 0f ? 1f : 0f, delta * 1.2f);

            foreach (Leg leg in _legs)
            {
                float p = Mathf.Repeat(_cycle + leg.Phase, 1f);
                bool inAir = InAir(p);
                if (inAir && !leg.Swinging)
                {
                    leg.Swinging = true;
                    leg.From = leg.Planted;
                }
                else if (!inAir && leg.Swinging)
                {
                    leg.Swinging = false;
                    leg.Planted = Landing(leg, SwingShare);
                }
                if (leg.Swinging)
                {
                    float s = p / SwingShare;
                    // Up first, then forward, then down: no foot slides along the ground as it leaves or lands.
                    float forward = Smooth(Mathf.Clamp01((s - 0.12f) / 0.76f));
                    leg.Foot = Vector3.Lerp(leg.From, Landing(leg, p), forward) + Vector3.up * (_lift * Mathf.Sin(Mathf.PI * s));
                }
                else leg.Foot = leg.Planted;
            }
            Pose(time);
        }

        /// <summary>Where the foot comes down if it lands when its phase reaches the end of its step.</summary>
        private Vector3 Landing(Leg leg, float p)
        {
            Vector3 at = leg.LastHome + leg.Travel * (SwingShare - p + (1f - SwingShare) * 0.5f);
            at.y = _root.position.y;
            return at;
        }

        private Vector3 HomeInWorld(Leg leg, float ground)
        {
            Vector3 home = _root.TransformPoint(leg.Home);
            home.y = ground;
            return home;
        }

        private void PlantAll()
        {
            float ground = _root.position.y;
            foreach (Leg leg in _legs)
            {
                leg.LastHome = leg.Planted = leg.From = leg.Foot = HomeInWorld(leg, ground);
                leg.Travel = Vector3.zero;
                leg.Swinging = false;
            }
            // Between two steps, every foot down: the first leg just about to lift.
            _cycle = Mathf.Repeat(-_legs[0].Phase, 1f);
            _lead = 0f;
            _walk = 0f;
            _planted = true;
        }

        /// <summary>A foot is in the air between the very start and the very end of its step; at either end it stands.</summary>
        private static bool InAir(float p) => p > Eps && p < SwingShare - Eps;

        /// <summary>Whether a planted foot stands far enough from its place to step back to it.</summary>
        private bool Unsettled()
        {
            foreach (Leg leg in _legs)
            {
                if (Flat(leg.Planted - leg.LastHome).magnitude > SettleShare * _stride) return true;
            }
            return false;
        }

        /// <summary>Places the body for the weight on the feet, then bends each leg to reach its foot.</summary>
        private void Pose(float time)
        {
            _lastRootPosition = _root.position;
            _lastRootRotation = _root.rotation;
            Vector3 shift = Vector3.zero;
            float dip = 0f, roll = 0f, pitch = 0f;
            foreach (Leg leg in _legs)
            {
                // The weight leaves a foot around its step: most at the middle of it, starting a little before it lifts.
                float centre = SwingShare * 0.5f - WeightLead;
                float x = Mathf.Repeat(_cycle + leg.Phase - centre + 0.5f, 1f) - 0.5f;
                float off = Mathf.Abs(x) < 0.25f ? Mathf.Cos(2f * Mathf.PI * x) * Mathf.Cos(2f * Mathf.PI * x) : 0f;
                shift -= leg.Home * (off * ShiftShare);
                roll += off * leg.Side * RollDegrees;
                pitch -= off * leg.End * PitchDegrees;
                if (leg.Swinging) dip += Mathf.Sin(Mathf.PI * Mathf.Repeat(_cycle + leg.Phase, 1f) / SwingShare) * _dip;
            }
            shift *= _walk;
            float bob = (1f - _walk) * _idleBob * Mathf.Sin((time * 0.18f + _phase) * Mathf.PI * 2f);
            // Lifted side and end up, so the body tips down over the feet that carry it. Around the hips' height, not the ground.
            Quaternion tilt = Quaternion.Euler(pitch * _walk, 0f, roll * _walk);
            var pivot = new Vector3(0f, _pivotHeight, 0f);
            _body.localRotation = tilt * _restRotation;
            _body.localPosition = tilt * (_restPosition - pivot) + pivot + shift + Vector3.up * (bob - _crouch - dip * _walk);

            foreach (Leg leg in _legs)
            {
                if (!leg.Swinging) leg.Foot = leg.Planted = KeepInReach(leg, leg.Planted);
                Bend(leg, leg.Foot);
            }
        }

        /// <summary>A planted foot that its leg can no longer reach, or that strays too far from its place, slides back toward it.</summary>
        private Vector3 KeepInReach(Leg leg, Vector3 foot)
        {
            Vector3 home = leg.LastHome;
            Vector3 stray = Flat(foot - home);
            float most = StrayLimit * _stride;
            if (stray.magnitude > most) foot = home + stray.normalized * most;
            Vector3 hip = leg.Hip.position;
            float longest = ReachLimit * (leg.Upper + leg.Lower);
            float down = hip.y - foot.y;
            Vector3 across = Flat(foot - hip);
            float room = longest * longest - down * down;
            if (room > 0f && across.sqrMagnitude > room) foot = new Vector3(hip.x, foot.y, hip.z) + across.normalized * Mathf.Sqrt(room);
            return foot;
        }

        /// <summary>Turns the hip and the knee so that the foot's tip is at foot, keeping the leg in the plane it bends in.</summary>
        private static void Bend(Leg leg, Vector3 foot)
        {
            Quaternion model = leg.Hip.parent.rotation;
            Vector3 hip = leg.Hip.position;
            Vector3 toFoot = foot - hip;
            float distance = toFoot.magnitude;
            if (distance < 1e-5f) return;
            Vector3 along = toFoot / distance;
            distance = Mathf.Clamp(distance, Mathf.Abs(leg.Upper - leg.Lower) + 1e-3f, (leg.Upper + leg.Lower) * 0.999f);
            Vector3 bend = model * leg.BendRest;
            bend -= along * Vector3.Dot(bend, along);
            if (bend.sqrMagnitude < 1e-10f) bend = Vector3.Cross(model * leg.NormalRest, along);
            bend.Normalize();
            float a = (leg.Upper * leg.Upper - leg.Lower * leg.Lower + distance * distance) / (2f * distance);
            float h = Mathf.Sqrt(Mathf.Max(0f, leg.Upper * leg.Upper - a * a));
            Vector3 knee = hip + along * a + bend * h;
            Vector3 tip = hip + along * distance;
            Vector3 normal = Vector3.Cross(along, bend);
            Vector3 restNormal = model * leg.NormalRest;
            Quaternion hipRest = model * leg.HipRest;
            Quaternion thigh = Quaternion.LookRotation(knee - hip, normal) * Quaternion.Inverse(Quaternion.LookRotation(model * leg.ThighRest, restNormal));
            leg.Hip.rotation = thigh * hipRest;
            Quaternion shin = Quaternion.LookRotation(tip - knee, normal) * Quaternion.Inverse(Quaternion.LookRotation(model * leg.ShinRest, restNormal));
            leg.Knee.rotation = shin * hipRest * leg.KneeRest;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
