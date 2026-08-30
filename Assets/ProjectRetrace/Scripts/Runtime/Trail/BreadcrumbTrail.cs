using System.Collections.Generic;
using UnityEngine;

namespace ProjectRetrace
{
    public enum TrailMode
    {
        Idle,
        Phase1,
        Phase2
    }

    /// <summary>
    /// Records a breadcrumb trail for each phase: one while searching, a second while
    /// retracing. The score is how well the two trails overlap (see RetraceScorer), so both
    /// phases record the same way and neither trail is "the" trail.
    ///
    /// Spacing is distance-based rather than time-based on purpose: sampling on a timer would
    /// pile breadcrumbs up wherever the player stood still, which both distorts the score and
    /// makes the debug view unreadable. Distance-based spacing also gives speed-independence
    /// structurally -- walk the same route at half speed and you leave the same trail.
    ///
    /// Matching is incremental so the live score costs nothing per frame:
    /// - A phase-1 crumb is Matched once the phase-2 player passes within collectRadius of it.
    /// - A phase-2 crumb is Matched at drop time if it lands within collectRadius of any
    ///   phase-1 crumb. Extra wandering therefore drops unmatched phase-2 crumbs, which is
    ///   exactly what the precision term punishes.
    /// </summary>
    [DisallowMultipleComponent]
    public class BreadcrumbTrail : MonoBehaviour
    {
        [Tooltip("The player. Assigned by ProjectRetrace > Setup Scene Systems.")]
        public Transform tracked;

        public RetraceSettings settings;

        private readonly List<Breadcrumb> _phase1 = new List<Breadcrumb>();
        private readonly List<Breadcrumb> _phase2 = new List<Breadcrumb>();
        private RetraceSettings _fallbackSettings;
        private TrailMode _mode = TrailMode.Idle;
        private Vector3 _lastPosition;
        private float _distanceSinceLastCrumb;
        private float _phase1Distance;
        private float _phase2Distance;
        private int _matched1;
        private int _matched2;

        public IReadOnlyList<Breadcrumb> Phase1Crumbs => _phase1;
        public IReadOnlyList<Breadcrumb> Phase2Crumbs => _phase2;
        public TrailMode Mode => _mode;
        public float Phase1Distance => _phase1Distance;
        public float Phase2Distance => _phase2Distance;

        public RetraceSettings EffectiveSettings
        {
            get
            {
                if (settings != null) return settings;
                if (_fallbackSettings == null) _fallbackSettings = RetraceSettings.CreateDefault();
                return _fallbackSettings;
            }
        }

        /// <summary>Phase 1: wipe any previous run and start recording the search trail.</summary>
        public void BeginPhase1()
        {
            _phase1.Clear();
            _phase2.Clear();
            _matched1 = 0;
            _matched2 = 0;
            _phase1Distance = 0f;
            _phase2Distance = 0f;
            _distanceSinceLastCrumb = 0f;
            _mode = TrailMode.Phase1;

            if (tracked == null)
            {
                Debug.LogError("[BreadcrumbTrail] No tracked transform assigned -- no trail will be recorded.", this);
                _mode = TrailMode.Idle;
                return;
            }

            _lastPosition = tracked.position;
            DropCrumb(tracked.position);
        }

        /// <summary>Phase 2: keep the search trail, start recording the retrace trail.</summary>
        public void BeginPhase2()
        {
            _phase2.Clear();
            _matched2 = 0;
            _phase2Distance = 0f;
            _distanceSinceLastCrumb = 0f;
            _mode = TrailMode.Phase2;
            if (tracked != null)
            {
                _lastPosition = tracked.position;
                DropCrumb(tracked.position);
            }
        }

        public void Stop()
        {
            _mode = TrailMode.Idle;
        }

        private void Update()
        {
            if (_mode == TrailMode.Idle || tracked == null) return;

            var position = tracked.position;

            // Accumulate on the horizontal plane only: vertical movement is dominated by
            // jumping, and letting jump-spam inflate distance would tank the score unfairly.
            // Stairs still accumulate, because climbing them also moves you in XZ.
            var delta = position - _lastPosition;
            delta.y = 0f;
            var travelled = delta.magnitude;
            _lastPosition = position;

            _distanceSinceLastCrumb += travelled;
            if (_mode == TrailMode.Phase1)
            {
                _phase1Distance += travelled;
            }
            else
            {
                _phase2Distance += travelled;
                MatchPhase1Nearby(position);
            }

            if (_distanceSinceLastCrumb >= EffectiveSettings.dotSpacing)
            {
                DropCrumb(position);
                _distanceSinceLastCrumb = 0f;
            }
        }

        private void DropCrumb(Vector3 position)
        {
            var crumb = new Breadcrumb(position);
            if (_mode == TrailMode.Phase1)
            {
                _phase1.Add(crumb);
                return;
            }

            // A retrace crumb on the original path is a match; off it, wandering.
            var radiusSquared = RadiusSquared();
            for (var i = 0; i < _phase1.Count; i++)
            {
                if ((_phase1[i].Position - position).sqrMagnitude > radiusSquared) continue;
                crumb.Matched = true;
                _matched2++;
                break;
            }

            _phase2.Add(crumb);
        }

        /// <summary>
        /// Plain loop rather than trigger colliders: a few hundred sqrMagnitude checks per
        /// frame costs nothing, and it avoids rigidbody / layer / physics-matrix setup.
        /// The check is full 3D so the floor above cannot match the floor below.
        /// </summary>
        private void MatchPhase1Nearby(Vector3 position)
        {
            var radiusSquared = RadiusSquared();
            for (var i = 0; i < _phase1.Count; i++)
            {
                var crumb = _phase1[i];
                if (crumb.Matched) continue;
                if ((crumb.Position - position).sqrMagnitude > radiusSquared) continue;

                crumb.Matched = true;
                _matched1++;
            }
        }

        private float RadiusSquared()
        {
            var radius = EffectiveSettings.collectRadius;
            return radius * radius;
        }

        /// <summary>Live score during phase 2, and the final score once stopped.</summary>
        public ScoreResult BuildScore()
        {
            return RetraceScorer.Score(_matched1, _phase1.Count, _matched2, _phase2.Count, _phase1Distance, _phase2Distance);
        }
    }
}
