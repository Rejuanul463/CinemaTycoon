using UnityEngine;

namespace CinemaTycoon.Schedule
{
    public enum MovieGenre { Action, Comedy, Drama, Horror, SciFi, Family }

    [CreateAssetMenu(fileName = "MovieData", menuName = "CinemaTycoon/Movie")]
    public class MovieData : ScriptableObject
    {
        [Header("Identity")]
        public string title = "New Movie";
        [TextArea] public string description;
        public MovieGenre genre;

        [Header("Runtime & Economy")]
        [Tooltip("In-game seconds the show lasts.")]
        public float duration = 60f;
        public float baseTicketPrice = 12f;

        [Header("Audience")]
        [Range(0f, 1f)] public float popularity = 0.5f;
    }
}
