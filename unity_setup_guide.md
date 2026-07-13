# Cinema Tycoon — Unity Setup Guide

Follow this step-by-step guide to wire up the C# scripts, animations, camera, and UI Toolkit inside your Unity 6 Editor and run the game.

---

## Step 1: Create the Folder Structure & Data Assets
In the **Project Window** under `Assets/`, create the following folder for ScriptableObject assets if it doesn't already exist:
*   `Assets/_CinemaTycoon/Data/`

Right-click in the `Data/` folder and create the following ScriptableObject instances using the Assets menu:

### 1. Movie Assets
Right-click $\rightarrow$ **Create** $\rightarrow$ **CinemaTycoon** $\rightarrow$ **Movie**
Create 3 Movie assets (e.g., `ActionMovie`, `ComedyMovie`, `HorrorMovie`):
*   Set their title, genre, base ticket price (e.g., `12`), popularity (e.g., `0.7`), and duration (in-game seconds, e.g., `30` to keep testing fast).

### 2. Staff Role Assets
Right-click $\rightarrow$ **Create** $\rightarrow$ **CinemaTycoon** $\rightarrow$ **StaffRole**
Create 3 Staff Role assets (e.g., `CashierRole`, `JanitorRole`, `GuardRole`):
*   Set their role enum (`Cashier`, `Janitor`, `Guard`).
*   Set hire costs (e.g., `150`) and wage per tick (e.g., `5`).

### 3. Upgrade Assets
Right-click $\rightarrow$ **Create** $\rightarrow$ **CinemaTycoon** $\rightarrow$ **Upgrade**
Create 4 Upgrade assets (`PremiumPopcorn`, `FasterCashier`, `ComfySeats`, `Marketing`):
*   Match their `UpgradeType` enum appropriately.
*   Set costs (e.g., `250`) and multipliers (e.g., `1.5`).

---

## Step 2: Create the Single Scene Setup
Cinema Tycoon operates in a **single scene** structure.
1.  In the **Project Window**, go to `Assets/Scenes/`.
2.  Open or create a scene named `Game.unity`.
3.  Go to **File** $\rightarrow$ **Build Settings...** and add the `Game` scene to the **Scenes In Build** list. (Remove any other scene references).

---

## Step 3: Create the NavMesh (AI Navigation Workflow)
To allow customers and staff to walk around the layout:
1.  Open the **Window** $\rightarrow$ **Package Manager**.
2.  Install the **AI Navigation** package if it is not already in the project.
3.  Select your floor/ground plane object in the hierarchy.
4.  In the Inspector, attach a **NavMeshSurface** component to the ground plane.
5.  Click **Bake** on the `NavMeshSurface` component. A blue overlay will represent the walkable pathing zone between your lobby, ticket booth, cinema hall, and exit.

---

