using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Veyra.Combat.Tactical
{
    public enum TacticalEnemyMovementStyle
    {
        Aggressive,
        Patient,
        Deceptive
    }

    [Serializable]
    public sealed class TacticalUnitBinding
    {
        public string unitId;
        public Transform actor;
        public SpriteRenderer visual;
        [Range(0, 3)] public int startRow;
        [Range(0, 5)] public int startColumn;
        public bool isHero;
        public bool isFlying;
        public bool sourceSpriteFacesRight = true;
        [Min(0.5f)] public float targetVisualHeight = 2.1f;
        [Min(0f)] public float flyingHeight = 0.55f;
        public SpriteRenderer shadow;
    }

    [DisallowMultipleComponent]
    public sealed class TacticalBattlefieldController : MonoBehaviour
    {
        public const int RowCount = 4;
        public const int ColumnCount = 6;

        [Header("Persistent scene references")]
        [SerializeField] private TacticalPlatformView[] platforms = new TacticalPlatformView[24];
        [SerializeField] private TacticalUnitBinding[] units = Array.Empty<TacticalUnitBinding>();
        [SerializeField] private Button moveButton;
        [SerializeField] private Button endTurnButton;
        [SerializeField] private TMP_Text feedbackText;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private UnityEvent endTurnRequested = new UnityEvent();

        [Header("Presentation")]
        [SerializeField] private float movementDuration = 0.18f;
        [SerializeField] private Color normalPlatformColor = new Color(0.10f, 0.19f, 0.27f, 1f);
        [SerializeField, Min(1)] private int attackPreviewRange = 1;
        [SerializeField, Min(1)] private int techniquePreviewRange = 2;

        private readonly Dictionary<Transform, Vector2Int> coordinates =
            new Dictionary<Transform, Vector2Int>();
        private readonly Dictionary<Vector2Int, Transform> occupants =
            new Dictionary<Vector2Int, Transform>();
        private readonly HashSet<Vector2Int> reachable = new HashSet<Vector2Int>();
        private TacticalUnitBinding hero;
        private Transform selectedEnemy;
        private bool heroTurn;
        private bool movementUsed;
        private bool actionUsed;
        private bool moveMode;
        private bool moving;

        public bool HeroTurn => heroTurn;
        public bool MovementUsed => movementUsed;
        public bool ActionUsed => actionUsed;
        public UnityEvent EndTurnRequested => endTurnRequested;
        public event Action<Transform> EnemySelected;

        private void Awake()
        {
            InitializeBoard();
            BeginHeroTurn();
        }

        private void Update()
        {
            if (moving || worldCamera == null || Pointer.current == null ||
                !Pointer.current.press.wasPressedThisFrame)
            {
                return;
            }

            if (Pointer.current == Mouse.current && EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Vector2 screenPosition = Pointer.current.position.ReadValue();
            Vector3 worldPosition = worldCamera.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, -worldCamera.transform.position.z));
            Collider2D[] hits = Physics2D.OverlapPointAll(worldPosition);
            TacticalPlatformView platform = hits
                .Select(hit => hit.GetComponent<TacticalPlatformView>())
                .FirstOrDefault(candidate => candidate != null);
            if (moveMode && platform != null)
            {
                TrySelectPlatform(platform.Row, platform.Column);
                return;
            }


            Transform selected = null;
            for (int index = 0; index < hits.Length && selected == null; index++)
            {
                Transform hitTransform = hits[index].transform;
                foreach (Transform candidate in coordinates.Keys)
                {
                    if (hero != null && candidate == hero.actor) continue;
                    if (hitTransform == candidate || hitTransform.IsChildOf(candidate))
                    {
                        selected = candidate;
                        break;
                    }
                }
            }

            if (selected == null && platform != null)
            {
                occupants.TryGetValue(new Vector2Int(platform.Column, platform.Row), out selected);
                if (hero != null && selected == hero.actor) selected = null;
            }

            if (selected != null)
            {
                SetSelectedEnemy(selected);
                EnemySelected?.Invoke(selected);
            }
        }

        public void InitializeBoard()
        {
            coordinates.Clear();
            occupants.Clear();
            hero = null;

            for (int index = 0; index < platforms.Length; index++)
            {
                if (platforms[index] != null)
                {
                    platforms[index].SetNormalColor(normalPlatformColor);
                }
            }

            for (int index = 0; index < units.Length; index++)
            {
                TacticalUnitBinding unit = units[index];
                if (unit == null || unit.actor == null)
                {
                    continue;
                }

                Vector2Int coordinate = new Vector2Int(unit.startColumn, unit.startRow);
                coordinates[unit.actor] = coordinate;
                occupants[coordinate] = unit.actor;
                if (unit.isHero)
                {
                    hero = unit;
                }

                NormalizeVisual(unit);
                SnapUnit(unit, coordinate);
            }

            selectedEnemy = FindNearestLivingEnemy();
            UpdateFacing();
            RefreshHighlights();
        }

        public void BeginHeroTurn()
        {
            heroTurn = true;
            movementUsed = false;
            actionUsed = false;
            moving = false;
            CancelMoveMode();
            SetFeedback("TUO TURNO · PUOI MUOVERTI E POI USARE UN'AZIONE");
            RefreshButtons();
            UpdateFacing();
            RefreshHighlights();
        }

        public void SetSelectedEnemy(Transform target)
        {
            if (target != null && coordinates.ContainsKey(target))
            {
                selectedEnemy = target;
                UpdateFacing();
                RefreshHighlights();
                int distance = GetDistance(hero != null ? hero.actor : null, target);
                SetFeedback(
                    $"BERSAGLIO · DISTANZA {distance} · ATTACCO {attackPreviewRange} · TECNICA {techniquePreviewRange}");
            }
        }

        public void ToggleMoveMode()
        {
            if (!heroTurn || actionUsed || movementUsed || moving || hero == null)
            {
                SetFeedback(movementUsed
                    ? "MOVIMENTO GIÀ USATO IN QUESTO TURNO"
                    : "MOVIMENTO NON DISPONIBILE");
                return;
            }

            if (moveMode)
            {
                CancelMoveMode();
                SetFeedback("MOVIMENTO ANNULLATO");
                return;
            }

            moveMode = true;
            reachable.Clear();
            Vector2Int origin = coordinates[hero.actor];
            for (int deltaRow = -1; deltaRow <= 1; deltaRow++)
            {
                for (int deltaColumn = -1; deltaColumn <= 1; deltaColumn++)
                {
                    if (deltaColumn == 0 && deltaRow == 0)
                    {
                        continue;
                    }

                    AddReachable(origin + new Vector2Int(deltaColumn, deltaRow));
                }
            }
            RefreshHighlights();
            SetFeedback("SCEGLI UNA PEDANA VERDE ADIACENTE");
        }

        public void TrySelectPlatform(int row, int column)
        {
            Vector2Int destination = new Vector2Int(column, row);
            if (!moveMode || !reachable.Contains(destination) || moving || hero == null)
            {
                return;
            }

            StartCoroutine(MoveHero(destination));
        }

        public bool CanUseOffensiveAction(int maximumRange, Transform explicitTarget = null)
        {
            Transform target = explicitTarget != null ? explicitTarget : selectedEnemy;
            if (!heroTurn || actionUsed || moving || hero == null || target == null ||
                !coordinates.TryGetValue(hero.actor, out Vector2Int heroCoordinate) ||
                !coordinates.TryGetValue(target, out Vector2Int targetCoordinate))
            {
                SetFeedback("BERSAGLIO NON VALIDO");
                return false;
            }

            int distance = Mathf.Max(
                Mathf.Abs(heroCoordinate.x - targetCoordinate.x),
                Mathf.Abs(heroCoordinate.y - targetCoordinate.y));
            if (distance > maximumRange)
            {
                SetFeedback($"FUORI PORTATA · DISTANZA {distance} · PORTATA {maximumRange} · USA MUOVI");
                return false;
            }

            SetSelectedEnemy(target);
            return true;
        }

        public void CommitAction()
        {
            actionUsed = true;
            heroTurn = false;
            CancelMoveMode();
            RefreshButtons();
        }

        public void RequestEndTurn()
        {
            if (!heroTurn || actionUsed || moving)
            {
                return;
            }

            CommitAction();
            SetFeedback("TURNO PASSATO");
            endTurnRequested.Invoke();
        }

        public void NotifyBattleEnded()
        {
            heroTurn = false;
            actionUsed = true;
            CancelMoveMode();
            RefreshButtons();
        }

        public void RemoveUnitFromArena(Transform actor)
        {
            if (actor == null || !coordinates.TryGetValue(actor, out Vector2Int coordinate))
            {
                return;
            }

            coordinates.Remove(actor);
            occupants.Remove(coordinate);
            if (selectedEnemy == actor)
            {
                selectedEnemy = FindNearestLivingEnemy();
            }

            CancelMoveMode();
            UpdateFacing();
        }

        public IEnumerator MoveEnemyForPersonality(
            Transform actor,
            TacticalEnemyMovementStyle style)
        {
            if (actor == null || hero == null || hero.actor == null || moving ||
                !coordinates.TryGetValue(actor, out Vector2Int origin) ||
                !coordinates.TryGetValue(hero.actor, out Vector2Int heroCoordinate))
            {
                yield break;
            }

            Vector2Int destination = origin;
            int currentDistance = ChebyshevDistance(origin, heroCoordinate);
            int bestScore = style == TacticalEnemyMovementStyle.Aggressive
                ? int.MaxValue
                : int.MinValue;
            for (int rowDelta = -1; rowDelta <= 1; rowDelta++)
            {
                for (int columnDelta = -1; columnDelta <= 1; columnDelta++)
                {
                    Vector2Int candidate = origin + new Vector2Int(columnDelta, rowDelta);
                    if ((columnDelta == 0 && rowDelta == 0) || candidate.x < 0 ||
                        candidate.x >= ColumnCount || candidate.y < 0 || candidate.y >= RowCount ||
                        occupants.ContainsKey(candidate))
                    {
                        continue;
                    }

                    int distance = ChebyshevDistance(candidate, heroCoordinate);
                    int score = style == TacticalEnemyMovementStyle.Deceptive
                        ? (distance == currentDistance ? 100 : 0) + Mathf.Abs(rowDelta) * 10 - Mathf.Abs(columnDelta)
                        : distance;
                    bool better = style == TacticalEnemyMovementStyle.Aggressive
                        ? score < bestScore
                        : score > bestScore;
                    if (better)
                    {
                        bestScore = score;
                        destination = candidate;
                    }
                }
            }

            if (destination == origin ||
                (style == TacticalEnemyMovementStyle.Aggressive && currentDistance <= 1))
            {
                yield break;
            }

            TacticalUnitBinding binding = units.FirstOrDefault(unit => unit != null && unit.actor == actor);
            if (binding == null) yield break;
            moving = true;
            occupants.Remove(origin);
            occupants[destination] = actor;
            coordinates[actor] = destination;
            Vector3 actorStart = actor.position;
            Vector3 actorEnd = GetUnitWorldPosition(binding, destination);
            Vector3 shadowStart = binding.shadow != null ? binding.shadow.transform.position : Vector3.zero;
            TacticalPlatformView destinationPlatform = GetPlatform(destination);
            Vector3 shadowEnd = destinationPlatform != null
                ? destinationPlatform.transform.position + new Vector3(0f, 0.12f, 0f)
                : shadowStart;
            float elapsed = 0f;
            while (elapsed < movementDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = movementDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / movementDuration);
                actor.position = Vector3.Lerp(actorStart, actorEnd, t);
                if (binding.shadow != null)
                {
                    binding.shadow.transform.position = Vector3.Lerp(shadowStart, shadowEnd, t);
                }
                yield return null;
            }

            actor.position = actorEnd;
            if (binding.shadow != null) binding.shadow.transform.position = shadowEnd;
            moving = false;
            UpdateFacing();
            RefreshHighlights();
        }

        private IEnumerator MoveHero(Vector2Int destination)
        {
            moving = true;
            moveMode = false;
            reachable.Clear();
            RefreshHighlights();

            Vector2Int origin = coordinates[hero.actor];
            occupants.Remove(origin);
            occupants[destination] = hero.actor;
            coordinates[hero.actor] = destination;

            Vector3 start = hero.actor.position;
            Vector3 end = GetUnitWorldPosition(hero, destination);
            Vector3 shadowStart = hero.shadow != null ? hero.shadow.transform.position : Vector3.zero;
            TacticalPlatformView destinationPlatform = GetPlatform(destination);
            Vector3 shadowEnd = destinationPlatform != null
                ? destinationPlatform.transform.position + new Vector3(0f, 0.12f, 0f)
                : shadowStart;
            float elapsed = 0f;
            while (elapsed < movementDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = movementDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / movementDuration);
                hero.actor.position = Vector3.Lerp(start, end, t);
                if (hero.shadow != null)
                {
                    hero.shadow.transform.position = Vector3.Lerp(shadowStart, shadowEnd, t);
                }
                yield return null;
            }

            hero.actor.position = end;
            if (hero.shadow != null)
            {
                hero.shadow.transform.position = shadowEnd;
            }
            movementUsed = true;
            moving = false;
            SetFeedback("MOVIMENTO COMPLETATO · SCEGLI UN'AZIONE");
            RefreshButtons();
            UpdateFacing();
        }

        private void AddReachable(Vector2Int coordinate)
        {
            if (coordinate.x < 0 || coordinate.x >= ColumnCount ||
                coordinate.y < 0 || coordinate.y >= RowCount || occupants.ContainsKey(coordinate))
            {
                return;
            }

            reachable.Add(coordinate);
        }

        private void CancelMoveMode()
        {
            moveMode = false;
            reachable.Clear();
            RefreshHighlights();
        }

        private void RefreshHighlights()
        {
            for (int index = 0; index < platforms.Length; index++)
            {
                TacticalPlatformView platform = platforms[index];
                if (platform != null)
                {
                    Vector2Int coordinate = new Vector2Int(platform.Column, platform.Row);
                    bool selectedTarget = selectedEnemy != null &&
                                          coordinates.TryGetValue(selectedEnemy, out Vector2Int targetCoordinate) &&
                                          coordinate == targetCoordinate;
                    int heroDistance = hero != null && hero.actor != null &&
                                       coordinates.TryGetValue(hero.actor, out Vector2Int heroCoordinate)
                        ? ChebyshevDistance(heroCoordinate, coordinate)
                        : int.MaxValue;
                    bool occupiedByEnemy = occupants.TryGetValue(coordinate, out Transform occupant) &&
                                           occupant != null && hero != null && occupant != hero.actor;
                    bool threatened = IsThreatened(coordinate);
                    platform.ShowState(
                        reachable.Contains(coordinate),
                        selectedTarget,
                        occupiedByEnemy && heroDistance <= attackPreviewRange,
                        occupiedByEnemy && heroDistance > attackPreviewRange &&
                        heroDistance <= techniquePreviewRange,
                        threatened);
                }
            }
        }

        public int GetDistance(Transform from, Transform to)
        {
            return from != null && to != null &&
                   coordinates.TryGetValue(from, out Vector2Int fromCoordinate) &&
                   coordinates.TryGetValue(to, out Vector2Int toCoordinate)
                ? ChebyshevDistance(fromCoordinate, toCoordinate)
                : int.MaxValue;
        }

        private bool IsThreatened(Vector2Int coordinate)
        {
            for (int index = 0; index < units.Length; index++)
            {
                TacticalUnitBinding unit = units[index];
                if (unit == null || unit.isHero || unit.actor == null ||
                    !unit.actor.gameObject.activeInHierarchy ||
                    !coordinates.TryGetValue(unit.actor, out Vector2Int enemyCoordinate))
                {
                    continue;
                }

                if (ChebyshevDistance(enemyCoordinate, coordinate) <= 1)
                {
                    return true;
                }
            }

            return false;
        }

        private static int ChebyshevDistance(Vector2Int left, Vector2Int right)
        {
            return Mathf.Max(Mathf.Abs(left.x - right.x), Mathf.Abs(left.y - right.y));
        }

        private void RefreshButtons()
        {
            if (moveButton != null)
            {
                moveButton.interactable = heroTurn && !actionUsed && !movementUsed && !moving;
            }

            if (endTurnButton != null)
            {
                endTurnButton.interactable = heroTurn && !actionUsed && !moving;
            }
        }

        private void NormalizeVisual(TacticalUnitBinding unit)
        {
            if (unit.visual == null || unit.visual.sprite == null)
            {
                return;
            }

            float naturalHeight = unit.visual.sprite.bounds.size.y;
            if (naturalHeight > 0.001f)
            {
                float scale = unit.targetVisualHeight / naturalHeight;
                unit.visual.transform.localScale = new Vector3(scale, scale, 1f);
            }

            if (unit.shadow != null)
            {
                unit.shadow.transform.localScale = new Vector3(
                    unit.isFlying ? 1.0f : 1.15f,
                    unit.isFlying ? 0.30f : 0.36f,
                    1f);
            }
        }

        private void SnapUnit(TacticalUnitBinding unit, Vector2Int coordinate)
        {
            unit.actor.position = GetUnitWorldPosition(unit, coordinate);
            if (unit.shadow != null)
            {
                unit.shadow.transform.position = GetPlatform(coordinate).transform.position +
                                                 new Vector3(0f, 0.12f, 0f);
            }
        }

        private Vector3 GetUnitWorldPosition(TacticalUnitBinding unit, Vector2Int coordinate)
        {
            TacticalPlatformView platform = GetPlatform(coordinate);
            Vector3 basePosition = platform != null ? platform.transform.position : Vector3.zero;
            return basePosition + new Vector3(0f, 0.33f + (unit.isFlying ? unit.flyingHeight : 0f), 0f);
        }

        private TacticalPlatformView GetPlatform(Vector2Int coordinate)
        {
            for (int index = 0; index < platforms.Length; index++)
            {
                TacticalPlatformView platform = platforms[index];
                if (platform != null && platform.Column == coordinate.x && platform.Row == coordinate.y)
                {
                    return platform;
                }
            }

            return null;
        }

        private Transform FindNearestLivingEnemy()
        {
            if (hero == null || hero.actor == null || !coordinates.TryGetValue(hero.actor, out Vector2Int origin))
            {
                return null;
            }

            Transform best = null;
            int bestDistance = int.MaxValue;
            for (int index = 0; index < units.Length; index++)
            {
                TacticalUnitBinding candidate = units[index];
                if (candidate == null || candidate.isHero || candidate.actor == null ||
                    !candidate.actor.gameObject.activeInHierarchy ||
                    !coordinates.TryGetValue(candidate.actor, out Vector2Int coordinate))
                {
                    continue;
                }

                int distance = Mathf.Abs(origin.x - coordinate.x) + Mathf.Abs(origin.y - coordinate.y);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate.actor;
                }
            }

            return best;
        }

        private void UpdateFacing()
        {
            if (hero == null || hero.actor == null)
            {
                return;
            }

            if (selectedEnemy == null || !selectedEnemy.gameObject.activeInHierarchy)
            {
                selectedEnemy = FindNearestLivingEnemy();
            }

            FaceUnit(hero, selectedEnemy);
            for (int index = 0; index < units.Length; index++)
            {
                TacticalUnitBinding unit = units[index];
                if (unit != null && !unit.isHero)
                {
                    FaceUnit(unit, hero.actor);
                }
            }
        }

        private static void FaceUnit(TacticalUnitBinding unit, Transform target)
        {
            if (unit == null || unit.visual == null || unit.actor == null || target == null)
            {
                return;
            }

            bool shouldFaceRight = target.position.x >= unit.actor.position.x;
            unit.visual.flipX = unit.sourceSpriteFacesRight != shouldFaceRight;
        }

        private void SetFeedback(string message)
        {
            if (feedbackText != null)
            {
                feedbackText.text = message;
            }
        }
    }
}
