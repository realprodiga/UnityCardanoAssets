using UnityEngine;

namespace Prodiga.CardanoSDK.Demo
{
    /// <summary>
    /// Combined third-person/first-person camera + movement controller. Attach to the player's
    /// root GameObject (which needs a CharacterController) and assign a Camera for it to drive.
    ///
    /// Camera:
    ///   - Left-click + drag: orbits the camera only (yaw/pitch). The character's facing is
    ///     untouched, so you can freely look around without turning.
    ///   - Right-click + drag: orbits the camera AND steers the character to face the camera's
    ///     yaw in real time, even while standing still.
    ///   - Mouse scroll wheel: zooms smoothly between first-person (distance 0, camera snaps to
    ///     EyeSocketOffset and hides HideWhenFirstPerson) and a configurable third-person distance,
    ///     with optional collision avoidance so the camera doesn't clip through geometry.
    ///
    /// Movement:
    ///   - WASD moves relative to the camera's current yaw.
    ///   - Rotation priority: right-click steering (if held) always wins; otherwise the character
    ///     turns to face its movement direction while moving, and holds its last facing while idle.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Camera")]
        [Tooltip("The camera this script drives. Keep it independent in the hierarchy (not parented under the player) -- this script fully controls its position/rotation every frame.")]
        public Camera PlayerCamera;
        [Tooltip("Local-space offset from the player's origin used as the camera's orbit pivot (roughly chest height).")]
        public Vector3 CameraPivotOffset = new Vector3(0f, 0.6f, 0f);
        [Tooltip("Local-space offset used as the exact camera position in first-person mode (roughly eye height, nudged slightly forward).")]
        public Vector3 EyeSocketOffset = new Vector3(0f, 0.9f, 0.15f);
        public float FieldOfView = 60f;

        [Header("Zoom / POV")]
        public float MinZoomDistance = 0f;
        public float MaxZoomDistance = 8f;
        public float DefaultZoomDistance = 4f;
        public float ZoomStep = 1f;
        [Tooltip("How long (seconds) the smoothed zoom takes to catch up to the target distance. Lower = snappier, higher = smoother/heavier.")]
        public float ZoomSmoothTime = 0.15f;
        [Tooltip("Below this distance, the camera is treated as first-person.")]
        public float FirstPersonThreshold = 0.5f;

        [Header("Look / Orbit")]
        [Tooltip("Left-click + drag orbits the camera only, without turning the character.")]
        public bool EnableLeftClickOrbit = true;
        [Tooltip("Right-click + drag orbits the camera AND turns the character to match.")]
        public bool EnableRightClickSteer = true;
        public float MouseSensitivityX = 3f;
        public float MouseSensitivityY = 2f;
        [Range(-89f, 0f)] public float MinPitch = -40f;
        [Range(0f, 89f)] public float MaxPitch = 80f;
        public bool InvertY = false;
        public float StartingYaw = 0f;
        public float StartingPitch = 15f;

        [Header("Camera Collision")]
        public bool EnableCameraCollision = true;
        [Tooltip("Exclude the Player layer from this mask, or the camera will collide with the player's own body.")]
        public LayerMask CameraCollisionMask = ~0;
        public float CollisionRadius = 0.2f;
        [Tooltip("Extra distance pulled in from the collision point so the camera doesn't sit exactly on a surface.")]
        public float CollisionBuffer = 0.15f;

        [Header("First Person")]
        [Tooltip("Renderers to disable while in first-person mode (e.g. the player's body mesh).")]
        public Renderer[] HideWhenFirstPerson;

        [Header("Cursor")]
        [Tooltip("Locks and hides the OS cursor while either orbit button is held, then restores it on release.")]
        public bool LockCursorWhileDragging = true;

