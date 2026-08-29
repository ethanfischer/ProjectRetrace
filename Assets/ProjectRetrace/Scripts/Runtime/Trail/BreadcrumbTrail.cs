using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectRetrace
{
    public enum TrailMode
    {
        Idle,
        Placing,
        Collecting
    }

    /// <summary>
    /// Drops a breadcrumb every dotSpacing metres of travel in phase 1, then collects those
    /// same breadcrumbs by proximity in phase 2.
    ///
    /// Spacing is distance-based rather than time-based on purpose: sampling on a timer would
    /// pile breadcrumbs up wherever the player stood still, which both distorts the score and
    /// makes the debug view unreadable. Distance-based spacing also gives speed-independence
    /// structurally -- walk the same route at half speed and you collect the same marks.
    /// </summary>
    [DisallowMultipleComponent]
    public class BreadcrumbTrail : MonoBehaviour
    {
        [Tooltip("The player. Assigned by ProjectRetrace > Setup Scene Systems.")]
        public Transform tracked;

        public RetraceSettings settings;

        private readonly List<Breadcrumb> _crumbs = new List<Breadcrumb>();
        private RetraceSettings _fallbackSettings;
        private TrailMode _mode = TrailMode.Idle;
        private Vector3 _lastPosition;
        private float _distanceSinceLastCrumb;
        private float _phase1Distance;
        private float _phase2Distance;
        private int _collected;

        public IReadOnlyList<Breadcrumb> Crumbs => _crumbs;
        public TrailMode Mode => _mode;
        public int Total => _crumbs.Count;
        public int Collected => _collected;
        public float Phase1Distance => _phase1Distance;
        public float Phase2Distance => _phase2Distance;

        /// <summary>Fired as each crumb is dropped, so the visualiser can spawn its dot.</summary>
        public event Action<Breadcrumb> CrumbPlaced;

        /// <summary>Fired as each crumb is collected, so the visualiser can recolour its dot.</summary>
        public event Action<Breadcrumb> CrumbCollected;

        public RetraceSettings EffectiveSettings
        {
            get
            {
                if (settings != null) return settings;
                if (_fallbackSettings == null) _fallbackSettings = RetraceSettings.CreateDefault();
                return _fallbackSettings;
            }
        }

        /// <summary>Phase 1: wipe any previous run and start dropping marks.</summary>
        public void BeginPlacement()
        {
            _crumbs.Clear();
            _collected = 0;
            _phase1Distance = 0f;
            _phase2Distance = 0f;
            _distanceSinceLastCrumb = 0f;
            _mode = TrailMode.Placing;

            if (tracked == null)
            {
                Debug.LogError("[BreadcrumbTrail] No tracked transform assigned -- no trail will be recorded.", this);
                _mode = TrailMode.Idle;
                return;
            }

            _lastPosition = tracked.position;
            PlaceCrumb(tracked.position);
        }

        /// <summary>Phase 2: keep the marks, clear their collected state, start collecting.</summary>
        public void BeginCollection()
        {
            _collected = 0;
            _phase2Distance = 0f;
            for (var i = 0; i < _crumbs.Count; i++)
            {
                _crumbs[i].Collected = false;
            }

            _mode = TrailMode.Collecting;
            if (tracked != null)
            {
                _lastPosition = tracked.position;
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
            // jumping, and letting jump-spam inflate distance would tank efficiency unfairly.
            // Stairs still accumulate, because climbing them also moves you in XZ.
            var delta = position - _lastPosition;
            delta.y = 0f;
            var travelled = delta.magnitude;
            _lastPosition = position;

            if (_mode == TrailMode.Placing)
            {
                _phase1Distance += travelled;
                _distanceSinceLastCrumb += travelled;

                if (_distanceSinceLastCrumb >= EffectiveSettings.dotSpacing)
                {
                    PlaceCrumb(position);
                    _distanceSinceLastCrumb = 0f;
                }

                return;
            }

            _phase2Distance += travelled;
            CollectNearby(position);
        }

        private void PlaceCrumb(Vector3 position)
        {
            var crumb = new Breadcrumb(position);
            _crumbs.Add(crumb);
            CrumbPlaced?.Invoke(crumb);
        }

        /// <summary>
        /// Plain loop rather than trigger colliders: a few hundred sqrMagnitude checks per
        /// frame costs nothing, and it avoids rigidbody / layer / physics-matrix setup.
        /// The check is full 3D so you cannot collect marks from the floor above.
        /// </summary>
        private void CollectNearby(Vector3 position)
        {
            var radius = EffectiveSettings.collectRadius;
            var radiusSquared = radius * radius;

            for (var i = 0; i < _crumbs.Count; i++)
            {
                var crumb = _crumbs[i];
                if (crumb.Collected) continue;
                if ((crumb.Position - position).sqrMagnitude > radiusSquared) continue;

                crumb.Collected = true;
                _collected++;
                CrumbCollected?.Invoke(crumb);
            }
        }

        /// <summary>Live score while collecting, and the final score once stopped.</summary>
        public ScoreResult BuildScore()
        {
            return RetraceScorer.Score(_collected, _crumbs.Count, _phase1Distance, _phase2Distance);
        }
    }
}
