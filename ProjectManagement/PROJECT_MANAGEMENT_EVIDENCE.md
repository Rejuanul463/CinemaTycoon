# Cinema Tycoon Project Management Evidence

This folder records the project's planned work, completed integration work, test responsibilities, and visual evidence for course submission. It is intentionally kept in the repository so the assessor can review project-management evidence without external access.

## Delivery Register

| Deliverable | Owner role | Status | Evidence |
| --- | --- | --- | --- |
| Multi-scene game flow | Systems / UI | Complete | `MainMenu` and `Demo` are enabled in Build Settings. |
| Customer and staff AI | Gameplay | Complete | NavMesh FSM, task dispatch, and behavior scripts under `Assets/_CinemaTycoon/Scripts`. |
| Economy, upgrades, and scheduling | Gameplay | Complete | Economy, Schedule, Staff, and Data folders. |
| UI Toolkit menu and HUD | UI / UX | Complete | UXML, USS, bindings, pause, settings, credits, loss, and victory overlays. |
| Audio routing | Audio / Systems | Ready for asset assignment | `GameManager` supports Master/Music/SFX Mixer routing; see `Assets/_CinemaTycoon/Audio/AUDIO_MIXER_SETUP.md`. |
| Final recorded walkthrough | Documentation | Pending | Add the final YouTube URL to the root README before submission. |
| Team member names and assigned roles | Team | Pending | Complete the role table below with real group members. |

## Role Matrix

| Team member | Primary role | Responsibilities | Evidence to attach before submission |
| --- | --- | --- | --- |
| _Add name_ | Gameplay systems | Economy, staff, scheduling, incidents | Commit history / walkthrough segment |
| _Add name_ | AI and environment | NavMesh, customer behavior, scene setup | Commit history / screenshot |
| _Add name_ | UI and presentation | UI Toolkit, menus, HUD, documentation | Commit history / screenshot |
| _Add name_ | QA and submission | Builds, testing, video, README | Test log / final video |

Do not submit this table with placeholders: replace each `_Add name_` entry with the real team member.

## Review Cadence

1. Integrate one feature at a time on a dedicated branch or commit.
2. Test the Main Menu -> Demo -> Pause -> Restart -> Main Menu flow after every scene-management change.
3. Record console errors, broken references, and test outcomes in `TEST_CHECKLIST.md`.
4. Before submission, capture the final build flow and update the README video link, team details, and screenshots.
