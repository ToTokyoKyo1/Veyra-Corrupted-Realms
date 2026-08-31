# Veyra Project Structure

Veyra: Corrupted Realms is a portrait mobile 2D RPG. The current project scope is intentionally limited to `Hero01`, `Enemy01`, and `World01`.

## Asset folders

All game-owned assets live under `Assets/_Veyra/`:

```text
Assets/_Veyra/
|-- Animations/
|   |-- Hero01/
|   |-- Enemies/World01/Enemy01/
|   `-- UI/
|       `-- MainMenu/
|-- Art/
|   |-- Fonts/UI/Prototype/
|   |-- Materials/
|   `-- Sprites/
|       |-- Characters/Hero01/
|       |-- Enemies/World01/Enemy01/
|       |-- Environment/World01/
|       |-- UI/
|       |   |-- MainMenu/Prototype/
|       |   |-- Settings/Prototype/
|       |   `-- Battle/Prototype/
|       `-- VFX/
|-- Audio/
|   |-- Music/
|   `-- SFX/
|-- Data/
|   |-- Heroes/
|   |-- Enemies/World01/
|   |-- Combat/
|   `-- Worlds/World01/
|-- Prefabs/
|   |-- Characters/Hero01/
|   |-- Enemies/World01/Enemy01/
|   |-- Environment/World01/
|   |-- UI/
|   |   |-- MainMenu/
|   |   |-- Settings/
|   |   `-- Battle/
|   `-- VFX/Combat/
|-- Scenes/
|-- Scripts/
|   |-- Editor/
|   `-- Runtime/
|       |-- AI/
|       |-- Combat/
|       |   `-- Preview/
|       |-- Core/
|       |-- Data/
|       `-- UI/
|           |-- MainMenu/
|           `-- Settings/
`-- Settings/
```

- `Art` contains source visuals and materials, never behavior.
- `Animations` contains clips and controllers once production sprites exist.
- `Audio` separates music from sound effects.
- `Data` is reserved for authored game data when later phases require it.
- `Prefabs` contains reusable, persistent GameObjects.
- `Scenes` contains persistent Unity scenes.
- `Scripts/Runtime` contains code allowed in a player build.
- `Scripts/Editor` contains manual authoring tools and is excluded from player builds by its folder location.
- `Settings` is reserved for Veyra-owned configuration assets.

World02 through World10 must not be created during the current phase.

## Naming conventions

| Asset | Prefix | Example |
|---|---|---|
| Scene | `SCN_` | `SCN_BattlePrototype` |
| Prefab | `PF_` | `PF_Hero01_Placeholder` |
| Sprite | `SPR_` | `SPR_Hero01_Placeholder` |
| Animation Clip | `AN_` | `AN_Hero01_Idle` |
| Animator Controller | `AC_` | `AC_Hero01` |
| ScriptableObject | `SO_` | `SO_Hero01` |
| Material | `MAT_` | `MAT_Background` |
| Music | `MUS_` | `MUS_World01` |
| Sound effect | `SFX_` | `SFX_Confirm` |
| UI button | `BTN_` | `BTN_Attack` |
| UI text | `TXT_` | `TXT_Attack` |

Scripts, classes, and namespaces use PascalCase. Runtime code uses the root namespace `Veyra`; Editor code uses `Veyra.Editor`.

## Runtime behavior versus runtime authoring

Runtime behavior changes the state of objects that already exist, such as fitting an existing `RectTransform` to `Screen.safeArea`. Runtime authoring creates visual assets or hierarchies after Play Mode begins. Runtime authoring is prohibited: scenes, UI, characters, prefabs, and visual components must already be serialized in the project.

Future runtime code may instantiate a prefab that was authored and saved in advance. It must not assemble that prefab from empty GameObjects or discover dependencies with global searches or resource-path strings.

Phase 2 preview behavior is stricter: its visual effects are already-instantiated, inactive scene objects. Runtime code only toggles and moves those objects, changes existing text/color state, saves local preferences, and changes scenes.

## Create the Phase 01 foundation

1. Open the project in Unity `6000.5.5f1` and wait for script compilation to finish.
2. Choose **Tools > Veyra > Phase 01 > Create Project Foundation**.
3. Read the `[Veyra Phase 01]` summary in the Console.
4. If the tool reports preserved assets, inspect them rather than deleting or overwriting them.

The command is manual and idempotent. It creates missing folders and placeholder assets, but preserves assets that already exist.

## Verify the prototype

1. Open `Assets/_Veyra/Scenes/SCN_BattlePrototype.unity`.
2. Confirm that `SCN_BattlePrototype` contains `Main Camera`, `WorldRoot`, and `UIRoot` before entering Play Mode.
3. Confirm that the Canvas is Screen Space Overlay and its scaler uses a `1080 x 1920` reference resolution with a `0.5` width/height match.
4. Check the Game view at `360 x 640`, `390 x 844`, and `412 x 915` in portrait orientation.
5. Confirm that panels and all four action buttons remain inside `SafeArea`.
6. Enter and leave Play Mode. The hierarchy must remain unchanged, and the Console must contain no Missing Script or Missing Reference errors.
7. Confirm Player Settings use product name `Veyra: Corrupted Realms`, company `TokyoKyo`, identifier `com.totokyokyo.veyra`, Portrait orientation, and no landscape or upside-down autorotation.

The Phase 01 prototype is static. Combat and all Phase 2 systems are intentionally absent.

## Phase 02 menu and battle preview

Phase 2 adds `SCN_MainMenu` and the draft shell `SCN_W01_L01_Tutorial`. The build order starts with those two scenes while preserving `SampleScene` and `SCN_BattlePrototype` afterward.

Use **Tools > Veyra > Phase 02 > Create Main Menu And Battle Preview** to create missing persistent assets. Use **Validate Phase 02** for Edit Mode checks and **Validate Phase 02 With Play Mode** for the complete menu/settings/four-effects/return flow. Existing Phase 2 scenes and prefabs are preserved instead of overwritten.

See `Docs/PHASE_02_MENU_AND_BATTLE_PREVIEW.md` for scene hierarchies, palette, PlayerPrefs keys, placeholder assets, validation, and the boundary between this visual preview and the future Phase 3 combat system.
