using UnityEngine;

/// <summary>
/// TreasureItemPickup (E키 줍기 버전)
/// - 플레이어가 범위(Trigger) 안에 들어오면 줍기 가능 상태가 되고,
/// - E키를 눌러야만 획득됩니다.
/// - 획득 시: (선택) RewardBundle 지급 + QuestEvent(GetItem) Push + (선택) UI 피드백 호출.
/// </summary>
[DisallowMultipleComponent]
public class TreasureItemPickup : MonoBehaviour
{
    [Header("Quest GetItem")]
    [Tooltip("퀘스트 Step에서 listenKey로 매칭할 아이템 키입니다. 예: 'CHEST_GEM_01'")]
    public string itemKey;

    [Tooltip("퀘스트에 누적할 획득 수량입니다. (보통 1)")]
    [Min(1)] public int amount = 1;

    [Header("Interact")]
    [Tooltip("줍기 키")]
    public KeyCode pickupKey = KeyCode.E;

    [Tooltip("플레이어가 범위 안에 있을 때만 줍기 가능")]
    public bool requireInRange = true;

    [Header("Feedback (optional)")]
    [Tooltip("플레이어가 범위 안에 있을 때 띄울 안내 문구(예: 'E: 줍기'). UI 연결이 없다면 로그로만 표시될 수 있습니다.")]
    public string hintText = "E: 줍기";

    [Tooltip("획득 시 띄울 표시용 아이템 이름(예: '루비'). 비워두면 itemKey를 대신 사용합니다.")]
    public string displayName;

    [Header("Reward (optional)")]
    [Tooltip("아이템 획득 시 실제 보상(골드/퀘스트이벤트 등)을 지급하고 싶으면 넣습니다. 비우면 보상 지급 없이 퀘스트 이벤트만 발생합니다.")]
    public RewardBundle rewardOnPickup;

    [Tooltip("RewardBundle 적용을 담당하는 RewardManager(비워두면 씬에서 자동 탐색).")]
    public RewardManager rewardManager;

    [Header("QuestManager (optional)")]
    [Tooltip("QuestEvent(GetItem)를 보낼 QuestManager(비워두면 QuestManager.I 또는 씬 탐색).")]
    public QuestManager questManager;

    [Header("Detection")]
    [Tooltip("플레이어 태그. Trigger에 들어온 Collider2D의 tag가 이 값과 같아야 줍기 가능해집니다.")]
    public string playerTag = "Player";

    [Header("One-shot")]
    [Tooltip("한 번 획득되면 즉시 파괴합니다.")]
    public bool destroyOnPickup = true;

    [Header("Debug")]
    [Tooltip("디버그 로그 출력")]
    public bool debugLog = false;

    bool _picked;
    bool _playerInRange;

    private void Awake()
    {
        if (!questManager) questManager = QuestManager.I ? QuestManager.I : FindFirstObjectByType<QuestManager>();
        if (!rewardManager) rewardManager = RewardManager.I ? RewardManager.I : FindFirstObjectByType<RewardManager>();

        // 안전장치: Trigger가 없으면 '범위'가 성립이 안 해요.
        if (requireInRange)
        {
            var col = GetComponent<Collider2D>();
            if (!col && debugLog) Debug.LogWarning("[Pickup] Collider2D가 없습니다. 범위 판정이 안 됩니다.", this);
            if (col && !col.isTrigger && debugLog) Debug.LogWarning("[Pickup] Collider2D.isTrigger=false 입니다. 범위 판정이 안 됩니다.", this);
        }
    }

    private void Update()
    {
        if (_picked) return;

        if (requireInRange && !_playerInRange) return;

        // ✅ E키를 눌러야 줍기
        if (Input.GetKeyDown(pickupKey))
        {
            if (debugLog) Debug.Log($"[Pickup] pickupKey pressed. key={itemKey}", this);
            Pickup();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_picked) return;
        if (!other || !other.CompareTag(playerTag)) return;

        _playerInRange = true;

        // 여기서 UI 힌트를 띄우고 싶으면, 프로젝트의 Hint UI에 연결하시면 됩니다.
        if (debugLog) Debug.Log($"[Pickup] In range. Hint='{hintText}'", this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (_picked) return;
        if (!other || !other.CompareTag(playerTag)) return;

        _playerInRange = false;
        if (debugLog) Debug.Log("[Pickup] Out of range.", this);
    }

    private void Pickup()
    {
        _picked = true;

        // (선택) 보상 지급
        if (rewardOnPickup != null && rewardManager != null)
        {
            string ctx = $"PICKUP:{itemKey}";
            rewardManager.Give(rewardOnPickup, ctx);
        }

        // ✅ 퀘스트 GetItem 이벤트
        if (questManager != null)
        {
            questManager.PushEvent(QuestEventType.GetItem, itemKey, Mathf.Max(1, amount));
        }
        else
        {
            Debug.LogWarning("[Pickup] QuestManager가 없습니다. GetItem 이벤트를 보낼 수 없어요.", this);
        }

        // ✅ “뭘 먹었는지” 피드백 (일단 로그라도 확실히)
        string nameToShow = string.IsNullOrEmpty(displayName) ? itemKey : displayName;
        Debug.Log($"[GET ITEM] {nameToShow} x{Mathf.Max(1, amount)}", this);

        if (destroyOnPickup)
            Destroy(gameObject);
    }
}