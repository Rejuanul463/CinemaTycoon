using UnityEngine;
using CinemaTycoon.Core;

namespace CinemaTycoon.Customers
{
    /// <summary>
    /// Per-gender, single-occupancy bathroom tracker. A customer can only enter
    /// a bathroom if no other customer of the same gender is currently using
    /// it. If a customer rolls "I need the bathroom" and the slot is taken, the
    /// trip is skipped (they stay seated or continue to the chair) — they do
    /// not queue. The next poll tick will roll again.
    ///
    /// Also drives the mid-show "need to go" poll for seated customers so the
    /// designer doesn't have to wire a separate monitor component into the
    /// scene. Pre-show bathroom trips (after buying a ticket) are driven
    /// directly from the Customer FSM (see PurchasingState / PopcornState),
    /// not from here.
    /// </summary>
    public class BathroomManager : MonoBehaviour
    {
        public static BathroomManager Instance { get; private set; }

        [Header("Mid-Show Trigger")]
        [Tooltip("Per-second chance a seated customer will need the bathroom. " +
                 "Keep this small — it is rolled every pollInterval for every " +
                 "seated customer, so the effective per-customer rate is roughly " +
                 "perSecondNeedChance * (show duration in seconds).")]
        [SerializeField, Range(0f, 0.5f)] private float perSecondNeedChance = 0.03f;

        [Tooltip("Seconds between polls. Lower = more responsive but more work " +
                 "per frame. 1s is plenty for a tycoon.")]
        [SerializeField] private float pollInterval = 1f;

        // Reference to the customer currently occupying each bathroom. The
        // reservation is held by the customer for the entire trip
        // (GoingToBathroom → UsingBathroom → ReturningFromBathroom) and
        // released by Customer.ReleaseBathroom() at the end.
        private Customer _femaleOccupant;
        private Customer _maleOccupant;

        private float _pollTimer;

        public bool FemaleOccupied => _femaleOccupant != null;
        public bool MaleOccupied => _maleOccupant != null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[BathroomManager] Duplicate instance detected — keeping the existing one.", this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>True while some customer is currently inside a bathroom.</summary>
        public bool IsOccupiedFor(Gender gender)
        {
            return gender == Gender.Female ? FemaleOccupied : MaleOccupied;
        }

        /// <summary>
        /// Reserve the matching-gender bathroom slot for <paramref name="c"/>.
        /// Returns the bathroom Transform on success, or null if the slot is
        /// already taken (or no bathroom waypoint is assigned for that gender).
        /// A failed reservation leaves no side effects on the caller — the
        /// caller can safely try again on the next poll.
        /// </summary>
        public bool TryReserve(Customer c, out Transform bathroom)
        {
            bathroom = null;
            if (c == null) return false;
            var wp = CinemaWaypoints.Instance;
            if (wp == null) return false;

            if (c.Gender == Gender.Female)
            {
                if (wp.FemaleBathroom == null) return false;
                if (_femaleOccupant != null) return false;
                _femaleOccupant = c;
                bathroom = wp.FemaleBathroom;
                return true;
            }
            else
            {
                if (wp.MaleBathroom == null) return false;
                if (_maleOccupant != null) return false;
                _maleOccupant = c;
                bathroom = wp.MaleBathroom;
                return true;
            }
        }

        /// <summary>
        /// Release a slot if (and only if) it is currently held by
        /// <paramref name="c"/>. Idempotent — calling twice is harmless, and
        /// calling with a different customer is a no-op. The customer also
        /// calls this in OnDestroy as a safety net so a scene reload or
        /// unexpected despawn never strands the slot.
        /// </summary>
        public void Release(Customer c)
        {
            if (c == null) return;
            if (_femaleOccupant == c) _femaleOccupant = null;
            else if (_maleOccupant == c) _maleOccupant = null;
        }

        private void Update()
        {
            // Per-second roll for seated customers to need the bathroom. Uses
            // unscaled-safe Time.deltaTime: when the game is paused
            // (Time.timeScale = 0) deltaTime is 0, so no roll happens and
            // existing trips are not interrupted.
            _pollTimer += Time.deltaTime;
            if (_pollTimer < pollInterval) return;
            _pollTimer = 0f;

            var gm = GameManager.Instance;
            var spawner = gm != null ? gm.Spawner : null;
            if (spawner == null) return;

            // Snapshot iteration via a small list: TryGoToBathroom may
            // transition the customer (state change) but does not despawn
            // them, so the underlying HashSet is not mutated. The local
            // copy also protects us if a future change does remove from the
            // set during the trip.
            var active = new System.Collections.Generic.List<Customer>(spawner.ActiveCustomers);
            for (int i = 0; i < active.Count; i++)
            {
                var c = active[i];
                if (c == null) continue;
                if (!c.IsSeated) continue;
                // Skip if the customer's bathroom is already full this tick —
                // avoids needlessly waking the seated model + re-sitting
                // them on a guaranteed-fail attempt.
                if (IsOccupiedFor(c.Gender)) continue;
                if (Random.value < perSecondNeedChance)
                    c.TryGoToBathroom(fromSeat: true);
            }
        }
    }
}
