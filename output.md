# Cinema Tycoon — Full Bootstrap

Below is the complete, compilable Unity 6 (URP) C# bootstrap for **Cinema Tycoon**. I've made a few reasonable assumptions (stated inline): a single shared `CinemaWaypoints` MonoBehaviour acts as the physical location authority, customers are spawned from a prefab and destroyed on despawn (no pooling — keeps scope honest), and HUD subscriptions are wired through static C# events to keep systems decoupled from UI.

Recommended folder/namespace layout:

```
Assets/_CinemaTycoon/
├── Scripts/
│   ├── Core/        (CinemaTycoon.Core)
│   ├── Economy/     (CinemaTycoon.Economy)
│   ├── Customers/   (CinemaTycoon.Customers)
│   ├── Schedule/    (CinemaTycoon.Schedule)
│   ├── Staff/       (CinemaTycoon.Staff)
│   ├── Events/      (CinemaTycoon.Events)
│   ├── UI/          (CinemaTycoon.UI)
│   └── DevTools/    (CinemaTycoon.DevTools)
└── Data/            (ScriptableObject instances)
```

---

## 1. Core — GameManager + CinemaWaypoints

**Design.** A single `[DefaultExecutionOrder(-100)]` `GameManager` singleton coordinates the subsystem managers. It uses a "safe singleton" pattern (destroy duplicates in `Awake`) plus `DontDestroyOnLoad` so scene reloads (e.g. restart) get a fresh instance. Cross-system communication uses static C# events on each manager so unrelated systems never hold direct references — for example, a ticket sale raises `Customer.OnTicketPurchased`, which `EconomyManager` doesn't even need to listen to (the customer itself calls `EconomyManager.AddIncome` directly, but the HUD listens to the same event to update its "last ticket" label). `CinemaWaypoints` is a tiny separate singleton that owns every physical transform customers/staff navigate to — this prevents the spaghetti of every script holding its own `Transform` references.

### `Core/GameManager.cs`

```csharp
using System;
using UnityEngine;
using CinemaTycoon.Economy;
using CinemaTycoon.Customers;
using CinemaTycoon.Staff;
using CinemaTycoon.Schedule;
using CinemaTycoon.Events;

namespace CinemaTycoon.Core
{
    /// <summary>
    /// Central coordinator singleton. Persists across scene reloads and
    /// exposes the subsystem managers so cross-system callers can reach
    /// them through a single, well-known entry point.
    /// </summary>
    [DefaultExecutionOrder(-100)] // Run before dependent managers
    public class GameManager : MonoBehaviour
    {
        #region Safe Singleton
        private static GameManager _instance;
        public static GameManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindFirstObjectByType<GameManager>();
                return _instance;
            }
        }
        #endregion

        [Header("Manager References (assign in Inspector)")]
        [SerializeField] private EconomyManager economyManager;
        [SerializeField] private CustomerSpawnManager customerSpawnManager;
        [SerializeField] private StaffManager staffManager;
        [SerializeField] private ScheduleManager scheduleManager;
        [SerializeField] private EventManager eventManager;

        [Header("Cinema Rating")]
        [SerializeField] private float startingCinemaRating = 75f;

        // Public accessors — read-only from outside.
        public EconomyManager Economy => economyManager;
        public CustomerSpawnManager Spawner => customerSpawnManager;
        public StaffManager Staff => staffManager;
        public ScheduleManager Schedule => scheduleManager;
        public EventManager Events => eventManager;
        public float CinemaRating => _cinemaRating;

        // Static events so UI/scene-flow can subscribe without a reference.
        public static event Action<float> OnCinemaRatingChanged;
        public static event Action<string> OnGameOver;

        private float _cinemaRating;
        private bool _gameOver;

        private void Awake()
        {
            // Safe singleton: destroy any duplicate spawned by a scene reload.
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            _cinemaRating = startingCinemaRating;
            OnCinemaRatingChanged?.Invoke(_cinemaRating);

            // Init order matters: economy first (others depend on it),
            // then services that spend money or read economy state.
            economyManager.Initialize();
            staffManager.Initialize();
            scheduleManager.Initialize();
            customerSpawnManager.Initialize();
            eventManager.Initialize();
        }

        /// <summary>
        /// Adjust the cinema-wide satisfaction rating. Customers, events,
        /// and schedule penalties all funnel through here.
        /// </summary>
        public void AdjustCinemaRating(float delta, string reason = "")
        {
            if (_gameOver) return;
            _cinemaRating = Mathf.Clamp(_cinemaRating + delta, 0f, 100f);
            OnCinemaRatingChanged?.Invoke(_cinemaRating);

            if (_cinemaRating <= 0f)
                TriggerGameOver("Audience satisfaction hit zero. The cinema closed.");
        }

        public void TriggerGameOver(string reason)
        {
            if (_gameOver) return;
            _gameOver = true;
            Time.timeScale = 0f;
            OnGameOver?.Invoke(reason);
        }
    }
}
```

### `Core/CinemaWaypoints.cs`

```csharp
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
        [SerializeField] private Transform[] hallSeats;
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
        public int SeatCount => hallSeats != null ? hallSeats.Length : 0;

        public Transform GetQueuePoint(int index)
        {
            if (queuePoints == null || queuePoints.Length == 0) return ticketBooth;
            return queuePoints[Mathf.Clamp(index, 0, queuePoints.Length - 1)];
        }

        public Transform GetHallSeat(int index)
        {
            if (hallSeats == null || hallSeats.Length == 0) return exitPoint;
            return hallSeats[index % hallSeats.Length];
        }
    }
}
```

