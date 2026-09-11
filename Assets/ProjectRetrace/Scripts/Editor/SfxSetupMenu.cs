using ProjectRetrace;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectRetrace.EditorTools
{
    /// <summary>
    /// Points every sound slot at the clips in Assets/SFX. Setup Scene Systems calls it
    /// for a fresh rig; the menu item retrofits a scene built before the bank existed,
    /// including the sound kind on doors that were wired before it was a field.
    /// Idempotent, so re-running after a clip is swapped is harmless.
    /// </summary>
    public static class SfxSetupMenu
    {
        private const string Folder = "Assets/SFX/";
        private const string MusicPath = "Assets/Music/music.wav";

        [MenuItem("ProjectRetrace/Audio/Wire SFX", false, 40)]
        public static void WireScene()
        {
            var director = Object.FindFirstObjectByType<GameDirector>();
            if (director == null)
            {
                Debug.LogError("[ProjectRetrace] No GameDirector in the scene -- run Setup Scene Systems first.");
                return;
            }

            var systems = director.gameObject;
            var bank = systems.GetComponent<SoundBank>() ?? Undo.AddComponent<SoundBank>(systems);
            WireBank(bank);
            WireMusic(systems.GetComponent<MusicPlayer>() ?? Undo.AddComponent<MusicPlayer>(systems));

            var footsteps = 0;
            foreach (var emitter in Object.FindObjectsByType<FootstepEmitter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                WireFootsteps(emitter);
                footsteps++;
            }

            var doors = 0;
            foreach (var door in Object.FindObjectsByType<DoorInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var serialized = new SerializedObject(door);
                var label = serialized.FindProperty("label").stringValue;
                serialized.FindProperty("sound").enumValueIndex = (int)SoundFor(door.transform, label);
                if (serialized.ApplyModifiedProperties()) doors++;
            }

            EditorSceneManager.MarkSceneDirty(systems.scene);
            EditorSceneManager.SaveScene(systems.scene);
            Debug.Log($"[ProjectRetrace] SFX wired: sound bank, {footsteps} footstep emitter(s), {doors} door sound(s) changed.");
        }

        public static void WireBank(SoundBank bank)
        {
            bank.doorOpen = Clip("Door open");
            bank.doorClose = Clip("Door close");
            bank.cabinetOpen = Clip("Cabinet open");
            bank.cabinetClose = Clip("Cabinet close");
            bank.dresserOpen = Clip("dresser open");
            bank.dresserClose = Clip("dresser close");
            bank.ovenOpen = Clip("oven open");
            bank.ovenClose = Clip("oven close");
            bank.grabKeys = Clip("grab keys");
            bank.ghostSpawn = Clip("Past self spawns");
            bank.spotted = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ProjectRetrace/Audio/whistle.wav");
            bank.buttonClick = Clip("button click");
            bank.pickUp = OptionalClip("pop");
            bank.throwWhoosh = OptionalClip("woosh");
            bank.projectileThud = OptionalClip("thud");
            bank.projectileHit = OptionalClip("projectile hit");
            bank.ghostStunned = OptionalClip("ghost stunned");
            bank.bombExplode = OptionalClip("explosion");
            bank.cashTaken = OptionalClip("coin bag");
            EditorUtility.SetDirty(bank);
        }

        public static void WireMusic(MusicPlayer music)
        {
            music.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicPath);
            if (music.clip == null) Debug.LogWarning($"[ProjectRetrace] Missing music at {MusicPath}");
            EditorUtility.SetDirty(music);
        }

        public static void WireFootsteps(FootstepEmitter emitter)
        {
            emitter.clip = Clip("walk");
            emitter.runClip = Clip("run");
            EditorUtility.SetDirty(emitter);
        }

        /// <summary>Room doors are the only hinges labelled "door"; the oven is the pack's
        /// one interactive appliance, and its prefab name is the only thing that sets it
        /// apart from a cabinet. Everything else hinged is furniture.</summary>
        public static OpenableSound SoundFor(Transform hinge, string label)
        {
            if (label == "door") return OpenableSound.Door;
            for (var t = hinge; t != null; t = t.parent)
            {
                if (t.name.StartsWith("Oven")) return OpenableSound.Oven;
            }

            return OpenableSound.Cabinet;
        }

        /// <summary>Slots whose samples have not been recorded yet: silent, not a warning
        /// on every setup run.</summary>
        private static AudioClip OptionalClip(string name)
        {
            return AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + name + ".wav");
        }

        private static AudioClip Clip(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + name + ".wav");
            if (clip == null) Debug.LogWarning($"[ProjectRetrace] Missing SFX clip '{name}.wav' in {Folder}");
            return clip;
        }
    }
}
