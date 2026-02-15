using UnityEngine;
using UnityEngine.UI;

public class Health : MonoBehaviour
{
    [Header("HP Settings")]
    public int maxHP = 100;
    [SerializeField] private int currentHP;

    [Header("Player Hearts (optional)")]
    [Tooltip("플레이어만 체크하세요. HP가 0이 될 때마다 하트가 1개 줄고, 하트가 남아있으면 HP가 풀로 회복됩니다.")]
    public bool isPlayer = false;
    public int maxHearts = 3;
    [SerializeField] private int currentHearts;
    public int CurrentHearts => currentHearts;

    [Header("Hearts UI (optional)")]
    [Tooltip("Canvas의 HeartsUI를 넣어주세요. 비워도 자동으로 찾아봅니다.")]
    public HeartsUI heartsUI;

    [Header("Bar Settings")]
    public Vector3 barOffset = new Vector3(0, -0.6f, 0);
    public float barWidth = 1.2f;
    public float barHeight = 0.15f;

    [Header("Down (Non-Player)")]
    [Tooltip("플레이어가 아닌 유닛은 HP 0이면 기절(Down) 상태로 만들고, 이동/공격 스크립트를 전부 끕니다.")]
    public bool downOnZeroHP = true;

   [Header("Invincible")]
   [SerializeField] private bool isInvincible = false;
   public bool IsInvincible => isInvincible;

public void SetInvincible(bool v)
{
    isInvincible = v;
}

    [Tooltip("기절 시 꺼줄 스크립트(이동/공격/AI 전부)를 여기에 넣으세요. 비워두면 자동으로 찾아서 끕니다(권장).")]
    public Behaviour[] disableBehavioursOnDown;

    [Tooltip("기절 시 콜라이더도 끄고 싶으면 여기에 넣으세요(선택).")]
    public Collider2D[] disableCollidersOnDown;

    // ✅ “기절 상태”는 HP값이 아니라 플래그로 고정(중요)
    [SerializeField] private bool isDownFlag = false;
    public bool IsDown => isDownFlag;

    private Image hpFill;
    private Transform canvasTransform;
    private Sprite whiteSprite;

    // ✅ Down 시 레이어 변경용
    private int originalLayer;

    private void Awake()
    {
        // 루트 기준으로 원래 레이어 저장
        originalLayer = transform.root.gameObject.layer;
    }

    void Start()
    {
        currentHP = maxHP;

        // ✅ 플레이어 하트 초기화
        if (isPlayer)
        {
            currentHearts = Mathf.Clamp(maxHearts, 0, 999);
            if (heartsUI == null) heartsUI = FindFirstObjectByType<HeartsUI>();
            SyncHeartsUI();
        }

        CreateHPBar();
        RefreshBarColor();
        UpdateHPBar();
    }

    void LateUpdate()
    {
        if (canvasTransform != null && Camera.main != null)
            canvasTransform.forward = Camera.main.transform.forward;
    }

    // ✅ 플레이어 하트 UI 동기화
    private void SyncHeartsUI()
    {
        if (!isPlayer) return;
        if (heartsUI == null) return;
        heartsUI.SetHearts(currentHearts);
    }

    public void RefreshBarColor()
    {
        if (hpFill == null) return;

        UnitTeam t = ResolveTeam();
        if (t != null && t.team == Team.Ally)
            hpFill.color = new Color(0.27f, 0.58f, 1f);
        else
            hpFill.color = new Color(0.85f, 0.2f, 0.2f);
    }

    public void TakeDamage(int damage)
 {
    // ✅ 이미 기절/사망 처리된 상태면 무시
    if (isDownFlag) return;
    if (currentHP <= 0) return;
    if (isInvincible) return;

    currentHP -= damage;
    currentHP = Mathf.Clamp(currentHP, 0, maxHP);

    UpdateHPBar();

    if (currentHP <= 0)
    {
        if (isPlayer)
        {
            Die_Player();
            return;
        }

        // ✅ 팀 확인
        UnitTeam t = ResolveTeam();

        // ✅ Ally는 죽으면 제거(Destroy)
        if (t != null && t.team == Team.Ally)
        {
            Die_DestroyNonPlayer();
            return;
        }

        // ✅ Enemy만 기절(영입용)
        if (downOnZeroHP) Down_NonPlayer();
        else Die_DestroyNonPlayer();
    }
}

    // ✅ 플레이어 하트만 리셋하고 싶을 때(예: 재시작)
    public void ResetHeartsToMax()
    {
        if (!isPlayer) return;
        currentHearts = Mathf.Clamp(maxHearts, 0, 999);
        SyncHeartsUI();
    }

    public void RestoreFullHP()
    {
        currentHP = maxHP;
        UpdateHPBar();
    }

    // ✅ (선택) 기절 해제 + HP 회복까지 한 번에 하고 싶으면 이걸 쓰세요
    public void ReviveFull()
    {
        isDownFlag = false;
        currentHP = maxHP;
        UpdateHPBar();
    }

    void UpdateHPBar()
    {
        if (hpFill != null)
            hpFill.fillAmount = (float)currentHP / maxHP;
    }

    // ----------------------------
    // HP 0 처리
    // ----------------------------

