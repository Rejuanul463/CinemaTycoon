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

        // State trackers
        private bool _isGameStarted = false;
        private bool _isPaused = false;
        private bool _isGameOver = false;

        private void OnEnable()
        {
            _root = GetComponent<UIDocument>().rootVisualElement;

            if (_root == null)
            {
                Debug.LogError("[UIToolkitHUDBindings] Root visual element is null. Check if UIDocument is initialized.");
                return;
            }

            // Apply USS stylesheet programmatically (avoids UXML <Style> tag parsing issues)
            if (hudStyleSheet != null)
            {
                _root.styleSheets.Add(hudStyleSheet);
            }
            else
            {
                Debug.LogWarning("[UIToolkitHUDBindings] hudStyleSheet is not assigned in the Inspector. UI will be unstyled.");
            }

            // Query Main Menu panels & buttons
            _mainMenuPanel = _root.Q<VisualElement>("main-menu-panel");
            _gameplayHud = _root.Q<VisualElement>("gameplay-hud");
            _startButton = _root.Q<Button>("start-button");
            _quitButton = _root.Q<Button>("quit-button");

            // Wire Main Menu button clicks
            if (_startButton != null) _startButton.clicked += HandleStartClicked;
            if (_quitButton != null) _quitButton.clicked += HandleQuitClicked;

            // Query Sidebar buttons
            _hireCashierButton = _root.Q<Button>("hire-cashier-button");
            _hireJanitorButton = _root.Q<Button>("hire-janitor-button");
            _hireGuardButton = _root.Q<Button>("hire-guard-button");
            _buyPopcornButton = _root.Q<Button>("buy-popcorn-button");
            _buyCashierButton = _root.Q<Button>("buy-cashier-button");
            _buySeatsButton = _root.Q<Button>("buy-seats-button");
            _buyMarketingButton = _root.Q<Button>("buy-marketing-button");
            _movieButtonsContainer = _root.Q<VisualElement>("movie-buttons-container");

            // Wire Sidebar clicks
            if (_hireCashierButton != null) _hireCashierButton.clicked += () => TryHireStaff(StaffRole.Cashier);
            if (_hireJanitorButton != null) _hireJanitorButton.clicked += () => TryHireStaff(StaffRole.Janitor);
            if (_hireGuardButton != null) _hireGuardButton.clicked += () => TryHireStaff(StaffRole.Guard);
            if (_buyPopcornButton != null) _buyPopcornButton.clicked += () => TryPurchaseUpgrade(UpgradeType.PremiumPopcorn);
            if (_buyCashierButton != null) _buyCashierButton.clicked += () => TryPurchaseUpgrade(UpgradeType.FasterCashier);
            if (_buySeatsButton != null) _buySeatsButton.clicked += () => TryPurchaseUpgrade(UpgradeType.ComfySeats);
            if (_buyMarketingButton != null) _buyMarketingButton.clicked += () => TryPurchaseUpgrade(UpgradeType.Marketing);

            // Query Fullscreen Overlays
            _pauseOverlay = _root.Q<VisualElement>("pause-overlay");
            _settingsOverlay = _root.Q<VisualElement>("settings-overlay");
            _gameoverOverlay = _root.Q<VisualElement>("gameover-overlay");

            // Query Pause Overlay buttons
            _resumeButton = _root.Q<Button>("resume-button");
            _restartButton = _root.Q<Button>("restart-button");
            _settingsButton = _root.Q<Button>("settings-button");
            _exitButton = _root.Q<Button>("exit-button");

            // Wire Pause click listeners
            if (_resumeButton != null) _resumeButton.clicked += ResumeGame;
            if (_restartButton != null) _restartButton.clicked += RestartGame;
            if (_settingsButton != null) _settingsButton.clicked += OpenSettingsMenu;
            if (_exitButton != null) _exitButton.clicked += ExitToMainMenu;

            // Query Settings buttons
            _settingsBackButton = _root.Q<Button>("settings-back-button");
            if (_settingsBackButton != null) _settingsBackButton.clicked += CloseSettingsMenu;

            // Query Game Over elements
            _gameoverReasonLabel = _root.Q<Label>("gameover-reason-label");
            _gameoverRestartButton = _root.Q<Button>("gameover-restart-button");
            _gameoverExitButton = _root.Q<Button>("gameover-exit-button");

            // Wire Game Over click listeners
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

            // Subscribe to static core events
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
        }

        private void OnDisable()
        {
            if (_startButton != null) _startButton.clicked -= HandleStartClicked;
            if (_quitButton != null) _quitButton.clicked -= HandleQuitClicked;

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
        }

        private void Start()
        {
            // Initial Menu Pause State
            Time.timeScale = 0f;
            _isGameStarted = false;
            _isPaused = false;
            _isGameOver = false;

            if (_mainMenuPanel != null) _mainMenuPanel.style.display = DisplayStyle.Flex;
            if (_gameplayHud != null) _gameplayHud.style.display = DisplayStyle.None;
            if (_pauseOverlay != null) _pauseOverlay.style.display = DisplayStyle.None;
            if (_settingsOverlay != null) _settingsOverlay.style.display = DisplayStyle.None;
            if (_gameoverOverlay != null) _gameoverOverlay.style.display = DisplayStyle.None;

            // Set camera to floating menu mode
            var cam = FindFirstObjectByType<FlyCameraController>();
            if (cam != null)
            {
                cam.SetState(FlyCameraController.CameraState.MainMenu);
            }

            var gm = GameManager.Instance;
            if (gm != null)
            {
                // Populate dynamic movie buttons
                PopulateMovieButtons();

                // Pull initial values for stats
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
            _movieButtonsContainer.Clear();

            var gm = GameManager.Instance;
            if (gm == null || gm.Schedule == null) return;

            foreach (var movie in gm.Schedule.AvailableMovies)
            {
                if (movie == null) continue;

                var button = new Button { text = $"{movie.title} (${movie.baseTicketPrice})" };
                button.AddToClassList("action-button");
                button.clicked += () => TryScheduleMovie(movie);
                _movieButtonsContainer.Add(button);
            }
        }

        private void TryHireStaff(StaffRole role)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Staff == null) return;

            gm.Staff.TryHire(role);
        }

        private void TryPurchaseUpgrade(UpgradeType type)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Economy == null) return;

            gm.Economy.TryPurchaseUpgrade(type);
            // Refresh buttons to reflect purchased status
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
            _isGameStarted = true;
            Time.timeScale = 1f;

            if (_mainMenuPanel != null) _mainMenuPanel.style.display = DisplayStyle.None;
            if (_gameplayHud != null) _gameplayHud.style.display = DisplayStyle.Flex;

            // Lock cursor and start flight
            var cam = FindFirstObjectByType<FlyCameraController>();
            if (cam != null)
            {
                cam.SetState(FlyCameraController.CameraState.FlyMode);
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

        private void PauseGame()
        {
            _isPaused = true;
            Time.timeScale = 0f;

            if (_pauseOverlay != null) _pauseOverlay.style.display = DisplayStyle.Flex;

            // Freeze camera in place and unlock cursor for UI interaction
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

            // Lock cursor and return to flying
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
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void ExitToMainMenu()
        {
            Time.timeScale = 1f;
            if (GameManager.Instance != null) Destroy(GameManager.Instance.gameObject);
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void HandleGameOver(string reason)
        {
            _isGameOver = true;
            Time.timeScale = 0f;

            if (_gameoverOverlay != null) _gameoverOverlay.style.display = DisplayStyle.Flex;
            if (_gameoverReasonLabel != null) _gameoverReasonLabel.text = reason;

            // Unlock cursor for menu clicks
            var cam = FindFirstObjectByType<FlyCameraController>();
            if (cam != null)
            {
                cam.SetState(FlyCameraController.CameraState.MainMenu);
            }
        }

        private void HandleBalanceChanged(float balance)
        {
            if (_balanceLabel != null) _balanceLabel.text = $"Balance: ${balance:F0}";

            // Update button interactivity based on affordability
            UpdateInteractivity(balance);
        }

        private void UpdateInteractivity(float balance)
        {
            // Staff Costs fallback constants (hiring Cashier is $150, Janitor $100, Guard $100)
            if (_hireCashierButton != null) _hireCashierButton.SetEnabled(balance >= 150f);
            if (_hireJanitorButton != null) _hireJanitorButton.SetEnabled(balance >= 100f);
            if (_hireGuardButton != null) _hireGuardButton.SetEnabled(balance >= 100f);

            var gm = GameManager.Instance;
            if (gm == null || gm.Economy == null) return;

            // Upgrades affordability & purchased verification
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

            // Instantiate UXML template for the event popup
            VisualElement popup = notificationTemplate.Instantiate();
            var titleText = popup.Q<Label>("event-title");
            var descText = popup.Q<Label>("event-desc");
            if (titleText != null) titleText.text = evt.Title;
            if (descText != null) descText.text = evt.Description;

            // Associate the visual element with the event instance
            popup.userData = evt;

            // Apply VIP Visit styling if it is a VIPVisit event
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
