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

    // =========================
    // 攻击命中帧（推荐使用）
    // =========================
    public void AnimEvent_AttackHit()
    {
        if (enemy == null) return;
        enemy.AnimEvent_AttackHit();
    }

    // =========================
    // 攻击结束（可选）
    // =========================
    public void AnimEvent_AttackEnd()
    {
        if (enemy == null) return;
        enemy.AnimEvent_AttackEnd();
    }

    // =========================
    // 兼容旧动画事件名（你控制台报错里的）
    // =========================
    public void AnimEvent_NormalHit()
    {
        if (enemy == null) return;
        enemy.AnimEvent_AttackHit();
    }
}