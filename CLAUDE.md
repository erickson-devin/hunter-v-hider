# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Hunter v Hider: a top-down asymmetric multiplayer tactical game. Unity **6000.5.6f1**, URP, Netcode for GameObjects (NGO) over Unity Relay. The main scene is `Assets/_Project/Scenes/Tactical_Main.unity`, which is the only scene in the build. Game code lives in `Assets/_Project/Scripts` (namespaces `HunterVsHider.*`). There are no asmdefs, so everything compiles into `Assembly-CSharp` / `Assembly-CSharp-Editor`.

## Game design

**Teams:**
- **Police:** the hunters. They breach into the map and search it with limited vision.
- **Assassin(s):** the hiders. They set up during the prep phase, then ambush.

**Player count:** up to 10 (5v5). The intended best experience is **3 Police vs 1 Assassin**. **Current focus: get the game working as 1 Assassin vs many Police.** `MatchManager.ExecuteRoleAssignment` always picks exactly one Assassin. Don't add support for multiple Assassins unless asked; that comes later.

**Win conditions:** a team wins when every member of the other team has been eliminated. The Assassin side wins by killing all the Police, and the Police win by killing all the Assassins.

**Match flow:**
1. **Lobby:** the host creates a Relay session and others join with the code. The map seed and size are chosen here.
2. **Role assignment:** the server assigns roles.
3. **Prep phase:**
   - **Assassin:** gets a top-down god view of the whole map to scout it and pick a spawn point. They **set traps** and **build hiding places from the movable objects available to them**.
   - **Police:** wait in staging areas outside the map with their vision suppressed. They pick a loadout and choose a breach room to enter from.
   - The minimap is hidden during this phase.
4. **Combat phase:** players are teleported into position, the Police breach, and fog of war and the minimap turn on.
5. **Match end:** the win conditions above decide it.

**Vision is the core mechanic.** Each side has its own fog-of-war and stealth rules:
- **Police:** see through wall-occluded vision cones. The cone's angle and range depend on the equipped weapon. Explored areas stay dimmed, and enemies are only shown while they're in someone's live line of sight.
- **Assassin:** has a separate fog and stealth ruleset.

**Loadouts:**
- **Police:** three slots (rifle, shotgun, pistol) with raycast firearms, ammo and reloads, plus sprint.
- **Assassin:** a melee slash and networked throwing knives.

**Current state:** a gray-box prototype with placeholder shaders, synthesized SFX and target dummies. Traps, movable hiding objects, the Assassin's stealth rules and the win/match-end logic are part of the design, but they're only partly built or not built yet. Check the code before assuming any of them exist.

## Working with the Editor (Unity CLI + Pipeline bridge)

The project has `com.unity.pipeline` installed, so a running Editor can be driven from the shell. If an Editor is open, prefer driving it over hand-editing `.unity`/`.prefab`/`.asset` YAML.

```powershell
unity status --project-path "E:\HvH\hunter-v-hider"            # look for state "ready"
unity recompile --project-path "E:\HvH\hunter-v-hider" --format json   # compile + list C# errors
unity command --project-path "E:\HvH\hunter-v-hider"           # list Editor commands
unity command script eval --project-path "E:\HvH\hunter-v-hider" '<C# expression>'
unity open "E:\HvH\hunter-v-hider"                             # launch the Editor
```

- After editing C#, run `unity recompile` to check that it compiles. Unity reports errors in stages, so fixing syntax errors can uncover semantic ones.
- If the project has compile errors when the Editor starts, it stops at the "Enter Safe Mode?" prompt and the bridge can't connect. `unity status` reports this. Read the errors from `Logs/Editor.log` (`grep "error CS"`), fix them, then reopen.
- **Build:** use the menu item `Tools/Hunter v Hider/Build Windows Standalone` (in `Assets/Editor/SetupTaskRelay.cs`). It builds StandaloneWindows64 to `Builds/HunterVsHider.exe`.
- **Tests:** there are none. `com.unity.test-framework` is installed but has no test assemblies. Verification is done by the `Verify …` Editor menu items (see below) and by Play mode.
- **Multiplayer testing:** ParrelSync (`com.veriorpies.parrelsync`) runs clone Editors. `Assets/Editor/ParrelSyncSuppressor.cs` stops clones from saving assets or scenes. Never make changes from inside a clone.

## Architecture

### Match flow (server-authoritative)
`Managers/MatchManager` (NetworkBehaviour singleton, `Instance` == `Singleton`) owns the game state as `NetworkVariable`s: `currentMatchState`, `selectedMapSize`, `mapGenerationSeed`. The states are `WaitingForPlayers → RoleAssignment → PrepPhase → CombatPhase → MatchEnd`. Only the server calls `SetMatchState`, which runs the entry logic for each state: a weighted roll that picks exactly one Assassin (everyone else becomes Police), then teleports for the prep and combat phases. Clients ask for changes with `[Rpc(SendTo.Server)]` calls (`RequestStartCombatPhaseServerRpc`, `SelectBreachRoomServerRpc`, `ConfirmAssassinSpawnServerRpc`, `RequestGenerateNewMapServerRpc`). Most other systems gate their behaviour on `MatchManager.CurrentState` or listen to `OnMatchStateChanged`. Examples: the Assassin god-view camera during PrepPhase, the minimap hidden until CombatPhase, and vision suppression in the lobby and prep phases.

