using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class AllyFollow : MonoBehaviour
{
    [Header("Auto Register (Player Formation)")]
    [Tooltip("ON이면 시작/Enable 시 플레이어의 FormationManager를 자동으로 찾아 Register 합니다.")]
    public bool autoRegisterToPlayerFormation = true;

    [Tooltip("플레이어 오브젝트 Tag (기본: Player). 플레이어에 이 Tag가 반드시 있어야 합니다.")]
    public string playerTag = "Player";

    [Tooltip("플레이어에서 FormationManager를 찾을 때, 자식까지 검색할지 여부입니다.\n- 플레이어 루트에 FormationManager가 없고 자식에 붙어있으면 ON이 필요합니다.")]
    public bool findFormationInChildren = true;

    [Tooltip("자동 등록 재시도 간격(초). 시작 시 못 찾았을 때 주기적으로 다시 찾습니다.")]
    [Min(0.05f)] public float autoRegisterRetryInterval = 0.5f;

    [Header("Formation (used by FormationManager / PlayerCapture)")]
    [Tooltip("자동 등록을 쓰면 비워둬도 됩니다. 수동으로 특정 Formation에 붙이고 싶으면 직접 넣어도 됩니다.")]
    public FormationManager formation; // ✅ 타입: FormationManager
    [Tooltip("FormationManager가 할당하는 슬롯 인덱스입니다. 직접 수정하지 않는 것을 권장합니다.")]
    public int slotIndex = -1;

    [Header("Move")]
    public float moveSpeed = 2.2f;

    [Header("Stop Distances")]
    public float formationStopDist = 0.08f;
    private float _chaseStopDist = 1.2f;

    [Header("Animator (optional)")]
    public Animator anim;
    public string paramIsMoving = "IsMoving";
    public string paramMoveX = "MoveX";
    public string paramMoveY = "MoveY";

    private Rigidbody2D rb;

    private Transform _chaseTarget;
    private bool _blockMove = false;
    private Vector2 _lastDir = Vector2.down;

    private float _nextRetryTime = 0f;
    private bool _registeredToFormation = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;

        if (anim == null)
            anim = GetComponentInChildren<Animator>(true);
    }

    private void OnEnable()
    {
        _registeredToFormation = false;
        _nextRetryTime = 0f;

        if (autoRegisterToPlayerFormation)
            TryAutoRegister(force: true);
    }

    private void OnDisable()
    {
        AutoUnregister();
    }

    private void OnDestroy()
    {
        AutoUnregister();
    }

    private void FixedUpdate()
    {
        // ✅ 자동 등록: 시작에 못 찾았으면 주기적으로 재시도
        if (autoRegisterToPlayerFormation && (formation == null || !_registeredToFormation))
        {
            TryAutoRegister(force: false);
        }

        if (_blockMove)
        {
            StopInternal();
            return;
        }

        // 1) 전투 추적 우선
        if (_chaseTarget != null && _chaseTarget.gameObject.activeInHierarchy)
        {
            FollowPoint(_chaseTarget.position, _chaseStopDist);
            return;
        }

        // 2) 진형 따라가기
        if (formation != null && slotIndex >= 0)
        {
            Vector2 slotPos = formation.GetSlotWorldPosition(slotIndex);
            FollowPoint(slotPos, formationStopDist);
            return;
        }

        StopInternal();
    }

    // === AllyAttack에서 호출하는 API ===

    public void StartChase(Transform target, float stopDist)
    {
        _chaseTarget = target;
        _chaseStopDist = Mathf.Max(0.01f, stopDist);

        if (_blockMove)
            StopInternal();
    }

    public void StopChase()
    {
        _chaseTarget = null;
    }

    public void SetBlockMove(bool block)
    {
        _blockMove = block;
        if (_blockMove)
            StopInternal();
    }

    // === Auto Register ===

    private void TryAutoRegister(bool force)
    {
        if (!force && Time.time < _nextRetryTime) return;
        _nextRetryTime = Time.time + autoRegisterRetryInterval;

        // 이미 수동으로 formation이 들어와 있고 등록만 안 된 상태면 Register만 시도
        if (formation != null)
        {
            if (!_registeredToFormation)
            {
                formation.Register(this);
                _registeredToFormation = true;
            }
            return;
        }

        // 플레이어 찾기
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player == null) return;

        FormationManager fm = null;

        if (findFormationInChildren)
            fm = player.GetComponentInChildren<FormationManager>(true);
        else
            fm = player.GetComponent<FormationManager>();

        if (fm == null) return;

        formation = fm;
        formation.Register(this);
        _registeredToFormation = true;
    }

    private void AutoUnregister()
    {
        if (formation == null) return;

        // 등록 여부와 무관하게 안전하게 시도
        formation.Unregister(this);
        _registeredToFormation = false;

        // 필요하면 formation 참조도 끊고 싶으면 아래 주석 해제
        // formation = null;
        // slotIndex = -1;
    }

    // === 내부 유틸 ===

    private void FollowPoint(Vector2 targetPos, float stopDist)
    {
        Vector2 me = rb.position;
        Vector2 to = targetPos - me;

        float stopSqr = stopDist * stopDist;
        if (to.sqrMagnitude <= stopSqr)
        {
            StopInternal();
            return;
        }

        Vector2 dir = to.normalized;

#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = dir * moveSpeed;
#else
        rb.velocity = dir * moveSpeed;
#endif

        if (dir.sqrMagnitude > 0.0001f)
            _lastDir = dir;

        UpdateAnim(true, _lastDir);
    }

    private void StopInternal()
    {
        if (rb != null)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = Vector2.zero;
#else
            rb.velocity = Vector2.zero;
#endif
        }
        UpdateAnim(false, _lastDir);
    }

    private void UpdateAnim(bool moving, Vector2 dir)
    {
        if (anim == null) return;
        anim.SetBool(paramIsMoving, moving);
        anim.SetFloat(paramMoveX, dir.x);
        anim.SetFloat(paramMoveY, dir.y);
    }
}