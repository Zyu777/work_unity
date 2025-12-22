using UnityEngine;
using UnityEngine.AI;

public class EnemyController : MonoBehaviour
{
    [Header("Refs")]
    public NavMeshAgent agent;
    public Animator animator;
    public PlayerController target;

    [Header("HP")]
    public int HP = 10;
    public bool isDead = false;

    [Header("Ranges")]
    public float chaseRange = 8f;
    public float attackRange = 2f;

    [Header("Attack")]
    public float attackCD = 1.0f;
    private float lastAttackTime = -999f;
    public int damageToPlayer = 2;

    [Header("Animator Params")]
    public string attackTrigger = "Attack";
    public string hitTrigger = "Hit";
    public string deadBool = "Dead";
    public string moveYParam = "MoveY";
    public string moveStateParam = "MoveState";

    void Awake()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    void Start()
    {
        if (agent != null)
        {
            // 关键：停止距离=攻击距离，避免很远就停/踏步
            agent.stoppingDistance = attackRange;
            agent.isStopped = false;
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

        float dis = Vector3.Distance(target.transform.position, transform.position);

        // 1) 攻击范围内：停下并攻击
        if (dis <= attackRange)
        {
            StopMove();
            FaceTarget(target.transform.position);

            if (Time.time - lastAttackTime >= attackCD)
            {
                if (animator != null) animator.SetTrigger(attackTrigger);
                target.TakeDamage(damageToPlayer);
                lastAttackTime = Time.time;
            }

            UpdateMoveAnim();
            return;
        }

        // 2) 追击范围内：追踪
        if (dis <= chaseRange)
        {
            if (agent != null)
            {
                agent.isStopped = false;

                // 投影到 NavMesh，避免目标点不可达造成抖动/踏步
                if (NavMesh.SamplePosition(target.transform.position, out var hit, 2f, NavMesh.AllAreas))
                    agent.SetDestination(hit.position);
                else
                    agent.SetDestination(target.transform.position);
            }

            UpdateMoveAnim();
            return;
        }

        // 3) 超出追击范围：停下
        StopMove();
        UpdateMoveAnim();
    }

    // 玩家攻击会调用这个
    public void TakeDamage(int damage)
    {
        if (isDead) return;

        HP -= damage;

        if (animator != null)
            animator.SetTrigger(hitTrigger);

        if (HP <= 0)
        {
            isDead = true;

            if (animator != null)
                animator.SetBool(deadBool, true);

            StopMove();

            // 可选：禁用碰撞/Agent，避免尸体挡路
            if (agent != null) agent.enabled = false;

            // 可选：禁用这个脚本，不再Update
            enabled = false;
        }
    }

    private void StopMove()
    {
        if (agent == null) return;

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
}
