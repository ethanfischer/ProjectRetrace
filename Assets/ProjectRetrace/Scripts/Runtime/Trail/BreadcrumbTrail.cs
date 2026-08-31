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
    /// Records a breadcrumb trail for each phase: the search route in phase 1, which becomes
    /// the sentry's patrol route, and the sneak route in phase 2, kept only so the Results
    /// view can show both side by side.
    ///
    /// Spacing is distance-based rather than time-based on purpose: sampling on a timer would
    /// pile breadcrumbs up wherever the player stood still, and the patrol only needs the
    /// route's geometry -- the sentry supplies its own pace.
    ///
    /// Dwells are recorded separately, because standing still drops no crumbs and lingering
    /// would otherwise be invisible to the sentry. One DwellPoint per stop no matter how long
    /// the stop lasts: the sentry pauses for a fixed duration at playback, so camping in
    /// phase 1 cannot stretch the patrol and soften phase 2.
    /// </summary>
    [DisallowMultipleComponent]
    public class BreadcrumbTrail : MonoBehaviour
    {
        /// <summary>Ignore the first moments of phase 1 so the spawn freeze while input is
        /// re-enabled does not read as a deliberate stop.</summary>
        private const float DwellWarmupSeconds = 1f;

        [Tooltip("The player. Assigned by ProjectRetrace > Setup Scene Systems.")]
        public Transform tracked;

        public RetraceSettings settings;

        private readonly List<Breadcrumb> _phase1 = new List<Breadcrumb>();
        private readonly List<Breadcrumb> _phase2 = new List<Breadcrumb>();
        private readonly List<DwellPoint> _dwells = new List<DwellPoint>();
        private RetraceSettings _fallbackSettings;
        private TrailMode _mode = TrailMode.Idle;
        private Vector3 _lastPosition;
        private Vector3 _lastCrumbPosition;
        private float _distanceSinceLastCrumb;
        private float _phase1Distance;
        private float _phase2Distance;
        private Vector3 _dwellAnchor;
        private float _dwellTime;
        private bool _dwellRecorded;
        private float _phase1StartTime;

        public IReadOnlyList<Breadcrumb> Phase1Crumbs => _phase1;
        public IReadOnlyList<Breadcrumb> Phase2Crumbs => _phase2;
        public IReadOnlyList<DwellPoint> DwellPoints => _dwells;
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
            _dwells.Clear();
            _phase1Distance = 0f;
            _phase2Distance = 0f;
            _distanceSinceLastCrumb = 0f;
            _dwellTime = 0f;
            _dwellRecorded = false;
            _phase1StartTime = Time.time;
            _mode = TrailMode.Phase1;

            if (tracked == null)
            {
                Debug.LogError("[BreadcrumbTrail] No tracked transform assigned -- no trail will be recorded.", this);
                _mode = TrailMode.Idle;
                return;
            }

            _lastPosition = tracked.position;
            _lastCrumbPosition = tracked.position;
            _dwellAnchor = tracked.position;
            DropCrumb(tracked.position);
        }

        /// <summary>Phase 2: keep the search trail for the patrol, record the sneak trail.</summary>
        public void BeginPhase2()
        {
            _phase2.Clear();
            _phase2Distance = 0f;
            _distanceSinceLastCrumb = 0f;
            _mode = TrailMode.Phase2;
            if (tracked != null)
            {
                _lastPosition = tracked.position;
                _lastCrumbPosition = tracked.position;
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
            // jumping, and jump-spam should not shorten the gap to the next crumb. Stairs
            // still accumulate, because climbing them also moves you in XZ.
            var delta = position - _lastPosition;
            delta.y = 0f;
            var travelled = delta.magnitude;
            _lastPosition = position;

            _distanceSinceLastCrumb += travelled;
            if (_mode == TrailMode.Phase1)
            {
                _phase1Distance += travelled;
                TrackDwell(position);
            }
            else
            {
                _phase2Distance += travelled;
            }

            if (_distanceSinceLastCrumb >= EffectiveSettings.dotSpacing)
            {
                DropCrumb(position);
                _distanceSinceLastCrumb = 0f;
            }
        }

        private void TrackDwell(Vector3 position)
        {
            if (Time.time - _phase1StartTime < DwellWarmupSeconds)
            {
                _dwellAnchor = position;
                return;
            }

            var offset = position - _dwellAnchor;
            offset.y = 0f;
            var radius = EffectiveSettings.dwellRadius;
            if (offset.sqrMagnitude > radius * radius)
            {
                _dwellAnchor = position;
                _dwellTime = 0f;
                _dwellRecorded = false;
                return;
            }

            if (_dwellRecorded) return;

            _dwellTime += Time.deltaTime;
            if (_dwellTime < EffectiveSettings.dwellSeconds) return;

            _dwells.Add(new DwellPoint(_dwellAnchor, tracked.eulerAngles.y, _phase1.Count - 1));
            _dwellRecorded = true;
        }

        private void DropCrumb(Vector3 position)
        {
            // The arrow points from the previous crumb to this one: the actual direction
            // walked over this stretch, not the instantaneous facing (which flicks around
            // while the player looks at things).
            var direction = position - _lastCrumbPosition;
            direction.y = 0f;
            direction = direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : (tracked != null ? Flatten(tracked.forward) : Vector3.forward);
            _lastCrumbPosition = position;

            var crumb = new Breadcrumb(position, direction);
            if (_mode == TrailMode.Phase1)
            {
                _phase1.Add(crumb);
            }
            else
            {
                _phase2.Add(crumb);
            }
        }

        private static Vector3 Flatten(Vector3 vector)
        {
            vector.y = 0f;
            return vector.sqrMagnitude > 0.0001f ? vector.normalized : Vector3.forward;
        }
    }
}
