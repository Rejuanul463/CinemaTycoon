using UnityEngine;
using UnityEngine.SceneManagement;

namespace CinemaTycoon.Core
{
    /// <summary>
    /// Runtime sanitizer that automatically runs on scene load to:
    /// 1. Disable extra AudioListeners so only 1 active AudioListener exists in the scene (prevents console warnings).
    /// 2. Attach a dummy AnimationEventReceiver to all Animators missing a ReleasePopcorn/HoldPopcorn method receiver.
    /// </summary>
    public class SceneSanitizer : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSanitizeOnLoad()
        {
            SanitizeActiveScene();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SanitizeActiveScene();
        }

        public static void SanitizeActiveScene()
        {
            SanitizeAudioListeners();
            SanitizeAnimationEventReceivers();
        }

        private static void SanitizeAudioListeners()
        {
            var listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (listeners == null || listeners.Length <= 1) return;

            AudioListener primary = null;
            var mainCam = Camera.main;
            if (mainCam != null) primary = mainCam.GetComponent<AudioListener>();

            if (primary == null)
            {
                foreach (var l in listeners)
                {
                    if (l != null && l.gameObject.activeInHierarchy && l.enabled)
                    {
                        primary = l;
                        break;
                    }
                }
            }

            if (primary == null && listeners.Length > 0) primary = listeners[0];

            int disabledCount = 0;
            foreach (var l in listeners)
            {
                if (l != null && l != primary)
                {
                    l.enabled = false;
                    disabledCount++;
                }
            }

            if (disabledCount > 0)
            {
                Debug.Log($"[SceneSanitizer] Disabled {disabledCount} extra AudioListeners in scene '{SceneManager.GetActiveScene().name}'. Primary AudioListener: '{primary.gameObject.name}'.");
            }
        }

        private static void SanitizeAnimationEventReceivers()
        {
            var animators = Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (animators == null) return;

            int receiverCount = 0;
            foreach (var anim in animators)
            {
                if (anim == null) continue;

                var receivers = anim.GetComponents<AnimationEventReceiver>();
                if (receivers == null || receivers.Length == 0)
                {
                    bool hasReceiver = anim.GetComponent<Customers.Customer>() != null || anim.GetComponent<Padestarian>() != null;
                    if (!hasReceiver)
                    {
                        anim.gameObject.AddComponent<AnimationEventReceiver>();
                        receiverCount++;
                    }
                }
            }

            if (receiverCount > 0)
            {
                Debug.Log($"[SceneSanitizer] Attached AnimationEventReceiver to {receiverCount} animators to capture unhandled animation events.");
            }
        }
    }

    /// <summary>
    /// Captures unhandled AnimationEvents (like ReleasePopcorn and HoldPopcorn) to prevent console warnings on background character props.
    /// </summary>
    public class AnimationEventReceiver : MonoBehaviour
    {
        public void ReleasePopcorn() { }
        public void HoldPopcorn() { }
        public void GrabPopcorn() { }
        public void SitDown() { }
        public void StandUp() { }
    }
}
