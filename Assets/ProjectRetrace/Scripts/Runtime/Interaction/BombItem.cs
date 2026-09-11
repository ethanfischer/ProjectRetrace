using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// The bomb: one per run, hidden in phase 1 like the keys but never in the same prop.
    /// Taking it puts it in the player's pocket; the carrier plants it inside any open
    /// drawer or cupboard, where it stays armed across every later round until it goes
    /// off. Anyone who then opens that container -- a ghost pausing at the stop that used
    /// it, or the player -- sets it off: the ghost's route leaves the run for good, the
    /// player loses a life. It spawns unarmed so finding it in phase 1 is safe.
    ///
    /// Restore keeps three facts the base pickup would forget: a carried bomb stays in
    /// the pocket, a planted bomb stays planted, a spent bomb stays gone.
    /// </summary>
    public class BombItem : PickupInteractable
    {
        public static BombItem Current { get; private set; }

        private PlayerBombCarrier _carrier;
        private IOpenable _armedIn;
        private Light _fuse;
        private Renderer _fuseGlow;
        private float _fuseSeed;

        public override string Prompt => "Take bomb";

        public bool Carried => _carrier != null;
        public bool Armed => _armedIn != null;
        public bool Spent { get; private set; }

        /// <summary>The drawer or door the bomb sits behind while armed. Read by the key
        /// spawner so the keys are never hidden in the trap.</summary>
        public IOpenable ArmedIn => _armedIn;

        /// <summary>Once planted it cannot be picked back up: opening the container is
        /// the trigger, so a safe "take it back" would contradict the rule.</summary>
        public override bool CanInteract => base.CanInteract && !Spent && !Armed && !Carried;

        protected override void OnEnable()
        {
            base.OnEnable();
            Current = this;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (Current == this) Current = null;
        }

        protected override void OnTaken(PlayerInteractor interactor)
        {
            var carrier = interactor.GetComponentInParent<PlayerBombCarrier>();
            if (carrier == null)
            {
                Debug.LogWarning("[BombItem] The player has no PlayerBombCarrier -- run Setup Scene Systems.", this);
                return;
            }

            _carrier = carrier;
            carrier.Hold(this);
            var bank = SoundBank.Instance;
            if (bank != null) SoundBank.PlayAt(bank.pickUp, transform.position);
        }

        /// <summary>The lit fuse says "armed" from across the room without a HUD label:
        /// a point light with a flicker and a small emissive tip at the top of the mesh.
        /// Built on first use, so the prefab stays the artist's.</summary>
        private void SetFuseLit(bool lit)
        {
            if (_fuse == null)
            {
                if (!lit) return;
                BuildFuse();
            }

            _fuse.enabled = lit;
            _fuseGlow.enabled = lit;
        }

        private void BuildFuse()
        {
            // Local bounds through the transform, not world bounds: those lag a frame
            // behind a move, and the fuse is built the instant the bomb lands in a drawer.
            var renderer = GetComponentInChildren<Renderer>();
            var top = transform.position;
            if (renderer != null)
            {
                var local = renderer.localBounds;
                top = renderer.transform.TransformPoint(new Vector3(local.center.x, local.max.y, local.center.z));
            }

            var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tip.name = "Fuse";
            Destroy(tip.GetComponent<Collider>());
            tip.transform.SetParent(transform, true);
            tip.transform.position = top + Vector3.up * 0.015f;
            tip.transform.localScale = Vector3.one * (0.02f / Mathf.Max(transform.lossyScale.x, 0.0001f));
            _fuseGlow = tip.GetComponent<Renderer>();
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            _fuseGlow.sharedMaterial = new Material(shader) { color = new Color(1f, 0.6f, 0.15f) };

            _fuse = tip.AddComponent<Light>();
            _fuse.type = LightType.Point;
            _fuse.color = new Color(1f, 0.55f, 0.15f);
            _fuse.range = 1.2f;
            _fuse.intensity = 2f;
            _fuse.shadows = LightShadows.None;
            _fuseSeed = Random.value * 100f;
        }

        private void Update()
        {
            if (_fuse == null || !_fuse.enabled) return;
            var flicker = Mathf.PerlinNoise(Time.time * 9f, _fuseSeed);
            _fuse.intensity = 1.2f + flicker * 1.6f;
        }

        /// <summary>Called by KeySpawner: the spawn spot is the bomb's home for the run.</summary>
        public void MakeAvailableAt(Transform spot)
        {
            transform.SetParent(spot, false);
            transform.SetPositionAndRotation(spot.position, spot.rotation);
            SurfaceRest.Settle(transform);
            CaptureInitialState();
            RestoreInitialState();
        }

        /// <summary>Plant it: from now on the spot is the bomb's restore pose, so it rides
        /// through every RestoreAll like the keys do, and the container is the trigger.</summary>
        public void PlaceIn(KeySpotMarker spot)
        {
            if (_carrier != null) _carrier.Release(this);
            _carrier = null;
            _armedIn = spot.Openable;
            MakeAvailableAt(spot.transform);
            SetFuseLit(true);
        }

        /// <summary>True, and the bomb is gone, when the given prop is the armed container.</summary>
        public static bool TryDetonateAt(InteractableBase prop)
        {
            var bomb = Current;
            if (bomb == null || !bomb.Armed || bomb.Spent) return false;
            if (!ReferenceEquals(prop, bomb._armedIn)) return false;

            bomb.Detonate();
            return true;
        }

        private void Detonate()
        {
            Spent = true;
            _armedIn = null;
            SetVisible(false);
            SetFuseLit(false);
            var bank = SoundBank.Instance;
            if (bank == null) return;
            var clip = bank.bombExplode != null ? bank.bombExplode : bank.ghostStunned;
            SoundBank.PlayAt(clip, transform.position, bank.bombExplode != null ? 1f : 0.6f, 1.5f);
        }

        /// <summary>New run: back to the unarmed pickup the spawner is about to place.</summary>
        public void ResetForRun()
        {
            if (_carrier != null) _carrier.Release(this);
            _carrier = null;
            _armedIn = null;
            Spent = false;
            SetFuseLit(false);
        }

        /// <summary>Online matches do not carry the bomb yet, so the run plays without it.</summary>
        public void Retire()
        {
            ResetForRun();
            Spent = true;
            SetVisible(false);
        }

        public override void RestoreInitialState()
        {
            if (Carried || Spent)
            {
                SetVisible(false);
                SetFuseLit(false);
                return;
            }

            base.RestoreInitialState();
            SetFuseLit(Armed);
        }
    }
}
