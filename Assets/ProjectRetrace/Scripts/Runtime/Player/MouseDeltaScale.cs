using System.Runtime.InteropServices;
using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// Brings WebGL mouse deltas back into line with the editor and desktop builds so one
    /// mouseSensitivity value feels the same everywhere. The browser reports deltas in device
    /// pixels (so a 2x display doubles them), and pointer lock hands over movement that has
    /// already been through OS acceleration, which runs a further ~2x hotter than the raw
    /// deltas native builds see. The pixel-ratio part is measured; the acceleration part is a
    /// constant because browsers expose no way to read it.
    /// </summary>
    public static class MouseDeltaScale
    {
        private const float BrowserPointerLockGain = 2.2f;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern float RetraceDevicePixelRatio();

        private static float _factor;

        public static float Factor
        {
            get
            {
                if (_factor <= 0f)
                {
                    var ratio = Mathf.Max(1f, RetraceDevicePixelRatio());
                    _factor = 1f / (ratio * BrowserPointerLockGain);
                }
                return _factor;
            }
        }
#else
        public static float Factor => 1f;
#endif
    }
}
