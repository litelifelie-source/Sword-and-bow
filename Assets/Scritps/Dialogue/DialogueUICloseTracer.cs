using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Dialogue UI Close Tracer (Compile Safe Version)
/// </summary>
public class DialogueUICloseTracer : MonoBehaviour
{
    [Header("Target (optional)")]
    public MonoBehaviour dialogueUIBehaviour;

    [Header("Logging")]
    public bool enable = true;
    public bool logOnStateChangeOnly = true;
    public bool includeStackTraceOnClose = true;
    public int stackTraceSkipFrames = 1;
    public int stackTraceMaxFrames = 25;

    private Func<bool> _getIsOpen;
    private bool _prevIsOpen;
    private bool _hasPrev;
    private string _uiTypeName = "(unknown)";
    private string _objPath;

    void Awake()
    {
        _objPath = GetTransformPath(transform);

        if (dialogueUIBehaviour == null)
        {
            var monos = GetComponents<MonoBehaviour>();
            foreach (var m in monos)
            {
                if (m == null) continue;
                if (HasIsOpen(m.GetType()))
                {
                    dialogueUIBehaviour = m;
                    break;
                }
            }
        }

        if (dialogueUIBehaviour != null)
        {
            var t = dialogueUIBehaviour.GetType();
            _uiTypeName = t.Name;
            _getIsOpen = BuildIsOpenGetter(t, dialogueUIBehaviour);
        }

        if (_getIsOpen != null)
        {
            _prevIsOpen = SafeGetIsOpen();
            _hasPrev = true;
        }
        else
        {
            UnityEngine.Debug.LogWarning(
                $"[UI-CLOSE-TRACER] No IsOpen getter found. path={_objPath}", this);
        }
    }

    void Update()
    {
        if (!enable || _getIsOpen == null) return;

        bool cur = SafeGetIsOpen();

        if (!_hasPrev)
        {
            _prevIsOpen = cur;
            _hasPrev = true;
            return;
        }

        if (logOnStateChangeOnly && cur == _prevIsOpen)
            return;

        if (_prevIsOpen && !cur)
        {
            LogClosed("IsOpen true -> false");
        }

        _prevIsOpen = cur;
    }

    void OnDisable()
    {
        if (!enable) return;
        LogClosed("OnDisable (GameObject disabled)");
    }

    void OnDestroy()
    {
        if (!enable) return;
        LogClosed("OnDestroy (Object destroyed)");
    }

    private void LogClosed(string reason)
    {
        string head =
            $"[UI-CLOSE-TRACER] CLOSED reason={reason} ui={_uiTypeName} path={_objPath}";

        if (!includeStackTraceOnClose)
        {
            UnityEngine.Debug.Log(head, this);
            return;
        }

        string st = GetStackTrace(stackTraceSkipFrames, stackTraceMaxFrames);
        UnityEngine.Debug.Log($"{head}\n--- stack ---\n{st}", this);
    }

    private bool SafeGetIsOpen()
    {
        try { return _getIsOpen(); }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning(
                $"[UI-CLOSE-TRACER] IsOpen getter failed: {e.Message}", this);
            return false;
        }
    }

    private static bool HasIsOpen(Type t)
    {
        var p = t.GetProperty("IsOpen",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        return p != null && p.PropertyType == typeof(bool)
               && p.GetGetMethod(true) != null;
    }

    private static Func<bool> BuildIsOpenGetter(Type t, object instance)
    {
        var p = t.GetProperty("IsOpen",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (p == null || p.PropertyType != typeof(bool)) return null;

        var getter = p.GetGetMethod(true);
        if (getter == null) return null;

        return () => (bool)getter.Invoke(instance, null);
    }

    private static string GetTransformPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }

    private static string GetStackTrace(int skipFrames, int maxFrames)
    {
        try
        {
            var st = new System.Diagnostics.StackTrace(true);
            int count = Math.Min(st.FrameCount, skipFrames + maxFrames);

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = skipFrames; i < count; i++)
            {
                var f = st.GetFrame(i);
                if (f == null) continue;

                var m = f.GetMethod();
                string method = m != null
                    ? (m.DeclaringType?.FullName + "." + m.Name)
                    : "(unknown)";

                string file = f.GetFileName();
                int line = f.GetFileLineNumber();

                if (!string.IsNullOrEmpty(file))
                    sb.AppendLine($"{method} ({System.IO.Path.GetFileName(file)}:{line})");
                else
                    sb.AppendLine(method);
            }
            return sb.ToString();
        }
        catch
        {
            return Environment.StackTrace;
        }
    }
}