using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Пост охраны. Цикл: посетитель -> документы -> рамка -> стол с вещами -> решение игрока.
public class CheckpointController : MonoBehaviour
{
    public enum Stage { Empty, Approaching, Documents, Scanning, ScanResult, Inspection }

    [Header("Visitors")]
    public List<GameObject> prefabs = new List<GameObject>();
    public ItemDatabase itemDatabase;
    public Vector2Int itemCount = new Vector2Int(1, 3);
    public Vector2 speedRange = new Vector2(1.1f, 1.6f);
    public Vector2 scaleRange = new Vector2(0.95f, 1.05f);

    [Header("Difficulty (start of shift -> end of shift)")]
    [Range(0f, 1f)] public float bannedChanceStart = 0.25f;
    [Range(0f, 1f)] public float bannedChanceEnd = 0.55f;
    [Range(0f, 1f)] public float badDocChanceStart = 0.20f;
    [Range(0f, 1f)] public float badDocChanceEnd = 0.50f;

    [Header("Animation")]
    public RuntimeAnimatorController animatorController;
    public bool overrideExistingController;
    [Tooltip("Optional trigger fired when the visitor stops inside the frame.")]
    public string scanTrigger = "Scan";

    [Header("Points")]
    public Transform spawnPoint;     // откуда выходит
    public Transform deskPoint;      // окно, показывает документ
    public Transform scannerPoint;   // внутри рамки
    public Transform tablePoint;     // стол досмотра за рамкой
    public Transform exitPoint;      // прошёл
    public Transform rejectPoint;    // развернули

    [Header("Routes (optional - leave empty to walk straight)")]
    [Tooltip("Spawn -> desk")]      public RoutePath routeToDesk;
    [Tooltip("Desk -> scanner")]    public RoutePath routeToScanner;
    [Tooltip("Scanner -> table")]   public RoutePath routeToTable;
    [Tooltip("Table -> exit")]      public RoutePath routeToExit;
    [Tooltip("Anywhere -> reject")] public RoutePath routeToReject;

    [Header("Systems")]
    public MetalDetectorVisual detector;
    public ItemTable table;
    public ShiftManager shift;
    public EventLog log;

    [Header("Timings")]
    public float scanDuration = 1.5f;
    public float resultHold = 0.8f;
    public float firstDelay = 1.5f;
    public float delayBetween = 1.5f;

    public Stage CurrentStage { get; private set; } = Stage.Empty;
    public Visitor Current { get; private set; }
    public bool ScannerAlarm { get; private set; }

    public int spawned;

    public event Action<Visitor> OnVisitorSpawned;
    public event Action<Visitor> OnDocumentsShown;
    public event Action<Visitor, bool> OnScanFinished;
    public event Action<Visitor> OnReachedTable;
    public event Action<Visitor, bool> OnVisitorHandled;

    static System.Collections.Generic.IReadOnlyList<Transform> Way(RoutePath r) => r ? r.Points : null;

    float BannedChance => Mathf.Lerp(bannedChanceStart, bannedChanceEnd, shift ? shift.Progress : 0f);
    float BadDocChance => Mathf.Lerp(badDocChanceStart, badDocChanceEnd, shift ? shift.Progress : 0f);

    void Start()
    {
        if (!detector) detector = FindFirstObjectByType<MetalDetectorVisual>();
        if (!table)    table    = FindFirstObjectByType<ItemTable>();
        if (!shift)    shift    = FindFirstObjectByType<ShiftManager>();
        if (!log)      log      = FindFirstObjectByType<EventLog>();
        if (!spawnPoint) spawnPoint = transform;

        if (detector) detector.SetIdle();
        if (table) table.Clear();

        prefabs.RemoveAll(p => p == null);
        if (prefabs.Count == 0)
        {
            Debug.LogError("[Checkpoint] Prefabs list is empty - nobody to spawn.", this);
            return;
        }
        if (!itemDatabase)
            Debug.LogWarning("[Checkpoint] No Item Database assigned - bags will be empty.", this);

        if (shift) shift.OnShiftFinished += HandleShiftFinished;

        StartCoroutine(SpawnAfter(firstDelay));
    }

    void OnDestroy()
    {
        if (shift) shift.OnShiftFinished -= HandleShiftFinished;
    }

    void HandleShiftFinished()
    {
        StopAllCoroutines();
        if (Current) Destroy(Current.gameObject);
        Current = null;
        CurrentStage = Stage.Empty;
        if (table) table.Clear();
        if (detector) detector.SetIdle();
    }

    // ===== спавн =====

