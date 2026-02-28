using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 협동기 1단계 프로토타입:
/// - 슈바르트가 잔느 왼쪽으로 텔레포트
/// - 짧은 대사 2줄
/// - 잔느 마법진 모션 + 슈바르트 장전(장전 끝날때까지 슈바르트 고정)
/// - 큰 마법진 생성
/// - 슈바르트 난사 + 랜덤 이동 + 잔느 방패로 사격(잔느는 끝까지 위치 고정)
/// - 방패 트리거에서 도탄 발사
/// + SFX:
///   - 잔느 마법진 1회 원샷
///   - 슈바르트 난사 루프(반복) + 종료 시 페이드아웃
/// </summary>
public class CoopJeanneSchwarzSkill : MonoBehaviour
{
    [Header("Actors")]
    [Tooltip("잔느 Transform (보통 이 스크립트가 잔느에 붙으면 자동으로 self 사용)")]
    public Transform jeanne;

    [Tooltip("슈바르트 Transform (필수)")]
    public Transform schwarz;

    [Header("Animators")]
    [Tooltip("잔느 Animator (비우면 자식에서 자동 탐색)")]
    public Animator jeanneAnim;

    [Tooltip("슈바르트 Animator (비우면 자식에서 자동 탐색)")]
    public Animator schwarzAnim;

    [Header("Dialogue (Optional)")]
    [Tooltip("DialogueManager가 씬에 있으면 자동으로 사용합니다. 없으면 Debug.Log로 대체합니다.")]
    public bool useDialogueManagerIfExists = true;

    [Tooltip("슈바르트 대사(짧게 1줄)")]
    public string schwarzLine = "간다.";

    [Tooltip("잔느 대사(짧게 1줄)")]
    public string jeanneLine = "좋아요. 준비됐어요!";

    [Tooltip("대사 자동 넘김 시간(초)")]
    [Min(0.1f)] public float dialogueSecondsPerLine = 0.8f;

    [Header("Teleport")]
    [Tooltip("슈바르트를 잔느 기준 왼쪽으로 보낼 X 오프셋(월드 단위)")]
    public float teleportLeftOffsetX = 0.9f;

    [Tooltip("텔레포트 시 Y 보정값(지형에 따라 살짝 올리고 싶을 때)")]
    public float teleportYOffset = 0f;

    [Header("Pre-Cast Motions")]
    [Tooltip("잔느 '마법진 생성' 애니메이션 State 이름(Animator.Play에 사용)")]
    public string jeanneAnimState_MagicCircle = "named_잔느_마법진생성";

    [Tooltip("슈바르트 '마탄 장전' 애니메이션 State 이름(Animator.Play에 사용)")]
    public string schwarzAnimState_Reload = "named_슈바르트_마탄장전";

    [Tooltip("슈바르트 '난사' 애니메이션 State 이름(Animator.Play에 사용)")]
    public string schwarzAnimState_Barrage = "named_슈바르트_마탄난사";

    [Tooltip("프리캐스트 대기시간(모션이 보이도록 잠깐 기다림). 장전 대기와 별개로 추가 연출 텀입니다.")]
    [Min(0f)] public float preCastWait = 0.55f;

    [Header("VFX")]
    [Tooltip("커다란 마법진 프리팹(연출용). 잔느 위치에 생성됩니다.")]
    public GameObject bigMagicCirclePrefab;

    [Tooltip("마법진이 붙을 앵커(비우면 잔느 Transform)")]
    public Transform circleAnchor;

    [Tooltip("마법진 로컬 오프셋(잔느 기준)")]
    public Vector3 circleOffset = new Vector3(0f, 0.1f, 0f);

    [Header("Shield (Optional)")]
    [Tooltip("잔느 방패 Proc가 있으면 협동기 중 방패모드 진입을 이걸로 수행합니다.")]
    public JeanneGuardShieldProc jeanneShieldProc;

    [Header("Barrage - Movement")]
    [Tooltip("난사 중 슈바르트가 잔느 주변에서 이동할 최소 반경")]
    [Min(0f)] public float moveRadiusMin = 0.8f;

