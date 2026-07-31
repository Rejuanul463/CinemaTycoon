# Cinema Tycoon Audio Mixer Setup

Create `CinemaTycoonMixer.mixer` using **Cinema Tycoon > Create Audio Mixer**. The command uses Unity's native Audio Mixer asset creation path and saves the mixer in this folder. Then create two child groups beneath `Master`: `Music` and `SFX`.

Expose the volume property of each group and name the exposed parameters exactly:

| Mixer group | Exposed parameter |
| --- | --- |
| Master | `MasterVolume` |
| Music | `MusicVolume` |
| SFX | `SfxVolume` |

On the `GameManager` object, assign the mixer to **Audio Mixer**, then drag the `Music` and `SFX` groups into their matching fields. Assign the background-music clip to **Background Music**.

`GameManager` routes its looping background source to Music and one-shot effects played through `PlaySfx` to SFX. When a gameplay scene loads, it also routes existing scene AudioSources to SFX. The UI Toolkit volume sliders set the three exposed parameters in decibels; if a mixer has not yet been assigned, they use a safe direct-volume fallback.