## Step 4: Setup the Waypoints Authority
Create an empty GameObject in the scene named `CinemaWaypoints`:
1.  Attach the [CinemaWaypoints](file:///C:/Users/Rahat/Projects/CinemaTycoon/Assets/_CinemaTycoon/Scripts/Core/CinemaWaypoints.cs) component to it.
2.  Under it, create child empty GameObjects and position them in your scene:
    *   **Spawn Point**: Where customers appear (near the lobby entrance).
    *   **Exit Point**: Where customers go when leaving (outside/exit doors).
    *   **Ticket Booth**: Where the cashier stands.
    *   **Queue Points**: Position 4-6 empty GameObjects in a neat line in front of the Ticket Booth. Assign these to the `Queue Points` array in the component.
    *   **Hall Seats**: Position 8-12 empty GameObjects inside the cinema hall representing the seats. Assign these to the `Hall Seats` array.
    *   **Cashier Station / Janitor Station / Guard Station**: Stations where staff sit or idle when not busy.
3.  Wire all these transforms into their corresponding slots in the `CinemaWaypoints` Inspector.

---

## Step 5: Setup Prefabs

### 1. Customer Prefab
1.  In the scene, create a 3D Capsule.
2.  Rename it to `CustomerPrefab` and add a `NavMeshAgent` component (set radius to `0.4`, speed to `2.5`).
3.  Add the [Customer](file:///C:/Users/Rahat/Projects/CinemaTycoon/Assets/_CinemaTycoon/Scripts/Customers/Customer.cs) component.
4.  Drag it into your Project window to turn it into a Prefab, then delete it from the scene hierarchy.

### 2. Staff Prefab & Animator Controller Setup
1.  In the project window, create a base **Animator Controller** named `Staff_Base_Controller`.
2.  Open it and add two parameters in the Parameters tab:
    *   `IsWalking` (**Bool**)
    *   `IsWorking` (**Bool**)
3.  Create three states: `Idle`, `Walk`, and `Work` (use dummy placeholder animations).
4.  Wire transition lines:
    *   `Idle` $\rightarrow$ `Walk` when `IsWalking` is true.
    *   `Walk` $\rightarrow$ `Idle` when `IsWalking` is false.
    *   `Idle` $\rightarrow$ `Work` when `IsWorking` is true.
    *   `Work` $\rightarrow$ `Idle` when `IsWorking` is false and `IsWalking` is false.
    *   `Walk` $\rightarrow$ `Work` when `IsWorking` is true.
    *   `Work` $\rightarrow$ `Walk` when `IsWorking` is false and `IsWalking` is true.
    *   *Uncheck "Has Exit Time" on all transitions for instant blending.*
5.  Create three **Animator Override Controllers** in your assets: `Cashier_Override`, `Janitor_Override`, and `Guard_Override`. Set the parent controller to `Staff_Base_Controller` for all of them. Override the `Work` clip with the role-specific working animations.
6.  Create a 3D Capsule in the scene named `StaffPrefab`, add a `NavMeshAgent` and an `Animator` component.
7.  Add the [Staff](file:///C:/Users/Rahat/Projects/CinemaTycoon/Assets/_CinemaTycoon/Scripts/Staff/Staff.cs) component.
8.  Drag the Capsule into your Project window to turn it into a Prefab, then delete it from the scene hierarchy.

---

## Step 6: Setup the Game Managers
Create an empty GameObject in your scene named `GameManager`:
1.  Attach the following components to it:
    *   [GameManager](file:///C:/Users/Rahat/Projects/CinemaTycoon/Assets/_CinemaTycoon/Scripts/Core/GameManager.cs)
    *   [EconomyManager](file:///C:/Users/Rahat/Projects/CinemaTycoon/Assets/_CinemaTycoon/Scripts/Economy/EconomyManager.cs)
    *   [CustomerSpawnManager](file:///C:/Users/Rahat/Projects/CinemaTycoon/Assets/_CinemaTycoon/Scripts/Customers/CustomerSpawnManager.cs)
    *   [ScheduleManager](file:///C:/Users/Rahat/Projects/CinemaTycoon/Assets/_CinemaTycoon/Scripts/Schedule/ScheduleManager.cs)
    *   [StaffManager](file:///C:/Users/Rahat/Projects/CinemaTycoon/Assets/_CinemaTycoon/Scripts/Staff/StaffManager.cs)
    *   [EventManager](file:///C:/Users/Rahat/Projects/CinemaTycoon/Assets/_CinemaTycoon/Scripts/Events/EventManager.cs)
    *   [SceneFlowManager](file:///C:/Users/Rahat/Projects/CinemaTycoon/Assets/_CinemaTycoon/Scripts/UI/SceneFlowManager.cs)
    *   [CheatManager](file:///C:/Users/Rahat/Projects/CinemaTycoon/Assets/_CinemaTycoon/Scripts/DevTools/CheatManager.cs)

2.  Configure the components in the Inspector:
    *   **GameManager**: Drag all manager components on the same GameObject into their respective slots.
    *   **EconomyManager**: Drag the 4 Upgrade assets you created in Step 1 into `Available Upgrades`.
    *   **CustomerSpawnManager**: Drag your `CustomerPrefab` into the `Customer Prefab` slot.
    *   **ScheduleManager**: Drag the Movie assets you created in Step 1 into `Available Movies`.
    *   **StaffManager**: Drag the Staff Role assets into their slots, and assign the `StaffPrefab` to the `Staff Prefab` slot.
    *   **EventManager**: Create a few empty GameObjects in your lobby as possible spill locations, and drag them into the `Possible Spill Locations` array.

---

## Step 7: Setup the Main Camera & Camera Controller
Select your **Main Camera** in the hierarchy:
1.  Attach the [FlyCameraController](file:///C:/Users/Rahat/Projects/CinemaTycoon/Assets/_CinemaTycoon/Scripts/Core/FlyCameraController.cs) component to the camera.
2.  Position and rotate the camera in the scene view to define the menu focal anchor point (the starting view when the game loads).

---

## Step 8: Setup UI Toolkit Layout
1.  Create an empty GameObject in the scene named `UI_HUD`.
2.  Attach a **UIDocument** component to it.
3.  Assign the [HUDLayout.uxml](file:///C:/Users/Rahat/Projects/CinemaTycoon/Assets/_CinemaTycoon/UI/Toolkit/HUDLayout.uxml) layout asset into the **Source Asset** slot on the `UIDocument`.
4.  Attach the [UIToolkitHUDBindings](file:///C:/Users/Rahat/Projects/CinemaTycoon/Assets/_CinemaTycoon/Scripts/UI/UIToolkitHUDBindings.cs) component to the GameObject.
5.  Assign the [EventPopupTemplate.uxml](file:///C:/Users/Rahat/Projects/CinemaTycoon/Assets/_CinemaTycoon/UI/Toolkit/EventPopupTemplate.uxml) template into the **Notification Template** slot on the script.

---

## Step 9: Play & Test
Press **Play** in Unity:
1.  The game will load in **Main Menu State** paused (`Time.timeScale = 0f`). The camera will bob and pan programmatically to showcase the scene.
2.  Click **START GAME** in the UI Toolkit overlay. The menu will vanish, the HUD will display, the simulation will unpause, and the camera will enter WASD flight controls.
3.  Press `Escape` to unlock the mouse cursor to interact with HUD buttons (like hiring cashiers or scheduling movies) and click back on the viewport to lock the cursor for flying.
4.  Press `F1` at runtime to open the Cheat Panel, check your balance/satisfaction rating, and inject cash if needed.