    [Tooltip("난사 중 슈바르트가 잔느 주변에서 이동할 최대 반경")]
    [Min(0f)] public float moveRadiusMax = 1.6f;

    [Tooltip("위치 변경 간격(초). 이 시간이 지나면 새 랜덤 위치로 이동합니다.")]
    [Min(0.05f)] public float moveInterval = 0.22f;

    [Tooltip("이동 보간 시간(초). 0이면 순간이동에 가깝게 됩니다.")]
    [Min(0f)] public float moveLerpTime = 0.10f;

    [Header("Barrage - Fire")]
    [Tooltip("난사 지속시간(초)")]
    [Min(0.1f)] public float barrageDuration = 2.2f;

    [Tooltip("초당 발사 수(=fireRate). 예: 18이면 1초에 18발")]
    [Min(1f)] public float fireRate = 18f;

    [Tooltip("마탄 프리팹(필수). CoopMagicBullet이 붙어 있어야 합니다.")]
    public GameObject magicBulletPrefab;

    [Tooltip("발사 위치(총구). 비우면 schwarz.position에서 생성합니다.")]
    public Transform muzzle;

    [Tooltip("슈바르트가 쏠 때 잔느 방패(도탄 트리거) 쪽으로 조준하는 앵커. 비우면 잔느 Transform.")]
    public Transform aimAtJeanneAnchor;

    [Tooltip("마탄 데미지(도탄된 탄이 적에게 줄 데미지)")]
    public int bulletDamage = 8;

    [Tooltip("마탄 속도")]
    [Min(0.1f)] public float bulletSpeed = 12f;

    [Tooltip("마탄 수명(초)")]
    [Min(0.1f)] public float bulletLifeTime = 2.2f;

    [Header("Lock Rules (This Coop)")]
    [Tooltip("협동기 시작~끝까지 잔느의 월드 위치를 고정합니다.")]
    public bool lockJeanneWholeCoop = true;

    [Tooltip("슈바르트는 '장전 모션이 끝날 때까지'만 위치를 고정합니다.")]
    public bool lockSchwarzWhileReload = true;

    [Tooltip("위치 고정 중 Rigidbody(2D/3D)가 있으면 속도를 0으로 만들어 밀림을 줄입니다.")]
    public bool zeroRigidVelocityWhileLocked = true;

    [Header("Reload Wait (Schwarz)")]
    [Tooltip("장전(마탄 장전) 애니메이션이 끝날 때까지 다음 단계(난사/이동)로 넘어가지 않습니다.")]
    public bool waitForReloadAnimEnd = true;

    [Tooltip("장전 상태로 진입했는지 확인하는 최대 대기시간(초). (전이/레이어 문제로 상태 진입이 안 될 때 무한대기 방지)")]
    [Min(0.05f)] public float reloadEnterTimeout = 0.35f;

    [Tooltip("장전이 끝난 것으로 인정할 normalizedTime 기준값. 보통 0.98~1.0 권장")]
    [Range(0.7f, 1.0f)] public float reloadFinishNormalized = 0.98f;

    [Header("SFX")]
    [Tooltip("잔느 마법진 '꽂는' 순간에 1회 재생할 사운드(원샷)")]
    public AudioClip sfxJeanneMagicCircleOneShot;

    [Tooltip("슈바르트 난사 중 반복 재생할 루프 사운드(지속 사운드). 난사 시작에 켜지고 종료 시 페이드로 꺼집니다.")]
    public AudioClip sfxSchwarzBarrageLoop;

    [Tooltip("원샷(SFX) 재생용 AudioSource. 비우면 자동 생성합니다.")]
    public AudioSource oneShotSource;

    [Tooltip("루프(SFX) 재생용 AudioSource. 비우면 자동 생성합니다.")]
    public AudioSource loopSource;

    [Tooltip("원샷 볼륨(0~1)")]
    [Range(0f, 1f)] public float oneShotVolume = 1f;

    [Tooltip("루프 볼륨(0~1)")]
    [Range(0f, 1f)] public float loopVolume = 0.85f;

    [Tooltip("난사 루프 종료 시 페이드아웃 시간(초). 0이면 즉시 정지.")]
    [Min(0f)] public float barrageLoopFadeOutTime = 0.12f;

