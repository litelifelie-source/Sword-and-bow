using System;
using UnityEngine;

/// <summary>
/// DialogueManager (교체본)
/// - ✅ DM을 반드시 거쳐서 UI를 열게 하기 위한 "UI Override" 재생 API 제공
/// - ✅ _activeUI 추적: IsPlaying/Watchdog/FollowTarget이 실제 열린 UI 기준으로 동작
/// - ✅ 완료 콜백(Action<bool>) 보장
/// </summary>
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager I { get; private set; }

    [Header("Default UI")]
    [Tooltip("기본 DialogueUI. 비워두면 씬에서 자동 탐색합니다.")]
    [SerializeField] private DialogueUI dialogueUI;

    // ============================================================
    // Multi-UI Session
    // - Quest UI / Daily UI 를 완전히 분리하기 위해
    //   "UI별로" 세션을 따로 관리합니다.
    // - ✅ 핵심: 어떤 UI가 열려 있어도, 다른 UI 재생은 차단하지 않습니다.
    // ============================================================
    private class Session
    {
        public DialogueUI ui;
        public Action<bool> onComplete;
        public string ctxQuestId = "";
        public string ctxNodeKey = "";

        // watchdog flags (per UI)
        public bool awaitingClose;
        public bool closedHandled;
        public bool wasOpenAfterPlay;
    }

    // key: UI instance
    private readonly System.Collections.Generic.Dictionary<DialogueUI, Session> _sessions =
        new System.Collections.Generic.Dictionary<DialogueUI, Session>();

    // 컨텍스트 SetContext는 "다음 재생"에 적용될 값으로 유지
    private string _pendingCtxQuestId = "";
    private string _pendingCtxNodeKey = "";

    // ✅ 워치독
    [Header("Close Watchdog")]
    [Tooltip("UI가 콜백 없이 닫혀도 DM이 강제로 종료 처리할지")]
    public bool enableCloseWatchdog = true;

    [Header("Debug")]
    public bool verboseLog = true;
    public bool logStackTrace = false;

    /// <summary>
    /// 기본 UI(=dialogueUI)가 재생 중인지.
    /// - 호환 유지용 (Quest UI를 기본으로 두면 Quest 흐름은 그대로 동작합니다.)
    /// </summary>
    public bool IsPlaying => IsPlayingOnUI(dialogueUI);

    /// <summary>
    /// ✅ 특정 UI가 재생 중인지 (Daily/Quest 분리 핵심)
    /// </summary>
    public bool IsPlayingOnUI(DialogueUI ui)
    {
        if (ui == null) ui = dialogueUI;
        if (ui == null) return false;
        return ui.IsOpen;
    }

    /// <summary>
    /// 어떤 UI든 하나라도 재생 중인지
    /// </summary>
    public bool IsAnyPlaying
    {
        get
        {
            foreach (var kv in _sessions)
            {
                if (kv.Key != null && kv.Key.IsOpen)
                    return true;
            }
            return false;
        }
    }

    private void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }
        I = this;

        if (dialogueUI == null)
            dialogueUI = FindFirstObjectByType<DialogueUI>();

        if (verboseLog)
        {
            Debug.Log($"[DM] Awake defaultUI={(dialogueUI ? dialogueUI.name : "null")}", this);
        }
    }

    private void Update()
    {
        if (!enableCloseWatchdog) return;
        // ✅ UI별 워치독 처리
        // - awaitingClose==true 인 세션들만 점검
        if (_sessions.Count == 0) return;

        // 안전하게 복사해서 순회 (닫힘 처리 중 dict가 바뀔 수 있음)
        var keys = new System.Collections.Generic.List<DialogueUI>(_sessions.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            var ui = keys[i];
            if (ui == null) continue;
            if (!_sessions.TryGetValue(ui, out var s) || s == null) continue;
            if (!s.awaitingClose) continue;
            if (s.closedHandled) continue;

            // Play 직후 실제로 open을 한번이라도 관측해야 워치독이 발동
            if (!s.wasOpenAfterPlay)
            {
                if (ui.IsOpen) s.wasOpenAfterPlay = true;
                continue;
            }

            // open이었다가 콜백 없이 닫힘
            if (!ui.IsOpen)
            {
                if (verboseLog)
                    Debug.Log($"[DM] CloseWatchdog fired ui={ui.name} -> HandleClosedOnUI(false)", this);

                HandleClosedOnUI(ui, false);
            }
        }
    }

    // ------------------------------------------------------------
    // Context
    // ------------------------------------------------------------
    public void SetContext(string questId, string nodeKey)
    {
        _pendingCtxQuestId = string.IsNullOrEmpty(questId) ? "" : questId;
        _pendingCtxNodeKey = string.IsNullOrEmpty(nodeKey) ? "" : nodeKey;

        if (verboseLog)
            Debug.Log($"[DM] SetContext questId={_pendingCtxQuestId} nodeKey={_pendingCtxNodeKey}", this);
    }

    // ------------------------------------------------------------
    // Follow helpers (DialogueFollow가 읽는 값)
    // ------------------------------------------------------------
    public Transform CurrentFollowTarget
    {
        get
        {
            return CurrentFollowTargetOnUI(dialogueUI);
        }
    }

    public Transform CurrentFollowTargetOnUI(DialogueUI ui)
    {
        if (ui == null) ui = dialogueUI;
        if (ui == null) return null;
        return ui.GetOrResolveTarget();
    }

    public string CurrentSpeakerId
    {
        get
        {
            return CurrentSpeakerIdOnUI(dialogueUI);
        }
    }

    public string CurrentSpeakerIdOnUI(DialogueUI ui)
    {
        if (ui == null) ui = dialogueUI;
        if (ui == null) return "";
        return ui.CurrentSpeakerId;
    }

    // ------------------------------------------------------------
    // Basic controls
    // ------------------------------------------------------------
    public void Next()
    {
        NextOnUI(dialogueUI);
    }

    public void NextOnUI(DialogueUI ui)
    {
        if (ui == null) ui = dialogueUI;
        if (ui == null) return;
        ui.Next();
    }

    public void ForceClose()
    {
        ForceCloseOnUI(dialogueUI);
    }

    public void ForceCloseOnUI(DialogueUI ui)
    {
        if (ui == null) ui = dialogueUI;
        if (ui == null) return;

        // 세션이 없더라도 UI가 닫히게는 시도
        if (_sessions.TryGetValue(ui, out var s) && s != null)
        {
            // 강제 종료 경로에서는 open 관측을 true로
            if (s.awaitingClose) s.wasOpenAfterPlay = true;
        }

        ui.ForceClose();

        // 콜백 없이 닫히는 경우까지 커버
        if (_sessions.TryGetValue(ui, out var s2) && s2 != null)
        {
            if (s2.awaitingClose && !s2.closedHandled && !ui.IsOpen)
                HandleClosedOnUI(ui, false);
        }
    }

    // ------------------------------------------------------------
    // Reset session
    // ------------------------------------------------------------
    private Session GetOrCreateSession(DialogueUI ui)
    {
        if (ui == null) ui = dialogueUI;
        if (ui == null) return null;

        if (_sessions.TryGetValue(ui, out var s) && s != null)
            return s;

        s = new Session();
        s.ui = ui;
        _sessions[ui] = s;
        return s;
    }

    private void ResetSessionFlags(Session s)
    {
        if (s == null) return;
        s.awaitingClose = false;
        s.closedHandled = false;
        s.wasOpenAfterPlay = false;
        s.onComplete = null;
        s.ctxQuestId = "";
        s.ctxNodeKey = "";
    }

    // ------------------------------------------------------------
    // ✅ 핵심: UI Override + AutoAdvance + Meta
    // ------------------------------------------------------------
    public bool PlayAutoAdvanceWithMetaOnUI(
        DialogueUI uiOverride,
        string defaultSpeaker, string defaultSpeakerId,
        string[] speakersPerLine, string[] speakerIdsPerLine,
        string[] lines,
        float defaultSeconds, float[] perLineSeconds,
        Action<bool> onCompleteBool)
    {
        var ui = uiOverride != null ? uiOverride : dialogueUI;

        var s = GetOrCreateSession(ui);

        if (verboseLog)
        {
            Debug.Log(
                $"[DM] PlayAutoAdvanceWithMetaOnUI ui={(ui ? ui.name : "null")} " +
                $"lines={(lines == null ? 0 : lines.Length)} defaultSec={defaultSeconds} " +
                $"perLine={(perLineSeconds == null ? 0 : perLineSeconds.Length)}",
                this);
            if (logStackTrace)
                Debug.Log("[DM] StackTrace:\n" + Environment.StackTrace, this);
        }

        if (ui == null)
        {
            Debug.LogError("[DM] Blocked: ui is null", this);
            ResetSessionFlags(s);
            onCompleteBool?.Invoke(false);
            return false;
        }

        if (lines == null || lines.Length == 0)
        {
            Debug.LogWarning("[DM] Blocked: lines empty", this);
            ResetSessionFlags(s);
            onCompleteBool?.Invoke(false);
            return false;
        }

        if (ui.IsOpen)
        {
            Debug.LogWarning($"[DM] Blocked: ui.IsOpen=true ui={ui.name}", this);
            ResetSessionFlags(s);
            onCompleteBool?.Invoke(false);
            return false;
        }

        s.onComplete = onCompleteBool ?? (_ => { });
        s.awaitingClose = true;
        s.closedHandled = false;
        s.wasOpenAfterPlay = false;
        s.ctxQuestId = _pendingCtxQuestId;
        s.ctxNodeKey = _pendingCtxNodeKey;

        ui.OpenWithAutoAdvanceWithMeta(
            defaultSpeaker, defaultSpeakerId,
            speakersPerLine, speakerIdsPerLine,
            lines,
            defaultSeconds, perLineSeconds,
            (finished) => HandleClosedOnUI(ui, finished));

        return true;
    }

    // ------------------------------------------------------------
    // ✅ LEGACY COMPAT: PlayWithMeta (기존 코드 호환용)
    // - 기존 호출들이 컴파일 되게 유지
    // - 기본 UI(dialogueUI)에 "메타(라인별 화자)"만 적용해서 재생 (자동넘김 없음)
    // ------------------------------------------------------------
    public bool PlayWithMeta(
        string defaultSpeaker, string defaultSpeakerId,
        string[] speakersPerLine, string[] speakerIdsPerLine,
        string[] lines,
        Action<bool> onCompleteBool)
    {
        // UI override 없이 기본 UI 사용
        return PlayWithMetaOnUI(
            null,
            defaultSpeaker, defaultSpeakerId,
            speakersPerLine, speakerIdsPerLine,
            lines,
            onCompleteBool);
    }

    // ------------------------------------------------------------
    // ✅ LEGACY COMPAT: PlayWithMetaOnUI (필요하면 외부에서 특정 UI 지정 가능)
    // ------------------------------------------------------------
    public bool PlayWithMetaOnUI(
        DialogueUI uiOverride,
        string defaultSpeaker, string defaultSpeakerId,
        string[] speakersPerLine, string[] speakerIdsPerLine,
        string[] lines,
        Action<bool> onCompleteBool)
    {
        var ui = uiOverride != null ? uiOverride : dialogueUI;

        var s = GetOrCreateSession(ui);

        if (ui == null)
        {
            Debug.LogError("[DM] Blocked: ui is null (PlayWithMetaOnUI)", this);
            ResetSessionFlags(s);
            onCompleteBool?.Invoke(false);
            return false;
        }

        if (lines == null || lines.Length == 0)
        {
            Debug.LogWarning("[DM] Blocked: lines empty (PlayWithMetaOnUI)", this);
            ResetSessionFlags(s);
            onCompleteBool?.Invoke(false);
            return false;
        }

        if (ui.IsOpen)
        {
            Debug.LogWarning($"[DM] Blocked: ui.IsOpen=true (PlayWithMetaOnUI) ui={ui.name}", this);
            ResetSessionFlags(s);
            onCompleteBool?.Invoke(false);
            return false;
        }

        s.onComplete = onCompleteBool ?? (_ => { });
        s.awaitingClose = true;
        s.closedHandled = false;
        s.wasOpenAfterPlay = false;
        s.ctxQuestId = _pendingCtxQuestId;
        s.ctxNodeKey = _pendingCtxNodeKey;

        // ✅ 자동넘김이 아니라 메타만 적용해서 OpenWithMeta 호출
        ui.OpenWithMeta(
            defaultSpeaker, defaultSpeakerId,
            speakersPerLine, speakerIdsPerLine,
            lines,
            (finished) => HandleClosedOnUI(ui, finished));

        return true;
    }

    // ------------------------------------------------------------
    // ✅ NEW: Stream(1줄씩) - Bridge가 라인을 하나씩 공급
    // ------------------------------------------------------------
    public bool PlayStreamWithMeta(
        string defaultSpeaker, string defaultSpeakerId,
        Func<DialogueUI.StreamLine?> streamNext,
        Action<bool> onCompleteBool)
    {
        return PlayStreamWithMetaOnUI(null, defaultSpeaker, defaultSpeakerId, streamNext, onCompleteBool);
    }

    public bool PlayStreamWithMetaOnUI(
        DialogueUI uiOverride,
        string defaultSpeaker, string defaultSpeakerId,
        Func<DialogueUI.StreamLine?> streamNext,
        Action<bool> onCompleteBool)
    {
        var ui = uiOverride != null ? uiOverride : dialogueUI;

        var s = GetOrCreateSession(ui);

        if (ui == null)
        {
            Debug.LogError("[DM] Blocked: ui is null (PlayStreamWithMetaOnUI)", this);
            ResetSessionFlags(s);
            onCompleteBool?.Invoke(false);
            return false;
        }

        if (streamNext == null)
        {
            Debug.LogWarning("[DM] Blocked: streamNext null (PlayStreamWithMetaOnUI)", this);
            ResetSessionFlags(s);
            onCompleteBool?.Invoke(false);
            return false;
        }

        if (ui.IsOpen)
        {
            Debug.LogWarning($"[DM] Blocked: ui.IsOpen=true (PlayStreamWithMetaOnUI) ui={ui.name}", this);
            ResetSessionFlags(s);
            onCompleteBool?.Invoke(false);
            return false;
        }

        s.onComplete = onCompleteBool ?? (_ => { });
        s.awaitingClose = true;
        s.closedHandled = false;
        s.wasOpenAfterPlay = false;
        s.ctxQuestId = _pendingCtxQuestId;
        s.ctxNodeKey = _pendingCtxNodeKey;

        bool started = ui.OpenStreamWithMeta(
            defaultSpeaker, defaultSpeakerId,
            streamNext,
            (finished) => HandleClosedOnUI(ui, finished));

        if (!started)
        {
            ResetSessionFlags(s);
            onCompleteBool?.Invoke(false);
            return false;
        }

        return true;
    }

    // ------------------------------------------------------------
    // Close handling (single fire)
    // ------------------------------------------------------------
    private void HandleClosedOnUI(DialogueUI ui, bool finishedAllLines)
    {
        if (ui == null) ui = dialogueUI;
        if (ui == null) return;

        if (!_sessions.TryGetValue(ui, out var s) || s == null)
            return;

        if (s.closedHandled) return;
        s.closedHandled = true;
        s.awaitingClose = false;

        if (verboseLog)
        {
            Debug.Log(
                $"[DM] HandleClosed ui={ui.name} finished={finishedAllLines} ctxQuestId={s.ctxQuestId} ctxNodeKey={s.ctxNodeKey}",
                this);
        }

        try
        {
            var cb = s.onComplete;
            cb?.Invoke(finishedAllLines);
        }
        catch (Exception e)
        {
            Debug.LogException(e, this);
        }
        finally
        {
            ResetSessionFlags(s);
        }
    }
}