using System;
using UnityEngine;

/// Смена: таймер, ошибки, счёт, нарастание сложности, итоговая оценка.
public class ShiftManager : MonoBehaviour
{
    [Header("Shift")]
    [Tooltip("Real seconds the shift lasts.")]
    public float shiftDuration = 300f;
    [Tooltip("In-game clock start, hours.")]
    public int clockStartHour = 8;
    [Tooltip("How many in-game hours the shift covers, for the displayed clock.")]
    public float clockHours = 8f;

    [Header("Rules")]
    public int maxMistakes = 3;
    public bool endShiftOnMistakeLimit = true;

    [Header("State (read only)")]
    public int checkedCount;
    public int correctDecisions;
    public int mistakes;

    public bool Running { get; private set; }
    public bool Finished { get; private set; }
    public bool Failed { get; private set; }

    public float TimeLeft { get; private set; }

    /// 0 в начале смены, 1 в конце. Используется для роста сложности.
    public float Progress => shiftDuration <= 0f ? 1f : Mathf.Clamp01(1f - TimeLeft / shiftDuration);

    public float Accuracy => checkedCount == 0 ? 0f : (float)correctDecisions / checkedCount;

    public string TimeLeftString
    {
        get
        {
            int t = Mathf.Max(0, Mathf.CeilToInt(TimeLeft));
            return $"{t / 60:00}:{t % 60:00}";
        }
    }

    /// Внутриигровые часы, например 13:24.
    public string ClockString
    {
        get
        {
            float h = clockStartHour + clockHours * Progress;
            int hh = Mathf.FloorToInt(h) % 24;
            int mm = Mathf.FloorToInt((h - Mathf.Floor(h)) * 60f);
            return $"{hh:00}:{mm:00}";
        }
    }

    public string Grade
    {
        get
        {
            if (Failed) return "СМЕНА ПРОВАЛЕНА";
            if (checkedCount == 0) return "—";
            float a = Accuracy;
            if (a >= 0.95f) return "S";
            if (a >= 0.85f) return "A";
            if (a >= 0.70f) return "B";
            if (a >= 0.55f) return "C";
            return "D";
        }
    }

    public event Action OnShiftStarted;
    public event Action OnShiftFinished;
    public event Action<bool> OnDecision;   // true = верно

    EventLog _log;

    void Awake() => _log = FindFirstObjectByType<EventLog>();

    void Start() => StartShift();

    [ContextMenu("Start Shift")]
    public void StartShift()
    {
        checkedCount = 0;
        correctDecisions = 0;
        mistakes = 0;
        TimeLeft = shiftDuration;
        Running = true;
        Finished = false;
        Failed = false;

        if (_log) { _log.Clear(); _log.Add("Смена началась."); }
        OnShiftStarted?.Invoke();
    }

    void Update()
    {
        if (!Running) return;

        TimeLeft -= Time.deltaTime;
        if (TimeLeft <= 0f)
        {
            TimeLeft = 0f;
            EndShift(false);
        }
    }

    /// Зарегистрировать решение игрока по посетителю.
    public void RegisterDecision(bool correct, string description)
    {
        if (!Running) return;

        checkedCount++;
        if (correct) correctDecisions++;
        else mistakes++;

        if (_log) _log.Add((correct ? "OK  " : "ОШИБКА  ") + description);
        OnDecision?.Invoke(correct);

        if (endShiftOnMistakeLimit && mistakes >= maxMistakes)
            EndShift(true);
    }

    public void EndShift(bool failed)
    {
        if (Finished) return;

        Running = false;
        Finished = true;
        Failed = failed;

        if (_log) _log.Add(failed ? "Смена прервана: лимит ошибок." : "Смена окончена.");
        OnShiftFinished?.Invoke();
    }
}
