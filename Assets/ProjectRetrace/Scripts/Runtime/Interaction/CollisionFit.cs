using System.Collections.Generic;
using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// Prepare's receipt for the BoxColliders it fitted to a static part. If the boxes on
    /// the part still match the receipt, Prepare owns them and may refit; if they differ,
    /// someone tuned them by hand and Prepare leaves them alone, and a re-import carries
    /// them over to the same part. No inspector work: editing the collider is the opt-out.
    /// </summary>
    public class CollisionFit : MonoBehaviour
    {
        public bool handTuned;
        public List<Vector3> centers = new List<Vector3>();
        public List<Vector3> sizes = new List<Vector3>();

        public void Record(IEnumerable<BoxCollider> boxes)
        {
            centers.Clear();
            sizes.Clear();
            foreach (var box in boxes)
            {
                centers.Add(box.center);
                sizes.Add(box.size);
            }
        }

        public bool Matches(BoxCollider[] boxes)
        {
            if (boxes.Length != centers.Count) return false;
            for (var i = 0; i < boxes.Length; i++)
            {
                if ((boxes[i].center - centers[i]).sqrMagnitude > 1e-6f) return false;
                if ((boxes[i].size - sizes[i]).sqrMagnitude > 1e-6f) return false;
            }

            return true;
        }
    }
}
