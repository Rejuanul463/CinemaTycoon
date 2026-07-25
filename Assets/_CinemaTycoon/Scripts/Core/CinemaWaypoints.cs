using UnityEngine;

namespace CinemaTycoon.Core
{
    /// <summary>
    /// Single source of truth for all physical navigation targets in the cinema.
    /// Keeps Customer/Staff prefabs free of Transform references.
    /// </summary>
    public class CinemaWaypoints : MonoBehaviour
    {
        public static CinemaWaypoints Instance { get; private set; }

        [Header("Customer Flow")]
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private Transform[] queuePoints;
        [SerializeField] private Transform ticketBooth;
        [Tooltip("Where customers go to buy popcorn/drinks after getting a ticket. " +
                 "Leave unassigned to skip the popcorn state entirely.")]
        [SerializeField] private Transform popcornStand;
        [SerializeField] private Transform exitPoint;

        [Header("Staff Stations")]
        [SerializeField] private Transform cashierStation;
        [Tooltip("Dedicated, navmesh-reachable spot the Cashier paths to and idles at " +
                 "while on duty. Place this NEAR the ticket booth but in an open, " +
                 "obstacle-free area — do NOT point it at the booth transform itself " +
                 "(the booth is often surrounded by colliders that block the agent). " +
                 "If left unassigned, falls back to Cashier Station.")]
        [SerializeField] private Transform cashierWorkPoint;
        [SerializeField] private Transform janitorStation;
        [SerializeField] private Transform guardStation;

        [Header("Bathrooms (one per gender — single-occupancy)")]
        [Tooltip("Where female customers walk to and stand while using the bathroom. " +
                 "Leave unassigned to disable the female bathroom entirely (female " +
                 "customers will skip the bathroom detour).")]
        [SerializeField] private Transform femaleBathroom;
        [Tooltip("Where male customers walk to and stand while using the bathroom. " +
                 "Leave unassigned to disable the male bathroom entirely (male " +
                 "customers will skip the bathroom detour).")]
        [SerializeField] private Transform maleBathroom;

        [Header("Arcade Machines (optional — not occupancy-limited)")]
        [Tooltip("Where customers walk to and stand when they 'play' an arcade game. " +
                 "Each customer picks one at random on entry. Unlike the bathroom, " +
                 "arcades are NOT single-occupancy — multiple customers can stand at " +
                 "the same machine at once. Leave the list empty to disable the " +
                 "arcade detour entirely.")]
        [SerializeField] private Transform[] arcadeMachines;

        private void Awake() => Instance = this;

        public Transform SpawnPoint => spawnPoint;
        public Transform TicketBooth => ticketBooth;
        public Transform PopcornStand => popcornStand;
        public Transform ExitPoint => exitPoint;
        public Transform CashierStation => cashierStation;
        public Transform CashierWorkPoint => cashierWorkPoint;
        public Transform JanitorStation => janitorStation;
        public Transform GuardStation => guardStation;
        public Transform FemaleBathroom => femaleBathroom;
        public Transform MaleBathroom => maleBathroom;
        public Transform[] ArcadeMachines => arcadeMachines;

        /// <summary>
        /// Pick a uniformly random non-null arcade machine. Returns null when
        /// no machines are assigned (or all assigned slots are null), which
        /// the Customer FSM treats as "skip the arcade detour this visit".
        /// Filters nulls first so a half-empty inspector list doesn't bias
        /// the pick toward the live entries.
        /// </summary>
        public Transform PickRandomArcade()
        {
            if (arcadeMachines == null || arcadeMachines.Length == 0) return null;
            int validCount = 0;
            for (int i = 0; i < arcadeMachines.Length; i++)
                if (arcadeMachines[i] != null) validCount++;
            if (validCount == 0) return null;

            int pick = UnityEngine.Random.Range(0, validCount);
            for (int i = 0; i < arcadeMachines.Length; i++)
            {
                if (arcadeMachines[i] == null) continue;
                if (pick == 0) return arcadeMachines[i];
                pick--;
            }
            return null; // unreachable
        }

        public int QueueCapacity => queuePoints != null ? queuePoints.Length : 0;

        public Transform GetQueuePoint(int index)
        {
            if (queuePoints == null || queuePoints.Length == 0) return ticketBooth;
            return queuePoints[Mathf.Clamp(index, 0, queuePoints.Length - 1)];
        }

        /// <summary>
        /// Returns a random lobby waypoint (GuardStation, TicketBooth, PopcornStand, ExitPoint)
        /// for Guard idle patrol.
        /// </summary>
        public Transform PickRandomPatrolPoint()
        {
            var candidates = new System.Collections.Generic.List<Transform>();
            if (guardStation != null) candidates.Add(guardStation);
            if (ticketBooth != null) candidates.Add(ticketBooth);
            if (popcornStand != null) candidates.Add(popcornStand);
            if (exitPoint != null) candidates.Add(exitPoint);
            if (arcadeMachines != null)
            {
                foreach (var a in arcadeMachines)
                    if (a != null) candidates.Add(a);
            }

            if (candidates.Count == 0) return null;
            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }
    }
}
