using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectRetrace
{
    /// <summary>
    /// Bakes the navmesh from live colliders when play starts, instead of a baked asset.
    /// The house is regenerated from an editor menu whenever a layout feels stale, and a
    /// persisted bake would silently go stale with it; baking at runtime means the sentry
    /// always walks the house that actually exists. A few hundred box colliders bake in
    /// tens of milliseconds, so the cost is invisible.
    /// </summary>
    [DisallowMultipleComponent]
    public class NavMeshRuntimeBaker : MonoBehaviour
    {
        /// <summary>Narrower than Unity's 0.5 default: the generated doorways are 1.1m wide,
        /// and a 0.5 radius leaves too thin a strip for the bake to carry a path through.</summary>
        private const float AgentRadius = 0.3f;

        private const float AgentHeight = 1.8f;
        private const float AgentClimb = 0.3f;

        private NavMeshDataInstance _instance;

        private void Awake()
        {
            var sources = CollectSources();

            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = AgentRadius;
            settings.agentHeight = AgentHeight;
            settings.agentClimb = AgentClimb;
            settings.agentSlope = 45f;

            var bounds = new Bounds(new Vector3(0f, 3f, 3f), new Vector3(60f, 24f, 60f));
            var data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            _instance = NavMesh.AddNavMeshData(data);
        }

        private void OnDestroy()
        {
            _instance.Remove();
        }

        /// <summary>
        /// Prefer the generated house roots: a whole-scene collect would also sweep up the
        /// player's capsule and any stray prop, punching phantom holes into the walkable area.
        /// </summary>
        private List<NavMeshBuildSource> CollectSources()
        {
            var sources = new List<NavMeshBuildSource>();
            var markups = new List<NavMeshBuildMarkup>();
            var foundHouse = false;

            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                if (!root.name.StartsWith("TestHouse")) continue;
                foundHouse = true;
                IgnoreDoors(root.transform, markups);
                NavMeshBuilder.CollectSources(root.transform, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
            }

            if (!foundHouse)
            {
                Debug.LogWarning("[NavMeshRuntimeBaker] No TestHouse root in the scene -- baking from every collider instead.", null);
                IgnoreDoors(null, markups);
                NavMeshBuilder.CollectSources((Transform)null, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
            }

            return sources;
        }

        /// <summary>
        /// Doors open and close at runtime but the bake happens once, and the sentries retrace
        /// routes without ever operating a door. A closed door baked as a wall would strand a
        /// ghost whose route runs through it, so doors are left out of the bake and the
        /// agents simply walk through them -- fittingly for ghosts.
        /// </summary>
        private static void IgnoreDoors(Transform root, List<NavMeshBuildMarkup> markups)
        {
            var doors = root != null
                ? root.GetComponentsInChildren<DoorInteractable>(true)
                : FindObjectsByType<DoorInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var door in doors)
            {
                markups.Add(new NavMeshBuildMarkup { root = door.transform, ignoreFromBuild = true });
            }
        }
    }
}
