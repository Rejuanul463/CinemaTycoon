using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
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
            // Dispatch queued tasks to nearest idle staff of the required role.
            while (_pendingTasks.Count > 0)
            {
                var task = _pendingTasks.Dequeue();
                var candidate = FindNearestIdleStaff(task.TargetPosition, task.RequiredRole);
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

        private Staff FindNearestIdleStaff(Vector3 target, StaffRole? requiredRole = null)
        {
            Staff best = null;
            float bestDist = float.MaxValue;
            foreach (var s in _activeStaff)
            {
                if (s.IsBusy) continue;
                if (requiredRole.HasValue && s.Role != requiredRole.Value) continue;
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
                // Cashier prefers the dedicated, reachable CashierWorkPoint (placed near
                // the booth but off the obstacle cluster) so it doesn't fight the booth's
                // colliders. Falls back to CashierStation if the work point is unassigned.
                StaffRole.Cashier => ResolveCashierHome(wp),
                StaffRole.Janitor => wp.JanitorStation != null ? wp.JanitorStation.position : Vector3.zero,
                StaffRole.Guard   => wp.GuardStation != null ? wp.GuardStation.position : Vector3.zero,
                _ => Vector3.zero
            };
        }

        private static Vector3 ResolveCashierHome(CinemaWaypoints wp)
        {
            if (wp.CashierWorkPoint != null) return wp.CashierWorkPoint.position;
            if (wp.CashierStation != null) return wp.CashierStation.position;
            return Vector3.zero;
        }

        /// <summary>
        /// Auto-divert a Janitor to a Spill event's location. When the Janitor
        /// finishes the task, the cleanup callback resolves the event.
        /// </summary>
        private void HandleEventTriggered(GameEvent evt)
        {
            if (evt.Type != GameEventType.Spill) return;

            // Pre-flight NavMesh check. The most common cause of "the janitor
            // never shows up to clean the spill" is that the event's Location
            // (or one of the random Possible Spill Locations) sits off the
            // baked NavMesh — the agent accepts the destination, computes no
            // path, and silently stays put. Sample the NavMesh first; if the
            // point isn't reachable, skip the task with a clear warning
            // pointing the designer at the offending config.
            const float SampleRadius = 1.5f; // 1.5m tolerance — Spill Locations may float slightly off-mesh
            if (!NavMesh.SamplePosition(evt.Location, out _, SampleRadius, NavMesh.AllAreas))
            {
                Debug.LogWarning($"[StaffManager] Spill at {evt.Location} is not on the NavMesh " +
                                 $"(sample radius {SampleRadius}m). Janitor task skipped — " +
                                 $"check that 'Possible Spill Locations' (or whatever produced this point) " +
                                 $"are placed on a walkable surface. Spill will time out and apply its penalty.");
                return;
            }

            Debug.Log($"[StaffManager] Spill task enqueued at {evt.Location} (decal: {(evt.PhysicalDecal != null ? evt.PhysicalDecal.name : "<none>")})");
            EnqueueTask(new StaffTask
            {
                TargetPosition = evt.Location,
                // Tie the task to the actual decal GameObject (if any). Staff
                // then watches the reference and cancels the task if the decal
                // is destroyed mid-walk (e.g. the spill expired). Tasks without
                // a decal target are position-only and run to completion.
                TargetDecal = evt.PhysicalDecal,
                Priority = 0,
                // Spill cleanup is the Janitor's job — a closer idle Cashier
                // or Guard must not be diverted from their own station.
                RequiredRole = StaffRole.Janitor,
                OnComplete = () =>
                {
                    var gm = GameManager.Instance;
                    if (gm != null && gm.Events != null) gm.Events.ResolveEvent(evt);
                }
            });
        }
    }
}
