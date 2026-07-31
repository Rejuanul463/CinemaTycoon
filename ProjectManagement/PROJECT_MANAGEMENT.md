# Cinema Tycoon - Project Management

This document records the development plan and review record for the course submission, from **8 July to 28 July**. Each item is linked to work that can be inspected in the Unity project, screenshots, or submission documentation.

## Scope and Workstreams

| Workstream | Intended outcome | Project artifact |
| --- | --- | --- |
| Core simulation | A playable cinema-management loop with customers, staff, money, movie scheduling, upgrades, and incidents | `Assets/_CinemaTycoon/Scripts/Customers`, `Staff`, `Economy`, `Schedule`, and `Events` |
| Environment and navigation | A functioning cinema space with customer/staff routes, seating, lobby, concessions, and halls | Cinema waypoints, customer state logic, and `Screenshots/` |
| User interface | A UI Toolkit HUD, management panels, instructions, pause/restart controls, settings, credits, and clear end states | `Assets/_CinemaTycoon/UI/Toolkit/` and `UIToolkitHUDBindings.cs` |
| Audio and feedback | Background music, a shared UI click sound, and separate Master/Music/SFX controls | `GameManager.cs`, `Assets/Audio/`, and `Assets/_CinemaTycoon/Audio/AUDIO_MIXER_SETUP.md` |
| Submission preparation | A readable README, gameplay screenshots, video walkthrough, asset attribution, and test checklist | `README.md`, `Screenshots/`, and `ProjectManagement/` |

## Development Timeline

| Date / period | Planned focus | Outcome / review point |
| --- | --- | --- |
| 8–10 July | Define the cinema-management concept and establish the Unity project structure | Core scene layout, cinema environment, and the initial gameplay direction were established. |
| 11–14 July | Build the customer flow and cinema spaces | Customer movement, cinema waypoints, lobby, halls, seating, and service areas were developed and reviewed in play mode. |
| 15–17 July | Implement the management loop | Economy, staffing, movie scheduling, upgrades, and satisfaction-related gameplay systems were added. |
| 18–20 July | Add challenge and progression | Events, staff responsibilities, loss conditions, and the cinema-rating progression loop were refined. |
| 21–23 July | Improve player clarity and controls | The HUD and management controls were organized; instructions and menu flow were planned around the gameplay loop. |
| 24–25 July | Complete menu and end-state flow | Main-menu panels, pause options, restart/main-menu routes, credits, and win/loss presentation were reviewed. |
| 26 July | Add sound and settings support | Background-music and UI-feedback support were connected through Master/Music/SFX volume controls. |
| 27 July | Playtest and polish | Menu navigation, pausing, restart behaviour, end screens, and UI readability were checked; issues were recorded for correction. |
| 28 July | Prepare the assessment submission | README, screenshots, walkthrough link, asset attribution, project-management record, and manual test checklist were finalized. |

## Responsibilities

**Rahat Abedin** — project development and submission preparation:

- Gameplay: economy, scheduling, staff behaviour, customer behaviour, events, and progression.
- Technical setup: scene flow, persistent `GameManager`, audio routing, NavMesh, and debugging support.
- UI and presentation: UI Toolkit layouts/styles, menus, settings, win/loss screens, screenshots, and README.
- Quality review: Build Settings review, documented manual checks, walkthrough link, and asset attribution.

## Review Process

1. Work was completed in small feature areas, then checked in the relevant Unity scene.
2. Scene flow was reviewed after changes to menus, pause/restart, audio, and game-state handling.
3. Manual checks and remaining verification items are recorded in `TEST_CHECKLIST.md`.
4. Presentation evidence is maintained in `Screenshots/`; the final walkthrough is linked from the README.
