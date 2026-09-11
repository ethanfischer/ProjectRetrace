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
            WireFootprints(systems.AddComponent<FootprintTrail>());
            var keySpawner = systems.AddComponent<KeySpawner>();
            var hud = systems.AddComponent<DebugHud>();
            var results = systems.AddComponent<ResultsScreen>();
            var menu = systems.AddComponent<StartMenu>();
            var configMenu = systems.AddComponent<ConfigMenu>();
            configMenu.director = director;
            var online = systems.AddComponent<OnlineSession>();
            var spectator = systems.AddComponent<SpectatorRig>();
            var lobby = systems.AddComponent<OnlineLobby>();
            SfxSetupMenu.WireBank(systems.AddComponent<SoundBank>());
            SfxSetupMenu.WireMusic(systems.AddComponent<MusicPlayer>());

            var player = BuildPlayer(out var controller, out var interactor, out var cameraTransform);
            var thrower = EnsureThrower(controller, cameraTransform);
            var bombCarrier = EnsureBombCarrier(controller, interactor);
            var spawnPoint = CreateObject("SpawnPoint", null).transform;
            spawnPoint.position = new Vector3(0f, 0.05f, 0f);

            var keys = BuildKeys();
            var bomb = BuildBomb();
            var cashSpawner = systems.AddComponent<CashSpawner>();
            cashSpawner.template = BuildCashTemplate();
            var sentryTemplate = BuildSentry("Sentry Template", Color.white);
            CreateObject("NavMesh Baker", null).AddComponent<NavMeshRuntimeBaker>();

            // Wiring.
            director.player = controller;
            director.interactor = interactor;
            director.thrower = thrower;
            director.bombCarrier = bombCarrier;
            director.trail = trail;
            director.keySpawner = keySpawner;
            director.spawnPoint = spawnPoint;
            director.sentryTemplate = sentryTemplate;
            director.cashSpawner = cashSpawner;
            director.online = online;
            director.spectator = spectator;

            online.director = director;
            online.spectator = spectator;
            spectator.director = director;
            spectator.player = controller;
            lobby.director = director;
            lobby.session = online;
            menu.online = online;

            trail.tracked = player.transform;

            keySpawner.key = keys;
            keySpawner.bomb = bomb;

            sentryTemplate.player = controller;

            hud.director = director;
            hud.interactor = interactor;
            hud.thrower = thrower;
            hud.bombCarrier = bombCarrier;
            hud.trail = trail;
            results.director = director;
            menu.director = director;

            controller.cameraPivot = cameraTransform;
            interactor.rayOrigin = cameraTransform;

            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Selection.activeGameObject = systems;

            Debug.Log("[ProjectRetrace] Scene systems created. Add a floor, press Play, " +
                      "and use F3 for the debug trail view.");
        }

        /// <summary>Retrofits online play onto a scene that already has the rig: adds the
        /// session, spectator, and lobby components and wires them, and gives the sentry
        /// template its transparent materials. Idempotent, so it doubles as the repair step
        /// after a scene merge that took the other side's copy.</summary>
        [MenuItem("ProjectRetrace/Setup Online Systems", false, 1)]
        public static void SetupOnline()
        {
            var director = Object.FindFirstObjectByType<GameDirector>();
            if (director == null && EditorBuildSettings.scenes.Length > 0)
            {
                // Headless (-executeMethod) starts in an empty scene: open the build scene.
                EditorSceneManager.OpenScene(EditorBuildSettings.scenes[0].path);
                director = Object.FindFirstObjectByType<GameDirector>();
            }

            if (director == null)
            {
                Debug.LogError("[ProjectRetrace] No GameDirector in the scene -- run Setup Scene Systems first.");
                return;
            }

            var systems = director.gameObject;
            var online = systems.GetComponent<OnlineSession>() ?? systems.AddComponent<OnlineSession>();
            var spectator = systems.GetComponent<SpectatorRig>() ?? systems.AddComponent<SpectatorRig>();
            var lobby = systems.GetComponent<OnlineLobby>() ?? systems.AddComponent<OnlineLobby>();
            var menu = systems.GetComponent<StartMenu>();

            director.online = online;
            director.spectator = spectator;
            online.director = director;
            online.spectator = spectator;
            spectator.director = director;
            spectator.player = director.player;
            lobby.director = director;
            lobby.session = online;
            if (menu != null) menu.online = online;

            if (director.player != null && director.player.cameraPivot != null)
            {
                director.thrower = EnsureThrower(director.player, director.player.cameraPivot);
                var hud = systems.GetComponent<DebugHud>();
                if (hud != null) hud.thrower = director.thrower;
            }

            if (director.sentryTemplate != null)
            {
                director.sentryTemplate.bodyMaterialTemplate = AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/ProjectRetrace/Art/GhostTransparent.mat");
                director.sentryTemplate.coneMaterialTemplate = AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/ProjectRetrace/Art/GhostConeTransparent.mat");
                EditorUtility.SetDirty(director.sentryTemplate);
            }

            EditorUtility.SetDirty(systems);
            EditorSceneManager.MarkSceneDirty(systems.scene);
            EditorSceneManager.SaveScene(systems.scene);
            Debug.Log("[ProjectRetrace] Online systems wired into " + systems.scene.name);
        }

        /// <summary>Retrofits the bomb onto a scene that already has the rig: the prop,
        /// the player's pocket, and the spawner and HUD references. Idempotent.</summary>
        [MenuItem("ProjectRetrace/Setup Footprints", false, 3)]
        public static void SetupFootprints()
        {
            var trail = Object.FindFirstObjectByType<BreadcrumbTrail>();
            if (trail == null)
            {
                Debug.LogError("[ProjectRetrace] No BreadcrumbTrail in the scene -- run Setup Scene Systems first.");
                return;
            }

            var footprints = trail.GetComponent<FootprintTrail>();
            if (footprints == null) footprints = trail.gameObject.AddComponent<FootprintTrail>();
            WireFootprints(footprints);
            EditorUtility.SetDirty(footprints);
            EditorSceneManager.MarkSceneDirty(trail.gameObject.scene);
            Debug.Log("[ProjectRetrace] Footprints wired into " + trail.gameObject.scene.name);
        }

        private static void WireFootprints(FootprintTrail footprints)
        {
            footprints.materialTemplate = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/ProjectRetrace/Art/GhostConeTransparent.mat");
        }

        [MenuItem("ProjectRetrace/Setup Cash", false, 5)]
        public static void SetupCash()
        {
            var director = Object.FindFirstObjectByType<GameDirector>();
            if (director == null)
            {
                Debug.LogError("[ProjectRetrace] No GameDirector in the scene -- run Setup Scene Systems first.");
                return;
            }

            var spawner = director.GetComponent<CashSpawner>();
            if (spawner == null) spawner = Undo.AddComponent<CashSpawner>(director.gameObject);
            if (spawner.template == null)
            {
                var existing = GameObject.Find(CashTemplateName);
                spawner.template = existing != null ? existing.GetComponent<CashItem>() : BuildCashTemplate();
            }

            director.cashSpawner = spawner;
            EditorUtility.SetDirty(director);
            EditorUtility.SetDirty(spawner);
            EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
            Debug.Log("[ProjectRetrace] Cash wired into " + director.gameObject.scene.name);
        }

        private const string CashTemplateName = "Cash Template";
        private const string CashMaterialPath = "Assets/ProjectRetrace/Art/Materials/Cash.mat";

        /// <summary>A flat green slab the size of a folded stack. Inactive: the spawner
        /// clones it, and an active template would register as a takeable stack of $0.</summary>
        private static CashItem BuildCashTemplate()
        {
            var cash = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cash.name = CashTemplateName;
            cash.transform.localScale = new Vector3(0.15f, 0.012f, 0.065f);
            cash.GetComponent<MeshRenderer>().sharedMaterial = CashMaterial();
            Undo.RegisterCreatedObjectUndo(cash, "Create Cash Template");
            cash.SetActive(false);
            return cash.AddComponent<CashItem>();
        }

        private static Material CashMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(CashMaterialPath);
            if (material != null) return material;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = "Cash" };
            material.SetColor("_BaseColor", new Color(0.24f, 0.58f, 0.3f));
            material.SetFloat("_Smoothness", 0.2f);
            AssetDatabase.CreateAsset(material, CashMaterialPath);
            return material;
        }

        [MenuItem("ProjectRetrace/Setup Bomb", false, 2)]
        public static void SetupBomb()
        {
            var director = Object.FindFirstObjectByType<GameDirector>();
            if (director == null || director.player == null || director.interactor == null)
            {
                Debug.LogError("[ProjectRetrace] No wired GameDirector in the scene -- run Setup Scene Systems first.");
                return;
            }

            var carrier = EnsureBombCarrier(director.player, director.interactor);
            director.bombCarrier = carrier;

            var bomb = Object.FindFirstObjectByType<BombItem>(FindObjectsInactive.Include);
            if (bomb == null) bomb = BuildBomb();
            if (director.keySpawner != null) director.keySpawner.bomb = bomb;

            var hud = director.GetComponent<DebugHud>();
            if (hud != null) hud.bombCarrier = carrier;

            EditorUtility.SetDirty(director);
            EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
            Debug.Log("[ProjectRetrace] Bomb wired into " + director.gameObject.scene.name);
        }

        private static PlayerBombCarrier EnsureBombCarrier(FirstPersonController controller, PlayerInteractor interactor)
        {
            var player = controller.gameObject;
            var carrier = player.GetComponent<PlayerBombCarrier>();
            if (carrier == null) carrier = Undo.AddComponent<PlayerBombCarrier>(player);
            carrier.interactor = interactor;
            EditorUtility.SetDirty(carrier);
            return carrier;
        }

        /// <summary>The art team's prefab, with the collider the pickup ray needs -- the
        /// FBX imports without one -- and parked by the keys until the spawner moves it.</summary>
        private static BombItem BuildBomb()
        {
            const string prefabPath = "Assets/ModelsNew/Bomb_PF.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            GameObject bomb;
            if (prefab != null)
            {
                bomb = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            }
            else
            {
                Debug.LogWarning("[ProjectRetrace] " + prefabPath + " is missing -- using a placeholder sphere for the bomb.");
                bomb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bomb.transform.localScale = Vector3.one * 0.2f;
            }

            bomb.name = "Bomb";
            bomb.transform.position = new Vector3(0f, 1f, 3.5f);
            if (bomb.GetComponentInChildren<Collider>() == null)
            {
                var collider = bomb.AddComponent<SphereCollider>();
                collider.radius = FitRadius(bomb);
            }

            Undo.RegisterCreatedObjectUndo(bomb, "Create Bomb");
            return bomb.AddComponent<BombItem>();
        }

        private static float FitRadius(GameObject bomb)
        {
            var renderer = bomb.GetComponentInChildren<Renderer>();
            if (renderer == null) return 0.15f;
            var extents = renderer.bounds.extents;
            var world = Mathf.Max(extents.x, extents.y, extents.z);
            var scale = Mathf.Max(bomb.transform.lossyScale.x, 0.0001f);
            return Mathf.Max(0.05f, world / scale);
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
            characterController.stepOffset = RetraceConfig.Current.stepHeight;

            controller = player.AddComponent<FirstPersonController>();
            interactor = player.AddComponent<PlayerInteractor>();
            AddFootsteps(player);

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

        /// <summary>The hands, and the anchor a carried item sits at: under the camera,
        /// low and to the right, so it reads as held and stays out of the reticle.</summary>
        private static PlayerThrower EnsureThrower(FirstPersonController controller, Transform cameraTransform)
        {
            var player = controller.gameObject;
            var thrower = player.GetComponent<PlayerThrower>();
            if (thrower == null) thrower = Undo.AddComponent<PlayerThrower>(player);
            var anchor = cameraTransform.Find("HandAnchor");
            if (anchor == null)
            {
                anchor = CreateObject("HandAnchor", cameraTransform).transform;
                anchor.localPosition = new Vector3(0.28f, -0.22f, 0.7f);
                anchor.localRotation = Quaternion.identity;
            }

            thrower.handAnchor = anchor;
            thrower.rayOrigin = cameraTransform;
            EditorUtility.SetDirty(thrower);
            return thrower;
        }

        private static PatrolSentry BuildSentry(string name, Color tint)
        {
            var sentry = CreateObject(name, null);
            sentry.transform.position = new Vector3(0f, 0.05f, 0f);

            var agent = sentry.AddComponent<UnityEngine.AI.NavMeshAgent>();
            agent.radius = 0.3f;
            agent.height = 1.8f;

            // Visual only: the collider comes off so the sentry's own capsule never blocks
            // its line-of-sight raycasts or pollutes a whole-scene navmesh bake.
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            Object.DestroyImmediate(body.GetComponent<CapsuleCollider>());
            body.transform.SetParent(sentry.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);

            // A nose so the facing reads from across a room, before the floor cone is visible.
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Nose";
            Object.DestroyImmediate(nose.GetComponent<BoxCollider>());
            nose.transform.SetParent(sentry.transform, false);
            nose.transform.localPosition = new Vector3(0f, 1.6f, 0.3f);
            nose.transform.localScale = new Vector3(0.12f, 0.12f, 0.25f);

            // Starts inactive: the sentry exists only during the stealth phase, and an active
            // agent would try to place itself on a navmesh that may not be baked yet.
            sentry.SetActive(false);
            var patrol = sentry.AddComponent<PatrolSentry>();
            patrol.bodyTint = tint;
            patrol.bodyMaterialTemplate = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/ProjectRetrace/Art/GhostTransparent.mat");
            patrol.coneMaterialTemplate = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/ProjectRetrace/Art/GhostConeTransparent.mat");
            AddFootsteps(sentry);
            return patrol;
        }

        private static void AddFootsteps(GameObject walker)
        {
            SfxSetupMenu.WireFootsteps(walker.AddComponent<FootstepEmitter>());
        }

        private const string KeysModelPath = "Assets/ProjectRetrace/Art/Models/Keys.fbx";
        private const string KeysModelName = "KeysModel";

        private static KeyItem BuildKeys()
        {
            var keys = CreateObject("Keys", null);
            keys.transform.position = new Vector3(0f, 1f, 3f);
            keys.AddComponent<SphereCollider>();
            var item = keys.AddComponent<KeyItem>();
            AttachKeysModel(item);
            return item;
        }

        [MenuItem("ProjectRetrace/Setup Keys Model", false, 4)]
        public static void SetupKeysModel()
        {
            var item = Object.FindFirstObjectByType<KeyItem>(FindObjectsInactive.Include);
            if (item == null)
            {
                Debug.LogError("[ProjectRetrace] No KeyItem in the scene -- run Setup Scene Systems first.");
                return;
            }

            AttachKeysModel(item);
            EditorSceneManager.MarkSceneDirty(item.gameObject.scene);
            Debug.Log("[ProjectRetrace] Keys model attached in " + item.gameObject.scene.name);
        }

        /// <summary>The art FBX hangs under the Keys root as a child, so the root keeps the
        /// collider the interaction ray needs, the KeyItem, and the plain transform
        /// KeySpawner drives; the model is centred on that root so the spawner's spot is
        /// the middle of the bunch. Any placeholder mesh on the root (the original sphere)
        /// is removed. Idempotent: rerunning replaces the previous model.</summary>
        private static void AttachKeysModel(KeyItem item)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(KeysModelPath);
            if (model == null)
            {
                Debug.LogError("[ProjectRetrace] Missing " + KeysModelPath + " -- keys keep their placeholder.");
                return;
            }

            var root = item.transform;
            var previous = root.Find(KeysModelName);
            if (previous != null) Undo.DestroyObjectImmediate(previous.gameObject);
            RemovePlaceholderMesh(item.gameObject);
            root.localScale = Vector3.one;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, item.gameObject.scene);
            instance.name = KeysModelName;
            Undo.RegisterCreatedObjectUndo(instance, "Attach Keys Model");
            instance.transform.SetParent(root, false);
            // Lying flat: hung upright the bunch is 15 cm tall and clips the shelf above
            // in the narrower cabinets; flat it is 6 cm and rests like a dropped keyring.
            instance.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            instance.transform.localPosition = Vector3.zero;

            var bounds = RendererBounds(instance);
            instance.transform.localPosition = root.position - bounds.center;

            var collider = item.GetComponent<SphereCollider>();
            if (collider == null) collider = Undo.AddComponent<SphereCollider>(item.gameObject);
            collider.center = Vector3.zero;
            collider.radius = bounds.extents.magnitude;
            EditorUtility.SetDirty(item);
        }

        private static void RemovePlaceholderMesh(GameObject keys)
        {
            var renderer = keys.GetComponent<MeshRenderer>();
            if (renderer != null) Undo.DestroyObjectImmediate(renderer);
            var filter = keys.GetComponent<MeshFilter>();
            if (filter != null) Undo.DestroyObjectImmediate(filter);
        }

        private static Bounds RendererBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
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
