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
    [System.Obsolete("Legacy uGUI HUD. Use CinemaTycoon.UI.UIToolkitHUDBindings (UI Toolkit) instead.")]
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

        private void HandleStaffHired(CinemaTycoon.Staff.Staff s)
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
