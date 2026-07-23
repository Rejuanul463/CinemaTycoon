using System.Collections;
using UnityEngine;

namespace CinemaTycoon.Events
{
    /// <summary>
    /// Visual representation of a Spill event on the floor. Instantiated by
    /// <see cref="EventManager"/> when a spill is triggered, destroyed by the
    /// EventManager when the spill is resolved (cleaned) or expires.
    ///
    /// The script owns fade-in / fade-out animations only — placement and
    /// lifetime are driven externally. Attach this to a prefab whose mesh
    /// lies flat (typically a quad rotated -90° on X) with a dirt/soda
    /// texture; the script will modulate its renderer's material color alpha.
    ///
    /// Lifecycle:
    ///   EventManager.TriggerSpill()  → Instantiate prefab, attach to GameEvent
    ///   EventManager.ResolveEvent()  → calls FadeOutAndDestroy() on resolve
    ///   EventManager.ExpireEvent()   → also calls FadeOutAndDestroy()
    /// </summary>
    public class SpillDecal : MonoBehaviour
    {
        [Header("Animation")]
        [Tooltip("Seconds to fade the decal from transparent → fully opaque on spawn.")]
        [SerializeField] private float fadeInDuration = 0.4f;
        [Tooltip("Seconds to fade the decal back to transparent before it is destroyed.")]
        [SerializeField] private float fadeOutDuration = 0.6f;
        [Tooltip("If true, the decal auto-aligns to the world up-vector on spawn " +
                 "so it stays glued to the floor even if the assigned transform is tilted.")]
        [SerializeField] private bool alignToFloor = true;

        private Renderer _renderer;
        private Material _materialInstance; // runtime-instanced so we don't mutate the shared asset
        private Vector3 _originalScale;     // captured in Awake so prefab scale is preserved across shrink
        private bool _isBeingCleaned;       // true while a janitor is actively scrubbing the spill
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");   // URP Lit
        private static readonly int ColorId     = Shader.PropertyToID("_Color");       // Built-in / Unlit
        private Coroutine _runningFade;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            if (_renderer == null)
            {
                Debug.LogWarning("[SpillDecal] No Renderer found — fade animation will be skipped.", this);
                return;
            }

            // Capture the prefab's authored scale so SetCleaningProgress can
            // shrink relative to it (1.0 = full size, 0.05 = tiny residue).
            _originalScale = transform.localScale;

            // Instantiate the material so multiple spills / cleanup don't share state.
            _materialInstance = _renderer.material;
            SetAlpha(0f);

            if (alignToFloor)
            {
                // Keep the decal glued to the floor regardless of the spawn
                // transform's rotation. We lock yaw so the texture isn't spun
                // randomly; pitch is forced to -90° (lying flat).
                Vector3 euler = transform.eulerAngles;
                transform.rotation = Quaternion.Euler(90f, euler.y, 0f);
            }
        }

        private void OnEnable()
        {
            if (_runningFade != null) StopCoroutine(_runningFade);
            _runningFade = StartCoroutine(FadeIn());
        }

        /// <summary>
        /// Called by EventManager when the spill is cleaned or expires. Plays the
        /// fade-out animation and then destroys the GameObject.
        /// </summary>
        public void FadeOutAndDestroy()
        {
            if (_runningFade != null) StopCoroutine(_runningFade);
            _runningFade = StartCoroutine(FadeOutThenDestroy());
        }

        /// <summary>True once a janitor has reached this decal and started cleaning.</summary>
        public bool IsBeingCleaned => _isBeingCleaned;

        /// <summary>
        /// Called by the assigned janitor's <see cref="CinemaTycoon.Staff.Staff"/>
        /// when it arrives at the spill and starts the Work timer. Stops the
        /// own fade-in animation (the spill is fully visible now), forces the
        /// alpha back to opaque so the subsequent shrink is the dominant
        /// visual, and flips the cleaning flag so <see cref="SetCleaningProgress"/>
        /// starts taking effect.
        /// </summary>
        public void BeginCleaning()
        {
            _isBeingCleaned = true;
            if (_runningFade != null) { StopCoroutine(_runningFade); _runningFade = null; }
            SetAlpha(1f);
        }

        /// <summary>
        /// Drive the visual cleanup progress. Called every frame the assigned
        /// janitor is working (i.e. IsBusy and the task is alive). Shrinks the
        /// decal from full size to a small residue, and gently fades the alpha
        /// so the spill visibly "dries up" as it's mopped. The final residue
        /// (at progress = 1) is intentionally still ~5% visible — the last
        /// <see cref="FadeOutAndDestroy"/> call from EventManager.ResolveEvent
        /// handles the actual disappearance.
        /// </summary>
        /// <param name="t01">0 = freshly spilled, 1 = fully scrubbed.</param>
        public void SetCleaningProgress(float t01)
        {
            if (!_isBeingCleaned) return;
            t01 = Mathf.Clamp01(t01);

            // Scale: 1.0 → 0.05 (leaves a tiny visible residue, fade-out finishes the job).
            float scaleFactor = Mathf.Lerp(1f, 0.05f, t01);
            transform.localScale = _originalScale * scaleFactor;

            // Alpha: 1.0 → 0.4 (extra feedback — the spill gets "thinner" as it dries).
            SetAlpha(Mathf.Lerp(1f, 0.4f, t01));
        }

        private IEnumerator FadeIn()
        {
            yield return Fade(0f, 1f, fadeInDuration);
            _runningFade = null;
        }

        private IEnumerator FadeOutThenDestroy()
        {
            yield return Fade(1f, 0f, fadeOutDuration);
            _runningFade = null;
            Destroy(gameObject);
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            if (_materialInstance == null || duration <= 0f)
            {
                SetAlpha(to);
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                SetAlpha(Mathf.Lerp(from, to, t / duration));
                yield return null;
            }
            SetAlpha(to);
        }

        private void SetAlpha(float a)
        {
            if (_materialInstance == null) return;

            // URP Lit uses _BaseColor; built-in / Unlit use _Color. Try both so
            // the decal works regardless of which shader the prefab uses.
            if (_materialInstance.HasProperty(BaseColorId))
            {
                Color c = _materialInstance.GetColor(BaseColorId);
                c.a = a;
                _materialInstance.SetColor(BaseColorId, c);
            }
            if (_materialInstance.HasProperty(ColorId))
            {
                Color c = _materialInstance.GetColor(ColorId);
                c.a = a;
                _materialInstance.SetColor(ColorId, c);
            }
        }

        private void OnDestroy()
        {
            // Clean up the instanced material so we don't leak it in the editor.
            if (_materialInstance != null)
            {
                if (Application.isPlaying) Destroy(_materialInstance);
                else DestroyImmediate(_materialInstance);
            }
        }
    }
}
