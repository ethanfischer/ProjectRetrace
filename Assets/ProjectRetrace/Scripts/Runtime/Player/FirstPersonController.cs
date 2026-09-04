using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectRetrace
{
    /// <summary>
    /// Minimal first-person controller on the Input System: WASD, mouse look, sprint.
    /// No jump, and no stepping up onto anything but stairs: furniture is cover to hide
    /// behind, not high ground, and ghosts retrace routes on a navmesh that has none.
    /// Deliberately plain -- the interesting part of this game is the trail, not the
    /// locomotion.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        /// <summary>The legacy Mouse X/Y axes applied this before returning a delta, so folding
        /// it in here keeps sensitivity values tuned against the old controller valid.</summary>
        private const float LegacyMouseAxisSensitivity = 0.1f;

        /// <summary>How close to a flight of stairs the feet must be before the step height
        /// opens up. Wide enough to catch the first tread from the floor, narrow enough that
        /// a couch beside the stairs stays unclimbable.</summary>
        private const float StairProbeRadius = 0.6f;

        private static readonly Collider[] Nearby = new Collider[16];

        public Transform cameraPivot;

        private CharacterController _controller;
        private float _pitch;
        private float _verticalVelocity;
        private bool _inputEnabled = true;
        private bool _movementEnabled = true;
        private bool _peeking;
        private float _peekCentreYaw;
        private bool _puppet;
        private Vector3? _eyeLocalPosition;

        /// <summary>Height of the surface the player last legitimately stood on. Any move
        /// that lifts the feet above it is undone unless stairs are within reach.</summary>
        private float _floorY;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (cameraPivot == null && Camera.main != null)
            {
                cameraPivot = Camera.main.transform;
            }
        }

        private void Start()
        {
            _floorY = transform.position.y;
            LockCursor(true);
        }

        /// <summary>Frozen during the phase transition and on the results screen.</summary>
        public void SetInputEnabled(bool inputEnabled)
        {
            _inputEnabled = inputEnabled;
            LockCursor(inputEnabled);
        }

        /// <summary>Hidden in a cupboard: you can still look around, you just can't walk
        /// through its walls.</summary>
        public void SetMovementEnabled(bool movementEnabled)
        {
            _movementEnabled = movementEnabled;
        }

        /// <summary>Peeking through a door crack: yaw is clamped around the crack and pitch
        /// is pinned level, so the view stays inside the slit the overlay draws.</summary>
        public void SetPeek(float centreYaw)
        {
            _peeking = true;
            _peekCentreYaw = centreYaw;
            _pitch = 0f;
            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.identity;
        }

        public void ClearPeek()
        {
            _peeking = false;
        }

        public static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        /// <summary>
        /// The CharacterController overwrites direct transform writes, so it has to be
        /// disabled across the move -- otherwise the player snaps straight back to spawn.
        /// </summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            SetPosition(position, rotation);
            _floorY = position.y;

            _verticalVelocity = 0f;
            _pitch = 0f;
            if (cameraPivot != null)
            {
                cameraPivot.localRotation = Quaternion.identity;
            }
        }

        private void SetPosition(Vector3 position, Quaternion rotation)
        {
            _controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            _controller.enabled = true;
        }

        /// <summary>Spectating: the rig stops being a body and becomes a camera mount. The
        /// CharacterController comes off so streamed poses are not fought by physics, and
        /// the camera slides back and up into a chase view of the streamed avatar.</summary>
        public void SetPuppet(bool puppet)
        {
            _puppet = puppet;
            _controller.enabled = !puppet;
            if (!puppet) SetCameraOffset(Vector3.zero);
        }

        public void SetPuppetPose(Vector3 position, float yaw, float pitch)
        {
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            _pitch = pitch;
            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        /// <summary>Offset from eye height, in the rig's local space.</summary>
        public void SetCameraOffset(Vector3 localOffset)
        {
            if (cameraPivot == null) return;
            if (_eyeLocalPosition == null) _eyeLocalPosition = cameraPivot.localPosition;
            cameraPivot.localPosition = _eyeLocalPosition.Value + localOffset;
        }

        private void Update()
        {
            if (!_inputEnabled || _puppet) return;

            Look();
            if (_movementEnabled) Move();
        }

        private void Look()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            var config = RetraceConfig.Current;
            var look = mouse.delta.ReadValue() * (LegacyMouseAxisSensitivity * MouseDeltaScale.Factor * config.mouseSensitivity);

            transform.Rotate(Vector3.up, look.x, Space.Self);
            if (_peeking)
            {
                ClampYawToPeek(config.peekYawDegrees);
                return;
            }

            _pitch = Mathf.Clamp(_pitch - look.y, -config.pitchLimit, config.pitchLimit);
            if (cameraPivot != null)
            {
                cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }
        }

        private void ClampYawToPeek(float halfRange)
        {
            var offset = Mathf.DeltaAngle(_peekCentreYaw, transform.eulerAngles.y);
            offset = Mathf.Clamp(offset, -halfRange, halfRange);
            transform.rotation = Quaternion.Euler(0f, _peekCentreYaw + offset, 0f);
        }

        private void Move()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            var input = ReadMoveInput(keyboard);
            if (input.sqrMagnitude > 1f) input.Normalize();
            Advance(input, keyboard.leftShiftKey.isPressed, Time.deltaTime);
        }

        /// <summary>
        /// Step height and slope limit cannot say "stairs only": the pack's chairs and
        /// cushions are mesh colliders whose slopes sit under any usable slope limit, so
        /// the controller would walk up them however low the step. Instead the move is
        /// checked against the floor the player was standing on and undone if it climbed,
        /// unless a marked flight of stairs is within reach of the feet.
        /// </summary>
        private void Advance(Vector3 input, bool sprint, float deltaTime)
        {
            var config = RetraceConfig.Current;
            var speed = sprint ? config.sprintSpeed : config.walkSpeed;
            var motion = transform.TransformDirection(input) * speed;

            var nearStairs = NearStairs();
            _controller.stepOffset = nearStairs ? config.stairStepHeight : config.stepHeight;

            if (_controller.isGrounded)
            {
                // A small downward bias keeps isGrounded stable on slopes and stair edges.
                _verticalVelocity = -2f;
            }
            else
            {
                _verticalVelocity += config.gravity * deltaTime;
            }

            motion.y = _verticalVelocity;
            var before = transform.position;
            _controller.Move(motion * deltaTime);

            var y = transform.position.y;
            if (nearStairs || y < _floorY) _floorY = y;
            else if (y > _floorY + config.stepHeight) SetPosition(before, transform.rotation);
        }

        private bool NearStairs()
        {
            var feet = transform.position + Vector3.up * (StairProbeRadius * 0.5f);
            var count = Physics.OverlapSphereNonAlloc(feet, StairProbeRadius, Nearby, ~0, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                if (Nearby[i].GetComponentInParent<Stairs>() != null) return true;
            }

            return false;
        }

        private static Vector3 ReadMoveInput(Keyboard keyboard)
        {
            var x = 0f;
            var z = 0f;

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) z -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) z += 1f;

            return new Vector3(x, 0f, z);
        }
    }
}
