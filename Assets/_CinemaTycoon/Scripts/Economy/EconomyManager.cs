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
