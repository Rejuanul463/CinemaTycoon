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
            var gm = GameManager.Instance;
            if (gm == null || gm.Economy == null) return;
            float interval = spawnInterval / gm.Economy.MarketingMultiplier;

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
