# Cinema Tycoon - Unity Course Project

## Executive Summary

Cinema Tycoon is a 3D management simulation developed in Unity 6. The player assumes the role of a cinema manager responsible for operating a movie theater business. Responsibilities include scheduling movie screenings, managing staff rosters, processing audience demand, maintaining facility cleanliness, purchasing venue upgrades, and responding to real-time emergency events.

The project demonstrates modular system design, decoupled event-driven architecture, NavMesh-driven artificial intelligence, custom finite state machines, UI Toolkit interface bindings, and developer tooling.

---

## Table of Contents

1. [Project Overview](#project-overview)
2. [Game Flow and Scene Architecture](#game-flow-and-scene-architecture)
3. [Controls and Player Usability](#controls-and-player-usability)
4. [Gameplay Mechanics Breakdown](#gameplay-mechanics-breakdown)
5. [Unity AI Systems and Navigation](#unity-ai-systems-and-navigation)
6. [External Framework Integration](#external-framework-integration)
7. [Cheat Manager and Debugging Tools](#cheat-manager-and-debugging-tools)
8. [Setup and Installation Guide](#setup-and-installation-guide)
9. [Technical Architecture and System Design](#technical-architecture-and-system-design)
10. [Asset Attribution](#asset-attribution)
11. [Media and Video Walkthrough](#media-and-video-walkthrough)
12. [Project Team and Credits](#project-team-and-credits)

---

## Project Overview

### Game Objective
Maintain a profitable cinema business while maximizing overall audience satisfaction. The player earns revenue through ticket sales and concession purchases, which can be reinvested into hiring staff and purchasing venue upgrades.

### Win and Lose Conditions
* **Victory Condition**: Reach **100% Cinema Rating**. This freezes gameplay and presents the `Cinema Thriving!` victory screen with restart and return-to-menu options.
* **Loss Conditions**:
  * **Bankruptcy**: Account balance drops to $0 or below.
  * **Audience Dissatisfaction**: Overall Cinema Rating hits 0% due to uncleaned spills, broken equipment, queue frustration, or dirty viewing halls.

---

## Game Flow and Scene Architecture

The project follows a standard multi-scene setup cleanly separating system initialization, menu flow, and active gameplay loop.

### Scene Structure
1. `MainMenu.unity`: Contains the primary interface, how-to-play guidance, audio/settings controls, and project credits.
2. `Demo.unity` (Gameplay Scene): Contains the 3D cinema hall layout, baked NavMesh navigation surfaces, waypoints authority, seating logic, and subsystem managers.

### Menu Flow

- **Main Menu:** Start Game loads the gameplay scene. How to Play explains the goal, failure conditions, controls, and management actions. Settings provides master, music, and SFX volume controls. Credits lists the project and bundled asset sources. Quit exits the built game (or stops Play Mode in the Unity Editor).
- **Pause Menu:** Press `Escape` to freeze gameplay and open Resume, Restart, Settings, and Exit to Main Menu options.
- **Game Over:** The overlay displays the loss reason (bankruptcy or zero satisfaction) and offers Restart Game or Exit to Main Menu.

Assign an audio clip to **Game Manager → Audio → Background Music** in the Unity Inspector. The manager creates a looping AudioSource automatically when one has not been assigned. Assign `CinemaTycoonMixer`, `Music`, and `SFX` from the Audio Mixer asset to the matching Game Manager fields to enable mixer-driven Master/Music/SFX controls.

### Game State Loop
```
[Main Menu Scene] -> Start Game -> [Gameplay Scene Initialization]
                                            |
                                    +-------+-------+
                                    | Game Loop     |
                                    +-------+-------+
                                            |
                +---------------------------+---------------------------+
                |                           |                           |
        [Schedule Movies]           [Manage Staff & UI]         [Handle Events & Spills]
                |                           |                           |
                +---------------------------+---------------------------+
                                            |
                                  +---------+---------+
                                  | Check End State   |
                                  +---------+---------+
                                            |
                     +----------------------+----------------------+
                     |                                             |
             [Bankrupt / Rating 0%]                       [Manual Pause / Restart]
                     |                                             |
             [Game Over Screen]                          [Pause Overlay Menu]
```

---

## Controls and Player Usability

The player navigates the cinema environment using a first-person fly camera with dual cursor modes.

### Key Bindings

| Action | Input (Keyboard / Mouse) | Description |
| :--- | :--- | :--- |
| Camera Movement | `W`, `A`, `S`, `D` | Move camera forward, left, backward, right |
| Camera Elevation | `E` / `Q` or `Space` / `Left Shift` | Ascend / Descend vertically |
| Camera Look | Mouse Movement | Rotates camera orientation when cursor is locked |
| Toggle Cursor Mode | `Left Alt` | Toggles between locked look mode and free cursor mode |
| Pause Menu | `Escape` | Toggles game pause state and opens pause overlay |
| Cheat Manager | `F1` | Toggles developer debug and cheat window (Editor/Dev builds) |

---

## Gameplay Mechanics Breakdown

The project implements six core gameplay mechanics exceeding the moderate complexity standards required by the course rubric.

### 1. NavMesh Customer AI Finite State Machine
* **Class Reference**: `CinemaTycoon.Customers.Customer`
* **Description**: Customers process complex behavioral flows driven by a custom 12-state finite state machine (FSM).
* **States**: `Entering`, `Queuing`, `Purchasing`, `Popcorn`, `Watching`, `Leaving`, `Unsatisfied`, `GoingToBathroom`, `UsingBathroom`, `ReturningFromBathroom`, `GoingToArcade`, `UsingArcade`.
* **State Behavior**: Customers evaluate pre-show conditions, optionally play arcade games, queue at ticket booths, make purchasing decisions based on ticket pricing, optionally detour to concession stands, use bathrooms, path to reserved seat approach points, sit down during movie screenings, and calculate individual satisfaction upon leaving.

### 2. Staff Hiring, Scheduling, and Priority Task Dispatcher
* **Class Reference**: `CinemaTycoon.Staff.StaffManager`, `CinemaTycoon.Staff.Staff`
* **Description**: A multi-role staffing system with automated task queuing and nearest-idle dispatch logic.
* **Roles**:
  * **Cashiers**: Required on duty to process ticket booth sales.
  * **Janitors**: Clean dirty chairs post-screening, resolve floor spills, fix projector breakdowns, unclog toilets, and provide passive hall cleanliness.
  * **Guards**: Reduce customer queue patience degradation by 25%, escort rowdy customers out of the building, and provide VIP escorts.
* **Economics**: Dynamic wage distribution per tick and escalating hire costs scaled per active staff count (`Base Cost * (1 + 0.5 * Active Count)`).

### 3. Cinema Economy and Upgrade Progression System
* **Class Reference**: `CinemaTycoon.Economy.EconomyManager`
* **Description**: Manages cash inflow, expense logging, periodic wage/utility deductions, and permanent business upgrades.
* **Upgrades**:
  * `PremiumPopcorn`: Multiplies concession purchase probability and per-sale popcorn revenue.
  * `FasterCashier`: Reduces queue waiting frustration rate.
  * `ComfySeats`: Enhances satisfaction gain rate while seated during screenings.
  * `Marketing`: Increases customer spawn frequency.

### 4. Movie Scheduling and Dynamic Genre Hype System
* **Class Reference**: `CinemaTycoon.Schedule.ScheduleManager`, `CinemaTycoon.Schedule.MovieData`
* **Description**: Handles film licensing, screening durations, ticket pricing, and market demand fluctuations.
* **Hype Mechanics**: Each screening lowers current market hype for that film's genre while unplayed genres gradually recover hype over time. Ticket revenue scales dynamically based on `Base Price * Genre Hype Multiplier`. Screening movies in dirty halls incurs severe overall rating penalties.

### 5. Dynamic Emergency Incident Management System
* **Class Reference**: `CinemaTycoon.Events.EventManager`
* **Description**: Asynchronous event manager generating unpredictable incidents during runtime.
* **Event Types**:
  * `Spill`: Spawns physical visual decals on NavMesh walkable ground requiring Janitor cleanup.
  * `VIPVisit`: Elevates a standard customer to VIP status, granting rating bonuses if served promptly and escorted by Guards.
  * `RowdyCustomer`: Flags unruly patrons needing Guard intervention.
  * `ProjectorBreakdown`: Ruination risk requiring urgent Janitor repair tasks.
  * `ToiletClog`: Facility issue causing audience dissatisfaction if neglected.

### 6. Dynamic Seating Discovery and Aisle Approach Point Algorithm
* **Class Reference**: `CinemaTycoon.Scripts.ChairLogic.ChairLogicHandler`, `OccupiedChairLogic`
* **Description**: Automatically discovers all theater seating hierarchies dynamically at runtime (`GetComponentsInChildren<OccupiedChairLogic>`).
* **Approach Point Logic**: Calculates precise local-space approach positions in row aisles to prevent `NavMeshAgent` pathing conflicts inside tightly packed chair geometry. Manages character seat occupancy visualization and chair cleanliness states.

---

## Unity AI Systems and Navigation

The project makes extensive use of the official Unity AI Navigation package (`UnityEngine.AI`).

* **NavMesh Surface**: Baked walkable navigation surface covering lobby, concessions, aisles, restrooms, and exit paths.
* **NavMeshAgent Integration**: Employed by all active Customer and Staff GameObjects for pathfinding and obstacle avoidance.
* **NavMesh Projection**: Dynamic incident points (e.g., floor spills) use `NavMesh.SamplePosition` with height-offset validation to guarantee task targets sit on valid walkable mesh.
* **Approach Vector Math**: Seating logic uses local-to-world offset transformations (`transform.TransformPoint(approachOffset)`) to resolve exact aisle coordinates for seat reservation.

---

## External Framework Integration

### Framework: DOTween (Demigiant)
* **Location**: `Assets/Plugins/Demigiant/DOTween`

### Justification and Usage
DOTween was selected to provide performant, code-driven visual animations without cluttering scene GameObjects with dedicated Unity Animator components.

### System Integration
1. **Decal Transition Effects**: Implemented in `SpillDecal.cs` for smooth alpha fade-in on spill creation and scaling fade-out upon Janitor task completion.
2. **UI Feedback and HUD Elements**: Applied to HUD notification popups, floating currency text, and UI modal transitions for smooth user feedback.
3. **Performance Optimization**: Eliminates garbage generation associated with runtime legacy animation components, ensuring smooth frame rates.

---

## Cheat Manager and Debugging Tools

To facilitate grading, testing, and system inspection, a dev-only Cheat Manager interface is included.

* **Class Reference**: `CinemaTycoon.DevTools.CheatManager`
* **Toggle Key**: `F1`
* **Features**:
  * Real-time balance injection (+$500)
  * Instant max satisfaction restoration (100%)
  * Instant hall cleanliness restoration (100%)
  * Event skipping and immediate incident triggering
  * Camera-relative floor spill spawning
* **Conditional Compilation**: Wrapped completely inside `#if UNITY_EDITOR || DEVELOPMENT_BUILD` directives, ensuring zero code footprint in production release builds.

---

## Setup and Installation Guide

### Prerequisites
* Unity 6 (6000.x) or higher
* AI Navigation Package (`com.unity.ai.navigation`)
* UI Toolkit (`com.unity.ui`)

### Opening the Project
1. Open Unity Hub, click **Add**, and select the root directory `CinemaTycoon`.
2. Select Unity Version 6 and open the project.

### Running in Editor
1. In the Project window, navigate to `Assets/Scenes/`.
2. Open `MainMenu.unity` (or `Assets/Synty/PolygonCity/Scenes/Demo.unity` for direct gameplay testing).
3. Press the **Play** button in the Unity Editor toolbar.

---

## Technical Architecture and System Design

The architecture relies on a centralized singleton coordinator pattern combined with static event notifications.

```
                              +--------------------+
                              |    GameManager     |
                              +---------+----------+
                                        |
        +------------------+------------+------------+-------------------+
        |                  |                         |                   |
+-------v-------+  +-------v-------+        +--------v-------+  +--------v-------+
|  Economy      |  |  Schedule     |        |  Staff         |  |  Events        |
|  Manager      |  |  Manager      |        |  Manager       |  |  Manager       |
+---------------+  +---------------+        +----------------+  +----------------+
        ^                  ^                         ^                   ^
        |                  |                         |                   |
        +------------------+------------+------------+-------------------+
                                        |
                             +----------v----------+
                             | UIToolkitHUDBindings |
                             +---------------------+
```

### Key Architectural Principles
* **Safe Singleton Pattern**: `GameManager` implements execution order prioritization (`[DefaultExecutionOrder(-100)]`) and handles duplicate destruction on scene reloads.
* **Event-Driven UI**: `UIToolkitHUDBindings` subscribes to static C# events (`EconomyManager.OnBalanceChanged`, `GameManager.OnCinemaRatingChanged`, etc.) ensuring UI updates only fire on state mutations.
* **ScriptableObject Data Containers**: Film attributes (`MovieData`), staff roles (`StaffRoleData`), and business upgrades (`UpgradeData`) are authored as reusable ScriptableObject assets.

---

## Asset Attribution

| Asset Category | Source / Provider | Description |
| :--- | :--- | :--- |
| Environment & Models | Synty Studios (Polygon Series) | 3D cinema building, furniture, props, and street environment |
| Character Models | Kevin Iglesias / Synty Studios | Animated customer and staff 3D character meshes |
| Concession Props | Mnostva Art | Food, drink, and popcorn models |
| Animation Controllers | Custom Unity Animator | `WorkerAnimator.controller` with `isWalking` and `isHolding` parameters |
| Animation Tweening | Demigiant | DOTween animation library |
| UI Framework | Unity Technologies | UI Toolkit (UXML and USS stylesheets) |

---

## Media and Video Walkthrough

### Gameplay Screenshots

| Main menu and UI | Cinema systems in action |
| :---: | :---: |
| ![Main Menu](Screenshots/Main%20Menu.PNG) | ![Cinema UI](Screenshots/UI%20%2B%20Front%20of%20the%20Cinema.PNG) |
| ![Lobby queue](Screenshots/Lobby%20%2B%20waiting%20line.PNG) | ![Customers using arcades](Screenshots/Customers%20using%20arcades.PNG) |
| ![Popcorn stand](Screenshots/Popcorn%20stand%20in%20the%20lobbyy.PNG) | ![Customers seated in hall](Screenshots/Hall%20Seats%20with%20customers%20sitting%20there.PNG) |

### Video Demonstration
* **YouTube Video Link**: [Cinema Tycoon narrated walkthrough](https://youtu.be/tvuFHeQwJGc?si=gNSKj6hJ6A3JHsMj)
* **Description**: A 5 to 10 minute narrated walkthrough showcasing game setup, menu flow, customer AI pathfinding, staff hiring, incident resolution, upgrade purchases, and cheat manager operations.

---

## Project Team and Credits

Developed as part of the Unity Course Project requirement.

* **Development Team**: Group Project Submission
* **Course**: Unity Game Development Course
* **Institution**: Computer Science / Game Development Department