**Wiring in Editor.** Create an empty GameObject named `GameManager` and attach `GameManager`. Drag the five manager prefabs/components into the slots. Create another empty GameObject `CinemaWaypoints` and assign child empty GameObjects as `SpawnPoint`, `QueuePoints` (a row of 4–6 transforms in front of the booth), `TicketBooth`, `HallSeats` (a grid of 8–12 seat transforms), `ExitPoint`, and three staff stations. **Bake a NavMesh** covering the floor of the lobby and hall so `NavMeshAgent` can path between these points.

---

## 2. Economy & Upgrades — EconomyManager

**Design.** `EconomyManager` is the single owner of the balance float; every money-touching system calls `AddIncome`/`Spend`. Each call logs a `Transaction` and raises `OnBalanceChanged`, so the HUD can refresh without polling. Wages are paid on a tick interval rather than per-frame to keep numbers legible. Upgrades are ScriptableObject-defined and stored as a `HashSet<UpgradeType>` flag set; relevant systems query `HasUpgrade`/`GetMultiplier` so the upgrade never needs to know which system it affects. Lose condition: `Spend` checks for `balance <= 0` and raises `OnGameOver`.

### `Economy/EconomyManager.cs`

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using CinemaTycoon.Core;
using CinemaTycoon.Staff;

namespace CinemaTycoon.Economy
{
    public enum TransactionType { Income, Expense }

    public enum UpgradeType
    {
        PremiumPopcorn, // +ticket revenue
        FasterCashier,  // reduces queue-frustration
        ComfySeats,     // boosts watch-time satisfaction
        Marketing       // faster customer spawn rate
    }

    [Serializable]
    public struct Transaction
    {
        public TransactionType type;
        public float amount;
        public string description;
        public float gameTime;
    }

    [CreateAssetMenu(fileName = "UpgradeData", menuName = "CinemaTycoon/Upgrade")]
    public class UpgradeData : ScriptableObject
    {
        public string upgradeName;
        [TextArea] public string description;
        public float cost = 250f;
        public UpgradeType type;
        [Tooltip("Multiplier applied to the affected stat. >1 is always 'better'.")]
        public float multiplier = 1.5f;
    }

    public class EconomyManager : MonoBehaviour
    {
        [Header("Starting State")]
        [SerializeField] private float startingBalance = 1000f;

        [Header("Wages")]
        [SerializeField] private float staffWagePerTick = 5f;
        [SerializeField] private float wageTickInterval = 15f;

        [Header("Upgrades")]
        [SerializeField] private List<UpgradeData> availableUpgrades = new();

        private float _balance;
        private readonly List<Transaction> _transactions = new();
        private readonly HashSet<UpgradeType> _purchased = new();
        private float _wageTimer;

        public float Balance => _balance;
        public IReadOnlyList<Transaction> Transactions => _transactions;
        public IReadOnlyList<UpgradeData> AvailableUpgrades => availableUpgrades;

        // Static events — HUD/SceneFlow subscribe without holding a reference.
        public static event Action<float> OnBalanceChanged;
        public static event Action<Transaction> OnTransactionLogged;
        public static event Action<UpgradeType> OnUpgradePurchased;

        public void Initialize()
        {
            _balance = startingBalance;
            OnBalanceChanged?.Invoke(_balance);
        }

        private void Update()
        {
            _wageTimer += Time.deltaTime;
            if (_wageTimer >= wageTickInterval)
            {
                _wageTimer = 0f;
                PayWages();
            }
        }

        public bool CanAfford(float amount) => _balance >= amount;

        public void AddIncome(float amount, string source)
        {
            if (amount <= 0f) return;
            _balance += amount;
            Log(TransactionType.Income, amount, source);
            OnBalanceChanged?.Invoke(_balance);
        }

        public void Spend(float amount, string reason)
        {
            if (amount <= 0f) return;
            _balance -= amount;
            Log(TransactionType.Expense, amount, reason);
            OnBalanceChanged?.Invoke(_balance);

            if (_balance <= 0f)
                GameManager.Instance.TriggerGameOver("You went bankrupt!");
        }

        public bool TryPurchaseUpgrade(UpgradeType type)
        {
            if (HasUpgrade(type)) return false;
            foreach (var up in availableUpgrades)
            {
                if (up.type != type) continue;
                if (!CanAfford(up.cost)) return false;
                Spend(up.cost, $"Upgrade: {up.upgradeName}");
                _purchased.Add(type);
                OnUpgradePurchased?.Invoke(type);
                return true;
            }
            return false;
        }

        public bool HasUpgrade(UpgradeType type) => _purchased.Contains(type);

        /// <summary>
        /// Returns the configured multiplier for an upgrade (1f if not purchased
        /// or not defined). Convention: multiplier > 1 is always "better".
        /// </summary>
        public float GetMultiplier(UpgradeType type)
        {
            if (!_purchased.Contains(type)) return 1f;
            foreach (var up in availableUpgrades)
                if (up.type == type) return up.multiplier;
            return 1f;
        }

        // Convenience accessors so callers don't need to know the convention.
        public float TicketRevenueMultiplier => GetMultiplier(UpgradeType.PremiumPopcorn);
        public float CashierSpeedMultiplier   => GetMultiplier(UpgradeType.FasterCashier);
        public float SeatComfortMultiplier    => GetMultiplier(UpgradeType.ComfySeats);
        public float MarketingMultiplier      => GetMultiplier(UpgradeType.Marketing);

