using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// IMGUI draws in raw device pixels, so the same font size is half the apparent height at
    /// 4K that it is at 720p. Now that the WebGL canvas matches the browser window, fullscreen
    /// resolution varies wildly per player. Scaling GUI.matrix once per OnGUI keeps the HUD a
    /// constant fraction of the screen; every layout after Apply() works in 720p-reference
    /// coordinates via Width/Height instead of Screen.width/height.
    /// </summary>
    public static class HudScale
    {
        private const float ReferenceHeight = 720f;

        public static float Factor => Screen.height / ReferenceHeight;

        public static float Width => Screen.width / Factor;

        public static float Height => ReferenceHeight;

        public static void Apply()
        {
            GUI.matrix = Matrix4x4.Scale(new Vector3(Factor, Factor, 1f));
        }
    }
}
