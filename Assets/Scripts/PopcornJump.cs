using UnityEngine;
using DG.Tweening;

public class PopcornJump : MonoBehaviour
{
    [Header("Area")]
    [SerializeField] private Transform center;
    [SerializeField] private Vector2 areaSize = new Vector2(0.5f, 0.5f);

    [Header("Jump")]
    [SerializeField] private float minJumpHeight = 0.15f;
    [SerializeField] private float maxJumpHeight = 0.35f;

    [SerializeField] private float jumpDuration = 0.25f;
    [SerializeField] private float minDelay = 0.05f;
    [SerializeField] private float maxDelay = 0.25f;

    private float baseY;

    private void Start()
    {
        baseY = transform.position.y;

        Invoke(nameof(StartJump), Random.Range(0f, 0.5f));
    }

    void StartJump()
    {
        Jump();
    }

    void Jump()
    {
        Vector3 pos = transform.position;
        pos.x = center.position.x + Random.Range(-areaSize.x / 2f, areaSize.x / 2f);
        pos.z = center.position.z + Random.Range(-areaSize.y / 2f, areaSize.y / 2f);
        pos.y = baseY;
        transform.position = pos;

        float height = Random.Range(minJumpHeight, maxJumpHeight);

        transform
            .DOMoveY(baseY + height, jumpDuration * 0.5f)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                transform
                    .DOMoveY(baseY, jumpDuration * 0.5f)
                    .SetEase(Ease.InQuad)
                    .OnComplete(() =>
                    {
                        DOVirtual.DelayedCall(Random.Range(minDelay, maxDelay), Jump);
                    });
            });
    }

    private void OnDisable()
    {
        DOTween.Kill(transform);
    }
}