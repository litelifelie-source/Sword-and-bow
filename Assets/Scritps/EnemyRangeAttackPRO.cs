using System;
using System.Collections;
using UnityEngine;

public class EnemyRangeAttackPRO : MonoBehaviour
{
    public enum FireMode
    {
        Projectile,
        Hitscan
    }

    [Header("Mode")]
    [Tooltip("Projectile=투사체 발사(화살/마법탄), Hitscan=즉발 레이(총)")]
    public FireMode fireMode = FireMode.Hitscan;

    [Header("Attack")]
    public float attackRange = 6f;
    public float attackCooldown = 0.25f;
    public int damage = 8;

    [Tooltip("공격 시작 후 IsAttacking을 자동으로 false로 내릴 시간(초). 애니 이벤트 없을 때 굳는 것 방지용")]
    [Min(0f)] public float autoEndAttackTime = 0.12f;

    [Header("Target Filter")]
    [Tooltip("UnitTeam을 못 찾을 때만 쓰는 백업 타겟 레이어")]
    public LayerMask fallbackTargetLayer;

    [Tooltip("히트스캔에서 먼저 맞으면 차단되는 장애물 레이어(벽/엄폐물)")]
    public LayerMask obstacleMask;

    [Header("Muzzle / Origin")]
    [Tooltip("발사 시작점 오프셋(총구/손 위치). 2D 기준")]
    public Vector2 muzzleOffset = new Vector2(0.5f, 0.2f);

    // ------------------------------------------------------------
    // Projectile
    // ------------------------------------------------------------
    [Header("Projectile (FireMode=Projectile)")]
    [Tooltip("발사할 투사체 프리팹 (예: ArrowProjectile)")]
    public ArrowProjectile projectilePrefab;

    [Tooltip("투사체 스폰을 방향으로 조금 더 밀고 싶을 때")]
    public float projectileSpawnForward = 0.0f;

    // ------------------------------------------------------------
    // Hitscan VFX (optional)
    // ------------------------------------------------------------
    [Header("Hitscan VFX (Optional)")]
    [Tooltip("선택: 라인렌더러가 있으면 총알 궤적을 잠깐 보여줍니다.")]
    public LineRenderer lineRenderer;

    [Min(0f)]
    public float lineShowTime = 0.04f;

    // ------------------------------------------------------------
    // SFX
    // ------------------------------------------------------------
    [Header("SFX")]
    [Tooltip("발사 사운드(총/화살). 공격 시작 시 재생")]
    public AudioClip fireSfx;

    [Tooltip("명중 사운드(선택). 타겟에 데미지 들어갈 때 재생")]
    public AudioClip hitSfx;

    [Tooltip("사용할 AudioSource. 비워두면 자동으로 Get/Add 합니다.")]
    public AudioSource audioSource;

    [Range(0f, 1f)] public float fireVolume = 1f;
    [Range(0f, 1f)] public float hitVolume = 1f;

    // ------------------------------------------------------------
    // Anim
    // ------------------------------------------------------------
    [Header("Anim")]
    public Animator animator;

    [Header("Anim - Attack Direction (BlendTree)")]
    [Tooltip("켜면 Trigger 1개 + (AttackX, AttackY) float 파라미터로 공격 방향을 보냅니다.\n" +
             "Animator에 Attack 스테이트 1개를 만들고, Motion을 2D Freeform Directional BlendTree로 설정하세요.\n" +
             "BlendTree 입력 파라미터는 (AttackX, AttackY)로 사용하시면 됩니다.")]
    public bool useBlendTreeAttack = true;

    [Tooltip("BlendTree 공격 진입용 Trigger 이름")]
    public string trigAttack = "Attack";

    [Tooltip("BlendTree X 파라미터 이름 (예: AttackX)")]
    public string paramAttackX = "AttackX";

    [Tooltip("BlendTree Y 파라미터 이름 (예: AttackY)")]
    public string paramAttackY = "AttackY";

    [Tooltip("정중앙(좌/우가 애매)일 때 흔들림을 막는 데드존입니다. (추천 0.10~0.15)")]
    [Range(0f, 0.49f)]
    public float attackAxisDeadzone = 0.12f;

    [Header("Anim - Legacy Triggers (4-way)")]
    [Tooltip("useBlendTreeAttack=false일 때만 사용됩니다. (기존 4방 트리거)")]
    public string trigUp = "AttackUp";
    public string trigDown = "AttackDown";
    public string trigLeft = "AttackLeft";
    public string trigRight = "AttackRight";

