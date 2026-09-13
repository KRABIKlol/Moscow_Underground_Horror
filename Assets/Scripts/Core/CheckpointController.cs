using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CheckpointController : MonoBehaviour
{
    public enum Stage { Empty, Approaching, Documents, Scanning, ScanResult, Inspection }

    public List<GameObject> prefabs = new List<GameObject>();
    public ItemDatabase itemDatabase;
    public Vector2Int itemCount = new Vector2Int(1, 3);
    public Vector2 speedRange = new Vector2(1.1f, 1.6f);
    public Vector2 scaleRange = new Vector2(0.95f, 1.05f);

    [Range(0f, 1f)] public float bannedChanceStart = 0.25f;
    [Range(0f, 1f)] public float bannedChanceEnd = 0.55f;
    [Range(0f, 1f)] public float badDocChanceStart = 0.20f;
    [Range(0f, 1f)] public float badDocChanceEnd = 0.50f;

  
    public RuntimeAnimatorController animatorController;
    public bool overrideExistingController;
   
    public string scanTrigger = "Scan";

    
    public Transform spawnPoint;     
    public Transform deskPoint;      
    public Transform scannerPoint;   
    public Transform tablePoint;    
    public Transform exitPoint;      
    public Transform rejectPoint;    

    public RoutePath routeToDesk;
    public RoutePath routeToScanner;
    public RoutePath routeToTable;
    public RoutePath routeToExit;
    public RoutePath routeToReject;
    
    public bool reverseRejectRoute = false;

    public MetalDetectorVisual detector;
    public ItemTable table;
    
    public JailCell jail;
    public ShiftManager shift;
    public EventLog log;

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

    System.Collections.Generic.IReadOnlyList<Transform> RejectWay()
    {
        if (!routeToReject) return null;
        return reverseRejectRoute ? routeToReject.PointsReversed : routeToReject.Points;
    }

    float BannedChance => Mathf.Lerp(bannedChanceStart, bannedChanceEnd, shift ? shift.Progress : 0f);
    float BadDocChance => Mathf.Lerp(badDocChanceStart, badDocChanceEnd, shift ? shift.Progress : 0f);

    void Start()
    {
        if (!detector) detector = FindFirstObjectByType<MetalDetectorVisual>();
        if (!table)    table    = FindFirstObjectByType<ItemTable>();
        if (!jail)     jail     = FindFirstObjectByType<JailCell>();
        if (!shift)    shift    = FindFirstObjectByType<ShiftManager>();
        if (!log)      log      = FindFirstObjectByType<EventLog>();
        if (!spawnPoint) spawnPoint = transform;

        if (detector) detector.SetIdle();
        if (table) table.Clear();

        prefabs.RemoveAll(p => p == null);
        
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


    IEnumerator SpawnAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (shift && !shift.Running) yield break;
        SpawnVisitor();
    }


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

     
        v.GoVia(Way(routeToDesk), deskPoint, () =>
        {
            if (Current != v) return;
            CurrentStage = Stage.Documents;
            if (log) log.Add("Посетитель: " + v.document.fullName);
            OnDocumentsShown?.Invoke(v);
        });
    }

    
    public void SendToScanner()
    {
        if (CurrentStage != Stage.Documents) return;

        var v = Current;
        CurrentStage = Stage.Scanning;
        if (detector) detector.SetScanning();
        v.GoVia(Way(routeToScanner), scannerPoint, () => StartCoroutine(ScanRoutine(v)));
    }

  
    public void LetThrough()
    {
        if (CurrentStage != Stage.Inspection) return;

        var v = Current;
        Score(v, true);
        Finish();
        v.GoVia(Way(routeToExit), exitPoint, () => Destroy(v.gameObject, 0.2f));
    }

  
    public void Detain()
    {
        if (CurrentStage == Stage.Empty || Current == null) return;

        var v = Current;
        Score(v, false);
        Finish();

        if (jail && jail.Put(v))
        {
            if (log) log.Add("Задержан, помещён в камеру.");
        }
        else
        {
          
            v.GoVia(RejectWay(), rejectPoint, () => Destroy(v.gameObject, 0.2f));
        }
    }

    public void Reject()
    {
        if (CurrentStage == Stage.Empty || Current == null) return;

        var v = Current;
        Score(v, false);
        Finish();
        v.GoVia(RejectWay(), rejectPoint, () => Destroy(v.gameObject, 0.2f));
    }



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

        Dot(spawnPoint,   Color.yellow,  0.3f);
        Dot(deskPoint,    Color.cyan,    0.3f);
        Dot(scannerPoint, Color.white,   0.3f);
        Dot(tablePoint,   Color.magenta, 0.3f);
        Dot(exitPoint,    Color.green,   0.3f);
        Dot(rejectPoint,  Color.red,     0.3f);

        DrawLeg(spawnPoint,   routeToDesk,    deskPoint);
        DrawLeg(deskPoint,    routeToScanner, scannerPoint);
        DrawLeg(scannerPoint, routeToTable,   tablePoint);
        DrawLeg(tablePoint,   routeToExit,    exitPoint);
        DrawRejectLeg();
    }

    void DrawRejectLeg()
    {
        if (!deskPoint || !rejectPoint) return;

        var pts = RejectWay();
        bool hasRoute = pts != null && pts.Count > 0;

        Gizmos.color = hasRoute
            ? new Color(1f, 0.4f, 0.4f, 0.7f)
            : new Color(1f, 0.55f, 0.1f, 0.7f);

        Vector3 prev = deskPoint.position;
        if (hasRoute)
        {
            foreach (var p in pts)
            {
                if (!p) continue;
                Gizmos.DrawLine(prev, p.position);
                prev = p.position;
            }
        }
        Gizmos.DrawLine(prev, rejectPoint.position);
    }

    void DrawLeg(Transform from, RoutePath route, Transform to)
    {
        if (!from || !to) return;

        
        bool hasRoute = route && route.Points.Count > 0;
        Gizmos.color = hasRoute
            ? new Color(1f, 1f, 1f, 0.8f)
            : new Color(1f, 0.55f, 0.1f, 0.7f);

        Vector3 prev = from.position;

        if (hasRoute)
        {
            foreach (var p in route.Points)
            {
                if (!p) continue;
                Gizmos.DrawLine(prev, p.position);
                prev = p.position;
            }
        }

        Gizmos.DrawLine(prev, to.position);
    }
}
