using ProjectRetrace;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectRetrace.EditorTools
{
    /// <summary>
    /// Builds searchable furniture props from primitives: a dresser with drawers, a cupboard
    /// with a hinged door, and a chest with a lid. Each carries a KeySpotMarker inside, which
    /// KeySpawner discovers at runtime -- so duplicate and scatter these freely; every copy is
    /// automatically a candidate hiding spot.
    /// </summary>
    public static class FurnitureBuilderMenu
    {
        [MenuItem("ProjectRetrace/Furniture/Create Dresser", false, 20)]
        public static void CreateDresser() => Place(BuildDresser());

        [MenuItem("ProjectRetrace/Furniture/Create Cupboard", false, 21)]
        public static void CreateCupboard() => Place(BuildCupboard());

        [MenuItem("ProjectRetrace/Furniture/Create Chest", false, 22)]
        public static void CreateChest() => Place(BuildChest());

        [MenuItem("ProjectRetrace/Furniture/Create One Of Each", false, 23)]
        public static void CreateAll()
        {
            var dresser = BuildDresser();
            dresser.transform.SetPositionAndRotation(new Vector3(-2.5f, 0f, 4f), Quaternion.Euler(0f, 180f, 0f));
            var cupboard = BuildCupboard();
            cupboard.transform.SetPositionAndRotation(new Vector3(0f, 0f, 4f), Quaternion.Euler(0f, 180f, 0f));
            var chest = BuildChest();
            chest.transform.SetPositionAndRotation(new Vector3(2.5f, 0f, 4f), Quaternion.Euler(0f, 180f, 0f));
            Finish(dresser);
        }

        // All props are built with their pivot on the floor and their front facing local +Z.

        private static GameObject BuildDresser()
        {
            var root = NewRoot("Dresser");

            Panel(root, "Bottom", new Vector3(0f, 0.01f, 0f), new Vector3(0.9f, 0.02f, 0.5f));
            Panel(root, "Top", new Vector3(0f, 0.79f, 0f), new Vector3(0.9f, 0.02f, 0.5f));
            Panel(root, "Left", new Vector3(-0.44f, 0.4f, 0f), new Vector3(0.02f, 0.8f, 0.5f));
            Panel(root, "Right", new Vector3(0.44f, 0.4f, 0f), new Vector3(0.02f, 0.8f, 0.5f));
            Panel(root, "Back", new Vector3(0f, 0.4f, -0.24f), new Vector3(0.9f, 0.8f, 0.02f));

            BuildDrawer(root, "Drawer Lower", 0.22f);
            BuildDrawer(root, "Drawer Upper", 0.58f);
            return root;
        }

        private static void BuildDrawer(GameObject root, string name, float height)
        {
            var drawer = Child(root.transform, name, new Vector3(0f, height, 0.25f));
            Panel(drawer, "Front", Vector3.zero, new Vector3(0.84f, 0.3f, 0.02f));
            Panel(drawer, "Tray", new Vector3(0f, -0.13f, -0.23f), new Vector3(0.8f, 0.02f, 0.44f));
            Panel(drawer, "TrayBack", new Vector3(0f, 0f, -0.44f), new Vector3(0.8f, 0.26f, 0.02f));
            KeySpot(drawer, new Vector3(0f, -0.06f, -0.2f));

            var interactable = drawer.AddComponent<DrawerInteractable>();
            var serialized = new SerializedObject(interactable);
            serialized.FindProperty("slideAxis").vector3Value = Vector3.forward;
            serialized.FindProperty("openDistance").floatValue = 0.35f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject BuildCupboard()
        {
            var root = NewRoot("Cupboard");

            Panel(root, "Bottom", new Vector3(0f, 0.01f, 0f), new Vector3(0.8f, 0.02f, 0.5f));
            Panel(root, "Top", new Vector3(0f, 1.79f, 0f), new Vector3(0.8f, 0.02f, 0.5f));
            Panel(root, "Left", new Vector3(-0.39f, 0.9f, 0f), new Vector3(0.02f, 1.8f, 0.5f));
            Panel(root, "Right", new Vector3(0.39f, 0.9f, 0f), new Vector3(0.02f, 1.8f, 0.5f));
            Panel(root, "Back", new Vector3(0f, 0.9f, -0.24f), new Vector3(0.8f, 1.8f, 0.02f));
            Panel(root, "Shelf", new Vector3(0f, 0.9f, 0f), new Vector3(0.76f, 0.02f, 0.46f));

            var hinge = Child(root.transform, "Door", new Vector3(-0.39f, 0.9f, 0.25f));
            Panel(hinge, "Panel", new Vector3(0.39f, 0f, 0f), new Vector3(0.76f, 1.76f, 0.02f));
            AddHinged(hinge, Vector3.up, -110f, "cupboard");

            KeySpot(root, new Vector3(0f, 0.96f, 0.1f));
            return root;
        }

        private static GameObject BuildChest()
        {
            var root = NewRoot("Chest");

            Panel(root, "Bottom", new Vector3(0f, 0.01f, 0f), new Vector3(0.9f, 0.02f, 0.5f));
            Panel(root, "Left", new Vector3(-0.44f, 0.25f, 0f), new Vector3(0.02f, 0.46f, 0.5f));
            Panel(root, "Right", new Vector3(0.44f, 0.25f, 0f), new Vector3(0.02f, 0.46f, 0.5f));
            Panel(root, "Front", new Vector3(0f, 0.25f, 0.24f), new Vector3(0.9f, 0.46f, 0.02f));
            Panel(root, "Back", new Vector3(0f, 0.25f, -0.24f), new Vector3(0.9f, 0.46f, 0.02f));

            var hinge = Child(root.transform, "Lid", new Vector3(0f, 0.49f, -0.25f));
            Panel(hinge, "Panel", new Vector3(0f, 0.01f, 0.25f), new Vector3(0.9f, 0.02f, 0.5f));
            AddHinged(hinge, Vector3.right, -100f, "chest");

            KeySpot(root, new Vector3(0f, 0.12f, 0f));
            return root;
        }

        private static void AddHinged(GameObject hinge, Vector3 axis, float angle, string label)
        {
            var interactable = hinge.AddComponent<DoorInteractable>();
            var serialized = new SerializedObject(interactable);
            serialized.FindProperty("hingeAxis").vector3Value = axis;
            serialized.FindProperty("openAngle").floatValue = angle;
            serialized.FindProperty("label").stringValue = label;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject NewRoot(string name)
        {
            var root = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(root, "Create " + name);
            return root;
        }

        private static GameObject Child(Transform parent, string name, Vector3 localPosition)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            return child;
        }

        private static void Panel(GameObject parent, string name, Vector3 localPosition, Vector3 localScale)
        {
            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = name;
            panel.transform.SetParent(parent.transform, false);
            panel.transform.localPosition = localPosition;
            panel.transform.localScale = localScale;
        }

        private static void KeySpot(GameObject parent, Vector3 localPosition)
        {
            Child(parent.transform, "KeySpot", localPosition).AddComponent<KeySpotMarker>();
        }

        private static void Place(GameObject root)
        {
            var view = SceneView.lastActiveSceneView;
            if (view != null)
            {
                var pivot = view.pivot;
                root.transform.position = new Vector3(pivot.x, 0f, pivot.z);
            }

            Finish(root);
        }

        private static void Finish(GameObject root)
        {
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Selection.activeGameObject = root;
        }
    }
}