        private void PayWages()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Staff == null) return;
            int count = gm.Staff.ActiveStaff.Count;
            if (count <= 0) return;
            Spend(count * staffWagePerTick, "Staff wages");
        }

        private void Log(TransactionType type, float amount, string desc)
        {
            var t = new Transaction
            {
                type = type,
                amount = amount,
                description = desc,
                gameTime = Time.time
            };
            _transactions.Add(t);
            OnTransactionLogged?.Invoke(t);
        }

        // Dev-only hook used by CheatManager. Never called from gameplay code.
        public void DevInjectFunds(float amount) => AddIncome(amount, "Cheat: money injection");
    }
}
```

**Wiring in Editor.** Attach `EconomyManager` to a child of `GameManager` (or the same object). Create 4 `UpgradeData` assets (`PremiumPopcorn`, `FasterCashier`, `ComfySeats`, `Marketing`) with cost 200–500 and multipliers 1.25–2.0. Drop them into `Available Upgrades`. Ensure `GameManager.economyManager` points here.

---

## 3. Customer AI & State Machine — Customer + CustomerSpawnManager

**Design.** Each customer is a `NavMeshAgent`-driven GameObject that delegates its per-frame logic to a `CustomerState` subclass. States are plain C# classes (not MonoBehaviours) holding a back-reference to the customer; they expose `Enter`/`Tick`/`Exit` and request transitions through `Customer.RequestTransition`. This keeps the state machine readable and extensible without bloating `Customer.Update` into a giant switch. Individual `Satisfaction` drains while waiting in queue and gains while watching; on despawn it feeds back into the cinema-wide rating via `GameManager.AdjustCinemaRating`. VIPs are simply customers with `IsVIP = true` and contribute larger swings.

### `Customers/Customer.cs`

```csharp
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

        public void Despawn()
        {
            OnDespawned?.Invoke(this);
            _spawner.NotifyDespawn(this);
            Destroy(gameObject);
        }
    }
}
```

### `Customers/CustomerSpawnManager.cs`

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using CinemaTycoon.Core;

namespace CinemaTycoon.Customers
{
    public class CustomerSpawnManager : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [SerializeField] private GameObject customerPrefab;
        [SerializeField] private float spawnInterval = 4f;
        [SerializeField] private int maxConcurrentCustomers = 12;
        [SerializeField] [Range(0f, 1f)] private float vipChance = 0.08f;

        private readonly List<Customer> _queue = new();
        private readonly HashSet<Customer> _active = new();
        private int _seatCursor;
        private float _spawnTimer;

        public IReadOnlyCollection<Customer> ActiveCustomers => _active;

        public static event Action<Customer> OnCustomerSpawned;

        public void Initialize() => _spawnTimer = 0f;

        private void Update()
        {
            _spawnTimer += Time.deltaTime;

            // Marketing upgrade reduces interval (i.e., more spawns per minute).
            float interval = spawnInterval / GameManager.Instance.Economy.MarketingMultiplier;

            if (_spawnTimer >= interval && _active.Count < maxConcurrentCustomers)
            {
                _spawnTimer = 0f;
                SpawnCustomer();
            }
        }

        private void SpawnCustomer()
        {
            if (customerPrefab == null || CinemaWaypoints.Instance == null) return;

            Vector3 pos = CinemaWaypoints.Instance.SpawnPoint.position;
            GameObject go = Instantiate(customerPrefab, pos, Quaternion.identity);
            var cust = go.GetComponent<Customer>();
            if (cust == null)
            {
                Debug.LogError("[CustomerSpawnManager] Prefab missing Customer component.");
                Destroy(go);
                return;
            }

            bool isVIP = UnityEngine.Random.value < vipChance;
            cust.Initialize(this, isVIP, _seatCursor++);
            _active.Add(cust);
            OnCustomerSpawned?.Invoke(cust);
        }

        public void NotifyDespawn(Customer c) => _active.Remove(c);

        public void AssignQueueIndex(Customer c)
        {
            if (!_queue.Contains(c)) _queue.Add(c);
            c.SetQueueIndex(_queue.IndexOf(c));
        }

        public void ReleaseQueueIndex(Customer c)
        {
            _queue.Remove(c);
            // Re-index remaining queued customers so they shuffle forward.
            for (int i = 0; i < _queue.Count; i++)
                _queue[i].SetQueueIndex(i);
        }

        public Customer GetFrontOfQueue() => _queue.Count > 0 ? _queue[0] : null;
    }
}
```

**Wiring in Editor.** Build a Customer prefab: a capsule/quad with `NavMeshAgent` (radius ~0.4, speed ~2.5) and the `Customer` component. Optionally add an `Animator` for visual flair (not required by `Customer` itself). Drop it into `CustomerSpawnManager.customerPrefab`. **Critical:** the floor under `SpawnPoint`, queue points, ticket booth, hall seats, and exit must all lie on the baked NavMesh.

---

## 4. Movie Scheduling — MovieData + ScheduleManager

**Design.** `MovieData` is a ScriptableObject (title, genre, duration, price, popularity) so designers can add movies without code changes. `ScheduleManager` owns the single hall's state: current movie, show timer, and `HallCleanliness` (0–100). `TryScheduleShow` is the public API the UI calls; it validates the staffing gate (a Cashier must be on duty) and applies a satisfaction penalty if the hall is dirty when the show starts. Cleanliness decays during a show and slowly regenerates otherwise — Janitors accelerate the regen via `CleanHall(amount)` called from `StaffManager` when a Janitor is idle-patrolling near the hall.

