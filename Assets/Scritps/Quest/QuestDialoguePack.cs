using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Dialogue/Quest Dialogue Pack", fileName = "QuestDialoguePack_")]
public class QuestDialoguePack : ScriptableObject
{
    [Header("Identity")]
    public string questId; // QuestLine.questId와 반드시 일치

    [Header("Nodes")]
    public List<Node> nodes = new();

    // =====================================================
    // Line Element (per-line speaker + audience filter)
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

        [Header("Audience Filter")]
        [Tooltip("Any=항상 출력, OnlyAlly=대상이 Ally 레이어(브릿지의 allyLayerMask)일 때만 출력")]
        public LineAudience audience = LineAudience.Any;
    }

    [Serializable]
    public class Node
    {
        [Tooltip("START / S1 / S2 / COMPLETE 같은 이벤트 키")]
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
        public List<LineElement> main = new();

        [Tooltip("Ally 분기 시퀀스(엘리먼트 단위). main 진행 중 OnlyAlly 라인이 '출력'되면 여기로 점프(락)합니다.")]
        public List<LineElement> ally = new();

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

        /// <summary>
        /// main이 비어있으면 legacy lines를 자동 변환하여 반환합니다.
        /// (에디터 데이터 마이그레이션 없이도 동작)
        /// </summary>
        public List<LineElement> GetMainResolved()
        {
            if (main != null && main.Count > 0) return main;

            var tmp = new List<LineElement>();
            if (lines == null) return tmp;

            for (int i = 0; i < lines.Length; i++)
            {
                tmp.Add(new LineElement
                {
                    speaker = speaker,
                    speakerId = speakerId,
                    text = lines[i],
                    audience = LineAudience.Any
                });
            }
            return tmp;
        }

        /// <summary>
        /// ally 시퀀스 반환 (null-safe)
        /// </summary>
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

    public void ResetRuntimeFlags()
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] == null) continue;
            nodes[i].played = false;
        }
    }
}
