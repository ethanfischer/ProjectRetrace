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
    /// Dwells are recorded where the player *used* something, not where they stood still.
    /// Searching is what the game is about, so the places a player rummaged are the ones a
    /// sentry should stop and check; merely lingering in a corner tells the sentry nothing.
    /// Repeat uses at one spot collapse into a single DwellPoint and playback pauses for a
    /// fixed duration, so rattling a drawer cannot stretch a patrol and soften the next round.
    /// </summary>
    [DisallowMultipleComponent]
    public class BreadcrumbTrail : MonoBehaviour
    {
        [Tooltip("The player. Assigned by ProjectRetrace > Setup Scene Systems.")]
        public Transform tracked;

        private readonly List<RecordedRoute> _routes = new List<RecordedRoute>();
        private bool _recording;
        private Vector3 _lastPosition;
        private Vector3 _lastCrumbPosition;
        private float _distanceSinceLastCrumb;
        private PlayerInteractor _interactor;

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

        /// <summary>The most recently finished route -- the one to hand an opponent.</summary>
        public RecordedRoute LastCompleted => CompletedRouteCount > 0 ? _routes[CompletedRouteCount - 1] : null;

        /// <summary>A route walked on another machine joins the pool exactly as a local one
        /// would: nothing downstream cares where a route came from.</summary>
        public void AddCompletedRoute(RecordedRoute route)
        {
            if (route == null) return;
            var insertAt = _recording ? _routes.Count - 1 : _routes.Count;
            _routes.Insert(insertAt, route);
        }

        /// <summary>Reconnect: the whole pool arrives from the relay's log at once.</summary>
        public void SetRoutes(IEnumerable<RecordedRoute> routes)
        {
            _recording = false;
            _routes.Clear();
            _routes.AddRange(routes);
        }

        private void OnDisable()
        {
            ListenTo(null);
        }

        private void StartRoute(int owner)
        {
            if (tracked == null)
            {
                Debug.LogError("[BreadcrumbTrail] No tracked transform assigned -- no trail will be recorded.", this);
                _recording = false;
                return;
            }

            ListenTo(tracked.GetComponentInChildren<PlayerInteractor>());

            _routes.Add(new RecordedRoute { Owner = owner });
            _recording = true;
            _distanceSinceLastCrumb = 0f;
            _lastPosition = tracked.position;
            _lastCrumbPosition = tracked.position;
            DropCrumb(tracked.position);
        }

        private void ListenTo(PlayerInteractor interactor)
        {
            if (_interactor == interactor) return;
            if (_interactor != null) _interactor.Interacted -= RecordDwell;
            _interactor = interactor;
            if (_interactor != null) _interactor.Interacted += RecordDwell;
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

            _distanceSinceLastCrumb += travelled;
            if (_distanceSinceLastCrumb >= RetraceConfig.Current.dotSpacing)
            {
                DropCrumb(position);
                _distanceSinceLastCrumb = 0f;
            }
        }

        private void RecordDwell(IInteractable used)
        {
            if (!_recording) return;

            var route = CurrentRoute;
            if (IsWithinLastDwell(route, tracked.position)) return;

            var propId = InteractableRegistry.IdOf(used);
            route.Dwells.Add(new DwellPoint(tracked.position, tracked.eulerAngles.y, route.Crumbs.Count - 1, propId));
        }

        /// <summary>A dresser's three drawers are one stop, not three: anything used within
        /// dwellRadius of the previous stop folds into it.</summary>
        private static bool IsWithinLastDwell(RecordedRoute route, Vector3 position)
        {
            if (route.Dwells.Count == 0) return false;

            var offset = position - route.Dwells[route.Dwells.Count - 1].Position;
            offset.y = 0f;
            var radius = RetraceConfig.Current.dwellRadius;
            return offset.sqrMagnitude <= radius * radius;
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
