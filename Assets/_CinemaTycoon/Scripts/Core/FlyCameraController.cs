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
        // Index into securityCameras of the camera the player is currently
        // viewing. -1 = never entered security mode (so the next entry starts
        // at index 0). Persists across re-entries so leaving and coming back
        // resumes on the same camera.
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
            // Capture the initial (scene-placed) position/rotation as the main menu
            // anchor NOW, in Awake, so any other component's Start that calls
            // SetState(MainMenu) reads a valid anchor instead of Vector3.zero
            // (which would teleport the camera to world origin).
            _menuAnchorPosition = transform.position;
            _menuAnchorRotation = transform.rotation;
        }

        private void Start()
        {
            // Apply starting state (anchor already captured in Awake).
            SetState(currentState);
        }

        private void Update()
        {
            if (currentState == CameraState.MainMenu)
                UpdateMainMenuBobbing();
            else if (currentState == CameraState.FlyMode)
            {
                UpdateFlyControls();
                // TAB in FlyMode enters security-camera mode (no-op if no
                // cameras are assigned — guards against the TAB key being
                // useful for other UI in the future).
                if (Input.GetKeyDown(KeyCode.Tab) && HasSecurityCameras())
                    SetState(CameraState.SecurityCameras);
            }
            else if (currentState == CameraState.SecurityCameras)
            {
                UpdateSecurityCameraControls();
                // TAB in security mode returns to FlyMode.
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
                // Snap to the clean menu anchor pose before capturing yaw/pitch.
                // During MainMenu the bob/pan continuously drifts transform, so
                // reading euler from the live transform would start the fly camera
                // wherever the bob happened to be — often looking off the placed
                // direction. Resetting to the anchor makes FlyMode begin exactly
                // where the menu was placed.
                transform.position = _menuAnchorPosition;
                transform.rotation = _menuAnchorRotation;
                Vector3 euler = transform.eulerAngles;
                _rotationY = euler.y;
                _rotationX = euler.x;
                SetCursorLockState(true);
            }
            else if (currentState == CameraState.CursorFree)
            {
                // Camera stays frozen in place; just release the cursor
                SetCursorLockState(false);
            }
            else if (currentState == CameraState.SecurityCameras)
            {
                // Snap to the current (or first) security camera waypoint.
                // If no waypoints are assigned, fall through to FlyMode so
                // the player isn't stranded in a state with no camera.
                if (!TryEnterSecurityCameraMode())
                {
                    currentState = CameraState.FlyMode;
                    return;
                }
                SetCursorLockState(true);
            }
        }

        // ---------- Security camera helpers ----------

        private bool HasSecurityCameras()
        {
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

            // First entry, or last index is now out of range / pointing at a
            // destroyed waypoint → start at the first non-null entry.
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
            // Alt held   → unlock cursor so player can interact with UI panels
            // Alt released → re-lock cursor for mouse-look. Same convention
            // as FlyMode so the player's muscle memory carries over.
            bool altHeld = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            if (altHeld && _isCursorLocked)
                SetCursorLockState(false);
            else if (!altHeld && !_isCursorLocked)
                SetCursorLockState(true);

            // Mouse-look when locked. Yaw is deliberately NOT clamped so the
            // camera can spin a full 360° as requested. Pitch is clamped so
            // the camera doesn't flip past the poles.
            if (_isCursorLocked)
            {
                float mouseX = Input.GetAxis("Mouse X") * lookSensitivity;
                float mouseY = Input.GetAxis("Mouse Y") * lookSensitivity;

                _rotationY += mouseX;            // 360° free yaw
                _rotationX -= mouseY;
                _rotationX = Mathf.Clamp(_rotationX, -securityPitchClamp, securityPitchClamp);

                transform.rotation = Quaternion.Euler(_rotationX, _rotationY, 0f);
            }

            // Camera cycling on mouse click. Left = previous, right = next.
            // Gated on !altHeld so left/right click is reserved for UI
            // interaction whenever the player is holding Alt (cursor free) —
            // a click on a panel button should not also cycle the camera.
            // GetMouseButtonDown so each press cycles once; holding the
            // button doesn't keep cycling.
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
            // Programmatic bobbing/floating using unscaled time (works when timescale = 0)
            float elapsed = Time.unscaledTime;
            
            // vertical position bobbing
            float offsetY = Mathf.Sin(elapsed * bobFrequency * Mathf.PI * 2f) * bobAmplitude;
            
            // gentle left-right panning rotation
            float offsetPan = Mathf.Sin(elapsed * panFrequency * Mathf.PI * 2f) * panAmplitude;
            
            transform.position = _menuAnchorPosition + new Vector3(0f, offsetY, 0f);
            transform.rotation = _menuAnchorRotation * Quaternion.Euler(0f, offsetPan, 0f);
        }

        private void UpdateFlyControls()
        {
            // Alt held   → unlock cursor so player can interact with UI panels
            // Alt released → re-lock cursor for mouse-look
            bool altHeld = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            if (altHeld && _isCursorLocked)
                SetCursorLockState(false);
            else if (!altHeld && !_isCursorLocked)
                SetCursorLockState(true);

            // Mouse Look Controls (only when locked)
            if (_isCursorLocked)
            {
                float mouseX = Input.GetAxis("Mouse X") * lookSensitivity;
                float mouseY = Input.GetAxis("Mouse Y") * lookSensitivity;

                _rotationY += mouseX;
                _rotationX -= mouseY;
                _rotationX = Mathf.Clamp(_rotationX, -85f, 85f); // prevent flipping

                transform.rotation = Quaternion.Euler(_rotationX, _rotationY, 0f);
                Debug.Log("Mouse X: " + _rotationX);
            }

            // Movement controls (Keyboard input works even if cursor is unlocked, but usually disabled for convenience)
            float speed = Input.GetKey(KeyCode.LeftShift) ? fastMoveSpeed : moveSpeed;
            Vector3 moveInput = Vector3.zero;

            if (Input.GetKey(KeyCode.W)) moveInput += transform.forward;
            if (Input.GetKey(KeyCode.S)) moveInput -= transform.forward;
            if (Input.GetKey(KeyCode.A)) moveInput -= transform.right;
            if (Input.GetKey(KeyCode.D)) moveInput += transform.right;
            
            // Q/E for vertical movement
            if (Input.GetKey(KeyCode.E)) moveInput += Vector3.up;
            if (Input.GetKey(KeyCode.Q)) moveInput -= Vector3.up;

            if (moveInput.sqrMagnitude > 0.01f)
            {
                transform.position += moveInput.normalized * speed * Time.unscaledDeltaTime;
            }
        }

        private void SetCursorLockState(bool locked)
        {
            if (_isCursorLocked == locked) return; // no-op guard so subscribers don't fire spuriously
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
