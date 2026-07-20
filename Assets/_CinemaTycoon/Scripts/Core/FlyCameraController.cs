using System;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CinemaTycoon.Core
{
    public class FlyCameraController : MonoBehaviour
    {
        public enum CameraState { MainMenu, FlyMode, CursorFree }

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

        private Vector3 _menuAnchorPosition;
        private Quaternion _menuAnchorRotation;
        
        private float _rotationX;
        private float _rotationY;
        private bool _isCursorLocked = false;

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
                UpdateFlyControls();
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
