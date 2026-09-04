using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectRetrace
{
    /// <summary>
    /// The spectator's whole job: draw the turn owner's stream. The player rig becomes a
    /// puppet wearing a visible avatar, the ghost pool becomes puppets, and the house
    /// mirrors whatever the owner's snapshots say is open. First person by default -- you
    /// see exactly what your opponent sees, ghost rounding the corner included; a chase cam
    /// for watching your own ghost stalk them, and free-fly for the whole floor.
    /// </summary>
    public class SpectatorRig : MonoBehaviour
    {
        public enum View { FirstPerson, Chase, Free }

        private const float FreeFlySpeed = 6f;
        private const float FreeFlyLookScale = 0.1f;
        private static readonly Vector3 ChaseOffset = new Vector3(0f, 0.9f, -2.6f);

        public GameDirector director;
        public FirstPersonController player;

        private readonly SnapshotBuffer _buffer = new SnapshotBuffer();
        private GameObject _avatar;
        private Vector3 _flyPosition;
        private float _flyYaw;
        private float _flyPitch;

        public bool Active { get; private set; }
        public View CurrentView { get; private set; }
        public View NextView => (View)(((int)CurrentView + 1) % 3);
        private bool FreeFly => CurrentView == View.Free;
        public bool HasStream => _buffer.Count > 0;
        public int StreamedSentries => _buffer.Latest != null ? _buffer.Latest.sentries.Count : 0;

        public void Begin()
        {
            if (Active) return;
            Active = true;
            _buffer.Clear();
            if (player != null) player.SetPuppet(true);
            EnsureAvatar();
            ApplyView(View.FirstPerson);
        }

        public void End()
        {
            if (!Active) return;
            Active = false;
            if (player != null) player.SetPuppet(false);
            if (_avatar != null) _avatar.SetActive(false);
        }

        public void OnRoundStart(RoundStartMsg message)
        {
            _buffer.Clear();
            InteractableRegistry.ApplyOpenables(message.props);
        }

        public void OnSnapshot(SnapshotMsg snapshot)
        {
            if (!Active) return;
            _buffer.Push(snapshot);
            // Prop state is applied on arrival, not on the delayed timeline: a drawer opening
            // a tenth of a second early is invisible, a drawer that opens late looks wrong
            // against the avatar already reaching for it.
            InteractableRegistry.ApplyOpenables(snapshot.props);
        }

        private void Update()
        {
            if (!Active || ConfigMenu.IsOpen) return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[RetraceConfig.Current.SpectatorCameraKey].wasPressedThisFrame)
            {
                ApplyView(NextView);
            }

            if (FreeFly) FlyInput();
        }

        private void LateUpdate()
        {
            if (!Active) return;
            if (!_buffer.Sample(RetraceConfig.Current.spectatorDelaySeconds, out var from, out var to, out var t)) return;

            var pose = from.player;
            var next = to.player;
            var position = Vector3.Lerp(pose.p, next.p, t);
            var yaw = Mathf.LerpAngle(pose.yaw, next.yaw, t);
            var pitch = Mathf.Lerp(pose.pitch, next.pitch, t);

            if (_avatar != null)
            {
                _avatar.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
                // In first person the avatar would sit inside the camera; a hider has no
                // body to show in any view.
                _avatar.SetActive(CurrentView != View.FirstPerson && !next.hiding);
            }

            if (player != null)
            {
                if (FreeFly) player.SetPuppetPose(_flyPosition, _flyYaw, _flyPitch);
                else player.SetPuppetPose(position, yaw, pitch);
            }

            ApplySentries(from, to, t);
        }

        private void ApplySentries(SnapshotMsg from, SnapshotMsg to, float t)
        {
            if (director == null) return;
            var sentries = director.Sentries;
            for (var i = 0; i < to.sentries.Count; i++)
            {
                var b = to.sentries[i];
                if (b.i < 0 || b.i >= sentries.Count) continue;
                var a = FindSentry(from, b.i) ?? b;
                sentries[b.i].ApplyPuppet(
                    Vector3.Lerp(a.p, b.p, t),
                    Mathf.LerpAngle(a.yaw, b.yaw, t),
                    (SentryState)b.state,
                    Mathf.Lerp(a.alpha, b.alpha, t));
            }
        }

        private static SentrySnap FindSentry(SnapshotMsg snapshot, int index)
        {
            for (var i = 0; i < snapshot.sentries.Count; i++)
            {
                if (snapshot.sentries[i].i == index) return snapshot.sentries[i];
            }

            return null;
        }

        private void ApplyView(View view)
        {
            CurrentView = view;
            if (player != null)
            {
                if (view == View.Free)
                {
                    _flyPosition = player.transform.position + Vector3.up * 0.9f;
                    _flyYaw = player.transform.eulerAngles.y;
                    _flyPitch = 20f;
                }

                player.SetCameraOffset(view == View.Chase ? ChaseOffset : Vector3.zero);
            }

            FirstPersonController.LockCursor(view == View.Free);
        }

        private void FlyInput()
        {
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (mouse != null)
            {
                var look = mouse.delta.ReadValue() * (FreeFlyLookScale * RetraceConfig.Current.mouseSensitivity);
                _flyYaw += look.x;
                _flyPitch = Mathf.Clamp(_flyPitch - look.y, -89f, 89f);
            }

            if (keyboard == null) return;
            var input = Vector3.zero;
            if (keyboard.wKey.isPressed) input.z += 1f;
            if (keyboard.sKey.isPressed) input.z -= 1f;
            if (keyboard.dKey.isPressed) input.x += 1f;
            if (keyboard.aKey.isPressed) input.x -= 1f;
            if (keyboard.eKey.isPressed) input.y += 1f;
            if (keyboard.qKey.isPressed) input.y -= 1f;
            var rotation = Quaternion.Euler(_flyPitch, _flyYaw, 0f);
            _flyPosition += rotation * input * (FreeFlySpeed * Time.unscaledDeltaTime);
        }

        /// <summary>The turn owner has no body of their own -- first person never needed
        /// one -- so the spectator conjures a capsule to follow.</summary>
        private GameObject EnsureAvatar()
        {
            if (_avatar != null) return _avatar;

            _avatar = new GameObject("Spectated Player");
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(_avatar.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(nose.GetComponent<Collider>());
            nose.transform.SetParent(_avatar.transform, false);
            nose.transform.localPosition = new Vector3(0f, 1.6f, 0.3f);
            nose.transform.localScale = new Vector3(0.12f, 0.12f, 0.25f);

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                var material = new Material(shader) { color = new Color(0.9f, 0.9f, 0.95f) };
                foreach (var renderer in _avatar.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = material;
            }

            return _avatar;
        }
    }
}
