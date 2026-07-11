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
