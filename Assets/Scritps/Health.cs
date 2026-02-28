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

    [Header("HP Bar Layer")]
    [Tooltip("HP바(HP_Canvas 및 자식들)를 생성할 때 사용할 '오브젝트 레이어' 이름입니다. (예: UI)\n⚠️ 해당 레이어가 프로젝트에 없으면 Default 레이어로 생성됩니다.")]
    public string hpBarObjectLayerName = "UI";

    [Header("Down (Non-Player)")]
    [Tooltip("플레이어가 아닌 유닛은 HP 0이면 기절(Down) 상태로 만들고, 이동/공격 스크립트를 전부 끕니다.")]
    public bool downOnZeroHP = true;

    [Header("Shield")]
    [SerializeField] private int currentShield = 0;
    [SerializeField] private float shieldExpireTime = 0f;
    public int CurrentShield => currentShield;
    public bool HasShield => currentShield > 0;

    [Header("Invincible")]
    [SerializeField] private bool isInvincible = false;
    public bool IsInvincible => isInvincible;

    [Header("Damage Modifier")]
    [Range(0.1f, 2.0f)]
    public float damageTakenMultiplier = 1f; // 0.75면 25% 피해 감소

    public void SetInvincible(bool v) => isInvincible = v;

    [Tooltip("기절 시 꺼줄 스크립트(이동/공격/AI 전부)를 여기에 넣으세요. 비워두면 자동으로 찾아서 끕니다(권장).")]
    public Behaviour[] disableBehavioursOnDown;

    [Tooltip("기절 시 콜라이더도 끄고 싶으면 여기에 넣으세요(선택). ⚠ 여기에 영입 판정 콜라이더가 들어가면 영입이 안 됩니다.")]
    public Collider2D[] disableCollidersOnDown;

    [SerializeField] private bool isDownFlag = false;
    public bool IsDown => isDownFlag;

    public bool IsDead => (isDownFlag || currentHP <= 0);

    private Image hpFill;
    private Image shieldFill;
    private Transform canvasTransform;
    private Sprite whiteSprite;

    private int originalLayer;

    public static System.Action<string> OnKillLog;
    private bool killReported = false;

    [Header("Quest Kill Key (optional)")]
    [Tooltip("프리팹에서 직접 지정하세요. 병사=Soldier / 궁수=Archer. 비우면 휴리스틱으로 판별합니다.")]
    public string killKeyOverride = "";

    private void Awake()
    {
        originalLayer = transform.root.gameObject.layer;
    }

    private void Start()
    {
        currentHP = maxHP;

        if (isPlayer)
        {
            currentHearts = Mathf.Clamp(maxHearts, 0, 999);
            if (heartsUI == null) heartsUI = FindFirstObjectByType<HeartsUI>();
            SyncHeartsUI();
        }

        CreateHPBar();
        RefreshBarColor();
        UpdateHPBar();
        UpdateShieldBar();
    }

    private void LateUpdate()
    {
        if (canvasTransform != null && Camera.main != null)
            canvasTransform.forward = Camera.main.transform.forward;

        if (currentShield > 0 && Time.time >= shieldExpireTime)
            ClearShield();
    }

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

        if (shieldFill != null)
            shieldFill.color = new Color(0.35f, 0.9f, 1f, 0.85f);
    }

    public void TakeDamage(int damage)
    {
        if (isDownFlag) return;
        if (currentHP <= 0) return;
        if (isInvincible) return;
        if (damage <= 0) return;

        damage = Mathf.CeilToInt(damage * damageTakenMultiplier);
        if (damage <= 0) return;

        if (currentShield > 0)
        {
            int absorb = Mathf.Min(currentShield, damage);
            currentShield -= absorb;
            damage -= absorb;
            UpdateShieldBar();

            if (damage <= 0) return;
        }

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

            ReportKillIfEnemy();

            UnitTeam t = ResolveTeam();

            if (t != null && t.team == Team.Ally)
            {
                Die_DestroyNonPlayer();
                return;
            }

            if (downOnZeroHP) Down_NonPlayer();
            else Die_DestroyNonPlayer();
        }
    }

    public void ResetHeartsToMax()
    {
        if (!isPlayer) return;
        currentHearts = Mathf.Clamp(maxHearts, 0, 999);
        SyncHeartsUI();
    }

    public void Heal(int amount)
    {
        if (amount <= 0) return;
        if (isDownFlag) return;
        if (currentHP <= 0) return;
        if (currentHP >= maxHP) return;

        currentHP = Mathf.Clamp(currentHP + amount, 0, maxHP);
        UpdateHPBar();
    }

    public void RestoreFullHP()
    {
        currentHP = maxHP;
        UpdateHPBar();
    }

    public void ReviveFull()
    {
        isDownFlag = false;
        currentHP = maxHP;
        UpdateHPBar();

        foreach (var col in transform.root.GetComponentsInChildren<Collider2D>(true))
        {
            col.isTrigger = false;
            col.enabled = true;
        }

        Rigidbody2D rb = transform.root.GetComponent<Rigidbody2D>();
        if (rb != null && rb.bodyType == RigidbodyType2D.Kinematic)
            rb.bodyType = RigidbodyType2D.Dynamic;
    }

    private void UpdateHPBar()
    {
        if (hpFill != null)
            hpFill.fillAmount = (float)currentHP / maxHP;
    }

    private void UpdateShieldBar()
    {
        if (shieldFill == null) return;

        float ratio = 0f;
        if (maxHP > 0) ratio = (float)currentShield / maxHP;

        shieldFill.fillAmount = Mathf.Clamp01(ratio);

        var c = shieldFill.color;
        c.a = (currentShield > 0) ? 0.85f : 0f;
        shieldFill.color = c;
    }

    public void GrantShield(int amount, float duration)
    {
        if (amount <= 0) return;
        if (isDownFlag) return;
        if (currentHP <= 0) return;

        currentShield = Mathf.Max(currentShield, amount);
        shieldExpireTime = Time.time + Mathf.Max(0f, duration);
        UpdateShieldBar();
    }

    public void ClearShield()
    {
        currentShield = 0;
        shieldExpireTime = 0f;
        UpdateShieldBar();
    }

    private void ReportKillIfEnemy()
    {
        if (killReported) return;
        if (isPlayer) return;

        UnitTeam t = ResolveTeam();
        if (t == null) return;
        if (t.team != Team.Enemy) return;

        killReported = true;

        string key = killKeyOverride;

        if (string.IsNullOrEmpty(key))
        {
            bool isArcher = transform.root.GetComponentInChildren<EnemyArcherAttack>(true) != null;
            key = isArcher ? "Archer" : "Soldier";
        }

        if (QuestManager.I != null)
            QuestManager.I.PushEvent(QuestEventType.KillEnemy, key, 1);

        string msg = (key == "Archer") ? "🏹 궁수 처치" : "🗡 병사 처치";
        OnKillLog?.Invoke(msg);

        Debug.Log($"[KILL] {msg} / {transform.root.name}");
    }

    private void Die_Player()
    {
        currentHearts = Mathf.Max(0, currentHearts - 1);
        Debug.Log($"[Player Die] {name} HP=0 -> ❤️ -1, 남은 하트={currentHearts}");

        SyncHeartsUI();

        if (currentHearts > 0)
        {
            ClearShield();
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

        ClearShield();

        Debug.Log($"[Down] {name} HP=0 -> 이동/공격 스크립트 OFF (기절)");

        foreach (var col in transform.root.GetComponentsInChildren<Collider2D>())
        {
            col.isTrigger = true;
        }

        Rigidbody2D rb = transform.root.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.simulated = true;
        }

        bool usedExplicitList = disableBehavioursOnDown != null && disableBehavioursOnDown.Length > 0;
        if (usedExplicitList)
        {
            foreach (var b in disableBehavioursOnDown)
                if (b != null) b.enabled = false;
        }
        else
        {
            DisableCombatScriptsAutomatically();
        }

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
        var monos = transform.root.GetComponentsInChildren<MonoBehaviour>(true);

        foreach (var m in monos)
        {
            if (m == null) continue;
            if (m == this) continue;
            if (m is UnitTeam) continue;
            if (m is HeartsUI) continue;

            m.enabled = false;
        }
    }

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

    private void CreateHPBar()
    {
        whiteSprite = CreateWhiteSprite();

        int layer = LayerMask.NameToLayer(hpBarObjectLayerName);
        if (layer < 0)
        {
            Debug.LogWarning($"[Health] Layer '{hpBarObjectLayerName}'가 없어서 HP바를 Default 레이어로 생성합니다. (Project Settings > Tags and Layers에서 레이어 추가하세요)");
            layer = 0; // Default
        }

        GameObject canvasObj = new GameObject("HP_Canvas");
        canvasObj.transform.SetParent(transform);
        canvasObj.transform.localPosition = barOffset;

        // ✅ 생성 직후부터 오브젝트 레이어를 UI로 고정 (Default로 찍히는 구간 제거)
        SetLayerRecursively(canvasObj, layer);

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingLayerName = "UI";
        canvas.sortingOrder = 500;
        canvas.renderMode = RenderMode.WorldSpace;

        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(barWidth * 100f, barHeight * 100f);
        canvasRect.localScale = Vector3.one * 0.01f;

        canvasTransform = canvasObj.transform;

        GameObject backObj = new GameObject("HP_Back");
        backObj.transform.SetParent(canvasObj.transform, false);
        backObj.layer = layer;

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
        fillObj.layer = layer;

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

        GameObject shieldObj = new GameObject("Shield_Fill");
        shieldObj.transform.SetParent(backObj.transform, false);
        shieldObj.layer = layer;

        shieldFill = shieldObj.AddComponent<Image>();
        shieldFill.sprite = whiteSprite;
        shieldFill.type = Image.Type.Filled;
        shieldFill.fillMethod = Image.FillMethod.Horizontal;
        shieldFill.fillOrigin = (int)Image.OriginHorizontal.Left;

        RectTransform shieldRect = shieldObj.GetComponent<RectTransform>();
        shieldRect.anchorMin = new Vector2(0.5f, 0.5f);
        shieldRect.anchorMax = new Vector2(0.5f, 0.5f);
        shieldRect.sizeDelta = canvasRect.sizeDelta;
        shieldRect.anchoredPosition = Vector2.zero;

        RefreshBarColor();
        UpdateShieldBar();
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private Sprite CreateWhiteSprite()
    {
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
    }
}