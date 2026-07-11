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
