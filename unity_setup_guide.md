# Cinema Tycoon — Unity Setup Guide

Follow this step-by-step guide to wire up the C# scripts inside your Unity Editor and run the game.

---

## Step 1: Create the Folder Structure & Data Assets
In the **Project Window** under `Assets/`, create the following folder for ScriptableObject assets if it doesn't already exist:
* `Assets/_CinemaTycoon/Data/`

Right-click in the `Data/` folder and create the following ScriptableObject instances using the Assets menu:

### 1. Movie Assets
Right-click $\rightarrow$ **Create** $\rightarrow$ **CinemaTycoon** $\rightarrow$ **Movie**
Create 3 Movie assets (e.g., `ActionMovie`, `ComedyMovie`, `HorrorMovie`):
* Set their title, genre, base ticket price (e.g., `12`), popularity (e.g., `0.7`), and duration (in-game seconds, e.g., `30` to keep testing fast).

### 2. Staff Role Assets
Right-click $\rightarrow$ **Create** $\rightarrow$ **CinemaTycoon** $\rightarrow$ **StaffRole**
Create 3 Staff Role assets (e.g., `CashierRole`, `JanitorRole`, `GuardRole`):
* Set their role enum (`Cashier`, `Janitor`, `Guard`).
* Set hire costs (e.g., `150`) and wage per tick (e.g., `5`).

### 3. Upgrade Assets
Right-click $\rightarrow$ **Create** $\rightarrow$ **CinemaTycoon** $\rightarrow$ **Upgrade**
Create 4 Upgrade assets (`PremiumPopcorn`, `FasterCashier`, `ComfySeats`, `Marketing`):
* Match their `UpgradeType` enum appropriately.
* Set costs (e.g., `250`) and multipliers (e.g., `1.5`).

---

## Step 2: Create the Scenes
1. In the **Project Window**, go to `Assets/Scenes/`.
2. Create two scenes:
   * `MainMenu.unity`
   * `Game.unity`
3. Go to **File** $\rightarrow$ **Build Settings...** and drag both scenes into the **Scenes In Build** list.

---

## Step 3: Create the NavMesh (Physics & Pathfinding)
1. Open the `Game` scene.
2. Build your physical level layout using standard 3D cubes/planes (a floor plane, a lobby, ticket booths, and a cinema hall).
3. Select your floor/ground objects, and in the Inspector, check **Static** (or mark them as **Navigation Static**).
4. Open the **Navigation (Obsolete)** window (or NavMesh Components window if using the newer system) and click **Bake**.
5. Ensure a blue NavMesh overlay covers the floor paths between the entrance, the ticket queue, the booths, the seats in the hall, and the exit.

---

