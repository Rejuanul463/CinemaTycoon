using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using CinemaTycoon.Core;
using CinemaTycoon.Economy;
using CinemaTycoon.Customers;
using CinemaTycoon.Schedule;
using CinemaTycoon.Events;
using CinemaTycoon.Staff;

namespace CinemaTycoon.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class UIToolkitHUDBindings : MonoBehaviour
    {
        [Header("Templates")]
        [SerializeField] private VisualTreeAsset notificationTemplate;
        [SerializeField] private StyleSheet hudStyleSheet;

        private VisualElement _root;

        private bool _debugPointerHooked;

        // Main Panels
        private VisualElement _mainMenuPanel;
        private VisualElement _gameplayHud;

        // Main Menu Buttons
        private Button _startButton;
        private Button _quitButton;

        // HUD Sidebar Management Buttons
        private Button _hireCashierButton;
        private Button _hireJanitorButton;
        private Button _hireGuardButton;
        private Button _buyPopcornButton;
        private Button _buyCashierButton;
        private Button _buySeatsButton;
        private Button _buyMarketingButton;
        private VisualElement _movieButtonsContainer;

        // Overlays
        private VisualElement _pauseOverlay;
        private VisualElement _settingsOverlay;
        private VisualElement _gameoverOverlay;

        // Pause Menu Buttons
        private Button _resumeButton;
        private Button _restartButton;
        private Button _settingsButton;
        private Button _exitButton;

        // Settings Back Button
        private Button _settingsBackButton;

        // Game Over Buttons & Text
        private Label _gameoverReasonLabel;
        private Button _gameoverRestartButton;
        private Button _gameoverExitButton;

        // Stats Labels
        private Label _balanceLabel;
        private Label _ratingLabel;
        private ProgressBar _ratingBar;
        private Label _nowPlayingLabel;
        private ProgressBar _cleanlinessBar;
        private Label _staffCountLabel;
        private VisualElement _notificationContainer;
        private Label _lastTicketLabel;

        private EventCallback<ClickEvent> _hireCashierCallback;
        private EventCallback<ClickEvent> _hireJanitorCallback;
        private EventCallback<ClickEvent> _hireGuardCallback;
        private EventCallback<ClickEvent> _buyPopcornCallback;
        private EventCallback<ClickEvent> _buyCashierCallback;
        private EventCallback<ClickEvent> _buySeatsCallback;
        private EventCallback<ClickEvent> _buyMarketingCallback;
        private readonly List<(Button button, EventCallback<ClickEvent> callback)> _movieButtonCallbacks = new();

        // Accordion category buttons and their collapsible content panels.
        // Only one category is open at a time; clicking the open one closes it.
        private enum ManagementCategory { None, Staff, Movies, Upgrades }
        private Button _staffCategoryButton;
        private Button _moviesCategoryButton;
        private Button _upgradesCategoryButton;
        private VisualElement _staffCategoryContent;
        private VisualElement _moviesCategoryContent;
        private VisualElement _upgradesCategoryContent;
        private EventCallback<ClickEvent> _staffCategoryCallback;
        private EventCallback<ClickEvent> _moviesCategoryCallback;
        private EventCallback<ClickEvent> _upgradesCategoryCallback;
        private ManagementCategory _openCategory = ManagementCategory.None;
        private const string StaffCategoryLabel   = "STAFF";
        private const string MoviesCategoryLabel  = "MOVIES";
        private const string UpgradesCategoryLabel = "UPGRADES";
        private const string CategoryClosedGlyph = "\u25B8  "; // ▸
        private const string CategoryOpenGlyph   = "\u25BE  "; // ▾

        // State trackers
        private bool _isGameStarted = false;
        private bool _isPaused = false;
        private bool _isGameOver = false;

        private bool _sanityLogged;

        private void OnEnable()
        {
            _root = GetComponent<UIDocument>().rootVisualElement;

            if (_root == null)
            {
                Debug.LogError("[UIToolkitHUDBindings] Root visual element is null. Check if UIDocument is initialized.");
                return;
            }

            _root.RegisterCallback<PointerDownEvent>(OnRootPointerDown);
            _root.RegisterCallback<PointerUpEvent>(OnRootPointerUp);

            // Apply USS stylesheet programmatically
            if (hudStyleSheet != null)
            {
                _root.styleSheets.Add(hudStyleSheet);
            }
            else
            {
                Debug.LogWarning("[UIToolkitHUDBindings] hudStyleSheet is not assigned in the Inspector. UI will be unstyled.");
            }

            _mainMenuPanel = _root.Q<VisualElement>("main-menu-panel");
            _gameplayHud = _root.Q<VisualElement>("gameplay-hud");
            _startButton = _root.Q<Button>("start-button");
            _quitButton = _root.Q<Button>("quit-button");

            // Wire Main Menu button clicks
            if (_startButton != null) _startButton.clicked += HandleStartClicked;
            if (_quitButton != null) _quitButton.clicked += HandleQuitClicked;

            _hireCashierButton  = _root.Q<Button>("hire-cashier-button");
            _hireJanitorButton  = _root.Q<Button>("hire-janitor-button");
            _hireGuardButton    = _root.Q<Button>("hire-guard-button");
            _buyPopcornButton   = _root.Q<Button>("buy-popcorn-button");
            _buyCashierButton   = _root.Q<Button>("buy-cashier-button");
            _buySeatsButton     = _root.Q<Button>("buy-seats-button");
            _buyMarketingButton = _root.Q<Button>("buy-marketing-button");
            _movieButtonsContainer = _root.Q<VisualElement>("movie-buttons-container");

            _staffCategoryButton    = _root.Q<Button>("staff-category-button");
            _moviesCategoryButton   = _root.Q<Button>("movies-category-button");
            _upgradesCategoryButton = _root.Q<Button>("upgrades-category-button");
            _staffCategoryContent    = _root.Q<VisualElement>("staff-category-content");
            _moviesCategoryContent   = _root.Q<VisualElement>("movies-category-content");
            _upgradesCategoryContent = _root.Q<VisualElement>("upgrades-category-content");

            // Wire category-button clicks
            if (_staffCategoryButton != null)
            {
                _staffCategoryCallback = _ => ToggleCategory(ManagementCategory.Staff);
                _staffCategoryButton.RegisterCallback<ClickEvent>(_staffCategoryCallback);
            }
            if (_moviesCategoryButton != null)
            {
                _moviesCategoryCallback = _ => ToggleCategory(ManagementCategory.Movies);
                _moviesCategoryButton.RegisterCallback<ClickEvent>(_moviesCategoryCallback);
            }
            if (_upgradesCategoryButton != null)
            {
                _upgradesCategoryCallback = _ => ToggleCategory(ManagementCategory.Upgrades);
                _upgradesCategoryButton.RegisterCallback<ClickEvent>(_upgradesCategoryCallback);
            }

            ApplyCategoryState();

            Debug.Log($"[HUD] Button query results — " +
                      $"Cashier:{_hireCashierButton != null} " +
                      $"Janitor:{_hireJanitorButton != null} " +
                      $"Guard:{_hireGuardButton != null} " +
                      $"Popcorn:{_buyPopcornButton != null} " +
                      $"FastCashier:{_buyCashierButton != null} " +
                      $"Seats:{_buySeatsButton != null} " +
                      $"Marketing:{_buyMarketingButton != null}");

            // Wire Sidebar clicks using ClickEvent
            if (_hireCashierButton != null)
            {
                _hireCashierCallback = _ =>
                {
                    Debug.Log("[HUD] hire-cashier-button ClickEvent");
                    TryHireStaff(StaffRole.Cashier);
                };
                _hireCashierButton.RegisterCallback<ClickEvent>(_hireCashierCallback);
            }

            if (_hireJanitorButton != null)
            {
                _hireJanitorCallback = _ =>
                {
                    Debug.Log("[HUD] hire-janitor-button ClickEvent");
                    TryHireStaff(StaffRole.Janitor);
                };
                _hireJanitorButton.RegisterCallback<ClickEvent>(_hireJanitorCallback);
            }

            if (_hireGuardButton != null)
            {
                _hireGuardCallback = _ =>
                {
                    Debug.Log("[HUD] hire-guard-button ClickEvent");
                    TryHireStaff(StaffRole.Guard);
                };
                _hireGuardButton.RegisterCallback<ClickEvent>(_hireGuardCallback);
            }

            if (_buyPopcornButton != null)
            {
                _buyPopcornCallback = _ =>
                {
                    Debug.Log("[HUD] buy-popcorn-button ClickEvent");
                    TryPurchaseUpgrade(UpgradeType.PremiumPopcorn);
                };
                _buyPopcornButton.RegisterCallback<ClickEvent>(_buyPopcornCallback);
            }

            if (_buyCashierButton != null)
            {
                _buyCashierCallback = _ =>
                {
                    Debug.Log("[HUD] buy-cashier-button ClickEvent");
                    TryPurchaseUpgrade(UpgradeType.FasterCashier);
                };
                _buyCashierButton.RegisterCallback<ClickEvent>(_buyCashierCallback);
            }

            if (_buySeatsButton != null)
            {
                _buySeatsCallback = _ =>
                {
                    Debug.Log("[HUD] buy-seats-button ClickEvent");
                    TryPurchaseUpgrade(UpgradeType.ComfySeats);
                };
                _buySeatsButton.RegisterCallback<ClickEvent>(_buySeatsCallback);
            }

            if (_buyMarketingButton != null)
            {
                _buyMarketingCallback = _ =>
                {
                    Debug.Log("[HUD] buy-marketing-button ClickEvent");
                    TryPurchaseUpgrade(UpgradeType.Marketing);
                };
                _buyMarketingButton.RegisterCallback<ClickEvent>(_buyMarketingCallback);
            }

            _pauseOverlay = _root.Q<VisualElement>("pause-overlay");
            _settingsOverlay = _root.Q<VisualElement>("settings-overlay");
            _gameoverOverlay = _root.Q<VisualElement>("gameover-overlay");

            _resumeButton = _root.Q<Button>("resume-button");
            _restartButton = _root.Q<Button>("restart-button");
            _settingsButton = _root.Q<Button>("settings-button");
            _exitButton = _root.Q<Button>("exit-button");

            if (_resumeButton != null) _resumeButton.clicked += ResumeGame;
            if (_restartButton != null) _restartButton.clicked += RestartGame;
            if (_settingsButton != null) _settingsButton.clicked += OpenSettingsMenu;
            if (_exitButton != null) _exitButton.clicked += ExitToMainMenu;

            _settingsBackButton = _root.Q<Button>("settings-back-button");
            if (_settingsBackButton != null) _settingsBackButton.clicked += CloseSettingsMenu;

            _gameoverReasonLabel = _root.Q<Label>("gameover-reason-label");
            _gameoverRestartButton = _root.Q<Button>("gameover-restart-button");
            _gameoverExitButton = _root.Q<Button>("gameover-exit-button");

            if (_gameoverRestartButton != null) _gameoverRestartButton.clicked += RestartGame;
            if (_gameoverExitButton != null) _gameoverExitButton.clicked += ExitToMainMenu;

            // Query Stats elements
            _balanceLabel = _root.Q<Label>("balance-label");
            _ratingLabel = _root.Q<Label>("rating-label");
            _ratingBar = _root.Q<ProgressBar>("rating-bar");
            _nowPlayingLabel = _root.Q<Label>("now-playing-label");
            _cleanlinessBar = _root.Q<ProgressBar>("cleanliness-bar");
            _staffCountLabel = _root.Q<Label>("staff-count-label");
            _notificationContainer = _root.Q<VisualElement>("notification-container");
            _lastTicketLabel = _root.Q<Label>("last-ticket-label");

            EconomyManager.OnBalanceChanged += HandleBalanceChanged;
            GameManager.OnCinemaRatingChanged += HandleRatingChanged;
            ScheduleManager.OnShowStarted += HandleShowStarted;
            ScheduleManager.OnShowEnded += HandleShowEnded;
            ScheduleManager.OnCleanlinessChanged += HandleCleanlinessChanged;
            StaffManager.OnStaffHired += HandleStaffHired;
            Customer.OnTicketPurchased += HandleTicketPurchased;
            EventManager.OnEventTriggered += HandleEventTriggered;
            EventManager.OnEventResolved += HandleEventResolved;
            EventManager.OnEventExpired += HandleEventExpired;
            GameManager.OnGameOver += HandleGameOver;
            FlyCameraController.OnCursorLockChanged += HandleCursorLockChanged;
        }

        private void OnDisable()
        {
            _root.UnregisterCallback<PointerDownEvent>(OnRootPointerDown);
            _root.UnregisterCallback<PointerUpEvent>(OnRootPointerUp);

            if (_startButton != null) _startButton.clicked -= HandleStartClicked;
            if (_quitButton != null) _quitButton.clicked -= HandleQuitClicked;

            // Unregister stored ClickEvent delegates on sidebar buttons.
            if (_hireCashierButton != null && _hireCashierCallback != null)
                _hireCashierButton.UnregisterCallback<ClickEvent>(_hireCashierCallback);
            if (_hireJanitorButton != null && _hireJanitorCallback != null)
                _hireJanitorButton.UnregisterCallback<ClickEvent>(_hireJanitorCallback);
            if (_hireGuardButton != null && _hireGuardCallback != null)
                _hireGuardButton.UnregisterCallback<ClickEvent>(_hireGuardCallback);
            if (_buyPopcornButton != null && _buyPopcornCallback != null)
                _buyPopcornButton.UnregisterCallback<ClickEvent>(_buyPopcornCallback);
            if (_buyCashierButton != null && _buyCashierCallback != null)
                _buyCashierButton.UnregisterCallback<ClickEvent>(_buyCashierCallback);
            if (_buySeatsButton != null && _buySeatsCallback != null)
                _buySeatsButton.UnregisterCallback<ClickEvent>(_buySeatsCallback);
            if (_buyMarketingButton != null && _buyMarketingCallback != null)
                _buyMarketingButton.UnregisterCallback<ClickEvent>(_buyMarketingCallback);

            // Unregister category-button delegates.
            if (_staffCategoryButton != null && _staffCategoryCallback != null)
                _staffCategoryButton.UnregisterCallback<ClickEvent>(_staffCategoryCallback);
            if (_moviesCategoryButton != null && _moviesCategoryCallback != null)
                _moviesCategoryButton.UnregisterCallback<ClickEvent>(_moviesCategoryCallback);
            if (_upgradesCategoryButton != null && _upgradesCategoryCallback != null)
                _upgradesCategoryButton.UnregisterCallback<ClickEvent>(_upgradesCategoryCallback);

            // Unregister all dynamically created movie buttons.
            foreach (var (button, callback) in _movieButtonCallbacks)
            {
                if (button != null && callback != null)
                    button.UnregisterCallback<ClickEvent>(callback);
            }
            _movieButtonCallbacks.Clear();

            if (_resumeButton != null) _resumeButton.clicked -= ResumeGame;
            if (_restartButton != null) _restartButton.clicked -= RestartGame;
            if (_settingsButton != null) _settingsButton.clicked -= OpenSettingsMenu;
            if (_exitButton != null) _exitButton.clicked -= ExitToMainMenu;
            if (_settingsBackButton != null) _settingsBackButton.clicked -= CloseSettingsMenu;
            if (_gameoverRestartButton != null) _gameoverRestartButton.clicked -= RestartGame;
            if (_gameoverExitButton != null) _gameoverExitButton.clicked -= ExitToMainMenu;

            EconomyManager.OnBalanceChanged -= HandleBalanceChanged;
            GameManager.OnCinemaRatingChanged -= HandleRatingChanged;
            ScheduleManager.OnShowStarted -= HandleShowStarted;
            ScheduleManager.OnShowEnded -= HandleShowEnded;
            ScheduleManager.OnCleanlinessChanged -= HandleCleanlinessChanged;
            StaffManager.OnStaffHired -= HandleStaffHired;
            Customer.OnTicketPurchased -= HandleTicketPurchased;
            EventManager.OnEventTriggered -= HandleEventTriggered;
            EventManager.OnEventResolved -= HandleEventResolved;
            EventManager.OnEventExpired -= HandleEventExpired;
            GameManager.OnGameOver -= HandleGameOver;
            FlyCameraController.OnCursorLockChanged -= HandleCursorLockChanged;
        }

        private void OnRootPointerDown(PointerDownEvent evt)
        {
            string target = evt.target != null ? evt.target.ToString() : "null";
            if (evt.target is Button b)
                Debug.Log($"[HUD] PointerDown target=Button text='{b.text}' enabledSelf={b.enabledSelf} focusable={b.focusable}");
            else
                Debug.Log($"[HUD] PointerDown target={target}");
        }

        private void OnRootPointerUp(PointerUpEvent evt)
        {
            string target = evt.target != null ? evt.target.ToString() : "null";
            if (evt.target is Button b)
                Debug.Log($"[HUD] PointerUp target=Button text='{b.text}' enabledSelf={b.enabledSelf} focusable={b.focusable}");
            else
                Debug.Log($"[HUD] PointerUp target={target}");
        }

        private void Start()
        {
            _isPaused = false;
            _isGameOver = false;

            string activeSceneName = SceneManager.GetActiveScene().name;
            bool isMenuScene = activeSceneName.Equals("MainMenu", StringComparison.OrdinalIgnoreCase);

            if (isMenuScene)
            {
                Time.timeScale = 0f;
                _isGameStarted = false;

                if (_mainMenuPanel != null) _mainMenuPanel.style.display = DisplayStyle.Flex;
                if (_gameplayHud != null) _gameplayHud.style.display = DisplayStyle.None;
                if (_pauseOverlay != null) _pauseOverlay.style.display = DisplayStyle.None;
                if (_settingsOverlay != null) _settingsOverlay.style.display = DisplayStyle.None;
                if (_gameoverOverlay != null) _gameoverOverlay.style.display = DisplayStyle.None;

                var cam = FindFirstObjectByType<FlyCameraController>();
                if (cam != null) cam.SetState(FlyCameraController.CameraState.MainMenu);
            }
            else
            {
                Time.timeScale = 1f;
                _isGameStarted = true;

                if (_mainMenuPanel != null) _mainMenuPanel.style.display = DisplayStyle.None;
                if (_gameplayHud != null) _gameplayHud.style.display = DisplayStyle.Flex;
                if (_pauseOverlay != null) _pauseOverlay.style.display = DisplayStyle.None;
                if (_settingsOverlay != null) _settingsOverlay.style.display = DisplayStyle.None;
                if (_gameoverOverlay != null) _gameoverOverlay.style.display = DisplayStyle.None;

                var cam = FindFirstObjectByType<FlyCameraController>();
                if (cam != null) cam.SetState(FlyCameraController.CameraState.FlyMode);
            }

            var gm = GameManager.Instance;
            if (gm != null)
            {
                Debug.Log("[HUD] Start(): GameManager found; populating UI + stats.");
                PopulateMovieButtons();

                HandleBalanceChanged(gm.Economy.Balance);
                HandleRatingChanged(gm.CinemaRating);
                HandleCleanlinessChanged(gm.Schedule.HallCleanliness);
                if (gm.Schedule.CurrentMovie != null) HandleShowStarted(gm.Schedule.CurrentMovie);
                else HandleShowEnded(null);
                UpdateStaffCount(gm.Staff.ActiveStaff.Count);
            }
        }

        private void Update()
        {
            if (!_sanityLogged)
            {
                _sanityLogged = true;

                string mainMenuDisplay = _mainMenuPanel != null ? _mainMenuPanel.style.display.ToString() : "null";
                string gameplayHudDisplay = _gameplayHud != null ? _gameplayHud.style.display.ToString() : "null";
                string pauseDisplay = _pauseOverlay != null ? _pauseOverlay.style.display.ToString() : "null";
                string settingsDisplay = _settingsOverlay != null ? _settingsOverlay.style.display.ToString() : "null";

                Debug.Log($"[HUD] Sanity: timeScale={Time.timeScale} mainMenu={mainMenuDisplay} gameplayHud={gameplayHudDisplay} pauseOverlay={pauseDisplay} settingsOverlay={settingsDisplay}");
                if (_hireCashierButton != null)
                    Debug.Log($"[HUD] Sanity: hire-cashier enabledSelf={_hireCashierButton.enabledSelf} focusable={_hireCashierButton.focusable}");
            }

            // Handle Pause Toggle Shortcut
            if (_isGameStarted && !_isGameOver)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    if (_isPaused)
                    {
                        ResumeGame();
                    }
                    else
                    {
                        PauseGame();
                    }
                }
            }
        }

        private void PopulateMovieButtons()
        {
            if (_movieButtonsContainer == null) return;

            // Unregister and clear previously registered movie button callbacks
            foreach (var (button, callback) in _movieButtonCallbacks)
            {
                if (button != null && callback != null)
                    button.UnregisterCallback<ClickEvent>(callback);
            }
            _movieButtonCallbacks.Clear();
            _movieButtonsContainer.Clear();

            var gm = GameManager.Instance;
            if (gm == null || gm.Schedule == null) return;

            foreach (var movie in gm.Schedule.AvailableMovies)
            {
                if (movie == null) continue;

                float hype = gm.Schedule.GetGenreHype(movie.genre);
                string labelText = $"{movie.title}\nLic: ${movie.licensingCost:F0} | Hype: {hype * 100f:F0}%";
                var button = new Button { text = labelText };
                button.AddToClassList("action-button");
                EventCallback<ClickEvent> callback = _ => TryScheduleMovie(movie);
                button.RegisterCallback<ClickEvent>(callback);
                _movieButtonsContainer.Add(button);
                _movieButtonCallbacks.Add((button, callback));
            }
        }

        private void TryHireStaff(StaffRole role)
        {
            Debug.Log($"[HUD] TryHireStaff called for {role}");
            var gm = GameManager.Instance;
            if (gm == null)  { Debug.LogError("[HUD] GameManager.Instance is NULL"); return; }
            if (gm.Staff == null) { Debug.LogError("[HUD] gm.Staff is NULL"); return; }
            bool result = gm.Staff.TryHire(role);
            Debug.Log($"[HUD] TryHire({role}) returned {result}");
        }

        private void TryPurchaseUpgrade(UpgradeType type)
        {
            Debug.Log($"[HUD] TryPurchaseUpgrade called for {type}");
            var gm = GameManager.Instance;
            if (gm == null)     { Debug.LogError("[HUD] GameManager.Instance is NULL"); return; }
            if (gm.Economy == null) { Debug.LogError("[HUD] gm.Economy is NULL"); return; }
            gm.Economy.TryPurchaseUpgrade(type);
            HandleBalanceChanged(gm.Economy.Balance);
        }

        private void TryScheduleMovie(MovieData movie)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Schedule == null) return;

            gm.Schedule.TryScheduleShow(movie);
        }

        private void HandleStartClicked()
        {
            Debug.Log("[HUD] HandleStartClicked()");
            _isGameStarted = true;
            Time.timeScale = 1f;

            if (Application.CanStreamedLevelBeLoaded("Demo"))
            {
                SceneManager.LoadScene("Demo");
            }
            else if (Application.CanStreamedLevelBeLoaded("Game"))
            {
                SceneManager.LoadScene("Game");
            }
            else
            {
                if (_mainMenuPanel != null) _mainMenuPanel.style.display = DisplayStyle.None;
                if (_gameplayHud != null) _gameplayHud.style.display = DisplayStyle.Flex;

                var cam = FindFirstObjectByType<FlyCameraController>();
                if (cam != null)
                {
                    cam.SetState(FlyCameraController.CameraState.FlyMode);
                }
            }
        }

        private void HandleQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// Accordion toggle: clicking the open category closes it; clicking a
        /// different category closes the current one and opens the new one.
        /// </summary>
        private void ToggleCategory(ManagementCategory category)
        {
            _openCategory = (_openCategory == category) ? ManagementCategory.None : category;
            ApplyCategoryState();
        }

        /// <summary>
        /// Collapses every category panel. Called when the cursor re-locks so
        /// the UI returns to its resting state when the player releases Alt.
        /// </summary>
        private void CollapseAllCategories()
        {
            if (_openCategory == ManagementCategory.None) return;
            _openCategory = ManagementCategory.None;
            ApplyCategoryState();
        }

        /// <summary>
        /// Subscribed to FlyCameraController.OnCursorLockChanged. When the
        /// cursor locks (Alt released in FlyMode) any open category panel
        /// collapses so the HUD doesn't dangle visible options behind a
        /// locked cursor.
        /// </summary>
        private void HandleCursorLockChanged(bool isLocked)
        {
            if (isLocked) CollapseAllCategories();
        }

        /// <summary>
        /// Pushes the current <see cref="_openCategory"/> into the UI:
        /// shows/hides each content panel and updates the disclosure glyph
        /// (▸/▾) + expanded styling on the category buttons.
        /// </summary>
        private void ApplyCategoryState()
        {
            SetCategoryOpen(ManagementCategory.Staff,
                _staffCategoryButton, _staffCategoryContent, StaffCategoryLabel);
            SetCategoryOpen(ManagementCategory.Movies,
                _moviesCategoryButton, _moviesCategoryContent, MoviesCategoryLabel);
            SetCategoryOpen(ManagementCategory.Upgrades,
                _upgradesCategoryButton, _upgradesCategoryContent, UpgradesCategoryLabel);
        }

        private void SetCategoryOpen(ManagementCategory category, Button header, VisualElement content, string label)
        {
            bool isOpen = _openCategory == category;
            if (content != null)
                content.style.display = isOpen ? DisplayStyle.Flex : DisplayStyle.None;
            if (header != null)
            {
                header.text = (isOpen ? CategoryOpenGlyph : CategoryClosedGlyph) + label;
                if (isOpen) header.AddToClassList("category-button--expanded");
                else        header.RemoveFromClassList("category-button--expanded");
            }
        }

        private void PauseGame()
        {
            _isPaused = true;
            Time.timeScale = 0f;

            if (_pauseOverlay != null) _pauseOverlay.style.display = DisplayStyle.Flex;

            var cam = FindFirstObjectByType<FlyCameraController>();
            if (cam != null)
                cam.SetState(FlyCameraController.CameraState.CursorFree);
        }

        private void ResumeGame()
        {
            _isPaused = false;
            Time.timeScale = 1f;

            if (_pauseOverlay != null) _pauseOverlay.style.display = DisplayStyle.None;
            if (_settingsOverlay != null) _settingsOverlay.style.display = DisplayStyle.None;

            var cam = FindFirstObjectByType<FlyCameraController>();
            if (cam != null)
            {
                cam.SetState(FlyCameraController.CameraState.FlyMode);
            }
        }

        private void OpenSettingsMenu()
        {
            if (_pauseOverlay != null) _pauseOverlay.style.display = DisplayStyle.None;
            if (_settingsOverlay != null) _settingsOverlay.style.display = DisplayStyle.Flex;
        }

        private void CloseSettingsMenu()
        {
            if (_settingsOverlay != null) _settingsOverlay.style.display = DisplayStyle.None;
            if (_pauseOverlay != null) _pauseOverlay.style.display = DisplayStyle.Flex;
        }

        private void RestartGame()
        {
            Time.timeScale = 1f;
            if (GameManager.Instance != null) Destroy(GameManager.Instance.gameObject);

            string activeScene = SceneManager.GetActiveScene().name;
            if (activeScene.Equals("MainMenu", StringComparison.OrdinalIgnoreCase))
            {
                if (Application.CanStreamedLevelBeLoaded("Demo")) SceneManager.LoadScene("Demo");
                else if (Application.CanStreamedLevelBeLoaded("Game")) SceneManager.LoadScene("Game");
                else SceneManager.LoadScene(activeScene);
            }
            else
            {
                SceneManager.LoadScene(activeScene);
            }
        }

        private void ExitToMainMenu()
        {
            Time.timeScale = 1f;
            if (GameManager.Instance != null) Destroy(GameManager.Instance.gameObject);

            if (Application.CanStreamedLevelBeLoaded("MainMenu"))
            {
                SceneManager.LoadScene("MainMenu");
            }
            else
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
        }

        private void HandleGameOver(string reason)
        {
            _isGameOver = true;
            Time.timeScale = 0f;

            if (_gameoverOverlay != null) _gameoverOverlay.style.display = DisplayStyle.Flex;
            if (_gameoverReasonLabel != null) _gameoverReasonLabel.text = reason;

            var cam = FindFirstObjectByType<FlyCameraController>();
            if (cam != null)
            {
                cam.SetState(FlyCameraController.CameraState.MainMenu);
            }
        }

        private void HandleBalanceChanged(float balance)
        {
            if (_balanceLabel != null) _balanceLabel.text = $"Balance: ${balance:F0}";

            UpdateInteractivity(balance);
        }

        private void UpdateInteractivity(float balance)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Staff == null) return;

            float cashierCost = gm.Staff.GetHireCost(StaffRole.Cashier);
            float janitorCost = gm.Staff.GetHireCost(StaffRole.Janitor);
            float guardCost = gm.Staff.GetHireCost(StaffRole.Guard);

            if (_hireCashierButton != null)
            {
                _hireCashierButton.text = $"Hire Cashier (${cashierCost:F0})";
                _hireCashierButton.SetEnabled(balance >= cashierCost);
            }
            if (_hireJanitorButton != null)
            {
                _hireJanitorButton.text = $"Hire Janitor (${janitorCost:F0})";
                _hireJanitorButton.SetEnabled(balance >= janitorCost);
            }
            if (_hireGuardButton != null)
            {
                _hireGuardButton.text = $"Hire Guard (${guardCost:F0})";
                _hireGuardButton.SetEnabled(balance >= guardCost);
            }

            if (gm.Economy == null) return;

            UpdateUpgradeButton(_buyPopcornButton, UpgradeType.PremiumPopcorn, balance);
            UpdateUpgradeButton(_buyCashierButton, UpgradeType.FasterCashier, balance);
            UpdateUpgradeButton(_buySeatsButton, UpgradeType.ComfySeats, balance);
            UpdateUpgradeButton(_buyMarketingButton, UpgradeType.Marketing, balance);
        }

        private void UpdateUpgradeButton(Button button, UpgradeType type, float balance)
        {
            if (button == null) return;

            var gm = GameManager.Instance;
            if (gm.Economy.HasUpgrade(type))
            {
                button.SetEnabled(false);
                button.text = $"{GetUpgradeDisplayName(type)} (Purchased)";
            }
            else
            {
                float cost = GetUpgradeCost(type);
                button.text = $"{GetUpgradeDisplayName(type)} (${cost:F0})";
                button.SetEnabled(balance >= cost);
            }
        }

        private float GetUpgradeCost(UpgradeType type)
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.Economy != null)
            {
                foreach (var up in gm.Economy.AvailableUpgrades)
                {
                    if (up.type == type) return up.cost;
                }
            }
            return 250f; // fallback cost
        }

        private string GetUpgradeDisplayName(UpgradeType type) => type switch
        {
            UpgradeType.PremiumPopcorn => "Premium Popcorn",
            UpgradeType.FasterCashier  => "Faster Cashier",
            UpgradeType.ComfySeats     => "Comfy Seats",
            UpgradeType.Marketing      => "Marketing Campaign",
            _ => "Upgrade"
        };

        private void HandleRatingChanged(float rating)
        {
            if (_ratingLabel != null) _ratingLabel.text = $"Rating: {rating:F1}%";
            if (_ratingBar != null) _ratingBar.value = rating;
        }

        private void HandleShowStarted(MovieData movie)
        {
            if (_nowPlayingLabel != null) _nowPlayingLabel.text = $"Now Playing: {movie.title}";
        }

        private void HandleShowEnded(MovieData movie)
        {
            if (_nowPlayingLabel != null) _nowPlayingLabel.text = "Now Playing: —";
        }

        private void HandleCleanlinessChanged(float cleanliness)
        {
            if (_cleanlinessBar != null) _cleanlinessBar.value = cleanliness;
        }

        private void HandleStaffHired(Staff.Staff staff)
        {
            if (GameManager.Instance != null && GameManager.Instance.Staff != null)
            {
                UpdateStaffCount(GameManager.Instance.Staff.ActiveStaff.Count);
            }
        }

        private void UpdateStaffCount(int count)
        {
            if (_staffCountLabel != null) _staffCountLabel.text = $"Staff: {count}";
        }

        private void HandleTicketPurchased(Customer customer, float ticketPrice)
        {
            if (_lastTicketLabel != null) _lastTicketLabel.text = $"+${ticketPrice:F0} ticket";
        }

        private void HandleEventTriggered(GameEvent evt)
        {
            if (notificationTemplate == null || _notificationContainer == null) return;

            VisualElement popup = notificationTemplate.Instantiate();
            var titleText = popup.Q<Label>("event-title");
            var descText = popup.Q<Label>("event-desc");
            if (titleText != null) titleText.text = evt.Title;
            if (descText != null) descText.text = evt.Description;

            popup.userData = evt;

            var popupRoot = popup.Q<VisualElement>("event-popup-root");
            if (popupRoot != null && evt.Type == GameEventType.VIPVisit)
            {
                popupRoot.AddToClassList("notification-popup-vip");
            }

            _notificationContainer.Add(popup);
        }

        private void HandleEventResolved(GameEvent evt)
        {
            RemoveEventPopup(evt);
        }

        private void HandleEventExpired(GameEvent evt)
        {
            RemoveEventPopup(evt);
        }

        private void RemoveEventPopup(GameEvent evt)
        {
            if (_notificationContainer == null) return;

            VisualElement toRemove = null;
            foreach (var child in _notificationContainer.Children())
            {
                if (child.userData == evt)
                {
                    toRemove = child;
                    break;
                }
            }

            if (toRemove != null)
            {
                _notificationContainer.Remove(toRemove);
            }
        }
    }
}
