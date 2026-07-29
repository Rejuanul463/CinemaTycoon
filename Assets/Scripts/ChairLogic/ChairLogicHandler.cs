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
        chairs.Clear();
        OccupiedChairLogic[] chairsToSearch = GetComponentsInChildren<OccupiedChairLogic>(true);
        chairs.AddRange(chairsToSearch);

        var wp = CinemaTycoon.Core.CinemaWaypoints.Instance;
        Vector3 referencePoint = wp != null && wp.TicketBooth != null ? wp.TicketBooth.position : Vector3.zero;

        chairs.Sort((a, b) =>
        {
            if (a == null || b == null) return 0;
            float distA = (a.transform.position - referencePoint).sqrMagnitude;
            float distB = (b.transform.position - referencePoint).sqrMagnitude;
            return distB.CompareTo(distA); // Descending distance = front row first
        });
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
    /// Reserves the next free chair in front-to-back row order for the given
    /// <paramref name="occupant"/>. Returns the chair, or null if none free.
    /// </summary>
    public OccupiedChairLogic ReserveNearestFree(Customer occupant, Vector3 fromPosition)
    {
        if (chairs == null || chairs.Count == 0) return null;

        for (int i = 0; i < chairs.Count; i++)
        {
            var chair = chairs[i];
            if (chair == null || !chair.IsFree) continue;
            chair.Reserve(occupant);
            return chair;
        }

        return null;
    }

    /// <summary>Returns a list of all chairs currently marked dirty.</summary>
    public List<OccupiedChairLogic> GetDirtyChairs()
    {
        var dirtyList = new List<OccupiedChairLogic>();
        if (chairs == null) return dirtyList;
        for (int i = 0; i < chairs.Count; i++)
            if (chairs[i] != null && chairs[i].IsDirty) dirtyList.Add(chairs[i]);
        return dirtyList;
    }
}