    [Header("Safety / Debug")]
    [Tooltip("협동기 실행 중 중복 발동 방지")]
    public bool blockWhileRunning = true;

    [Tooltip("디버그 로그")]
    public bool debugLog = true;

    private bool _running;
    private GameObject _circleInstance;

    // ====== position lock runtime ======
    private Coroutine _posLockCo;
    private Vector3 _jeanneLockPos;
    private Vector3 _schwarzLockPos;
    private bool _lockJeanneNow;
    private bool _lockSchwarzNow;

    // ====== sfx runtime ======
    private Coroutine _loopFadeCo;

    private void Awake()
    {
        if (jeanne == null) jeanne = transform;
        if (jeanneAnim == null) jeanneAnim = GetComponentInChildren<Animator>(true);

        if (jeanneShieldProc == null)
            jeanneShieldProc = GetComponent<JeanneGuardShieldProc>() ?? GetComponentInChildren<JeanneGuardShieldProc>(true);

        if (circleAnchor == null) circleAnchor = jeanne;
        if (aimAtJeanneAnchor == null) aimAtJeanneAnchor = jeanne;
        if (muzzle == null && schwarz != null) muzzle = schwarz;

        if (schwarzAnim == null && schwarz != null)
            schwarzAnim = schwarz.GetComponentInChildren<Animator>(true);

        // ===== SFX AudioSources auto setup =====
        if (oneShotSource == null)
        {
            oneShotSource = gameObject.AddComponent<AudioSource>();
            oneShotSource.playOnAwake = false;
            oneShotSource.loop = false;
        }

        if (loopSource == null)
        {
            loopSource = gameObject.AddComponent<AudioSource>();
            loopSource.playOnAwake = false;
            loopSource.loop = true;
        }
    }

    public void TriggerCoop()
    {
        if (blockWhileRunning && _running) return;

        if (jeanne == null || schwarz == null)
        {
            Debug.LogWarning("[Coop] Missing actor references (jeanne/schwarz).", this);
            return;
        }

        StartCoroutine(CoRun());
    }

    private IEnumerator CoRun()
    {
        _running = true;

        // ✅ 협동기 전체 락 시작(잔느는 끝까지 고정)
        BeginPositionLock();

        // 1) 텔레포트: 슈바르트를 잔느 왼쪽으로
        Vector3 tp = jeanne.position + new Vector3(-Mathf.Abs(teleportLeftOffsetX), teleportYOffset, 0f);
        schwarz.position = tp;

        // 장전 고정 기준도 텔레포트 위치로 갱신
        _schwarzLockPos = tp;

        if (debugLog) Debug.Log("[Coop] Teleport Schwarz -> left of Jeanne", this);

        // 2) 짧은 대사(슈 -> 잔)
        yield return CoPlayTwoLinesDialogue();

        // 3) 잔느 마법진 / 슈바르트 장전
        PlayStateSafe(jeanneAnim, jeanneAnimState_MagicCircle, "jeanneAnimState_MagicCircle");
        PlayStateSafe(schwarzAnim, schwarzAnimState_Reload, "schwarzAnimState_Reload");

        // ✅ 잔느 마법진 1회 SFX
        PlayOneShot(sfxJeanneMagicCircleOneShot);

        // ✅ 슈바르트는 "장전 끝날 때까지" 위치 고정 ON
        if (lockSchwarzWhileReload)
            SetSchwarzLock(true);

        // ✅ 장전 모션 종료까지 대기(요구사항 핵심)
        if (waitForReloadAnimEnd && schwarzAnim != null && !string.IsNullOrEmpty(schwarzAnimState_Reload))
        {
            yield return CoWaitAnimStateFinish(
                schwarzAnim,
                schwarzAnimState_Reload,
                layerIndex: 0,
                enterTimeout: reloadEnterTimeout,
                finishNormalized: reloadFinishNormalized
            );
        }

        // ✅ 장전 끝 -> 슈바르트 고정 해제(이제부터 랜덤 이동 가능)
        if (lockSchwarzWhileReload)
            SetSchwarzLock(false);

        // 4) 큰 마법진 생성
        SpawnBigCircle();

        if (preCastWait > 0f)
            yield return new WaitForSeconds(preCastWait);

        // 5) 난사 단계: 잔느 방패모드, 슈바르트 난사
        if (jeanneShieldProc != null)
        {
            jeanneShieldProc.StartShield_FromDistributor();
        }
        PlayStateSafe(schwarzAnim, schwarzAnimState_Barrage, "schwarzAnimState_Barrage");

        // ✅ 난사 루프 SFX 시작(반복재생)
        StartLoop(sfxSchwarzBarrageLoop);

        // 6) 난사 루프(슈바르트 랜덤 이동)
        yield return CoBarrageLoop();

        // ✅ 난사 종료: 루프 SFX 페이드아웃 종료
        StopLoopWithFade();

        // 마무리
        CleanupCircle();

        // ✅ 락 종료
        EndPositionLock();

        _running = false;
    }

