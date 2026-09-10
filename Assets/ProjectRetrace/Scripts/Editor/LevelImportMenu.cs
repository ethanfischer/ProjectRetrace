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
        /// <summary>Each storey is its own art scene, authored at ground level so the artist
        /// works in one place; the import lifts it to its elevation. The storey height is the
        /// pack's wall height, which stacks a floor plate exactly on the walls below.</summary>
        private sealed class Floor
        {
            public string ScenePath;
            public string RootName;
            public float Elevation;
            public bool IsGround => Elevation == 0f;
        }

        private const float StoreyHeight = 2.5f;
        private static readonly Floor GroundFloor = new Floor
        {
            ScenePath = "Assets/Scenes/HomeInterior_FirstFloor.unity",
            RootName = "TestHouse (HomeInterior_FirstFloor)",
            Elevation = 0f,
        };
        private static readonly Floor UpperFloor = new Floor
        {
            ScenePath = "Assets/Scenes/HomeInterior_SecondFloor.unity",
            RootName = "TestHouse (HomeInterior_SecondFloor)",
            Elevation = StoreyHeight,
        };
        private static readonly Floor[] Floors = { GroundFloor, UpperFloor };

        private const string AdditionsRootName = "TestHouse (Additions)";
        private const string GeneratedRootPrefix = "TestHouse (seed";
        private const string KeySpotName = "KeySpot";
        private const string PackPrefabs = "Assets/LowPolyInterior/Prefabs/";

        /// <summary>The pack ships most cabinets twice, once as a single mesh and once with
        /// the doors and drawers as separate parts. The art scene uses the single-mesh ones
        /// in places the game wants searchable, so those are swapped for their twins on
        /// import; each pair shares a footprint and facing, bar the two noted.</summary>
        private static readonly Dictionary<string, (string twin, float yaw)> InteractiveTwins = new Dictionary<string, (string, float)>
        {
            { "Kitchen/Oven", ("InteractiveFurniture/Oven_02", 0f) },
            { "Bathroom/ShowerTable_02", ("InteractiveFurniture/ShowerTable_04", 0f) },
            { "Kitchen/KitchenTabletop2_03", ("InteractiveFurniture/InteractiveFurniture_10", 0f) },
            { "Room/RoomFurniture_07", ("InteractiveFurniture/InteractiveFurniture_04", 0f) },
            // 23 cm wider than the original.
            { "Room/RoomFurniture_06", ("InteractiveFurniture/InteractiveFurniture_05", 0f) },
            // 21 cm taller than the original; anything sitting on top needs lifting.
            { "Room/RoomFurniture_05", ("InteractiveFurniture/InteractiveFurniture_05", 0f) },
            { "Room/OfficeTable_02", ("InteractiveFurniture/InteractiveFurniture_07", 0f) },
            // The upper floor's wardrobes, nightstands and bedside chests.
            { "Room/RoomFurniture_08", ("InteractiveFurniture/InteractiveFurniture_06", 0f) },
            { "Room/Bed_Table_01", ("InteractiveFurniture/InteractiveFurniture_02", 0f) },
            { "Room/RoomFurniture_04", ("InteractiveFurniture/InteractiveFurniture_03", 0f) },
        };
        private const string InteractivePrefabFolder = "Assets/LowPolyInterior/Prefabs/InteractiveFurniture/";

        /// <summary>The pack paints a twin from the same palette atlas as everything else,
        /// so a twin whose UVs land on a different swatch than the static original comes in
        /// the wrong colour; the kitchen's one such twin is brown in a row of blue. These
        /// materials carry a copy of the atlas with the offending swatches repainted.</summary>
        private static readonly Dictionary<string, string> TwinMaterials = new Dictionary<string, string>
        {
            { "Kitchen/KitchenTabletop2_03", "Assets/ProjectRetrace/Art/Materials/KitchenCabinetBlue.mat" },
        };
        private const string PackMainMaterialName = "LowPolyInterior_MAIN";

        /// <summary>The pack's room doors share one mesh and hinge layout under different
        /// numbers; the art scenes use both.</summary>
        private static readonly HashSet<string> RoomDoorPrefabPaths = new HashSet<string>
        {
            "Assets/LowPolyInterior/Prefabs/Walls/Door_04.prefab",
            "Assets/LowPolyInterior/Prefabs/Walls/Door_05.prefab",
        };
        private const string BackMaterialPath = "Assets/ProjectRetrace/Art/Materials/FurnitureBack.mat";
        private const string BackName = "Back";
        private const string LandingName = "Stair Landing";

        /// <summary>Art-scene roots the import switches off by name, each with its reason.
        /// The list is the record of what was hand-deleted from the playable copy, so a
        /// re-import cannot quietly bring it back.</summary>
        private static readonly Dictionary<string, string> DroppedProps = new Dictionary<string, string>();

        /// <summary>The upper floor's plate is tiled straight across the stair flight below
        /// it. Any tile covering this much of the flight's footprint is cut out to make the
        /// stairwell; the sliver a tile shares with the flight's edge rail is left alone.</summary>
        private const float StairwellTileOverlap = 0.25f;

        /// <summary>The plate sits only 2.4 m over the ground, so a player on the first few
        /// treads still has a tile above them at head height; the cut runs this far past the
        /// bottom of the flight (one tile row) to open that up.</summary>
        private const float StairwellRunPastBottom = 1.2f;


        private static readonly string[] MeshCollisionPrefixes = { "Floor", "Wall", "Corner", "Stairs", "Door" };

        /// <summary>Wall art gets no collision: nothing hides in a picture, no ray needs to
        /// land on one, and the row hung over the stair flight was at head height.</summary>
        private static readonly string[] NoCollisionPrefixes = { "Picture" };

        // Profile boxing: a static mesh is sliced into horizontal slabs this thick, slabs
        // whose footprints agree within the tolerance merge, and each run becomes a box.
        private const float ProfileSlabHeight = 0.1f;
        private const float ProfileMergeTolerance = 0.06f;
        private const int MaxProfileBoxes = 6;
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
            public int props, doors, drawers, keySpots, hidingSpots, roomDoors, backs, colliders, boxed, tuned, readableMeshes, swapped, stairs, batched;
            public override string ToString() =>
                $"{swapped} static prop(s) swapped for interactive twins; {props} prop(s): {doors} door(s), {drawers} drawer(s), {keySpots} key spot(s), " +
                $"{hidingSpots} hiding spot(s), {roomDoors} room door(s), {backs} back(s); {colliders} collider(s) added, {boxed} part(s) boxed, {tuned} hand-tuned part(s) kept, " +
                $"{readableMeshes} mesh import(s) made readable, {stairs} stair flight(s) marked, {batched} renderer(s) marked static for batching";
        }

        [MenuItem("ProjectRetrace/Level/Import HomeInterior_FirstFloor", false, 42)]
        public static void ImportFirstFloor() => ImportFloor(GroundFloor);

        [MenuItem("ProjectRetrace/Level/Import HomeInterior_SecondFloor", false, 43)]
        public static void ImportSecondFloor() => ImportFloor(UpperFloor);

        [MenuItem("ProjectRetrace/Level/Import Both Floors", false, 44)]
        public static void ImportBothFloors()
        {
            foreach (var floor in Floors)
            {
                if (!ImportFloor(floor)) return;
            }
        }

        private static bool ImportFloor(Floor floor)
        {
            var target = SceneManager.GetActiveScene();
            if (target.path == floor.ScenePath || SceneManager.GetSceneByPath(floor.ScenePath).isLoaded)
            {
                EditorUtility.DisplayDialog(
                    "ProjectRetrace",
                    $"Open the gameplay scene (Main) as the active scene, with {System.IO.Path.GetFileNameWithoutExtension(floor.ScenePath)} closed, then import.",
                    "OK");
                return false;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Import " + floor.RootName);

            var source = EditorSceneManager.OpenScene(floor.ScenePath, OpenSceneMode.Additive);
            var sourcePaths = HierarchyPaths(source.GetRootGameObjects());

            var tunedCollision = HarvestTunedCollision(target, floor);
            var firstImport = RemovePreviousHouse(target, floor, sourcePaths, out var rescued);

            var root = new GameObject(floor.RootName);
            Undo.RegisterCreatedObjectUndo(root, "Import level");
            root.transform.position = Vector3.up * floor.Elevation;

            foreach (var sceneObject in source.GetRootGameObjects())
            {
                if (sceneObject.GetComponent<Camera>() != null || HasComponentNamed(sceneObject, "Volume")) continue;
                if (!floor.IsGround && IsDirectionalLight(sceneObject)) continue;
                Undo.MoveGameObjectToScene(sceneObject, target, "Import level");
                Undo.SetTransformParent(sceneObject.transform, root.transform, false, "Import level");
            }

            EditorSceneManager.CloseScene(source, true);

            var summary = PrepareFurniture(root.transform);
            var restored = RestoreTunedCollision(root.transform, tunedCollision);
            ShelveDroppedProps(root.transform);
            var stairwells = JoinFloors(target);
            if (firstImport && floor.IsGround) PlaceSpawnPoint();

            EditorSceneManager.MarkSceneDirty(target);
            Selection.activeGameObject = root;
            Debug.Log($"[ProjectRetrace] Imported {floor.ScenePath}: {summary}; {restored}/{tunedCollision.Count} hand-tuned collider set(s) carried over; {stairwells}.");
            if (rescued.Count > 0)
            {
                Debug.LogWarning($"[ProjectRetrace] Moved {rescued.Count} hand-placed object(s) out of the old import into " +
                                 $"'{AdditionsRootName}': {string.Join(", ", rescued)}. Keep additions under that root.");
            }

            return true;
        }

        [MenuItem("ProjectRetrace/Level/Prepare LowPoly Furniture", false, 45)]
        public static void PrepareAllHouses()
        {
            var summary = new Summary();
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (!root.name.StartsWith("TestHouse")) continue;
                Accumulate(ref summary, PrepareFurniture(root.transform));
            }

            var stairwells = JoinFloors(SceneManager.GetActiveScene());
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[ProjectRetrace] Prepared LowPoly furniture: {summary}; {stairwells}.");
        }

        /// <summary>Each storey above the ground gets its stairwell cut. Runs after every
        /// import, so whichever floor arrives second finds the other. Locking the stairs
        /// is the hand-placed FloorGate's job, under the Additions root.</summary>
        private static string JoinFloors(Scene scene)
        {
            var ground = FindRoot(scene, GroundFloor.RootName);
            var upper = FindRoot(scene, UpperFloor.RootName);
            if (ground == null || upper == null) return "no stairwell (one floor only)";

            var cut = CutStairwell(upper, ground, UpperFloor.Elevation);
            return $"{cut} floor tile(s) cut for the stairwell";
        }

        private static Transform FindRoot(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root.transform;
            }

            return null;
        }

        /// <summary>The upper floor's plate was tiled straight across the flight, so the tiles
        /// covering it come out, and where the hole runs past the top step a plain landing
        /// slab bridges the gap. The rule re-applies on every import, so once the artist cuts
        /// a proper stairwell herself nothing is left to remove and no landing is added.</summary>
        private static int CutStairwell(Transform upper, Transform ground, float elevation)
        {
            var cut = 0;
            foreach (var flight in StairFlights(ground))
            {
                var footprint = WorldBounds(flight);
                var alongX = footprint.size.x >= footprint.size.z;
                var topAtMax = TreadHeight(flight, footprint, alongX, true) > TreadHeight(flight, footprint, alongX, false);
                var cutArea = ExtendPastBottom(footprint, alongX, topAtMax);
                Bounds hole = default;
                var holeFound = false;
                foreach (var filter in upper.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!IsFloorTile(filter, elevation)) continue;
                    var tile = filter.GetComponent<Renderer>().bounds;
                    if (OverlapXZ(tile, cutArea) < StairwellTileOverlap * tile.size.x * tile.size.z) continue;

                    if (holeFound) hole.Encapsulate(tile);
                    else { hole = tile; holeFound = true; }
                    Undo.DestroyObjectImmediate(filter.gameObject);
                    cut++;
                }

                if (holeFound)
                {
                    ShelvePropsOverHole(upper, hole, elevation);
                    AddLanding(upper, footprint, hole, alongX, topAtMax);
                }
            }

            return cut;
        }

        /// <summary>MarkStairs tags the flight and its side rail alike; only the flight has
        /// treads to walk, and it is the one whose footprint the stairwell follows.</summary>
        private static List<Transform> StairFlights(Transform ground)
        {
            var flights = new List<Transform>();
            foreach (var stairs in ground.GetComponentsInChildren<Stairs>(true))
            {
                if (stairs.name.StartsWith("StairsPart") || stairs.name == LandingName) continue;
                flights.Add(stairs.transform);
            }

            return flights;
        }

        private static Bounds ExtendPastBottom(Bounds footprint, bool alongX, bool topAtMax)
        {
            var run = (alongX ? Vector3.right : Vector3.forward) * StairwellRunPastBottom;
            var extended = footprint;
            extended.Encapsulate(topAtMax ? footprint.min - run : footprint.max + run);
            return extended;
        }

        private static bool IsFloorTile(MeshFilter filter, float elevation)
        {
            if (!filter.name.StartsWith("Floor") || filter.sharedMesh == null) return false;
            var renderer = filter.GetComponent<Renderer>();
            if (renderer == null) return false;
            var bounds = renderer.bounds;
            return bounds.size.y < 0.3f && Mathf.Abs(bounds.max.y - elevation) < 0.2f;
        }

        private static float OverlapXZ(Bounds a, Bounds b)
        {
            var x = Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x);
            var z = Mathf.Min(a.max.z, b.max.z) - Mathf.Max(a.min.z, b.min.z);
            return x > 0f && z > 0f ? x * z : 0f;
        }

        /// <summary>The flight's top end is whichever end its treads are highest at; the
        /// landing fills the hole from that end to the hole's edge, across the hole's
        /// width, at the plate's own thickness. It counts as stairs so the controller's
        /// stair step height covers the lip between the top tread and the slab.</summary>
        private static void AddLanding(Transform upper, Bounds footprint, Bounds hole, bool alongX, bool topAtMax)
        {
            if (upper.Find(LandingName) != null) return;

            var landing = new Bounds();
            if (alongX)
            {
                var from = topAtMax ? footprint.max.x : hole.min.x;
                var to = topAtMax ? hole.max.x : footprint.min.x;
                landing.SetMinMax(new Vector3(from, hole.min.y, hole.min.z), new Vector3(to, hole.max.y, hole.max.z));
            }
            else
            {
                var from = topAtMax ? footprint.max.z : hole.min.z;
                var to = topAtMax ? hole.max.z : footprint.min.z;
                landing.SetMinMax(new Vector3(hole.min.x, hole.min.y, from), new Vector3(hole.max.x, hole.max.y, to));
            }

            if (landing.size.x < 0.05f || landing.size.z < 0.05f) return;

            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = LandingName;
            Undo.RegisterCreatedObjectUndo(slab, "Add stair landing");
            slab.transform.SetParent(upper, true);
            slab.transform.position = landing.center;
            slab.transform.localScale = landing.size;
            slab.GetComponent<Renderer>().sharedMaterial = BackMaterial();
            slab.AddComponent<Stairs>();
        }

        private static float TreadHeight(Transform flight, Bounds footprint, bool alongX, bool atMax)
        {
            Physics.SyncTransforms();
            var inset = 0.3f;
            var x = alongX ? (atMax ? footprint.max.x - inset : footprint.min.x + inset) : footprint.center.x;
            var z = alongX ? footprint.center.z : (atMax ? footprint.max.z - inset : footprint.min.z + inset);
            var origin = new Vector3(x, footprint.max.y + 0.5f, z);
            var best = float.MinValue;
            foreach (var hit in Physics.RaycastAll(origin, Vector3.down, footprint.size.y + 1f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(flight)) best = Mathf.Max(best, hit.point.y);
            }

            return best;
        }

        /// <summary>The artist dressed the tiles that just came out, so whatever stood on
        /// them now hangs in the air over the flight, and its colliders sit under the agent
        /// height above the treads: they pinch the navmesh off the stairs entirely. Those
        /// props are switched off rather than moved -- placing them is hers -- and named,
        /// so moving them in the art scene brings them back on the next import.</summary>
        private static void ShelvePropsOverHole(Transform upper, Bounds hole, float elevation)
        {
            var shelved = new List<Transform>();
            foreach (Transform child in upper)
            {
                if (child.name.StartsWith("Wall") || child.name.StartsWith("Floor") || child.name == LandingName) continue;
                if (child.GetComponentsInChildren<Renderer>(true).Length == 0) continue;
                var bounds = WorldBounds(child);
                var standsOnPlate = Mathf.Abs(bounds.min.y - elevation) < 0.1f;
                if (standsOnPlate && OverlapXZ(bounds, hole) > 0f) shelved.Add(child);
            }

            ShelveWhatRestsOn(upper, shelved);
            if (shelved.Count == 0) return;

            var names = new List<string>();
            foreach (var prop in shelved)
            {
                Undo.RecordObject(prop.gameObject, "Shelve prop over stairwell");
                prop.gameObject.SetActive(false);
                names.Add(prop.name);
            }

            Debug.LogWarning($"[ProjectRetrace] {names.Count} prop(s) stood over the stairwell cut from the upper floor and were switched off; move them in the art scene: {string.Join(", ", names)}.");
        }

        private static void ShelveDroppedProps(Transform house)
        {
            foreach (Transform child in house)
            {
                if (!DroppedProps.TryGetValue(child.name, out var reason)) continue;
                Undo.RecordObject(child.gameObject, "Shelve dropped prop");
                child.gameObject.SetActive(false);
                Debug.Log($"[ProjectRetrace] Switched off '{child.name}' ({reason}).");
            }
        }

        /// <summary>A lamp or photo on a shelved nightstand would be left floating where
        /// the nightstand was, so whatever sits on a shelved prop's top goes with it. Only
        /// the furniture is checked, not what it carried: a wall picture hung just above a
        /// plant reads as resting on it and would be chained off with it.</summary>
        private static void ShelveWhatRestsOn(Transform upper, List<Transform> shelved)
        {
            var furniture = shelved.Count;
            for (var i = 0; i < furniture; i++)
            {
                var top = WorldBounds(shelved[i]);
                foreach (Transform child in upper)
                {
                    if (shelved.Contains(child) || child.GetComponentsInChildren<Renderer>(true).Length == 0) continue;
                    var bounds = WorldBounds(child);
                    var restsOnTop = Mathf.Abs(bounds.min.y - top.max.y) < 0.06f;
                    var withinFootprint = bounds.center.x > top.min.x && bounds.center.x < top.max.x && bounds.center.z > top.min.z && bounds.center.z < top.max.z;
                    if (restsOnTop && withinFootprint) shelved.Add(child);
                }
            }
        }

        private static bool IsDirectionalLight(GameObject sceneObject)
        {
            var light = sceneObject.GetComponent<Light>();
            return light != null && light.type == LightType.Directional;
        }

        /// <summary>The generated house, its dev grid and its point light were all stand-ins
        /// for a real level; the imported scene brings its own lighting. Returns true when
        /// no earlier import of this floor existed, which is the only time the spawn point
        /// may be moved.</summary>
        private static bool RemovePreviousHouse(Scene scene, Floor floor, HashSet<string> sourcePaths, out List<string> rescued)
        {
            rescued = new List<string>();
            var firstImport = true;
            foreach (var root in scene.GetRootGameObjects())
            {
                var isImport = root.name == floor.RootName;
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
        /// there by hand. Prepare's own KeySpot, Back and landing objects are the exception.</summary>
        private static void RescueAdditions(Scene scene, Transform oldRoot, HashSet<string> sourcePaths, List<string> rescued)
        {
            Transform additions = null;
            foreach (var transform in oldRoot.GetComponentsInChildren<Transform>(true))
            {
                if (transform == oldRoot || transform.name == KeySpotName || transform.name == BackName || transform.name == LandingName) continue;
                if (PrefabUtility.IsPartOfPrefabInstance(transform) && !PrefabUtility.IsAnyPrefabInstanceRoot(transform.gameObject)) continue;
                // A twin's doors and drawers are nested model instances, so they count as
                // prefab roots of their own; what marks them as hers is the prop they sit in.
                var outermost = PrefabUtility.GetOutermostPrefabInstanceRoot(transform.gameObject);
                if (outermost != null && outermost != transform.gameObject && sourcePaths.Contains(PathUnder(oldRoot, outermost.transform))) continue;
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
            summary.swapped = SwapStaticPropsForTwins(house);
            (summary.colliders, summary.boxed, summary.tuned) = FitColliders(house);
            summary.readableMeshes = MakeCollisionMeshesReadable(house);
            summary.stairs = MarkStairs(house);
            summary.batched = MarkStaticForBatching(house);
            foreach (var transform in house.GetComponentsInChildren<Transform>(true))
            {
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(transform.gameObject)) continue;

                var assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(transform.gameObject);
                if (RoomDoorPrefabPaths.Contains(assetPath))
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

        /// <summary>Nearly a thousand small meshes, each drawn once per shadow cascade on
        /// top of the main pass, is the whole frame cost of this house; static batching
        /// folds everything that never moves into a few combined meshes. Only the parts
        /// that swing or slide -- anything carrying an interactable -- are left out.</summary>
        private static int MarkStaticForBatching(Transform house)
        {
            var marked = 0;
            foreach (var renderer in house.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.GetComponentInParent<InteractableBase>(true) != null) continue;
                var flags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
                if ((flags & StaticEditorFlags.BatchingStatic) != 0) continue;
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, flags | StaticEditorFlags.BatchingStatic);
                marked++;
            }

            return marked;
        }

        /// <summary>The pack names its flights "Stairs_NN" and their side panels
        /// "StairsPart_NN"; both get the marker, since the panel's collider is what the
        /// controller's probe meets first from the hall.</summary>
        private static int MarkStairs(Transform house)
        {
            var marked = 0;
            foreach (var transform in house.GetComponentsInChildren<Transform>(true))
            {
                if (!transform.name.StartsWith("Stairs") || transform.GetComponent<Stairs>() != null) continue;
                transform.gameObject.AddComponent<Stairs>();
                marked++;
            }

            return marked;
        }

        /// <summary>Keeps the original's name and transform so the swapped prop still matches
        /// its path in the art scene, which is how a re-import tells her objects from
        /// hand-placed ones.</summary>
        private static int SwapStaticPropsForTwins(Transform house)
        {
            var swapped = 0;
            foreach (var transform in house.GetComponentsInChildren<Transform>(true))
            {
                if (transform == null || !PrefabUtility.IsAnyPrefabInstanceRoot(transform.gameObject)) continue;

                var assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(transform.gameObject);
                if (!assetPath.StartsWith(PackPrefabs)) continue;

                var key = assetPath.Substring(PackPrefabs.Length).Replace(".prefab", "");
                if (!InteractiveTwins.TryGetValue(key, out var entry)) continue;

                var twinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PackPrefabs + entry.twin + ".prefab");
                if (twinPrefab == null)
                {
                    Debug.LogWarning($"[ProjectRetrace] Interactive twin '{entry.twin}' for '{key}' not found; leaving it static.");
                    continue;
                }

                var twin = (GameObject)PrefabUtility.InstantiatePrefab(twinPrefab, transform.gameObject.scene);
                Undo.RegisterCreatedObjectUndo(twin, "Swap static prop");
                twin.name = transform.name;
                twin.transform.SetParent(transform.parent, false);
                twin.transform.SetLocalPositionAndRotation(transform.localPosition, transform.localRotation * Quaternion.Euler(0f, entry.yaw, 0f));
                twin.transform.localScale = transform.localScale;
                twin.transform.SetSiblingIndex(transform.GetSiblingIndex());
                LiftWhatRestsOn(house, WorldBounds(transform), WorldBounds(twin.transform));
                RecolourTwin(twin, key);
                Undo.DestroyObjectImmediate(transform.gameObject);
                swapped++;
            }

            return swapped;
        }

        private static void RecolourTwin(GameObject twin, string key)
        {
            if (!TwinMaterials.TryGetValue(key, out var materialPath)) return;

            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Debug.LogWarning($"[ProjectRetrace] Recolour material '{materialPath}' for '{key}' not found; leaving the pack colour.");
                return;
            }

            foreach (var renderer in twin.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                var changed = false;
                for (var i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null || materials[i].name != PackMainMaterialName) continue;
                    materials[i] = material;
                    changed = true;
                }

                if (changed) renderer.sharedMaterials = materials;
            }
        }

        /// <summary>A taller twin would swallow the lamp or TV the artist stood on the
        /// original, so anything whose base sat on the old top within its footprint rides
        /// up by the difference.</summary>
        private static void LiftWhatRestsOn(Transform house, Bounds oldBounds, Bounds newBounds)
        {
            var lift = newBounds.max.y - oldBounds.max.y;
            if (lift < 0.02f) return;

            var lifted = new HashSet<Transform>();
            foreach (var renderer in house.GetComponentsInChildren<Renderer>(true))
            {
                var root = PrefabUtility.GetNearestPrefabInstanceRoot(renderer.gameObject);
                if (root == null || lifted.Contains(root.transform)) continue;
                var b = renderer.bounds;
                var restsOnTop = Mathf.Abs(b.min.y - oldBounds.max.y) < 0.06f;
                var withinFootprint = b.center.x > oldBounds.min.x && b.center.x < oldBounds.max.x && b.center.z > oldBounds.min.z && b.center.z < oldBounds.max.z;
                if (!restsOnTop || !withinFootprint) continue;

                Undo.RecordObject(root.transform, "Lift prop onto taller twin");
                root.transform.position += Vector3.up * lift;
                lifted.Add(root.transform);
            }
        }

        private static Bounds WorldBounds(Transform prop)
        {
            var renderers = prop.GetComponentsInChildren<Renderer>(true);
            var bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(prop.position, Vector3.zero);
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        /// <summary>Static props collide as a few BoxColliders fitted to each mesh part's
        /// vertical profile (see ProfileBoxes); only the shell,
        /// the stairs, the room doors and the interactive furniture keep mesh collision.
        /// The pack's chairs and cushions are sloped enough that a CharacterController
        /// walks up them at any step height, and a box has no slope to climb. Interactive
        /// furniture stays mesh because its interior has to be open: the interaction ray
        /// must reach a key inside a drawer, and a hider has to fit in the cupboard.</summary>
        private static (int added, int boxed, int tuned) FitColliders(Transform house)
        {
            var added = 0;
            var boxed = 0;
            var tuned = 0;
            foreach (var filter in house.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.name == BackName) continue;

                var existing = filter.GetComponent<Collider>();
                if (SkipsCollision(house, filter.transform))
                {
                    StripCollision(filter.gameObject);
                    continue;
                }

                if (KeepsMeshCollision(house, filter.transform))
                {
                    if (UndoMistakenBoxes(filter.gameObject)) existing = null;
                    if (existing != null) continue;
                    Undo.AddComponent<MeshCollider>(filter.gameObject);
                    added++;
                    continue;
                }

                var wanted = ProfileBoxes(filter.sharedMesh);
                if (IsHandTuned(filter.gameObject, wanted))
                {
                    tuned++;
                    continue;
                }

                if (HasBoxes(filter.gameObject, wanted))
                {
                    Receipt(filter.gameObject).Record(filter.GetComponents<BoxCollider>());
                    continue;
                }

                var colliders = filter.GetComponents<Collider>();
                if (colliders.Length == 0) added++;
                else boxed++;
                foreach (var collider in colliders) Undo.DestroyObjectImmediate(collider);
                foreach (var bounds in wanted)
                {
                    var box = Undo.AddComponent<BoxCollider>(filter.gameObject);
                    box.center = bounds.center;
                    box.size = bounds.size;
                }

                Receipt(filter.gameObject).Record(filter.GetComponents<BoxCollider>());
            }

            return (added, boxed, tuned);
        }

        /// <summary>Boxes that differ from the receipt were edited by hand. A part with
        /// boxes but no receipt predates receipts: adopt it as tuned if it differs from
        /// the profile, since that is the only way it could have come to differ.</summary>
        private static bool IsHandTuned(GameObject target, List<Bounds> wanted)
        {
            var boxes = target.GetComponents<BoxCollider>();
            if (boxes.Length == 0) return false;

            var receipt = target.GetComponent<CollisionFit>();
            if (receipt != null && receipt.handTuned) return true;
            if (receipt != null ? receipt.Matches(boxes) : HasBoxes(target, wanted)) return false;

            receipt = Receipt(target);
            receipt.handTuned = true;
            receipt.Record(boxes);
            EditorUtility.SetDirty(receipt);
            return true;
        }

        private static CollisionFit Receipt(GameObject target)
        {
            var receipt = target.GetComponent<CollisionFit>();
            if (receipt == null) receipt = Undo.AddComponent<CollisionFit>(target);
            EditorUtility.SetDirty(receipt);
            return receipt;
        }

        /// <summary>Hand-tuned box sets under the current import, keyed by path, so the
        /// re-import can put them back on the same parts of the fresh copy.</summary>
        private static Dictionary<string, CollisionFit> HarvestTunedCollision(Scene scene, Floor floor)
        {
            var tuned = new Dictionary<string, CollisionFit>();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name != floor.RootName) continue;
                foreach (var fit in root.GetComponentsInChildren<CollisionFit>(true))
                {
                    if (!fit.handTuned) continue;
                    var copy = new GameObject("tuned collision").AddComponent<CollisionFit>();
                    copy.gameObject.hideFlags = HideFlags.HideAndDontSave;
                    copy.handTuned = true;
                    copy.centers.AddRange(fit.centers);
                    copy.sizes.AddRange(fit.sizes);
                    tuned[PathUnder(root.transform, fit.transform)] = copy;
                }
            }

            return tuned;
        }

        private static int RestoreTunedCollision(Transform root, Dictionary<string, CollisionFit> tuned)
        {
            var restored = 0;
            foreach (var (path, saved) in tuned)
            {
                var part = root.Find(path);
                if (part != null)
                {
                    foreach (var collider in part.GetComponents<Collider>()) Undo.DestroyObjectImmediate(collider);
                    for (var i = 0; i < saved.centers.Count; i++)
                    {
                        var box = Undo.AddComponent<BoxCollider>(part.gameObject);
                        box.center = saved.centers[i];
                        box.size = saved.sizes[i];
                    }

                    var receipt = Receipt(part.gameObject);
                    receipt.handTuned = true;
                    receipt.Record(part.GetComponents<BoxCollider>());
                    restored++;
                }
                else
                {
                    Debug.LogWarning($"[ProjectRetrace] Hand-tuned colliders for \"{path}\" had no matching part in the new import and were dropped.");
                }

                Object.DestroyImmediate(saved.gameObject);
            }

            return restored;
        }

        private static bool HasBoxes(GameObject target, List<Bounds> wanted)
        {
            var boxes = target.GetComponents<BoxCollider>();
            if (boxes.Length != wanted.Count || target.GetComponents<Collider>().Length != boxes.Length) return false;
            for (var i = 0; i < boxes.Length; i++)
            {
                if ((boxes[i].center - wanted[i].center).sqrMagnitude > 1e-6f) return false;
                if ((boxes[i].size - wanted[i].size).sqrMagnitude > 1e-6f) return false;
            }

            return true;
        }

        /// <summary>One box per mesh is too coarse for anything with an overhang: a chair's
        /// single box fills the space over its arms and blocks the interaction ray to the
        /// drawer behind it. Slicing the mesh into horizontal slabs and boxing each run of
        /// matching footprints gives a chair its base, seat and backrest, and a lamp its
        /// pole and shade, with nothing sloped for the player to climb. Footprints come
        /// from points sampled along every triangle edge, not the vertices alone: a tall
        /// backrest is one quad with corners at its top and bottom, and the slabs between
        /// would otherwise see nothing of it.</summary>
        private static List<Bounds> ProfileBoxes(Mesh mesh)
        {
            var whole = mesh.bounds;
            var slabCount = Mathf.Clamp(Mathf.CeilToInt(whole.size.y / ProfileSlabHeight), 1, 40);
            if (slabCount < 3) return new List<Bounds> { whole };

            var slabHeight = whole.size.y / slabCount;
            var min = new Vector2[slabCount];
            var max = new Vector2[slabCount];
            var filled = new bool[slabCount];
            for (var i = 0; i < slabCount; i++)
            {
                min[i] = new Vector2(float.MaxValue, float.MaxValue);
                max[i] = new Vector2(float.MinValue, float.MinValue);
            }

            void Accumulate(Vector3 point)
            {
                var i = Mathf.Clamp(Mathf.FloorToInt((point.y - whole.min.y) / slabHeight), 0, slabCount - 1);
                min[i] = Vector2.Min(min[i], new Vector2(point.x, point.z));
                max[i] = Vector2.Max(max[i], new Vector2(point.x, point.z));
                filled[i] = true;
            }

            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            for (var t = 0; t < triangles.Length; t += 3)
            {
                for (var e = 0; e < 3; e++)
                {
                    var a = vertices[triangles[t + e]];
                    var b = vertices[triangles[t + (e + 1) % 3]];
                    var steps = Mathf.CeilToInt(Mathf.Abs(b.y - a.y) / (slabHeight * 0.5f)) + 1;
                    for (var k = 0; k <= steps; k++) Accumulate(Vector3.Lerp(a, b, (float)k / steps));
                }
            }

            for (var i = 1; i < slabCount; i++)
            {
                if (filled[i]) continue;
                min[i] = min[i - 1];
                max[i] = max[i - 1];
                filled[i] = true;
            }

            var runs = new List<(int first, int last, Vector2 min, Vector2 max)>();
            for (var i = 0; i < slabCount; i++)
            {
                if (!filled[i]) continue;
                if (runs.Count > 0 && FootprintsAgree(runs[runs.Count - 1].min, runs[runs.Count - 1].max, min[i], max[i]))
                {
                    var run = runs[runs.Count - 1];
                    runs[runs.Count - 1] = (run.first, i, Vector2.Min(run.min, min[i]), Vector2.Max(run.max, max[i]));
                }
                else
                {
                    runs.Add((i, i, min[i], max[i]));
                }
            }

            while (runs.Count > MaxProfileBoxes) MergeCheapestNeighbours(runs);

            var boxes = new List<Bounds>();
            foreach (var run in runs)
            {
                var bottom = whole.min.y + run.first * slabHeight;
                var top = whole.min.y + (run.last + 1) * slabHeight;
                var box = new Bounds();
                box.SetMinMax(new Vector3(run.min.x, bottom, run.min.y), new Vector3(run.max.x, top, run.max.y));
                boxes.Add(box);
            }

            return boxes;
        }

        private static bool FootprintsAgree(Vector2 minA, Vector2 maxA, Vector2 minB, Vector2 maxB)
        {
            return (minA - minB).magnitude <= ProfileMergeTolerance && (maxA - maxB).magnitude <= ProfileMergeTolerance;
        }

        private static void MergeCheapestNeighbours(List<(int first, int last, Vector2 min, Vector2 max)> runs)
        {
            var bestIndex = 0;
            var bestCost = float.MaxValue;
            for (var i = 0; i + 1 < runs.Count; i++)
            {
                var a = runs[i];
                var b = runs[i + 1];
                var union = Vector2.Max(a.max, b.max) - Vector2.Min(a.min, b.min);
                var cost = union.x * union.y * (b.last - a.first + 1) - (a.max - a.min).x * (a.max - a.min).y * (a.last - a.first + 1)
                    - (b.max - b.min).x * (b.max - b.min).y * (b.last - b.first + 1);
                if (cost >= bestCost) continue;
                bestCost = cost;
                bestIndex = i;
            }

            var merged = runs[bestIndex];
            var next = runs[bestIndex + 1];
            runs[bestIndex] = (merged.first, next.last, Vector2.Min(merged.min, next.min), Vector2.Max(merged.max, next.max));
            runs.RemoveAt(bestIndex + 1);
        }

        /// <summary>Boxes Prepare fitted to a part that must stay mesh, before it knew the
        /// part was interactive. Hand-tuned boxes are the owner's and stay.</summary>
        private static bool UndoMistakenBoxes(GameObject part)
        {
            var receipt = part.GetComponent<CollisionFit>();
            if (receipt == null || receipt.handTuned) return false;
            foreach (var box in part.GetComponents<BoxCollider>()) Undo.DestroyObjectImmediate(box);
            Undo.DestroyObjectImmediate(receipt);
            return part.GetComponent<Collider>() == null;
        }

        /// <summary>A twin's doors and drawers are nested model instances, so the nearest
        /// prefab root names the FBX, not the interactive prefab; the outermost root does.</summary>
        private static bool KeepsMeshCollision(Transform house, Transform part)
        {
            var top = TopLevelAncestor(house, part);
            foreach (var prefix in MeshCollisionPrefixes)
            {
                if (top.name.StartsWith(prefix)) return true;
            }

            if (!PrefabUtility.IsPartOfPrefabInstance(part.gameObject)) return false;
            if (IsInteractiveAsset(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(part.gameObject))) return true;
            var outermost = PrefabUtility.GetOutermostPrefabInstanceRoot(part.gameObject);
            return outermost != null && IsInteractiveAsset(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(outermost));
        }

        private static bool SkipsCollision(Transform house, Transform part)
        {
            var top = TopLevelAncestor(house, part);
            foreach (var prefix in NoCollisionPrefixes)
            {
                if (top.name.StartsWith(prefix)) return true;
            }

            return false;
        }

        private static void StripCollision(GameObject part)
        {
            foreach (var collider in part.GetComponents<Collider>()) Undo.DestroyObjectImmediate(collider);
            var receipt = part.GetComponent<CollisionFit>();
            if (receipt != null) Undo.DestroyObjectImmediate(receipt);
        }

        private static Transform TopLevelAncestor(Transform house, Transform part)
        {
            var top = part;
            while (top.parent != null && top.parent != house) top = top.parent;
            return top;
        }

        private static bool IsInteractiveAsset(string assetPath)
        {
            return RoomDoorPrefabPaths.Contains(assetPath) || assetPath.StartsWith(InteractivePrefabFolder) || assetPath.Contains("/InteractiveFurniture/");
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
            if (prop.GetComponent<SearchableProp>() == null) Undo.AddComponent<SearchableProp>(prop);
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

        /// <summary>Union of the prop's mesh bounds in its own space, including a carcass
        /// mesh on the root itself (the oven and sinks keep it there). The pack's parts
        /// carry no rotation or scale, so a local offset is the whole transform.</summary>
        private static Bounds LocalBounds(Transform prop)
        {
            var bounds = new Bounds();
            var first = true;
            var rootFilter = prop.GetComponent<MeshFilter>();
            if (rootFilter != null && rootFilter.sharedMesh != null)
            {
                bounds = rootFilter.sharedMesh.bounds;
                first = false;
            }

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
            total.boxed += part.boxed;
            total.tuned += part.tuned;
            total.swapped += part.swapped;
            total.stairs += part.stairs;
            total.batched += part.batched;

        }
    }
}
