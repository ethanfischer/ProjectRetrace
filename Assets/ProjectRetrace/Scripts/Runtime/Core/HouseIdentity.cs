using System.Collections.Generic;
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
                var house = string.Join("+", HouseRootNames());
                return (house.Length > 0 ? house : "no-house") + "|" + Application.version;
            }
        }

        public static int KeySpotCount => Object.FindObjectsByType<KeySpotMarker>(FindObjectsSortMode.None).Length;

        /// <summary>Every house root counts, sorted: a peer missing the upper floor is a
        /// different house, and hierarchy order must not decide the name.</summary>
        private static List<string> HouseRootNames()
        {
            var names = new List<string>();
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name.StartsWith("TestHouse")) names.Add(root.name);
            }

            names.Sort(System.StringComparer.Ordinal);
            return names;
        }
    }
}
