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
        private Rect _panelRect = new Rect(10, 10, 280, 320);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (FindFirstObjectByType<CheatManager>() == null)
            {
                var go = new GameObject("[Dev] CheatManager");
                go.AddComponent<CheatManager>();
                DontDestroyOnLoad(go);
                Debug.Log("[CheatManager] Auto-initialized dev cheat panel. Press F1 or ~ (tilde) to toggle.");
            }
        }

        private void Update()
        {
            // Support F1, Fn+F1 on Mac, and ~ (BackQuote) key
            if (Input.GetKeyDown(toggleKey) || Input.GetKeyDown(KeyCode.F1) || Input.GetKeyDown(KeyCode.BackQuote))
            {
                _showPanel = !_showPanel;
            }
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
            GUILayout.Label($"Janitors: {CountJanitors(gm)} | Guards: {CountGuards(gm)} on duty");

            // Show the cleanliness trend so the user understands the bar's
            // "fighting itself" behaviour. State is sourced from the public
            // ScheduleManager fields, not from inspecting the bar.
            float clean = gm.Schedule.HallCleanliness;
            string trend = gm.Schedule.IsMoviePlaying
                ? $"<color=#e74c3c>▼ decaying</color> (show in progress, -5/s)"
                : $"<color=#27ae60>▲ recovering</color> (+{(CountJanitors(gm) > 0 ? "2.5" : "0.5")}/s, no show)";
            GUILayout.Label($"Cleanliness: {clean:F0}% {trend}");
            GUILayout.Space(8);

            if (GUILayout.Button($"Inject ${cheatMoneyAmount:F0}"))
                gm.Economy.DevInjectFunds(cheatMoneyAmount);

            if (GUILayout.Button("Force-Max Satisfaction"))
                gm.AdjustCinemaRating(100f - gm.CinemaRating, "Cheat: max satisfaction");

            if (GUILayout.Button("Force-Max Cleanliness"))
                gm.Schedule.CleanHall(1000f);

            if (GUILayout.Button("Skip to Next Event"))
                gm.Events.DevForceNextEvent();

            if (GUILayout.Button("Force Spill Here (at camera)"))
            {
                if (Mathf.Approximately(Time.timeScale, 0f))
                {
                    Debug.LogWarning("[CheatManager] Time.timeScale is 0 — you appear to be in the " +
                                     "main menu or the pause overlay. The spill was spawned but the " +
                                     "simulation is frozen.");
                }
                var cam = Camera.main;
                Vector3 pos = cam != null ? cam.transform.position : Vector3.zero;
                gm.Events.DevForceSpillAt(pos);
            }

            GUI.DragWindow();
        }

        private static int CountJanitors(GameManager gm)
        {
            if (gm?.Staff == null) return 0;
            int n = 0;
            foreach (var s in gm.Staff.ActiveStaff)
                if (s != null && s.Role == Staff.StaffRole.Janitor) n++;
            return n;
        }

        private static int CountGuards(GameManager gm)
        {
            if (gm?.Staff == null) return 0;
            int n = 0;
            foreach (var s in gm.Staff.ActiveStaff)
                if (s != null && s.Role == Staff.StaffRole.Guard) n++;
            return n;
        }
    }
}
#endif
