# Veyra Campaign Core

## Source of truth

`CampaignLevelCatalog` contains the ten visible World01 slots. Levels 1–4 are
implemented; levels 5–10 are inert `PROSSIMAMENTE` placeholders with no scene,
enemy roster or reward. Every level and enemy uses a stable constant from
`CampaignContentIds`.

Menu and navigation code should query `CampaignLevelCatalog` instead of keeping
parallel arrays of titles, scene names or prerequisites.

## Save format

`CampaignProgressStore` writes `Veyra.Campaign.Progress`, currently version 3.
The serialized data uses lists because `JsonUtility` does not serialize ordinary
dictionaries:

- `levelRecords`: completion, one-time reward and completion count by level ID;
- `moralDecisions`: `Saved`/`Killed` by level ID and enemy ID;
- `tutorialRecords`: whether a contextual explanation was already seen;
- `playerActionProfile`: totals and the last 20 valid player actions.

The scalar fields `tutorialCompleted`, `encounter02Resolved`,
`encounter03Resolved` and the three Level 4 outcomes are compatibility mirrors.
New code must prefer stable-ID APIs, but old scenes can continue reading those
fields during the transition.

## Migration guarantees

Loading a valid v1/v2 JSON save:

1. deep-copies and normalizes all collections;
2. converts legacy completions and moral results to stable-ID records;
3. preserves already-earned rewards and the prerequisite chain;
4. synchronizes the legacy mirrors;
5. writes version 3 only after successful parsing.

`CampaignProgressStore.Migrate(data)` performs the same conversion without
touching `PlayerPrefs`. It is idempotent and is used by the editor validator.
Malformed JSON is not overwritten. `CampaignProgressStore.Reset()` remains the
single full campaign reset and also clears Hero01 progress; UI must protect that
call with an explicit confirmation.

## Moral decisions and rewards

Use:

- `SetTutorialResolution(resolution)` for the Tutorial enemy;
- `SetEncounterResolution(encounter, resolution)` for Levels 2 and 3;
- `SetEnemyResolution(levelId, enemyId, resolution)` for each Level 4 enemy;
- `TryGetEnemyResolution(...)` to render the current saved story;
- `IsLevelRewardClaimed(...)` to label replay rewards.

`Set...Resolution` may replace an existing result after replay confirmation.
When the final required decision for a level is written, the first completion
and reward are committed automatically. Later replacements update the story but
do not award XP again. Do not call `HeroProgressStore.RecordFirstClear` after a
successful `Set...Resolution`; the store already coordinates it.

## Persistent player-action profile

After a valid combat action, map the controller-specific enum to
`PlayerCombatAction` and call `RecordPlayerAction`. A controller whose enum uses
the same English member names can alternatively call
`TryRecordPlayerAction(action.ToString())`.

`GetPlayerActionProfile()` exposes totals, ratios, dominant action, recent
actions and the current repetition length. `CanEnemiesUsePlayerProfile(3)` (or a
higher campaign level) becomes true only after at least three recorded actions.
This is a small deterministic tendency model, not perfect prediction.

## Combat presentation and phases

The three existing controllers remain separate because Tutorial, single-enemy
encounters and the Level 4 roster have genuinely different rules. They share a
consistent visible sequence: introduction, target/action choice, player
resolution, enemy resolution, incapacitation, moral choice, then victory or
defeat. Buttons are disabled while an action or scene load is resolving. A
single-enemy encounter skips target selection; Level 4 never does so while two
or more valid enemies remain.

An enemy at zero HP is `INCAPACITATO`, not dead. It loses its intent, stops
acting, cannot be selected or damaged, and stays visible until its moral result
is confirmed. Navigation offers `RIPROVA` after defeat and never writes campaign
progress merely because a level was loaded, abandoned or lost.

## Level 4 target and moral flow

`MultiEnemyBattleState` owns the selected stable enemy ID. Attack, Technique and
Analyze require a valid target; Guard remains self-targeted. Both the authored
enemy card and the authored actor click target the same controller method. When
only one enemy remains, it can be selected automatically and the UI names that
enemy explicitly.

After all three enemies are incapacitated, `MultiEnemyBattleController` presents
one profile at a time. The player temporarily selects Save/Kill for Bruto,
Veglia and Maschera, then reviews all three results. Nothing is persisted until
the final confirmation. A replay preloads existing results, permits review and
change, warns when the saved story will change, and still cannot re-award XP.

## Isolated manual QA

Use `Tools > Veyra > QA > Begin Isolated Progress Session` before a destructive
manual progression test. It backs up both game progress keys outside
`PlayerPrefs`, starts with empty test progress, and restores the real values when
the interrupted session is recovered or when `Restore Real Progress` is run.
It does not touch audio, display or other local options.

## Validation

Run `Veyra > Validate Campaign Core` in the Unity Editor. The validator is pure:
it checks catalog integrity, v2->v3 migration, idempotence, replay authority and
the 20-action bound without reading or changing the player's `PlayerPrefs`.
