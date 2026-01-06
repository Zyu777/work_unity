using UnityEngine;

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
    public int maxHP = 20;         // ✅ 最大血量
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

    public string heavyAttackTrigger = "HeavyAttack";     // RightDown: Locomotion -> Attack_Start
    public string chargingBool = "Charging";              // Hold: true
    public string chargedBool = "Charged";                // Animator 蓄满标记
    public string releaseHeavyTrigger = "ReleaseHeavy";   // RightUp(蓄满): Attack_Hold -> HeavyAttack(释放)
    public string cancelHeavyTrigger = "CancelHeavy";     // RightUp(未蓄满)/打断：回Locomotion

    public string hitTrigger = "Hit";
    public string deadBool = "Dead";
    public string moveXParam = "MoveX";
    public string moveYParam = "MoveY";
    public string moveStateParam = "MoveState";

    [Header("Right Click Charge")]
    public float chargeTime = 0.4f;

    // 运行时状态
    private float chargeTimer = 0f;
    private bool holdingRight = false;
    private bool chargedLogic = false; // 仅用于判断“是否蓄满”
    private bool normalHitArmed = false;

    void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (ps == null) ps = GetComponentInChildren<ParticleSystem>(true);
        if (attackOrigin == null) attackOrigin = transform;

        // ✅ 保底修正
        HP = Mathf.Clamp(HP, 0, maxHP);
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;

        if (ps != null)
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

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

    // =========================
    // 普攻：只触发动画，命中由动画事件结算
    // =========================
    public void Attack()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        // 正在蓄力/重击则取消
        if (holdingRight || IsAnimatorInHeavyStates())
            CancelCharge();

        normalHitArmed = true;

        if (animator != null)
        {
            animator.ResetTrigger(attackTrigger);
            animator.SetTrigger(attackTrigger);
        }
    }

    // =========================
    // 右键蓄力：起手/蓄力不造成伤害，释放动作命中帧造成伤害
    // =========================
    public void HeavyAttack()
    {
        // Down：起手
        if (Input.GetMouseButtonDown(1))
        {
            holdingRight = true;
            chargeTimer = 0f;
            chargedLogic = false;

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

            if (!chargedLogic && chargeTimer >= chargeTime)
            {
                chargedLogic = true;

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

            if (!chargedLogic)
            {
                CancelCharge();
                return;
            }

            // ✅ 释放：进入 HeavyAttack（释放动作）
            if (animator != null)
            {
                animator.SetBool(chargingBool, false);
                animator.SetBool(chargedBool, true);

                animator.ResetTrigger(cancelHeavyTrigger);
                animator.ResetTrigger(releaseHeavyTrigger);
                animator.SetTrigger(releaseHeavyTrigger);
            }

            chargeTimer = 0f;
            chargedLogic = false;
        }
    }

    // =========================
    // Animation Event：普攻命中帧（加在 Attack1/Attack2 的命中帧）
    // =========================
    public void AnimEvent_NormalHit()
    {
        if (isDead) return;
        if (!normalHitArmed) return;

        DealDamageInFront(normalAttackRange, normalDamage);
        normalHitArmed = false;
    }

    // =========================
    // Animation Event：重击命中帧（加在 HeavyAttack 释放动作的命中帧）
    // =========================
    public void AnimEvent_HeavyHit()
    {
        if (isDead) return;

        // ✅ 无条件结算（你现在确认这条链路是通的）
        DealDamageInFront(heavyAttackRange, heavyDamage);

        // 清 Animator 蓄力标记，避免卡条件
        if (animator != null)
        {
            animator.SetBool(chargedBool, false);
            animator.SetBool(chargingBool, false);
        }
    }

    // =========================
    // Damage
    // =========================
    private void DealDamageInFront(float range, int damage)
    {
        Vector3 center = attackOrigin.position + transform.forward * (range * 0.6f);

        Collider[] hits = Physics.OverlapSphere(center, attackRadius, enemyLayer, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return;

        // 你原来的方式：SendMessage
        hits[0].transform.root.gameObject.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
    }

    // =========================
    // HP: 受击 / 回血
    // =========================
    public void TakeDamage(int attackValue)
    {
        if (isDead) return;

        // 受击时打断蓄力
        normalHitArmed = false;
        if (holdingRight || IsAnimatorInHeavyStates())
            CancelCharge();

        HP -= attackValue;
        HP = Mathf.Clamp(HP, 0, maxHP);

        if (animator != null) animator.SetTrigger(hitTrigger);
        if (ps != null) ps.Play();

        if (HP <= 0)
        {
            isDead = true;
            if (animator != null) animator.SetBool(deadBool, true);
            ResetChargeState();
        }
    }

    // ✅ 血包调用：回血 5，满血不消耗（返回 false）
    public bool Heal(int amount)
    {
        if (isDead) return false;
        if (amount <= 0) return false;
        if (HP >= maxHP) return false;

        HP += amount;
        HP = Mathf.Clamp(HP, 0, maxHP);

        // 可选：回血播放粒子
        if (ps != null) ps.Play();
        return true;
    }

    // =========================
    // Cancel / Reset
    // =========================
    private void CancelCharge()
    {
        holdingRight = false;
        chargedLogic = false;
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
        holdingRight = false;
        chargedLogic = false;
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

    void OnDrawGizmosSelected()
    {
        if (attackOrigin == null) return;
        Gizmos.DrawWireSphere(attackOrigin.position + transform.forward * (normalAttackRange * 0.6f), attackRadius);
    }
}
