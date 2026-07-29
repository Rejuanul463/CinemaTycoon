using UnityEngine;
using CinemaTycoon.Customers;

public class OccupiedChairLogic : MonoBehaviour
{
    public GameObject[] characterPrefabs;
    private int randomIndex;

    [Header("Approach Point")]
        [Tooltip("Local-space offset from the chair where a customer should stand before " +
                 "sitting. Default is behind the chair (toward the row's entry aisle).")]
    [SerializeField] private Vector3 approachOffset = new Vector3(0f, 0f, -0.6f);

        [Tooltip("Optional explicit approach Transform. If assigned, overrides approachOffset.")]
    [SerializeField] private Transform approachPoint;

    [Header("Trash & Cleanliness")]
    [Tooltip("Optional child GameObject toggled on when the chair is dirty/trashed post-show.")]
    [SerializeField] private GameObject trashProp;
    [Tooltip("Chance (0..1) that a customer leaving this chair leaves trash behind.")]
    [SerializeField, Range(0f, 1f)] private float trashChance = 0.4f;

    /// <summary>True while no customer has reserved this chair for the show.</summary>
    public bool IsFree { get; private set; } = true;

    /// <summary>True while a sitting-character child is visibly shown.</summary>
    public bool IsOccupied { get; private set; }

    /// <summary>True if the chair is dirty from trash left by a previous customer.</summary>
    public bool IsDirty { get; private set; }

    /// <summary>The customer that reserved this chair (debugging aid); null when free.</summary>
    public Customer Occupant { get; private set; }

    public void SetDirty(bool dirty)
    {
        IsDirty = dirty;
        if (trashProp != null) trashProp.SetActive(dirty);
    }

    /// <summary>
    /// World position the customer should path to before sitting. Prefer the
    /// explicit <see cref="approachPoint"/> Transform; fall back to
    /// <see cref="approachOffset"/> in local space. This keeps agents in the aisle
    /// rather than trying to reach the chair center (which may be unreachable in
    /// tightly packed rows).
    /// </summary>
    public Vector3 GetApproachPosition()
    {
        if (approachPoint != null) return approachPoint.position;
        return transform.TransformPoint(approachOffset);
    }

    /// <summary>
    /// Atomically reserve this chair for <paramref name="occupant"/>. Returns false
    /// if already reserved. Does NOT show a sitting child — call OccupyChair() once
    /// the customer arrives.
    /// </summary>
    public bool Reserve(Customer occupant)
    {
        if (!IsFree) return false;
        IsFree = false;
        Occupant = occupant;
        return true;
    }

    /// <summary>
    /// Frees the reservation slot only. Does not hide a sitting child — call
    /// UnOccupyChair() first if the customer was visually seated.
    /// </summary>
    public void Release()
    {
        IsFree = true;
        Occupant = null;
    }

    public void OccupyChair()
    {
        if (IsOccupied) return;
        if (characterPrefabs == null || characterPrefabs.Length == 0) return;

        randomIndex = Random.Range(0, characterPrefabs.Length);
        characterPrefabs[randomIndex].SetActive(true);
        IsOccupied = true;
    }

    public void UnOccupyChair()
    {
        if (!IsOccupied) return;
        if (characterPrefabs != null && characterPrefabs.Length > 0)
            characterPrefabs[randomIndex].SetActive(false);
        IsOccupied = false;

        if (Random.value < trashChance)
        {
            SetDirty(true);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 p = GetApproachPosition();
        Gizmos.color = new Color(0.2f, 0.9f, 0.2f, 0.9f);
        Gizmos.DrawSphere(p, 0.18f);
        Gizmos.color = new Color(0.2f, 0.9f, 0.2f, 0.4f);
        Gizmos.DrawLine(transform.position, p);
    }
}
