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
        [Tooltip("List of staff prefabs. One is picked at random on each hire. " +
                 "Every entry must have a Staff component.")]
        [SerializeField] private GameObject[] staffPrefabs;

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
            var gm = GameManager.Instance;
            if (gm != null && gm.Spawner != null)
            {
                bool cashierOnDuty = HasRoleOnDuty(StaffRole.Cashier);
                var front = gm.Spawner.GetFrontOfQueue();
                if (front != null) front.NotifyCashierReady(cashierOnDuty);
            }

            // Idle Janitors passively clean the hall.
            _idleCleanTimer += Time.deltaTime;
            if (_idleCleanTimer >= 1f)
            {
                _idleCleanTimer = 0f;
                if (HasRoleOnDuty(StaffRole.Janitor) && gm != null && gm.Schedule != null)
                    gm.Schedule.CleanHall(idleCleanAmount);
            }
        }

        public bool TryHire(StaffRole role)
        {
            var cfg = GetConfig(role);
            if (cfg == null) return false;

            var gm = GameManager.Instance;
            if (gm == null || gm.Economy == null) return false;

            if (!gm.Economy.CanAfford(cfg.hireCost))
            {
                Debug.Log($"[StaffManager] Cannot afford ${cfg.hireCost} to hire {role}.");
                return false;
            }

            gm.Economy.Spend(cfg.hireCost, $"Hire {role}");

            var prefab = PickStaffPrefab();
            if (prefab == null)
            {
                Debug.LogError("[StaffManager] No staff prefab assigned to staffPrefabs.", this);
                return false;
            }

            Vector3 station = GetStationPosition(role);
            GameObject go = Instantiate(prefab, station, Quaternion.identity);
            var staff = go.GetComponent<Staff>();
            if (staff == null)
            {
                Debug.LogError($"[StaffManager] Prefab '{prefab.name}' is missing a Staff component.", prefab);
                Destroy(go);
                return false;
            }
            staff.Initialize(role, cfg, station);
            _activeStaff.Add(staff);
            OnStaffHired?.Invoke(staff);
            return true;
        }

        /// <summary>Returns a random entry from staffPrefabs, or null if the list is empty.</summary>
        private GameObject PickStaffPrefab()
        {
            if (staffPrefabs == null || staffPrefabs.Length == 0) return null;
            return staffPrefabs[UnityEngine.Random.Range(0, staffPrefabs.Length)];
        }

        public bool HasRoleOnDuty(StaffRole role)
        {
            foreach (var s in _activeStaff)
                if (s.Role == role) return true;
            return false;
        }

        /// <summary>
        /// Returns the configured hire cost for the given role, or 0 if no config
        /// is assigned. Used by the HUD to gate hire buttons by affordability.
        /// </summary>
        public float GetHireCost(StaffRole role)
        {
            var cfg = GetConfig(role);
            return cfg != null ? cfg.hireCost : 0f;
        }

        public void EnqueueTask(StaffTask task) => _pendingTasks.Enqueue(task);

        /// <summary>
        /// Removes a staff member from the active roster, raises OnStaffFired,
        /// and destroys the underlying GameObject. Safe to call with null.
        /// </summary>
        public void FireStaff(Staff staff)
        {
            if (staff == null) return;
            if (!_activeStaff.Remove(staff)) return;
            OnStaffFired?.Invoke(staff);
            if (staff.gameObject != null) Destroy(staff.gameObject);
        }

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
                OnComplete = () =>
                {
                    var gm = GameManager.Instance;
                    if (gm != null && gm.Events != null) gm.Events.ResolveEvent(evt);
                }
            });
        }
    }
}