        [Header("Movement")]
        [Tooltip("Optional: the visual model to rotate. If left empty, this GameObject's own transform is rotated instead.")]
        public Transform ModelToRotate;
        public float WalkSpeed = 4f;
        public float RunSpeed = 7f;
        public KeyCode RunKey = KeyCode.LeftShift;
        [Tooltip("Degrees per second the model turns to face its movement direction.")]
        public float TurnSpeed = 540f;
        [Tooltip("Degrees per second the model turns to match the camera while steering (right-click held). Usually higher than TurnSpeed for a direct, responsive feel.")]
        public float SteerTurnSpeed = 720f;

        [Header("Jumping & Gravity")]
        public bool AllowJump = true;
        public float JumpHeight = 1.2f;
        public float Gravity = -20f;
        [Tooltip("Small constant downward force applied while grounded, so the CharacterController stays firmly planted.")]
        public float GroundedStickForce = -2f;

        [Header("Ground Check")]
        [Tooltip("Local-space offset from the player's origin used to test for ground contact (roughly at the feet).")]
        public Vector3 GroundCheckOffset = new Vector3(0f, -1f, 0f);
        public float GroundCheckRadius = 0.25f;
        [Tooltip("Exclude the Player layer, or the character will always detect itself as grounded.")]
        public LayerMask GroundMask = ~0;

        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public bool IsFirstPerson => _currentZoomDistance <= FirstPersonThreshold;
        public bool IsSteering { get; private set; }

        private CharacterController _controller;
        private Vector3 _verticalVelocity;
        private bool _isGrounded;