### `Schedule/MovieData.cs`

```csharp
using UnityEngine;

namespace CinemaTycoon.Schedule
{
    public enum MovieGenre { Action, Comedy, Drama, Horror, SciFi, Family }

    [CreateAssetMenu(fileName = "MovieData", menuName = "CinemaTycoon/Movie")]
    public class MovieData : ScriptableObject
    {
        [Header("Identity")]
        public string title = "New Movie";
        [TextArea] public string description;
        public MovieGenre genre;

        [Header("Runtime & Economy")]
        [Tooltip("In-game seconds the show lasts.")]
        public float duration = 60f;
        public float baseTicketPrice = 12f;

        [Header("Audience")]
        [Range(0f, 1f)] public float popularity = 0.5f;
    }
}
```

### `Schedule/ScheduleManager.cs`

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using CinemaTycoon.Core;
using CinemaTycoon.Staff;

namespace CinemaTycoon.Schedule
{
    public class ScheduleManager : MonoBehaviour
    {
        [Header("Movies")]
        [SerializeField] private MovieData[] availableMovies;

        [Header("Hall State")]
        [SerializeField] private float cleanlinessThreshold = 50f;
        [SerializeField] private float dirtyStartPenalty = 10f;
        [SerializeField] private float decayPerSecondDuringShow = 5f;
        [SerializeField] private float passiveCleanPerSecond = 0.5f;

        private MovieData _currentMovie;
        private float _showStartGameTime;
        private float _scheduledStartTime = -1f;
        private bool _showActive;
        private float _hallCleanliness = 100f;

        public MovieData CurrentMovie => _currentMovie;
        public bool IsMoviePlaying => _showActive;
        public float CurrentTicketPrice => _currentMovie != null ? _currentMovie.baseTicketPrice : 0f;
        public float HallCleanliness => _hallCleanliness;
        public IReadOnlyList<MovieData> AvailableMovies => availableMovies;

        // True if a show is currently running OR scheduled to start soon —
        // customers use this to decide whether buying a ticket is worthwhile.
        public bool IsMoviePlayingOrImminent => _showActive || _scheduledStartTime > 0f;

        public static event Action<MovieData> OnShowStarted;
        public static event Action<MovieData> OnShowEnded;
        public static event Action<float> OnCleanlinessChanged;

        public void Initialize()
        {
            _hallCleanliness = 100f;
            OnCleanlinessChanged?.Invoke(_hallCleanliness);
        }

        private void Update()
        {
            if (_showActive)
            {
                _hallCleanliness = Mathf.Max(0f, _hallCleanliness - decayPerSecondDuringShow * Time.deltaTime);
                OnCleanlinessChanged?.Invoke(_hallCleanliness);

                if (Time.time - _showStartGameTime >= _currentMovie.duration)
                    EndShow();
            }
            else
            {
                // Passive regen when no show is running.
                if (_hallCleanliness < 100f)
                {
                    _hallCleanliness = Mathf.Min(100f, _hallCleanliness + passiveCleanPerSecond * Time.deltaTime);
                    OnCleanlinessChanged?.Invoke(_hallCleanliness);
                }

                // Auto-start a previously-scheduled show if its time has come.
                if (_scheduledStartTime > 0f && Time.time >= _scheduledStartTime)
                {
                    TryStartShow(_currentMovie);
                    _scheduledStartTime = -1f;
                }
            }
        }

        /// <summary>
        /// UI entry point: schedule a movie with optional delay. Returns false
        /// if a show is already running or the movie is null.
        /// </summary>
        public bool TryScheduleShow(MovieData movie, float delaySeconds = 0f)
        {
            if (movie == null || _showActive) return false;
            _currentMovie = movie;
            if (delaySeconds <= 0f) return TryStartShow(movie);
            _scheduledStartTime = Time.time + delaySeconds;
            return true;
        }

        /// <summary>
        /// Actually start the show. Validates the staffing gate (Cashier on duty)
        /// and applies a cleanliness penalty if the hall is dirty.
        /// </summary>
        public bool TryStartShow(MovieData movie)
        {
            if (movie == null || _showActive) return false;

            // Staffing gate: need a Cashier to sell tickets during this show.
            if (!GameManager.Instance.Staff.HasRoleOnDuty(StaffRole.Cashier))
            {
                Debug.Log("[Schedule] Cannot start show — no Cashier on duty.");
                return false;
            }

            if (_hallCleanliness < cleanlinessThreshold)
                GameManager.Instance.AdjustCinemaRating(-dirtyStartPenalty, "Show started in dirty hall");

            _currentMovie = movie;
            _showActive = true;
            _showStartGameTime = Time.time;
            OnShowStarted?.Invoke(movie);
            return true;
        }

        private void EndShow()
        {
            _showActive = false;
            var m = _currentMovie;
            _currentMovie = null;
            OnShowEnded?.Invoke(m);
        }

