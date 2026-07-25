# Cinema Tycoon — Unity Setup Guide

Follow this step-by-step guide to wire up the C# scripts, animations, camera, and UI Toolkit inside your Unity 6 Editor and run the game.

> **Canonical scene:** `Assets/Synty/PolygonCity/Scenes/Demo.unity`. This is where the cinema layout, NavMesh, and the hall chair hierarchy live. The scene at `Assets/Scenes/Game.unity` is a minimal test bed — wire it up only for isolated testing.

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
Right-click $\rightarrow$ **Create** $\rightarrow$ **CinemaTycoon** $\rightarrow$ **Upgrade`
Create 4 Upgrade assets (`PremiumPopcorn`, `FasterCashier`, `ComfySeats`, `Marketing`):
*   Match their `UpgradeType` enum appropriately.
*   Set costs (e.g., `250`) and multipliers (e.g., `1.5`).
*   **`PremiumPopcorn`**: drives both `PopcornChanceMultiplier` (the chance a customer detours to the concession stand) and `PopcornRevenueMultiplier` (the per-sale price). A `multiplier` of `1.5` means a 40% base chance becomes 60% and a $5 popcorn becomes $7.50. It also retains a legacy effect on ticket revenue.

---

## Step 2: Configure Multi-Scene Build Settings
Cinema Tycoon operates in a clean **multi-scene** architecture:
1. In Unity, go to **File** $\rightarrow$ **Build Settings...**.
2. Add `Assets/Scenes/MainMenu.unity` as **Scene 0** (loaded first when building/launching).
3. Add `Assets/Synty/PolygonCity/Scenes/Demo.unity` as **Scene 1** (the main gameplay environment).
4. `Demo.unity` contains the 3D cinema layout, NavMesh Surface, waypoints, and seating hierarchy.

---

## Step 3: Create the NavMesh (AI Navigation Workflow)
To allow customers and staff to walk around the layout:
1.  Open the **Window** $\rightarrow$ **Package Manager**.
2.  Install the **AI Navigation** package if it is not already in the project.
3.  Select your floor/ground plane object in the hierarchy (a `NavMeshSurface` already exists in `Demo.unity` — reuse it).
4.  In the Inspector, ensure the **NavMeshSurface** covers the lobby, ticket booth, cinema hall, and — critically — **the aisle in front of (or behind) each chair row**. Customers path to a per-chair *approach point* in the aisle, not the chair center, so the walkable surface must reach every row's aisle. You do **not** need walkable NavMesh *between* tightly packed seats.
5.  Click **Bake** on the `NavMeshSurface` component. A blue overlay will represent the walkable pathing zone between your lobby, ticket booth, cinema hall aisles, and exit. Re-bake whenever you add or move chairs.

---

## Step 4: Setup the Waypoints Authority
Create an empty GameObject in the scene named `CinemaWaypoints`:
1.  Attach the [CinemaWaypoints](file:///C:/Users/Rahat/Projects/CinemaTycoon/Assets/_CinemaTycoon/Scripts/Core/CinemaWaypoints.cs) component to it.
2.  Under it, create child empty GameObjects and position them in your scene:
    *   **Spawn Point**: Where customers appear (near the lobby entrance).
    *   **Exit Point**: Where customers go when leaving (outside/exit doors).
    *   **Ticket Booth**: Where the cashier stands.
    *   **Popcorn Stand**: The concession stand customers visit *after* buying a ticket. Place it in the lobby, on the NavMesh, somewhere between the ticket booth and the hall entrance. The stand is **self-serve** (no staff role required) — customers walk to it, instantly "buy" popcorn, and continue to the hall. **If left unassigned, the popcorn detour is skipped entirely** and customers go straight from the ticket booth to a chair.
    *   **Queue Points**: Position 4-6 empty GameObjects in a neat line in front of the Ticket Booth. Assign these to the `Queue Points` array in the component.
    *   **Cashier Station / Janitor Station / Guard Station**: Stations where staff sit or idle when not busy.
    *   **Cashier Work Point**: A dedicated, navmesh-reachable spot the Cashier paths to and idles at while on duty. Place this **near** the ticket booth but in an open, obstacle-free area — do **not** point it at the booth transform itself (the booth is often surrounded by colliders that block the agent and force long detours). Customers still path to **Ticket Booth** for purchases; the cashier stands at **Cashier Work Point**. If left unassigned, the cashier falls back to **Cashier Station**.
3.  Wire all these transforms into their corresponding slots in the `CinemaWaypoints` Inspector.

> **Note:** There is no longer a `Hall Seats` array on `CinemaWaypoints`. Hall seating is now driven by the chair hierarchy discovered dynamically in **Step 5**. Customers reserve the nearest free chair at watch-time.

---

## Step 5: Setup the Hall Chairs (Seat Triggers)
The hall seat system replaces the old manual `Hall Seats` transform array. It uses your existing `OccupiedChair` prefab and a `ChairLogicHandler` parent that auto-discovers every chair in the hall.

### 1. The `OccupiedChair` Prefab
The prefab at `Assets/Prefab/OccupiedChair.prefab` already contains:
*   An [OccupiedChairLogic](file:///C:/Users/Rahat/Projects/CinemaTycoon/Assets/Scripts/ChairLogic/OccupiedChairLogic.cs) component (the seat authority — handles reservation, the approach point, sitting-character visibility, and release).
*   The chair mesh (visual only — no collider, so it does not carve the NavMesh).
*   **9 character child GameObjects**, all **inactive by default**. When a customer sits, `OccupiedChair.OccupyChair()` picks one at random and `SetActive(true)`; on wake, `UnOccupyChair()` deactivates it. You do **not** need to touch these children.

### 2. Configure the Approach Point (important for tight rows)
Because cinema seats are packed with little space between them, a customer cannot (and should not) path to the chair's center. Instead each chair exposes an **approach point** — the world position the `NavMeshAgent` walks to before sitting. It is defined on `OccupiedChairLogic`:

*   **`Approach Offset`** (Vector3, local space, default `(0, 0, -0.6)`): a point 0.6 m behind the chair, i.e. toward the row's entry aisle (assuming the chair's +Z faces the screen). Tune per your layout:
    *   If your aisle is in **front** of the chairs (chairs face away from the aisle), flip the sign to `(0, 0, 0.6)`.
    *   Increase the magnitude (e.g. `-1.0`) if your aisle is wider so the agent stands fully in the aisle.
*   **`Approach Point`** (optional Transform): for awkward chairs where an offset isn't enough, drop a child empty GameObject into the aisle and assign it here. When set, it overrides `Approach Offset`.

To verify placement: select any `OccupiedChair` instance in the Scene. A green sphere + line is drawn (Scene view) showing the approach point relative to the chair. Confirm the sphere lies on the baked NavMesh (blue). If it floats off-mesh, adjust the offset or assign an explicit `Approach Point`.

> **Why this matters:** if the approach point is off the NavMesh, the agent gets no path and the customer will stand in place until the show ends (then leave) rather than sit — the `hasPath` guard in `WatchingState` prevents an instant-teleport-sit. The green gizmo is how you catch this before playing.

### 3. Place the Chair Hierarchy
1.  Create an empty GameObject in the hall named `ChairLogicHandler` (or reuse the one already present in `Demo.unity`).
2.  Attach the [ChairLogicHandler](file:///C:/Users/Rahat/Projects/CinemaTycoon/Assets/Scripts/ChairLogic/ChairLogicHandler.cs) component to it.
3.  Drag instances of the `OccupiedChair` prefab into the hall as children of the `ChairLogicHandler` object. Position them in rows facing the screen. (Demo.unity already ships with ~220 instances; add/remove to taste.)
4.  **No manual list assignment is needed.** `ChairLogicHandler` populates its `chairs` list automatically in `Awake` via `GetComponentsInChildren<OccupiedChairLogic>(true)`. Leave the `Chairs` list empty in the Inspector — it fills at runtime.
5.  Confirm every chair's character children are inactive in the prefab/instances (they are by default). Customers will enable one when they sit.

### 4. How It Works at Runtime
*   When a customer enters the `WatchingState`, `ChairLogicHandler.ReserveNearestFree` finds the nearest free chair (by distance to the customer) and reserves it (atomically marking it non-free).
*   The customer's `NavMeshAgent` paths to that chair's **approach point** (in the aisle — see section 2), not the chair center.
*   On arrival (`Agent.remainingDistance < Chair Arrival Distance`, a serialized field on the Customer, default `0.8`, and only when the agent actually has a path), the customer calls `SitOnChair()`:
    *   `OccupiedChair.OccupyChair()` shows one sitting-character child at the chair.
    *   The walking **Customer GameObject is disabled** (`SetActive(false)`) for the whole show — it is reused, not destroyed. (The customer visually transfers from the aisle to the seated pose — standard for this kind of tycoon.)
    *   The Customer subscribes to `ScheduleManager.OnShowEnded` (a static C# event that fires even on inactive GameObjects).
*   When the show ends, the same Customer instance wakes (`WakeFromChair()`): it re-enables, hides the sitting child, releases the chair, applies a lump satisfaction gain for the time seated, and walks to the exit.
*   If no chair is free when a customer wants to watch, they go to the `Unsatisfied` state (cinema rating drops). Keep `maxConcurrentCustomers` on the `CustomerSpawnManager` at or below the chair count to avoid guaranteed churn.
*   If a reserved chair's approach point is off the NavMesh (no path), the customer will not sit and will leave when the show ends — verify approach points with the green gizmo (section 2).

---

## Step 6: Setup Prefabs

### 1. Customer Prefab
1.  In the scene, create a 3D Capsule.
2.  Rename it to `CustomerPrefab` and add a `NavMeshAgent` component (set radius to `0.4`, speed to `2.5`).
3.  Add the [Customer](file:///C:/Users/Rahat/Projects/CinemaTycoon/Assets/_CinemaTycoon/Scripts/Customers/Customer.cs) component.
4.  (Optional) On the `Customer` component, tune `Chair Arrival Distance` (default `0.8`) — how close the agent must get to a chair's transform before it sits.
5.  Drag it into your Project window to turn it into a Prefab, then delete it from the scene hierarchy.

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

### 3. Spill Decal Prefab
A Spill event now drops a physical decal on the floor so the player can see *where* the mess is. The decal is a simple flat quad with the `SpillDecal` component attached; the EventManager spawns it when a spill triggers and the `SpillDecal` script handles its own fade-in and fade-out.

1.  In the Project window, right-click $\rightarrow$ **Create** $\rightarrow$ **3D Object** $\rightarrow$ **Quad**. Rename it `SpillDecal`.
2.  Create a material for the spill (e.g. `M_Spill_Dirt`) with a brown/dark splat texture, a transparent/diffuse alpha, and **Render Queue = Transparent** (or use URP's `Universal Render Pipeline/Lit` with the Surface Type set to Transparent).
3.  Apply the material to the `SpillDecal` quad. Rotate the quad **-90° on the X axis** so it lies flat on the floor (the `SpillDecal` component will re-apply this rotation at runtime if `Align To Floor` is enabled, so the rotation in the prefab is just for the editor preview).
4.  Add the [SpillDecal](file:///Users/gamerangers/Documents/GitHub/CinemaTycoon/Assets/_CinemaTycoon/Scripts/Events/SpillDecal.cs) component to the quad.
5.  Drag the `SpillDecal` quad into the Project window to turn it into a Prefab, then delete the original from the scene hierarchy.
6.  The EventManager will reference this prefab (see Step 7). If you leave the `Spill Decal Prefab` slot on `EventManager` empty, the gameplay loop still works — spills just become invisible events.

> **Why a custom component?** The `SpillDecal` script handles the lifecycle so the EventManager doesn't have to: it fades the alpha in on spawn (so the spill doesn't pop in) and fades it out when the janitor cleans the floor (so the cleanup isn't jarring). It also writes the fade animation through both `_BaseColor` (URP) and `_Color` (built-in / unlit) so it works with either shader.

---

## Step 7: Setup the Game Managers
Create an empty GameObject in your scene named `GameManager`:
1.  Attach the following components to it:
    *   [GameManager](file:///C:/Users/Rahat/Projects/CinemaTycoon/Assets/_CinemaTycoon/Scripts/Core/GameManager.cs)
    *   [EconomyManager](file:///C:/Users/Rahat/Projects/CinemaTycoon/Assets/_CinemaTycoon/Scripts/Economy/EconomyManager.cs)
    *   [CustomerSpawnManager](file:///C:/Users/Rahat/Projects/CinemaTycoon/Assets/_CinemaTycoon/Scripts/Customers/CustomerSpawnManager.cs)
    *   [ScheduleManager](file:///C:/Users/Rahat/Projects/CinemaTycoon/Assets/_CinemaTycoon/Scripts/Schedule/ScheduleManager.cs)
    *   [StaffManager](file:///C:/Users/Rahat/Projects/CinemaTycoon/Assets/_CinemaTycoon/Scripts/Staff/StaffManager.cs)
    *   [EventManager](file:///C:/Users/Rahat/Projects/CinemaTycoon/Assets/_CinemaTycoon/Scripts/Events/EventManager.cs)
    *   [CheatManager](file:///C:/Users/Rahat/Projects/CinemaTycoon/Assets/_CinemaTycoon/Scripts/DevTools/CheatManager.cs)

    > The pause / settings / game-over flow and all HUD plumbing live on the `UIToolkitHUDBindings` component (see Step 9). There is no separate `SceneFlowManager` component.

2.  Configure the components in the Inspector:
    *   **GameManager**: Drag all manager components on the same GameObject into their respective slots.
    *   **EconomyManager**: Drag the 4 Upgrade assets you created in Step 1 into `Available Upgrades`.
    *   **CustomerSpawnManager**: Drag your `CustomerPrefab` into the `Customer Prefab` slot. Keep `Max Concurrent Customers` at or below the number of hall chairs (see Step 5) so customers don't churn to `Unsatisfied` when all chairs are taken.
    *   **ScheduleManager**: Drag the Movie assets you created in Step 1 into `Available Movies`.
    *   **StaffManager**: Drag the Staff Role assets into their slots, and assign the `StaffPrefab` to the `Staff Prefab` slot.
    *   **EventManager**: Create a few empty GameObjects in your lobby as possible spill locations, and drag them into the `Possible Spill Locations` array. Then drag the `SpillDecal` prefab you created in **Step 6.3** into the `Spill Decal Prefab` slot — when a spill triggers, the decal will be instantiated at the chosen location and fade out again when the janitor cleans it. Leave the slot empty to run spills without visuals.

---

## Step 8: Setup the Main Camera & Camera Controller
Select your **Main Camera** in the hierarchy:
1.  Attach the [FlyCameraController](file:///C:/Users/Rahat/Projects/CinemaTycoon/Assets/_CinemaTycoon/Scripts/Core/FlyCameraController.cs) component to the camera.
2.  Position and rotate the camera in the scene view to define the menu focal anchor point (the starting view when the game loads).

---

## Step 9: Setup UI Toolkit Layout
1.  Create an empty GameObject in the scene named `UI_HUD`.
2.  Attach a **UIDocument** component to it.
3.  Assign the [HUDLayout.uxml](file:///C:/Users/Rahat/Projects/CinemaTycoon/Assets/_CinemaTycoon/UI/Toolkit/HUDLayout.uxml) layout asset into the **Source Asset** slot on the `UIDocument`.
4.  Attach the [UIToolkitHUDBindings](file:///C:/Users/Rahat/Projects/CinemaTycoon/Assets/_CinemaTycoon/Scripts/UI/UIToolkitHUDBindings.cs) component to the GameObject.
5.  Assign the [EventPopupTemplate.uxml](file:///C:/Users/Rahat/Projects/CinemaTycoon/Assets/_CinemaTycoon/UI/Toolkit/EventPopupTemplate.uxml) template into the **Notification Template** slot on the script.
6.  (Optional but recommended) Assign your HUD `.uss` stylesheet into the **Hud Style Sheet** slot on `UIToolkitHUDBindings`. If left empty the script logs a warning and the UI runs unstyled.

---

## Step 10: Play & Test
Press **Play** in Unity:
1.  The game will load in **Main Menu State** paused (`Time.timeScale = 0f`). The camera will bob and pan programmatically to showcase the scene.
2.  Click **START GAME** in the UI Toolkit overlay. The menu will vanish, the HUD will display, the simulation will unpause, and the camera will enter WASD flight controls.
3.  Press `Escape` to toggle the **Pause** overlay. While flying (cursor locked), hold **`Alt`** to release the mouse cursor so you can interact with HUD buttons (hire staff, schedule movies, buy upgrades); release `Alt` to re-lock the cursor for mouse-look. Click back on the viewport if needed.
4.  **Hall seating flow:** customers spawn → queue → purchase a ticket → if a popcorn stand waypoint is assigned **and** the customer rolled a "wants popcorn" check (40% base, scaled by the `PremiumPopcorn` upgrade), they detour to the popcorn stand, instantly "buy" popcorn (balance += base price × `PopcornRevenueMultiplier`), then walk to the **nearest free chair** → the walking customer disappears and one of the chair's sitting-character children appears. When the scheduled movie ends, the same customer instance stands back up, walks to the exit, and despawns; the chair becomes free again.
5.  **Spill cleanup flow:** every 25–45 seconds the EventManager may roll a Spill. A `SpillDecal` is spawned on the lobby floor at one of the `Possible Spill Locations` (fade-in over 0.4s), the rating starts a 30-second countdown, and a janitor task is auto-enqueued. The nearest idle janitor walks to the decal, plays its `Work` animation, and on completion the decal fades out and the rating is restored. If the timer expires first, the decal still tears down (no leaking wet floor) and the rating takes a penalty.
6.  Press `F1` at runtime to open the Cheat Panel, check your balance/satisfaction rating, and inject cash if needed.