    private int _lastAttackXSign = 1;
    private float _nextAttackTime;
    private UnitTeam _myTeam;
    private Coroutine _lineRoutine;
    private Coroutine _autoEndRoutine;

    public bool IsAttacking { get; private set; }

    private void Awake()
    {
        _myTeam = GetComponentInParent<UnitTeam>();
        if (animator == null) animator = GetComponentInChildren<Animator>(true);

        if (lineRenderer != null)
            lineRenderer.enabled = false;

        // SFX: AudioSource 자동 확보
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
    }

    /// <summary>
    /// AI가 호출:
    /// target : 타겟 Transform(팀/다운 체크용)
    /// aimRaw : (targetPos - myPos)
    /// </summary>
    public bool TryStartAttack(Transform target, Vector2 aimRaw)
    {
        if (!isActiveAndEnabled) return false;
        if (Time.time < _nextAttackTime) return false;

        if (!target || !target.gameObject.activeInHierarchy) return false;

        // Down 체크
        Health thp = target.GetComponentInChildren<Health>(true);
        if (thp != null && thp.IsDown) return false;

        // 적대 체크
        if (!IsHostileTarget(target)) return false;

        if (aimRaw.sqrMagnitude < 0.0001f) return false;

        float dist = aimRaw.magnitude;
        if (dist > attackRange) return false;

        _nextAttackTime = Time.time + attackCooldown;
        IsAttacking = true;

        // 애니 이벤트가 없어도 굳지 않게 자동 해제(옵션)
        if (_autoEndRoutine != null) StopCoroutine(_autoEndRoutine);
        if (autoEndAttackTime > 0f)
            _autoEndRoutine = StartCoroutine(AutoEndAttack(autoEndAttackTime));

        Vector2 shotDir = aimRaw.normalized;
        Vector2 animAxes = Snap8Axes(aimRaw);

        // 애니 방향 전달
        PlayAttackAnimDirectional(animAxes);

        Vector2 origin = (Vector2)transform.position + muzzleOffset;

        // 발사음
        if (fireSfx != null && audioSource != null)
            audioSource.PlayOneShot(fireSfx, fireVolume);

        if (fireMode == FireMode.Hitscan)
        {
            DoHitscan(origin, shotDir);
        }
        else
        {
            DoProjectile(origin, shotDir);
        }

        return true;
    }

    // 애니 이벤트에서 호출해도 되고, 안 써도 됨(쿨다운 기반)
    public void EndAttack()
    {
        IsAttacking = false;
        if (_autoEndRoutine != null)
        {
            StopCoroutine(_autoEndRoutine);
            _autoEndRoutine = null;
        }
    }

    private IEnumerator AutoEndAttack(float t)
    {
        yield return new WaitForSeconds(t);
        IsAttacking = false;
        _autoEndRoutine = null;
    }

    // =========================================================
    // Hitscan
    // =========================================================
    private void DoHitscan(Vector2 origin, Vector2 dir)
    {
        int mask = obstacleMask.value | fallbackTargetLayer.value;

        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, dir, attackRange, mask);
        Vector2 end = origin + dir * attackRange;

        if (hits == null || hits.Length == 0)
        {
            DrawLine(origin, end);
            return;
        }

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;

            // 0) 자기 자신(또는 자식) 콜라이더 스킵 (자해 방지)
            if (hit.collider.transform.IsChildOf(transform))
                continue;

            // 여기 도달했다 = "첫 유효 충돌"
            end = hit.point;

            // 1) 장애물 먼저면 차단
            int bit = 1 << hit.collider.gameObject.layer;
            bool isObstacle = (obstacleMask.value & bit) != 0;
            if (isObstacle)
                break;

            // 2) 타겟이면: 팀/다운 체크 후 데미지
            Health hp = hit.collider.GetComponentInParent<Health>();
            if (hp != null && !hp.IsDown)
            {
                // UnitTeam이 있는 경우: 아군/적군 오발 방지
                UnitTeam other = hit.collider.GetComponentInParent<UnitTeam>();
                if (other == null || IsHostileTeam(other))
                {
                    hp.TakeDamage(damage);

                    // 명중음(선택)
                    if (hitSfx != null && audioSource != null)
                        audioSource.PlayOneShot(hitSfx, hitVolume);
                }
            }