        /// <summary>Called by StaffManager when a Janitor cleans the hall.</summary>
        public void CleanHall(float amount)
        {
            _hallCleanliness = Mathf.Min(100f, _hallCleanliness + amount);
            OnCleanlinessChanged?.Invoke(_hallCleanliness);
        }
    }
}
```

**Wiring in Editor.** Attach `ScheduleManager` to a child of `GameManager`. Create 3–5 `MovieData` assets (varied genres/durations/prices) and assign to `Available Movies`. The UI's "Schedule Movie" button should call `TryScheduleShow(selectedMovie, delaySeconds)`.

---

## 5. Staff Management — Staff + StaffManager + StaffRoleData

**Design.** Three roles only — `Cashier`, `Janitor`, `Guard` — each defined by a `StaffRoleData` ScriptableObject (cost, wage, task duration). Each `Staff` member is a `NavMeshAgent` + `Animator` driven object that idles at its home station until `StaffManager.AssignTask` redirects it. Animator parameters `IsWalking` and `IsWorking` are set every frame from agent velocity and work state — this is the "trigger hookup" the spec asks for. `StaffManager` maintains a `Queue<StaffTask>`; on each `Update` it pulls tasks and dispatches them to the nearest idle staff. When `EventManager` raises a Spill, `StaffManager` auto-enqueues a cleanup task at the spill's location — Janitors are pulled off idle-patrol automatically. Cashier presence is broadcast to the front-of-queue customer every frame so they can advance to purchasing.

### `Staff/StaffRoleData.cs`

```csharp
using UnityEngine;

namespace CinemaTycoon.Staff
{
    public enum StaffRole { Cashier, Janitor, Guard }

    [CreateAssetMenu(fileName = "StaffRoleData", menuName = "CinemaTycoon/StaffRole")]
    public class StaffRoleData : ScriptableObject
    {
        public StaffRole role;
        public string displayName;
        public float hireCost = 100f;
        public float wagePerTick = 5f;
        [Tooltip("Seconds of in-game time to complete one assigned task.")]
        public float taskDuration = 4f;
    }

    /// <summary>
    /// Lightweight POCO task envelope. Priority is informational — StaffManager
    /// currently processes FIFO, but the field is here for future expansion.
    /// </summary>
    public class StaffTask
    {
        public Vector3 TargetPosition;
        public System.Action OnComplete;
        public int Priority;
    }
}
```

### `Staff/Staff.cs`

```csharp
using UnityEngine;
using UnityEngine.AI;

