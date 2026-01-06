using UnityEngine;
using UnityEngine.AI;

public class EnemyController : MonoBehaviour
{
    [Header("Refs")]
    public NavMeshAgent agent;
    public Animator animator;
    public PlayerController target;

    [Header("Auto Find Player")]
    public string playerTag = "Player";
    public float retargetInterval = 1.0f;
    private float nextRetargetTime = 0f;

    [Header("HP")]
    public int HP = 10;
    public bool isDead = false;

    [Header("Ranges")]
    public float chaseRange = 15f;
    public float attackRange = 2f;

    [Header("Attack")]
    public float attackCD = 3f;
    private float lastAttackTime = -999f;
    public int damageToPlayer = 2;

    [Header("Hit Check (Animation Event Only)")]
    public Transform attackOrigin;             // 空则用 transform
    public float hitRadius = 0.6f;
    public float hitForwardOffset = 0.8f;
    public LayerMask playerLayer;              // 只检测 Player Layer
    public bool requireFacing = true;
    [Range(0f, 180f)] public float facingAngle = 70f;

    // ✅ 攻击结算门闩：建议由动画事件开启/关闭
    private bool attackArmed = false;

    [Header("Animator Params")]
    public string attackTrigger = "Attack";
    public string hitTrigger = "Hit";
    public string deadBool = "Dead";
    public string moveYParam = "MoveY";
    public string moveStateParam = "MoveState";

    [Header("Drop (Health Pack)")]
    public GameObject healthPackPrefab;
    [Range(0f, 1f)] public float dropChance = 0.5f; // 50%
    public Vector3 dropOffset = new Vector3(0, 0.2f, 0);
    private bool dropped = false;

    void Awake()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (attackOrigin == null) attackOrigin = transform;

        TryFindTarget(force: true);

        // ✅ 防呆：LayerMask 没配时，尽量自动推断 Player Layer
        AutoFixPlayerLayerMaskIfNeeded();
    }

    void Start()
    {
        if (agent != null)
        {
            agent.stoppingDistance = attackRange;
            agent.isStopped = false;

            // 交给我们自己 FaceTarget 来转向（否则 Agent 会和你手动旋转抢控制）
            agent.updateRotation = false;
        }

        EnsureOnNavMesh();
    }

    void Update()
    {
        if (isDead)
        {
            StopMove();
            UpdateMoveAnim();
            return;
        }

        // 目标可能后生成/被销毁：定时补找
        TryFindTarget(force: false);

        if (target == null || target.isDead)
        {
            StopMove();
            UpdateMoveAnim();
            return;
        }

        // NavMesh 安全
        if (agent != null && agent.enabled && !agent.isOnNavMesh)
        {
            EnsureOnNavMesh();
            StopMove();
            UpdateMoveAnim();
            return;
        }

        float dis = Vector3.Distance(target.transform.position, transform.position);

        // =========================
        // 攻击范围内：只触发攻击动画
        // 命中/结算完全交给动画事件：
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

                // ⚠️ 不建议在这里直接 attackArmed=true（容易和动画不同步）
                // attackArmed = true;  // 改为由 AnimEvent_AttackStart 开启
            }

            UpdateMoveAnim();
            return;
        }

        // =========================
        // 追击范围：跑向玩家（并且面向玩家）
        // =========================
        if (dis <= chaseRange)
        {
            if (agent != null && agent.enabled)
            {
                agent.isStopped = false;

                // 更稳：目的地采样到 NavMesh
                if (NavMesh.SamplePosition(target.transform.position, out var hit, 2f, NavMesh.AllAreas))
                    agent.SetDestination(hit.position);
                else
                    agent.SetDestination(target.transform.position);
            }

            FaceTarget(target.transform.position);
            UpdateMoveAnim();
            return;
        }

        // =========================
        // 超出范围：停止
        // =========================
        StopMove();
        UpdateMoveAnim();
    }

    // =========================================================
    // ✅ Animation Events（强烈推荐你在攻击动画里加）
    // =========================================================

    // ✅ 攻击开始帧：建议在抬手/出招起始那一帧加事件
    public void AnimEvent_AttackStart()
    {
        if (isDead) return;
        attackArmed = true;
    }

    // ✅ 命中帧：挥到人的那一下
    public void AnimEvent_AttackHit()
    {
        // Debug：你需要时可以打开
        // Debug.Log($"[Enemy] AnimEvent_AttackHit fired on {name}, attackArmed={attackArmed}");

        if (isDead) return;
        if (!attackArmed) return; // 没开窗就不结算
        if (target == null || target.isDead) return;

        // 命中后立刻关闭，防止同一攻击多次触发
        attackArmed = false;

        // 可选：面向判定（避免背对也打到）
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

        // 保险：确保确实是当前 target（避免同 layer 其他东西误伤）
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] == null) continue;
            var pc = cols[i].GetComponentInParent<PlayerController>();
            if (pc != null && pc == target)
            {
                target.TakeDamage(damageToPlayer);
                return;
            }
        }
    }

    // ✅ 攻击结束帧（可选）：在动画最后一帧加，保险清理
    public void AnimEvent_AttackEnd()
    {
        attackArmed = false;
    }

    // ✅ 兼容旧动画事件名（你之前报错的 NormalHit）
    public void AnimEvent_NormalHit()
    {
        AnimEvent_AttackHit();
    }

    // =========================================================
    // Damage / Die / Drop
    // =========================================================
    public void TakeDamage(int damage)
    {
        if (isDead) return;

        HP -= damage;

        if (animator != null) animator.SetTrigger(hitTrigger);

        if (HP <= 0)
            Die();
    }

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
    // Helpers
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

        float speed01 =
            (agent != null && agent.enabled && agent.speed > 0.01f)
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

    private void TryFindTarget(bool force)
    {
        if (!force && Time.time < nextRetargetTime) return;
        nextRetargetTime = Time.time + retargetInterval;

        if (target != null && !target.isDead) return;

        GameObject go = GameObject.FindGameObjectWithTag(playerTag);
        if (go == null) return;

        target = go.GetComponent<PlayerController>();
    }

    private void EnsureOnNavMesh()
    {
        if (agent == null || !agent.enabled) return;

        if (!agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out var hit, 5f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
        }
    }

    // ✅ 如果 LayerMask 没配（value==0），尝试自动推断 Player Layer
    private void AutoFixPlayerLayerMaskIfNeeded()
    {
        if (playerLayer.value != 0) return;

        // 优先从 target 的 Layer 推断
        if (target != null)
        {
            int layer = target.gameObject.layer;
            if (layer >= 0 && layer <= 31)
            {
                playerLayer = 1 << layer;
                return;
            }
        }

        // 否则尝试按名字找 Layer
        int playerLayerIndex = LayerMask.NameToLayer("Player");
        if (playerLayerIndex >= 0)
        {
            playerLayer = 1 << playerLayerIndex;
        }
    }

    void OnDrawGizmosSelected()
    {
        Transform o = attackOrigin != null ? attackOrigin : transform;
        Gizmos.DrawWireSphere(
            o.position + transform.forward * hitForwardOffset,
            hitRadius
        );
    }
}
