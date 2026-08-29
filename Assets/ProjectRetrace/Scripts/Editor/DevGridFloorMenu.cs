using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectRetrace.EditorTools
{
    /// <summary>
    /// Creates a walkable floor plane with a world-space dev grid so playtests have visible
    /// orientation before any real house geometry exists.
    /// </summary>
    public static class DevGridFloorMenu
    {
        private const string MaterialPath = "Assets/ProjectRetrace/Art/Materials/DevGrid.mat";
        private const float FloorSize = 40f;

        [MenuItem("ProjectRetrace/Create Dev Grid Floor", false, 1)]
        public static void CreateFloor()
        {
            var existing = GameObject.Find("DevGridFloor");
            if (existing != null)
            {
                Selection.activeGameObject = existing;
                return;
            }

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "DevGridFloor";
            // A Unity plane is 10x10 at scale 1; scale to FloorSize on a side.
            floor.transform.localScale = Vector3.one * (FloorSize / 10f);
            floor.transform.position = Vector3.zero;
            floor.GetComponent<MeshRenderer>().sharedMaterial = GetOrCreateMaterial();
            Undo.RegisterCreatedObjectUndo(floor, "Create Dev Grid Floor");

            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Selection.activeGameObject = floor;
        }

        private static Material GetOrCreateMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null) return material;

            var shader = Shader.Find("ProjectRetrace/DevGrid");
            if (shader == null)
            {
                Debug.LogError("[ProjectRetrace] DevGrid shader not found; floor will use the default material.");
                return null;
            }

            var directory = System.IO.Path.GetDirectoryName(MaterialPath);
            if (!AssetDatabase.IsValidFolder(directory))
            {
                AssetDatabase.CreateFolder("Assets/ProjectRetrace/Art", "Materials");
            }

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }
    }
}
