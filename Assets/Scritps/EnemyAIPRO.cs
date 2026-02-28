using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAIPro : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 2f;
    public float detectionRange = 8f;

    [Header("Range Combat")]
    [Tooltip("사거리 밖이면 접근, 사거리 안이면 정지 후 사격")]
    public float attackRange = 4f;

    [Tooltip("너무 가까우면 뒤로 빠지기(0이면 비활성)")]
    public float keepAwayRange = 1.8f;

    [Tooltip("keepAway 경계에서 앞뒤로 떨림 방지(0.2~0.4 추천)")]
    public float keepAwayHysteresis = 0.3f;

    [Header("Targeting")]
    [Tooltip("탐색 대상으로 잡을 레이어 후보(보통 Ally/Enemy 포함).\nUnitTeam으로 최종 적대판정합니다.\n- ⚠️ Enemy 레이어가 candidateLayer에 없으면 '스캔 자체가 안 됨'처럼 보입니다.")]
    public LayerMask candidateLayer;

    // =========================================================
    // ✅ Ally Follow
    // =========================================================
    [Header("Ally Follow")]
    [Tooltip("내 팀이 Ally면 플레이어를 따라갑니다. (단, 아래 allyPreferCombat이 ON이면 적이 보이면 전투 우선)")]
    public bool followPlayerWhenAlly = true;

    [Tooltip("✅ Ally 상태에서 '적이 보이면 전투 우선'으로 할지.\nON이면: 적 탐색 -> 전투 / OFF이면: 무조건 플레이어 추종(기존 동작)")]
    public bool allyPreferCombat = true;

    [Tooltip("플레이어 오브젝트에 붙어있는 태그 (기본 Player)")]
    public string playerTag = "Player";

    [Tooltip("플레이어와 이 거리 이내면 멈춥니다(0이면 비활성).")]
    public float followStopRange = 1.2f;

    [Tooltip("followStopRange 경계 떨림 방지(0.2~0.4 추천)")]
    public float followStopHysteresis = 0.25f;

    [Header("Refs")]
    public Rigidbody2D rb;
    public Animator anim;
    public EnemyRangeAttackPRO rangeAttack;

    [Header("Anim Direction Options")]
    [Tooltip("업/다운 이동(또는 바라보기)에서도 좌/우 방향을 세분화해서 애니를 고릅니다.\n예) 위로 움직일 때, 타겟이 왼쪽이면 UpLeft(=MoveY=1, MoveX=-1)처럼 처리\n- Animator가 이 파라미터 조합을 해석하도록(BlendTree/State) 만들어져 있어야 합니다.")]
    public bool splitUpDownMoveByTargetX = true;

    [Tooltip("타겟 X 차이가 이 값보다 작으면(절대값) '정중앙'으로 취급합니다.\n정중앙일 때는 마지막 좌/우 방향을 유지합니다.")]
    [Min(0f)] public float upDownMoveXDeadzone = 0.05f;

    private int _lastFaceXSign = 1; // 1=Right, -1=Left
    public UnitTeam myTeam;

    private Transform target;

    // 물리 적용용 의도 값
    private Vector2 desiredVel;
    private Vector2 desiredDir;
    private bool desiredMove;

    // keep-away 래치
    private bool isKeepingAway;

    // follow stop 래치
    private bool isFollowStopping;

    // ✅ 플레이어 캐시(매 프레임 Find 방지)
    private Transform _player;

    private void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!anim) anim = GetComponentInChildren<Animator>(true);

        if (!rangeAttack)
        {
            rangeAttack = GetComponentInChildren<EnemyRangeAttackPRO>(true);
            if (!rangeAttack) rangeAttack = GetComponent<EnemyRangeAttackPRO>();
        }

        if (!myTeam) myTeam = GetComponentInParent<UnitTeam>();

        rb.freezeRotation = true;
        rb.gravityScale = 0f;

        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        if (anim == null) Debug.LogError("[EnemyAIPro] Animator를 찾지 못했습니다.", this);
        if (rangeAttack == null) Debug.LogError("[EnemyAIPro] EnemyRangeAttackPRO를 찾지 못했습니다.", this);
    }

    private void Update()
    {
        if (rb == null || anim == null || rangeAttack == null) return;

        // ✅ 타겟이 Destroy/비활성화되면 비우기
        if (!target || !target.gameObject.activeInHierarchy)
            target = null;

        // ✅ NPC면 아무것도 안 함(전환 대응)
        if (myTeam != null && myTeam.team == Team.NPC)
        {
            ForceStop();
            target = null;
            isKeepingAway = false;
            isFollowStopping = false;
            return;
        }

        // ✅ 공격 중이면 이동 멈춤 (모든 팀 공통)
        if (rangeAttack.IsAttacking)
        {
            ForceStop();
            return;
        }

        bool isAlly = (myTeam != null && myTeam.team == Team.Ally);

        // =========================================================
        // ✅ Ally 모드: (선택) 전투 우선 → 적이 있으면 전투, 없으면 플레이어 추종
        // =========================================================
        if (isAlly && followPlayerWhenAlly && allyPreferCombat)
        {
            Transform hostile = FindTarget(); // ✅ 여기서 스캔을 '반드시' 함
            if (hostile != null)
            {
                target = hostile;
                isFollowStopping = false; // 전투 전환 시 follow 래치 초기화
                DoCombat(target);
                return;
            }

            // 적이 없으면 follow로 폴백
            DoFollowPlayer();
            return;
        }

        // =========================================================
        // ✅ Ally 모드: 전투 우선 OFF면 기존대로 "무조건 추종"
        // =========================================================
        if (isAlly && followPlayerWhenAlly && !allyPreferCombat)
        {
            DoFollowPlayer();
            return;
        }

        // =========================================================
        // ✅ 기본(Enemy 등): 적 탐색 -> 전투
        // =========================================================
        target = FindTarget();
        if (target == null)
        {
            ForceStop();
            isKeepingAway = false;
            return;
        }

        DoCombat(target);
    }

    // -----------------------------
    // ✅ Ally Follow
    // -----------------------------
    private void DoFollowPlayer()
    {
        if (_player == null)
        {
            GameObject go = GameObject.FindGameObjectWithTag(playerTag);
            _player = go != null ? go.transform : null;
        }

        if (_player == null || !_player.gameObject.activeInHierarchy)
        {
            ForceStop();
            target = null;
            isKeepingAway = false;
            isFollowStopping = false;
            return;
        }

        target = _player;

        Vector2 to = (Vector2)(target.position - transform.position);
        UpdateFaceXSign(to);
        float dist = to.magnitude;

        if (followStopRange > 0f)
        {
            float enter = followStopRange;
            float exit = followStopRange + followStopHysteresis;

            if (!isFollowStopping && dist <= enter) isFollowStopping = true;
            else if (isFollowStopping && dist >= exit) isFollowStopping = false;
        }
        else
        {
            isFollowStopping = false;
        }

        if (isFollowStopping)
        {
            ForceStop();
            return;
        }

        SetMoveIntent(to.normalized);
    }

    // -----------------------------
    // ✅ Combat (기존 로직 함수화)
    // -----------------------------
    private void DoCombat(Transform t)
    {
        if (t == null)
        {
            ForceStop();
            isKeepingAway = false;
            return;
        }

        Vector2 toEnemy = (Vector2)(t.position - transform.position);
        UpdateFaceXSign(toEnemy);
        float distEnemy = toEnemy.magnitude;

        // keep-away 토글 방지(들어갈 때/나올 때 범위 분리)
        float enterKA = keepAwayRange;
        float exitKA = keepAwayRange + keepAwayHysteresis;

        if (keepAwayRange > 0f)
        {
            if (!isKeepingAway && distEnemy < enterKA) isKeepingAway = true;
            else if (isKeepingAway && distEnemy > exitKA) isKeepingAway = false;
        }
        else isKeepingAway = false;

        // 1) 너무 가까우면 뒤로 빠짐
        if (isKeepingAway)
        {
            Vector2 away = (-toEnemy).normalized;
            SetMoveIntent(away);
            return;
        }

        // 2) 사거리 밖이면 접근
        if (distEnemy > attackRange)
        {
            SetMoveIntent(toEnemy.normalized);
            return;
        }

        // 3) 사거리 안이면 멈추고 사격
        ForceStop();
        rangeAttack.TryStartAttack(t, toEnemy);
    }


    private void UpdateFaceXSign(Vector2 to)
    {
        if (!splitUpDownMoveByTargetX) return;

        float ax = to.x;
        if (ax < -upDownMoveXDeadzone) _lastFaceXSign = -1;
        else if (ax > upDownMoveXDeadzone) _lastFaceXSign = 1;
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        rb.linearVelocity = desiredVel;

        float max = moveSpeed;
        if (rb.linearVelocity.sqrMagnitude > max * max)
            rb.linearVelocity = rb.linearVelocity.normalized * max;

        if (anim != null)
        {
            if (desiredMove)
            {
                anim.SetBool("IsMoving", true);

                float mx = desiredDir.x;
                float my = desiredDir.y;

                // ✅ 업/다운 이동일 때도 좌/우를 표현(UpLeft/UpRight 같은 분기용)
                if (splitUpDownMoveByTargetX)
                {
                    bool isVertical = Mathf.Abs(my) >= Mathf.Abs(mx);
                    if (isVertical)
                    {
                        // 순수 수직에 가까우면 MoveX를 마지막 바라본 방향으로 보정
                        if (Mathf.Abs(mx) < upDownMoveXDeadzone) mx = _lastFaceXSign;
                        else mx = (mx < 0f) ? -1f : 1f;

                        my = (my < 0f) ? -1f : 1f;
                    }
                }

                anim.SetFloat("MoveX", mx);
                anim.SetFloat("MoveY", my);
            }
            else
            {
                anim.SetBool("IsMoving", false);
            }
        }
    }

    private void SetMoveIntent(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
        dir.Normalize();

        desiredMove = true;
        desiredDir = dir;
        desiredVel = dir * moveSpeed;
    }

    private void ForceStop()
    {
        desiredMove = false;
        desiredVel = Vector2.zero;
        if (anim != null) anim.SetBool("IsMoving", false);
    }

    private Transform FindTarget()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectionRange, candidateLayer);

        float best = Mathf.Infinity;
        Transform result = null;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i];
            if (col == null) continue;

            UnitTeam ut = col.GetComponentInParent<UnitTeam>();
            if (ut == null) continue;

            if (!IsHostile(ut)) continue;

            Health hp = ut.GetComponentInChildren<Health>(true);
            if (hp == null) continue;
            if (hp.IsDown) continue;

            Transform tr = ut.transform.root;
            if (!tr || !tr.gameObject.activeInHierarchy) continue;

            float d = Vector2.Distance(transform.position, tr.position);
            if (d < best)
            {
                best = d;
                result = tr;
            }
        }

        return result;
    }

    private bool IsHostile(UnitTeam other)
    {
        if (other == null) return false;
        if (myTeam == null) return false;
        if (myTeam.team == Team.NPC || other.team == Team.NPC) return false;

        return (myTeam.team == Team.Ally && other.team == Team.Enemy)
            || (myTeam.team == Team.Enemy && other.team == Team.Ally);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = new Color(1f, 0.3f, 0.3f, 1f);
        Gizmos.DrawWireSphere(transform.position, attackRange);

        if (keepAwayRange > 0f)
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 1f);
            Gizmos.DrawWireSphere(transform.position, keepAwayRange);
        }
    }
}