namespace CinemaTycoon.Staff
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Animator))]
    public class Staff : MonoBehaviour
    {
        [SerializeField] private float workSpeed = 1f;

        public StaffRole Role { get; private set; }
        public StaffRoleData Config { get; private set; }
        public bool IsBusy { get; private set; }
        public Vector3 HomeStation { get; private set; }

        private NavMeshAgent _agent;
        private Animator _animator;
        private StaffTask _currentTask;
        private float _workTimer;

        // Cached Animator parameter hashes — avoids string lookups per frame.
        private static readonly int WalkHash = Animator.StringToHash("IsWalking");
        private static readonly int WorkHash = Animator.StringToHash("IsWorking");

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _animator = GetComponent<Animator>();
        }

        public void Initialize(StaffRole role, StaffRoleData config, Vector3 station)
        {
            Role = role;
            Config = config;
            HomeStation = station;
            ReturnHome();
        }

        private void Update()
        {
            // Drive animator parameters from real state.
            bool moving = _agent.velocity.sqrMagnitude > 0.01f;
            _animator.SetBool(WalkHash, moving);
            _animator.SetBool(WorkHash, IsBusy && !moving);

            if (_currentTask != null && !IsBusy)
            {
                // Arrived at task target? Begin work.
                if (!_agent.pathPending && _agent.remainingDistance < 1.0f)
                    BeginWork();
            }
            else if (IsBusy)
            {
                _workTimer -= Time.deltaTime * workSpeed;
                if (_workTimer <= 0f) CompleteTask();
            }
        }

        public void AssignTask(StaffTask task)
        {
            _currentTask = task;
            _agent.isStopped = false;
            _agent.SetDestination(task.TargetPosition);
        }

        public void ReturnHome()
        {
            _currentTask = null;
            _agent.isStopped = false;
            _agent.SetDestination(HomeStation);
        }

        private void BeginWork()
        {
            IsBusy = true;
            _workTimer = Config != null ? Config.taskDuration : 4f;
            _agent.isStopped = true;
        }

        private void CompleteTask()
        {
            IsBusy = false;
            _agent.isStopped = false;
            _currentTask?.OnComplete?.Invoke();
            _currentTask = null;
            ReturnHome();
        }
    }
}
```

### `Staff/StaffManager.cs`

```csharp
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
```

**Wiring in Editor.** Attach `StaffManager` to a child of `GameManager`. Create 3 `StaffRoleData` assets (one per role) with sensible costs/wages. Build a Staff prefab (capsule + `NavMeshAgent` + `Animator` + `Staff`); assign an AnimatorController with `IsWalking` and `IsWorking` bool parameters wired to blend between idle/walk/work clips (or just leave the parameters present — the code sets them regardless). Assign the prefab and the three role configs.

---

## 6. Random Events — EventManager

**Design.** Exactly two event types: `Spill` (Janitor must clean; unresolved → satisfaction penalty) and `VIPVisit` (a randomly-selected active customer is flagged; if they reach a successful ticket purchase within the window, reward; if they leave unsatisfied or the window expires, larger penalty). `EventManager` runs on randomized intervals (`minInterval`/`maxInterval`) and ticks down each event's `RemainingTime`. Events are POCOs (not MonoBehaviours) so they're cheap to create/destroy. `OnEventTriggered`/`OnEventResolved`/`OnEventExpired` are static events the HUD listens to for notifications. `StaffManager` subscribes to `OnEventTriggered` to auto-divert a Janitor on Spills; `EventManager` subscribes to `Customer.OnTicketPurchased` to detect successful VIP service.

### `Events/EventManager.cs`

```csharp
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
            _activeEvents.Add(evt);
            OnEventTriggered?.Invoke(evt);
        }

        private void TriggerVipVisit()
        {
            // Find a non-VIP customer currently in the cinema to elevate.
            Customer target = null;
            foreach (var c in GameManager.Instance.Spawner.ActiveCustomers)
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

            if (evt.Type == GameEventType.VIPVisit && evt.RelatedVIP != null)
            {
                float vipSat = evt.RelatedVIP.Satisfaction;
                float delta = vipSat > 60f ? vipGoodServiceReward : -vipPoorServicePenalty;
                GameManager.Instance.AdjustCinemaRating(delta, "VIP visit resolved");
            }
            else
            {
                GameManager.Instance.AdjustCinemaRating(2f, "Spill cleaned");
            }

            OnEventResolved?.Invoke(evt);
        }

        private void ExpireEvent(GameEvent evt)
        {
            if (evt.Type == GameEventType.Spill)
                GameManager.Instance.AdjustCinemaRating(-unresolvedSpillPenalty, "Unresolved spill");
            else
                GameManager.Instance.AdjustCinemaRating(-vipPoorServicePenalty, "VIP left unserved");

            OnEventExpired?.Invoke(evt);
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
```

I referenced `Customer.MarkAsVIP()` — add this one-liner to the `Customer` class:

```csharp
/// <summary>Used by EventManager to elevate a normal customer to VIP mid-visit.</summary>
public void MarkAsVIP() => IsVIP = true;
```

**Wiring in Editor.** Attach `EventManager` to a child of `GameManager`. Assign 3–4 empty GameObjects around the lobby as `Possible Spill Locations`. The HUD should subscribe to `OnEventTriggered`/`OnEventResolved`/`OnEventExpired` to show/hide notification toasts.

---

## 7. Cheat Manager

**Design.** Wrapped in `#if UNITY_EDITOR || DEVELOPMENT_BUILD` so it's stripped from release builds entirely — no risk of leaking cheats. The panel is drawn with IMGUI (`OnGUI`) so there are zero prefab/Canvas dependencies. Crucially, it never duplicates logic — every button calls an existing public method on a manager (`Economy.DevInjectFunds`, `GameManager.AdjustCinemaRating`, `EventManager.DevForceNextEvent`).

### `DevTools/CheatManager.cs`

```csharp
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using CinemaTycoon.Core;

namespace CinemaTycoon.DevTools
{
    /// <summary>
    /// Dev-only cheat panel. Wrapped in #if so it cannot ship in release.
    /// All actions delegate to existing manager methods — no duplicated logic.
    /// </summary>
    public class CheatManager : MonoBehaviour
    {
        [SerializeField] private KeyCode toggleKey = KeyCode.F1;
        [SerializeField] private float cheatMoneyAmount = 500f;

        private bool _showPanel;
        private Rect _panelRect = new Rect(10, 10, 280, 200);

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey)) _showPanel = !_showPanel;
        }

        private void OnGUI()
        {
            if (!_showPanel) return;
            _panelRect = GUI.Window(0, _panelRect, DrawWindow, "Cheat Panel (DEV)");
        }

        private void DrawWindow(int id)
        {
            var gm = GameManager.Instance;
            if (gm == null) { GUILayout.Label("GameManager not ready."); GUI.DragWindow(); return; }

            GUILayout.Label($"Balance: ${gm.Economy.Balance:F0}");
            GUILayout.Label($"Rating:  {gm.CinemaRating:F1}%");
            GUILayout.Space(8);

            if (GUILayout.Button($"Inject ${cheatMoneyAmount:F0}"))
                gm.Economy.DevInjectFunds(cheatMoneyAmount);

            if (GUILayout.Button("Force-Max Satisfaction"))
                gm.AdjustCinemaRating(100f - gm.CinemaRating, "Cheat: max satisfaction");

            if (GUILayout.Button("Skip to Next Event"))
                gm.Events.DevForceNextEvent();

            GUI.DragWindow();
        }
    }
}
#endif
```

**Wiring in Editor.** Drop `CheatManager` on any GameObject in the gameplay scene. Toggle the panel at runtime with `F1`. The script compiles to nothing in non-dev builds.

---

## 8. Pause Menu & Scene Flow — SceneFlowManager

**Design.** Pause is `Time.timeScale = 0` (which halts every `Update`-driven simulation, including customers, events, and the spawn timer). The `SceneFlowManager` owns the pause/settings/game-over panels via `[SerializeField]` references and exposes `Resume`/`Restart`/`OpenSettings`/`CloseSettings`/`ExitToMainMenu` as public methods that UI buttons can wire to in the Inspector via `Button.onClick`. On `Restart`/`ExitToMainMenu`, the `GameManager` singleton is explicitly destroyed so the next scene starts with fresh state rather than inheriting persisted balance/staff from the previous run.

### `UI/SceneFlowManager.cs`

```csharp
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using CinemaTycoon.Core;

namespace CinemaTycoon.UI
{
    public class SceneFlowManager : MonoBehaviour
    {
        [Header("UI Panels")]
        [SerializeField] private GameObject pauseMenuPanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject gameOverPanel;

        [Header("Scene Names")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private string gameplaySceneName = "Game";

        [Header("Input")]
        [SerializeField] private KeyCode pauseKey = KeyCode.Escape;

        private bool _isPaused;
        public bool IsPaused => _isPaused;

        public static event Action<bool> OnPauseToggled;

        private void OnEnable()  => GameManager.OnGameOver += HandleGameOver;
        private void OnDisable() => GameManager.OnGameOver -= HandleGameOver;

        private void Update()
        {
            if (Input.GetKeyDown(pauseKey)) TogglePause();
        }

        public void TogglePause()
        {
            if (_isPaused) Resume(); else Pause();
        }

        public void Pause()
        {
            _isPaused = true;
            Time.timeScale = 0f;
            if (pauseMenuPanel) pauseMenuPanel.SetActive(true);
            OnPauseToggled?.Invoke(true);
        }

        public void Resume()
        {
            _isPaused = false;
            Time.timeScale = 1f;
            if (pauseMenuPanel) pauseMenuPanel.SetActive(false);
            if (settingsPanel) settingsPanel.SetActive(false);
            OnPauseToggled?.Invoke(false);
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            // Destroy the persisted singleton so the fresh scene gets a clean GameManager.
            if (GameManager.Instance != null) Destroy(GameManager.Instance.gameObject);
            SceneManager.LoadScene(gameplaySceneName);
        }

        public void OpenSettings()
        {
            if (settingsPanel) settingsPanel.SetActive(true);
        }

        public void CloseSettings()
        {
            if (settingsPanel) settingsPanel.SetActive(false);
        }

        public void ExitToMainMenu()
        {
            Time.timeScale = 1f;
            if (GameManager.Instance != null) Destroy(GameManager.Instance.gameObject);
            SceneManager.LoadScene(mainMenuSceneName);
        }

        private void HandleGameOver(string reason)
        {
            if (gameOverPanel) gameOverPanel.SetActive(true);
            // Wire `reason` to a TMP_Text in a real build — omitted here for scope.
            Debug.Log($"[SceneFlow] Game Over: {reason}");
        }
    }
}
```

**Wiring in Editor.** Create a Canvas with three child panels (`PauseMenuPanel`, `SettingsPanel`, `GameOverPanel`), all initially inactive except as needed. Attach `SceneFlowManager` and drag the panels in. Wire UI buttons: `Resume Button → SceneFlowManager.Resume`, `Restart Button → SceneFlowManager.Restart`, `Settings Button → SceneFlowManager.OpenSettings`, `Exit Button → SceneFlowManager.ExitToMainMenu`. Add a "Back" button on the settings panel wired to `CloseSettings`.

---

## 9. HUD Bindings — the data hooks

**Design.** `HUDBindings` is the single place where UI meets gameplay events. Every manager's `static event` is subscribed in `OnEnable` and unsubscribed in `OnDisable`. The class itself owns no state — it's pure plumbing from event → UI element. Each `[SerializeField]` is optional (null-checked) so partial HUDs still compile and run. The comments at the top of the file enumerate exactly which event each HUD element should react to, satisfying the spec's requirement that HUD hooks be real even if UI polish isn't.

### `UI/HUDBindings.cs`

```csharp
using UnityEngine;
using UnityEngine.UI;
using CinemaTycoon.Core;
using CinemaTycoon.Economy;
using CinemaTycoon.Customers;
using CinemaTycoon.Staff;
using CinemaTycoon.Schedule;
using CinemaTycoon.Events;

namespace CinemaTycoon.UI
{
    /// <summary>
    /// Central HUD plumbing. Subscribes to manager events and updates UI elements.
    ///
    /// Event → HUD element mapping (the spec asks for this enumeration):
    ///   EconomyManager.OnBalanceChanged      → balanceText
    ///   GameManager.OnCinemaRatingChanged    → ratingText + ratingSlider
    ///   ScheduleManager.OnShowStarted/Ended  → nowPlayingText
    ///   ScheduleManager.OnCleanlinessChanged → cleanlinessSlider
    ///   StaffManager.OnStaffHired            → staffCountText
    ///   Customer.OnTicketPurchased           → lastTicketText
    ///   EventManager.OnEventTriggered        → eventNotification (instantiated prefab)
    ///   EventManager.OnEventResolved/Expired → (fade-out hook)
    ///   GameManager.OnGameOver               → handled by SceneFlowManager.gameOverPanel
    /// </summary>
    public class HUDBindings : MonoBehaviour
    {
        [Header("Balance")]      [SerializeField] private Text balanceText;
        [Header("Rating")]       [SerializeField] private Text ratingText;
                                 [SerializeField] private Slider ratingSlider;
        [Header("Schedule")]     [SerializeField] private Text nowPlayingText;
                                 [SerializeField] private Slider cleanlinessSlider;
        [Header("Staff")]        [SerializeField] private Text staffCountText;
        [Header("Events")]       [SerializeField] private GameObject eventNotificationPrefab;
                                 [SerializeField] private Transform eventNotificationContainer;
        [Header("Tickets")]      [SerializeField] private Text lastTicketText;

        private void OnEnable()
        {
            EconomyManager.OnBalanceChanged    += HandleBalanceChanged;
            GameManager.OnCinemaRatingChanged  += HandleRatingChanged;
            ScheduleManager.OnShowStarted      += HandleShowStarted;
            ScheduleManager.OnShowEnded        += HandleShowEnded;
            ScheduleManager.OnCleanlinessChanged += HandleCleanlinessChanged;
            StaffManager.OnStaffHired          += HandleStaffHired;
            Customer.OnTicketPurchased         += HandleTicketPurchased;
            EventManager.OnEventTriggered      += HandleEventTriggered;
            EventManager.OnEventResolved       += HandleEventResolved;
            EventManager.OnEventExpired        += HandleEventExpired;
        }

        private void OnDisable()
        {
            EconomyManager.OnBalanceChanged    -= HandleBalanceChanged;
            GameManager.OnCinemaRatingChanged  -= HandleRatingChanged;
            ScheduleManager.OnShowStarted      -= HandleShowStarted;
            ScheduleManager.OnShowEnded        -= HandleShowEnded;
            ScheduleManager.OnCleanlinessChanged -= HandleCleanlinessChanged;
            StaffManager.OnStaffHired          -= HandleStaffHired;
            Customer.OnTicketPurchased         -= HandleTicketPurchased;
            EventManager.OnEventTriggered      -= HandleEventTriggered;
            EventManager.OnEventResolved       -= HandleEventResolved;
            EventManager.OnEventExpired        -= HandleEventExpired;
        }

        private void Start()
        {
            // Pull current state on first frame — events may have fired before subscribe
            // in edge cases; this guarantees the HUD isn't stale on scene load.
            var gm = GameManager.Instance;
            if (gm != null)
            {
                HandleBalanceChanged(gm.Economy.Balance);
                HandleRatingChanged(gm.CinemaRating);
                HandleCleanlinessChanged(gm.Schedule.HallCleanliness);
                if (gm.Schedule.CurrentMovie != null) HandleShowStarted(gm.Schedule.CurrentMovie);
                else HandleShowEnded(null);
                if (staffCountText) staffCountText.text = $"Staff: {gm.Staff.ActiveStaff.Count}";
            }
        }

        private void HandleBalanceChanged(float b)
            { if (balanceText) balanceText.text = $"Balance: ${b:F0}"; }

        private void HandleRatingChanged(float r)
        {
            if (ratingText)   ratingText.text   = $"Rating: {r:F1}%";
            if (ratingSlider) ratingSlider.value = r / 100f;
        }

        private void HandleShowStarted(MovieData m)
            { if (nowPlayingText) nowPlayingText.text = $"Now Playing: {m.title}"; }

        private void HandleShowEnded(MovieData m)
            { if (nowPlayingText) nowPlayingText.text = "Now Playing: —"; }

        private void HandleCleanlinessChanged(float v)
            { if (cleanlinessSlider) cleanlinessSlider.value = v / 100f; }

        private void HandleStaffHired(Staff s)
        {
            if (staffCountText)
                staffCountText.text = $"Staff: {GameManager.Instance.Staff.ActiveStaff.Count}";
        }

        private void HandleTicketPurchased(Customer c, float amt)
            { if (lastTicketText) lastTicketText.text = $"+${amt:F0} ticket"; }

        private void HandleEventTriggered(GameEvent evt)
        {
            if (!eventNotificationPrefab || !eventNotificationContainer) return;
            var go = Instantiate(eventNotificationPrefab, eventNotificationContainer);
            var text = go.GetComponentInChildren<Text>();
            if (text) text.text = $"{evt.Title}\n{evt.Description}";
            Destroy(go, evt.TotalDuration); // auto-dismiss
        }

        private void HandleEventResolved(GameEvent evt) { /* hook for fade-out animations */ }
        private void HandleEventExpired(GameEvent evt)  { /* hook for fade-out animations */ }
    }
}
```

**Wiring in Editor.** Build a Canvas with `Text`/`Slider` children for balance, rating, now-playing, cleanliness, staff count, last-ticket, and a `Vertical Layout Group` container for event notifications. Create a simple notification prefab (a `Text` on a `Image` background) and assign it. Attach `HUDBindings` and drag every element into its slot — unused slots can be left null safely.

---

## Final integration checklist

1. **Scene setup.** Create `Game` and `MainMenu` scenes (add both to Build Settings). In `Game`, place `GameManager`, `CinemaWaypoints`, the five manager MonoBehaviours (or attach them all under `GameManager`), `CheatManager`, `SceneFlowManager`, `HUDBindings`, and the Canvas.
2. **NavMesh.** Bake a NavMesh across the lobby floor, queue area, ticket booth approach, hall floor, and exit path. Verify by toggling the NavMesh visualization — every waypoint transform must lie on the blue surface.
3. **ScriptableObject assets.** Create 3–5 `MovieData`, 3 `StaffRoleData`, 4 `UpgradeData` instances in `Assets/_CinemaTycoon/Data/`.
4. **Prefabs.** Customer prefab (capsule + `NavMeshAgent` + `Customer`) and Staff prefab (capsule + `NavMeshAgent` + `Animator` + `Staff` with an AnimatorController exposing `IsWalking` and `IsWorking` bool parameters).
5. **Initial gameplay loop to test.** Press Play → customers spawn and queue but cannot purchase (no Cashier) → satisfaction drops → cinema rating falls. Hire a Cashier → customers advance and purchase. Schedule a movie → customers watch → satisfaction recovers. Wait ~30s → a Spill or VIP event triggers → assign/observe resolution. Open the cheat panel with `F1` to inject cash if you go bankrupt while testing.

This gives you a fully wired end-to-end loop: spawn → queue → purchase → watch → leave, with staff gating, dynamic economy, random events, pause/restart, dev cheats, and a HUD that reacts to every meaningful state change via events — all in idiomatic Unity 6 C#.
