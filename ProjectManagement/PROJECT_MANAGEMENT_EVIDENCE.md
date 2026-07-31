# Cinema Tycoon - Project Management Evidence

This is a repository-backed delivery record for the course submission. It links work to concrete source files, screenshots, and Git commits rather than claiming a separate task-management tool that was not used for this project.

## Scope and Workstreams

| Workstream | Delivered outcome | Repository evidence |
| --- | --- | --- |
| Core simulation | Customer behavior, staff roles, economy, movie scheduling, upgrades, and incidents | `Assets/_CinemaTycoon/Scripts/Customers`, `Staff`, `Economy`, `Schedule`, and `Events` |
| Navigation and environment | NavMesh-driven customer/staff movement, seating, lobby, concessions, and hall spaces | `CinemaWaypoints`, customer FSM, and `Screenshots/` environment captures |
| User experience | UI Toolkit HUD, management controls, menus, instructions, pause/restart, settings, credits, loss, and victory states | `UI/Toolkit/HUDLayout.uxml`, `HUDStyle.uss`, `UIToolkitHUDBindings.cs` |
| Audio and feedback | Background music, shared UI click feedback, Master/Music/SFX control path, and Audio Mixer setup notes | `GameManager.cs`, `Assets/Audio`, and `Assets/_CinemaTycoon/Audio/AUDIO_MIXER_SETUP.md` |
| Submission materials | README, gameplay screenshots, video walkthrough link, attribution, and QA checklist | `README.md`, `Screenshots/`, `ProjectManagement/` |

## Development Milestones

The current Git history provides the review trail below. Commit hashes are included so the assessor can inspect the corresponding changes directly.

| Date | Milestone | Commit evidence |
| --- | --- | --- |
| 2026-07-25 | README and project documentation baseline updated | `b429c79` |
| 2026-07-26 | Main-menu work and developer cheat tooling added | `5aaaf59`, `718d834` |
| 2026-07-29 | Project cleanup and serialization work | `f310808`, `8b75b52` |
| 2026-07-30 to 2026-07-31 | Audio routing, mixer references, menu polish, walkthrough, and submission documentation finalized | `aeaecf5`, `8454f48`, `c998e35`, `518c4ce`, `3786307` |

## Contribution Record

Git commits in the current repository history are authored by **Rahat Abedin**. The work is organized by technical responsibility rather than an invented team-role table:

- Gameplay systems: economy, scheduling, staff behavior, customer behavior, events, and progression.
- Technical systems: scene flow, persistent `GameManager`, audio routing, NavMesh, and debugging support.
- UI and presentation: UI Toolkit layouts/styles, menus, settings, win/loss screens, screenshot selection, and README.
- Quality assurance and submission: Build Settings review, documented manual test checklist, video link, and asset attribution.

If additional contributors participated outside the visible Git history, add their real names and contributions here before submission; do not add placeholder names.

## Review Process Used

1. Make focused changes and preserve a Git commit trail for feature integration.
2. Verify scene flow after menu, pause, restart, audio, and state-management changes.
3. Record static checks and outstanding manual verification in `TEST_CHECKLIST.md`.
4. Maintain presentation evidence in `Screenshots/` and link the final walkthrough from the README.