    private IEnumerator CoPlayTwoLinesDialogue()
    {
        bool usedDM = false;

        if (useDialogueManagerIfExists && DialogueManager.I != null)
        {
            string schwarzDisplayName = "슈바르트";
            string jeanneDisplayName = "잔느";

            string schwarzKey = ResolveSpeakerIdFromActor(schwarz, fallback: "Schwarz");
            string jeanneKey = ResolveSpeakerIdFromActor(jeanne, fallback: "Jeanne");

            usedDM = DialogueManager.I.PlayAutoAdvanceWithMetaOnUI(
                null,
                schwarzDisplayName, schwarzKey,
                new[] { schwarzDisplayName, jeanneDisplayName },
                new[] { schwarzKey, jeanneKey },
                new[] { schwarzLine, jeanneLine },
                dialogueSecondsPerLine,
                null,
                _ => { }
            );
        }

        if (!usedDM)
        {
            Debug.Log($"[CoopTalk] Schwarz: {schwarzLine}", this);
            yield return new WaitForSeconds(dialogueSecondsPerLine);
            Debug.Log($"[CoopTalk] Jeanne: {jeanneLine}", this);
            yield return new WaitForSeconds(dialogueSecondsPerLine);
            yield break;
        }

        yield return new WaitForSeconds(dialogueSecondsPerLine * 2f + 0.1f);
    }

    private void SpawnBigCircle()
    {
        CleanupCircle();

        if (bigMagicCirclePrefab == null) return;

        Transform anchor = circleAnchor != null ? circleAnchor : jeanne;
        Vector3 pos = anchor.position + circleOffset;

        _circleInstance = Instantiate(bigMagicCirclePrefab, pos, Quaternion.identity, anchor);
    }

    private void CleanupCircle()
    {
        if (_circleInstance != null)
        {
            Destroy(_circleInstance);
            _circleInstance = null;
        }
    }

    private IEnumerator CoBarrageLoop()
    {
        if (magicBulletPrefab == null)
        {
            Debug.LogWarning("[Coop] magicBulletPrefab is null. Barrage will run without shooting.", this);
        }

        float endTime = Time.time + barrageDuration;

        float fireInterval = 1f / Mathf.Max(1f, fireRate);
        float nextFire = Time.time;

        float nextMove = Time.time;

        Vector3 moveFrom = schwarz.position;
        Vector3 moveTo = schwarz.position;
        float moveT = 1f;

        while (Time.time < endTime)
        {
            // 이동 목표 갱신
            if (Time.time >= nextMove)
            {
                nextMove = Time.time + moveInterval;

                float r = UnityEngine.Random.Range(moveRadiusMin, Mathf.Max(moveRadiusMin, moveRadiusMax));
                float ang = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;

                // Y 흔들림은 조금 줄이기
                Vector3 offset = new Vector3(
                    Mathf.Cos(ang) * r,
                    Mathf.Sin(ang) * (r * 0.45f),
                    0f
                );

                moveFrom = schwarz.position;
                moveTo = jeanne.position + offset;
                moveT = 0f;
            }

            // 이동 보간(장전 끝난 뒤에는 슈바르트 락이 꺼져있어서 정상 이동)
            if (moveLerpTime <= 0f)
            {
                schwarz.position = moveTo;
            }
            else
            {
                moveT += Time.deltaTime / moveLerpTime;
                schwarz.position = Vector3.Lerp(moveFrom, moveTo, Mathf.Clamp01(moveT));
            }

            // 발사
            if (Time.time >= nextFire)
            {
                nextFire = Time.time + fireInterval;
                FireAtJeanneShield();
            }

            yield return null;
        }
    }

