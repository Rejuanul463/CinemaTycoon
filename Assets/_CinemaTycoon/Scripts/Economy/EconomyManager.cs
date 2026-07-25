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
        PremiumPopcorn, // boosts popcorn purchase chance + popcorn revenue (also retains legacy +ticket revenue)
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

        [Header("Wages & Utilities")]
        [SerializeField] private float staffWagePerTick = 5f;
        [SerializeField] private float wageTickInterval = 15f;
        [SerializeField] private float utilityCostPerTick = 40f;
        [SerializeField] private float utilityTickInterval = 30f;

        [Header("Upgrades")]
        [SerializeField] private List<UpgradeData> availableUpgrades = new();

        private float _balance;
        private readonly List<Transaction> _transactions = new();
        private readonly HashSet<UpgradeType> _purchased = new();
        private float _wageTimer;
        private float _utilityTimer;

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

            // Fallback: if no UpgradeData assets were wired in the Inspector,
            // auto-discover them from any "Resources" folder at runtime. This
            // lets the game ship with working upgrades without requiring the
            // EconomyManager Inspector to reference each .asset explicitly.
            if (availableUpgrades == null || availableUpgrades.Count == 0)
            {
                var loaded = Resources.LoadAll<UpgradeData>("");
                if (loaded != null && loaded.Length > 0)
                {
                    availableUpgrades.AddRange(loaded);
                    Debug.Log($"[EconomyManager] Auto-loaded {loaded.Length} UpgradeData assets from Resources.");
                }
                else
                {
                    Debug.LogWarning("[EconomyManager] No UpgradeData assets found. Upgrade buttons will be inert. " +
                                     "Author UpgradeData assets via Create > CinemaTycoon > Upgrade, assign them to " +
                                     "EconomyManager.availableUpgrades, or place them in a Resources folder.");
                }
            }
        }

        private void Update()
        {
            _wageTimer += Time.deltaTime;
            if (_wageTimer >= wageTickInterval)
            {
                _wageTimer = 0f;
                PayWages();
            }

            _utilityTimer += Time.deltaTime;
            if (_utilityTimer >= utilityTickInterval)
            {
                _utilityTimer = 0f;
                Spend(utilityCostPerTick, "Utility & venue overhead");
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
            {
                var gm = GameManager.Instance;
                if (gm != null) gm.TriggerGameOver("You went bankrupt!");
            }
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
            Debug.LogWarning($"[EconomyManager] No UpgradeData asset found for upgrade type {type}. " +
                             $"Check that availableUpgrades contains an asset with type={type}.");
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

        /// <summary>
        /// Multiplier applied to a customer's base chance of buying popcorn.
        /// Backed by PremiumPopcorn — purchasing the upgrade makes the concession
        /// stand more attractive to customers (better menu, marketing, etc).
        /// </summary>
        public float PopcornChanceMultiplier => GetMultiplier(UpgradeType.PremiumPopcorn);

        /// <summary>
        /// Multiplier applied to the per-sale popcorn revenue. Also PremiumPopcorn,
        /// so a single upgrade purchase improves both the volume and the per-sale
        /// margin of the concession stand.
        /// </summary>
        public float PopcornRevenueMultiplier => GetMultiplier(UpgradeType.PremiumPopcorn);

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
