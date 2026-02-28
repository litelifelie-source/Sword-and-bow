using System;
using UnityEngine;

[DisallowMultipleComponent]
public class RectTransformMoveTracer : MonoBehaviour
{
    public bool logStack = true;
    public float minDelta = 0.001f;

    RectTransform _rt;
    Vector3 _prevPos;
    Vector3 _prevLocal;
    Vector3 _prevAnchored;

    void Awake()
    {
        _rt = GetComponent<RectTransform>();
        _prevPos = transform.position;
        _prevLocal = transform.localPosition;
        _prevAnchored = _rt ? (Vector3)_rt.anchoredPosition3D : Vector3.zero;
    }

    void Update() => Check("U");
    void LateUpdate() => Check("L");

    void Check(string phase)
    {
        var pos = transform.position;
        var local = transform.localPosition;
        var anchored = _rt ? (Vector3)_rt.anchoredPosition3D : Vector3.zero;

        bool moved =
            (pos - _prevPos).sqrMagnitude > minDelta * minDelta ||
            (local - _prevLocal).sqrMagnitude > minDelta * minDelta ||
            (_rt && (anchored - _prevAnchored).sqrMagnitude > minDelta * minDelta);

        if (!moved) return;

        Debug.Log($"[UI-MOVE:{phase}] {name}\n" +
                  $"  pos   {_prevPos} -> {pos}\n" +
                  $"  local {_prevLocal} -> {local}\n" +
                  (_rt ? $"  anch  {_prevAnchored} -> {anchored}\n" : ""), this);

        if (logStack)
            Debug.Log($"[UI-MOVE:{phase}][STACK]\n{Environment.StackTrace}", this);

        _prevPos = pos;
        _prevLocal = local;
        _prevAnchored = anchored;
    }
}