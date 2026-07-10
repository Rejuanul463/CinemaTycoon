using UnityEngine;
using UnityEngine.InputSystem;

public class InputHandler : MonoBehaviour
{
    [Header("Input Actions")]
    public InputActionReference Move;
    public InputActionReference Fire;
    public InputActionReference Jump;
    public InputActionReference Crouch;
    public InputActionReference Sprint;
    public  InputActionReference look;
    public InputActionReference rightClick;
    
    [Header("Input Values")]
    public Vector3 moveDirection;
    public Vector2 lookDirection;
    public bool sprint;

    // One-frame triggers
    public bool jump;
    public bool LeftClick;
    public bool crouch;
    public bool RightClick;


    private void Start()
    {
        
    }

    private void OnEnable()
    {
        Move.action.Enable();
        Fire.action.Enable();
        Jump.action.Enable();
        Crouch.action.Enable();
        Sprint.action.Enable();
        rightClick.action.Enable();

        Fire.action.performed += OnFire;
        Jump.action.performed += OnJump;
        rightClick.action.started += OnParryStarted;
        rightClick.action.canceled += OnParryEnded;
        Sprint.action.started += OnSprintStarted;
        Sprint.action.canceled += OnSprintCanceled;

        Crouch.action.performed += OnCrouchStarted;
        // Crouch.action.canceled += OnCrouchCanceled;
    }

    private void OnDisable()
    {
        rightClick.action.started -= OnParryStarted;
        rightClick.action.canceled -= OnParryEnded;
        
        Fire.action.performed -= OnFire;
        Jump.action.performed -= OnJump;

        Sprint.action.started -= OnSprintStarted;
        Sprint.action.canceled -= OnSprintCanceled;

        Crouch.action.performed -= OnCrouchStarted;
        // Crouch.action.canceled -= OnCrouchCanceled;

        Move.action.Disable();
        Fire.action.Disable();
        Jump.action.Disable();
        Crouch.action.Disable();
        Sprint.action.Disable();
    }

    private void Update()
    {
        // Continuous movement input
        Vector2 moveInput = Move.action.ReadValue<Vector2>();
        moveDirection = new Vector3(moveInput.x, 0f, moveInput.y).normalized;
        
        lookDirection = look.action.ReadValue<Vector2>();
        
        // Reset one-frame triggers
        jump = false;
        LeftClick = false;
    }

    private void LateUpdate()
    {
        // Reset triggers after everyone has had a chance to read them this frame.
        jump = false;
        LeftClick = false;
        crouch = false;
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        jump = true;
    }

    private void OnFire(InputAction.CallbackContext context)
    {
        LeftClick = true;
    }

    private void OnSprintStarted(InputAction.CallbackContext context)
    {
        sprint = true;
    }

    private void OnSprintCanceled(InputAction.CallbackContext context)
    {
        sprint = false;
    }

    private void OnCrouchStarted(InputAction.CallbackContext context)
    {
        crouch = true;
    }

    private void OnParryStarted(InputAction.CallbackContext context)
    {
        LeftClick = true;
    }

    private void OnParryEnded(InputAction.CallbackContext context)
    {
        LeftClick = false;
    }
}
