using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Move")]
    public float speed = 3.5f;
    public float sensitivity = 2f;
    public bool canMove = true;

    [Header("Refs")]
    public Rigidbody rb;
    public Animator animator;
    public ParticleSystem ps;

    [Header("HP")]
    public int maxHP = 20;
    public int HP = 20;
    public bool isDead;

    [Header("Hit Stun")]
    public float hitStunSeconds = 0.25f;
    private float hitStunEndTime = -999f;
    private bool hitStunActive = false;
    private RigidbodyConstraints cachedConstraints;

    [Header("Combat")]
    public LayerMask enemyLayer;
    public Transform attackOrigin;
    public float attackRadius = 1.0f;

    public float normalAttackRange = 2.0f;
    public int normalDamage = 2;

    public float heavyAttackRange = 3.5f;
    public int heavyDamage = 4;

    [Header("Attack Buff")]
    public int attackBonus = 0;

    [Header("Animator Params")]
    public string attackTrigger = "Attack";
    public string heavyAttackTrigger = "HeavyAttack";
    public string chargingBool = "Charging";
    public string chargedBool = "Charged";
    public string releaseHeavyTrigger = "ReleaseHeavy";
    public string cancelHeavyTrigger = "CancelHeavy";

    public string hitTrigger = "Hit";
    public string deadBool = "Dead";
    public string moveXParam = "MoveX";
    public string moveYParam = "MoveY";
    public string moveStateParam = "MoveState";

    [Header("Right Click Charge")]
    public float chargeTime = 0.4f;

    // =========================
    // Backstep / I-Frames
    // =========================
    [Header("Backstep (Ctrl)")]
    public KeyCode backstepKey = KeyCode.LeftControl;
    public float backstepDistance = 2.5f;      // 固定后撤距离
    public float backstepDuration = 0.15f;     // 后撤位移时间
    public float backstepCooldown = 0.45f;     // 后撤CD
    public float invincibleDuration = 0.30f;   // i-frame 时长
    public string backstepTrigger = "Backstep";// Animator Trigger
    public string locomotionStateName = "Locomotion"; // 你Animator里空闲状态名字就是 Locomotion

    private bool isBackstepping = false;
    private bool invincible = false;
    private float nextBackstepTime = -999f;

    // runtime
    private float chargeTimer = 0f;
    private bool holdingRight = false;
    private bool chargedLogic = false;
    private bool normalHitArmed = false;

    // input cache
    private float inputH;
    private float inputV;
    private float mouseX;

    void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (ps == null) ps = GetComponentInChildren<ParticleSystem>(true);
        if (attackOrigin == null) attackOrigin = transform;

        HP = Mathf.Clamp(HP, 0, maxHP);

        if (rb != null)
        {
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }
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

        UpdateHitStunLifecycle();

        // ✅ 魂游规则后撤：只允许在“空闲Locomotion”时
        TryBackstep();

        bool lockByAnim = IsAnimatorInHeavyStates() || (animator != null && animator.GetBool(chargingBool));
        bool lockByHit = Time.time < hitStunEndTime;
        bool lockByBackstep = isBackstepping;

        bool allowControlNow = canMove && !lockByAnim && !lockByHit && !hitStunActive && !lockByBackstep;

        if (allowControlNow)
        {
            inputH = Input.GetAxis("Horizontal");
            inputV = Input.GetAxis("Vertical");
            mouseX = Input.GetAxis("Mouse X") * sensitivity;
        }
        else
        {
            inputH = 0f;
            inputV = 0f;
            mouseX = 0f;

            if (animator != null)
            {
                animator.SetFloat(moveXParam, 0f);
                animator.SetFloat(moveYParam, 0f);
            }
        }

        // 后撤期间不允许攻击/蓄力
        if (!isBackstepping)
        {
            Attack();
            HeavyAttack();
        }
    }

    void FixedUpdate()
    {
        if (isDead) return;
        if (rb == null) return;

        bool lockByAnim = IsAnimatorInHeavyStates() || (animator != null && animator.GetBool(chargingBool));
        bool lockByHit = Time.time < hitStunEndTime;
        bool lockByBackstep = isBackstepping;

        bool allowControlNow = canMove && !lockByAnim && !lockByHit && !hitStunActive && !lockByBackstep;
        if (!allowControlNow) return;

        MovePlayer_RB(inputH, inputV);
        RotatePlayer_RB(mouseX);
    }

    private void MovePlayer_RB(float horizontal, float vertical)
    {
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

        Vector3 localMove = new Vector3(horizontal, 0, vertical);
        Vector3 worldMove = transform.TransformDirection(localMove).normalized * speed * Time.fixedDeltaTime;

        rb.MovePosition(rb.position + worldMove);
    }

    private void RotatePlayer_RB(float mouseXDelta)
    {
        if (Mathf.Abs(mouseXDelta) < 0.0001f) return;

        Quaternion rot = Quaternion.Euler(0f, mouseXDelta, 0f);
        rb.MoveRotation(rb.rotation * rot);
    }

    // =========================
    // Backstep: 只能在 Locomotion（未攻击未受击未蓄力）时用
    // =========================
    private void TryBackstep()
    {
        bool pressed = Input.GetKeyDown(backstepKey) || Input.GetKeyDown(KeyCode.RightControl);
        if (!pressed) return;

        if (Time.time < nextBackstepTime) return;
        if (isBackstepping) return;
        if (isDead) return;

        // 受击/硬直中不允许
        if (hitStunActive) return;
        if (Time.time < hitStunEndTime) return;

        // 蓄力/重击中不允许
        if (holdingRight || IsAnimatorInHeavyStates()) return;
        if (animator != null && animator.GetBool(chargingBool)) return;

        // ✅ 必须在 Locomotion（空闲）才能后撤
        if (!IsInState(locomotionStateName)) return;

        StartCoroutine(CoBackstep());
    }

    private IEnumerator CoBackstep()
    {
        isBackstepping = true;
        nextBackstepTime = Time.time + backstepCooldown;

        // 开无敌
        invincible = true;
        float invEnd = Time.time + invincibleDuration;

        // 清速度，避免惯性
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // 播放后撤动画 Trigger
        if (animator != null && !string.IsNullOrEmpty(backstepTrigger))
        {
            animator.ResetTrigger(backstepTrigger);
            animator.SetTrigger(backstepTrigger);
        }

        // 后撤固定距离、短时间完成（更像“后退加快”）
        Vector3 start = rb.position;
        Vector3 dirBack = -transform.forward;
        dirBack.y = 0f;
        if (dirBack.sqrMagnitude < 0.0001f) dirBack = -transform.forward;
        dirBack.Normalize();

        Vector3 targetPos = start + dirBack * backstepDistance;

        // 后撤期间禁用常规输入移动/旋转
        bool oldCanMove = canMove;
        canMove = false;

        float t = 0f;
        while (t < backstepDuration)
        {
            if (isDead) break;

            t += Time.fixedDeltaTime;
            float alpha = Mathf.Clamp01(t / Mathf.Max(0.0001f, backstepDuration));

            // EaseOut：一开始快（魂游感觉）
            float eased = 1f - Mathf.Pow(1f - alpha, 3f);

            rb.MovePosition(Vector3.Lerp(start, targetPos, eased));

            yield return new WaitForFixedUpdate();
        }

        canMove = oldCanMove;
        isBackstepping = false;

        // 无敌可能还没结束
        while (Time.time < invEnd)
            yield return null;

        invincible = false;
    }

    private bool IsInState(string stateName)
    {
        if (animator == null) return true;
        var cur = animator.GetCurrentAnimatorStateInfo(0);
        // 只看当前层0
        return cur.IsName(stateName);
    }

    // =========================
    // 强制受击表现（Boss 二阶段用）
    // =========================
    public void ForceHitReaction(float lockSeconds = 0.25f)
    {
        if (isDead) return;

        // 后撤中不打断（魂游一般允许翻滚期间无敌）
        if (isBackstepping) return;

        normalHitArmed = false;
        if (holdingRight || IsAnimatorInHeavyStates())
            CancelCharge();

        EnterHitStun(lockSeconds);

        if (animator != null) animator.SetTrigger(hitTrigger);
        if (ps != null) ps.Play();
    }

    // =========================
    // Attacks
    // =========================
    public void Attack()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        if (holdingRight || IsAnimatorInHeavyStates())
            CancelCharge();

        normalHitArmed = true;

        if (animator != null)
        {
            animator.ResetTrigger(attackTrigger);
            animator.SetTrigger(attackTrigger);
        }
    }

    public void HeavyAttack()
    {
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

        if (holdingRight && Input.GetMouseButtonUp(1))
        {
            holdingRight = false;

            if (!chargedLogic)
            {
                CancelCharge();
                return;
            }

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

    public void AnimEvent_NormalHit()
    {
        if (isDead) return;
        if (!normalHitArmed) return;

        DealDamageInFront(normalAttackRange, normalDamage + attackBonus);
        normalHitArmed = false;
    }

    public void AnimEvent_HeavyHit()
    {
        if (isDead) return;

        DealDamageInFront(heavyAttackRange, heavyDamage + attackBonus);

        if (animator != null)
        {
            animator.SetBool(chargedBool, false);
            animator.SetBool(chargingBool, false);
        }
    }

    private void DealDamageInFront(float range, int damage)
    {
        Vector3 center = attackOrigin.position + transform.forward * (range * 0.6f);

        Collider[] hits = Physics.OverlapSphere(center, attackRadius, enemyLayer, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return;

        hits[0].transform.root.gameObject.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
    }

    public void AddAttack(int amount)
    {
        if (isDead) return;
        if (amount <= 0) return;

        attackBonus += amount;
    }

    // =========================
    // TakeDamage with i-frames
    // =========================
    public void TakeDamage(int attackValue)
    {
        if (isDead) return;

        // ✅ 后撤无敌期间：完全不受伤
        if (invincible) return;

        // 受击打断
        normalHitArmed = false;
        if (holdingRight || IsAnimatorInHeavyStates())
            CancelCharge();

        EnterHitStun(hitStunSeconds);

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

    public bool Heal(int amount)
    {
        if (isDead) return false;
        if (amount <= 0) return false;
        if (HP >= maxHP) return false;

        HP += amount;
        HP = Mathf.Clamp(HP, 0, maxHP);

        if (ps != null) ps.Play();
        return true;
    }

    // =========================
    // Hit Stun Core
    // =========================
    private void EnterHitStun(float seconds)
    {
        if (seconds <= 0f) return;

        // 后撤期间不吃硬直（魂游 i-frame）
        if (isBackstepping) return;

        hitStunEndTime = Mathf.Max(hitStunEndTime, Time.time + seconds);

        if (rb == null) return;

        if (!hitStunActive)
        {
            hitStunActive = true;
            cachedConstraints = rb.constraints;
        }

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.constraints = RigidbodyConstraints.FreezeAll;
    }

    private void UpdateHitStunLifecycle()
    {
        if (!hitStunActive) return;

        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (Time.time >= hitStunEndTime)
        {
            if (rb != null)
                rb.constraints = cachedConstraints;

            hitStunActive = false;
        }
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