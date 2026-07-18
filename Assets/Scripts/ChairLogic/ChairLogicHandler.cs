using System.Collections.Generic;
using UnityEngine;
using CinemaTycoon.Customers;

public class ChairLogicHandler : MonoBehaviour
{
    public static ChairLogicHandler Instance { get; private set; }

    public List<OccupiedChairLogic> chairs = new List<OccupiedChairLogic>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[ChairLogicHandler] Duplicate instance detected — keeping the existing one.", this);
            return;
        }
        Instance = this;
        searchForChairs();
    }

    public void searchForChairs()
    {
        OccupiedChairLogic[] chairsToSearch = GetComponentsInChildren<OccupiedChairLogic>(true);
        chairs.AddRange(chairsToSearch);
    }

    /// <summary>True if at least one chair currently accepts a reservation.</summary>
    public bool HasFreeChair()
    {
        if (chairs == null) return false;
        for (int i = 0; i < chairs.Count; i++)
            if (chairs[i] != null && chairs[i].IsFree) return true;
        return false;
    }

    /// <summary>
    /// Reserves the free chair nearest to <paramref name="fromPosition"/> for the
    /// given <paramref name="occupant"/>. Returns the chair, or null if none free.
    /// </summary>
    public OccupiedChairLogic ReserveNearestFree(Customer occupant, Vector3 fromPosition)
    {
        if (chairs == null || chairs.Count == 0) return null;

        OccupiedChairLogic best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < chairs.Count; i++)
        {
            var chair = chairs[i];
            if (chair == null || !chair.IsFree) continue;
            float d = (chair.transform.position - fromPosition).sqrMagnitude;
            if (d < bestDist) { best = chair; bestDist = d; }
        }

        if (best != null) best.Reserve(occupant);
        return best;
    }
}
