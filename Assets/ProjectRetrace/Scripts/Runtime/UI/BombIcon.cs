using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// The HUD's bomb pictogram, drawn into a texture at first use. Procedural rather
    /// than an asset because the whole HUD is IMGUI with nothing to wire, and a sprite
    /// reference on the HUD component would be the one thing a fresh scene could forget.
    /// </summary>
    public static class BombIcon
    {
        private const int Size = 64;
        private static Texture2D _texture;

        public static Texture2D Texture
        {
            get
            {
                if (_texture == null) _texture = Build();
                return _texture;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _texture = null;

        private static Texture2D Build()
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "BombIcon", filterMode = FilterMode.Bilinear };
            var pixels = new Color[Size * Size];
            var body = new Vector2(Size * 0.5f, Size * 0.4f);
            var bodyRadius = Size * 0.32f;
            var neck = new Vector2(Size * 0.5f, Size * 0.74f);
            var spark = new Vector2(Size * 0.72f, Size * 0.9f);

            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    var color = Color.clear;
                    if (Vector2.Distance(p, body) <= bodyRadius) color = new Color(0.1f, 0.1f, 0.12f, 0.95f);
                    else if (Mathf.Abs(p.x - neck.x) <= Size * 0.09f && p.y >= body.y && p.y <= neck.y) color = new Color(0.25f, 0.25f, 0.28f, 0.95f);
                    else if (DistanceToFuse(p, neck, spark) <= Size * 0.035f) color = new Color(0.6f, 0.45f, 0.25f, 0.95f);
                    if (Vector2.Distance(p, spark) <= Size * 0.08f) color = new Color(1f, 0.75f, 0.2f, 0.95f);
                    pixels[y * Size + x] = color;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>A quadratic arc from the neck to the spark, sampled coarsely: close
        /// enough for a 64 px glyph.</summary>
        private static float DistanceToFuse(Vector2 p, Vector2 from, Vector2 to)
        {
            var control = new Vector2(from.x, to.y + Size * 0.05f);
            var best = float.MaxValue;
            for (var i = 0; i <= 16; i++)
            {
                var t = i / 16f;
                var point = (1 - t) * (1 - t) * from + 2 * (1 - t) * t * control + t * t * to;
                best = Mathf.Min(best, Vector2.Distance(p, point));
            }

            return best;
        }
    }
}
