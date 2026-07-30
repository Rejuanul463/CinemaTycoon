using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using CinemaTycoon.Economy;
using CinemaTycoon.Customers;
using CinemaTycoon.Staff;
using CinemaTycoon.Schedule;
using CinemaTycoon.Events;

namespace CinemaTycoon.Core
{
    /// <summary>
    /// Central coordinator singleton. Persists across scene reloads and
    /// exposes the subsystem managers so cross-system callers can reach
    /// them through a single, well-known entry point.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        #region Safe Singleton
        private static GameManager _instance;
        public static GameManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindFirstObjectByType<GameManager>();
                return _instance;
            }
        }
        #endregion

        [Header("Manager References (assign in Inspector)")]
        [SerializeField] private EconomyManager economyManager;
        [SerializeField] private CustomerSpawnManager customerSpawnManager;
        [SerializeField] private StaffManager staffManager;
        [SerializeField] private ScheduleManager scheduleManager;
        [SerializeField] private EventManager eventManager;

        [Header("Cinema Rating")]
        [SerializeField, Range(0f, 100f)] private float startingCinemaRating = 55f;

        [Header("Audio")]
        [Tooltip("Optional AudioSource used for looping background music. One is added automatically when empty.")]
        [SerializeField] private AudioSource backgroundMusicSource;
        [Tooltip("Assign the music clip to play while the game is running.")]
        [SerializeField] private AudioClip backgroundMusic;
        [Tooltip("One-shot sound used for UI button interactions.")]
        [SerializeField] private AudioClip uiClickSound;
        [Tooltip("Assign the Cinema Tycoon Audio Mixer asset here.")]
        [SerializeField] private AudioMixer audioMixer;
        [Tooltip("Assign the Music group from the Cinema Tycoon Audio Mixer.")]
        [SerializeField] private AudioMixerGroup musicMixerGroup;
        [Tooltip("Assign the SFX group from the Cinema Tycoon Audio Mixer.")]
        [SerializeField] private AudioMixerGroup sfxMixerGroup;
        [SerializeField] private string masterVolumeParameter = "MasterVolume";
        [SerializeField] private string musicVolumeParameter = "MusicVolume";
        [SerializeField] private string sfxVolumeParameter = "SfxVolume";
        [Range(0f, 1f)] [SerializeField] private float masterVolume = 1f;
        [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.7f;
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;
        private AudioSource _sfxSource;

        public EconomyManager Economy => economyManager;
        public CustomerSpawnManager Spawner => customerSpawnManager;
        public StaffManager Staff => staffManager;
        public ScheduleManager Schedule => scheduleManager;
        public EventManager Events => eventManager;
        public float CinemaRating => _cinemaRating;
        public float MasterVolume => masterVolume;
        public float MusicVolume => musicVolume;
        public float SfxVolume => sfxVolume;

        public static event Action<float> OnCinemaRatingChanged;
        public static event Action<string> OnGameOver;
        public static event Action OnGameWon;

        private float _cinemaRating;
        private bool _gameOver;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            PreloadUiAudio();
            ConfigureAudio();
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void Start()
        {
            _cinemaRating = startingCinemaRating;
            OnCinemaRatingChanged?.Invoke(_cinemaRating);

            economyManager.Initialize();
            staffManager.Initialize();
            scheduleManager.Initialize();
            customerSpawnManager.Initialize();
            eventManager.Initialize();
        }

        /// <summary>Sets the mixer Master group volume, with an AudioListener fallback.</summary>
        public void SetMasterVolume(float value)
        {
            masterVolume = Mathf.Clamp01(value);
            ApplyAudioVolumes();
        }

        /// <summary>Sets the relative volume of the looping background music.</summary>
        public void SetMusicVolume(float value)
        {
            musicVolume = Mathf.Clamp01(value);
            ApplyAudioVolumes();
        }

        /// <summary>Sets the volume used by <see cref="PlaySfx"/>.</summary>
        public void SetSfxVolume(float value)
        {
            sfxVolume = Mathf.Clamp01(value);
            ApplyAudioVolumes();
        }

        /// <summary>Plays a one-shot sound effect using the configured SFX volume.</summary>
        public void PlaySfx(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null) return;
            if (_sfxSource == null) ConfigureAudio();
            _sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale) * sfxVolume);
        }

        /// <summary>Plays the shared UI interaction sound, when assigned.</summary>
        public void PlayUiClick() => PlaySfx(uiClickSound);

        private void PreloadUiAudio()
        {
            if (uiClickSound != null && uiClickSound.loadState != AudioDataLoadState.Loaded)
                uiClickSound.LoadAudioData();
        }

        private void ConfigureAudio()
        {
            if (backgroundMusicSource == null)
                backgroundMusicSource = GetComponent<AudioSource>();
            if (backgroundMusicSource == null)
                backgroundMusicSource = gameObject.AddComponent<AudioSource>();

            if (_sfxSource == null)
            {
                var sfxObject = new GameObject("SFX Audio Source");
                sfxObject.transform.SetParent(transform, false);
                _sfxSource = sfxObject.AddComponent<AudioSource>();
            }

            backgroundMusicSource.loop = true;
            backgroundMusicSource.playOnAwake = false;
            if (musicMixerGroup != null)
                backgroundMusicSource.outputAudioMixerGroup = musicMixerGroup;
            if (sfxMixerGroup != null)
                _sfxSource.outputAudioMixerGroup = sfxMixerGroup;
            if (backgroundMusic != null)
                backgroundMusicSource.clip = backgroundMusic;

            ApplyAudioVolumes();
            if (backgroundMusicSource.clip != null && !backgroundMusicSource.isPlaying)
                backgroundMusicSource.Play();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RouteSceneAudioSources();
        }

        private void RouteSceneAudioSources()
        {
            if (sfxMixerGroup == null) return;

            foreach (var source in FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (source == backgroundMusicSource)
                    source.outputAudioMixerGroup = musicMixerGroup;
                else if (source != _sfxSource)
                    source.outputAudioMixerGroup = sfxMixerGroup;
            }
        }

        private void ApplyAudioVolumes()
        {
            bool masterIsRouted = SetMixerVolume(masterVolumeParameter, masterVolume);
            bool musicIsRouted = SetMixerVolume(musicVolumeParameter, musicVolume);
            SetMixerVolume(sfxVolumeParameter, sfxVolume);

            // Keep audio working before the mixer fields are assigned, and if an
            // exposed parameter was accidentally renamed in the mixer asset.
            if (!masterIsRouted)
                AudioListener.volume = masterVolume;
            if (!musicIsRouted && backgroundMusicSource != null)
                backgroundMusicSource.volume = musicVolume;
        }

        private bool SetMixerVolume(string parameterName, float linearValue)
        {
            return audioMixer != null && !string.IsNullOrWhiteSpace(parameterName)
                && audioMixer.SetFloat(parameterName, LinearToDecibels(linearValue));
        }

        private static float LinearToDecibels(float value)
        {
            return value <= 0.0001f ? -80f : Mathf.Log10(value) * 20f;
        }

        /// <summary>
        /// Adjust the cinema-wide satisfaction rating. Customers, events,
        /// and schedule penalties all funnel through here.
        /// </summary>
        public void AdjustCinemaRating(float delta, string reason = "")
        {
            if (_gameOver) return;
            _cinemaRating = Mathf.Clamp(_cinemaRating + delta, 0f, 100f);
            OnCinemaRatingChanged?.Invoke(_cinemaRating);

            if (_cinemaRating <= 0f)
                TriggerGameOver("Audience satisfaction hit zero. The cinema closed.");
            else if (_cinemaRating >= 100f)
                TriggerGameWon();
        }

        public void TriggerGameWon()
        {
            if (_gameOver) return;
            _gameOver = true;
            Time.timeScale = 0f;
            OnGameWon?.Invoke();
        }

        public void TriggerGameOver(string reason)
        {
            if (_gameOver) return;
            _gameOver = true;
            Time.timeScale = 0f;
            OnGameOver?.Invoke(reason);
        }
    }
}
