using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// ✅ F3 치트: "현재 activeQuests 전부"를 한 단계(인덱스 +1) 앞으로 진행시키고,
///           QuestManager.OnStepIndexChanged(questId, newIndex)를 강제로 발생시켜
///           브릿지가 노드 재생을 다시 타게 만드는 스크립트.
///
/// 동작:
/// - F3 누르면 모든 activeQuests에 대해:
///   1) 현재 인덱스 읽기
///   2) newIndex = cur + advanceSteps (기본 1)
///   3) (옵션) steps.Count 기준으로 클램프
///   4) 인덱스 강제 설정(QuestManager 메서드 우선, 없으면 QuestLine 리플렉션)
///   5) OnStepIndexChanged 이벤트 강제 Invoke
///
/// ✅ 컴파일 안정화:
/// - QuestManager/QuestLine 실제 구현이 달라도 최대한 동작하도록 리플렉션 후보를 넓게 잡았습니다.
/// - 찾을 수 없으면 setOk/eventOk=false로 로그에 남습니다.
/// </summary>
public class CheatAdvanceActiveQuestsF3 : MonoBehaviour
{
    [Header("Key")]
    public KeyCode hotkey = KeyCode.F3;

    [Header("Behavior")]
    [Tooltip("대사가 재생 중이면 먼저 강제 종료할지")]
    public bool forceCloseDialogueIfPlaying = true;

    [Tooltip("한 번 누를 때 몇 단계 앞으로 갈지(기본 1)")]
    public int advanceSteps = 1;

    [Tooltip("QuestLine.steps.Count 기준으로 마지막 인덱스(Count-1)까지만 허용")]
    public bool clampToLastStep = true;

    [Header("Debug")]
    public bool verboseLog = true;

    // cached refs
    private QuestManager _qm;
    private DialogueManager _dm;

    private void Awake()
    {
        _qm = QuestManager.I;
        _dm = DialogueManager.I;
    }

    private void Update()
    {
        if (Input.GetKeyDown(hotkey))
            AdvanceAllActiveQuests();
    }

    private void AdvanceAllActiveQuests()
    {
        if (!_qm) _qm = QuestManager.I;
        if (!_dm) _dm = DialogueManager.I;

        if (!_qm)
        {
            Debug.LogWarning("[CHEAT][F3] QuestManager.I is NULL", this);
            return;
        }

        if (forceCloseDialogueIfPlaying && _dm && _dm.IsPlaying)
            _dm.ForceClose();

        var list = _qm.activeQuests;
        if (list == null || list.Count == 0)
        {
            if (verboseLog) Debug.Log("[CHEAT][F3] activeQuests empty", this);
            return;
        }

        int steps = Mathf.Max(1, advanceSteps);

        if (verboseLog)
            Debug.Log($"[CHEAT][F3] ADVANCE ALL activeQuests count={list.Count} steps=+{steps}", this);

        for (int i = 0; i < list.Count; i++)
        {
            var q = list[i];
            if (!q) continue;

            string questId = GetQuestId(q);
            int cur = GetCurrentStepIndex(q);

            int next = cur + steps;

            if (clampToLastStep)
            {
                int last = GetLastStepIndex(q);
                if (last >= 0) next = Mathf.Min(next, last);
            }

            if (next == cur)
            {
                if (verboseLog)
                    Debug.Log($"[CHEAT][F3] Skip questId={questId} cur={cur} next={next} (no change)", this);
                continue;
            }

            bool setOk =
                TrySetStepIndexViaQuestManager(_qm, questId, next) ||
                TrySetStepIndexOnQuestLine(q, next);

            // ✅ 브릿지가 노드를 다시 타게 하려면 이 이벤트가 나가야 함
            bool eventOk = TryInvokeOnStepIndexChanged(_qm, questId, next);

            if (verboseLog)
            {
                Debug.Log(
                    $"[CHEAT][F3] questId={questId} {cur} -> {next} " +
                    $"setOk={setOk} eventOk={eventOk}",
                    this
                );
            }
        }
    }

