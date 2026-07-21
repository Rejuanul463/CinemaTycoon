using System;
using System.Collections.Generic;
using UnityEngine;
using CinemaTycoon.Core;
using CinemaTycoon.Customers;

namespace CinemaTycoon.Events
{
    public enum GameEventType { Spill, VIPVisit }

    public class GameEvent
    {
        public GameEventType Type;
        public string Title;
        public string Description;
        public Vector3 Location;
        public float RemainingTime;
        public float TotalDuration;
        public Customer RelatedVIP;
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
        [SerializeField] private Transform[] possibleSpillLocations;
        [Tooltip("Prefab spawned on the floor when a Spill triggers. Should have a " +
                 "flat quad mesh, a dirt/soda material, and the SpillDecal component " +
                 "(which handles the fade-in / fade-out). Optional — leaving it null " +
                 "just makes spills invisible, the gameplay loop still works.")]
        [SerializeField] private GameObject spillDecalPrefab;
        [Tooltip("Vertical offset added to the spill location when instantiating the " +
                 "decal. Prevents z-fighting with the floor when the location is " +
                 "exactly on the ground plane.")]
        [SerializeField] private float spillDecalHeightOffset = 0.01f;

        [Header("VIP")]
        [SerializeField] private float vipServiceWindow = 60f;
        [SerializeField] private float vipGoodServiceReward = 20f;
        [SerializeField] private float vipPoorServicePenalty = 25f;
        [SerializeField] [Range(0f, 1f)] private float spillWeight = 0.6f;

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
            // settoig nthe new state based on the current state
            GameEventType type = UnityEngine.Random.value < spillWeight
                ? GameEventType.Spill
                : GameEventType.VIPVisit;

            if (type == GameEventType.Spill) TriggerSpill();
            else TriggerVipVisit();
        }

        private void TriggerSpill()
        {
            Vector3 loc = possibleSpillLocations != null && possibleSpillLocations.Length > 0
                ? possibleSpillLocations[UnityEngine.Random.Range(0, possibleSpillLocations.Length)].position
                : CinemaWaypoints.Instance != null
                    ? CinemaWaypoints.Instance.TicketBooth.position
                    : Vector3.zero;

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

            target.MarkAsVIP(); // see note below — added via partial? Actually we need a public method.

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

        public void ResolveEvent(GameEvent evt)
        {
            if (evt.Resolved) return;
            evt.Resolved = true;

            var gm = GameManager.Instance;
            if (gm == null) return;

            // Decal goes away the moment the spill is cleaned — play the
            // fade-out animation (SpillDecal destroys itself when finished)
            // and null the reference so the next Update tick doesn't touch it.
            TearDownDecal(evt);

            if (evt.Type == GameEventType.VIPVisit && evt.RelatedVIP != null)
            {
                float vipSat = evt.RelatedVIP.Satisfaction;
                float delta = vipSat > 60f ? vipGoodServiceReward : -vipPoorServicePenalty;
                gm.AdjustCinemaRating(delta, "VIP visit resolved");
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

            // Janitor never came. Still tear the decal down so the floor
            // doesn't keep a stale "wet floor" forever.
            TearDownDecal(evt);

            if (evt.Type == GameEventType.Spill)
                gm.AdjustCinemaRating(-unresolvedSpillPenalty, "Unresolved spill");
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
    }
}
