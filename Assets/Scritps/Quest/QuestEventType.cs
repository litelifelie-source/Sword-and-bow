public enum QuestEventType
{
    KillEnemy,
    TalkToNPC,
    EnterArea,
    GetItem,
    SpendGold,
    Custom,
    Reward,
    DialogueEnded // ✅ 추가
}

public readonly struct QuestEvent
{
    public readonly QuestEventType type;
    public readonly string key;   // 식별자(예: "Archer", "Jeanne", "CampGate")
    public readonly int value;    // 기본 1

    public QuestEvent(QuestEventType type, string key = null, int value = 1)
    {
        this.type = type;
        this.key = key;
        this.value = value;
    }
}
