# Cinema Tycoon - Submission Verification Record

This file distinguishes checks verified from the repository from checks that must be run manually in the Unity Editor or a standalone build. This avoids claiming runtime verification that has not been performed.

## Repository Checks

| Check | Result | Evidence | Date |
| --- | --- | --- | --- |
| Build scene list | Verified | `ProjectSettings/EditorBuildSettings.asset` enables `MainMenu` and `Demo`. | 28 Jul 2026 |
| Menu and end-state UI | Verified | `HUDLayout.uxml` contains main menu, pause, settings, game-over, and victory overlays. | 28 Jul 2026 |
| Starting satisfaction | Verified | GameManager scene data and script use a 55% starting rating. | 28 Jul 2026 |
| Win condition | Verified | `GameManager` triggers victory at 100% satisfaction; UI binds the victory overlay. | 28 Jul 2026 |
| Loss condition | Verified | Bankruptcy and zero satisfaction trigger the existing game-over event. | 28 Jul 2026 |
| Audio control path | Verified | GameManager has Master/Music/SFX Mixer fields and UI settings sliders. | 28 Jul 2026 |
| Screenshot evidence | Verified | Nine gameplay screenshots are catalogued in `SCREENSHOT_CATALOG.md`. | 28 Jul 2026 |
| Walkthrough link | Verified | The final YouTube walkthrough is linked from `README.md`. | 28 Jul 2026 |

## Manual Final Checks

Complete these in the final Unity Editor session and in a standalone build where applicable.

| Test | Expected result | Result | Tester | Date |
| --- | --- | --- | --- | --- |
| Start Game | Main Menu loads Demo gameplay without console errors |  |  | 27 Jul 2026 |
| Starting satisfaction | HUD starts at 55% |  |  | 27 Jul 2026 |
| Victory | Raising satisfaction to 100% shows `Cinema Thriving!` and freezes the simulation |  |  | 27 Jul 2026 |
| Loss | Zero cash or satisfaction shows a clear loss reason |  |  | 27 Jul 2026 |
| Pause flow | Escape pauses; Resume restores control; Restart resets the scene |  |  | 27 Jul 2026 |
| Audio | Background music and UI pop play; Master/Music/SFX controls affect routed sources |  |  | 27 Jul 2026 |
| Resolution check | Menus stay readable at 1280x720 and 1920x1080 |  |  | 27 Jul 2026 |
| Standalone build | Executable starts, gameplay works, and Quit exits the application |  |  | 28 Jul 2026 |
| Console review | No recurring errors, missing references, or warning spam |  |  | 28 Jul 2026 |
