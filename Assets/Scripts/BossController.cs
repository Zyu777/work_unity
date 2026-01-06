using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class BossController : MonoBehaviour
{
    [Header("Refs")]
    public NavMeshAgent agent;
    public Animator animator;
    public PlayerController target;

    [Header("HP")]
    public int maxHP = 80;
    public int HP = 80;
    public bool isDead = false;

    [Header("Ranges")]
    public float chaseRange = 15f;
    public float attackRange = 2.2f;

    [Header("Attack")]
    public float attackCD = 2.5f;
    private float lastAttackTime = -999f;
    public int damageToPlayer = 3;

    [Header("Hit Check (Animation Event Only)")]
    public Transform attackOrigin;             // 命中检测中心（空就用 transform）
    public float hitRadius = 0.7f;             // 命中球半径
    public float hitForwardOffset = 1.0f;      // 命中球向前偏移
    public LayerMask playerLayer;              // 只检测 Player Layer
    public bool requireFacing = true;          // 需要面向玩家
    [Range(0f, 180f)] public float facingAngle = 80f;

    // ✅ 本次攻击是否允许结算（建议由动画事件 AttackStart/End 控制）
    private bool attackArmed = false;

    [Header("Animator Params")]
    public string attackTrigger = "Attack";
    public string hitTrigger = "Hit";
    public string deadBool = "Dead";
    public string moveYParam = "MoveY";
    public string moveStateParam = "MoveState";

    [Header("Drop (Health Pack)")]
    public GameObject healthPackPrefab;
    [Range(0f, 1f)] public float dropChance = 0.5f;
    public Vector3 dropOffset = new Vector3(0, 0.2f, 0);
    private bool dropped = false;

    // =========================
    // Phase 2
    // =========================
    [Header("Boss Phase 2")]
    [Range(0.1f, 0.9f)] public float phase2HpPercent = 0.5f; // 50%
    public bool phase2 = false;

    [Header("Phase 2 Buff")]
    public int phase2AddDamage = 2;
    public float phase2AttackCdMul = 0.85f;

    [Header("Phase 2 Visual (Material Swap)")]
    public Material phase2Material; // Enemy_Body_Boss_Phase2_
    public SkinnedMeshRenderer bodyRenderer;
    public SkinnedMeshRenderer headRenderer;
    public SkinnedMeshRenderer upperRenderer;
    public SkinnedMeshRenderer hatRenderer;         // 可选
    public SkinnedMeshRenderer accessoriesRenderer; // 可选

    [Header("Phase 2 Flash (Red Flash -> Phase2 Material)")]
    public bool flashBeforePhase2 = true;
    public Color flashColor = Color.red;
    public float flashDuration = 0.2f;
    private bool phase2CoroutineRunning = false;

    [Header("Phase 2 Impact (Screen Shake + Force Player Hit)")]
    public CameraShake cameraShake;
    public float shakeDuration = 0.35f;
    public float shakeStrength = 0.25f;

    public bool forcePlayerHitOnPhase2 = true; // 强制玩家受击表现（不扣血）
    public bool lockPlayerMoveBriefly = true;
    public float lockMoveSeconds = 0.25f;

    void Awake()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (attackOrigin == null) attackOrigin = transform;

        if (target == null) target = FindObjectOfType<PlayerController>();

        if (cameraShake == null)
        {
            var cam = Camera.main;
            if (cam != null) cameraShake = cam.GetComponent<CameraShake>();
        }

        AutoFixPlayerLayerMaskIfNeeded();
    }

    void Start()
    {
        if (maxHP <= 0) maxHP = HP;
        HP = Mathf.Clamp(HP, 0, maxHP);

        if (agent != null)
        {
            agent.stoppingDistance = attackRange;
            agent.isStopped = false;

            // 你如果需要 boss 自动转向，可以保留 agent.updateRotation=true
            // 若你想自己 FaceTarget，建议关掉避免抢转向
            agent.updateRotation = false;
        }
    }

    void Update()
    {
        if (isDead)
        {
            StopMove();
            UpdateMoveAnim();
            return;
        }

        if (target == null || target.isDead)
        {
            StopMove();
            UpdateMoveAnim();
            return;
        }

        if (agent != null && agent.enabled && !agent.isOnNavMesh)
        {
            StopMove();
            UpdateMoveAnim();
            return;
        }

        // ✅ 二阶段检查
        CheckPhase2();

        float dis = Vector3.Distance(target.transform.position, transform.position);

        // =========================
        // 攻击范围内：只触发攻击动画
        // 命中结算交给动画事件：
        //   AnimEvent_AttackStart / AnimEvent_AttackHit / AnimEvent_AttackEnd
        // =========================
        if (dis <= attackRange)
        {
            StopMove();
            FaceTarget(target.transform.position);

            if (Time.time - lastAttackTime >= attackCD)
            {
                if (animator != null) animator.SetTrigger(attackTrigger);
                lastAttackTime = Time.time;

                // ⚠️ 不建议在这里直接 attackArmed=true（容易与动画不同步）
                // attackArmed = true; // 改为由 AnimEvent_AttackStart 开启
            }

            UpdateMoveAnim();
            return;
        }

        // =========================
        // 追击
        // =========================
        if (dis <= chaseRange)
        {
            if (agent != null && agent.enabled)
            {
                agent.isStopped = false;

                if (NavMesh.SamplePosition(target.transform.position, out var hit, 2f, NavMesh.AllAreas))
                    agent.SetDestination(hit.position);
                else
                    agent.SetDestination(target.transform.position);
            }

            FaceTarget(target.transform.position);
            UpdateMoveAnim();
            return;
        }

        StopMove();
        UpdateMoveAnim();
    }

    // =========================================================
    // ✅ Animation Events（你现在动画里加了 AttackStart，所以 Boss 必须有）
    // =========================================================

    // ✅ 攻击开始帧（抬手那一帧）
    public void AnimEvent_AttackStart()
    {
        if (isDead) return;
        attackArmed = true;
    }

    // ✅ 攻击命中帧（挥到那一下）
    public void AnimEvent_AttackHit()
    {
        if (isDead) return;
        if (!attackArmed) return;
        if (target == null || target.isDead) return;

        // 命中后立刻关闭，防止同一次攻击多次结算
        attackArmed = false;

        // 面向判定（可选）
        if (requireFacing)
        {
            Vector3 toPlayer = target.transform.position - transform.position;
            toPlayer.y = 0f;

            if (toPlayer.sqrMagnitude > 0.0001f)
            {
                float ang = Vector3.Angle(transform.forward, toPlayer.normalized);
                if (ang > facingAngle) return;
            }
        }

        AutoFixPlayerLayerMaskIfNeeded();

        Vector3 originPos = (attackOrigin != null) ? attackOrigin.position : transform.position;
        Vector3 center = originPos + transform.forward * hitForwardOffset;

        Collider[] cols = Physics.OverlapSphere(center, hitRadius, playerLayer, QueryTriggerInteraction.Ignore);
        if (cols == null || cols.Length == 0) return;

        // 保险：只对玩家扣血（避免场景里其它同 layer 物体误触）
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] == null) continue;
            PlayerController pc = cols[i].GetComponentInParent<PlayerController>();
            if (pc != null && pc == target)
            {
                target.TakeDamage(damageToPlayer);
                return;
            }
        }
    }

    // ✅ 攻击结束帧（可选）
    public void AnimEvent_AttackEnd()
    {
        attackArmed = false;
    }

    // ✅ 兼容旧事件名（如果动画里还有 NormalHit）
    public void AnimEvent_NormalHit()
    {
        AnimEvent_AttackHit();
    }

    // =========================================================
    // Take Damage / Phase2
    // =========================================================
    public void TakeDamage(int damage)
    {
        if (isDead) return;

        HP -= damage;
        HP = Mathf.Clamp(HP, 0, maxHP);

        if (animator != null) animator.SetTrigger(hitTrigger);

        CheckPhase2();

        if (HP <= 0)
        {
            Die();
        }
    }

    private void CheckPhase2()
    {
        if (phase2) return;
        if (maxHP <= 0) return;

        int threshold = Mathf.CeilToInt(maxHP * phase2HpPercent);
        if (HP <= threshold)
        {
            EnterPhase2();
        }
    }

    private void EnterPhase2()
    {
        if (phase2) return;
        phase2 = true;

        // 数值强化
        damageToPlayer += phase2AddDamage;
        attackCD *= phase2AttackCdMul;

        // 屏幕抖动
        if (cameraShake != null)
            cameraShake.Shake(shakeDuration, shakeStrength);

        // 强制玩家受击表现（不扣血）
        if (forcePlayerHitOnPhase2 && target != null && !target.isDead)
        {
            if (lockPlayerMoveBriefly)
                target.ForceHitReaction(lockMoveSeconds);
            else
                target.ForceHitReaction(0f);
        }

        // 闪红 -> 切 Phase2 材质
        if (flashBeforePhase2 && !phase2CoroutineRunning)
            StartCoroutine(CoFlashThenApplyPhase2());
        else
            ApplyPhase2All();
    }

    private IEnumerator CoFlashThenApplyPhase2()
    {
        phase2CoroutineRunning = true;

        // 兼容 Standard(_Color) + URP/HDRP(_BaseColor)
        SetColor(bodyRenderer, flashColor);
        SetColor(headRenderer, flashColor);
        SetColor(upperRenderer, flashColor);
        SetColor(hatRenderer, flashColor);
        SetColor(accessoriesRenderer, flashColor);

        yield return new WaitForSeconds(flashDuration);

        ApplyPhase2All();

        phase2CoroutineRunning = false;
    }

    private void ApplyPhase2All()
    {
        ApplyPhase2Mat(bodyRenderer);
        ApplyPhase2Mat(headRenderer);
        ApplyPhase2Mat(upperRenderer);
        ApplyPhase2Mat(hatRenderer);
        ApplyPhase2Mat(accessoriesRenderer);
    }

    private void ApplyPhase2Mat(SkinnedMeshRenderer r)
    {
        if (r == null || phase2Material == null) return;

        var mats = r.materials; // 实例化材质，不影响别的敌人
        for (int i = 0; i < mats.Length; i++)
            mats[i] = phase2Material;
        r.materials = mats;
    }

    private void SetColor(SkinnedMeshRenderer r, Color c)
    {
        if (r == null) return;

        var mats = r.materials;
        for (int i = 0; i < mats.Length; i++)
        {
            var m = mats[i];
            if (m == null) continue;

            if (m.HasProperty("_Color"))
                m.SetColor("_Color", c);

            if (m.HasProperty("_BaseColor"))
                m.SetColor("_BaseColor", c);
        }
        r.materials = mats;
    }

    // =========================================================
    // Die / Drop
    // =========================================================
    private void Die()
    {
        if (isDead) return;

        isDead = true;

        if (animator != null) animator.SetBool(deadBool, true);

        StopMove();
        TryDropHealthPack();

        if (agent != null) agent.enabled = false;
    }

    private void TryDropHealthPack()
    {
        if (dropped) return;
        dropped = true;

        if (healthPackPrefab == null) return;
        if (Random.value > dropChance) return;

        Instantiate(healthPackPrefab, transform.position + dropOffset, Quaternion.identity);
    }

    // =========================================================
    // Movement helpers
    // =========================================================
    private void StopMove()
    {
        if (agent == null || !agent.enabled) return;

        agent.isStopped = true;
        agent.ResetPath();
    }

    private void UpdateMoveAnim()
    {
        if (animator == null) return;

        float speed01 = (agent != null && agent.enabled && agent.speed > 0.01f)
            ? agent.velocity.magnitude / agent.speed
            : 0f;

        animator.SetFloat(moveYParam, speed01);
        animator.SetFloat(moveStateParam, speed01 > 0.1f ? 1f : 0f);
    }

    private void FaceTarget(Vector3 targetPos)
    {
        Vector3 dir = targetPos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion rot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * 10f);
    }

    // ✅ 如果 LayerMask 没配（value==0），尝试自动推断 Player Layer
    private void AutoFixPlayerLayerMaskIfNeeded()
    {
        if (playerLayer.value != 0) return;

        if (target != null)
        {
            int layer = target.gameObject.layer;
            if (layer >= 0 && layer <= 31)
            {
                playerLayer = 1 << layer;
                return;
            }
        }

        int playerLayerIndex = LayerMask.NameToLayer("Player");
        if (playerLayerIndex >= 0)
        {
            playerLayer = 1 << playerLayerIndex;
        }
    }

    void OnDrawGizmosSelected()
    {
        Transform o = attackOrigin != null ? attackOrigin : transform;
        Gizmos.DrawWireSphere(o.position + transform.forward * hitForwardOffset, hitRadius);
    }
}
