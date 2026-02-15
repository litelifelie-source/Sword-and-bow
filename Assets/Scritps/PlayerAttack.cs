using UnityEngine;

public class PlayerAttack : MonoBehaviour
{
    public GameObject attackEffectPrefab;
    public float attackOffset = 2f;

    private Vector2 lastDirection = Vector2.down; // 기본 방향

    void Update()
    {
        // 이동 입력 감지
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector2 moveDir = new Vector2(h, v);

        // 이동 중이면 마지막 방향 저장
        if (moveDir != Vector2.zero)
        {
            lastDirection = moveDir.normalized;
        }

        // 스페이스 공격
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Attack();
        }
    }

void Attack()
{
    Vector3 spawnPos = transform.position + (Vector3)lastDirection * attackOffset;

    GameObject effect = Instantiate(
        attackEffectPrefab,
        spawnPos,
        Quaternion.identity
    );

    Destroy(effect, 0.3f); // 0.3초 후 자동 삭제
    }
}
