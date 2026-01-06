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
    public float chaseRange = 15f;
    public float attackRange = 2f;

    [Header("Attack")]
    public float attackCD = 3f;
    private float lastAttackTime = -999f;
    public int damageToPlayer = 2;

    [Header("Animator Params")]
    public string attackTrigger = "Attack";
    public string hitTrigger = "Hit";
    public string deadBool = "Dead";
    public string moveYParam = "MoveY";
    public string moveStateParam = "MoveState";

    [Header("Drop (Health Pack)")]
    public GameObject healthPackPrefab;
    [Range(0f, 1f)] public float dropChance = 0.5f; // ✅ 50%
    public Vector3 dropOffset = new Vector3(0, 0.2f, 0);

    private bool dropped = false;

    void Awake()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    void Start()
    {
        if (agent != null)
        {
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

        if (agent != null && agent.enabled && !agent.isOnNavMesh)
        {
            StopMove();
            UpdateMoveAnim();
            return;
        }

        float dis = Vector3.Distance(target.transform.position, transform.position);

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

            UpdateMoveAnim();
            return;
        }

        StopMove();
        UpdateMoveAnim();
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;

        HP -= damage;

        if (animator != null) animator.SetTrigger(hitTrigger);

        if (HP <= 0)
        {
            Die();
        }
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
}
