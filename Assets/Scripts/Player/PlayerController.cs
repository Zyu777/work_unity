using UnityEngine;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    [Header("Move")]
    public float speed = 3.5f;
    public float sensitivity = 2f;
    public bool canMove = true;

    [Header("Refs")]
    public Animator animator;
    public ParticleSystem ps;

    [Header("HP")]
    public int HP = 20;
    public bool isDead;

    [Header("Combat")]
    public LayerMask enemyLayer;
    public Transform attackOrigin;
    public float attackRadius = 1.0f;
    public float normalAttackRange = 2.0f;
    public int normalDamage = 2;
    public float heavyAttackRange = 3.5f;
    public int heavyDamage = 4;

    [Header("Animator Params")]
    public string attackTrigger = "Attack";

    public string heavyAttackTrigger = "HeavyAttack";     // Down: Locomotion -> Attack_Start
    public string chargingBool = "Charging";              // Hold: true
    public string chargedBool = "Charged";                // 蓄满标记（建议不要当硬过渡条件）
    public string releaseHeavyTrigger = "ReleaseHeavy";   // Up(蓄满): Attack_Hold -> HeavyAttack
    public string cancelHeavyTrigger = "CancelHeavy";     // Up(未蓄满)/打断：回Locomotion

    public string hitTrigger = "Hit";
    public string deadBool = "Dead";
    public string moveXParam = "MoveX";
    public string moveYParam = "MoveY";
    public string moveStateParam = "MoveState";

    [Header("Right Click Charge")]
    public float chargeTime = 0.4f;
    private float chargeTimer = 0f;
    private bool holdingRight = false;
    private bool charged = false;

    private Coroutine clearChargedCo;
    private Coroutine safetyCo;

    void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (ps == null) ps = GetComponentInChildren<ParticleSystem>(true);
        if (attackOrigin == null) attackOrigin = transform;
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;

        if (ps != null)
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (!canMove) canMove = true;

        ResetChargeState();
    }

    void Update()
    {
        if (isDead) return;

        bool lockByAnim = IsAnimatorInHeavyStates() || (animator != null && animator.GetBool(chargingBool));
        bool allowMoveNow = canMove && !lockByAnim;

        if (allowMoveNow)
        {
            Moveplayer();
            RotatePlayer();
        }
        else
        {
            if (animator != null)
            {
                animator.SetFloat(moveXParam, 0f);
                animator.SetFloat(moveYParam, 0f);
            }
        }

        Attack();
        HeavyAttack();
    }

    private void Moveplayer()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        if (animator != null)
        {
            animator.SetFloat(moveXParam, horizontal);
            animator.SetFloat(moveYParam, vertical);
        }

        if (Input.GetKey(KeyCode.LeftShift))
        {
            if (animator != null) animator.SetFloat(moveStateParam, 0);
            speed = 1.5f;
        }
        else
        {
            if (animator != null) animator.SetFloat(moveStateParam, 1);
            speed = 3.5f;
        }

        Vector3 movement = new Vector3(horizontal, 0, vertical) * Time.deltaTime * speed;
        transform.Translate(movement, Space.Self);
    }

    private void RotatePlayer()
    {
        float mouseX = Input.GetAxis("Mouse X") * sensitivity;
        transform.Rotate(Vector3.up * mouseX);
    }

    public void Attack()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        if (holdingRight || IsAnimatorInHeavyStates())
            CancelCharge();

        if (animator != null)
        {
            animator.ResetTrigger(attackTrigger);
            animator.SetTrigger(attackTrigger);
        }

        DealDamageInFront(normalAttackRange, normalDamage);
    }

    public void HeavyAttack()
    {
        // Down：起手
        if (Input.GetMouseButtonDown(1))
        {
            holdingRight = true;
            chargeTimer = 0f;
            charged = false;

            StopSafetyCoroutines();

            if (animator != null)
            {
                animator.SetBool(chargingBool, false);
                animator.SetBool(chargedBool, false);

                animator.ResetTrigger(releaseHeavyTrigger);
                animator.ResetTrigger(cancelHeavyTrigger);

                animator.ResetTrigger(heavyAttackTrigger);
                animator.SetTrigger(heavyAttackTrigger);
            }
        }

        // Hold：达到阈值进入蓄力
        if (holdingRight && Input.GetMouseButton(1))
        {
            chargeTimer += Time.deltaTime;

            if (!charged && chargeTimer >= chargeTime)
            {
                charged = true;

                if (animator != null)
                {
                    animator.SetBool(chargingBool, true);
                    animator.SetBool(chargedBool, true);
                }
            }
        }

        // Up：未蓄满取消；蓄满释放
        if (holdingRight && Input.GetMouseButtonUp(1))
        {
            holdingRight = false;

            // 未蓄满：取消回Locomotion
            if (!charged)
            {
                CancelCharge();
                return;
            }

            // 蓄满：释放重击
            if (animator != null)
            {
                // 关键：不要同帧把 Charged 清掉，让过渡稳定吃到
                animator.SetBool(chargingBool, false);
                animator.SetBool(chargedBool, true);

                animator.ResetTrigger(cancelHeavyTrigger);
                animator.ResetTrigger(releaseHeavyTrigger);
                animator.SetTrigger(releaseHeavyTrigger);
            }

            // 伤害结算（更建议用动画事件命中帧结算）
            DealDamageInFront(heavyAttackRange, heavyDamage);

            chargeTimer = 0f;
            charged = false;

            // ✅延迟清理 Charged（解决你“松开卡Hold/再点没反应”）
            clearChargedCo = StartCoroutine(ClearChargedAfterDelay(0.15f));

            // ✅保险：如果 0.25s 后还在 Hold，就强制 CancelHeavy 回去（防止任何配置问题导致卡死）
            safetyCo = StartCoroutine(SafetyExitIfStuck(0.25f));
        }
    }

    private IEnumerator ClearChargedAfterDelay(float t)
    {
        yield return new WaitForSeconds(t);
        if (animator != null) animator.SetBool(chargedBool, false);
        clearChargedCo = null;
    }

    private IEnumerator SafetyExitIfStuck(float t)
    {
        yield return new WaitForSeconds(t);

        // 如果仍然在 Attack_Hold，就说明 ReleaseHeavy 没被吃到，强制回 Locomotion
        if (animator != null)
        {
            var s = animator.GetCurrentAnimatorStateInfo(0);
            if (s.IsName("Attack_Hold"))
            {
                animator.SetBool(chargingBool, false);
                animator.SetBool(chargedBool, false);
                animator.ResetTrigger(cancelHeavyTrigger);
                animator.SetTrigger(cancelHeavyTrigger);
            }
        }

        safetyCo = null;
    }

    private void StopSafetyCoroutines()
    {
        if (clearChargedCo != null) { StopCoroutine(clearChargedCo); clearChargedCo = null; }
        if (safetyCo != null) { StopCoroutine(safetyCo); safetyCo = null; }
    }

    private void CancelCharge()
    {
        StopSafetyCoroutines();

        holdingRight = false;
        charged = false;
        chargeTimer = 0f;

        if (animator != null)
        {
            animator.SetBool(chargingBool, false);
            animator.SetBool(chargedBool, false);

            animator.ResetTrigger(releaseHeavyTrigger);
            animator.ResetTrigger(cancelHeavyTrigger);
            animator.SetTrigger(cancelHeavyTrigger);
        }
    }

    private void ResetChargeState()
    {
        StopSafetyCoroutines();

        holdingRight = false;
        charged = false;
        chargeTimer = 0f;

        if (animator != null)
        {
            animator.SetBool(chargingBool, false);
            animator.SetBool(chargedBool, false);
            animator.ResetTrigger(releaseHeavyTrigger);
            animator.ResetTrigger(cancelHeavyTrigger);
        }
    }

    private bool IsAnimatorInHeavyStates()
    {
        if (animator == null) return false;

        var cur = animator.GetCurrentAnimatorStateInfo(0);
        var next = animator.GetNextAnimatorStateInfo(0);

        bool curIn = cur.IsName("Attack_Start") || cur.IsName("Attack_Hold") || cur.IsName("HeavyAttack");
        bool nextIn = next.IsName("Attack_Start") || next.IsName("Attack_Hold") || next.IsName("HeavyAttack");
        return curIn || nextIn;
    }

    private void DealDamageInFront(float range, int damage)
    {
        Vector3 center = attackOrigin.position + transform.forward * (range * 0.6f);

        Collider[] hits = Physics.OverlapSphere(center, attackRadius, enemyLayer, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return;

        Transform root = hits[0].transform.root;
        root.gameObject.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
    }

    public void TakeDamage(int attackValue)
    {
        if (isDead) return;

        if (holdingRight || IsAnimatorInHeavyStates())
            CancelCharge();

        HP -= attackValue;

        if (animator != null) animator.SetTrigger(hitTrigger);
        if (ps != null) ps.Play();

        if (HP <= 0)
        {
            isDead = true;
            if (animator != null) animator.SetBool(deadBool, true);
            ResetChargeState();
        }
    }

    void OnDrawGizmosSelected()
    {
        if (attackOrigin == null) return;
        Gizmos.DrawWireSphere(attackOrigin.position + transform.forward * (normalAttackRange * 0.6f), attackRadius);
    }
}
