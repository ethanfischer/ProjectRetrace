using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectRetrace
{
    /// <summary>
    /// A round-locked opening into a room with no door of its own. The stair barrier splits
    /// the house by height; this one seals a volume, because a room is a box and the kitchen
    /// has a five-metre open side rather than a doorway. It reads the same round clock as the
    /// door locks and FloorGate, so it carries no state through retries or couch handovers.
    /// While locked the collider is solid to full height and the keys never hide inside the
    /// sealed volume. The prop is a baby gate that is simply gone from the unlock round on:
    /// an open gate left standing in a doorway reads as furniture to walk around. The HUD
    /// says nothing about it: a gate that was there and is not is the whole message. The gate is knee-high and the
    /// collider is not, which every player will test once; the veil pane (GateVeil shader,
    /// gateVeilEnabled) is kept for playtests where that reads as an invisible wall rather
    /// than a rule.
    /// </summary>
    public class RoomGate : MonoBehaviour
    {
        [Tooltip("World volumes sealed while locked. Keys never hide inside any of them until the unlock round. Several because a gated corridor plus the wing behind it is an L, not a box.")]
        public Bounds[] sealedAreas = new Bounds[0];

        [Tooltip("Seconds the veil takes to fade once the gate unlocks.")]
        public float revealSeconds = 1.2f;

        [Tooltip("The gate prop, shown only while locked. Optional: without one only the collider and veil gate the room.")]
        public GameObject gateProp;

        [Tooltip("The veil pane's renderer. Shown only while gateVeilEnabled is on.")]
        public Renderer veil;

        private static readonly int StrengthId = Shader.PropertyToID("_Strength");

        private Collider _collider;
        private MaterialPropertyBlock _block;
        private float _veil;

        /// <summary>Displayed round the gate opens on. One room, one config field; a second
        /// gated room gets its own field rather than a lookup table nobody can read.</summary>
        private static int UnlockRound => RetraceConfig.Current.kitchenUnlockRound;

        public static bool Enabled => UnlockRound > 0;

        public static bool Locked =>
            Enabled
            && GameDirector.Instance != null
            && GameDirector.Instance.StealthRound + 1 < UnlockRound;

        private void Awake()
        {
            _collider = GetComponent<Collider>();
            _block = new MaterialPropertyBlock();
        }

        private void Start()
        {
            // The veil samples the camera's opaque texture. Without it the pane is a flat
            // tint that lies about what it is, so refuse to run rather than look half right.
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (VeilEnabled && (pipeline == null || !pipeline.supportsCameraOpaqueTexture))
            {
                Debug.LogError("[RoomGate] The active URP asset has Opaque Texture off -- the GateVeil shader has nothing to sample. Turn it on in the pipeline asset.", this);
            }

            // Start at rest: the search phase should not open on a veil fading in.
            _veil = Locked ? 1f : 0f;
            Apply();
        }

        private void Update()
        {
            var target = Locked ? 1f : 0f;
            var step = Time.deltaTime / Mathf.Max(revealSeconds, 0.01f);
            _veil = Mathf.MoveTowards(_veil, target, step);
            Apply();
        }

        private static bool VeilEnabled => RetraceConfig.Current.gateVeilEnabled;

        private void Apply()
        {
            if (_collider != null) _collider.enabled = Locked;
            if (gateProp != null && gateProp.activeSelf != Locked) gateProp.SetActive(Locked);
            if (veil == null) return;
            veil.enabled = VeilEnabled && _veil > 0f;
            veil.GetPropertyBlock(_block);
            _block.SetFloat(StrengthId, _veil);
            veil.SetPropertyBlock(_block);
        }

        /// <summary>Whether a key may hide at this point in the current round: never inside
        /// a locked gate's volume. Unlike the stairs, an opened room does not become the
        /// only place keys hide -- it is one room, not half a house.</summary>
        public static bool Allows(Vector3 worldPoint)
        {
            if (!Locked) return true;
            foreach (var gate in FindObjectsByType<RoomGate>(FindObjectsSortMode.None))
            {
                foreach (var area in gate.sealedAreas)
                {
                    if (area.Contains(worldPoint)) return false;
                }
            }

            return true;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.25f);
            foreach (var area in sealedAreas) Gizmos.DrawCube(area.center, area.size);
        }
    }
}
