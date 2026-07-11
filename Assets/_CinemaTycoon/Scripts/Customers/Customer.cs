using System;
using UnityEngine;
using UnityEngine.AI;
using CinemaTycoon.Core;
using CinemaTycoon.Economy;

namespace CinemaTycoon.Customers
{
    public enum CustomerStateType
    { Entering, Queuing, Purchasing, Watching, Leaving, Unsatisfied }

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
            // Satisfaction drains while waiting. FasterCashier upgrade softens this.
            float patience = Customer.QueuePatiencePerSecond;
            float cashierMult = GameManager.Instance.Economy.CashierSpeedMultiplier;
            Customer.ReduceSatisfaction(patience * Time.deltaTime / cashierMult);

            if (Customer.Satisfaction <= Customer.UnsatisfiedThreshold)
            {
                GoTo(CustomerStateType.Unsatisfied);
                return;
            }

            // Re-path if our queue index changed (someone ahead left).
            Customer.Agent.SetDestination(
                CinemaWaypoints.Instance.GetQueuePoint(Customer.QueueIndex).position);

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
            GoTo(GameManager.Instance.Schedule.IsMoviePlayingOrImminent
                ? CustomerStateType.Watching
                : CustomerStateType.Unsatisfied);
        }
    }

    public sealed class WatchingState : CustomerState
    {
        public override CustomerStateType Type => CustomerStateType.Watching;
        public WatchingState(Customer c) : base(c) { }

        public override void Enter()
        {
            Customer.Agent.isStopped = false;
            Customer.Agent.SetDestination(
                CinemaWaypoints.Instance.GetHallSeat(Customer.SeatIndex).position);
        }

        public override void Tick()
        {
            // Gain satisfaction over the show; ComfySeats upgrade amplifies this.
            float gain = Customer.WatchSatisfactionPerSecond
                         * GameManager.Instance.Economy.SeatComfortMultiplier;
            Customer.AddSatisfaction(gain * Time.deltaTime);

            if (!GameManager.Instance.Schedule.IsMoviePlaying)
                GoTo(CustomerStateType.Leaving);
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

        // Read access for state classes
        public float QueuePatiencePerSecond => queuePatiencePerSecond;
        public float WatchSatisfactionPerSecond => watchSatisfactionPerSecond;
        public float UnsatisfiedThreshold => unsatisfiedThreshold;
        public NavMeshAgent Agent { get; private set; }
        public float Satisfaction { get; private set; }
        public bool IsVIP { get; private set; }
        public int QueueIndex { get; private set; } = -1;
        public int SeatIndex { get; private set; } = -1;
        public bool IsAtFrontOfQueue { get; private set; }
        public bool CashierReady { get; private set; }

        // Static events — EconomyManager and HUD both listen to OnTicketPurchased.
        public static event Action<Customer, float> OnTicketPurchased;
        public static event Action<Customer, float> OnSatisfactionFinalized;
        public static event Action<Customer> OnDespawned;

        private CustomerState _currentState;
        private CustomerSpawnManager _spawner;
        private bool _finalized;

        private void Awake() => Agent = GetComponent<NavMeshAgent>();

        public void Initialize(CustomerSpawnManager spawner, bool isVIP, int seatIndex)
        {
            _spawner = spawner;
            IsVIP = isVIP;
            SeatIndex = seatIndex;
            Satisfaction = initialSatisfaction;
            ChangeState(new EnteringState(this));
        }

        private void Update() => _currentState?.Tick();

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
            var schedule = GameManager.Instance.Schedule;
            if (!schedule.IsMoviePlayingOrImminent) return; // no movie, no sale

            float price = schedule.CurrentTicketPrice
                          * GameManager.Instance.Economy.TicketRevenueMultiplier;
            GameManager.Instance.Economy.AddIncome(price, "Ticket sale");
            OnTicketPurchased?.Invoke(this, price);
        }

        public void FinalizeSatisfaction(bool penalty)
        {
            if (_finalized) return;
            _finalized = true;

            float weight = IsVIP ? 2f : 1f;
            float delta = penalty
                ? -5f * weight
                : ((Satisfaction - 50f) / 50f) * 5f * weight; // +ve if satisfied, -ve if not

            GameManager.Instance.AdjustCinemaRating(delta, "Customer feedback");
            OnSatisfactionFinalized?.Invoke(this, Satisfaction);
        }

        /// <summary>Used by EventManager to elevate a normal customer to VIP mid-visit.</summary>
        public void MarkAsVIP() => IsVIP = true;

        public void Despawn()
        {
            OnDespawned?.Invoke(this);
            _spawner.NotifyDespawn(this);
            Destroy(gameObject);
        }
    }
}
