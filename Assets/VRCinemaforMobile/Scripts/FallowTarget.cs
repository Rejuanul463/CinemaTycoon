using UnityEngine;

public class FallowTarget : MonoBehaviour
{
    public Transform target;

    void Start()
    {
    }

    void Update()
    {
        if (target != null)
        {
            transform.position = target.transform.position;
        }
    }
}
