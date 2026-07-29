using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using CinemaTycoon.Core;
using CinemaTycoon.Customers;

namespace CinemaTycoon.Events
{
    public enum GameEventType { Spill, VIPVisit, RowdyCustomer, ProjectorBreakdown, ToiletClog }

    public class GameEvent
    {
        public GameEventType Type;
        public string Title;
        public string Description;
        public Vector3 Location;
        public float RemainingTime;
        public float TotalDuration;
        public Customer RelatedVIP;
        public Customer RelatedCustomer;
        public bool IsGuardEscorted;
        public bool Resolved;

        /// <summary>
        /// Optional visual proxy. For Spill events, this is the SpillDecal instance
        /// sitting on the floor; the EventManager destroys it when the event is
        /// resolved (janitor cleaned) or expires (timer ran out). Null for events
        /// without a physical representation (e.g. VIPVisit).
        /// </summary>
        public GameObject PhysicalDecal;

        public float Progress => 1f - Mathf.Clamp01(RemainingTime / Mathf.Max(0.001f, TotalDuration));
    }

    public class EventManager : MonoBehaviour
    {
        [Header("Timing")]
        [SerializeField] private float minInterval = 25f;
        [SerializeField] private float maxInterval = 45f;

        [Header("Spill")]
        [SerializeField] private float spillDuration = 30f;
        [SerializeField] private float unresolvedSpillPenalty = 15f;
        [Tooltip("Designer-placed transforms where spills may spawn. Each entry must " +
                 "point at a live GameObject in the scene at runtime — destroyed / " +
                 "missing entries are skipped automatically, and if every entry is " +
                 "invalid the spill falls back to the TicketBooth waypoint.")]
        [SerializeField] private Transform[] possibleSpillLocations;
        [Tooltip("Prefab spawned on the floor when a Spill triggers. Should have a " +
                 "flat quad mesh, a dirt/soda material, and the SpillDecal component. " +
                 "Optional — leaving it null just makes spills invisible.")]
        [SerializeField] private GameObject spillDecalPrefab;
        [Tooltip("Vertical offset added to the spill location when instantiating the " +
                 "decal. Prevents z-fighting with the floor when the location is " +
                 "exactly on the ground plane.")]
        [SerializeField] private float spillDecalHeightOffset = 0.01f;

        [Header("VIP")]
        [SerializeField] private float vipServiceWindow = 60f;
        [SerializeField] private float vipGoodServiceReward = 20f;
        [SerializeField] private float vipPoorServicePenalty = 25f;
        [SerializeField] [Range(0f, 1f)] private float spillWeight = 0.4f;

        [Header("Rowdy Customer")]
        [SerializeField] private float rowdyDuration = 35f;
        [SerializeField] private float rowdyPenalty = 15f;
        [SerializeField] private float rowdyResolutionReward = 15f;

        private readonly List<GameEvent> _activeEvents = new();
        private float _nextEventTime;
        private bool _initialized;

        public IReadOnlyList<GameEvent> ActiveEvents => _activeEvents;

        public static event Action<GameEvent> OnEventTriggered;
        public static event Action<GameEvent> OnEventResolved;
        public static event Action<GameEvent> OnEventExpired;

        public void Initialize()
        {
            _nextEventTime = Time.time + UnityEngine.Random.Range(minInterval, maxInterval);
            _initialized = true;
        }

        private void OnEnable()  => Customer.OnTicketPurchased += HandleTicketPurchased;
        private void OnDisable() => Customer.OnTicketPurchased -= HandleTicketPurchased;

        /// <summary>
        /// Editor-time helper: draw the configured Possible Spill Locations as
        /// coloured spheres so the designer can see at a glance whether they're
        /// actually on the NavMesh. Green = on mesh, red = off mesh. Helps
        /// catch the most common "janitor never shows up" configuration bug
        /// before runtime.
        /// </summary>
        private void OnDrawGizmos()
        {
            if (possibleSpillLocations == null) return;
            for (int i = 0; i < possibleSpillLocations.Length; i++)
            {
                var t = possibleSpillLocations[i];
                if (t == null) continue;
                bool onMesh = NavMesh.SamplePosition(t.position, out _, 1.5f, NavMesh.AllAreas);
                Gizmos.color = onMesh ? Color.green : Color.red;
                Gizmos.DrawWireSphere(t.position, 0.4f);
            }
        }

