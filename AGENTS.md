# Veyra: Corrupted Realms - Repository Rules

## Project scope

- Veyra: Corrupted Realms is a portrait 2D RPG for mobile, built with Unity's Universal 2D template.
- Work only with `Hero01` and `World01` until the project owner explicitly expands the scope.
- Do not create content, folders, data, or systems for World02 through World10.
- Do not add speculative systems for future features.

## Persistent authoring

- Scenes, prefabs, Canvases, Animator Controllers, Animation Clips, sprites, and other authored assets must be persistent project files before Play Mode starts.
- Never perform visual authoring at runtime. Runtime code must not build scene or UI hierarchies with `new GameObject`, `AddComponent`, `CreatePrimitive`, runtime initialization hooks, or bootstrap scripts.
- Generation tools must be Editor-only, started manually, safe to run repeatedly, and must save their results through Unity Editor APIs.
- Runtime behavior may control existing scene objects and may instantiate already-authored prefabs when a feature actually requires it.

## Asset boundaries

- Keep all Veyra assets under `Assets/_Veyra/`.
- Keep sprites separate from animations.
- Keep prefabs separate from sprites, animations, and scripts.
- Preserve identical canvas dimensions and pivots across every frame of the same sprite animation.
- Do not add Assembly Definitions or packages without explicit authorization.
- Do not modify or delete existing `.meta` files.

## Dependency rules

- Prefer explicit `[SerializeField]` references assigned in the Inspector.
- Do not use `GameObject.Find`, `FindObjectOfType`, `Resources.Load`, or string paths to discover runtime dependencies.
- Do not hide dependencies behind global service locators, dependency-injection containers, ECS, or global event buses.

## Naming

- Scenes: `SCN_`
- Prefabs: `PF_`
- Sprites: `SPR_`
- Animation Clips: `AN_`
- Animator Controllers: `AC_`
- ScriptableObjects: `SO_`
- Materials: `MAT_`
- Music: `MUS_`
- Sound effects: `SFX_`
- UI buttons: `BTN_`
- UI text: `TXT_`
- Use PascalCase for scripts, classes, and namespaces. The root namespace is `Veyra`.

## Version control

- Preserve existing project and URP assets unless a requested change requires otherwise.
- Never track `Library`, `Temp`, `Logs`, `Obj`, `Builds`, or `UserSettings`.
- Never commit secrets, tokens, or credentials.

