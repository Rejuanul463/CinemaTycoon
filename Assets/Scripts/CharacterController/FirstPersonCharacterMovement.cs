 using UnityEngine;

public class FirstPersonCharacterMovement : MonoBehaviour
{
    private CharacterController controller;
    [SerializeField] private InputHandler inputHandler;

    [Header("Movement Parameters")] 
    [SerializeField] private float speed = 10;
    [SerializeField] private float gravity = 20f;
    
    private float verticalVelocity;
    
    [Header("Character Look Parameters")]
    public float lookSensitivity = 50f;
    [SerializeField] private Transform cameraTransform;
    private float xRotation;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    // Update is called once per frame
    void Update()
    {
        CharacterMovement();
        CharacterLook();
    }

    private void CharacterMovement()
    {
        Vector3 characterMovement = inputHandler.moveDirection;
        
        if (controller.isGrounded) verticalVelocity = -2f;
        else verticalVelocity -= gravity * Time.deltaTime;
        
        characterMovement.y = verticalVelocity;
        controller.Move(characterMovement * speed * Time.deltaTime);
    }

    private void CharacterLook()
    {
        Vector2  lookDirection = inputHandler.lookDirection;

        xRotation -= lookDirection.y;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);
        
        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        
        transform.Rotate(Vector3.up * lookDirection.x);
    }
}
