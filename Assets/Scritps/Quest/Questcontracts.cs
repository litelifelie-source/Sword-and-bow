using System;

public interface IQuestStepKeyProvider
{
    // stepIndex에 해당하는 "노드 키"를 최대한 안전하게 산출
    bool TryGetStepKey(string questId, int stepIndex, out string key);
}

// QuestManager -> Bridge 로 "특정 노드 키를 재생해달라" 요청하는 표준 신호
public delegate void DialogueNodeRequest(string questId, string nodeKey);