using ProjectRetrace;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectRetrace.EditorTools
{
    /// <summary>
    /// Drops a fully wired set of systems into the open scene, so nobody loses an hour to
    /// "which script goes on which GameObject". It does not build the house -- that is yours.
    /// </summary>
    public static class SceneSetupMenu
    {
        private const float EyeHeight = 1.65f;

        [MenuItem("ProjectRetrace/Setup Scene Systems", false, 0)]
        public static void SetupScene()
        {
            var existing = Object.FindFirstObjectByType<GameDirector>();
            if (existing != null)
            {
                EditorUtility.DisplayDialog(
                    "ProjectRetrace",
                    "This scene already has a GameDirector. Delete it first if you want a fresh rig.",
                    "OK");
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            var systems = CreateObject("ProjectRetrace Systems", null);
            var director = systems.AddComponent<GameDirector>();
            var trail = systems.AddComponent<BreadcrumbTrail>();
            systems.AddComponent<TrailVisualizer>();
            var keySpawner = systems.AddComponent<KeySpawner>();
            var hud = systems.AddComponent<DebugHud>();
            var results = systems.AddComponent<ResultsScreen>();

            var player = BuildPlayer(out var controller, out var interactor, out var cameraTransform);
            var spawnPoint = CreateObject("SpawnPoint", null).transform;
            spawnPoint.position = new Vector3(0f, 0.05f, 0f);

            var keys = BuildKeys();

            // Wiring.
            director.player = controller;
            director.interactor = interactor;
            director.trail = trail;
            director.keySpawner = keySpawner;
            director.spawnPoint = spawnPoint;

            trail.tracked = player.transform;

            keySpawner.key = keys;

            hud.director = director;
            hud.interactor = interactor;
            hud.trail = trail;
            results.director = director;

            controller.cameraPivot = cameraTransform;
            interactor.rayOrigin = cameraTransform;

            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Selection.activeGameObject = systems;

            Debug.Log("[ProjectRetrace] Scene systems created. Add a floor, press Play, " +
                      "and use F3 for the debug trail view.");
        }

        private static GameObject BuildPlayer(
            out FirstPersonController controller,
            out PlayerInteractor interactor,
            out Transform cameraTransform)
        {
            var player = CreateObject("Player", null);
            player.transform.position = new Vector3(0f, 0.05f, 0f);

            var characterController = player.AddComponent<CharacterController>();
            characterController.height = 1.8f;
            characterController.radius = 0.3f;
            characterController.center = new Vector3(0f, 0.9f, 0f);

            controller = player.AddComponent<FirstPersonController>();
            interactor = player.AddComponent<PlayerInteractor>();

            // Reuse the scene's existing main camera when there is one, rather than leaving a
            // second camera behind to fight over rendering and audio listeners.
            var mainCamera = Camera.main;
            if (mainCamera != null)
            {
                Undo.SetTransformParent(mainCamera.transform, player.transform, "Reparent Camera");
                mainCamera.transform.localPosition = new Vector3(0f, EyeHeight, 0f);
                mainCamera.transform.localRotation = Quaternion.identity;
                cameraTransform = mainCamera.transform;
            }
            else
            {
                var cameraObject = CreateObject("PlayerCamera", player.transform);
                cameraObject.tag = "MainCamera";
                cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
                cameraObject.transform.localPosition = new Vector3(0f, EyeHeight, 0f);
                cameraTransform = cameraObject.transform;
            }

            return player;
        }

        private static KeyItem BuildKeys()
        {
            var keys = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            keys.name = "Keys";
            keys.transform.localScale = Vector3.one * 0.2f;
            keys.transform.position = new Vector3(0f, 1f, 3f);
            Undo.RegisterCreatedObjectUndo(keys, "Create Keys");
            return keys.AddComponent<KeyItem>();
        }

        private static GameObject CreateObject(string name, Transform parent)
        {
            var created = new GameObject(name);
            if (parent != null) created.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(created, "Create " + name);
            return created;
        }
    }
}
