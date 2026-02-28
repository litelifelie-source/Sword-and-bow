using System;

public enum RewardType
{
    Gold = 0,

    // ✅ 확장 포인트
    // Item = 10,
    // Exp = 20,
    // Reputation = 30,

    // ✅ 퀘스트 이벤트를 보상처럼 쏘고 싶을 때 (예: 해금 트리거)
    QuestEvent = 100,

    // ✅ NEW: 특정 speakerId(=SpeakerIdTag) 유닛을 Ally로 전환
    RecruitAllyBySpeakerId = 200,
}
