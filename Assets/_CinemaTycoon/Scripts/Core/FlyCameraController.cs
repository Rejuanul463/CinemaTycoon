using UnityEngine;

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

        private void Start()
        {
            // Capture the initial position as the main menu anchor
            _menuAnchorPosition = transform.position;
            _menuAnchorRotation = transform.rotation;

            // Apply starting state
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
        }
    }
}
