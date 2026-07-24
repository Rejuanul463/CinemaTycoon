using System;
using UnityEngine;
using UnityEngine.AI;
using CinemaTycoon.Core;
using CinemaTycoon.Economy;
using CinemaTycoon.Schedule;

namespace CinemaTycoon.Customers
{
    public enum CustomerStateType
    { Entering, Queuing, Purchasing, Popcorn, Watching, Leaving, Unsatisfied }

    // ---------- State base + concretes ----------

    public abstract class CustomerState
    {
        public abstract CustomerStateType Type { get; }
        protected readonly Customer Customer;
        protected CustomerState(Customer c) { Customer = c; }
        public virtual void Enter() { }
        public virtual void Tick() { }
        public virtual void Exit() { }
        protected void GoTo(CustomerStateType next) => Customer.RequestTransition(next);
    }

    public sealed class EnteringState : CustomerState
    {
        public override CustomerStateType Type => CustomerStateType.Entering;
        public EnteringState(Customer c) : base(c) { }

        public override void Enter()
        {
            Customer.Agent.isStopped = false;
            Customer.Agent.SetDestination(CinemaWaypoints.Instance.TicketBooth.position);
        }

        public override void Tick()
        {
            if (!Customer.Agent.pathPending && Customer.Agent.remainingDistance < 1.5f)
                GoTo(CustomerStateType.Queuing);
        }
    }

    public sealed class QueuingState : CustomerState
    {
        public override CustomerStateType Type => CustomerStateType.Queuing;
        public QueuingState(Customer c) : base(c) { }

        public override void Enter()
        {
            Customer.RegisterInQueue();
            Customer.Agent.isStopped = false;
            Customer.Agent.SetDestination(
                CinemaWaypoints.Instance.GetQueuePoint(Customer.QueueIndex).position);
        }

        public override void Tick()
        {
            var gm = GameManager.Instance;
            var wp = CinemaWaypoints.Instance;
            if (gm == null || wp == null) return;

            // Satisfaction drains while waiting. FasterCashier upgrade softens this.
            float patience = Customer.QueuePatiencePerSecond;
            float cashierMult = gm.Economy != null ? gm.Economy.CashierSpeedMultiplier : 1f;
            Customer.ReduceSatisfaction(patience * Time.deltaTime / cashierMult);

            if (Customer.Satisfaction <= Customer.UnsatisfiedThreshold)
            {
                GoTo(CustomerStateType.Unsatisfied);
                return;
            }

            // Re-path if our queue index changed (someone ahead left).
            Customer.Agent.SetDestination(
                wp.GetQueuePoint(Customer.QueueIndex).position);

            // Front of queue + cashier on duty → advance to purchase.
            if (Customer.IsAtFrontOfQueue && Customer.CashierReady)
                GoTo(CustomerStateType.Purchasing);
        }

        public override void Exit() => Customer.LeaveQueue();
    }

    public sealed class PurchasingState : CustomerState
    {
        public override CustomerStateType Type => CustomerStateType.Purchasing;
        public PurchasingState(Customer c) : base(c) { }

        public override void Enter()
        {
            Customer.Agent.isStopped = false;
            Customer.Agent.SetDestination(CinemaWaypoints.Instance.TicketBooth.position);
        }

        public override void Tick()
        {
            if (Customer.Agent.pathPending || Customer.Agent.remainingDistance >= 1.0f) return;
            Customer.AttemptPurchase();
            var gm = GameManager.Instance;
            if (gm == null || gm.Schedule == null) return;

            // No movie → no point buying popcorn. Bail out as unsatisfied.
            if (!gm.Schedule.IsMoviePlayingOrImminent)
            {
                GoTo(CustomerStateType.Unsatisfied);
                return;
            }

            // Self-serve popcorn detour after the ticket purchase. If the
            // PopcornStand waypoint is unassigned or this customer declines,
            // skip straight to Watching.
            if (Customer.WantsPopcorn() && CinemaWaypoints.Instance.PopcornStand != null)
                GoTo(CustomerStateType.Popcorn);
            else
                GoTo(CustomerStateType.Watching);
        }
    }

