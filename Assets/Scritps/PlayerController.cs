using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float speed = 5f;
    private Animator animator;
    private SpriteRenderer spriteRenderer;

    private FormationManager formationManager;

    void Start()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        formationManager = GetComponent<FormationManager>();
    }

    void Update()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 move = new Vector3(h, v, 0).normalized;
        transform.position += move * speed * Time.deltaTime;

        // ✅ 진형 방향 갱신
        if (formationManager != null && move.sqrMagnitude > 0.01f)
        {
            formationManager.SetFacing(move);
        }

        float moveSpeed = move.magnitude;

        if (animator)
        {
            animator.SetBool("Player_Walk_UP", v > 0f);
            animator.SetBool("Idle", v < 0f);
            animator.SetBool("Player_Walk_Right", h > 0f);
            animator.SetBool("Player_Walk_Left", h < 0f);
        }
    }
}
