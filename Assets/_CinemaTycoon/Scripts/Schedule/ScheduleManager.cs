using System;
using System.Collections.Generic;
using UnityEngine;
using CinemaTycoon.Core;
using CinemaTycoon.Staff;

namespace CinemaTycoon.Schedule
{
    public class ScheduleManager : MonoBehaviour
    {
        [Header("Movies")]
        [SerializeField] private MovieData[] availableMovies;

        [Header("Hall State")]
        [SerializeField] private float cleanlinessThreshold = 50f;
        [SerializeField] private float dirtyStartPenalty = 10f;
        [SerializeField] private float decayPerSecondDuringShow = 5f;
        [SerializeField] private float passiveCleanPerSecond = 0.5f;

        private MovieData _currentMovie;
        private float _showStartGameTime;
        private float _scheduledStartTime = -1f;
        private bool _showActive;
        private float _hallCleanliness = 100f;

        public MovieData CurrentMovie => _currentMovie;
        public bool IsMoviePlaying => _showActive;
        public float CurrentTicketPrice => _currentMovie != null ? _currentMovie.baseTicketPrice : 0f;
        public float HallCleanliness => _hallCleanliness;
        public IReadOnlyList<MovieData> AvailableMovies => availableMovies;

        // True if a show is currently running OR scheduled to start soon —
        // customers use this to decide whether buying a ticket is worthwhile.
        public bool IsMoviePlayingOrImminent => _showActive || _scheduledStartTime > 0f;

        public static event Action<MovieData> OnShowStarted;
        public static event Action<MovieData> OnShowEnded;
        public static event Action<float> OnCleanlinessChanged;

        public void Initialize()
        {
            _hallCleanliness = 100f;
            OnCleanlinessChanged?.Invoke(_hallCleanliness);
        }

        private void Update()
        {
            if (_showActive)
            {
                _hallCleanliness = Mathf.Max(0f, _hallCleanliness - decayPerSecondDuringShow * Time.deltaTime);
                OnCleanlinessChanged?.Invoke(_hallCleanliness);

                if (Time.time - _showStartGameTime >= _currentMovie.duration)
                    EndShow();
            }
            else
            {
                // Passive regen when no show is running.
                if (_hallCleanliness < 100f)
                {
                    _hallCleanliness = Mathf.Min(100f, _hallCleanliness + passiveCleanPerSecond * Time.deltaTime);
                    OnCleanlinessChanged?.Invoke(_hallCleanliness);
                }

                // Auto-start a previously-scheduled show if its time has come.
                if (_scheduledStartTime > 0f && Time.time >= _scheduledStartTime)
                {
                    TryStartShow(_currentMovie);
                    _scheduledStartTime = -1f;
                }
            }
        }

        /// <summary>
        /// UI entry point: schedule a movie with optional delay. Returns false
        /// if a show is already running or the movie is null.
        /// </summary>
        public bool TryScheduleShow(MovieData movie, float delaySeconds = 0f)
        {
            if (movie == null || _showActive) return false;
            _currentMovie = movie;
            if (delaySeconds <= 0f) return TryStartShow(movie);
            _scheduledStartTime = Time.time + delaySeconds;
            return true;
        }

        /// <summary>
        /// Actually start the show. Validates the staffing gate (Cashier on duty)
        /// and applies a cleanliness penalty if the hall is dirty.
        /// </summary>
        public bool TryStartShow(MovieData movie)
        {
            if (movie == null || _showActive) return false;

            var gm = GameManager.Instance;
            if (gm == null || gm.Staff == null) return false;

            // Staffing gate: need a Cashier to sell tickets during this show.
            if (!gm.Staff.HasRoleOnDuty(StaffRole.Cashier))
            {
                Debug.Log("[Schedule] Cannot start show — no Cashier on duty.");
                return false;
            }

            if (_hallCleanliness < cleanlinessThreshold)
                gm.AdjustCinemaRating(-dirtyStartPenalty, "Show started in dirty hall");

            _currentMovie = movie;
            _showActive = true;
            _showStartGameTime = Time.time;
            OnShowStarted?.Invoke(movie);
            return true;
        }

        private void EndShow()
        {
            _showActive = false;
            var m = _currentMovie;
            _currentMovie = null;
            OnShowEnded?.Invoke(m);
        }

        /// <summary>Called by StaffManager when a Janitor cleans the hall.</summary>
        public void CleanHall(float amount)
        {
            _hallCleanliness = Mathf.Min(100f, _hallCleanliness + amount);
            OnCleanlinessChanged?.Invoke(_hallCleanliness);
        }
    }
}
