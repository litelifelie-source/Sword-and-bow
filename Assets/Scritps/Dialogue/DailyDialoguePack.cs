using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Dialogue/Daily Dialogue Pack", fileName = "DailyDialoguePack_")]
public class DailyDialoguePack : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("기존 트리거가 questId로 팩을 찾는 구조면, 여기 값을 사용합니다. (데일리는 퀘스트랑 무관하지만 '찾기 키'로만 씀)")]
    public string questId;

    [Header("Nodes")]
    public List<Node> nodes = new List<Node>();

    // =====================================================
    // Line Element (per-line speaker + auto advance + audience filter)
    // =====================================================
    public enum LineAudience
    {
        Any = 0,
        OnlyAlly = 1,
        // 필요하면 확장: OnlyNPC, OnlyEnemy ...
    }

    [Serializable]
    public class LineElement
    {
        [Header("Speaker (per element)")]
        [Tooltip("대화창 이름칸 표시값. 공백이면 노드 기본 speaker를 사용합니다.")]
        public string speaker;

        [Tooltip("화자 내부 ID(Transform/Anchor 매핑용). 공백이면 노드 기본 speakerId를 사용합니다.")]
        public string speakerId;

        [Header("Text")]
        [TextArea(2, 6)]
        public string text;

        [Header("Auto Advance (optional)")]
        [Tooltip("라인 자동 넘김 시간(초). 0 이하 또는 미입력(=0)인 경우 노드 기본 규칙을 사용합니다.")]
        public float autoAdvanceSeconds = 0f;

        [Header("Audience Filter")]
        [Tooltip("Any=항상 출력, OnlyAlly=대상이 Ally 레이어(트리거의 allyLayerMask)일 때만 출력")]
        public LineAudience audience = LineAudience.Any;
    }

    /// <summary>
    /// 런타임 상태 초기화 (이벤트 트리거 호환용)
    /// </summary>
    public void ResetRuntimeFlags()
    {
        if (nodes == null) return;

        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] == null) continue;
            nodes[i].played = false;
        }
    }

    [Serializable]
    public class Node
    {
        [Tooltip("기존 트리거가 START/S1 같은 키로 노드를 찾는 구조면 그대로 사용합니다. (데일리는 예: START 만 써도 OK)")]
        public string key;

        [Tooltip("노드 기본 화자 표시 이름(엘리먼트의 speaker가 공백이면 이 값을 사용)")]
        public string speaker = "NPC";

        [Tooltip("노드 기본 화자 내부 ID(엘리먼트의 speakerId가 공백이면 이 값을 사용)")]
        public string speakerId;

        // ------------------------------
        // ✅ NEW: Element sequences
        // ------------------------------
        [Header("Lines (Element)")]
        [Tooltip("기본 시퀀스(엘리먼트 단위). 비어있으면 Legacy lines를 자동 변환해서 사용합니다.")]
        public List<LineElement> main = new List<LineElement>();

        [Tooltip("Ally 분기 시퀀스(엘리먼트 단위). main 진행 중 OnlyAlly 라인이 '출력'되면 여기로 점프(락)합니다.")]
        public List<LineElement> ally = new List<LineElement>();

        // ------------------------------
        // Legacy (backward compatibility)
        // ------------------------------
        [Header("Legacy (String Lines)")]
        [Tooltip("레거시 라인 배열. main이 비어있을 때만 자동 변환 소스로 사용됩니다.")]
        [TextArea(2, 6)]
        public string[] lines;

        [Header("Optional")]
        public bool playOnlyOnce = false;

        [NonSerialized] public bool played;

        [Header("Auto Advance (Daily Only)")]
        [Tooltip("이 노드의 라인 자동 넘김 기본 시간(초). perLine이 있으면 perLine 우선.")]
        [Min(0f)]
        public float defaultAutoAdvanceSeconds = 1.5f;

        [Tooltip("라인별 자동 넘김 시간(초). 길이가 부족하면 defaultAutoAdvanceSeconds 적용")]
        public float[] perLineAutoAdvanceSeconds;

        public List<LineElement> GetMainResolved()
        {
            if (main != null && main.Count > 0) return main;

            var tmp = new List<LineElement>();
            if (lines == null) return tmp;

            for (int i = 0; i < lines.Length; i++)
            {
                float sec = 0f;
                if (perLineAutoAdvanceSeconds != null && i >= 0 && i < perLineAutoAdvanceSeconds.Length)
                    sec = perLineAutoAdvanceSeconds[i];

                tmp.Add(new LineElement
                {
                    speaker = speaker,
                    speakerId = speakerId,
                    text = lines[i],
                    autoAdvanceSeconds = sec,
                    audience = LineAudience.Any
                });
            }
            return tmp;
        }

        public List<LineElement> GetAllyResolved()
        {
            return ally ?? new List<LineElement>();
        }
    }

    public Node FindNode(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        return nodes.Find(n => n != null && string.Equals(n.key, key, StringComparison.Ordinal));
    }
}
