#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace CinemaTycoon.EditorTools
{
    /// <summary>Creates the project mixer asset from Unity's editor API.</summary>
    public static class CinemaTycoonAudioMixerCreator
    {
        private const string MixerPath = "Assets/_CinemaTycoon/Audio/CinemaTycoonMixer.mixer";

        [MenuItem("Cinema Tycoon/Create Audio Mixer")]
        public static void CreateAudioMixer()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (existing != null)
            {
                Debug.Log($"[Audio] Mixer already exists at {MixerPath}.");
                return;
            }

            // AudioMixerController is an internal native Unity type and cannot
            // be safely created via ScriptableObject.CreateInstance. Use the
            // same supported menu command as Assets > Create > Audio Mixer.
            var audioFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets/_CinemaTycoon/Audio");
            if (audioFolder == null)
            {
                Debug.LogError("[Audio] The Cinema Tycoon Audio folder was not found.");
                return;
            }

            Selection.activeObject = audioFolder;
            string mixerMenuPath = FindAudioMixerCreateMenuPath();
            if (mixerMenuPath == null || !EditorApplication.ExecuteMenuItem(mixerMenuPath))
            {
                Debug.LogError("[Audio] Unity could not find its Audio Mixer create command. " +
                               "Use the Project window's Create menu to make an Audio Mixer, then place it in Assets/_CinemaTycoon/Audio.");
                return;
            }

            var mixer = Selection.activeObject as AudioMixer;
            if (mixer == null)
            {
                Debug.LogError("[Audio] Unity did not return the newly created Audio Mixer asset.");
                return;
            }

            string createdPath = AssetDatabase.GetAssetPath(mixer);
            string moveError = AssetDatabase.MoveAsset(createdPath, MixerPath);
            if (!string.IsNullOrEmpty(moveError))
            {
                Debug.LogError($"[Audio] Created a mixer at {createdPath}, but could not move it: {moveError}");
                return;
            }

            mixer.name = "CinemaTycoonMixer";
            AssetDatabase.SaveAssets();
            Selection.activeObject = mixer;
            Debug.Log($"[Audio] Created {MixerPath}. Add Music and SFX child groups, expose MasterVolume, MusicVolume, and SfxVolume, then assign them to GameManager.");
        }

        private static string FindAudioMixerCreateMenuPath()
        {
            // Unity 6 moved this menu in some installations. Query the editor
            // rather than assuming the Unity 2022-era menu location.
            var unsupportedType = typeof(Editor).Assembly.GetType("UnityEditor.Unsupported");
            var getSubmenus = unsupportedType?.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(method => method.Name == "GetSubmenus" && method.GetParameters().Length == 1);
            var submenuItems = getSubmenus?.Invoke(null, new object[] { "Assets/Create" }) as string[];

            string discoveredPath = submenuItems?
                .FirstOrDefault(item => item.IndexOf("Audio Mixer", StringComparison.OrdinalIgnoreCase) >= 0);
            if (!string.IsNullOrEmpty(discoveredPath))
                return discoveredPath.StartsWith("Assets/", StringComparison.Ordinal) ? discoveredPath : $"Assets/Create/{discoveredPath}";

            // Known locations across supported Unity versions.
            string[] candidates =
            {
                "Assets/Create/Audio/Audio Mixer",
                "Assets/Create/Audio Mixer"
            };

            return candidates[0];
        }
    }
}
#endif
