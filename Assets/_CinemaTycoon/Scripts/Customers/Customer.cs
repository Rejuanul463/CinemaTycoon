using System;
using UnityEngine;
using UnityEngine.AI;
using CinemaTycoon.Core;
using CinemaTycoon.Economy;
using CinemaTycoon.Schedule;
using CinemaTycoon.Staff;

namespace CinemaTycoon.Customers
{
    public enum CustomerStateType
    { Entering, Queuing, Purchasing, Popcorn, Watching, Leaving, Unsatisfied,
      GoingToBathroom, UsingBathroom, ReturningFromBathroom,
      GoingToArcade, UsingArcade }

    /// <summary>Customer gender. Drives which bathroom waypoint they may use.</summary>
    public enum Gender { Male, Female }

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
            {
                // Pre-queue arcade: a small chance to play a random arcade
                // game before joining the queue, mimicking the "kill time
                // before the movie" behavior. NeedsArcade() is a fresh roll
                // per customer; TryGoToArcade() is a no-op when no machines
                // are assigned, so the call is safe even with an empty list.
                if (Customer.NeedsArcade() && Customer.TryGoToArcade())
                    return; // TryGoToArcade already transitioned the state

                GoTo(CustomerStateType.Queuing);
            }
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
            // Presence of a Guard on duty reduces queue frustration by 25%.
            float patience = Customer.QueuePatiencePerSecond;
            if (gm.Staff != null && gm.Staff.HasRoleOnDuty(StaffRole.Guard))
                patience *= 0.75f;
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

            // Popcorn detour before the bathroom: the natural order is
            // ticket → optional popcorn → optional bathroom → seat.
            if (Customer.WantsPopcorn() && CinemaWaypoints.Instance.PopcornStand != null)
            {
                GoTo(CustomerStateType.Popcorn);
                return;
            }

            // No popcorn — pre-show bathroom chance is rolled here for the
            // "ticket only" path. The "ticket + popcorn" path is handled in
            // PopcornState.Tick so the same chance applies to both.
            if (Customer.NeedsBathroom() && Customer.TryGoToBathroom(fromSeat: false))
                return; // TryGoToBathroom already transitioned the state

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

            // Pre-show bathroom detour for the "ticket + popcorn" path.
            // NeedsBathroom() is a fresh roll — same chance as the ticket-only
            // path, so a customer buying popcorn is no more or less likely to
            // need the bathroom than one who didn't.
            if (Customer.NeedsBathroom() && Customer.TryGoToBathroom(fromSeat: false))
                return; // TryGoToBathroom already transitioned the state

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
            // Idempotent — releases a held bathroom slot if the customer bailed
            // out mid-trip (show ended while walking to / using the bathroom).
            // No-op for customers who never entered a bathroom.
            Customer.ReleaseBathroom();
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
            // Idempotent — same reasoning as LeavingState.Enter.
            Customer.ReleaseBathroom();
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

    // ---------- Bathroom trip ----------

    /// <summary>
    /// Walks the customer to their reserved bathroom waypoint. The reservation
    /// was already taken in <see cref="Customer.TryGoToBathroom"/> before
    /// entering this state, so Enter just looks up the matching bathroom
    /// transform and dispatches the agent. Release happens at the end of the
    /// trip (see ReturningFromBathroomState.Exit) — not here — so the slot
    /// stays held for the entire walk + use + return.
    /// </summary>
    public sealed class GoingToBathroomState : CustomerState
    {
        public override CustomerStateType Type => CustomerStateType.GoingToBathroom;
        public GoingToBathroomState(Customer c) : base(c) { }

