using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectRetrace
{
    /// <summary>
    /// Minimal first-person controller on the Input System: WASD, mouse look, sprint,
    /// jump. Deliberately plain -- the interesting part of this game is the trail, not the
    /// locomotion.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        /// <summary>The legacy Mouse X/Y axes applied this before returning a delta, so folding
        /// it in here keeps sensitivity values tuned against the old controller valid.</summary>
        private const float LegacyMouseAxisSensitivity = 0.1f;

        public Transform cameraPivot;

        private CharacterController _controller;
        private float _pitch;
        private float _verticalVelocity;
        private bool _inputEnabled = true;
        private bool _movementEnabled = true;

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
            _controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            _controller.enabled = true;

            _verticalVelocity = 0f;
            _pitch = 0f;
            if (cameraPivot != null)
            {
                cameraPivot.localRotation = Quaternion.identity;
            }
        }

        private void Update()
        {
            if (!_inputEnabled) return;

            Look();
            if (_movementEnabled) Move();
        }

        private void Look()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            var config = RetraceConfig.Current;
            var look = mouse.delta.ReadValue() * (LegacyMouseAxisSensitivity * config.mouseSensitivity);

            transform.Rotate(Vector3.up, look.x, Space.Self);

            _pitch = Mathf.Clamp(_pitch - look.y, -config.pitchLimit, config.pitchLimit);
            if (cameraPivot != null)
            {
                cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }
        }

        private void Move()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            var input = ReadMoveInput(keyboard);
            if (input.sqrMagnitude > 1f) input.Normalize();

            var config = RetraceConfig.Current;
            var speed = keyboard.leftShiftKey.isPressed ? config.sprintSpeed : config.walkSpeed;
            var motion = transform.TransformDirection(input) * speed;

            if (_controller.isGrounded)
            {
                // A small downward bias keeps isGrounded stable on slopes and stair edges.
                _verticalVelocity = -2f;
                if (keyboard.spaceKey.wasPressedThisFrame) _verticalVelocity = config.jumpSpeed;
            }
            else
            {
                _verticalVelocity += config.gravity * Time.deltaTime;
            }

            motion.y = _verticalVelocity;
            _controller.Move(motion * Time.deltaTime);
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