    private string ResolveSpeakerIdFromActor(Transform actor, string fallback)
    {
        if (actor == null) return fallback;

        var tag = actor.GetComponentInChildren<SpeakerIdTag>(true);
        if (tag != null && !string.IsNullOrEmpty(tag.speakerId))
            return tag.speakerId;

        return fallback;
    }

    private void FireAtJeanneShield()
    {
        if (magicBulletPrefab == null) return;

        Vector3 spawnPos = (muzzle != null) ? muzzle.position : schwarz.position;
        Transform aimAnchor = (aimAtJeanneAnchor != null) ? aimAtJeanneAnchor : jeanne;

        Vector2 dir = (aimAnchor.position - spawnPos);
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;

        GameObject go = Instantiate(magicBulletPrefab, spawnPos, Quaternion.identity);

        CoopMagicBullet b = go.GetComponent<CoopMagicBullet>();
        if (b == null)
        {
            Debug.LogWarning("[Coop] magicBulletPrefab needs CoopMagicBullet component.", go);
            Destroy(go);
            return;
        }

        // incoming: 슈바르트가 잔느 방패로 쏘는 탄 (도탄되면 팀이 Ally로 바뀜)
        b.InitIncoming(
            dir.normalized,
            bulletSpeed,
            bulletLifeTime,
            bulletDamage,
            schwarz,
            null   // ✅ 잔느 콜라이더를 무시하지 않게!
        );
    }

    private void PlayStateSafe(Animator a, string stateName, string fieldLabel)
    {
        if (a == null) return;
        if (string.IsNullOrEmpty(stateName))
        {
            if (debugLog) Debug.LogWarning($"[Coop] {fieldLabel} is empty. Animator.Play skipped.", this);
            return;
        }
        a.Play(stateName, 0, 0f);
    }

    // =====================================================
    // Position Lock System
    // - Jeanne: whole coop lock
    // - Schwarz: reload-only lock
    // =====================================================
    private void BeginPositionLock()
    {
        _jeanneLockPos = (jeanne != null) ? jeanne.position : Vector3.zero;
        _schwarzLockPos = (schwarz != null) ? schwarz.position : Vector3.zero;

        _lockJeanneNow = lockJeanneWholeCoop;
        _lockSchwarzNow = false; // 장전 때만 ON

        if (_posLockCo != null) StopCoroutine(_posLockCo);
        _posLockCo = StartCoroutine(CoPositionLockLoop());
    }

    private void EndPositionLock()
    {
        _lockJeanneNow = false;
        _lockSchwarzNow = false;

        if (_posLockCo != null)
        {
            StopCoroutine(_posLockCo);
            _posLockCo = null;
        }
    }

    private void SetSchwarzLock(bool on)
    {
        _lockSchwarzNow = on;

        // ON 되는 순간 기준 위치 저장
        if (on && schwarz != null)
            _schwarzLockPos = schwarz.position;
    }

    private IEnumerator CoPositionLockLoop()
    {
        while (_running)
        {
            if (_lockJeanneNow && jeanne != null)
            {
                jeanne.position = _jeanneLockPos;

                if (zeroRigidVelocityWhileLocked)
                {
                    Rigidbody2D rb2d = jeanne.GetComponent<Rigidbody2D>();
                    if (rb2d != null) rb2d.linearVelocity = Vector2.zero;

                    Rigidbody rb3d = jeanne.GetComponent<Rigidbody>();
                    if (rb3d != null) rb3d.linearVelocity = Vector3.zero;
                }
            }

            if (_lockSchwarzNow && schwarz != null)
            {
                schwarz.position = _schwarzLockPos;

                if (zeroRigidVelocityWhileLocked)
                {
                    Rigidbody2D rb2d = schwarz.GetComponent<Rigidbody2D>();
                    if (rb2d != null) rb2d.linearVelocity = Vector2.zero;

                    Rigidbody rb3d = schwarz.GetComponent<Rigidbody>();
                    if (rb3d != null) rb3d.linearVelocity = Vector3.zero;
                }
            }

            yield return null;
        }
    }

