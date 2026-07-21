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

        private void Awake() => Instance = this;

        public Transform SpawnPoint => spawnPoint;
        public Transform TicketBooth => ticketBooth;
        public Transform PopcornStand => popcornStand;
        public Transform ExitPoint => exitPoint;
        public Transform CashierStation => cashierStation;
        public Transform CashierWorkPoint => cashierWorkPoint;
        public Transform JanitorStation => janitorStation;
        public Transform GuardStation => guardStation;

        public int QueueCapacity => queuePoints != null ? queuePoints.Length : 0;

        public Transform GetQueuePoint(int index)
        {
            if (queuePoints == null || queuePoints.Length == 0) return ticketBooth;
            return queuePoints[Mathf.Clamp(index, 0, queuePoints.Length - 1)];
        }
    }
}
