using System;
using System.Collections.Generic;
using UnityEngine;

public static class AllyCrossJump
{
    public enum JumpMode { LockToAlly }


    /// <summary>
    /// ✅ 스트리밍 버전: 결과 시퀀스를 미리 리스트로 만들지 않고,
    /// 필요할 때마다 LineElement를 하나씩 yield 합니다.
    /// (Jump/Filter 로직은 BuildResolvedSequence와 동일)
    /// </summary>
    public static IEnumerable<QuestDialoguePack.LineElement> StreamResolvedSequence(
        QuestDialoguePack.Node node,
        Func<string, SpeakerIdTag> resolveSpeakerTag,
        Func<string, bool> isAllySpeaker,
        JumpMode jumpMode = JumpMode.LockToAlly,
        bool debugLog = false,
        UnityEngine.Object logOwner = null,
        string debugPrefix = "[ACJ]"
    )
    {
        if (node == null)
        {
            if (debugLog) Debug.Log($"{debugPrefix} node=NULL -> empty", logOwner);
            yield break;
        }

        var main = node.GetMainResolved();
        var ally = node.GetAllyResolved();

        if (debugLog)
        {
            Debug.Log(
                $"{debugPrefix} START(Stream) nodeKey='{node.key}' nodeSpeakerId='{node.speakerId}' " +
                $"main={(main?.Count ?? 0)} ally={(ally?.Count ?? 0)} (allyCheck={(isAllySpeaker != null ? "custom" : "null")})",
                logOwner
            );
        }

        bool jumped = false;

        // MAIN
        if (main != null)
        {
            for (int i = 0; i < main.Count; i++)
            {
                var elem = main[i];
                if (elem == null)
                {
                    if (debugLog) Debug.Log($"{debugPrefix} MAIN[{i}] null -> skip", logOwner);
                    continue;
                }

                bool output = ShouldOutput(elem, node, resolveSpeakerTag, isAllySpeaker, debugLog, logOwner, debugPrefix, $"MAIN[{i}]");
                if (!output)
                {
                    if (debugLog) Debug.Log($"{debugPrefix} MAIN[{i}] FILTERED audience={elem.audience} sid='{ResolveSpeakerId(elem, node)}'", logOwner);
                    continue;
                }

                if (debugLog)
                    Debug.Log($"{debugPrefix} MAIN[{i}] OUTPUT ✅ aud={elem.audience} sid='{ResolveSpeakerId(elem, node)}' text='{Trim(elem.text)}'", logOwner);

                yield return elem;

                if (!jumped
                    && jumpMode == JumpMode.LockToAlly
                    && elem.audience == QuestDialoguePack.LineAudience.OnlyAlly
                    && ally != null
                    && ally.Count > 0)
                {
                    jumped = true;
                    if (debugLog) Debug.Log($"{debugPrefix} CROSS-JUMP 🔁 by MAIN[{i}] OnlyAlly OUTPUT -> LOCK ALLY", logOwner);
                    break;
                }
            }
        }

        // ALLY
        if (jumped && ally != null)
        {
            for (int i = 0; i < ally.Count; i++)
            {
                var elem = ally[i];
                if (elem == null)
                {
                    if (debugLog) Debug.Log($"{debugPrefix} ALLY[{i}] null -> skip", logOwner);
                    continue;
                }

                bool output = ShouldOutput(elem, node, resolveSpeakerTag, isAllySpeaker, debugLog, logOwner, debugPrefix, $"ALLY[{i}]");
                if (!output)
                {
                    if (debugLog) Debug.Log($"{debugPrefix} ALLY[{i}] FILTERED audience={elem.audience} sid='{ResolveSpeakerId(elem, node)}'", logOwner);
                    continue;
                }

                if (debugLog)
                    Debug.Log($"{debugPrefix} ALLY[{i}] OUTPUT ✅ aud={elem.audience} sid='{ResolveSpeakerId(elem, node)}' text='{Trim(elem.text)}'", logOwner);

                yield return elem;
            }
        }
        else
        {
            if (debugLog)
            {
                if (!jumped) Debug.Log($"{debugPrefix} NO-JUMP (OnlyAlly OUTPUT 트리거 없음 / ally 비었음)", logOwner);
                else Debug.Log($"{debugPrefix} JUMP=true but ally empty", logOwner);
            }
        }

        if (debugLog) Debug.Log($"{debugPrefix} END(Stream)", logOwner);
    }

