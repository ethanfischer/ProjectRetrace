using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// Pushes the "Look" block of RetraceConfig into the toon shaders and the sun. The
    /// shaders read globals rather than material properties so the config stays the only
    /// copy of each value: a player's edit lands on every material at once and never bakes
    /// into a .mat. Polls the config's identity each frame because Save and Reload replace
    /// the Current instance and there is no change event to subscribe to.
    /// </summary>
    public class LookSettings : MonoBehaviour
    {
        private RetraceConfig _applied;

        private void Update()
        {
            var config = RetraceConfig.Current;
            if (ReferenceEquals(config, _applied)) return;
            Apply(config);
            _applied = config;
        }

        public static void Apply(RetraceConfig config)
        {
            ApplyShaderGlobals(config);
            ApplyShadows(config.castShadows);
        }

        public static void ApplyShaderGlobals(RetraceConfig config)
        {
            Shader.SetGlobalFloat("_ToonFlat", config.flatShading ? 1f : 0f);
            Shader.SetGlobalFloat("_ToonSteps", Mathf.Clamp(config.lightBands, 1, 8));
            Shader.SetGlobalFloat("_ToonBandSoftness", Mathf.Clamp(config.bandSoftness, 0.001f, 0.5f));
            Shader.SetGlobalColor("_ToonShadowTint", ParseColor(config.shadowTint, new Color(0.42f, 0.40f, 0.62f)));
            Shader.SetGlobalFloat("_ToonAmbientBoost", Mathf.Max(0f, config.ambientBoost));

            Shader.SetGlobalFloat("_ToonOutlineEnabled", config.outline ? 1f : 0f);
            Shader.SetGlobalColor("_ToonOutlineColor", ParseColor(config.outlineColor, new Color(0.08f, 0.06f, 0.1f)));
            Shader.SetGlobalFloat("_ToonOutlineThickness", Mathf.Clamp(config.outlineThickness, 0.5f, 6f));
            Shader.SetGlobalFloat("_ToonOutlineDepthThreshold", Mathf.Max(0.001f, config.outlineDepthThreshold));
            Shader.SetGlobalFloat("_ToonOutlineNormalThreshold", Mathf.Max(0.001f, config.outlineNormalThreshold));
            Shader.SetGlobalFloat("_ToonOutlineFadeDistance", Mathf.Max(1f, config.outlineFadeDistance));
        }

        // Turning shadows off on the light itself is what makes URP skip the shadow-map
        // passes; the pipeline asset keeps them supported so this switch can go either way.
        private static void ApplyShadows(bool cast)
        {
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;
                light.shadows = cast ? LightShadows.Soft : LightShadows.None;
            }
        }

        private static Color ParseColor(string html, Color fallback)
        {
            return ColorUtility.TryParseHtmlString(html, out var color) ? color : fallback;
        }
    }
}
