using System;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CinemaTycoon.Core
{
    public class FlyCameraController : MonoBehaviour
    {
        public enum CameraState { MainMenu, FlyMode, CursorFree, SecurityCameras }

        [Header("State")]
        [SerializeField] private CameraState currentState = CameraState.MainMenu;

        [Header("Fly Mode Settings")]
        [SerializeField] private float moveSpeed = 15f;
        [SerializeField] private float fastMoveSpeed = 30f;
        [SerializeField] private float lookSensitivity = 2f;

        [Header("Main Menu Floating Settings")]
        [SerializeField] private float bobAmplitude = 0.4f;
        [SerializeField] private float bobFrequency = 0.5f;
        [SerializeField] private float panAmplitude = 2f;
        [SerializeField] private float panFrequency = 0.2f;

        [Header("Security Cameras")]
        [Tooltip("List of fixed camera positions. Press TAB in FlyMode to " +
                 "switch to security-camera mode, where the camera snaps to " +
                 "the current waypoint and the player can look 360° with " +
                 "the mouse. Left click cycles to the previous camera, right " +
                 "click to the next. Press TAB again to return to FlyMode. " +
                 "Empty list = TAB in FlyMode is a no-op.")]
        [SerializeField] private Transform[] securityCameras;
        [Tooltip("Pitch clamp (degrees) for the security-camera mouse-look. " +
                 "Prevents the camera from flipping upside-down at the poles. " +
                 "Yaw is intentionally unclamped so the player can spin 360°.")]
        [SerializeField, Range(10f, 89f)] private float securityPitchClamp = 85f;

        private Vector3 _menuAnchorPosition;
        private Quaternion _menuAnchorRotation;

        private float _rotationX;
        private float _rotationY;
        private bool _isCursorLocked = false;
        private int _currentSecurityCameraIndex = -1;

        /// <summary>
        /// Raised whenever the cursor lock state changes. Argument is the new
        /// locked state (true = locked, false = free). Subscribers (e.g. the
        /// HUD) use this to collapse transient UI panels when the player
        /// releases the cursor-unlock modifier (Alt).
        /// </summary>
        public static event Action<bool> OnCursorLockChanged;

        private void Awake()
        {
            _menuAnchorPosition = transform.position;
            _menuAnchorRotation = transform.rotation;
        }

        private void Start()
        {
            SetState(currentState);
        }

        private void Update()
        {
            if (currentState == CameraState.MainMenu)
                UpdateMainMenuBobbing();
            else if (currentState == CameraState.FlyMode)
            {
                UpdateFlyControls();
                if (Input.GetKeyDown(KeyCode.Tab) && HasSecurityCameras())
                    SetState(CameraState.SecurityCameras);
            }
            else if (currentState == CameraState.SecurityCameras)
            {
                UpdateSecurityCameraControls();
                if (Input.GetKeyDown(KeyCode.Tab))
                    SetState(CameraState.FlyMode);
            }
            // CursorFree: camera frozen, cursor managed externally
        }

        public void SetState(CameraState state)
        {
            currentState = state;
            if (currentState == CameraState.MainMenu)
            {
                transform.position = _menuAnchorPosition;
                transform.rotation = _menuAnchorRotation;
                SetCursorLockState(false);
            }
            else if (currentState == CameraState.FlyMode)
            {
                transform.position = _menuAnchorPosition;
                transform.rotation = _menuAnchorRotation;
                Vector3 euler = transform.eulerAngles;
                _rotationY = euler.y;
                _rotationX = euler.x;
                SetCursorLockState(true);
            }
            else if (currentState == CameraState.CursorFree)
            {
                SetCursorLockState(false);
            }
            else if (currentState == CameraState.SecurityCameras)
            {
                if (!TryEnterSecurityCameraMode())
                {
                    currentState = CameraState.FlyMode;
                    return;
                }
                SetCursorLockState(true);
            }
        }

        // ---------- Security camera helpers ----------

        private bool HasSecurityCameras()        {
            if (securityCameras == null || securityCameras.Length == 0) return false;
            for (int i = 0; i < securityCameras.Length; i++)
                if (securityCameras[i] != null) return true;
            return false;
        }

        /// <summary>
        /// Snap the camera to the current security-camera waypoint (or the
        /// first non-null one if the cached index is invalid) and seed
        /// _rotationX / _rotationY from the waypoint's rotation so the
        /// first mouse-look delta doesn't snap the view to (0,0,0).
        /// Returns false if no usable waypoint exists; the caller should
        /// fall back to a different state.
        /// </summary>
        private bool TryEnterSecurityCameraMode()
        {
            if (securityCameras == null || securityCameras.Length == 0) return false;

            if (_currentSecurityCameraIndex < 0
                || _currentSecurityCameraIndex >= securityCameras.Length
                || securityCameras[_currentSecurityCameraIndex] == null)
            {
                int first = -1;
                for (int i = 0; i < securityCameras.Length; i++)
                {
                    if (securityCameras[i] != null) { first = i; break; }
                }
                if (first < 0) return false;
                _currentSecurityCameraIndex = first;
            }

            Transform cam = securityCameras[_currentSecurityCameraIndex];
            transform.position = cam.position;
            transform.rotation = cam.rotation;
            Vector3 euler = transform.eulerAngles;
            _rotationY = euler.y;
            _rotationX = euler.x;
            return true;
        }

        /// <summary>
        /// Cycle the security camera by <paramref name="direction"/> (-1 for
        /// previous, +1 for next) and snap to it. Skips null / destroyed
        /// waypoints so an inspector mistake doesn't strand the player.
        /// </summary>
        private void SwitchSecurityCamera(int direction)
        {
            if (securityCameras == null || securityCameras.Length == 0) return;

            int count = securityCameras.Length;
            int startIndex = _currentSecurityCameraIndex < 0 ? 0 : _currentSecurityCameraIndex;
            int index = startIndex;

            // Walk the list until we find a non-null waypoint. If every
            // waypoint is null we give up silently — the player can still
            // look around from the current position until TAB takes them
            // back to FlyMode.
            for (int i = 0; i < count; i++)
            {
                index = ((index + direction) % count + count) % count;
                if (securityCameras[index] != null)
                {
                    _currentSecurityCameraIndex = index;
                    Transform cam = securityCameras[index];
                    transform.position = cam.position;
                    transform.rotation = cam.rotation;
                    Vector3 euler = transform.eulerAngles;
                    _rotationY = euler.y;
                    _rotationX = euler.x;
                    return;
                }
            }
        }

        private void UpdateSecurityCameraControls()
        {
            bool altHeld = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            if (altHeld && _isCursorLocked)
                SetCursorLockState(false);
            else if (!altHeld && !_isCursorLocked)
                SetCursorLockState(true);

            if (_isCursorLocked)
            {
                float mouseX = Input.GetAxis("Mouse X") * lookSensitivity;
                float mouseY = Input.GetAxis("Mouse Y") * lookSensitivity;

                _rotationY += mouseX;
                _rotationX -= mouseY;
                _rotationX = Mathf.Clamp(_rotationX, -securityPitchClamp, securityPitchClamp);

                transform.rotation = Quaternion.Euler(_rotationX, _rotationY, 0f);
            }

            if (!altHeld)
            {
                if (Input.GetMouseButtonDown(0)) SwitchSecurityCamera(-1);
                if (Input.GetMouseButtonDown(1)) SwitchSecurityCamera(+1);
            }
        }

        /// <summary>Force-unlocks cursor without changing camera state (e.g. for pause overlay).</summary>
        public void ForceCursorUnlock() => SetCursorLockState(false);

        /// <summary>Force-locks cursor without changing camera state.</summary>
        public void ForceCursorLock() => SetCursorLockState(true);

        private void UpdateMainMenuBobbing()
        {
            float elapsed = Time.unscaledTime;
            float offsetY = Mathf.Sin(elapsed * bobFrequency * Mathf.PI * 2f) * bobAmplitude;
            float offsetPan = Mathf.Sin(elapsed * panFrequency * Mathf.PI * 2f) * panAmplitude;
            
            transform.position = _menuAnchorPosition + new Vector3(0f, offsetY, 0f);
            transform.rotation = _menuAnchorRotation * Quaternion.Euler(0f, offsetPan, 0f);
        }

        private void UpdateFlyControls()
        {
            bool altHeld = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            if (altHeld && _isCursorLocked)
                SetCursorLockState(false);
            else if (!altHeld && !_isCursorLocked)
                SetCursorLockState(true);

            if (_isCursorLocked)
            {
                float mouseX = Input.GetAxis("Mouse X") * lookSensitivity;
                float mouseY = Input.GetAxis("Mouse Y") * lookSensitivity;

                _rotationY += mouseX;
                _rotationX -= mouseY;
                _rotationX = Mathf.Clamp(_rotationX, -85f, 85f);

                transform.rotation = Quaternion.Euler(_rotationX, _rotationY, 0f);
            }

            float speed = Input.GetKey(KeyCode.LeftShift) ? fastMoveSpeed : moveSpeed;
            Vector3 moveInput = Vector3.zero;

            if (Input.GetKey(KeyCode.W)) moveInput += transform.forward;
            if (Input.GetKey(KeyCode.S)) moveInput -= transform.forward;
            if (Input.GetKey(KeyCode.A)) moveInput -= transform.right;
            if (Input.GetKey(KeyCode.D)) moveInput += transform.right;

            if (Input.GetKey(KeyCode.E)) moveInput += Vector3.up;
            if (Input.GetKey(KeyCode.Q)) moveInput -= Vector3.up;

            if (moveInput.sqrMagnitude > 0.01f)
            {
                transform.position += moveInput.normalized * speed * Time.unscaledDeltaTime;
            }
        }

        private void SetCursorLockState(bool locked)
        {
            if (_isCursorLocked == locked) return;
            _isCursorLocked = locked;
            if (locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            OnCursorLockChanged?.Invoke(locked);
        }
    }
}
