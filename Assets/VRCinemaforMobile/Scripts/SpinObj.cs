using UnityEngine;

public class SpinObj : MonoBehaviour
{
    public float rotationleft = 360;
    public float rotationSpeed = 10;
    public float rotation;

    void Start()
    {
    }
    
    void Update()
    {
        if (Input.GetKey(KeyCode.R))
        {
            rotation = rotationSpeed * Time.deltaTime;

            if (rotationleft > 0)
            {
                rotationleft -= rotation;
            }
            else
            {
                rotation = rotationleft;
                rotationleft = 0;
            }

            transform.Rotate(0, rotation, 0);
        }
    }
}
