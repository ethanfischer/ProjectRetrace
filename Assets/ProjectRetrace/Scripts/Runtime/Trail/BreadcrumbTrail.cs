using System.Collections.Generic;
using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// Records the player's route through the house, one RecordedRoute per round, endlessly:
    /// the search is route 0, each stealth round records the next. Every completed route
    /// becomes a sentry's patrol script in the following round -- that accumulation is the
    /// entire difficulty curve, so the trail never throws a completed route away.
    ///
    /// Spacing is distance-based rather than time-based on purpose: sampling on a timer
    /// would pile breadcrumbs up wherever the player stood still, and a patrol only needs
    /// the route's geometry -- the sentry supplies its own pace.
    ///
    /// Dwells are recorded separately, because standing still drops no crumbs and lingering
    /// would otherwise be invisible to the sentries. One DwellPoint per stop no matter how
    /// long the stop lasts: playback pauses for a fixed duration, so camping cannot stretch
    /// a patrol and soften the next round.
    /// </summary>
    [DisallowMultipleComponent]
    public class BreadcrumbTrail : MonoBehaviour
    {
        /// <summary>Ignore the first moments of each route so the spawn freeze while input
        /// is re-enabled does not read as a deliberate stop.</summary>
        private const float DwellWarmupSeconds = 1f;

        [Tooltip("The player. Assigned by ProjectRetrace > Setup Scene Systems.")]
        public Transform tracked;

        private readonly List<RecordedRoute> _routes = new List<RecordedRoute>();
        private bool _recording;
        private Vector3 _lastPosition;
        private Vector3 _lastCrumbPosition;
        private float _distanceSinceLastCrumb;
        private Vector3 _dwellAnchor;
        private float _dwellTime;
        private bool _dwellRecorded;
        private float _routeStartTime;

        /// <summary>All routes, oldest first. The last entry is still being written while
        /// Recording is true.</summary>
        public IReadOnlyList<RecordedRoute> Routes => _routes;

        public bool Recording => _recording;

        public RecordedRoute CurrentRoute => _recording ? _routes[_routes.Count - 1] : null;

        /// <summary>Routes finished being recorded -- the ones sentries may patrol.</summary>
        public int CompletedRouteCount => _recording ? _routes.Count - 1 : _routes.Count;

        /// <summary>New run: wipe every route and start recording route 0 (the search).</summary>
        public void BeginFirstRoute(int owner = 1)
        {
            _routes.Clear();
            StartRoute(owner);
        }

        /// <summary>New round: the route just walked is finalised as a patrol script and a
        /// fresh one starts recording.</summary>
        public void BeginNextRoute(int owner = 1)
        {
            StartRoute(owner);
        }

        /// <summary>Caught mid-round: throw away the failed attempt's recording and record
        /// the round again, so a route that ends in a catch never becomes a patrol.</summary>
        public void RestartRoute()
        {
            var owner = 1;
            if (_recording)
            {
                owner = _routes[_routes.Count - 1].Owner;
                _routes.RemoveAt(_routes.Count - 1);
            }

            StartRoute(owner);
        }

        public void Stop()
        {
            _recording = false;
        }

        /// <summary>Eliminated mid-round: the failed attempt's recording is thrown away
        /// without a replacement -- an eliminated player's last, doomed walk never becomes
        /// a patrol. Their earlier routes stay.</summary>
        public void DiscardRoute()
        {
            if (!_recording) return;
            _routes.RemoveAt(_routes.Count - 1);
            _recording = false;
        }

        private void StartRoute(int owner)
        {
            if (tracked == null)
            {
                Debug.LogError("[BreadcrumbTrail] No tracked transform assigned -- no trail will be recorded.", this);
                _recording = false;
                return;
            }

            _routes.Add(new RecordedRoute { Owner = owner });
            _recording = true;
            _distanceSinceLastCrumb = 0f;
            _dwellTime = 0f;
            _dwellRecorded = false;
            _routeStartTime = Time.time;
            _lastPosition = tracked.position;
            _lastCrumbPosition = tracked.position;
            _dwellAnchor = tracked.position;
            DropCrumb(tracked.position);
        }

        private void Update()
        {
            if (!_recording || tracked == null) return;

            var position = tracked.position;

            // Accumulate on the horizontal plane only: vertical movement is dominated by
            // jumping, and jump-spam should not shorten the gap to the next crumb. Stairs
            // still accumulate, because climbing them also moves you in XZ.
            var delta = position - _lastPosition;
            delta.y = 0f;
            var travelled = delta.magnitude;
            _lastPosition = position;

            CurrentRoute.Distance += travelled;
            TrackDwell(position);

            _distanceSinceLastCrumb += travelled;
            if (_distanceSinceLastCrumb >= RetraceConfig.Current.dotSpacing)
            {
                DropCrumb(position);
                _distanceSinceLastCrumb = 0f;
            }
        }

        private void TrackDwell(Vector3 position)
        {
            if (Time.time - _routeStartTime < DwellWarmupSeconds)
            {
                _dwellAnchor = position;
                return;
            }

            var offset = position - _dwellAnchor;
            offset.y = 0f;
            var radius = RetraceConfig.Current.dwellRadius;
            if (offset.sqrMagnitude > radius * radius)
            {
                _dwellAnchor = position;
                _dwellTime = 0f;
                _dwellRecorded = false;
                return;
            }

            if (_dwellRecorded) return;

            _dwellTime += Time.deltaTime;
            if (_dwellTime < RetraceConfig.Current.dwellSeconds) return;

            var route = CurrentRoute;
            route.Dwells.Add(new DwellPoint(_dwellAnchor, tracked.eulerAngles.y, route.Crumbs.Count - 1));
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

            CurrentRoute.Crumbs.Add(new Breadcrumb(position, direction));
        }

        private static Vector3 Flatten(Vector3 vector)
        {
            vector.y = 0f;
            return vector.sqrMagnitude > 0.0001f ? vector.normalized : Vector3.forward;
        }
    }
}
