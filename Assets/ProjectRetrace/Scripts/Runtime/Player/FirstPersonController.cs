using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// Minimal first-person controller on the legacy Input Manager: WASD, mouse look, sprint,
    /// jump. Deliberately plain -- the interesting part of this game is the trail, not the
    /// locomotion.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [Header("Look")]
        public Transform cameraPivot;
        [SerializeField] private float mouseSensitivity = 2.2f;
        [SerializeField] private float pitchLimit = 89f;

        [Header("Move")]
        [SerializeField] private float walkSpeed = 3.4f;
        [SerializeField] private float sprintSpeed = 6.0f;
        [SerializeField] private float jumpSpeed = 4.5f;
        [SerializeField] private float gravity = -18f;

        private CharacterController _controller;
        private float _pitch;
        private float _verticalVelocity;
        private bool _inputEnabled = true;

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
            Move();
        }

        private void Look()
        {
            var mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            var mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            transform.Rotate(Vector3.up, mouseX, Space.Self);

            _pitch = Mathf.Clamp(_pitch - mouseY, -pitchLimit, pitchLimit);
            if (cameraPivot != null)
            {
                cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }
        }

        private void Move()
        {
            var input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            if (input.sqrMagnitude > 1f) input.Normalize();

            var speed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : walkSpeed;
            var motion = transform.TransformDirection(input) * speed;

            if (_controller.isGrounded)
            {
                // A small downward bias keeps isGrounded stable on slopes and stair edges.
                _verticalVelocity = -2f;
                if (Input.GetButtonDown("Jump")) _verticalVelocity = jumpSpeed;
            }
            else
            {
                _verticalVelocity += gravity * Time.deltaTime;
            }

            motion.y = _verticalVelocity;
            _controller.Move(motion * Time.deltaTime);
        }
    }
}
