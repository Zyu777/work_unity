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
    public float chaseRange = 15f;     // 用于“第一次感知”
    public float attackRange = 2f;

    [Header("Aggro (Solution 2)")]
    public bool hasAggro = false;      // 一旦锁定目标就一直追
    public float loseAggroRange = 0f;  // 0=永不丢失；>0=超过此距离才丢失仇恨

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

    // 攻击结算门闩：由动画事件开启/关闭
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

    [Header("Chase Destination Update")]
    public float repathInterval = 0.15f; // 追击目的地更新节流
    private float nextRepathTime = 0f;
    private Vector3 lastChaseGoal = new Vector3(99999, 99999, 99999);
    public float goalChangeThreshold = 0.25f;

    // ================= DEBUG (可选) =================
    public bool showDebugGUI = false;
    private string _dbg;
    private float _dbgNext;

    void Awake()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (attackOrigin == null) attackOrigin = transform;

        TryFindTarget(force: true);
        AutoFixPlayerLayerMaskIfNeeded();
    }

    void Start()
    {
        if (agent != null)
        {
            agent.stoppingDistance = attackRange;
            agent.isStopped = false;
            agent.updateRotation = false; // 我们自己转向
        }

        // 出生点吸附
        EnsureOnNavMesh();

        // 如果你项目里用 RootMotion，建议先关掉避免和Agent抢控制
        if (animator != null) animator.applyRootMotion = false;
    }

    void Update()
    {
        if (isDead)
        {
            StopMove(clearPath: true);
            UpdateMoveAnim();
            return;
        }

        // 定时补找玩家
        TryFindTarget(force: false);

        if (target == null || target.isDead)
        {
            hasAggro = false;
            StopMove(clearPath: false);
            UpdateMoveAnim();
            return;
        }

        // 不在NavMesh上就吸附
        EnsureOnNavMesh();
        if (agent != null && agent.enabled && !agent.isOnNavMesh)
        {
            StopMove(clearPath: false);
            UpdateMoveAnim();
            return;
        }

        float dis = Vector3.Distance(target.transform.position, transform.position);

        // =========================
        // Aggro 逻辑（解法二）
        // - 未锁定：只有进入 chaseRange 才锁定
        // - 已锁定：一直追（可选：超出 loseAggroRange 才丢失）
        // =========================
        // ===== Aggro（靠近触发 + 锁定）=====
        if (!hasAggro)
        {
            // 玩家第一次进入追击范围 => 锁定
            if (dis <= chaseRange)
                hasAggro = true;
        }
        else
        {
            // 默认不丢失仇恨（如果你想丢失，再用 loseAggroRange）
            if (loseAggroRange > 0f && dis >= loseAggroRange)
                hasAggro = false;
        }


        // =========================
        // 攻击（有仇恨且在攻击范围）
        // =========================
        if (hasAggro && dis <= attackRange)
        {
            StopMove(clearPath: false); // ✅ 只停，不清路
            FaceTarget(target.transform.position);

            if (Time.time - lastAttackTime >= attackCD)
            {
                if (animator != null) animator.SetTrigger(attackTrigger);
                lastAttackTime = Time.time;
                // attackArmed 由动画事件 AttackStart 开启
            }

            UpdateMoveAnim();
            return;
        }

        // =========================
        // 追击（有仇恨就追）
        // =========================
        if (hasAggro)
        {
            ChaseTarget();
            FaceTarget(target.transform.position);
            UpdateMoveAnim();
            return;
        }

        // =========================
        // 没仇恨：停
        // =========================
        StopMove(clearPath: false);
        UpdateMoveAnim();
    }

    // =========================================================
    // 追击逻辑（更稳 + 不每帧重算）
    // =========================================================
    private void ChaseTarget()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        agent.isStopped = false;

        // 节流：避免每帧 SetDestination
        if (Time.time < nextRepathTime) return;
        nextRepathTime = Time.time + repathInterval;

        Vector3 desired = target.transform.position;

        // 目标变化很小就不重设
        Vector3 flatDelta = desired - lastChaseGoal;
        flatDelta.y = 0;
        if (flatDelta.sqrMagnitude < goalChangeThreshold * goalChangeThreshold && agent.hasPath)
            return;

        // 采样到 NavMesh 上
        if (NavMesh.SamplePosition(desired, out var hit, 10f, NavMesh.AllAreas))
        {
            lastChaseGoal = hit.position;
            agent.SetDestination(hit.position);
        }
        else
        {
            // 采样不到目标附近网格时：不要直接给玩家原点（容易 PathInvalid）
            // 没路才兜底到自己附近可走点
            if (!agent.hasPath && NavMesh.SamplePosition(transform.position, out var hit2, 10f, NavMesh.AllAreas))
            {
                lastChaseGoal = hit2.position;
                agent.SetDestination(hit2.position);
            }
        }
    }

    // =========================================================
    // Animation Events
    // =========================================================
    public void AnimEvent_AttackStart()
    {
        if (isDead) return;
        attackArmed = true;
    }

    public void AnimEvent_AttackHit()
    {
        if (isDead) return;
        if (!attackArmed) return;
        if (target == null || target.isDead) return;

        attackArmed = false; // 防止同一次攻击多次结算

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

    public void AnimEvent_AttackEnd()
    {
        attackArmed = false;
    }

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

        // 被打也算“激怒”
        hasAggro = true;

        if (HP <= 0)
            Die();
    }

    private void Die()
    {
        if (isDead) return;

        isDead = true;
        hasAggro = false;

        if (animator != null) animator.SetBool(deadBool, true);

        StopMove(clearPath: true);
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
    private void StopMove(bool clearPath)
    {
        if (agent == null || !agent.enabled) return;

        agent.isStopped = true;

        if (clearPath)
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
        AutoFixPlayerLayerMaskIfNeeded();
    }

    private void EnsureOnNavMesh()
    {
        if (agent == null || !agent.enabled) return;

        if (!agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out var hit, 30f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                agent.ResetPath();
                agent.isStopped = false;
            }
        }
    }

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

    // ================= Debug GUI (可选) =================
    void LateUpdate()
    {
        if (!showDebugGUI) return;

        if (agent == null) { _dbg = "agent = null"; return; }

        if (Time.time > _dbgNext)
        {
            _dbgNext = Time.time + 0.5f;

            string t = target ? target.name : "null";
            float dis = target ? Vector3.Distance(transform.position, target.transform.position) : -1f;

            string state =
                (target == null || target.isDead) ? "NO TARGET" :
                (!hasAggro) ? "IDLE" :
                (dis <= attackRange) ? "ATTACK" :
                "CHASE";

            _dbg =
                $"STATE={state}  aggro={hasAggro}\n" +
                $"t={t} dis={dis:F2}\n" +
                $"attackRange={attackRange:F2} chaseRange={chaseRange:F2} loseAggroRange={loseAggroRange:F2}\n" +
                $"onNavMesh={agent.isOnNavMesh} enabled={agent.enabled}\n" +
                $"stopped={agent.isStopped} speed={agent.speed:F2}\n" +
                $"hasPath={agent.hasPath} pathStatus={agent.pathStatus}\n" +
                $"remain={agent.remainingDistance:F2} vel={agent.velocity.magnitude:F2}";
        }
    }

    void OnGUI()
    {
        if (!showDebugGUI) return;

        GUI.color = Color.white;
        GUI.Box(new Rect(8, 8, 560, 190), "");
        GUI.Label(new Rect(16, 16, 540, 170), _dbg);
    }

    void OnDrawGizmosSelected()
    {
        Transform o = attackOrigin != null ? attackOrigin : transform;
        Gizmos.DrawWireSphere(o.position + transform.forward * hitForwardOffset, hitRadius);
    }
}