        private void Update()
        {
            if (!_initialized) return;

            if (Time.time >= _nextEventTime)
            {
                TriggerRandomEvent();
                _nextEventTime = Time.time + UnityEngine.Random.Range(minInterval, maxInterval);
            }

            // Tick active events backwards so we can remove safely.
            for (int i = _activeEvents.Count - 1; i >= 0; i--)
            {
                var evt = _activeEvents[i];
                if (evt.Resolved) { _activeEvents.RemoveAt(i); continue; }
                evt.RemainingTime -= Time.deltaTime;
                if (evt.RemainingTime <= 0f)
                {
                    ExpireEvent(evt);
                    _activeEvents.RemoveAt(i);
                }
            }
        }

        private void TriggerRandomEvent()
        {
            float roll = UnityEngine.Random.value;
            if (roll < spillWeight) TriggerSpill();
            else if (roll < spillWeight + 0.15f) TriggerVipVisit();
            else if (roll < spillWeight + 0.30f) TriggerRowdyCustomer();
            else if (roll < spillWeight + 0.45f) TriggerProjectorBreakdown();
            else TriggerToiletClog();
        }

        private void TriggerSpill()
        {
            Vector3 rawLoc = TryGetRandomSpillLocation(out Vector3 candidateLoc)
                ? candidateLoc
                : CinemaWaypoints.Instance != null
                    ? CinemaWaypoints.Instance.TicketBooth.position
                    : Vector3.zero;

            if (!TryProjectOntoNavMesh(rawLoc, 2f, out Vector3 loc, out string failureReason))
            {
                Debug.LogWarning($"[EventManager] Spill location {rawLoc} could not be projected onto the " +
                                 $"NavMesh ({failureReason}). Spill not spawned — this is a configuration " +
                                 $"error in 'Possible Spill Locations'.");
                return;
            }

            var evt = new GameEvent
            {
                Type = GameEventType.Spill,
                Title = "Spill in the lobby!",
                Description = "A customer knocked over a soda. Send the Janitor.",
                Location = loc,
                RemainingTime = spillDuration,
                TotalDuration = spillDuration
            };

            // Spawn the physical decal so the spill is visible to the player.
            // The prefab is optional — gameplay (event timer, janitor task, rating
            // impact) runs even if no decal was assigned. The decal component
            // handles its own fade-in; destruction is managed by ResolveEvent /
            // ExpireEvent (see the GameEvent.PhysicalDecal doc).
            if (spillDecalPrefab != null)
            {
                Vector3 decalPos = loc + Vector3.up * spillDecalHeightOffset;
                evt.PhysicalDecal = Instantiate(spillDecalPrefab, decalPos, Quaternion.identity);
            }

            _activeEvents.Add(evt);
            OnEventTriggered?.Invoke(evt);
        }

        /// <summary>
        /// Pick a random live Transform from <c>possibleSpillLocations</c>.
        /// Returns false if the array is unset, empty, or every entry has been
        /// destroyed. Two-pass (count, then pick) keeps the roll fair across
        /// valid entries regardless of how many stale ones sit in the array.
        /// </summary>
        private bool TryGetRandomSpillLocation(out Vector3 location)
        {
            location = default;
            if (possibleSpillLocations == null || possibleSpillLocations.Length == 0) return false;

            int validCount = 0;
            for (int i = 0; i < possibleSpillLocations.Length; i++)
                if (possibleSpillLocations[i] != null) validCount++;

            if (validCount == 0) return false;

            int target = UnityEngine.Random.Range(0, validCount);
            int seen = 0;
            for (int i = 0; i < possibleSpillLocations.Length; i++)
            {
                var t = possibleSpillLocations[i];
                if (t == null) continue;
                if (seen == target)
                {
                    location = t.position;
                    return true;
                }
                seen++;
            }

            return false; // unreachable — validCount > 0 guarantees a hit above
        }

        /// <summary>
        /// Project a raw world position onto the NavMesh. The most common caller
        /// is a designer-placed Transform (Possible Spill Location) that floats
        /// a few centimetres above the floor and therefore sits off the baked
        /// mesh. We snap to the nearest walkable point within the radius; if
        /// nothing is found (caller is way off the mesh, e.g. the camera is
        /// flying above the level) we return false with a reason so the caller
        /// can log a useful error instead of silently spawning a stranded spill.
        /// </summary>
        private static bool TryProjectOntoNavMesh(Vector3 raw, float radius, out Vector3 projected, out string reason)
        {
            if (NavMesh.SamplePosition(raw, out NavMeshHit hit, radius, NavMesh.AllAreas))
            {
                projected = hit.position;
                reason = null;
                return true;
            }
            projected = raw;
            reason = $"no walkable surface within {radius:F1}m";
            return false;
        }

