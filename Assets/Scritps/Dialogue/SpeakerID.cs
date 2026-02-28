using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class SpeakerIdTag : MonoBehaviour
{
    [FormerlySerializedAs("id")]
    [Tooltip("퀘스트 노드의 speakerId와 1:1 매칭되는 고정 키 (영문 권장)")]
    public string speakerId;

    [Tooltip("말풍선/대사 UI가 따라갈 앵커. 비워두면 자기 Transform을 사용합니다.")]
    public Transform followAnchor;

    public Transform FollowAnchor => followAnchor ? followAnchor : transform;

    // =====================================================
    // ✅ NEW: Runtime Team/Layer state exposure
    // =====================================================

    [Header("Optional - Team Source")]
    [Tooltip("비우면 부모에서 UnitTeam을 자동 탐색합니다. (권장)")]
    [SerializeField] private UnitTeam unitTeam;

    [Header("Optional - Layer Fallback")]
    [Tooltip("UnitTeam이 없을 때만 사용합니다. 이 마스크에 포함되면 Ally로 간주합니다.")]
    public LayerMask allyLayerMaskFallback;

    public UnitTeam UnitTeam
    {
        get
        {
            if (unitTeam == null) unitTeam = GetComponentInParent<UnitTeam>();
            return unitTeam;
        }
    }

    public bool IsAlly
    {
        get
        {
            var ut = UnitTeam;
            if (ut != null) return ut.IsAlly;

            // fallback: FollowAnchor의 레이어로 판정
            int layer = FollowAnchor.gameObject.layer;
            return (allyLayerMaskFallback.value & (1 << layer)) != 0;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!string.IsNullOrEmpty(speakerId))
            speakerId = speakerId.Trim();
    }
#endif
}

public class SpeakerId : SpeakerIdTag { }