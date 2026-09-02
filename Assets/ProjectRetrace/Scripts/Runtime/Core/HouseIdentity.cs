using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// What two online clients compare before agreeing to play: the house is baked into
    /// the scene rather than generated from a seed at runtime, so "same build" is the only
    /// guarantee that hiding spots and prop ids line up. The generated root carries its
    /// seed in its name, and the key-spot count catches a hand-edited copy of the same seed.
    /// </summary>
    public static class HouseIdentity
    {
        public const int Protocol = 1;

        public static string Current
        {
            get
            {
                var root = FindHouseRoot();
                var house = root != null ? root.name : "no-house";
                return house + "|" + Application.version;
            }
        }

        public static int KeySpotCount => Object.FindObjectsByType<KeySpotMarker>(FindObjectsSortMode.None).Length;

        private static GameObject FindHouseRoot()
        {
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name.StartsWith("TestHouse")) return root;
            }

            return null;
        }
    }
}
