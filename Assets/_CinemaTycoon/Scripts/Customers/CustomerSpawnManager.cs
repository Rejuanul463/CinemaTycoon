using System;
using System.Collections.Generic;
using UnityEngine;
using CinemaTycoon.Core;

namespace CinemaTycoon.Customers
{
    public class CustomerSpawnManager : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [Tooltip("List of customer prefabs. One is picked at random on each spawn. " +
                 "Every entry must have a Customer component.")]
        [SerializeField] private GameObject[] customerPrefabs;
        [SerializeField] private float spawnInterval = 4f;
        [SerializeField] private int maxConcurrentCustomers = 30;
        [SerializeField] [Range(0f, 1f)] private float vipChance = 0.08f;

        private readonly List<Customer> _queue = new();
        private readonly HashSet<Customer> _active = new();
        private float _spawnTimer;

        public IReadOnlyCollection<Customer> ActiveCustomers => _active;

        public static event Action<Customer> OnCustomerSpawned;

        public void Initialize() => _spawnTimer = 0f;

        private void Update()
        {
            _spawnTimer += Time.deltaTime;

            var gm = GameManager.Instance;
            if (gm == null || gm.Economy == null) return;

            // Showtime Rush: when a movie is playing or about to start, accelerate
            // spawn rate and expand concurrent capacity so the auditorium fills up!
            bool isShowtime = gm.Schedule != null && gm.Schedule.IsMoviePlayingOrImminent;
            float speedMult = isShowtime ? 2.5f : 1.0f;
            float interval = (spawnInterval / speedMult) / gm.Economy.MarketingMultiplier;
            int effectiveCap = isShowtime ? maxConcurrentCustomers : 12;

            if (_spawnTimer >= interval && _active.Count < effectiveCap)
            {
                _spawnTimer = 0f;
                SpawnCustomer();
            }
        }

        private void SpawnCustomer()
        {
            var prefab = PickCustomerPrefab();
            if (prefab == null || CinemaWaypoints.Instance == null) return;

            Vector3 pos = CinemaWaypoints.Instance.SpawnPoint.position;
            GameObject go = Instantiate(prefab, pos, Quaternion.identity);
            var cust = go.GetComponent<Customer>();
            if (cust == null)
            {
                Debug.LogError($"[CustomerSpawnManager] Prefab '{prefab.name}' missing Customer component.", prefab);
                Destroy(go);
                return;
            }

            bool isVIP = UnityEngine.Random.value < vipChance;
            cust.Initialize(this, isVIP);
            _active.Add(cust);
            OnCustomerSpawned?.Invoke(cust);
        }

        /// <summary>Returns a random entry from customerPrefabs, or null if the list is empty.</summary>
        private GameObject PickCustomerPrefab()
        {
            if (customerPrefabs == null || customerPrefabs.Length == 0) return null;
            return customerPrefabs[UnityEngine.Random.Range(0, customerPrefabs.Length)];
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