        private float _targetZoomDistance;
        private float _currentZoomDistance;
        private float _zoomVelocity;
        private bool _wasFirstPerson;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (ModelToRotate == null) ModelToRotate = transform;
        }

        void Start()
        {
            if (PlayerCamera != null) PlayerCamera.fieldOfView = FieldOfView;

            Yaw = StartingYaw;
            Pitch = StartingPitch;
            _targetZoomDistance = Mathf.Clamp(DefaultZoomDistance, MinZoomDistance, MaxZoomDistance);
            _currentZoomDistance = _targetZoomDistance;
        }

        void Update()
        {
            HandleLookInput();
            HandleZoomInput();
            HandleMovement();
        }

        void LateUpdate()
        {
            UpdateCamera();
        }

        // ---------------------------------------------------------------- Look / Zoom

        private void HandleLookInput()
        {
            bool leftHeld = EnableLeftClickOrbit && Input.GetMouseButton(0);
            bool rightHeld = EnableRightClickSteer && Input.GetMouseButton(1);
            bool anyHeld = leftHeld || rightHeld;

            IsSteering = rightHeld;

            if (LockCursorWhileDragging)
            {
                if (anyHeld && Cursor.lockState != CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                else if (!anyHeld && Cursor.lockState == CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }

            if (!anyHeld) return;

            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");
            float yInvert = InvertY ? 1f : -1f;

            Yaw += mouseX * MouseSensitivityX;
            Pitch += mouseY * MouseSensitivityY * yInvert;
            Pitch = Mathf.Clamp(Pitch, MinPitch, MaxPitch);
        }

        private void HandleZoomInput()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Approximately(scroll, 0f)) return;

            // The raw scroll axis is small (~0.1 per notch); scale it so ZoomStep behaves intuitively.
            _targetZoomDistance -= scroll * ZoomStep * 10f;
            _targetZoomDistance = Mathf.Clamp(_targetZoomDistance, MinZoomDistance, MaxZoomDistance);
        }

        private void UpdateCamera()
        {
            if (PlayerCamera == null) return;

            _currentZoomDistance = Mathf.SmoothDamp(_currentZoomDistance, _targetZoomDistance, ref _zoomVelocity, ZoomSmoothTime);

            bool firstPerson = IsFirstPerson;
            if (firstPerson != _wasFirstPerson)
            {
                SetHiddenRenderers(firstPerson);
                _wasFirstPerson = firstPerson;
            }

            Vector3 pivotPosition = transform.TransformPoint(CameraPivotOffset);
            Quaternion rotation = Quaternion.Euler(Pitch, Yaw, 0f);

            if (firstPerson)
            {
                Vector3 eyePosition = transform.TransformPoint(EyeSocketOffset);
                PlayerCamera.transform.SetPositionAndRotation(eyePosition, rotation);
                return;
            }

            Vector3 desiredPosition = pivotPosition - (rotation * Vector3.forward * _currentZoomDistance);
            float finalDistance = _currentZoomDistance;

            if (EnableCameraCollision)
            {
                Vector3 direction = desiredPosition - pivotPosition;
                float directionMagnitude = direction.magnitude;
                if (directionMagnitude > 0.0001f)
                {
                    Vector3 directionNormalized = direction / directionMagnitude;
                    if (Physics.SphereCast(pivotPosition, CollisionRadius, directionNormalized, out RaycastHit hit, directionMagnitude, CameraCollisionMask, QueryTriggerInteraction.Ignore))
                    {
                        finalDistance = Mathf.Max(MinZoomDistance, hit.distance - CollisionBuffer);
                    }
                }
            }

            PlayerCamera.transform.SetPositionAndRotation(pivotPosition - (rotation * Vector3.forward * finalDistance), rotation);
        }

        private void SetHiddenRenderers(bool hidden)
        {
            if (HideWhenFirstPerson == null) return;
            foreach (var meshRenderer in HideWhenFirstPerson)
            {
                if (meshRenderer != null) meshRenderer.enabled = !hidden;
            }
        }

        // ---------------------------------------------------------------- Movement

        private void HandleMovement()
        {
            _isGrounded = Physics.CheckSphere(transform.TransformPoint(GroundCheckOffset), GroundCheckRadius, GroundMask, QueryTriggerInteraction.Ignore);
            if (_isGrounded && _verticalVelocity.y < 0f)
            {
                _verticalVelocity.y = GroundedStickForce;
            }

            Vector3 moveDirection = ComputeMoveDirection();
            float speed = Input.GetKey(RunKey) ? RunSpeed : WalkSpeed;

            ApplyRotation(moveDirection);

            if (AllowJump && _isGrounded && Input.GetButtonDown("Jump"))
            {
                _verticalVelocity.y = Mathf.Sqrt(JumpHeight * -2f * Gravity);
            }

            _verticalVelocity.y += Gravity * Time.deltaTime;

            Vector3 motion = moveDirection * speed + Vector3.up * _verticalVelocity.y;
            _controller.Move(motion * Time.deltaTime);
        }

        private void ApplyRotation(Vector3 moveDirection)
        {
            // Right-click steering takes priority: turn to match the camera even while standing
            // still, and even if a strafe direction would otherwise imply a different facing.
            if (IsSteering)
            {
                Quaternion steerRotation = Quaternion.Euler(0f, Yaw, 0f);
                ModelToRotate.rotation = Quaternion.RotateTowards(ModelToRotate.rotation, steerRotation, SteerTurnSpeed * Time.deltaTime);
                return;
            }

            if (moveDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
                ModelToRotate.rotation = Quaternion.RotateTowards(ModelToRotate.rotation, targetRotation, TurnSpeed * Time.deltaTime);
            }
            // Otherwise: idle and not steering -- hold the last facing.
        }

        private Vector3 ComputeMoveDirection()
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            if (Mathf.Approximately(horizontal, 0f) && Mathf.Approximately(vertical, 0f)) return Vector3.zero;

            Quaternion cameraYawRotation = Quaternion.Euler(0f, Yaw, 0f);
            Vector3 forward = cameraYawRotation * Vector3.forward;
            Vector3 right = cameraYawRotation * Vector3.right;

            Vector3 direction = forward * vertical + right * horizontal;
            direction.y = 0f;
            return direction.normalized;
        }
    }
}