    /// <summary>
    /// Optional detour state: customer walks from the ticket booth to the
    /// popcorn stand, "buys" popcorn (instant AddIncome + state flag), and
    /// then continues to the chair. Skipped when the stand is unassigned or
    /// the customer declined (see <see cref="Customer.WantsPopcorn"/>).
    /// </summary>
    public sealed class PopcornState : CustomerState
    {
        public override CustomerStateType Type => CustomerStateType.Popcorn;
        public PopcornState(Customer c) : base(c) { }

        public override void Enter()
        {
            var wp = CinemaWaypoints.Instance;
            if (wp == null || wp.PopcornStand == null)
            {
                // Defensive: PurchasingState already gates on this, but if a
                // designer wires the state in manually we still bail safely.
                GoTo(CustomerStateType.Watching);
                return;
            }

            Customer.Agent.isStopped = false;
            Customer.Agent.SetDestination(wp.PopcornStand.position);
        }

        public override void Tick()
        {
            if (Customer.Agent.pathPending || Customer.Agent.remainingDistance >= 1.0f) return;
            Customer.AttemptPopcornPurchase();
            GoTo(CustomerStateType.Watching);
        }
    }

    public sealed class WatchingState : CustomerState
    {
        public override CustomerStateType Type => CustomerStateType.Watching;
        public WatchingState(Customer c) : base(c) { }

        public override void Enter()
        {
            var handler = ChairLogicHandler.Instance;
            if (handler == null || !handler.HasFreeChair())
            {
                GoTo(CustomerStateType.Unsatisfied);
                return;
            }

            var chair = handler.ReserveNearestFree(Customer, Customer.transform.position);
            if (chair == null)
            {
                GoTo(CustomerStateType.Unsatisfied);
                return;
            }

            Customer.AssignChair(chair);
            Customer.Agent.isStopped = false;
            Customer.Agent.SetDestination(chair.GetApproachPosition());
        }

        public override void Tick()
        {
            var gm = GameManager.Instance;
            if (gm?.Schedule == null) return;

            if (!gm.Schedule.IsMoviePlaying)
            {
                Customer.ReleaseReservedChair();
                GoTo(CustomerStateType.Leaving);
                return;
            }

            // Arrived at the reserved chair's approach point: sit down (this disables
            // the customer GO, so Tick will not run again until the show ends and
            // wakes the customer). hasPath guards against an off-mesh approach point
            // (no path → remainingDistance is 0 but we must not teleport-sit).
            if (!Customer.Agent.pathPending
                && Customer.Agent.hasPath
                && Customer.Agent.remainingDistance < Customer.ChairArrivalDistance)
            {
                Customer.SitOnChair();
            }
        }
    }

    public sealed class LeavingState : CustomerState
    {
        public override CustomerStateType Type => CustomerStateType.Leaving;
        public LeavingState(Customer c) : base(c) { }

        public override void Enter()
        {
            Customer.Agent.isStopped = false;
            Customer.Agent.SetDestination(CinemaWaypoints.Instance.ExitPoint.position);
            Customer.FinalizeSatisfaction(penalty: false);
        }

        public override void Tick()
        {
            if (!Customer.Agent.pathPending && Customer.Agent.remainingDistance < 1.0f)
                Customer.Despawn();
        }
    }

    public sealed class UnsatisfiedState : CustomerState
    {
        public override CustomerStateType Type => CustomerStateType.Unsatisfied;
        public UnsatisfiedState(Customer c) : base(c) { }

        public override void Enter()
        {
            Customer.Agent.isStopped = false;
            Customer.Agent.SetDestination(CinemaWaypoints.Instance.ExitPoint.position);
            Customer.FinalizeSatisfaction(penalty: true);
        }

        public override void Tick()
        {
            if (!Customer.Agent.pathPending && Customer.Agent.remainingDistance < 1.0f)
                Customer.Despawn();
        }
    }

    // ---------- Customer MonoBehaviour ----------

