using UnityEngine;

public class DoorOpenOrClose : MonoBehaviour
{
    [Header("Door Frames")]
    public Transform doorFrame1;
    public Transform doorFrame2;

    [Header("Settings")]
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float rotationSpeed = 3f;

    private Quaternion frame1ClosedRotation;
    private Quaternion frame2ClosedRotation;

    private Quaternion frame1OpenRotation;
    private Quaternion frame2OpenRotation;

    private bool isOpen = false;

    private void Start()
    {
        frame1ClosedRotation = doorFrame1.localRotation;
        frame2ClosedRotation = doorFrame2.localRotation;

        frame1OpenRotation = frame1ClosedRotation * Quaternion.Euler(0f, openAngle, 0f);
        frame2OpenRotation = frame2ClosedRotation * Quaternion.Euler(0f, -openAngle, 0f);
    }

    private void Update()
    {
        Quaternion target1 = isOpen ? frame1OpenRotation : frame1ClosedRotation;
        Quaternion target2 = isOpen ? frame2OpenRotation : frame2ClosedRotation;

        doorFrame1.localRotation = Quaternion.Slerp(
            doorFrame1.localRotation,
            target1,
            rotationSpeed * Time.deltaTime
        );

        doorFrame2.localRotation = Quaternion.Slerp(
            doorFrame2.localRotation,
            target2,
            rotationSpeed * Time.deltaTime
        );
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Character"))
        {
            OpenDoor();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Character"))
        {
            CloseDoor();
        }
    }

    private void OpenDoor()
    {
        isOpen = true;
    }

    private void CloseDoor()
    {
        isOpen = false;
    }
}