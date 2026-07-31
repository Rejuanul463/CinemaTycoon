# Cinema Tycoon - Submission Verification Record

This file distinguishes checks verified from the repository from checks that must be run manually in the Unity Editor or a standalone build. This avoids claiming runtime verification that has not been performed.

## Repository Checks

| Check | Result | Evidence |
| --- | --- | --- |
| Build scene list | Verified | `ProjectSettings/EditorBuildSettings.asset` enables `MainMenu` and `Demo`. |
| Menu and end-state UI | Verified | `HUDLayout.uxml` contains main menu, pause, settings, game-over, and victory overlays. |
| Starting satisfaction | Verified | GameManager scene data and script use a 55% starting rating. |
| Win condition | Verified | `GameManager` triggers victory at 100% satisfaction; UI binds the victory overlay. |
| Loss condition | Verified | Bankruptcy and zero satisfaction trigger the existing game-over event. |
| Audio control path | Verified | GameManager has Master/Music/SFX Mixer fields and UI settings sliders. |
| Screenshot evidence | Verified | Nine gameplay screenshots are catalogued in `SCREENSHOT_CATALOG.md`. |
| Walkthrough link | Verified | The final YouTube walkthrough is linked from `README.md`. |

## Manual Final Checks

Complete these in the final Unity Editor session and in a standalone build where applicable.

| Test | Expected result | Result / tester / date |
| --- | --- | --- |
| Start Game | Main Menu loads Demo gameplay without console errors | ________________________ |
| Starting satisfaction | HUD starts at 55% | ________________________ |
| Victory | Raising satisfaction to 100% shows `Cinema Thriving!` and freezes the simulation | ________________________ |
| Loss | Zero cash or satisfaction shows a clear loss reason | ________________________ |
| Pause flow | Escape pauses; Resume restores control; Restart resets the scene | ________________________ |
| Audio | Background music and UI pop play; Master/Music/SFX controls affect routed sources | ________________________ |
| Resolution check | Menus stay readable at 1280x720 and 1920x1080 | ________________________ |
| Standalone build | Executable starts, gameplay works, and Quit exits the application | ________________________ |
| Console review | No recurring errors, missing references, or warning spam | ________________________ |