    [RequireComponent(typeof(NavMeshAgent))]
    public class Customer : MonoBehaviour
    {
        [Header("Tuning")]
        [SerializeField] private float initialSatisfaction = 80f;
        [SerializeField] private float queuePatiencePerSecond = 1.5f;
        [SerializeField] private float watchSatisfactionPerSecond = 1.5f;
        [SerializeField] private float unsatisfiedThreshold = 20f;
        [SerializeField] private float chairArrivalDistance = 0.8f;

        [Header("Popcorn")]
        [Tooltip("Base price of one popcorn sale, before the PremiumPopcorn multiplier.")]
        [SerializeField] private float popcornBasePrice = 5f;
        [Tooltip("Base chance (0..1) that a customer will detour to the popcorn stand " +
                 "after buying a ticket. PremiumPopcorn multiplies this.")]
        [SerializeField, Range(0f, 1f)] private float popcornBaseChance = 0.4f;
        [Tooltip("Disabled-by-default child GameObject (e.g. FullPopcornTubM) that " +
                 "is toggled on when the customer buys popcorn and off when they " +
                 "leave. Leave unassigned and Awake will auto-find a child named " +
                 "'FullPopcornTubM' (case-insensitive) so prefabs that already " +
                 "have the prop parented work with zero inspector wiring.")]
        [SerializeField] private GameObject popcornProp;

        // Read access for state classes
        public float QueuePatiencePerSecond => queuePatiencePerSecond;
        public float WatchSatisfactionPerSecond => watchSatisfactionPerSecond;
        public float UnsatisfiedThreshold => unsatisfiedThreshold;
        public float ChairArrivalDistance => chairArrivalDistance;
        public NavMeshAgent Agent { get; private set; }
        public float Satisfaction { get; private set; }
        public bool IsVIP { get; private set; }
        public int QueueIndex { get; private set; } = -1;
        public bool IsAtFrontOfQueue { get; private set; }
        public bool CashierReady { get; private set; }
        /// <summary>True if this customer has purchased popcorn at the stand.</summary>
        public bool HasPopcorn { get; private set; }

        // Static events — EconomyManager and HUD both listen to OnTicketPurchased.
        public static event Action<Customer, float> OnTicketPurchased;
        public static event Action<Customer, float> OnPopcornPurchased;
        public static event Action<Customer, float> OnSatisfactionFinalized;
        public static event Action<Customer> OnDespawned;

        private CustomerState _currentState;
        private CustomerSpawnManager _spawner;
        private bool _finalized;
        private OccupiedChairLogic _reservedChair;
        private float _sitStartTime;
        private bool _destroyed;

        // Animator driving — matches WorkerAnimator.controller's "isWalking" param
        // (lowercase, case-sensitive). Null-guarded so a placeholder prefab without
        // an Animator still compiles and runs.
        private Animator _animator;
        private static readonly int WalkHash = Animator.StringToHash("isWalking");
        // Same controller also has a Bool "isHolding" wired to the popcorn-carry
        // clip. Keep the name aligned with the controller parameter so the
        // transition fires when the customer picks up / drops the popcorn.
        private static readonly int HoldHash = Animator.StringToHash("isHolding");

        private void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            _animator = GetComponent<Animator>();
            // NavMeshAgent owns the transform; animator plays clips in place.
            if (_animator != null) _animator.applyRootMotion = false;

            // Auto-resolve the popcorn prop if the inspector slot is empty.
            // Every Character_*.prefab parents a FullPopcornTubM as a disabled
            // child; this lets HoldPopcorn / ReleasePopcorn just toggle
            // SetActive without per-prefab drag-and-drop.
            if (popcornProp == null)
                popcornProp = FindPopcornChild();
        }

