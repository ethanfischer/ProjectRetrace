using System.Collections.Generic;
using ProjectRetrace;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectRetrace.EditorTools
{
    /// <summary>
    /// Generates a two-story test house with furniture scattered as key hiding spots. Each floor
    /// is two rows of rooms; the row boundary and the column splits inside each row are all
    /// seeded independently, so rooms vary in size and the T-junctions stagger -- organic
    /// layouts without ever risking an unwalkable house.
    ///
    /// Exactly one doorway per shared wall (one per adjacent room pair). Staggered splits mean
    /// middle rooms often border two rooms across one line, which keeps most rooms at 3+ exits;
    /// corner rooms bottom out at 2.
    ///
    /// The stairs sit in their own ground-floor enclosure behind a door that stays locked
    /// until UpstairsUnlockRound, so the early rounds play out on one floor.
    ///
    /// Regenerating replaces the previous house; the seed is baked into the root name so a
    /// layout can be reported and reproduced.
    /// </summary>
    public static class HouseGeneratorMenu
    {
        private const string SeedPref = "ProjectRetrace.HouseSeed";
        private const float Thickness = 0.1f;
        private const float GroundTop = 0.05f;
        private const float UpperTop = 2.8f;
        private const float DoorWidth = 1.1f;
        private const int UpstairsUnlockRound = 4;

        // South face of the stair enclosure; step 1 begins at z 3.5, just past the door.
        private const float StairGateZ = 3.4f;

        // Footprint: x in [-9, 9], z in [-4, 10]. SpawnPoint (0, 0) lands in the entry room.
        private const float X0 = -9f, X1 = 9f, Z0 = -4f, Z1 = 10f;

        // Stairs hug the west wall; the stairwell hole in the upper slab is fixed regardless of
        // seed, so every layout constraint below just has to steer clear of this band.
        private static readonly Rect2 StairZone = new Rect2(X0, -7.6f, 3.2f, Z1);

        private struct Rect2
        {
            public float xMin, xMax, zMin, zMax;
            public Rect2(float xMin, float xMax, float zMin, float zMax)
            {
                this.xMin = xMin; this.xMax = xMax; this.zMin = zMin; this.zMax = zMax;
            }

            public bool Contains(Vector2 p) => p.x >= xMin && p.x <= xMax && p.y >= zMin && p.y <= zMax;

            public bool Overlaps(Rect2 o) => xMin < o.xMax && xMax > o.xMin && zMin < o.zMax && zMax > o.zMin;
        }

        private struct Room
        {
            public Rect2 bounds;
            public float floorY;
            public bool nearStairs;
        }

        [MenuItem("ProjectRetrace/Generate Test House (New Seed)", false, 40)]
        public static void GenerateNewSeed()
        {
            var seed = new System.Random().Next(0, 100000);
            EditorPrefs.SetInt(SeedPref, seed);
            Generate(seed);
        }

        [MenuItem("ProjectRetrace/Regenerate Test House (Last Seed)", false, 41)]
        public static void GenerateLastSeed() => Generate(EditorPrefs.GetInt(SeedPref, 12345));

        private static void Generate(int seed)
        {
            foreach (var existing in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (existing != null && existing.parent == null && existing.name.StartsWith("TestHouse"))
                {
                    Undo.DestroyObjectImmediate(existing.gameObject);
                }
            }

            var rnd = new System.Random(seed);
            var root = new GameObject($"TestHouse (seed {seed})");
            Undo.RegisterCreatedObjectUndo(root, "Generate Test House");

            var doorways = new List<Vector2>();
            var rooms = new List<Room>();

            BuildFloors(root);
            BuildStairs(root);

            // Ground floor: two rows of three rooms. The row boundary stops well south of the
            // stair gate so there is always a vestibule to stand in and open the door from;
            // a boundary flush against the gate would wall the stairs off for good.
            var groundWallH = UpperTop - 0.1f - GroundTop;
            BuildFloorPlan(root, rnd, doorways, rooms, "G", GroundTop, groundWallH,
                zSplit: Lerp(rnd, 0.5f, StairGateZ - 1.2f),
                bottomSplits: new[] { Lerp(rnd, -5f, -2f), Lerp(rnd, 1f, 4f) },
                topSplits: new[] { Lerp(rnd, -5f, -2f), Lerp(rnd, 1f, 4f) },
                frontDoorX: 0f);
            BuildStairEnclosure(root, doorways, groundWallH);

            // Upper floor: two rows of two rooms. The row boundary stays south of the stairwell
            // hole so no wall floats over it and the stair exit stays open.
            BuildFloorPlan(root, rnd, doorways, rooms, "U", UpperTop, 2.4f,
                zSplit: Lerp(rnd, 0.5f, 3.2f),
                bottomSplits: new[] { Lerp(rnd, -3f, 3f) },
                topSplits: new[] { Lerp(rnd, -3f, 3f) },
                frontDoorX: null);

            // Rail along the stairwell's east edge; stops short so the stair top opens east
            // onto the main slab.
            Box(root, "Stair Rail", new Vector3(-7.85f, UpperTop + 0.5f, 5.1f), new Vector3(Thickness, 1f, 3.2f));

            BuildLights(root, rooms);

            var furniture = new GameObject("Furniture").transform;
            furniture.SetParent(root.transform, false);
            var placed = new List<Vector2>();
            var total = 0;
            foreach (var room in rooms)
            {
                total += FillRoom(furniture, room, rnd, doorways, placed);
            }

            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Selection.activeGameObject = root;
            Debug.Log($"[ProjectRetrace] Generated TestHouse seed {seed}: {rooms.Count} rooms, {total} furniture props.");
        }

        /// <summary>
        /// Builds one floor's interior walls and room list from a row boundary plus independent
        /// column splits per row. One doorway per adjacent room pair: along the row boundary
        /// that is one door per bottom/top room overlap, so no wall ever carries two doors.
        /// </summary>
        private static void BuildFloorPlan(
            GameObject root, System.Random rnd, List<Vector2> doorways, List<Room> rooms,
            string prefix, float floorY, float wallH,
            float zSplit, float[] bottomSplits, float[] topSplits, float? frontDoorX)
        {
            // Exterior ring. Only the ground floor gets an outside door.
            WallAlongX(root, prefix + " South", Z0, X0, X1, floorY, wallH,
                frontDoorX.HasValue ? DoorsX(doorways, Z0, frontDoorX.Value) : System.Array.Empty<float>());
            WallAlongX(root, prefix + " North", Z1, X0, X1, floorY, wallH);
            WallAlongZ(root, prefix + " West", X0, Z0, Z1, floorY, wallH);
            WallAlongZ(root, prefix + " East", X1, Z0, Z1, floorY, wallH);

            var bottomRooms = RowRooms(bottomSplits, Z0, zSplit, floorY);
            var topRooms = RowRooms(topSplits, zSplit, Z1, floorY);
            rooms.AddRange(bottomRooms);
            rooms.AddRange(topRooms);

            // Column walls inside each row, one door each.
            BuildRowDividers(root, rnd, doorways, prefix + " Col S", bottomSplits, Z0, zSplit, floorY, wallH);
            BuildRowDividers(root, rnd, doorways, prefix + " Col N", topSplits, zSplit, Z1, floorY, wallH);

            // Row boundary: one door per bottom/top overlap, kept off the stair band.
            var doors = new List<float>();
            foreach (var b in bottomRooms)
            {
                foreach (var t in topRooms)
                {
                    var lo = Mathf.Max(b.bounds.xMin, t.bounds.xMin, StairZone.xMax);
                    var hi = Mathf.Min(b.bounds.xMax, t.bounds.xMax);
                    if (hi - lo < 2f) continue;
                    doors.Add(Lerp(rnd, lo + 0.8f, hi - 0.8f));
                }
            }

            WallAlongX(root, prefix + " Divider", zSplit, X0, X1, floorY, wallH, DoorsX(doorways, zSplit, doors.ToArray()));
        }

        private static List<Room> RowRooms(float[] splits, float z0, float z1, float floorY)
        {
            var edges = new List<float> { X0 };
            edges.AddRange(splits);
            edges.Add(X1);

            var rooms = new List<Room>();
            for (var i = 0; i < edges.Count - 1; i++)
            {
                var bounds = new Rect2(edges[i], edges[i + 1], z0, z1);
                rooms.Add(new Room
                {
                    bounds = bounds,
                    floorY = floorY,
                    nearStairs = bounds.Overlaps(StairZone),
                });
            }

            return rooms;
        }

        private static void BuildRowDividers(
            GameObject root, System.Random rnd, List<Vector2> doorways,
            string name, float[] splits, float z0, float z1, float floorY, float wallH)
        {
            foreach (var x in splits)
            {
                var door = Lerp(rnd, z0 + 0.8f, z1 - 0.8f);
                WallAlongZ(root, name, x, z0, z1, floorY, wallH, DoorsZ(doorways, x, door));
            }
        }

        private static void BuildFloors(GameObject root)
        {
            var material = DevGridFloorMenu.GetOrCreateMaterial();

            // Ground slab: top face at GroundTop so the spawn point rests exactly on it.
            Slab(root, "Ground Slab", new Vector3(0f, GroundTop - 0.05f, 3f), new Vector3(18f, 0.1f, 14f), material);

            // Upper slab in three pieces, leaving the stairwell hole (x -9..-7.9, z 3.5..7.7).
            var upperY = UpperTop - 0.05f;
            Slab(root, "Upper Slab", new Vector3(0.55f, upperY, 3f), new Vector3(16.9f, 0.1f, 14f), material);
            Slab(root, "Upper Slab SW", new Vector3(-8.45f, upperY, -0.25f), new Vector3(1.1f, 0.1f, 7.5f), material);
            Slab(root, "Upper Slab NW", new Vector3(-8.45f, upperY, 8.85f), new Vector3(1.1f, 0.1f, 2.3f), material);
        }

        /// <summary>
        /// Walls the stair run into a closet with a single lockable door on its south face.
        /// The doorway is registered like any other so furniture keeps clear of the vestibule.
        /// </summary>
        private static void BuildStairEnclosure(GameObject root, List<Vector2> doorways, float wallH)
        {
            var doorX = (X0 + StairZone.xMax) * 0.5f;
            WallAlongZ(root, "Stair Partition", StairZone.xMax, StairGateZ, Z1, GroundTop, wallH);
            WallAlongX(root, "Stair Gate", StairGateZ, X0, StairZone.xMax, GroundTop, wallH,
                DoorsX(doorways, StairGateZ, doorX));

            // Hinged on the west jamb, swinging north into the stairwell so an open door
            // never blocks the vestibule.
            var hinge = new GameObject("Stair Door");
            hinge.transform.SetParent(root.transform, false);
            hinge.transform.localPosition = new Vector3(doorX - DoorWidth * 0.5f, GroundTop, StairGateZ);
            Box(hinge, "Panel",
                new Vector3(DoorWidth * 0.5f, wallH * 0.5f, 0f),
                new Vector3(DoorWidth - 0.04f, wallH - 0.02f, 0.06f));

            var door = FurnitureBuilderMenu.AddHinged(hinge, Vector3.up, -100f, "door", closable: true);
            var serialized = new SerializedObject(door);
            serialized.FindProperty("unlocksAtRound").intValue = UpstairsUnlockRound;

            // Everything from just above the tallest ground-floor hiding spot (~0.96) up: the
            // whole upper floor and the stairwell, with no ground spot ever caught.
            var sealedVolume = new Bounds();
            sealedVolume.SetMinMax(new Vector3(X0 - 0.5f, 1f, Z0 - 0.5f), new Vector3(X1 + 0.5f, UpperTop + 3f, Z1 + 0.5f));
            serialized.FindProperty("sealedArea").boundsValue = sealedVolume;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildStairs(GameObject root)
        {
            // Straight run up the west side: 15 solid steps, ~0.183 rise (< CharacterController
            // stepOffset 0.3), 0.28 tread. Top step lands flush with the upper floor.
            const int steps = 15;
            var totalRise = UpperTop - GroundTop;
            var rise = totalRise / steps;
            var parent = new GameObject("Stairs").transform;
            parent.SetParent(root.transform, false);

            for (var i = 1; i <= steps; i++)
            {
                var top = GroundTop + rise * i;
                var z = 3.5f + 0.28f * (i - 1) + 0.14f;
                Box(parent.gameObject, $"Step {i}",
                    new Vector3(-8.45f, (GroundTop + top) * 0.5f, z),
                    new Vector3(1.1f, top - GroundTop, 0.28f));
            }
        }

        private static void BuildLights(GameObject root, List<Room> rooms)
        {
            // The upper slab blocks the directional light, so ground rooms need their own.
            foreach (var room in rooms)
            {
                if (room.floorY > GroundTop) continue;

                var b = room.bounds;
                var light = new GameObject("Room Light").AddComponent<Light>();
                light.transform.SetParent(root.transform, false);
                light.transform.position = new Vector3((b.xMin + b.xMax) * 0.5f, UpperTop - 0.45f, (b.zMin + b.zMax) * 0.5f);
                light.type = LightType.Point;
                light.range = Mathf.Max(b.xMax - b.xMin, b.zMax - b.zMin) + 2f;
                light.intensity = 1.4f;
                light.shadows = LightShadows.None;
            }
        }

        private static float[] DoorsX(List<Vector2> doorways, float z, params float[] xs)
        {
            foreach (var x in xs) doorways.Add(new Vector2(x, z));
            return xs;
        }

        private static float[] DoorsZ(List<Vector2> doorways, float x, params float[] zs)
        {
            foreach (var z in zs) doorways.Add(new Vector2(x, z));
            return zs;
        }

        private static int FillRoom(Transform parent, Room room, System.Random rnd, List<Vector2> doorways, List<Vector2> placed)
        {
            const int want = 2;
            var made = 0;
            for (var attempt = 0; attempt < 30 && made < want; attempt++)
            {
                var side = rnd.Next(4);
                var b = room.bounds;
                float x, z, yaw;
                // Prop depth is 0.5, so 0.31 from the wall centreline leaves the back face
                // ~1cm off the wall: visually flush, no collider overlap.
                const float inset = 0.31f;
                switch (side)
                {
                    case 0: x = b.xMin + inset; z = Lerp(rnd, b.zMin + 0.7f, b.zMax - 0.7f); yaw = 90f; break;
                    case 1: x = b.xMax - inset; z = Lerp(rnd, b.zMin + 0.7f, b.zMax - 0.7f); yaw = -90f; break;
                    case 2: z = b.zMin + inset; x = Lerp(rnd, b.xMin + 0.7f, b.xMax - 0.7f); yaw = 0f; break;
                    default: z = b.zMax - inset; x = Lerp(rnd, b.xMin + 0.7f, b.xMax - 0.7f); yaw = 180f; break;
                }

                var p = new Vector2(x, z);
                if (!Clear(p, room, doorways, placed)) continue;

                var rotation = Quaternion.Euler(0f, yaw, 0f);
                var position = new Vector3(x, room.floorY, z);
                if (!BackedByWall(position, rotation)) continue;

                var prop = rnd.Next(3) switch
                {
                    0 => FurnitureBuilderMenu.BuildDresser(),
                    1 => FurnitureBuilderMenu.BuildCupboard(),
                    _ => FurnitureBuilderMenu.BuildChest(),
                };
                prop.transform.SetParent(parent, false);
                prop.transform.SetPositionAndRotation(position, rotation);
                placed.Add(p);
                made++;
            }

            return made;
        }

        /// <summary>
        /// Distance-to-wall alone lets a prop sit at a wall stub's end or beside a doorway,
        /// looking free-standing. Require solid wall behind the prop's full width instead --
        /// the walls are real colliders by the time furniture is placed, so just ask physics.
        /// </summary>
        private static bool BackedByWall(Vector3 position, Quaternion rotation)
        {
            Physics.SyncTransforms();
            var back = rotation * Vector3.back;
            for (var side = -1; side <= 1; side++)
            {
                var origin = position + Vector3.up * 0.5f + rotation * Vector3.right * (0.4f * side) - back * 0.05f;
                if (!Physics.Raycast(origin, back, 0.6f)) return false;
            }

            return true;
        }

        private static bool Clear(Vector2 p, Room room, List<Vector2> doorways, List<Vector2> placed)
        {
            if (room.nearStairs && StairZone.Contains(p)) return false;

            foreach (var door in doorways)
            {
                if ((door - p).sqrMagnitude < 1.3f * 1.3f) return false;
            }

            foreach (var other in placed)
            {
                if ((other - p).sqrMagnitude < 1.2f * 1.2f) return false;
            }

            // Keep the spawn point breathable.
            return p.sqrMagnitude > 1.2f * 1.2f || room.floorY > GroundTop;
        }

        private static void WallAlongX(GameObject root, string name, float z, float x0, float x1, float y0, float h, params float[] doorCenters)
        {
            var edges = new List<float> { x0 };
            System.Array.Sort(doorCenters);
            foreach (var d in doorCenters)
            {
                edges.Add(d - DoorWidth * 0.5f);
                edges.Add(d + DoorWidth * 0.5f);
            }
            edges.Add(x1);

            for (var i = 0; i < edges.Count; i += 2)
            {
                var a = edges[i];
                var b = edges[i + 1];
                if (b - a < 0.05f) continue;
                Box(root, name, new Vector3((a + b) * 0.5f, y0 + h * 0.5f, z), new Vector3(b - a, h, Thickness));
            }
        }

        private static void WallAlongZ(GameObject root, string name, float x, float z0, float z1, float y0, float h, params float[] doorCenters)
        {
            var edges = new List<float> { z0 };
            System.Array.Sort(doorCenters);
            foreach (var d in doorCenters)
            {
                edges.Add(d - DoorWidth * 0.5f);
                edges.Add(d + DoorWidth * 0.5f);
            }
            edges.Add(z1);

            for (var i = 0; i < edges.Count; i += 2)
            {
                var a = edges[i];
                var b = edges[i + 1];
                if (b - a < 0.05f) continue;
                Box(root, name, new Vector3(x, y0 + h * 0.5f, (a + b) * 0.5f), new Vector3(Thickness, h, b - a));
            }
        }

        private static void Slab(GameObject root, string name, Vector3 center, Vector3 size, Material material)
        {
            var slab = Box(root, name, center, size);
            slab.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static GameObject Box(GameObject root, string name, Vector3 center, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(root.transform, false);
            box.transform.localPosition = center;
            box.transform.localScale = size;
            return box;
        }

        private static float Lerp(System.Random rnd, float min, float max)
        {
            return min + (float)rnd.NextDouble() * (max - min);
        }
    }
}
