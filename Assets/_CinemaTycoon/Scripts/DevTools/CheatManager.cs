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
