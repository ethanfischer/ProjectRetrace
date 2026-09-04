using System.Collections.Generic;
using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// Snapshots keyed by the sender's clock, read back a fixed delay behind it so there is
    /// always a later one to interpolate towards. The clock offset is the smallest
    /// (arrival - sent) seen recently: the frame that arrived with the least queueing is
    /// the truest measure of the gap between the two clocks. Unscaled time throughout,
    /// because the settings menu pauses the world with timeScale and the stream must not.
    /// </summary>
    public class SnapshotBuffer
    {
        private const int Capacity = 64;
        private const int OffsetWindow = 32;

        private readonly List<SnapshotMsg> _snapshots = new List<SnapshotMsg>();
        private readonly Queue<float> _offsets = new Queue<float>();
        private float _offset = float.NaN;

        public int Count => _snapshots.Count;
        public SnapshotMsg Latest => _snapshots.Count > 0 ? _snapshots[_snapshots.Count - 1] : null;

        public void Clear()
        {
            _snapshots.Clear();
            _offsets.Clear();
            _offset = float.NaN;
        }

        public void Push(SnapshotMsg snapshot)
        {
            var offset = Time.unscaledTime - snapshot.t;
            _offsets.Enqueue(offset);
            while (_offsets.Count > OffsetWindow) _offsets.Dequeue();
            _offset = float.PositiveInfinity;
            foreach (var o in _offsets) _offset = Mathf.Min(_offset, o);

            if (Latest != null && snapshot.t <= Latest.t) return;
            _snapshots.Add(snapshot);
            while (_snapshots.Count > Capacity) _snapshots.RemoveAt(0);
        }

        /// <summary>The pair bracketing the render time and how far between them it falls.
        /// Past the newest snapshot it holds the last pose rather than extrapolating: a
        /// ghost sliding through a wall on a hiccup would be worse than a brief freeze.</summary>
        public bool Sample(float delaySeconds, out SnapshotMsg from, out SnapshotMsg to, out float t)
        {
            from = to = null;
            t = 0f;
            if (_snapshots.Count == 0 || float.IsNaN(_offset)) return false;

            var renderTime = Time.unscaledTime - _offset - delaySeconds;
            for (var i = _snapshots.Count - 1; i >= 0; i--)
            {
                if (_snapshots[i].t > renderTime) continue;

                from = _snapshots[i];
                to = i + 1 < _snapshots.Count ? _snapshots[i + 1] : from;
                var span = to.t - from.t;
                t = span > 0.0001f ? Mathf.Clamp01((renderTime - from.t) / span) : 0f;
                return true;
            }

            from = to = _snapshots[0];
            return true;
        }
    }
}