        /// <summary>
        /// Locate the popcorn prop as a child of this customer by name. Returns
        /// null (with a one-shot warning) if nothing is found, so a missing
        /// prop surfaces clearly in the console instead of silently no-op'ing
        /// every popcorn purchase.
        /// </summary>
        private GameObject FindPopcornChild()
        {
            const string PropName = "FullPopcornTubM";
            // GetComponentsInChildren includes inactive children, which matters
            // here — the prop starts disabled in the prefab and we still want
            // to find it so HoldPopcorn can flip it on.
            var all = GetComponentsInChildren<Transform>(includeInactive: true);
            for (int i = 0; i < all.Length; i++)
            {
                if (string.Equals(all[i].name, PropName, StringComparison.OrdinalIgnoreCase))
                    return all[i].gameObject;
            }
            Debug.LogWarning($"[Customer] No '{PropName}' child found under {name} and the " +
                             $"'popcornProp' inspector slot is empty. Popcorn will be sold but no " +
                             $"prop will appear. Either parent a '{PropName}' GameObject to this " +
                             $"prefab or assign the slot in the inspector.");
            return null;
        }

        public void Initialize(CustomerSpawnManager spawner, bool isVIP)
        {
            _spawner = spawner;
            IsVIP = isVIP;
            Satisfaction = initialSatisfaction;
            ChangeState(new EnteringState(this));
        }

        private void Update()
        {
            // Drive walk animation from agent velocity (matches Staff.cs approach).
            if (_animator != null)
            {
                bool moving = Agent != null && Agent.velocity.sqrMagnitude > 0.01f;
                _animator.SetBool(WalkHash, moving);
            }
            _currentState?.Tick();
        }

        public void ChangeState(CustomerState newState)
        {
            _currentState?.Exit();
            _currentState = newState;
            _currentState.Enter();
        }

        public void RequestTransition(CustomerStateType next)
        {
            // Single dispatch point — keeps state classes ignorant of each other.
            ChangeState(next switch
            {
                CustomerStateType.Entering    => new EnteringState(this),
                CustomerStateType.Queuing     => new QueuingState(this),
                CustomerStateType.Purchasing  => new PurchasingState(this),
                CustomerStateType.Popcorn     => new PopcornState(this),
                CustomerStateType.Watching    => new WatchingState(this),
                CustomerStateType.Leaving     => new LeavingState(this),
                CustomerStateType.Unsatisfied => new UnsatisfiedState(this),
                _ => _currentState
            });
        }

        public void AddSatisfaction(float delta) =>
            Satisfaction = Mathf.Clamp(Satisfaction + delta, 0f, 100f);

        public void ReduceSatisfaction(float delta) => AddSatisfaction(-delta);

        public void SetQueueIndex(int index)
        {
            QueueIndex = index;
            IsAtFrontOfQueue = index == 0;
        }

        public void RegisterInQueue() => _spawner.AssignQueueIndex(this);
        public void LeaveQueue() => _spawner.ReleaseQueueIndex(this);

        public void NotifyCashierReady(bool ready) => CashierReady = ready;

