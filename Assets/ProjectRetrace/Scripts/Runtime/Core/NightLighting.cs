using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectRetrace
{
    /// <summary>
    /// The house at night, in one flat blue wash, switched by <c>nightMode</c> in the config.
    /// The scene stays authored as daylight: this captures that on Awake and lays night over
    /// it, so turning the setting off gives the artist's lighting back exactly and the scene
    /// file never has to carry the choice.
    ///
    /// There are no practicals on purpose. Lamps and a flashlight were tried and read as a
    /// lit house with the sun off, and every local light also made the ghosts pop, which is
    /// a balance the game has not chosen. The ceiling blocks the sun indoors, so the ambient
    /// term carries the rooms and the sun only tints what the windows reach.
    /// </summary>
    public class NightLighting : MonoBehaviour
    {
        private static readonly Color NightSun = new Color(0.55f, 0.7f, 1f);
        private const float NightSunIntensity = 1.2f;
        private static readonly Color NightAmbient = new Color(150f / 255f, 174f / 255f, 1f);
        private static readonly Color NightSky = new Color(0.05f, 0.07f, 0.16f);

        private struct CameraDay
        {
            public Camera Camera;
            public CameraClearFlags ClearFlags;
            public Color Background;
        }

        private Light _sun;
        private Color _daySunColor;
        private float _daySunIntensity;
        private AmbientMode _dayAmbientMode;
        private Color _dayAmbient;
        private CameraDay[] _cameras;
        private bool _captured;
        private bool _nightApplied;

        private void Awake()
        {
            Capture();
        }

        /// <summary>Consumers read the config at the point of use, so a Tab-menu save or a
        /// hand edit shows up on the next frame without anyone having to call in.</summary>
        private void Update()
        {
            var night = RetraceConfig.Current.nightMode;
            if (night == _nightApplied) return;
            if (night) ApplyNight(); else RestoreDay();
            _nightApplied = night;
        }

        private void OnDisable()
        {
            if (_nightApplied) RestoreDay();
            _nightApplied = false;
        }

        private void Capture()
        {
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;
                _sun = light;
                _daySunColor = light.color;
                _daySunIntensity = light.intensity;
                break;
            }
            _dayAmbientMode = RenderSettings.ambientMode;
            _dayAmbient = RenderSettings.ambientLight;

            var cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            _cameras = new CameraDay[cameras.Length];
            for (int i = 0; i < cameras.Length; i++)
            {
                _cameras[i] = new CameraDay
                {
                    Camera = cameras[i],
                    ClearFlags = cameras[i].clearFlags,
                    Background = cameras[i].backgroundColor,
                };
            }
            _captured = true;
        }

        private void ApplyNight()
        {
            if (!_captured) Capture();
            if (_sun != null)
            {
                _sun.color = NightSun;
                _sun.intensity = NightSunIntensity;
            }
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = NightAmbient;

            // The windows show the camera background, so it has to be night too.
            foreach (var entry in _cameras)
            {
                if (entry.Camera == null) continue;
                entry.Camera.clearFlags = CameraClearFlags.SolidColor;
                entry.Camera.backgroundColor = NightSky;
            }
        }

        private void RestoreDay()
        {
            if (!_captured) return;
            if (_sun != null)
            {
                _sun.color = _daySunColor;
                _sun.intensity = _daySunIntensity;
            }
            RenderSettings.ambientMode = _dayAmbientMode;
            RenderSettings.ambientLight = _dayAmbient;
            foreach (var entry in _cameras)
            {
                if (entry.Camera == null) continue;
                entry.Camera.clearFlags = entry.ClearFlags;
                entry.Camera.backgroundColor = entry.Background;
            }
        }
    }
}
