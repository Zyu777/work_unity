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
    public int HP = 20;
    public bool isDead;

    [Header("Combat")]
    public LayerMask enemyLayer;          // 记得勾选 Enemy Layer
    public Transform attackOrigin;        // 不填就用自己
    public float attackRadius = 1.0f;     // 攻击半径
    public float normalAttackRange = 2.0f;
    public int normalDamage = 2;
    public float heavyAttackRange = 3.5f;
    public int heavyDamage = 4;

    [Header("Animator Params")]
    public string attackTrigger = "Attack";
    public string heavyAttackTrigger = "HeavyAttack";
    public string chargingBool = "Charging";   // 右键蓄力用
    public string hitTrigger = "Hit";
    public string deadBool = "Dead";
    public string moveXParam = "MoveX";
    public string moveYParam = "MoveY";
    public string moveStateParam = "MoveState";

    [Header("Right Click Charge")]
    public float chargeTime = 0.4f;   // 按住超过这个时间算“蓄力成功”
    private float chargeTimer = 0f;
    private bool holdingRight = false;
    private bool charged = false;     // 是否已经蓄满（决定伤害/范围）

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

        // 避免Inspector忘记勾导致一直不能转向/打不到
        if (!canMove) canMove = true;

        // 确保初始不是蓄力状态
        if (animator != null) animator.SetBool(chargingBool, false);
    }

    void Update()
    {
        if (isDead) return;

        // ✅ 新增：如果正在蓄力（Charging=true），禁止移动/转向
        bool isChargingAnim = animator != null && animator.GetBool(chargingBool);
        bool allowMoveNow = canMove && !isChargingAnim;

        if (allowMoveNow)
        {
            Moveplayer();
            RotatePlayer();
        }
        else
        {
            // 可选：锁住移动时把移动参数归零，避免脚踩步/BlendTree乱动
            if (animator != null)
            {
                animator.SetFloat(moveXParam, 0f);
                animator.SetFloat(moveYParam, 0f);
            }
        }

        Attack();        // 左键
        HeavyAttack();   // 右键按住蓄力 + 松开释放（原逻辑保留）
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

    // 左键普攻（保留原样）
    public void Attack()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        if (animator != null)
        {
            animator.ResetTrigger(attackTrigger);
            animator.SetTrigger(attackTrigger);
        }

        DealDamageInFront(normalAttackRange, normalDamage);
    }

    // 右键：按住蓄力（Charging=true）-> 松开释放重击（Charging=false）
    public void HeavyAttack()
    {
        // 右键按下：开始蓄力起手（进入Attack_Start）
        if (Input.GetMouseButtonDown(1))
        {
            holdingRight = true;
            chargeTimer = 0f;
            charged = false;

            if (animator != null)
            {
                animator.ResetTrigger(heavyAttackTrigger);
                animator.SetTrigger(heavyAttackTrigger);

                // 初始不蓄力
                animator.SetBool(chargingBool, false);
            }
        }

        // 右键按住：计时，超过阈值进入 Attack_Hold（Charging=true）
        if (holdingRight && Input.GetMouseButton(1))
        {
            chargeTimer += Time.deltaTime;

            if (!charged && chargeTimer >= chargeTime)
            {
                charged = true;

                if (animator != null)
                {
                    animator.SetBool(chargingBool, true); // ✅ 进入蓄力
                }
            }
        }

        // 右键松开：结束蓄力（Charging=false）并结算伤害
        if (holdingRight && Input.GetMouseButtonUp(1))
        {
            holdingRight = false;

            if (animator != null)
            {
                animator.SetBool(chargingBool, false); // ✅ 松开就结束蓄力 -> 可以移动了（Update会放开）
            }

            // 原版：蓄满用重击，否则按普攻（你也可以改成“没蓄满不出招”）
            float range = charged ? heavyAttackRange : normalAttackRange;
            int dmg = charged ? heavyDamage : normalDamage;

            DealDamageInFront(range, dmg);

            // 重置
            chargeTimer = 0f;
            charged = false;
        }
    }

    private void DealDamageInFront(float range, int damage)
    {
        Vector3 center = attackOrigin.position + transform.forward * (range * 0.6f);

        Collider[] hits = Physics.OverlapSphere(center, attackRadius, enemyLayer, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return;

        foreach (var h in hits)
        {
            Transform root = h.transform.root;

            root.gameObject.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
            h.GetComponentInParent<Transform>().gameObject.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);

            break; // 只打一个
        }
    }

    public void TakeDamage(int attackValue)
    {
        if (isDead) return;

        HP -= attackValue;

        if (animator != null) animator.SetTrigger(hitTrigger);
        if (ps != null) ps.Play();

        if (HP <= 0)
        {
            isDead = true;
            if (animator != null) animator.SetBool(deadBool, true);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (attackOrigin == null) return;
        Gizmos.DrawWireSphere(attackOrigin.position + transform.forward * (normalAttackRange * 0.6f), attackRadius);
    }
}