        public void AttemptPurchase()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Schedule == null || gm.Economy == null) return;

            var schedule = gm.Schedule;
            if (!schedule.IsMoviePlayingOrImminent) return; // no movie, no sale

            float price = schedule.CurrentTicketPrice * gm.Economy.TicketRevenueMultiplier;
            gm.Economy.AddIncome(price, "Ticket sale");
            OnTicketPurchased?.Invoke(this, price);
        }

        /// <summary>
        /// Roll whether this customer will detour to the popcorn stand after the
        /// ticket purchase. Result is influenced by the PremiumPopcorn upgrade:
        /// the upgrade's multiplier (>1) raises the base chance proportionally.
        /// Capped at 1f so a high upgrade doesn't make every customer buy.
        /// </summary>
        public bool WantsPopcorn()
        {
            if (HasPopcorn) return false;
            var gm = GameManager.Instance;
            float mult = gm?.Economy != null ? gm.Economy.PopcornChanceMultiplier : 1f;
            return UnityEngine.Random.value < Mathf.Clamp01(popcornBaseChance * mult);
        }

        /// <summary>
        /// Resolve a popcorn purchase: charge the player via EconomyManager, raise
        /// the OnPopcornPurchased event (so the HUD can flash a "+$5 popcorn" line),
        /// and set <see cref="HasPopcorn"/> so the customer visually represents the
        /// purchase (callers can parent a popcorn prefab here if desired).
        /// </summary>
        public void AttemptPopcornPurchase()
        {
            if (HasPopcorn) return;
            var gm = GameManager.Instance;
            if (gm == null || gm.Economy == null) return;

            float price = popcornBasePrice * gm.Economy.PopcornRevenueMultiplier;
            gm.Economy.AddIncome(price, "Popcorn sale");
            HasPopcorn = true;
            // Drive the "isHolding" Animator parameter (UpperBody layer) so the
            // popcorn-carry clip plays for the rest of the customer's visit.
            HoldPopcorn();
            OnPopcornPurchased?.Invoke(this, price);
        }

        public void FinalizeSatisfaction(bool penalty)
        {
            if (_finalized) return;
            _finalized = true;

            float weight = IsVIP ? 2f : 1f;
            float delta = penalty
                ? -5f * weight
                : ((Satisfaction - 50f) / 50f) * 5f * weight; // +ve if satisfied, -ve if not

            var gm = GameManager.Instance;
            if (gm != null) gm.AdjustCinemaRating(delta, "Customer feedback");
            OnSatisfactionFinalized?.Invoke(this, Satisfaction);
        }

        /// <summary>Used by EventManager to elevate a normal customer to VIP mid-visit.</summary>
        public void MarkAsVIP() => IsVIP = true;

        public void AssignChair(OccupiedChairLogic chair) => _reservedChair = chair;

        public void ReleaseReservedChair()
        {
            if (_reservedChair == null) return;
            _reservedChair.Release();
            _reservedChair = null;
        }

        public void SitOnChair()
        {
            if (_reservedChair == null) return;
            _sitStartTime = Time.time;
            _reservedChair.OccupyChair();
            ScheduleManager.OnShowEnded += HandleShowEndedWhileSitting;
            gameObject.SetActive(false);
        }

        private void HandleShowEndedWhileSitting(MovieData m)
        {
            if (_destroyed) return;
            WakeFromChair();
        }

        public void WakeFromChair()
        {
            ScheduleManager.OnShowEnded -= HandleShowEndedWhileSitting;

            float elapsed = Time.time - _sitStartTime;
            var gm = GameManager.Instance;
            float comfort = gm?.Economy?.SeatComfortMultiplier ?? 1f;
            AddSatisfaction(WatchSatisfactionPerSecond * elapsed * comfort);

            if (_reservedChair != null)
            {
                _reservedChair.UnOccupyChair();
                _reservedChair.Release();
                _reservedChair = null;
            }

            gameObject.SetActive(true);
            RequestTransition(CustomerStateType.Leaving);
        }

        public void Despawn()
        {
            OnDespawned?.Invoke(this);
            _spawner.NotifyDespawn(this);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            _destroyed = true;
            ScheduleManager.OnShowEnded -= HandleShowEndedWhileSitting;
        }

        // True while the customer is visually holding their popcorn. Drives the
        // Animator's "isHolding" Bool parameter (UpperBody layer) and gates
        // HoldPopcorn so it's safe to call repeatedly. The prop itself is a
        // pre-placed child GameObject toggled via SetActive, not something we
        // instantiate at runtime.
        private bool isHolding;

        public void HoldPopcorn()
        {
            if (isHolding) return;

            isHolding = true;

            // The prop is already a child of this customer (designer-placed
            // and disabled in the prefab), so toggling SetActive is enough —
            // no Instantiate / Destroy, no allocation churn, no placement to
            // recompute at runtime.
            if (popcornProp != null) popcornProp.SetActive(true);

            if (_animator != null) _animator.SetBool(HoldHash, true);
        }

        public void ReleasePopcorn()
        {
            if (!isHolding) return;

            isHolding = false;

            if (popcornProp != null) popcornProp.SetActive(false);

            if (_animator != null) _animator.SetBool(HoldHash, false);
        }
    }
}
