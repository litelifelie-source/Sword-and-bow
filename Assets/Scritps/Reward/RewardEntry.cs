using System;
using UnityEngine;

[Serializable]
public class RewardEntry
{
    public RewardType type = RewardType.Gold;

    [Tooltip("Gold/Exp 등 수치형 보상")]
    public int amount = 0;

    [Tooltip("QuestEvent / RecruitAllyBySpeakerId 같은 '키'가 필요한 보상\n" +
             "- QuestEvent: key 그대로 QuestEvent.key로 전달\n" +
             "- RecruitAllyBySpeakerId: SpeakerIdTag.speakerId 와 일치하는 유닛을 Ally로 전환")]
    public string key;

    [Tooltip("QuestEvent의 value로 전달 (기본 1)\n" +
             "※ RecruitAllyBySpeakerId에서는 기본적으로 사용하지 않습니다.")]
    public int value = 1;

    public bool IsValid()
    {
        switch (type)
        {
            case RewardType.Gold:
                return amount != 0;

            case RewardType.QuestEvent:
                return !string.IsNullOrEmpty(key);

            case RewardType.RecruitAllyBySpeakerId:
                return !string.IsNullOrEmpty(key);

            default:
                return true;
        }
    }

    public override string ToString()
    {
        return type switch
        {
            RewardType.Gold => $"Gold({amount})",
            RewardType.QuestEvent => $"QuestEvent(key={key}, value={value})",
            RewardType.RecruitAllyBySpeakerId => $"RecruitAllyBySpeakerId(speakerId={key})",
            _ => $"{type}"
        };
    }
}