    IEnumerator SpawnAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (shift && !shift.Running) yield break;
        SpawnVisitor();
    }

    [ContextMenu("Spawn Visitor Now")]
    public void SpawnVisitor()
    {
        if (Current != null || prefabs.Count == 0) return;
        if (shift && !shift.Running) return;

        var prefab = prefabs[UnityEngine.Random.Range(0, prefabs.Count)];
        var go = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
        go.name = "Visitor_" + spawned;
        go.transform.localScale *= UnityEngine.Random.Range(scaleRange.x, scaleRange.y);

        var v = go.GetComponent<Visitor>();
        if (!v) v = go.AddComponent<Visitor>();

        v.SetupAnimator(animatorController, overrideExistingController);
        v.moveSpeed = UnityEngine.Random.Range(speedRange.x, speedRange.y);
        v.document = DocumentGenerator.Create(BadDocChance);
        if (itemDatabase) v.items = itemDatabase.RollItems(itemCount, BannedChance);

        spawned++;
        Current = v;
        ScannerAlarm = false;
        CurrentStage = Stage.Approaching;
        OnVisitorSpawned?.Invoke(v);

        // 1. идёт к окну и показывает документ
        v.GoVia(Way(routeToDesk), deskPoint, () =>
        {
            if (Current != v) return;
            CurrentStage = Stage.Documents;
            if (log) log.Add("Посетитель: " + v.document.fullName);
            OnDocumentsShown?.Invoke(v);
        });
    }

    // ===== действия игрока =====

    /// Документы приняты - отправить к рамке.
    public void SendToScanner()
    {
        if (CurrentStage != Stage.Documents) return;

        var v = Current;
        CurrentStage = Stage.Scanning;
        if (detector) detector.SetScanning();
        v.GoVia(Way(routeToScanner), scannerPoint, () => StartCoroutine(ScanRoutine(v)));
    }

    /// Пропустить - доступно после стола.
    public void LetThrough()
    {
        if (CurrentStage != Stage.Inspection) return;

        var v = Current;
        Score(v, true);
        Finish();
        v.GoVia(Way(routeToExit), exitPoint, () => Destroy(v.gameObject, 0.2f));
    }

    /// Развернуть или задержать - доступно на любой стадии, где есть посетитель.
    public void Reject()
    {
        if (CurrentStage == Stage.Empty || Current == null) return;

        var v = Current;
        Score(v, false);
        Finish();
        v.GoVia(Way(routeToReject), rejectPoint, () => Destroy(v.gameObject, 0.2f));
    }

    // ===== внутреннее =====

    IEnumerator ScanRoutine(Visitor v)
    {
        if (Current != v) yield break;

        v.PlayTrigger(scanTrigger);
        yield return new WaitForSeconds(scanDuration);
        if (Current != v) yield break;

        ScannerAlarm = v.DetectorTriggers;
        CurrentStage = Stage.ScanResult;
        if (detector) detector.SetResult(ScannerAlarm);
        if (log) log.Add(ScannerAlarm ? "Рамка: тревога." : "Рамка: чисто.");
        OnScanFinished?.Invoke(v, ScannerAlarm);

        yield return new WaitForSeconds(resultHold);
        if (Current != v) yield break;

        v.GoVia(Way(routeToTable), tablePoint, () =>
        {
            if (Current != v) return;
            if (table) table.Show(v.items);
            CurrentStage = Stage.Inspection;
            OnReachedTable?.Invoke(v);
        });
    }

    void Score(Visitor v, bool passed)
    {
        bool correct = v.ShouldBeAllowed == passed;

        string what;
        if (passed) what = correct ? "пропущен чистый" : "пропущен нарушитель";
        else        what = correct ? "задержан нарушитель" : "развёрнут чистый";

        if (shift) shift.RegisterDecision(correct, $"{v.document.fullName} — {what}");
        OnVisitorHandled?.Invoke(v, passed);
    }

    void Finish()
    {
        Current = null;
        CurrentStage = Stage.Empty;
        ScannerAlarm = false;

        if (table) table.Clear();
        if (detector) detector.SetIdle();

        if (!shift || shift.Running)
            StartCoroutine(SpawnAfter(delayBetween));
    }

    void OnDrawGizmos()
    {
        void Dot(Transform t, Color c, float r)
        {
            if (!t) return;
            Gizmos.color = c;
            Gizmos.DrawWireSphere(t.position, r);
            Gizmos.DrawRay(t.position, t.forward * 0.6f);
        }

        Dot(spawnPoint,   Color.yellow, 0.3f);
        Dot(deskPoint,    Color.cyan,   0.3f);
        Dot(scannerPoint, Color.white,  0.3f);
        Dot(tablePoint,   Color.magenta,0.3f);
        Dot(exitPoint,    Color.green,  0.3f);
        Dot(rejectPoint,  Color.red,    0.3f);

        Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
        void Line(Transform a, Transform b) { if (a && b) Gizmos.DrawLine(a.position, b.position); }
        Line(spawnPoint, deskPoint);
        Line(deskPoint, scannerPoint);
        Line(scannerPoint, tablePoint);
        Line(tablePoint, exitPoint);
    }
}
