using TMPro;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public class DialogueUI : MonoBehaviour
{
    [Header("UI Refs")]
    public CanvasGroup group;
    public Graphic[] graphicsFallback;
    public TMP_Text nameTMP;
    public TMP_Text bodyTMP;

    [Header("Input (Legacy)")]
    public bool allowClickToNext = false;
    public bool allowSpaceToNext = false;

    [Header("Input (UI Next - Toggle)")]
    public bool enableNextInput = true;
    public KeyCode nextKey = KeyCode.E;
    [Min(0f)] public float nextInputCooldown = 0.12f;
    public bool nextInputDebugLog = false;
    private float _nextInputReadyTime = 0f;

    [Header("Input Gate (Distance)")]
    public bool requireDistanceToNext = false;
    public Transform distanceOrigin;
    [Min(0f)] public float maxNextDistance = 2.0f;
    public bool resolveTargetBySpeakerId = true;
    public bool resolveByNameFirst = true;
    public bool resolveByReflectionFallback = true;
    public bool distanceGateDebugLog = false;

    public bool IsOpen => _isOpen;

    private bool _isOpen;
    private string[] _lines;

    // ============================================================
    // Stream mode (Bridge가 라인을 1개씩 공급)
    // ============================================================
    public struct StreamLine
    {
        public string speaker;
        public string speakerId;
        public string text;

        public StreamLine(string speaker, string speakerId, string text)
        {
            this.speaker = speaker;
            this.speakerId = speakerId;
            this.text = text;
        }
    }

    private bool _streamMode = false;
    private Func<StreamLine?> _streamNext = null;
    private readonly System.Collections.Generic.List<string> _streamLines = new();
    private readonly System.Collections.Generic.List<string> _streamSpeakers = new();
    private readonly System.Collections.Generic.List<string> _streamSpeakerIds = new();

    private int LineCount => _streamMode ? _streamLines.Count : (_lines != null ? _lines.Length : 0);

    private string GetLine(int i)
    {
        if (_streamMode)
            return (i >= 0 && i < _streamLines.Count) ? (_streamLines[i] ?? "") : "";
        return (_lines != null && i >= 0 && i < _lines.Length) ? (_lines[i] ?? "") : "";
    }

    private string GetSpeakerAt(int i)
    {
        if (_streamMode)
            return (i >= 0 && i < _streamSpeakers.Count) ? _streamSpeakers[i] : null;

        if (_speakersPerLine != null && i >= 0 && i < _speakersPerLine.Length)
            return _speakersPerLine[i];

        return null;
    }

    private string GetSpeakerIdAt(int i)
    {
        if (_streamMode)
            return (i >= 0 && i < _streamSpeakerIds.Count) ? _streamSpeakerIds[i] : null;

        if (_speakerIdsPerLine != null && i >= 0 && i < _speakerIdsPerLine.Length)
            return _speakerIdsPerLine[i];

        return null;
    }



    // ✅ Node 기본값 (fallback)
    private string _speaker;
    private string _speakerId;

    // ✅ Element(라인)별 값
    private string[] _speakersPerLine;
    private string[] _speakerIdsPerLine;
    private bool _hasMetaThisOpen;

    // ✅ Target cache
    private Transform _cachedTarget;
    private string _cachedTargetSpeakerKey;

    public string CurrentSpeakerId => ResolveCurrentSpeakerKey();

    private int _index;
    private Action<bool> _onClosed;

    // ------------------------------------------------------------
    // Auto Advance (Daily Pack compatibility)
    // ------------------------------------------------------------
    [Header("Auto Advance")]
    [Tooltip("ON이면: 라인을 자동으로 넘깁니다. (데일리팩 자동넘김 호환용)")]
    public bool autoAdvanceEnabled = false;

    [Tooltip("autoAdvanceEnabled=true일 때, perLine이 없으면 이 시간을 사용합니다.")]
    [Min(0f)]
    public float autoAdvanceDefaultSeconds = 1.5f;

    [Tooltip("라인별 자동 넘김 시간(초). 길이가 부족하면 autoAdvanceDefaultSeconds 적용")]
    public float[] autoAdvancePerLineSeconds;

    private Coroutine _autoAdvanceCo;

    [Header("Start State")]
    public bool startClosed = true;

    [Header("Debug Trace")]
    public bool traceCloseCallers = true;
    public bool traceOpenCallers = false;

    // ============================================================
    // Public API
    // ============================================================

    public void Open(string speaker, string[] lines) => Open(speaker, null, lines, null);
    public void Open(string speaker, string[] lines, Action<bool> onClosed) => Open(speaker, null, lines, onClosed);

    public void Open(string speaker, string speakerId, string[] lines, Action<bool> onClosed)
    {
        autoAdvanceEnabled = false;
        autoAdvanceDefaultSeconds = Mathf.Max(0f, autoAdvanceDefaultSeconds);
        autoAdvancePerLineSeconds = null;

        _hasMetaThisOpen = false;
        OpenInternal(speaker, speakerId, lines, onClosed);
    }

    /// <summary>
    /// ✅ 엘리먼트(라인)별 화자 이름/스피커키를 함께 넘기는 Open
    /// - speakersPerLine / speakerIdsPerLine 길이는 lines와 같게 맞추는 것을 권장합니다.
    /// - 배열이 null이거나 값이 공백이면 노드 기본 speaker/speakerId 규칙으로 fallback 됩니다.
    /// </summary>
    public void OpenWithMeta(string[] speakersPerLine, string[] speakerIdsPerLine, string[] lines, Action<bool> onClosed)
        => OpenWithMeta(null, null, speakersPerLine, speakerIdsPerLine, lines, onClosed);

    public void OpenWithMeta(
        string defaultSpeaker, string defaultSpeakerId,
        string[] speakersPerLine, string[] speakerIdsPerLine,
        string[] lines, Action<bool> onClosed)
    {
        autoAdvanceEnabled = false;
        autoAdvanceDefaultSeconds = Mathf.Max(0f, autoAdvanceDefaultSeconds);
        autoAdvancePerLineSeconds = null;

        _hasMetaThisOpen = true;
        OpenInternalWithMeta(defaultSpeaker, defaultSpeakerId, speakersPerLine, speakerIdsPerLine, lines, onClosed);
    }

    /// <summary>
    /// ✅ 스트리밍 Open (라인을 미리 배열로 만들지 않고 1개씩 공급)
    /// - streamNext가 null을 반환하면 "더 이상 라인 없음"으로 간주하고 종료합니다.
    /// </summary>
    public bool OpenStreamWithMeta(
        string defaultSpeaker, string defaultSpeakerId,
        Func<StreamLine?> streamNext,
        Action<bool> onClosed)
    {
        if (streamNext == null)
            return false;

        autoAdvanceEnabled = false;
        autoAdvanceDefaultSeconds = Mathf.Max(0f, autoAdvanceDefaultSeconds);
        autoAdvancePerLineSeconds = null;

        _hasMetaThisOpen = true;

        // ✅ 스트림 초기화
        _streamMode = true;
        _streamNext = streamNext;
        _streamLines.Clear();
        _streamSpeakers.Clear();
        _streamSpeakerIds.Clear();

        // ✅ 기본값
        _speaker = defaultSpeaker ?? "NPC";
        _speakerId = defaultSpeakerId;

        // ✅ 최소 1줄은 받아야 UI를 열 수 있음
        if (!TryAppendOneFromStream())
        {
            _streamMode = false;
            _streamNext = null;
            return false;
        }

        // OpenInternalWithMeta의 공통 처리 일부를 재사용 (UI 표시/인덱스/콜백)
        _onClosed = onClosed;
        _index = 0;
        _isOpen = true;

        _cachedTargetSpeakerKey = null;
        _cachedTarget = null;

        SetVisible(true);
        RenderCurrent();

        return true;
    }

    private bool TryAppendOneFromStream()
    {
        if (!_streamMode || _streamNext == null)
            return false;

        StreamLine? maybe = _streamNext();
        if (maybe == null)
            return false;

        var line = maybe.Value;
        _streamLines.Add(line.text ?? "");
        _streamSpeakers.Add(line.speaker);
        _streamSpeakerIds.Add(line.speakerId);
        return true;
    }



    /// <summary>
    /// ✅ 데일리팩 자동넘김용 Open (레거시 호환)
    /// </summary>
    public void OpenWithAutoAdvance(
        string speaker, string speakerId, string[] lines,
        float defaultSeconds, float[] perLineSeconds, Action<bool> onClosed)
    {
        autoAdvanceEnabled = true;
        autoAdvanceDefaultSeconds = Mathf.Max(0f, defaultSeconds);
        autoAdvancePerLineSeconds = perLineSeconds;

        _hasMetaThisOpen = false;
        OpenInternal(speaker, speakerId, lines, onClosed);
    }

    /// <summary>
    /// ✅ 데일리팩 자동넘김 + 엘리먼트(라인)별 화자 메타 지원
    /// </summary>
    public void OpenWithAutoAdvanceWithMeta(
        string defaultSpeaker, string defaultSpeakerId,
        string[] speakersPerLine, string[] speakerIdsPerLine, string[] lines,
        float defaultSeconds, float[] perLineSeconds, Action<bool> onClosed)
    {
        autoAdvanceEnabled = true;
        autoAdvanceDefaultSeconds = Mathf.Max(0f, defaultSeconds);
        autoAdvancePerLineSeconds = perLineSeconds;

        _hasMetaThisOpen = true;
        OpenInternalWithMeta(defaultSpeaker, defaultSpeakerId, speakersPerLine, speakerIdsPerLine, lines, onClosed);
    }

    /// <summary>
    /// 라인별 speakerKey에 맞춰 타겟을 다시 찾습니다.
    /// (DialogueFollow/DialogueManager가 참조 가능)
    /// </summary>
    public Transform GetOrResolveTarget()
    {
        var key = ResolveCurrentSpeakerKey();

        if (_cachedTargetSpeakerKey != key)
        {
            _cachedTargetSpeakerKey = key;
            _cachedTarget = null;
        }

        if (!_cachedTarget && resolveTargetBySpeakerId && !string.IsNullOrEmpty(key))
            _cachedTarget = ResolveTargetTransform(key);

        return _cachedTarget;
    }

    // ============================================================
    // Unity
    // ============================================================

    private void Awake()
    {
        if (group == null)
            group = GetComponentInChildren<CanvasGroup>(true);

        if (group == null && (graphicsFallback == null || graphicsFallback.Length == 0))
            graphicsFallback = GetComponentsInChildren<Graphic>(true);

        if (startClosed) HideImmediate();
        else SetVisible(true);
    }

    private void Update()
    {
        if (!_isOpen) return;

        bool nextLegacy =
            (allowClickToNext && Input.GetMouseButtonDown(0)) ||
            (allowSpaceToNext && Input.GetKeyDown(KeyCode.Space));

        bool nextUiKey = false;
        if (enableNextInput && Input.GetKeyDown(nextKey))
        {
            if (nextInputCooldown <= 0f)
            {
                nextUiKey = true;
            }
            else
            {
                float t = Time.unscaledTime;
                if (t >= _nextInputReadyTime)
                {
                    _nextInputReadyTime = t + nextInputCooldown;
                    nextUiKey = true;
                }
            }
        }

        bool next = nextLegacy || nextUiKey;
        if (!next) return;

        if (requireDistanceToNext && !DistanceGatePass())
        {
            if (distanceGateDebugLog)
                Debug.Log($"[DUI] Next blocked by distance gate (id={GetInstanceID()})", this);
            return;
        }

        if (nextInputDebugLog && nextUiKey)
            Debug.Log($"[DUI] Next key pressed ({nextKey}) (id={GetInstanceID()})", this);

        Next();
    }

    // ============================================================
    // Core
    // ============================================================

    private void OpenInternal(string speaker, string speakerId, string[] lines, Action<bool> onClosed)
    {
        if (!_hasMetaThisOpen)
        {
            _speakersPerLine = null;
            _speakerIdsPerLine = null;
        }

        if (lines == null || lines.Length == 0)
        {
            Debug.LogWarning("[DialogueUI] lines empty");
            return;
        }

        _speaker = string.IsNullOrEmpty(speaker) ? "NPC" : speaker;

        // ✅ speakerId 없으면 speaker(표시값)을 스피커키로 사용
        _speakerId = string.IsNullOrEmpty(speakerId) ? _speaker : speakerId;

        _lines = lines;
        _index = 0;
        _isOpen = true;
        _onClosed = onClosed;

        _nextInputReadyTime = Time.unscaledTime;

        // ✅ 캐시 초기화 (라인별 화자 대응)
        _cachedTarget = null;
        _cachedTargetSpeakerKey = null;

        if (traceOpenCallers)
        {
            Debug.Log(
                "[DUI-TRACE] OPEN caller\n" +
                UnityEngine.StackTraceUtility.ExtractStackTrace(),
                this);
        }

        SetVisible(true);
        RenderCurrent();
    }

    private void OpenInternalWithMeta(
        string defaultSpeaker, string defaultSpeakerId,
        string[] speakersPerLine, string[] speakerIdsPerLine,
        string[] lines, Action<bool> onClosed)
    {
        _speakersPerLine = speakersPerLine;
        _speakerIdsPerLine = speakerIdsPerLine;
        _hasMetaThisOpen = true;

        OpenInternal(defaultSpeaker, defaultSpeakerId, lines, onClosed);
    }

    public void Next()
    {
        if (!_isOpen) return;

        StopAutoAdvance();

        _index++;

        // ✅ Stream 모드: 부족하면 한 줄 더 받아서 이어붙임
        if (_streamMode)
        {
            while (_index >= LineCount)
            {
                if (!TryAppendOneFromStream())
                {
                    CloseInternal(true);
                    return;
                }
            }

            RenderCurrent();
            return;
        }

        // Legacy 배열 모드
        if (_lines == null || _index >= _lines.Length)
        {
            CloseInternal(true);
            return;
        }

        RenderCurrent();
    }

    public void Close()
    {
        CloseInternal(IsAtLastLine());
    }

    public void ForceClose()
    {
        if (!_isOpen) return;
        CloseInternal(IsAtLastLine());
    }

    private bool IsAtLastLine()
    {
        // ✅ Stream 모드는 "마지막 라인"을 미리 알 수 없으므로 false 처리
        if (_streamMode) return false;

        if (_lines == null || _lines.Length == 0) return false;
        return _index >= _lines.Length - 1;
    }

    private void CloseInternal(bool finishedAllLines)
    {
        StopAutoAdvance();

        bool wasOpen = _isOpen;

        if (traceCloseCallers && wasOpen)
        {
            Debug.Log(
                $"[DUI-TRACE] CLOSE caller finishedAllLines={finishedAllLines} (id={GetInstanceID()})\n" +
                UnityEngine.StackTraceUtility.ExtractStackTrace(),
                this);
        }

        _isOpen = false;

        var cb = _onClosed;
        _onClosed = null;

        _lines = null;
        _streamMode = false;
        _streamNext = null;
        _streamLines.Clear();
        _streamSpeakers.Clear();
        _streamSpeakerIds.Clear();
        _speaker = "";
        _speakerId = "";
        _speakersPerLine = null;
        _speakerIdsPerLine = null;
        _cachedTargetSpeakerKey = null;
        _hasMetaThisOpen = false;
        _index = 0;
        _cachedTarget = null;

        SetVisible(false);

        if (wasOpen)
            cb?.Invoke(finishedAllLines);
    }

    // ============================================================
    // Line-level speaker resolve (Element compatibility)
    // ============================================================

    private string ResolveCurrentSpeakerName()
    {
        var s = GetSpeakerAt(_index);
        if (!string.IsNullOrEmpty(s)) return s;
        return _speaker;
    }

    private string ResolveCurrentSpeakerKey()
    {
        // ✅ Stream/Meta 모두 커버: "현재 라인" 기준 speakerId 우선
        var id = GetSpeakerIdAt(_index);
        if (!string.IsNullOrEmpty(id)) return id;

        // fallback: node 기본 speakerId
        if (!string.IsNullOrEmpty(_speakerId)) return _speakerId;

        // 최후 fallback: 표시 이름을 key로 사용(레거시)
        return ResolveCurrentSpeakerName();
    }

    private void RenderCurrent()
    {
        if (nameTMP) nameTMP.text = ResolveCurrentSpeakerName();
        if (bodyTMP) bodyTMP.text = GetLine(_index);

        // ✅ 라인별 화자 변화를 반영하기 위해 타겟은 필요할 때 GetOrResolveTarget()로 갱신됩니다.

        StartAutoAdvanceIfNeeded();
    }

    // ============================================================
    // Auto Advance
    // ============================================================

    private void StartAutoAdvanceIfNeeded()
    {
        if (!_isOpen) return;
        if (!autoAdvanceEnabled) return;

        StopAutoAdvance();

        float sec = autoAdvanceDefaultSeconds;
        if (autoAdvancePerLineSeconds != null && _index >= 0 && _index < autoAdvancePerLineSeconds.Length)
            sec = Mathf.Max(0f, autoAdvancePerLineSeconds[_index]);

        _autoAdvanceCo = StartCoroutine(CoAutoAdvance(_index, sec));
    }

    private void StopAutoAdvance()
    {
        if (_autoAdvanceCo != null)
        {
            StopCoroutine(_autoAdvanceCo);
            _autoAdvanceCo = null;
        }
    }

    private System.Collections.IEnumerator CoAutoAdvance(int lineIndexSnapshot, float sec)
    {
        if (sec > 0f) yield return new WaitForSecondsRealtime(sec);
        else yield return null;

        if (!_isOpen) yield break;
        if (!autoAdvanceEnabled) yield break;
        if (_lines == null) yield break;
        if (_index != lineIndexSnapshot) yield break;

        Next();
    }

    // ============================================================
    // Visuals
    // ============================================================

    private void HideImmediate()
    {
        _isOpen = false;
        SetVisible(false);
    }

    private void SetVisible(bool v)
    {
        if (group != null)
        {
            group.alpha = v ? 1f : 0f;
            group.interactable = v;
            group.blocksRaycasts = v;
            return;
        }

        if (graphicsFallback != null)
        {
            float a = v ? 1f : 0f;
            for (int i = 0; i < graphicsFallback.Length; i++)
            {
                var g = graphicsFallback[i];
                if (!g) continue;
                var c = g.color;
                c.a = a;
                g.color = c;
            }
        }
    }

    // ============================================================
    // Distance gate + target resolve
    // ============================================================

    private bool DistanceGatePass()
    {
        if (!distanceOrigin) return false;

        // ✅ 라인별 speakerKey 기준으로 타겟을 찾음
        var key = ResolveCurrentSpeakerKey();

        if (!_cachedTarget && resolveTargetBySpeakerId && !string.IsNullOrEmpty(key))
            _cachedTarget = ResolveTargetTransform(key);

        if (!_cachedTarget) return false;

        float d = Vector2.Distance(distanceOrigin.position, _cachedTarget.position);
        return d <= maxNextDistance;
    }

    private Transform ResolveTargetTransform(string speakerKey)
    {
        if (string.IsNullOrEmpty(speakerKey))
            return null;

        if (resolveByNameFirst)
        {
            var go = GameObject.Find(speakerKey);
            if (go != null) return go.transform;
        }

        if (resolveByReflectionFallback)
        {
            var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                var mb = behaviours[i];
                if (!mb) continue;

                Type t = mb.GetType();
                string typeName = t.Name;

                if (!(typeName.Contains("Speaker") || typeName.Contains("Id") || typeName.Contains("Tag")))
                    continue;

                string idValue = TryGetStringMember(mb, t, "speakerId")
                              ?? TryGetStringMember(mb, t, "SpeakerId")
                              ?? TryGetStringMember(mb, t, "id")
                              ?? TryGetStringMember(mb, t, "Id");

                if (!string.IsNullOrEmpty(idValue) && string.Equals(idValue, speakerKey, StringComparison.Ordinal))
                    return mb.transform;
            }
        }

        return null;
    }

    private static string TryGetStringMember(object obj, Type t, string memberName)
    {
        const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        var fi = t.GetField(memberName, F);
        if (fi != null && fi.FieldType == typeof(string))
            return fi.GetValue(obj) as string;

        var pi = t.GetProperty(memberName, F);
        if (pi != null && pi.PropertyType == typeof(string) && pi.CanRead)
            return pi.GetValue(obj) as string;

        return null;
    }
}