        public override void Enter()
        {
            var bm = BathroomManager.Instance;
            if (bm == null) { GoTo(CustomerStateType.Watching); return; }

            // Reservation was made before transition; look it up via the
            // customer's gender. If the slot was lost (e.g. another script
            // released it) fall back to the matching waypoint so the customer
            // still walks somewhere sensible rather than standing still.
            var wp = CinemaWaypoints.Instance;
            Transform target = null;
            if (Customer.Gender == Gender.Female)
            {
                target = wp != null ? wp.FemaleBathroom : null;
            }
            else
            {
                target = wp != null ? wp.MaleBathroom : null;
            }
            if (target == null)
            {
                // No bathroom waypoint assigned for this gender — give up the
                // slot (if we still have it) and head to the chair.
                Customer.ReleaseBathroom();
                GoTo(CustomerStateType.Watching);
                return;
            }

            Customer.Agent.isStopped = false;
            Customer.Agent.SetDestination(target.position);
        }

        public override void Tick()
        {
            if (Customer.Agent.pathPending) return;
            if (Customer.Agent.remainingDistance < 1.0f)
            {
                // Arrived at the bathroom. UsingBathroomState will tick down
                // the in-bathroom timer before the customer walks back.
                GoTo(CustomerStateType.UsingBathroom);
            }
        }
    }

    /// <summary>
    /// Customer stands at the bathroom waypoint for a fixed duration. The
    /// NavMeshAgent stays active but the agent has no new destination, so
    /// it idles at the bathroom. The slot is still held by the customer
    /// for the entire duration; the next customer of the same gender
    /// cannot enter until the slot is released by ReturningFromBathroom.
    /// </summary>
    public sealed class UsingBathroomState : CustomerState
    {
        public override CustomerStateType Type => CustomerStateType.UsingBathroom;
        private float _timer;

        public UsingBathroomState(Customer c) : base(c) { }

        public override void Enter()
        {
            _timer = Customer.BathroomUseDuration;
        }