## Step 4: Setup the Waypoints Authority
Create an empty GameObject in the scene named `CinemaWaypoints`:
1. Attach the [CinemaWaypoints](file:///Users/gamerangers/Documents/GitHub/CinemaTycoon/Assets/_CinemaTycoon/Scripts/Core/CinemaWaypoints.cs) component to it.
2. Under it, create child empty GameObjects and position them in your scene:
   * **Spawn Point**: Where customers appear (near the lobby entrance).
   * **Exit Point**: Where customers go when leaving (outside/exit doors).
   * **Ticket Booth**: Where the cashier stands.
   * **Queue Points**: Position 4-6 empty gameobjects in a neat line in front of the Ticket Booth. Assign these to the `Queue Points` array in the component.
   * **Hall Seats**: Position 8-12 empty gameobjects inside the cinema hall representing the seats. Assign these to the `Hall Seats` array.
   * **Cashier Station / Janitor Station / Guard Station**: Stations where staff sit or idle when not busy.
3. Wire all these transforms into their corresponding slots in the `CinemaWaypoints` inspector.

---

## Step 5: Setup Prefabs

### 1. Customer Prefab
1. In the scene, create a 3D Capsule.
2. Rename it to `CustomerPrefab` and add a `NavMeshAgent` component (set radius to `0.4`, speed to `2.5`).
3. Add the [Customer](file:///Users/gamerangers/Documents/GitHub/CinemaTycoon/Assets/_CinemaTycoon/Scripts/Customers/Customer.cs) component.
4. Drag it into your Project window to turn it into a Prefab, then delete it from the scene hierarchy.

### 2. Staff Prefab
1. In the scene, create a 3D Capsule.
2. Rename it to `StaffPrefab` and add a `NavMeshAgent` component.
3. Add an `Animator` component (and if you have basic walking/working animations, wire them to parameters named `IsWalking` (bool) and `IsWorking` (bool)).
4. Add the [Staff](file:///Users/gamerangers/Documents/GitHub/CinemaTycoon/Assets/_CinemaTycoon/Scripts/Staff/Staff.cs) component.
5. Drag it into your Project window to turn it into a Prefab, then delete it from the scene hierarchy.

---

## Step 6: Setup the Game Managers
Create an empty GameObject in your scene named `GameManager`:
1. Attach the following components to it:
   * [GameManager](file:///Users/gamerangers/Documents/GitHub/CinemaTycoon/Assets/_CinemaTycoon/Scripts/Core/GameManager.cs)
   * [EconomyManager](file:///Users/gamerangers/Documents/GitHub/CinemaTycoon/Assets/_CinemaTycoon/Scripts/Economy/EconomyManager.cs)
   * [CustomerSpawnManager](file:///Users/gamerangers/Documents/GitHub/CinemaTycoon/Assets/_CinemaTycoon/Scripts/Customers/CustomerSpawnManager.cs)
   * [ScheduleManager](file:///Users/gamerangers/Documents/GitHub/CinemaTycoon/Assets/_CinemaTycoon/Scripts/Schedule/ScheduleManager.cs)
   * [StaffManager](file:///Users/gamerangers/Documents/GitHub/CinemaTycoon/Assets/_CinemaTycoon/Scripts/Staff/StaffManager.cs)
   * [EventManager](file:///Users/gamerangers/Documents/GitHub/CinemaTycoon/Assets/_CinemaTycoon/Scripts/Events/EventManager.cs)
   * [SceneFlowManager](file:///Users/gamerangers/Documents/GitHub/CinemaTycoon/Assets/_CinemaTycoon/Scripts/UI/SceneFlowManager.cs)
   * [CheatManager](file:///Users/gamerangers/Documents/GitHub/CinemaTycoon/Assets/_CinemaTycoon/Scripts/DevTools/CheatManager.cs)

2. Configure the components in the Inspector:
   * **GameManager**: Drag all the manager components on the same GameObject into their respective slots.
   * **EconomyManager**: Drag the 4 Upgrade assets you created in Step 1 into `Available Upgrades`.
   * **CustomerSpawnManager**: Drag your `CustomerPrefab` into the `Customer Prefab` slot.
   * **ScheduleManager**: Drag the Movie assets you created in Step 1 into `Available Movies`.
   * **StaffManager**: Drag the Staff Role assets into their slots, and assign the `StaffPrefab` to the `Staff Prefab` slot.
   * **EventManager**: Create a few empty GameObjects in your lobby as possible spill locations, and drag them into the `Possible Spill Locations` array.

---

## Step 7: Create the UI Canvas & HUDBindings
1. In the Hierarchy, right-click $\rightarrow$ **UI** $\rightarrow$ **Canvas**.
2. Add the [HUDBindings](file:///Users/gamerangers/Documents/GitHub/CinemaTycoon/Assets/_CinemaTycoon/Scripts/UI/HUDBindings.cs) component to the Canvas.
3. Design and create your UI elements under the Canvas:
   * Text fields for `Balance`, `Rating`, `Now Playing`, `Staff Count`, and `Last Ticket`.
   * Sliders for `Rating` and `Cleanliness`.
   * An empty container transform for `Event Notification Container`.
   * Create a simple panel/text child UI item to act as the notification template, turn it into a Prefab, and reference it as `Event Notification Prefab` on the component.
4. Drag all UI objects into the fields under `HUDBindings` in the Inspector.
5. Setup UI Panels for Pause, Settings, and Game Over, and reference them in the `SceneFlowManager` component under the `GameManager` object. Add Buttons and wire their `onClick` events to the corresponding methods inside `SceneFlowManager` (`Resume()`, `Restart()`, `OpenSettings()`, `CloseSettings()`, `ExitToMainMenu()`).

---

## Step 8: Play & Test
Press **Play** in Unity:
1. Customers will spawn and walk to the queue, but they can't purchase tickets since no Cashier is hired. Their satisfaction drops and they leave.
2. In your UI, add a button to hire staff and link it to `StaffManager.TryHire(StaffRole.Cashier)`. Hire a Cashier to watch them walk to their station and start processing the queue!
3. Add a button to schedule movies and link it to `ScheduleManager.TryScheduleShow()`. Schedule your movie, and customers will buy tickets, enter the hall, watch the movie, and leave satisfied.
4. Press `F1` at runtime to open the Cheat Panel, check your balance/satisfaction rating, and inject cash if needed.
