using System.Collections.Generic;
using ProjectRetrace;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectRetrace.EditorTools
{
    /// <summary>
    /// Pulls the art team's dressed level scene into the gameplay scene and makes it
    /// playable. The art scene stays pure art -- no ProjectRetrace components -- so the
    /// artist can keep iterating without merge conflicts against gameplay wiring; every
    /// re-import throws away the previous copy and rebuilds the wiring from scratch.
    ///
    /// Hand-placed additions belong under the Additions root, which a re-import never
    /// touches. Anything found inside the imported copy that the art scene does not
    /// contain is moved there rather than deleted, because a re-import that silently ate
    /// an afternoon's ceiling work is worse than a stray object in the wrong folder.
    /// </summary>
    public static class LevelImportMenu
    {
        private const string SourceScenePath = "Assets/Scenes/HomeInterior_FirstFloor.unity";
        private const string ImportedRootName = "TestHouse (HomeInterior_FirstFloor)";
        private const string AdditionsRootName = "TestHouse (Additions)";
        private const string GeneratedRootPrefix = "TestHouse (seed";
        private const string KeySpotName = "KeySpot";
        private const string InteractivePrefabFolder = "Assets/LowPolyInterior/Prefabs/InteractiveFurniture/";
        private const string RoomDoorPrefabPath = "Assets/LowPolyInterior/Prefabs/Walls/Door_04.prefab";
        private const string BackMaterialPath = "Assets/ProjectRetrace/Art/Materials/FurnitureBack.mat";
        private const string BackName = "Back";
        private const float BackThickness = 0.02f;

        /// <summary>Some carcasses do have a back, painted the same off-white as the room
        /// walls, so it reads as wall anyway, and its thickness varies from prop to prop.
        /// The panel therefore sits just in front of whatever interior face a ray cast from
        /// inside the carcass finds, and this far in front of the mesh edge when it finds none.</summary>
        private const float BackInset = 0.03f;
        private const float BackClearance = 0.01f;
        private static readonly Color BackColour = new Color(0.45f, 0.30f, 0.17f);

        // Entry hall beside the stairs, facing the door into the office.
        private static readonly Vector3 SpawnPosition = new Vector3(-17.5f, 0.05f, 1f);

        /// <summary>A moving part's pivot sits on its hinge edge, while a drawer's sits in
        /// its middle -- that offset, not the part's depth, tells the two apart (a
        /// wardrobe door with a handle is deeper than a shallow drawer).</summary>
        private const float HingeEdgeFraction = 0.4f;

        /// <summary>Anything smaller is a rail or handle, not a part that opens.</summary>
        private const float MinPartVolume = 0.005f;

        /// <summary>A body child dwarfs every moving part; without one (kitchen sinks keep
        /// the carcass on the root) the parts are all of a size and nothing is skipped.</summary>
        private const float BodyVolumeRatio = 2f;

        private const float CupboardMinHeight = 1.5f;
        private const float CupboardMinDepth = 0.45f;

        private struct Summary
        {
            public int props, doors, drawers, keySpots, hidingSpots, roomDoors, backs, colliders, readableMeshes;
            public override string ToString() =>
                $"{props} prop(s): {doors} door(s), {drawers} drawer(s), {keySpots} key spot(s), " +
                $"{hidingSpots} hiding spot(s), {roomDoors} room door(s), {backs} back(s); {colliders} collider(s) added, " +
                $"{readableMeshes} mesh import(s) made readable";
        }

        [MenuItem("ProjectRetrace/Level/Import HomeInterior_FirstFloor", false, 42)]
        public static void ImportFirstFloor()
        {
            var target = SceneManager.GetActiveScene();
            if (target.path == SourceScenePath || SceneManager.GetSceneByPath(SourceScenePath).isLoaded)
            {
                EditorUtility.DisplayDialog(
                    "ProjectRetrace",
                    "Open the gameplay scene (Main) as the active scene, with HomeInterior_FirstFloor closed, then import.",
                    "OK");
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Import HomeInterior_FirstFloor");

            var source = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Additive);
            var sourcePaths = HierarchyPaths(source.GetRootGameObjects());

            var firstImport = RemovePreviousHouse(target, sourcePaths, out var rescued);

            var root = new GameObject(ImportedRootName);
            Undo.RegisterCreatedObjectUndo(root, "Import level");

            foreach (var sceneObject in source.GetRootGameObjects())
            {
                if (sceneObject.GetComponent<Camera>() != null || HasComponentNamed(sceneObject, "Volume")) continue;
                Undo.MoveGameObjectToScene(sceneObject, target, "Import level");
                Undo.SetTransformParent(sceneObject.transform, root.transform, "Import level");
            }

            EditorSceneManager.CloseScene(source, true);

            var summary = PrepareFurniture(root.transform);
            if (firstImport) PlaceSpawnPoint();

            EditorSceneManager.MarkSceneDirty(target);
            Selection.activeGameObject = root;
            Debug.Log($"[ProjectRetrace] Imported {SourceScenePath}: {summary}.");
            if (rescued.Count > 0)
            {
                Debug.LogWarning($"[ProjectRetrace] Moved {rescued.Count} hand-placed object(s) out of the old import into " +
                                 $"'{AdditionsRootName}': {string.Join(", ", rescued)}. Keep additions under that root.");
            }
        }

        [MenuItem("ProjectRetrace/Level/Prepare LowPoly Furniture", false, 43)]
        public static void PrepareAllHouses()
        {
            var summary = new Summary();
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (!root.name.StartsWith("TestHouse")) continue;
                Accumulate(ref summary, PrepareFurniture(root.transform));
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[ProjectRetrace] Prepared LowPoly furniture: {summary}.");
        }

        /// <summary>The generated house, its dev grid and its point light were all stand-ins
        /// for a real level; the imported scene brings its own lighting. Returns true when
        /// no earlier import existed, which is the only time the spawn point may be moved.</summary>
        private static bool RemovePreviousHouse(Scene scene, HashSet<string> sourcePaths, out List<string> rescued)
        {
            rescued = new List<string>();
            var firstImport = true;
            foreach (var root in scene.GetRootGameObjects())
            {
                var isImport = root.name == ImportedRootName;
                var isGenerated = root.name.StartsWith(GeneratedRootPrefix);
                var isGrid = root.name == "DevGridFloor";
                var isLight = root.name == "Directional Light" && root.GetComponent<Light>() != null;
                if (isImport)
                {
                    firstImport = false;
                    RescueAdditions(scene, root.transform, sourcePaths, rescued);
                }

                if (isImport || isGenerated || isGrid || isLight) Undo.DestroyObjectImmediate(root);
            }

            return firstImport;
        }

        /// <summary>Anything under the old import that the art scene never contained was put
        /// there by hand. Prepare's own KeySpot and Back objects are the one exception.</summary>
        private static void RescueAdditions(Scene scene, Transform oldRoot, HashSet<string> sourcePaths, List<string> rescued)
        {
            Transform additions = null;
            foreach (var transform in oldRoot.GetComponentsInChildren<Transform>(true))
            {
                if (transform == oldRoot || transform.name == KeySpotName || transform.name == BackName) continue;
                if (sourcePaths.Contains(PathUnder(oldRoot, transform))) continue;
                if (transform.parent != oldRoot && !sourcePaths.Contains(PathUnder(oldRoot, transform.parent))) continue;

                additions ??= AdditionsRoot(scene);
                Undo.SetTransformParent(transform, additions, "Rescue level addition");
                rescued.Add(transform.name);
            }
        }

        private static Transform AdditionsRoot(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == AdditionsRootName) return root.transform;
            }

            var created = new GameObject(AdditionsRootName);
            Undo.RegisterCreatedObjectUndo(created, "Create additions root");
            return created.transform;
        }

        private static HashSet<string> HierarchyPaths(GameObject[] roots)
        {
            var paths = new HashSet<string>();
            foreach (var root in roots)
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    paths.Add(PathUnder(null, transform));
                }
            }

            return paths;
        }

        private static string PathUnder(Transform root, Transform transform)
        {
            var path = transform.name;
            for (var parent = transform.parent; parent != null && parent != root; parent = parent.parent)
            {
                path = parent.name + "/" + path;
            }

            return path;
        }

        private static Summary PrepareFurniture(Transform house)
        {
            var summary = new Summary();
            summary.colliders = AddMissingColliders(house);
            summary.readableMeshes = MakeCollisionMeshesReadable(house);
            foreach (var transform in house.GetComponentsInChildren<Transform>(true))
            {
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(transform.gameObject)) continue;

                var assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(transform.gameObject);
                if (assetPath == RoomDoorPrefabPath)
                {
                    summary.roomDoors += PrepareRoomDoor(transform.gameObject);
                }
                else if (assetPath.StartsWith(InteractivePrefabFolder))
                {
                    Accumulate(ref summary, PrepareProp(transform.gameObject));
                    summary.props++;
                }
            }

            return summary;
        }

        /// <summary>The pack's prefabs carry MeshColliders but its raw FBX model instances do
        /// not, and the artist mixes both freely; a floor tile without a collider is one the
        /// player falls through and the navmesh never sees.</summary>
        private static int AddMissingColliders(Transform house)
        {
            var added = 0;
            foreach (var filter in house.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.GetComponent<Collider>() != null) continue;
                Undo.AddComponent<MeshCollider>(filter.gameObject);
                added++;
            }

            return added;
        }

        /// <summary>The navmesh bakes from MeshColliders at runtime, which needs the mesh
        /// data on the CPU. The editor reads it regardless (and reports every mesh as
        /// readable, so only the importer setting is trustworthy); the problem only shows in
        /// a player build, where every unreadable mesh silently drops out of the bake.</summary>
        private static int MakeCollisionMeshesReadable(Transform house)
        {
            var importers = new HashSet<ModelImporter>();
            foreach (var collider in house.GetComponentsInChildren<MeshCollider>(true))
            {
                if (collider.sharedMesh == null) continue;
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(collider.sharedMesh)) as ModelImporter;
                if (importer != null && !importer.isReadable) importers.Add(importer);
            }

            foreach (var importer in importers)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            return importers.Count;
        }

        private static int PrepareRoomDoor(GameObject door)
        {
            if (door.GetComponent<DoorInteractable>() != null) return 0;

            // The pack's door pivots on its left edge with the leaf extending along +X, the
            // same layout as the builder cupboard, so it takes the same negative swing.
            FurnitureBuilderMenu.AddHinged(door, Vector3.up, -100f, "door");
            return 1;
        }

        private static Summary PrepareProp(GameObject prop)
        {
            var summary = new Summary();
            var parts = MovingParts(prop.transform);
            foreach (var (part, bounds) in parts)
            {
                if (part.GetComponent<InteractableBase>() != null) continue;

                var center = bounds.center;
                var size = bounds.size;
                if (Mathf.Abs(center.x) >= HingeEdgeFraction * size.x)
                {
                    FurnitureBuilderMenu.AddHinged(part.gameObject, Vector3.up, center.x > 0f ? -110f : 110f, DoorLabel(prop));
                    summary.doors++;
                    summary.keySpots += AddKeySpotBehindDoor(prop.transform, part, center);
                }
                else if (Mathf.Abs(center.y) >= HingeEdgeFraction * size.y)
                {
                    FurnitureBuilderMenu.AddHinged(part.gameObject, Vector3.right, center.y > 0f ? 90f : -90f, DoorLabel(prop));
                    summary.doors++;
                    summary.keySpots += AddKeySpotBehindDoor(prop.transform, part, center);
                }
                else
                {
                    FurnitureBuilderMenu.AddSliding(part.gameObject, Vector3.forward, size.z * 0.7f);
                    summary.drawers++;
                    summary.keySpots += AddKeySpot(part, center);
                }
            }

            if (summary.doors > 0 && IsCupboardSized(prop) && prop.GetComponent<HidingSpot>() == null)
            {
                Undo.AddComponent<HidingSpot>(prop);
                summary.hidingSpots++;
            }

            summary.backs += AddBack(prop);
            return summary;
        }

        /// <summary>The pack models its carcasses open at the back, since they stand against
        /// a wall; with the door open the room wall shows through, and from inside a hiding
        /// spot that reads as a hole. A plain panel closes it and gives the ray something
        /// of the prop's own to land on.</summary>
        private static int AddBack(GameObject prop)
        {
            if (prop.transform.Find(BackName) != null) return 0;

            var bounds = LocalBounds(prop.transform);
            if (bounds.size.sqrMagnitude <= 0f) return 0;

            var back = GameObject.CreatePrimitive(PrimitiveType.Cube);
            back.name = BackName;
            Undo.RegisterCreatedObjectUndo(back, "Add furniture back");
            back.transform.SetParent(prop.transform, false);
            var faceZ = InteriorBackFace(prop.transform, bounds);
            back.transform.localPosition = new Vector3(bounds.center.x, bounds.center.y, faceZ + BackClearance + BackThickness * 0.5f);
            back.transform.localScale = new Vector3(bounds.size.x, bounds.size.y, BackThickness);
            back.GetComponent<Renderer>().sharedMaterial = BackMaterial();
            return 1;
        }

        /// <summary>Casts a small grid of rays from the middle of the carcass toward its rear.
        /// Of the prop's own faces it keeps the deepest (a shallow one is a divider or shelf
        /// edge, not the back); of anything else it keeps the shallowest, because the
        /// artist pushes furniture into the walls and a wall face inside the carcass would
        /// otherwise sit in front of the panel and show through. No hit means an open back.</summary>
        private static float InteriorBackFace(Transform prop, Bounds bounds)
        {
            Physics.SyncTransforms();
            var ownDeepest = float.MaxValue;
            var foreignShallowest = float.MinValue;
            for (var ix = -1; ix <= 1; ix++)
            for (var iy = -1; iy <= 1; iy++)
            {
                var local = new Vector3(
                    bounds.center.x + ix * bounds.size.x * 0.3f,
                    bounds.center.y + iy * bounds.size.y * 0.3f,
                    bounds.center.z);
                var origin = prop.TransformPoint(local);
                foreach (var hit in Physics.RaycastAll(origin, -prop.forward, bounds.size.z, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.name == BackName) continue;
                    var z = prop.InverseTransformPoint(hit.point).z;
                    if (z <= bounds.min.z) continue;
                    if (hit.collider.transform.IsChildOf(prop)) ownDeepest = Mathf.Min(ownDeepest, z);
                    else foreignShallowest = Mathf.Max(foreignShallowest, z);
                }
            }

            if (foreignShallowest > float.MinValue && foreignShallowest > ownDeepest) return foreignShallowest;
            return ownDeepest < float.MaxValue ? ownDeepest : bounds.min.z + BackInset;
        }

        /// <summary>Union of the children's mesh bounds in the prop's space. The pack's
        /// parts carry no rotation or scale, so a local offset is the whole transform.</summary>
        private static Bounds LocalBounds(Transform prop)
        {
            var bounds = new Bounds();
            var first = true;
            foreach (Transform child in prop)
            {
                var filter = child.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null || child.name == BackName) continue;

                var local = filter.sharedMesh.bounds;
                local.center += child.localPosition;
                if (first) { bounds = local; first = false; }
                else bounds.Encapsulate(local);
            }

            return bounds;
        }

        private static Material BackMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(BackMaterialPath);
            if (material != null) return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = BackColour };
            AssetDatabase.CreateAsset(material, BackMaterialPath);
            return material;
        }

        private static List<(Transform part, Bounds bounds)> MovingParts(Transform prop)
        {
            var parts = new List<(Transform, Bounds)>();
            foreach (Transform child in prop)
            {
                var filter = child.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null || child.name == BackName) continue;

                var bounds = filter.sharedMesh.bounds;
                if (Volume(bounds) < MinPartVolume) continue;
                parts.Add((child, bounds));
            }

            parts.Sort((a, b) => Volume(b.Item2).CompareTo(Volume(a.Item2)));
            if (parts.Count > 1 && Volume(parts[0].Item2) > BodyVolumeRatio * Volume(parts[1].Item2))
            {
                parts.RemoveAt(0);
            }

            return parts;
        }

        private static float Volume(Bounds bounds) => bounds.size.x * bounds.size.y * bounds.size.z;

        private static string DoorLabel(GameObject prop) => IsCupboardSized(prop) ? "cupboard" : "cabinet";

        private static bool IsCupboardSized(GameObject prop)
        {
            var renderers = prop.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return false;

            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds.size.y >= CupboardMinHeight && Mathf.Min(bounds.size.x, bounds.size.z) >= CupboardMinDepth;
        }

        /// <summary>Keys inside a drawer ride along as it slides, so the spot lives on the
        /// drawer itself, mirroring the builder's dresser.</summary>
        private static int AddKeySpot(Transform parent, Vector3 localPosition)
        {
            if (parent.Find("KeySpot") != null) return 0;

            var spot = new GameObject("KeySpot");
            Undo.RegisterCreatedObjectUndo(spot, "Add key spot");
            spot.transform.SetParent(parent, false);
            spot.transform.localPosition = localPosition;
            spot.AddComponent<KeySpotMarker>();
            return 1;
        }

        /// <summary>A door's spot sits on the carcass just behind the leaf, one per door, so
        /// a two-door wardrobe offers two hiding places and the keys stay put as it swings.</summary>
        private static int AddKeySpotBehindDoor(Transform prop, Transform door, Vector3 doorLocalCenter)
        {
            var behind = prop.InverseTransformPoint(door.TransformPoint(doorLocalCenter));
            behind.z -= 0.15f;

            foreach (Transform child in prop)
            {
                if (child.name == "KeySpot" && Vector3.Distance(child.localPosition, behind) < 0.05f) return 0;
            }

            var spot = new GameObject("KeySpot");
            Undo.RegisterCreatedObjectUndo(spot, "Add key spot");
            spot.transform.SetParent(prop, false);
            spot.transform.localPosition = behind;
            spot.AddComponent<KeySpotMarker>();
            return 1;
        }

        private static void PlaceSpawnPoint()
        {
            var director = Object.FindFirstObjectByType<GameDirector>();
            var spawn = director != null && director.spawnPoint != null
                ? director.spawnPoint
                : GameObject.Find("SpawnPoint")?.transform;
            if (spawn == null) return;

            Undo.RecordObject(spawn, "Move spawn point");
            spawn.SetPositionAndRotation(SpawnPosition, Quaternion.identity);
        }

        /// <summary>The URP Volume type lives in an assembly this one does not reference,
        /// and a name check is all that is needed to leave the gameplay scene's own alone.</summary>
        private static bool HasComponentNamed(GameObject sceneObject, string typeName)
        {
            foreach (var component in sceneObject.GetComponents<Component>())
            {
                if (component != null && component.GetType().Name == typeName) return true;
            }

            return false;
        }

        private static void Accumulate(ref Summary total, Summary part)
        {
            total.props += part.props;
            total.doors += part.doors;
            total.drawers += part.drawers;
            total.keySpots += part.keySpots;
            total.hidingSpots += part.hidingSpots;
            total.roomDoors += part.roomDoors;
            total.backs += part.backs;
            total.colliders += part.colliders;
            total.readableMeshes += part.readableMeshes;
        }
    }
}