        public override void Tick()
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f) GoTo(CustomerStateType.ReturningFromBathroom);
        }
    }

    /// <summary>
    /// Walks the customer back to the chair they were sitting in (if any).
    /// The bathroom slot is released in this state's Exit so the moment the
    /// customer physically leaves the bathroom, the next same-gender
    /// customer can enter. If the show has ended or there is no reserved
    /// chair, transitions to Watching (which will sit them in a fresh
    /// chair) or Leaving as appropriate.
    /// </summary>
    public sealed class ReturningFromBathroomState : CustomerState
    {
        public override CustomerStateType Type => CustomerStateType.ReturningFromBathroom;
        public ReturningFromBathroomState(Customer c) : base(c) { }

        public override void Enter()
        {
            var chair = Customer.ReservedChair;
            var gm = GameManager.Instance;

            // Pre-show customers don't have a reserved chair yet — they
            // just head to the hall like any first-time customer. If the
            // movie has already started by the time they return they can
            // still find a free chair via WatchingState.
            if (chair == null)
            {
                GoTo(CustomerStateType.Watching);
                return;
            }

            // Show ended while we were in the bathroom. Bail out and leave;
            // we won't find a free chair anyway.
            if (gm?.Schedule != null
                && !gm.Schedule.IsMoviePlaying
                && !gm.Schedule.IsMoviePlayingOrImminent)
            {
                GoTo(CustomerStateType.Leaving);
                return;
            }

            Customer.Agent.isStopped = false;
            Customer.Agent.SetDestination(chair.GetApproachPosition());
        }

        public override void Tick()
        {
            if (Customer.Agent.pathPending) return;

            // Re-sit when we reach the chair's approach point. The customer
            // re-Occupies the same chair (its reservation is still held) and
            // re-subscribes to OnShowEnded so the show-end wake-up still
            // fires. SitOnChair also re-disables the GameObject, so the
            // ReturningFromBathroomState stops ticking until the next event.
            if (Customer.Agent.remainingDistance < Customer.ChairArrivalDistance
                && Customer.Agent.hasPath)
            {
                Customer.ResumeWatchingAfterBathroom();
            }
        }

        public override void Exit() => Customer.ReleaseBathroom();
    }

    // ---------- Arcade trip ----------

    /// <summary>
    /// Walks the customer to a randomly-picked arcade machine. The pick
    /// happens in Enter (via <see cref="CinemaWaypoints.PickRandomArcade"/>)
    /// so the same customer doesn't get re-rolled if the state is somehow
    /// re-entered. Multiple customers can target the same machine — the
    /// arcade is NOT single-occupancy, unlike the bathroom. If the list is
    /// empty (e.g. designer removed all entries) the state falls through
    /// to Queuing so the customer isn't stranded.
    /// </summary>
    public sealed class GoingToArcadeState : CustomerState
    {
        public override CustomerStateType Type => CustomerStateType.GoingToArcade;
        public GoingToArcadeState(Customer c) : base(c) { }

        public override void Enter()
        {
            var wp = CinemaWaypoints.Instance;
            var arcade = wp != null ? wp.PickRandomArcade() : null;
            if (arcade == null)
            {
                // No arcades available — skip the detour and join the queue.
                GoTo(CustomerStateType.Queuing);
                return;
            }

            Customer.Agent.isStopped = false;
            Customer.Agent.SetDestination(arcade.position);
        }

        public override void Tick()
        {
            if (Customer.Agent.pathPending) return;
            if (Customer.Agent.remainingDistance < 1.0f)
                GoTo(CustomerStateType.UsingArcade);
        }
    }

    /// <summary>
    /// Customer stands at the arcade machine for a fixed duration
    /// (mimicking play — no actual interaction or animation). When the
    /// timer expires, the customer goes to QueuingState to resume the
    /// normal flow (ticket → popcorn → bathroom → seat).
    /// </summary>
    public sealed class UsingArcadeState : CustomerState
    {
        public override CustomerStateType Type => CustomerStateType.UsingArcade;
        private float _timer;

        public UsingArcadeState(Customer c) : base(c) { }

        public override void Enter()
        {
            _timer = Customer.ArcadeUseDuration;
        }

        public override void Tick()
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f) GoTo(CustomerStateType.Queuing);
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

        [Header("Bathroom")]
        [Tooltip("Seconds a customer spends inside the bathroom before heading back.")]
        [SerializeField] private float bathroomUseDuration = 8f;
        [Tooltip("Chance (0..1) that a customer will detour to the bathroom after " +
                 "buying a ticket (and optional popcorn) but before sitting. " +
                 "Rolled once per purchase — if the matching bathroom is full the " +
                 "detour is skipped for this visit.")]
        [SerializeField, Range(0f, 1f)] private float preShowBathroomChance = 0.15f;

        [Header("Arcade")]
        [Tooltip("Seconds a customer spends standing at an arcade machine " +
                 "('playing' — no actual interaction). Stand-in for the visual " +
                 "duration of a quick arcade game.")]
        [SerializeField] private float arcadeUseDuration = 10f;
        [Tooltip("Chance (0..1) that a customer will detour to a random arcade " +
                 "machine on entering the cinema, BEFORE joining the queue. " +
                 "Mimics the 'kill time before the movie' behavior. Rolls once " +
                 "per entry; if no arcades are assigned the detour is skipped.")]
        [SerializeField, Range(0f, 1f)] private float preShowArcadeChance = 0.2f;

        // Read access for state classes
        public float QueuePatiencePerSecond => queuePatiencePerSecond;
        public float WatchSatisfactionPerSecond => watchSatisfactionPerSecond;
        public float UnsatisfiedThreshold => unsatisfiedThreshold;
        public float ChairArrivalDistance => chairArrivalDistance;
        public float BathroomUseDuration => bathroomUseDuration;
        public float ArcadeUseDuration => arcadeUseDuration;
        public NavMeshAgent Agent { get; private set; }
        public float Satisfaction { get; private set; }
        public bool IsVIP { get; private set; }
        /// <summary>True if customer has been flagged as a Rowdy Customer event target.</summary>
        public bool IsRowdy { get; private set; }
        public int QueueIndex { get; private set; } = -1;
        public bool IsAtFrontOfQueue { get; private set; }
        public bool CashierReady { get; private set; }
        /// <summary>True if this customer has purchased popcorn at the stand.</summary>
        public bool HasPopcorn { get; private set; }
        /// <summary>Customer gender. Drives which bathroom waypoint they may use.</summary>
        public Gender Gender { get; private set; }
        /// <summary>True between <see cref="SitOnChair"/> and either
        /// <see cref="WakeFromChair"/> or <see cref="LeaveChairForBathroom"/>.
        /// While true, the GameObject is disabled and BathroomManager uses this
        /// flag to decide who to roll the "need to go" check on.</summary>
        public bool IsSeated { get; private set; }
        /// <summary>True while the customer holds a reserved bathroom slot in
        /// BathroomManager. Released by the ReturningFromBathroomState exit
        /// (or earlier if the customer bails out of the bathroom trip).</summary>
        public bool HasBathroomReserved { get; private set; }
        /// <summary>The currently reserved chair (only non-null while seated or
        /// during the bathroom return trip). Exposed so ReturningFromBathroom
        /// can path back to the same chair.</summary>
        public OccupiedChairLogic ReservedChair => _reservedChair;

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
        // Total seconds spent inside the bathroom during the current sit.
        // Subtracted from the watching-elapsed time when calculating
        // satisfaction on wake so the player isn't credited for time spent
        // away from the screen. Reset on each new SitOnChair.
        private float _bathroomTime;
        // Time.time at the moment the customer left the chair for the
        // bathroom. Used to compute the delta added to _bathroomTime when
        // they return. Only meaningful between LeaveChairForBathroom and
        // ResumeWatchingAfterBathroom.
        private float _bathroomStartTime;
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
            // Prefabs with the "Female" tag on their root use the female
            // bathroom. Anything else is treated as male. This keeps the
            // gender decision entirely on the asset — CustomerSpawnManager
            // does not need a per-prefab gender list, and the same prefab
            // set can contain both.
            Gender = CompareTag("Female") ? Gender.Female : Gender.Male;
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
                CustomerStateType.GoingToBathroom       => new GoingToBathroomState(this),
                CustomerStateType.UsingBathroom         => new UsingBathroomState(this),
                CustomerStateType.ReturningFromBathroom => new ReturningFromBathroomState(this),
                CustomerStateType.GoingToArcade         => new GoingToArcadeState(this),
                CustomerStateType.UsingArcade           => new UsingArcadeState(this),
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

        /// <summary>Used by EventManager to flag a customer as rowdy.</summary>
        public void MarkAsRowdy() => IsRowdy = true;

        /// <summary>Called when a Guard intercept task completes on a rowdy customer.</summary>
        public void EscortOutByGuard()
        {
            IsRowdy = false;
            ReleaseBathroom();
            ReleaseReservedChair();
            RequestTransition(CustomerStateType.Leaving);
        }

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
            if (IsSeated) return; // idempotent — guards against double-subscribe on re-sit
            _sitStartTime = Time.time;
            _bathroomTime = 0f;
            _reservedChair.OccupyChair();
            ScheduleManager.OnShowEnded += HandleShowEndedWhileSitting;
            IsSeated = true;
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
            IsSeated = false;

            // Subtract any time the customer spent in the bathroom — they
            // weren't watching the screen during those seconds, so the
            // satisfaction award should only reflect actual screen time.
            float elapsed = Time.time - _sitStartTime - _bathroomTime;
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
            // Safety net: if the customer is destroyed mid-bathroom-trip
            // (scene reload, GameManager teardown) we still want to free
            // the slot. Release is idempotent.
            ReleaseBathroom();
        }

        // ---------- Bathroom trip helpers ----------

        /// <summary>
        /// Roll whether this customer will need the bathroom before sitting
        /// down. Called once per purchase (either the ticket-only path in
        /// PurchasingState or the ticket+popcorn path in PopcornState). If
        /// the matching bathroom is full the FSM still transitions to
        /// Watching — the bathroom detour is silently skipped.
        /// </summary>
        public bool NeedsBathroom()
        {
            return UnityEngine.Random.value < preShowBathroomChance;
        }

        /// <summary>
        /// Roll whether this customer will detour to an arcade machine on
        /// entering the cinema, before joining the queue. Independent of
        /// the bathroom chance — a customer can roll true for both
        /// (one will fire first), false for both, or either combination.
        /// </summary>
        public bool NeedsArcade()
        {
            var gm = GameManager.Instance;
            if (gm?.Schedule != null && gm.Schedule.IsMoviePlayingOrImminent)
                return false; // Skip arcade detour when a show is imminent so auditorium fills fast
            return UnityEngine.Random.value < preShowArcadeChance;
        }

        /// <summary>
        /// Try to start an arcade trip. If at least one arcade machine is
        /// assigned, transitions to GoingToArcadeState which picks a
        /// random machine and walks there. Returns false (no transition)
        /// when no machines are available so the caller can proceed with
        /// its normal flow.
        /// </summary>
        public bool TryGoToArcade()
        {
            var wp = CinemaWaypoints.Instance;
            if (wp == null || wp.ArcadeMachines == null || wp.ArcadeMachines.Length == 0)
                return false;
            // GoingToArcadeState.Enter picks a random non-null machine via
            // CinemaWaypoints.PickRandomArcade, so we don't need to roll
            // here — an empty / all-null list is the only failure mode.
            ChangeState(new GoingToArcadeState(this));
            return true;
        }

        /// <summary>
        /// Try to start a bathroom trip. From-seat trips wake the customer
        /// from the chair (un-reserving the show-end subscription, hiding
        /// the sitting model). Pre-show trips are no-ops for the chair. If
        /// the matching bathroom is full or no waypoint is assigned, the
        /// call is a no-op and returns false so the caller can proceed
        /// with its normal flow (Watching / seat assignment).
        /// </summary>
        public bool TryGoToBathroom(bool fromSeat)
        {
            if (fromSeat && !LeaveChairForBathroom()) return false;

            var bm = BathroomManager.Instance;
            if (bm == null || !bm.TryReserve(this, out _))
            {
                // Slot taken or no waypoint — undo any chair-wake and bail.
                if (fromSeat) SitOnChair();
                return false;
            }

            HasBathroomReserved = true;
            ChangeState(new GoingToBathroomState(this));
            return true;
        }

        /// <summary>
        /// Release the held bathroom slot. Idempotent — called from many
        /// places (ReturningFromBathroomState.Exit, LeavingState.Enter,
        /// UnsatisfiedState.Enter, OnDestroy) and is safe to call when
        /// no slot is held.
        /// </summary>
        public void ReleaseBathroom()
        {
            if (!HasBathroomReserved) return;
            HasBathroomReserved = false;
            BathroomManager.Instance?.Release(this);
        }

        /// <summary>
        /// Mid-show helper: wake the customer from the chair so they can
        /// walk to the bathroom. Hides the sitting model, unsubscribes
        /// from the show-end event (so they don't get a double-wake), and
        /// records the time the trip started so satisfaction on the final
        /// wake can subtract the bathroom duration. The chair reservation
        /// is preserved — the same chair will be reclaimed on return.
        /// </summary>
        public bool LeaveChairForBathroom()
        {
            if (!IsSeated) return false;
            if (_reservedChair == null) return false;

            ScheduleManager.OnShowEnded -= HandleShowEndedWhileSitting;
            _reservedChair.UnOccupyChair(); // hide the sitting child
            IsSeated = false;
            _bathroomStartTime = Time.time;
            gameObject.SetActive(true);
            return true;
        }

        /// <summary>
        /// Re-sit on the same chair after a bathroom trip. Re-occupies
        /// the model, re-subscribes to OnShowEnded, accumulates the
        /// bathroom duration into <see cref="_bathroomTime"/> so the
        /// final satisfaction award excludes it, and re-disables the
        /// GameObject so the FSM pauses until the next event.
        /// </summary>
        public void ResumeWatchingAfterBathroom()
        {
            if (_reservedChair == null) return;
            if (IsSeated) return; // idempotent

            _bathroomTime += Time.time - _bathroomStartTime;
            _reservedChair.OccupyChair();
            ScheduleManager.OnShowEnded += HandleShowEndedWhileSitting;
            IsSeated = true;
            gameObject.SetActive(false);
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
