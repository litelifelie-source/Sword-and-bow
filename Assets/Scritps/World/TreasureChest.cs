using System.Collections;
using UnityEngine;

/// <summary>
/// TreasureChest
/// - 플레이어가 근처(Trigger)에서 E키를 누르면 상자를 연출하고,
/// - 아이템 프리팹(픽업 오브젝트)을 1회 스폰합니다.
/// - 실제 퀘스트 GetItem 처리(다음 인덱스)는 픽업 스크립트(TreasureItemPickup)가 QuestManager.PushEvent(GetItem)를 호출하며 진행됩니다.
/// </summary>
[DisallowMultipleComponent]
public class TreasureChest : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("상자 상호작용 키")]
    public KeyCode interactKey = KeyCode.E;

    [Header("Player Detection")]
    [Tooltip("플레이어 태그. Trigger에 들어온 Collider2D의 tag가 이 값과 같아야 상호작용이 가능합니다.")]
    public string playerTag = "Player";

    [Tooltip("Trigger 방식으로 근접을 판정합니다. 이 오브젝트(상자)에 Collider2D(isTrigger=true)가 있어야 합니다.")]
    public bool useTriggerRange = true;

    [Header("Animation (optional)")]
    [Tooltip("상자 애니메이터(없으면 애니메이션 없이 동작).")]
    public Animator animator;

    [Tooltip("열기 트리거 파라미터 이름(Animator Trigger).")]
    public string openTriggerName = "Open";

    [Tooltip("아이템 스폰을 애니메이션 이후로 지연시키고 싶을 때 사용(초). 0이면 즉시 스폰.")]
    [Min(0f)] public float spawnDelay = 0.25f;

    [Header("Spawn (required)")]
    [Tooltip("스폰할 아이템 픽업 프리팹. (TreasureItemPickup 컴포넌트가 붙어있어야 합니다.)")]
    public GameObject itemPickupPrefab;

    [Tooltip("아이템 스폰 위치(비워두면 상자 Transform 위치에 스폰).")]
    public Transform spawnPoint;

    [Tooltip("아이템 스폰 시 회전. 보통 0으로 두면 됩니다.")]
    public Vector3 spawnEulerAngles;

    [Header("One-shot")]
    [Tooltip("한 번 열면 다시는 열리지 않게 합니다.")]
    public bool openOnce = true;

    [Header("Disappear (optional)")]
    [Tooltip("상자를 연 뒤 일정 시간 후 상자를 제거(비활성/삭제)할지 여부")]
    public bool removeChestAfterOpen = false;

    [Tooltip("removeChestAfterOpen가 true일 때, 상자를 제거할 때까지 대기 시간(초). 애니메이션 길이에 맞추세요.")]
    [Min(0f)] public float removeDelay = 0.8f;

    [Header("Debug")]
    [Tooltip("디버그 로그 출력")]
    public bool debugLog = false;

    bool _playerInRange;
    bool _opened;
    GameObject _spawned;

    IEnumerator CoRemoveAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        // 상자를 완전히 삭제하고 싶으면:
        Destroy(gameObject);

        // 만약 "삭제 말고 비활성화"가 좋으면 Destroy 대신 아래로:
        // gameObject.SetActive(false);
    }

    private void Awake()
    {
        if (!animator) animator = GetComponentInChildren<Animator>();

        // 안전장치: Trigger 사용인데 Collider2D가 없거나 isTrigger=false면 로그
        if (useTriggerRange)
        {
            var col = GetComponent<Collider2D>();
            if (!col && debugLog) Debug.LogWarning("[Chest] Collider2D가 없습니다. Trigger 판정이 안 됩니다.", this);
            if (col && !col.isTrigger && debugLog) Debug.LogWarning("[Chest] Collider2D.isTrigger=false 입니다. Trigger 판정이 안 됩니다.", this);
        }
    }

    private void Update()
    {
        if (_opened && openOnce) return;
        if (!_playerInRange) return;

        if (Input.GetKeyDown(interactKey))
        {
            if (debugLog) Debug.Log($"[Chest] Interact key pressed. opened={_opened}", this);
            TryOpen();
        }
    }

    private void TryOpen()
    {
        if (_opened && openOnce) return;

        if (!itemPickupPrefab)
        {
            Debug.LogWarning("[Chest] itemPickupPrefab이 비어있습니다. 아이템을 스폰할 수 없어요.", this);
            return;
        }
        // (선택) 상자 제거 예약
        if (removeChestAfterOpen)
        {
            StartCoroutine(CoRemoveAfterDelay(removeDelay));
        }

        _opened = true;

        // 애니메이션 트리거
        if (animator && !string.IsNullOrEmpty(openTriggerName))
        {
            animator.ResetTrigger(openTriggerName);
            animator.SetTrigger(openTriggerName);
        }

        // 아이템 스폰 (지연 가능)
        if (spawnDelay <= 0f) SpawnItem();
        else StartCoroutine(CoSpawnAfterDelay(spawnDelay));
    }

    IEnumerator CoSpawnAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        SpawnItem();
    }

    private void SpawnItem()
    {
        if (_spawned) return; // 중복 방지

        Vector3 pos = spawnPoint ? spawnPoint.position : transform.position;
        Quaternion rot = Quaternion.Euler(spawnEulerAngles);

        _spawned = Instantiate(itemPickupPrefab, pos, rot);

        if (debugLog) Debug.Log($"[Chest] Item spawned: {_spawned.name}", this);
    }

    // -------------------------
    // Trigger range (2D)
    // -------------------------
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!useTriggerRange) return;
        if (!other || !other.CompareTag(playerTag)) return;

        _playerInRange = true;
        if (debugLog) Debug.Log("[Chest] Player enter range", this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!useTriggerRange) return;
        if (!other || !other.CompareTag(playerTag)) return;

        _playerInRange = false;
        if (debugLog) Debug.Log("[Chest] Player exit range", this);
    }
}