        private void TriggerVipVisit()
        {
            // Find a non-VIP customer currently in the cinema to elevate.
            var gm = GameManager.Instance;
            if (gm == null || gm.Spawner == null) return;

            Customer target = null;
            foreach (var c in gm.Spawner.ActiveCustomers)
            {
                if (!c.IsVIP) { target = c; break; }
            }
            if (target == null) return; // skip this round

            target.MarkAsVIP();

            var evt = new GameEvent
            {
                Type = GameEventType.VIPVisit,
                Title = "VIP Visit!",
                Description = "A VIP is approaching. Serve them promptly for a big reward.",
                Location = target.transform.position,
                RemainingTime = vipServiceWindow,
                TotalDuration = vipServiceWindow,
                RelatedVIP = target
            };
            _activeEvents.Add(evt);
            OnEventTriggered?.Invoke(evt);
        }

        private void TriggerRowdyCustomer()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Spawner == null) return;

            Customer target = null;
            foreach (var c in gm.Spawner.ActiveCustomers)
            {
                if (!c.IsVIP && !c.IsRowdy && c.gameObject.activeInHierarchy)
                {
                    target = c;
                    break;
                }
            }
            if (target == null) return;

            target.MarkAsRowdy();

            var evt = new GameEvent
            {
                Type = GameEventType.RowdyCustomer,
                Title = "Rowdy Customer!",
                Description = "A customer is causing a disruption. Send a Guard to escort them out.",
                Location = target.transform.position,
                RemainingTime = rowdyDuration,
                TotalDuration = rowdyDuration,
                RelatedCustomer = target
            };
            _activeEvents.Add(evt);
            OnEventTriggered?.Invoke(evt);
        }

        private void TriggerProjectorBreakdown()
        {
            var wp = CinemaWaypoints.Instance;
            Vector3 loc = wp != null && wp.TicketBooth != null ? wp.TicketBooth.position : Vector3.zero;
            var evt = new GameEvent
            {
                Type = GameEventType.ProjectorBreakdown,
                Title = "Projector Failure!",
                Description = "The theater projector broke down! Send a Janitor to fix it immediately.",
                Location = loc,
                RemainingTime = 35f,
                TotalDuration = 35f
            };
            _activeEvents.Add(evt);
            OnEventTriggered?.Invoke(evt);
        }

        private void TriggerToiletClog()
        {
            var wp = CinemaWaypoints.Instance;
            Vector3 loc = wp != null && wp.MaleBathroom != null ? wp.MaleBathroom.position : Vector3.zero;
            var evt = new GameEvent
            {
                Type = GameEventType.ToiletClog,
                Title = "Clogged Bathroom!",
                Description = "A bathroom stall is clogged. Send a Janitor to unclog it.",
                Location = loc,
                RemainingTime = 40f,
                TotalDuration = 40f
            };
            _activeEvents.Add(evt);
            OnEventTriggered?.Invoke(evt);
        }

        public void ResolveEvent(GameEvent evt)
        {
            if (evt.Resolved) return;
            evt.Resolved = true;

            var gm = GameManager.Instance;
            if (gm == null) return;

            // Decal goes away the moment the spill is cleaned.
            TearDownDecal(evt);

            if (evt.Type == GameEventType.VIPVisit && evt.RelatedVIP != null)
            {
                float vipSat = evt.RelatedVIP.Satisfaction;
                float bonus = evt.IsGuardEscorted ? 15f : 0f;
                float delta = (vipSat > 60f ? vipGoodServiceReward : -vipPoorServicePenalty) + bonus;
                string reason = evt.IsGuardEscorted ? "VIP visit resolved (Guard escorted!)" : "VIP visit resolved";
                gm.AdjustCinemaRating(delta, reason);
            }
            else if (evt.Type == GameEventType.RowdyCustomer)
            {
                gm.AdjustCinemaRating(rowdyResolutionReward, "Rowdy customer escorted out by Guard");
            }
            else if (evt.Type == GameEventType.ProjectorBreakdown)
            {
                gm.AdjustCinemaRating(5f, "Projector repaired by Janitor");
            }
            else if (evt.Type == GameEventType.ToiletClog)
            {
                gm.AdjustCinemaRating(4f, "Toilet unclogged by Janitor");
            }
            else
            {
                gm.AdjustCinemaRating(2f, "Spill cleaned");
            }

            OnEventResolved?.Invoke(evt);
        }

        private void ExpireEvent(GameEvent evt)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            // Janitor/Guard never came. Still tear the decal down.
            TearDownDecal(evt);

            if (evt.Type == GameEventType.Spill)
                gm.AdjustCinemaRating(-unresolvedSpillPenalty, "Unresolved spill");
            else if (evt.Type == GameEventType.RowdyCustomer)
            {
                if (evt.RelatedCustomer != null && evt.RelatedCustomer.IsRowdy)
                    evt.RelatedCustomer.EscortOutByGuard();
                gm.AdjustCinemaRating(-rowdyPenalty, "Rowdy customer caused disruption");
            }
            else if (evt.Type == GameEventType.ProjectorBreakdown)
            {
                gm.AdjustCinemaRating(-20f, "Projector failure ruined show");
                if (gm.Economy != null) gm.Economy.Spend(100f, "Refunds for broken show");
            }
            else if (evt.Type == GameEventType.ToiletClog)
            {
                gm.AdjustCinemaRating(-15f, "Unresolved toilet clog angered audience");
            }
            else
                gm.AdjustCinemaRating(-vipPoorServicePenalty, "VIP left unserved");

            OnEventExpired?.Invoke(evt);
        }

        /// <summary>
        /// Fade out and destroy the event's physical decal (if any). Centralised
        /// here so ResolveEvent and ExpireEvent behave identically and we never
        /// leak a decal GameObject when an event ends by either path. No-op if
        /// the event had no decal (e.g. VIPVisit, or spillDecalPrefab was null).
        /// </summary>
        private static void TearDownDecal(GameEvent evt)
        {
            if (evt?.PhysicalDecal == null) return;

            // Prefer the animated fade if the SpillDecal component is present;
            // fall back to a hard destroy so a missing component can't leak the GO.
            if (evt.PhysicalDecal.TryGetComponent(out SpillDecal decal))
                decal.FadeOutAndDestroy();
            else
                Destroy(evt.PhysicalDecal);

            evt.PhysicalDecal = null;
        }

        /// <summary>
        /// VIP service is detected via Customer.OnTicketPurchased: if the ticket
        /// buyer is the VIP flagged by an active event, resolve that event.
        /// </summary>
        private void HandleTicketPurchased(Customer c, float amount)
        {
            foreach (var evt in _activeEvents)
            {
                if (evt.Type == GameEventType.VIPVisit && evt.RelatedVIP == c && !evt.Resolved)
                {
                    ResolveEvent(evt);
                    return;
                }
            }
        }

        // Dev-only hook used by CheatManager.
        public void DevForceNextEvent() => _nextEventTime = Time.time;

        /// <summary>
        /// Dev-only hook used by CheatManager. Triggers a Spill event at a
        /// specific world position (typically the main camera's position so
        /// testers can validate the cleanup loop without waiting for the
        /// random timer). Bypasses the <c>possibleSpillLocations</c> array
        /// and projects the position onto the NavMesh.
        /// </summary>
        public void DevForceSpillAt(Vector3 worldPosition)
        {
            if (!_initialized) Initialize();

            if (!TryProjectOntoNavMesh(worldPosition, 4f, out Vector3 loc, out string reason))
            {
                Debug.LogWarning($"[EventManager] DevForceSpillAt: {worldPosition} could not be projected " +
                                 $"onto the NavMesh ({reason}). Move the camera over a walkable surface " +
                                 $"(the lobby floor) and try again.");
                return;
            }

            var evt = new GameEvent
            {
                Type = GameEventType.Spill,
                Title = "Spill (debug)",
                Description = "Manually triggered spill from CheatManager.",
                Location = loc,
                RemainingTime = spillDuration,
                TotalDuration = spillDuration
            };

            if (spillDecalPrefab != null)
            {
                Vector3 decalPos = loc + Vector3.up * spillDecalHeightOffset;
                evt.PhysicalDecal = Instantiate(spillDecalPrefab, decalPos, Quaternion.identity);
            }

            _activeEvents.Add(evt);
            OnEventTriggered?.Invoke(evt);
        }
    }
}
