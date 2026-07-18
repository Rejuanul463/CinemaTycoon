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
        [SerializeField] private Transform exitPoint;

        [Header("Staff Stations")]
        [SerializeField] private Transform cashierStation;
        [SerializeField] private Transform janitorStation;
        [SerializeField] private Transform guardStation;

        private void Awake() => Instance = this;

        public Transform SpawnPoint => spawnPoint;
        public Transform TicketBooth => ticketBooth;
        public Transform ExitPoint => exitPoint;
        public Transform CashierStation => cashierStation;
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