            break;
        }

        DrawLine(origin, end);
    }

    private void DrawLine(Vector2 origin, Vector2 end)
    {
        if (lineRenderer == null) return;

        if (_lineRoutine != null) StopCoroutine(_lineRoutine);
        _lineRoutine = StartCoroutine(ShowLine(origin, end));
    }

    private IEnumerator ShowLine(Vector2 a, Vector2 b)
    {
        lineRenderer.enabled = true;
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, a);
        lineRenderer.SetPosition(1, b);

        if (lineShowTime > 0f)
            yield return new WaitForSeconds(lineShowTime);

        lineRenderer.enabled = false;
        _lineRoutine = null;
    }

    // =========================================================
    // Projectile
    // =========================================================
    private void DoProjectile(Vector2 origin, Vector2 dir)
    {
        if (projectilePrefab == null) return;

        Vector2 spawn = origin + dir * projectileSpawnForward;

        ArrowProjectile proj = Instantiate(projectilePrefab, spawn, Quaternion.identity);
        if (!proj) return;

        proj.Initialize(dir, damage, transform);
    }

    // =========================================================
    // Anim
    // =========================================================
    private void PlayAttackAnimDirectional(Vector2 animAxes)
    {
        if (animator == null) return;

        if (useBlendTreeAttack)
        {
            // 8방 축 값(-1/0/1)을 BlendTree에 전달
            animator.SetFloat(paramAttackX, animAxes.x);
            animator.SetFloat(paramAttackY, animAxes.y);

            if (!string.IsNullOrEmpty(trigAttack))
                animator.SetTrigger(trigAttack);

            // 마지막 좌/우 기억(업/다운에서 중앙일 때 흔들림 방지)
            if (Mathf.Abs(animAxes.x) > 0.01f)
                _lastAttackXSign = animAxes.x > 0f ? 1 : -1;

            return;
        }

        // Legacy (기존 4방 트리거)
        if (Mathf.Abs(animAxes.x) > Mathf.Abs(animAxes.y))
        {
            if (animAxes.x >= 0f) animator.SetTrigger(trigRight);
            else animator.SetTrigger(trigLeft);
        }
        else
        {
            if (animAxes.y >= 0f) animator.SetTrigger(trigUp);
            else animator.SetTrigger(trigDown);
        }
    }

    private Vector2 Snap8Axes(Vector2 v)
    {
        // v는 (targetPos - myPos) 같은 벡터
        if (v.sqrMagnitude < 0.0001f) return Vector2.right;

        float ax = Mathf.Abs(v.x);
        float ay = Mathf.Abs(v.y);

        float x = 0f;
        float y = 0f;

        // 주축(가장 큰 축)을 먼저 결정
        if (ay >= ax)
        {
            y = v.y >= 0f ? 1f : -1f;

            // 업/다운 상황에서 좌우 분기 (대각 공격 지원)
            if (ax > attackAxisDeadzone) x = v.x >= 0f ? 1f : -1f;
            else x = 0f;
        }
        else
        {
            x = v.x >= 0f ? 1f : -1f;

            if (ay > attackAxisDeadzone) y = v.y >= 0f ? 1f : -1f;
            else y = 0f;
        }

        // 업/다운인데 X가 0이면 마지막 좌/우 유지
        if (Mathf.Abs(y) > 0.01f && Mathf.Abs(x) < 0.01f)
            x = _lastAttackXSign;

        return new Vector2(x, y);
    }

    // =========================================================
    // Team filter
    // =========================================================
    private bool IsHostileTarget(Transform target)
    {
        UnitTeam other = target.GetComponentInParent<UnitTeam>();

        // UnitTeam 없으면 백업 레이어로만 판정
        if (_myTeam == null || other == null)
        {
            int layerBit = 1 << target.gameObject.layer;
            return (fallbackTargetLayer.value & layerBit) != 0;
        }

        return IsHostileTeam(other);
    }

    private bool IsHostileTeam(UnitTeam other)
    {
        if (_myTeam.team == Team.NPC) return false;
        if (other.team == Team.NPC) return false;

        return (_myTeam.team == Team.Ally && other.team == Team.Enemy)
            || (_myTeam.team == Team.Enemy && other.team == Team.Ally);
    }
}