    // =====================================================
    // Wait for Reload End
    // =====================================================
    private IEnumerator CoWaitAnimStateFinish(
        Animator anim,
        string stateName,
        int layerIndex,
        float enterTimeout,
        float finishNormalized
    )
    {
        if (anim == null || string.IsNullOrEmpty(stateName))
            yield break;

        float enterDeadline = Time.time + Mathf.Max(0.05f, enterTimeout);

        // 1) 상태 진입 대기(전이 시간 고려) - 타임아웃 있음
        bool entered = false;
        while (Time.time < enterDeadline)
        {
            AnimatorStateInfo st = anim.GetCurrentAnimatorStateInfo(layerIndex);
            if (st.IsName(stateName))
            {
                entered = true;
                break;
            }
            yield return null;
        }

        if (!entered)
        {
            if (debugLog) Debug.LogWarning($"[Coop] Reload state '{stateName}' not entered within timeout. Continue.", this);
            yield break;
        }

        // 2) 종료까지 대기(normalizedTime 기준)
        while (true)
        {
            if (anim.IsInTransition(layerIndex))
            {
                yield return null;
                continue;
            }

            AnimatorStateInfo st = anim.GetCurrentAnimatorStateInfo(layerIndex);

            if (!st.IsName(stateName))
                break;

            if (st.normalizedTime >= finishNormalized)
                break;

            yield return null;
        }
    }

    // =====================================================
    // SFX Helpers
    // =====================================================
    private void PlayOneShot(AudioClip clip)
    {
        if (clip == null) return;
        if (oneShotSource == null) return;

        oneShotSource.volume = oneShotVolume;
        oneShotSource.PlayOneShot(clip);
    }

    private void StartLoop(AudioClip clip)
    {
        if (clip == null) return;
        if (loopSource == null) return;

        // 페이드 중이면 끊고 새로 시작
        if (_loopFadeCo != null)
        {
            StopCoroutine(_loopFadeCo);
            _loopFadeCo = null;
        }

        // 이미 같은 클립이 재생 중이면 그대로 유지
        if (loopSource.isPlaying && loopSource.clip == clip)
            return;

        loopSource.Stop();
        loopSource.clip = clip;
        loopSource.volume = loopVolume;
        loopSource.loop = true;
        loopSource.Play();
    }

    private void StopLoopWithFade()
    {
        if (loopSource == null) return;
        if (!loopSource.isPlaying) return;

        if (barrageLoopFadeOutTime <= 0f)
        {
            StopLoopImmediate_Internal();
            return;
        }

        if (_loopFadeCo != null)
            StopCoroutine(_loopFadeCo);

        _loopFadeCo = StartCoroutine(CoFadeOutLoop());
    }

    private IEnumerator CoFadeOutLoop()
    {
        float startVolume = loopSource.volume;
        float t = 0f;

        while (t < barrageLoopFadeOutTime)
        {
            t += Time.deltaTime;
            float ratio = 1f - Mathf.Clamp01(t / barrageLoopFadeOutTime);
            loopSource.volume = startVolume * ratio;
            yield return null;
        }

        StopLoopImmediate_Internal();

        // 다음 재생 대비 원복
        loopSource.volume = loopVolume;

        _loopFadeCo = null;
    }

    private void StopLoopImmediate_Internal()
    {
        if (loopSource == null) return;

        loopSource.Stop();
        loopSource.clip = null;
    }

    private void OnDisable()
    {
        // ✅ 강제 종료 상황에서는 코루틴이 멈출 수 있어서 "즉시 Stop"이 안전합니다.
        if (_loopFadeCo != null)
        {
            StopCoroutine(_loopFadeCo);
            _loopFadeCo = null;
        }
        StopLoopImmediate_Internal();
    }

    private void OnDestroy()
    {
        // ✅ 파괴 시에도 즉시 정리
        if (_loopFadeCo != null)
        {
            StopCoroutine(_loopFadeCo);
            _loopFadeCo = null;
        }
        StopLoopImmediate_Internal();
    }
}