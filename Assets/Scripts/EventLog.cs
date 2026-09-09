using System.Collections.Generic;
using UnityEngine;

/// Журнал событий смены - правая панель интерфейса.
public class EventLog : MonoBehaviour
{
    public int maxLines = 14;
    public bool alsoLogToConsole;

    readonly List<string> _lines = new List<string>();
    public IReadOnlyList<string> Lines => _lines;

    ShiftManager _shift;

    void Awake() => _shift = FindFirstObjectByType<ShiftManager>();

    public void Add(string text)
    {
        string stamp = _shift ? _shift.ClockString : "--:--";
        string line = $"[{stamp}] {text}";

        _lines.Add(line);
        while (_lines.Count > maxLines) _lines.RemoveAt(0);

        if (alsoLogToConsole) Debug.Log(line);
    }

    public void Clear() => _lines.Clear();
}