    public static List<QuestDialoguePack.LineElement> BuildResolvedSequence(
            QuestDialoguePack.Node node,
            Func<string, SpeakerIdTag> resolveSpeakerTag,
            Func<string, bool> isAllySpeaker,
            JumpMode jumpMode = JumpMode.LockToAlly,
            bool debugLog = false,
            UnityEngine.Object logOwner = null,
            string debugPrefix = "[ACJ]"
        )
    {
        var result = new List<QuestDialoguePack.LineElement>();
        if (node == null)
        {
            if (debugLog) Debug.Log($"{debugPrefix} node=NULL -> empty", logOwner);
            return result;
        }

        var main = node.GetMainResolved();
        var ally = node.GetAllyResolved();

        if (debugLog)
        {
            Debug.Log(
                $"{debugPrefix} START nodeKey='{node.key}' nodeSpeakerId='{node.speakerId}' " +
                $"main={(main?.Count ?? 0)} ally={(ally?.Count ?? 0)} (allyCheck={(isAllySpeaker != null ? "custom" : "null")})",
                logOwner
            );
        }

        bool jumped = false;

        // MAIN
        if (main != null)
        {
            for (int i = 0; i < main.Count; i++)
            {
                var elem = main[i];
                if (elem == null)
                {
                    if (debugLog) Debug.Log($"{debugPrefix} MAIN[{i}] null -> skip", logOwner);
                    continue;
                }

                bool output = ShouldOutput(elem, node, resolveSpeakerTag, isAllySpeaker, debugLog, logOwner, debugPrefix, $"MAIN[{i}]");
                if (!output)
                {
                    if (debugLog) Debug.Log($"{debugPrefix} MAIN[{i}] FILTERED audience={elem.audience} sid='{ResolveSpeakerId(elem, node)}'", logOwner);
                    continue;
                }

                result.Add(elem);

                if (debugLog)
                    Debug.Log($"{debugPrefix} MAIN[{i}] OUTPUT ✅ aud={elem.audience} sid='{ResolveSpeakerId(elem, node)}' text='{Trim(elem.text)}'", logOwner);

                if (!jumped
                    && jumpMode == JumpMode.LockToAlly
                    && elem.audience == QuestDialoguePack.LineAudience.OnlyAlly
                    && ally != null
                    && ally.Count > 0)
                {
                    jumped = true;
                    if (debugLog) Debug.Log($"{debugPrefix} CROSS-JUMP 🔁 by MAIN[{i}] OnlyAlly OUTPUT -> LOCK ALLY", logOwner);
                    break;
                }
            }
        }

        // ALLY
        if (jumped && ally != null)
        {
            for (int i = 0; i < ally.Count; i++)
            {
                var elem = ally[i];
                if (elem == null)
                {
                    if (debugLog) Debug.Log($"{debugPrefix} ALLY[{i}] null -> skip", logOwner);
                    continue;
                }

                bool output = ShouldOutput(elem, node, resolveSpeakerTag, isAllySpeaker, debugLog, logOwner, debugPrefix, $"ALLY[{i}]");
                if (!output)
                {
                    if (debugLog) Debug.Log($"{debugPrefix} ALLY[{i}] FILTERED audience={elem.audience} sid='{ResolveSpeakerId(elem, node)}'", logOwner);
                    continue;
                }

                result.Add(elem);

                if (debugLog)
                    Debug.Log($"{debugPrefix} ALLY[{i}] OUTPUT ✅ aud={elem.audience} sid='{ResolveSpeakerId(elem, node)}' text='{Trim(elem.text)}'", logOwner);
            }
        }
        else
        {
            if (debugLog)
            {
                if (!jumped) Debug.Log($"{debugPrefix} NO-JUMP (OnlyAlly OUTPUT 트리거 없음 / ally 비었음)", logOwner);
                else Debug.Log($"{debugPrefix} JUMP=true but ally empty", logOwner);
            }
        }

        if (debugLog) Debug.Log($"{debugPrefix} END resultCount={result.Count}", logOwner);
        return result;
    }

    private static bool ShouldOutput(
        QuestDialoguePack.LineElement e,
        QuestDialoguePack.Node node,
        Func<string, SpeakerIdTag> resolveSpeakerTag,
        Func<string, bool> isAllySpeaker,
        bool debugLog,
        UnityEngine.Object logOwner,
        string prefix,
        string slot
    )
    {
        if (e == null) return false;

        if (e.audience == QuestDialoguePack.LineAudience.Any)
            return true;

        if (e.audience == QuestDialoguePack.LineAudience.OnlyAlly)
        {
            bool fromElement = !string.IsNullOrEmpty(e.speakerId);
            string sid = ResolveSpeakerId(e, node);

            if (string.IsNullOrEmpty(sid))
            {
                if (debugLog) Debug.Log($"{prefix} {slot} FAIL ❌ sid empty (element+node)", logOwner);
                return false;
            }

            if (resolveSpeakerTag == null)
            {
                if (debugLog) Debug.Log($"{prefix} {slot} FAIL ❌ resolveSpeakerTag null", logOwner);
                return false;
            }

            if (isAllySpeaker == null)
            {
                if (debugLog) Debug.Log($"{prefix} {slot} FAIL ❌ isAllySpeaker null", logOwner);
                return false;
            }

            var tag = resolveSpeakerTag(sid);
            if (!tag)
            {
                if (debugLog) Debug.Log($"{prefix} {slot} FAIL ❌ SpeakerIdTag not found sid='{sid}'", logOwner);
                return false;
            }

            bool pass = isAllySpeaker(sid);

            if (debugLog)
            {
                string anchorName = tag.FollowAnchor ? tag.FollowAnchor.name : "null";
                int anchorLayer = tag.FollowAnchor ? tag.FollowAnchor.gameObject.layer : -1;

                Debug.Log(
                    $"{prefix} {slot} OnlyAlly check sid='{sid}' (src={(fromElement ? "element.speakerId" : "node.speakerId")}) " +
                    $"tag='{tag.name}' tagLayer={tag.gameObject.layer} anchor='{anchorName}' anchorLayer={anchorLayer} -> {(pass ? "PASS ✅" : "FAIL ❌")}",
                    logOwner
                );
            }

            return pass;
        }

        // audience 확장 대비: 기본은 출력
        return true;
    }

    private static string ResolveSpeakerId(QuestDialoguePack.LineElement e, QuestDialoguePack.Node node)
    {
        if (e != null && !string.IsNullOrEmpty(e.speakerId)) return e.speakerId;
        if (node != null && !string.IsNullOrEmpty(node.speakerId)) return node.speakerId;
        return null;
    }

    private static string Trim(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace("\n", " ").Replace("\r", " ");
        return s.Length > 40 ? s.Substring(0, 40) + "..." : s;
    }
}