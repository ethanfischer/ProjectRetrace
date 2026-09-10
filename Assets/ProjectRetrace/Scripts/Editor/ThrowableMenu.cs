using ProjectRetrace;
using UnityEditor;
using UnityEngine;

namespace ProjectRetrace.EditorTools
{
    /// <summary>
    /// Turns a loose prop into something the player can pick up and throw. The rigidbody
    /// starts kinematic so a marked prop sits still until it is thrown; a convex box is
    /// fitted when the prop has no collider, because a dynamic body rejects a concave mesh.
    /// Idempotent, so re-running on a marked prop changes nothing.
    /// </summary>
    public static class ThrowableMenu
    {
        private const string MenuPath = "ProjectRetrace/Furniture/Mark Selection Throwable";

        [MenuItem(MenuPath, false, 45)]
        public static void MarkSelection()
        {
            var count = 0;
            foreach (var go in Selection.gameObjects)
            {
                MakeThrowable(go);
                count++;
            }

            Debug.Log("[ProjectRetrace] Marked " + count + " prop(s) throwable.");
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateMarkSelection() => Selection.gameObjects.Length > 0;

        public static ThrowableInteractable MakeThrowable(GameObject go)
        {
            // A statically batched mesh is drawn where it was baked no matter where its
            // transform goes: the prop would seem to stay put while an invisible copy flew.
            GameObjectUtility.SetStaticEditorFlags(go, 0);
            EnsureCollider(go);

            // Explicit null checks: in the editor a missing component comes back as a fake
            // null that ?? does not see.
            var body = go.GetComponent<Rigidbody>();
            if (body == null) body = Undo.AddComponent<Rigidbody>(go);
            body.isKinematic = true;
            body.mass = 0.5f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            // A thrown mug is small and fast: discrete collision would let it pass through a wall.
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            if (go.GetComponent<Projectile>() == null) Undo.AddComponent<Projectile>(go);
            var throwable = go.GetComponent<ThrowableInteractable>();
            if (throwable == null) throwable = Undo.AddComponent<ThrowableInteractable>(go);
            EditorUtility.SetDirty(go);
            return throwable;
        }

        private static void EnsureCollider(GameObject go)
        {
            var existing = go.GetComponentInChildren<Collider>();
            if (existing is MeshCollider mesh)
            {
                Undo.RecordObject(mesh, "Convex collider");
                mesh.convex = true;
                return;
            }

            if (existing != null) return;

            var renderers = go.GetComponentsInChildren<Renderer>();
            var box = Undo.AddComponent<BoxCollider>(go);
            if (renderers.Length == 0) return;

            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            box.center = go.transform.InverseTransformPoint(bounds.center);
            box.size = go.transform.InverseTransformVector(bounds.size);
        }
    }
}