    // ----------------------------
    // QuestLine helpers (reflection-safe)
    // ----------------------------
    private static string GetQuestId(object questLine)
    {
        if (questLine == null) return "";

        var t = questLine.GetType();

        var f = t.GetField("questId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null && f.FieldType == typeof(string))
            return (f.GetValue(questLine) as string) ?? "";

        var p = t.GetProperty("questId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
             ?? t.GetProperty("QuestId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (p != null && p.PropertyType == typeof(string))
        {
            try { return (p.GetValue(questLine) as string) ?? ""; } catch { }
        }

        return "";
    }

    private static int GetCurrentStepIndex(object questLine)
    {
        if (questLine == null) return -1;

        var t = questLine.GetType();

        var p = t.GetProperty("CurrentStepIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p != null && p.PropertyType == typeof(int))
        {
            try { return (int)p.GetValue(questLine); } catch { }
        }

        var f = t.GetField("currentStepIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null && f.FieldType == typeof(int))
        {
            try { return (int)f.GetValue(questLine); } catch { }
        }

        return -1;
    }

    private static int GetLastStepIndex(object questLine)
    {
        if (questLine == null) return -1;

        var t = questLine.GetType();

        // field: steps (List<QuestStep>)
        var stepsField = t.GetField("steps", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (stepsField != null)
        {
            var stepsObj = stepsField.GetValue(questLine);
            if (stepsObj != null)
            {
                var countProp = stepsObj.GetType().GetProperty("Count");
                if (countProp != null && countProp.PropertyType == typeof(int))
                {
                    try
                    {
                        int count = (int)countProp.GetValue(stepsObj);
                        return count > 0 ? count - 1 : -1;
                    }
                    catch { }
                }
            }
        }

        // property: Steps
        var stepsProp = t.GetProperty("steps", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?? t.GetProperty("Steps", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (stepsProp != null)
        {
            try
            {
                var stepsObj = stepsProp.GetValue(questLine);
                if (stepsObj != null)
                {
                    var countProp = stepsObj.GetType().GetProperty("Count");
                    if (countProp != null && countProp.PropertyType == typeof(int))
                    {
                        int count = (int)countProp.GetValue(stepsObj);
                        return count > 0 ? count - 1 : -1;
                    }
                }
            }
            catch { }
        }

        return -1;
    }

    private static bool TrySetStepIndexOnQuestLine(object questLine, int newIndex)
    {
        if (questLine == null) return false;

        var t = questLine.GetType();

        // method candidates: SetStepIndex(int), ForceSetStepIndex(int), JumpToStep(int), SetCurrentStepIndex(int)
        string[] methodNames = { "SetStepIndex", "ForceSetStepIndex", "JumpToStep", "SetCurrentStepIndex", "SetStep" };
        for (int i = 0; i < methodNames.Length; i++)
        {
            var mi = t.GetMethod(methodNames[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(int) }, null);
            if (mi != null)
            {
                try
                {
                    mi.Invoke(questLine, new object[] { newIndex });
                    return true;
                }
                catch { }
            }
        }

        // field candidates: currentStepIndex / stepIndex
        string[] fieldNames = { "currentStepIndex", "stepIndex", "_currentStepIndex", "_stepIndex" };
        for (int i = 0; i < fieldNames.Length; i++)
        {
            var fi = t.GetField(fieldNames[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (fi != null && fi.FieldType == typeof(int))
            {
                try
                {
                    fi.SetValue(questLine, newIndex);
                    return true;
                }
                catch { }
            }
        }

        return false;
    }

    // ----------------------------
    // QuestManager set (prefer)
    // ----------------------------
    private static bool TrySetStepIndexViaQuestManager(QuestManager qm, string questId, int newIndex)
    {
        if (!qm || string.IsNullOrEmpty(questId)) return false;

        var t = qm.GetType();

        // common method signatures:
        // - SetStepIndex(string questId, int index)
        // - ForceSetStepIndex(string questId, int index)
        // - JumpTo(string questId, int index)
        // - SetQuestStepIndex(string questId, int index)
        string[] methodNames = { "SetStepIndex", "ForceSetStepIndex", "SetQuestStepIndex", "JumpTo", "JumpToStep", "ForceJumpToStep", "SetStep" };

        for (int i = 0; i < methodNames.Length; i++)
        {
            var mi = t.GetMethod(methodNames[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(string), typeof(int) }, null);
            if (mi != null)
            {
                try
                {
                    mi.Invoke(qm, new object[] { questId, newIndex });
                    return true;
                }
                catch { }
            }
        }

        // fallback: AdvanceStep(string) repeated? (less ideal) -> not used here.

        return false;
    }

    // ----------------------------
    // Force invoke OnStepIndexChanged
    // ----------------------------
    private static bool TryInvokeOnStepIndexChanged(QuestManager qm, string questId, int stepIndex)
    {
        if (!qm || string.IsNullOrEmpty(questId)) return false;

        var t = qm.GetType();

        // event backing field often exists as: OnStepIndexChanged
        var fi = t.GetField("OnStepIndexChanged", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (fi != null)
        {
            try
            {
                var del = fi.GetValue(qm) as Delegate;
                if (del != null)
                {
                    del.DynamicInvoke(questId, stepIndex);
                    return true;
                }
            }
            catch { }
        }

        // sometimes compiler uses different backing field name; try common patterns
        var fi2 = t.GetField("<OnStepIndexChanged>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        if (fi2 != null)
        {
            try
            {
                var del = fi2.GetValue(qm) as Delegate;
                if (del != null)
                {
                    del.DynamicInvoke(questId, stepIndex);
                    return true;
                }
            }
            catch { }
        }

        // if QuestManager has a method to notify:
        var mi = t.GetMethod("NotifyStepIndexChanged", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(string), typeof(int) }, null)
              ?? t.GetMethod("RaiseStepIndexChanged", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(string), typeof(int) }, null);

        if (mi != null)
        {
            try
            {
                mi.Invoke(qm, new object[] { questId, stepIndex });
                return true;
            }
            catch { }
        }

        return false;
    }
}