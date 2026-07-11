using System;
using System.Collections.Generic;
using UnityEngine;
using CinemaTycoon.Core;
using CinemaTycoon.Economy;
using CinemaTycoon.Events;
using CinemaTycoon.Schedule;

namespace CinemaTycoon.Staff
{
    public class StaffManager : MonoBehaviour
    {
        [Header("Role Configs")]
        [SerializeField] private StaffRoleData cashierConfig;
        [SerializeField] private StaffRoleData janitorConfig;
        [SerializeField] private StaffRoleData guardConfig;
        [SerializeField] private GameObject staffPrefab;

        [Header("Janitor Idle Cleaning")]
        [SerializeField] private float idleCleanAmount = 2f; // per task tick when Janitor is idle

        private readonly List<Staff> _activeStaff = new();
        private readonly Queue<StaffTask> _pendingTasks = new();
        private float _idleCleanTimer;

        public IReadOnlyList<Staff> ActiveStaff => _activeStaff;

        public static event Action<Staff> OnStaffHired;
        public static event Action<Staff> OnStaffFired;

        public void Initialize() { }

        private void OnEnable()
        {
            EventManager.OnEventTriggered += HandleEventTriggered;
        }

        private void OnDisable()
        {
            EventManager.OnEventTriggered -= HandleEventTriggered;
        }

        private void Update()
        {
            // Dispatch queued tasks to nearest idle staff.
            while (_pendingTasks.Count > 0)
            {
                var task = _pendingTasks.Dequeue();
                var candidate = FindNearestIdleStaff(task.TargetPosition);
                if (candidate != null)
                {
                    candidate.AssignTask(task);
                }
                else
                {
                    _pendingTasks.Enqueue(task); // try again next frame
                    break;
                }
            }

            // Cashier presence gate: tell the front-of-queue customer they can advance.
            bool cashierOnDuty = HasRoleOnDuty(StaffRole.Cashier);
            var front = GameManager.Instance.Spawner.GetFrontOfQueue();
            if (front != null) front.NotifyCashierReady(cashierOnDuty);

            // Idle Janitors passively clean the hall.
            _idleCleanTimer += Time.deltaTime;
            if (_idleCleanTimer >= 1f)
            {
                _idleCleanTimer = 0f;
                if (HasRoleOnDuty(StaffRole.Janitor))
                    GameManager.Instance.Schedule.CleanHall(idleCleanAmount);
            }
        }

        public bool TryHire(StaffRole role)
        {
            var cfg = GetConfig(role);
            if (cfg == null) return false;

            if (!GameManager.Instance.Economy.CanAfford(cfg.hireCost))
            {
                Debug.Log($"[StaffManager] Cannot afford ${cfg.hireCost} to hire {role}.");
                return false;
            }

            GameManager.Instance.Economy.Spend(cfg.hireCost, $"Hire {role}");

            Vector3 station = GetStationPosition(role);
            GameObject go = Instantiate(staffPrefab, station, Quaternion.identity);
            var staff = go.GetComponent<Staff>();
            staff.Initialize(role, cfg, station);
            _activeStaff.Add(staff);
            OnStaffHired?.Invoke(staff);
            return true;
        }

        public bool HasRoleOnDuty(StaffRole role)
        {
            foreach (var s in _activeStaff)
                if (s.Role == role) return true;
            return false;
        }

        public void EnqueueTask(StaffTask task) => _pendingTasks.Enqueue(task);

        private Staff FindNearestIdleStaff(Vector3 target)
        {
            Staff best = null;
            float bestDist = float.MaxValue;
            foreach (var s in _activeStaff)
            {
                if (s.IsBusy) continue;
                float d = (s.transform.position - target).sqrMagnitude;
                if (d < bestDist) { best = s; bestDist = d; }
            }
            return best;
        }

        private StaffRoleData GetConfig(StaffRole role) => role switch
        {
            StaffRole.Cashier => cashierConfig,
            StaffRole.Janitor => janitorConfig,
            StaffRole.Guard   => guardConfig,
            _ => null
        };

        private Vector3 GetStationPosition(StaffRole role)
        {
            var wp = CinemaWaypoints.Instance;
            if (wp == null) return Vector3.zero;
            return role switch
            {
                StaffRole.Cashier => wp.CashierStation.position,
                StaffRole.Janitor => wp.JanitorStation.position,
                StaffRole.Guard   => wp.GuardStation.position,
                _ => Vector3.zero
            };
        }

        /// <summary>
        /// Auto-divert a Janitor to a Spill event's location. When the Janitor
        /// finishes the task, the cleanup callback resolves the event.
        /// </summary>
        private void HandleEventTriggered(GameEvent evt)
        {
            if (evt.Type != GameEventType.Spill) return;
            EnqueueTask(new StaffTask
            {
                TargetPosition = evt.Location,
                Priority = 0,
                OnComplete = () => GameManager.Instance.Events.ResolveEvent(evt)
            });
        }
    }
}
