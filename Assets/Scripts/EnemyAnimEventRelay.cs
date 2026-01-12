using UnityEngine;

/// <summary>
/// 敌人动画事件转发器
/// 挂在 Animator 所在的 GameObject 上
/// 负责把 Animation Event 转发给 EnemyController
/// </summary>
public class EnemyAnimEventRelay : MonoBehaviour
{
    [Header("Target")]
    public EnemyController enemy;   // 拖父物体上的 EnemyController

    public void AnimEvent_AttackStart()
    {
        if (enemy == null) return;
        enemy.AnimEvent_AttackStart();
    }

    public void AnimEvent_AttackHit()
    {
        if (enemy == null) return;
        enemy.AnimEvent_AttackHit();
    }

    public void AnimEvent_AttackEnd()
    {
        if (enemy == null) return;
        enemy.AnimEvent_AttackEnd();
    }

    public void AnimEvent_NormalHit()
    {
        if (enemy == null) return;
        enemy.AnimEvent_AttackHit();
    }
}