Per-player networked state lives in `Player/PlayerNetworkState` (`currentRole`, `selectedWeaponID`, `isSprinting`), together with `HealthComponent` and `PlayerWeaponManager`. Player movement is client-authoritative through `ClientNetworkTransform`.

### Networking / sessions
`Managers/RelayManager` signs in anonymously with Unity Authentication, creates or joins a Relay allocation by join code, and then calls `NetworkManager.StartHost/StartClient`. `RelayLobbyUI` / `UI/MainMenuUI` are the front ends.

### Procedural map
`Map/MapGenerator` (~2000 lines) builds a corridor-first arena with breach rooms deterministically from a seed. The server picks `mapGenerationSeed`, and **every peer generates the map locally** when the value changes, so generation must stay deterministic. Don't add unseeded randomness or iteration that depends on dictionary or hash ordering. Map size goes through `MapGenerator.ClampMapSize`. `GridManager` / `GridCellData` hold the occupancy grid, and `BreachRoom` marks Police entry points.

### Vision and fog of war (the most tangled area)
Several layers work together, and some of them came from different branches:
- `Vision/VisionController`: line-of-sight raycasts for each Police unit (`RayResults`, `ProximityResults`, `IsTargetInActiveVision`).
- `Player/FieldOfView`: builds the FOV mesh from the VisionController's ray results (`EnsureComponents`, `GenerateFOVMesh`, `CurrentMesh`).
- `Vision/FogOfWarManager`: stamps every FOV mesh into render textures with a CommandBuffer (`FoW_Stamp` / `FoW_Accumulate` shaders), keeps a persistent discovery buffer, sets the `_FogOfWarTex` / `_FoWArenaMin` / `_FoWArenaSize` shader globals, and drives a screen-darkness quad under the main camera. It also exposes `fovMaskRT` / `OnFoWTextureReady`, which `UI/MiniMapUI` consumes.
- `Vision/FogMemoryManager`: a separate memory/decay render-texture pipeline. When it is present, `FogOfWarManager.fovMaskRT` prefers its mask.
- `Vision/DynamicFOV`: networked per-weapon FOV profiles (angle and distance change with the equipped weapon).
- Shaders are in `Assets/_Project/Shaders` (stamp/accumulate/mask) and `Assets/_Project/Materials` (3-tier FoW, screen-space, minimap overlay, memory decay).
- `Gameplay/TargetVisibility` shows or hides enemies based on `FogOfWarManager.IsPositionVisible`.

### Weapons
`Weapons/Weapon` is the base class. It handles ammo, reloading, audio, and three fire modes chosen by `WeaponData.weaponType`: Firearm raycast, Melee slash, and Throwable launch. Tune weapons through `WeaponData` ScriptableObjects (`Create > HunterVsHider > Weapon Data`). `Gun` is a thin legacy subclass that builds a `WeaponData` at runtime if none is assigned. `PlayerWeaponManager` handles the Police slots (rifle, shotgun, pistol). `AssassinWeaponManager` and `ThrowingKnife`/`ThrowingKnifeProjectile` cover the Assassin's networked kit. Damage goes through `IDamageable`.

### Editor tooling (`Assets/Editor`)
Most scene setup is done in code, not by hand. Each `SetupTask*.cs` script builds or repairs part of `Tactical_Main` (NGO foundation, Relay, match zones, fog of war, minimap, loadout UI, map generator, and so on), and most have a matching `Verify …` item. They're under `Tools/Hunter v Hider/…`, `Tools/Setup Tasks/…`, and `HunterVsHider/…`. When you change a system, check whether a SetupTask script configures it. Rerunning that script may overwrite changes you made to the scene by hand.

## Known pitfalls

- **Duplicate class names across namespaces**, left over from the `Impliment-Netowrking` ↔ `main` merge:
  - `FieldOfView` exists in both `Player` and `Vision`. `Player.FieldOfView` is the mesh generator. `Vision.FieldOfView` is a facade over `DynamicFOV`.
  - `TargetVisibility` exists in both `Gameplay` and `Player`. The `Gameplay` one is the real one; the `Player` one is a stub.
  
  Code that imports both namespaces needs a fully qualified name or a `using X = …;` alias.
- That merge (`eb016a8`) concatenated several files instead of resolving them. If you hit odd duplicated members or a half-finished method, check it with `git diff eb016a8^1 eb016a8 -- <file>` and `git diff eb016a8^2 eb016a8 -- <file>`.
- Input: `activeInputHandler` is set to *Both*. Gameplay scripts use the legacy `UnityEngine.Input` API even though `com.unity.inputsystem` is installed.
- Many singletons find or create themselves lazily (`FindAnyObjectByType`, or by creating a GameObject). They guard against shutdown with static flags (such as `FogOfWarManager.IsShuttingDown`) and reset those flags in `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` so they work with Enter Play Mode options. Keep this pattern when you add a new singleton.

## Repo notes

- `.agents/skills/` is a vendored, engine-agnostic collection of game-dev skill docs. It is reference material, not project code.
- Third-party assets: `Assets/DynamicFog`, `Assets/TextMesh Pro`.