    private void Die_Player()
    {
        currentHearts = Mathf.Max(0, currentHearts - 1);
        Debug.Log($"[Player Die] {name} HP=0 -> ❤️ -1, 남은 하트={currentHearts}");

        SyncHeartsUI();

        if (currentHearts > 0)
        {
            currentHP = maxHP;
            UpdateHPBar();
            return;
        }

        Debug.Log("[Player] GAME OVER");
        Destroy(transform.root.gameObject);
    }

    private void Down_NonPlayer()
    {
        if (isDownFlag) return;
        isDownFlag = true;

        Debug.Log($"[Down] {name} HP=0 -> 이동/공격 스크립트 OFF (기절)");

        // ✅ 물리 멈춤
        Rigidbody2D rb2d = transform.root.GetComponent<Rigidbody2D>();
        if (rb2d != null) rb2d.linearVelocity = Vector2.zero;

        // 1) 인스펙터에 지정된 스크립트 끄기
        bool usedExplicitList = disableBehavioursOnDown != null && disableBehavioursOnDown.Length > 0;
        if (usedExplicitList)
        {
            foreach (var b in disableBehavioursOnDown)
                if (b != null) b.enabled = false;
        }
        else
        {
            // 2) ✅ 자동: 전투/이동/공격 관련 MonoBehaviour를 찾아서 끄기(Health/UnitTeam/HeartsUI는 제외)
            DisableCombatScriptsAutomatically();
        }

        // (선택) 콜라이더 끄기
        if (disableCollidersOnDown != null && disableCollidersOnDown.Length > 0)
        {
            foreach (var c in disableCollidersOnDown)
                if (c != null) c.enabled = false;
        }
    }

    private void Die_DestroyNonPlayer()
    {
        Debug.Log($"[Destroy] {name} HP=0 -> Destroy");
        Destroy(transform.root.gameObject);
    }

    private void DisableCombatScriptsAutomatically()
    {
        // ✅ 루트 기준으로 전부 긁어서 끈다(그래야 자식에 붙은 공격 스크립트도 꺼짐)
        var monos = transform.root.GetComponentsInChildren<MonoBehaviour>(true);

        foreach (var m in monos)
        {
            if (m == null) continue;
            if (m == this) continue;                // Health는 끄면 안 됨
            if (m is UnitTeam) continue;            // 팀 정보 유지
            if (m is HeartsUI) continue;            // UI 유지(혹시 붙어있다면)

            // ✅ 핵심: "움직임/공격/AI"는 대부분 MonoBehaviour라 그냥 꺼도 됨
            m.enabled = false;
        }
    }

    // ----------------------------
    // 팀/컴포넌트 탐색
    // ----------------------------

    private UnitTeam ResolveTeam()
    {
        UnitTeam t = GetComponent<UnitTeam>();
        if (t != null) return t;

        t = GetComponentInParent<UnitTeam>();
        if (t != null) return t;

        t = GetComponentInChildren<UnitTeam>();
        if (t != null) return t;

        if (transform.root != null)
        {
            t = transform.root.GetComponent<UnitTeam>();
            if (t != null) return t;

            t = transform.root.GetComponentInChildren<UnitTeam>();
            if (t != null) return t;
        }

        return null;
    }

    // ----------------------------
    // Stun Layer 처리
    // ----------------------------

    private void ApplyStunLayer()
    {
        int stunLayer = LayerMask.NameToLayer("Stun");
        if (stunLayer != -1)
            SetLayerRecursively(transform.root.gameObject, stunLayer);
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        for (int i = 0; i < obj.transform.childCount; i++)
            SetLayerRecursively(obj.transform.GetChild(i).gameObject, layer);
    }

    // ----------------------------
    // HP Bar 생성
    // ----------------------------

    void CreateHPBar()
    {
        whiteSprite = CreateWhiteSprite();

        GameObject canvasObj = new GameObject("HP_Canvas");
        canvasObj.transform.SetParent(transform);
        canvasObj.transform.localPosition = barOffset;

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(barWidth * 100f, barHeight * 100f);
        canvasRect.localScale = Vector3.one * 0.01f;

        canvasTransform = canvasObj.transform;

        GameObject backObj = new GameObject("HP_Back");
        backObj.transform.SetParent(canvasObj.transform, false);

        Image backImage = backObj.AddComponent<Image>();
        backImage.sprite = whiteSprite;
        backImage.color = Color.black;

        RectTransform backRect = backObj.GetComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0.5f, 0.5f);
        backRect.anchorMax = new Vector2(0.5f, 0.5f);
        backRect.sizeDelta = canvasRect.sizeDelta;
        backRect.anchoredPosition = Vector2.zero;

        GameObject fillObj = new GameObject("HP_Fill");
        fillObj.transform.SetParent(backObj.transform, false);

        hpFill = fillObj.AddComponent<Image>();
        hpFill.sprite = whiteSprite;

        hpFill.type = Image.Type.Filled;
        hpFill.fillMethod = Image.FillMethod.Horizontal;
        hpFill.fillOrigin = (int)Image.OriginHorizontal.Left;

        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0.5f, 0.5f);
        fillRect.anchorMax = new Vector2(0.5f, 0.5f);
        fillRect.sizeDelta = canvasRect.sizeDelta;
        fillRect.anchoredPosition = Vector2.zero;
    }

    private Sprite CreateWhiteSprite()
    {
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
    }
}
