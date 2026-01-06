using UnityEngine;

/// <summary>
/// 陷阱脚本：玩家接触后受到30点伤害
/// </summary>
public class TrapDamage : MonoBehaviour
{
    [Header("陷阱配置")]
    public int damage = 5; // 固定30点伤害
    public float hitCD = 1f; // 防止陷阱连续触发（可选，1秒内只触发一次）
    private float lastHitTime = -999f;

    // 玩家接触陷阱时触发
    private void OnTriggerEnter(Collider other)
    {
        // 1. 只对玩家生效（玩家对象必须设Tag为"Player"）
        if (!other.CompareTag("Player")) return;

        // 2. 冷却时间内不触发（可选，避免瞬间多次扣血）
        if (Time.time - lastHitTime < hitCD) return;

        // 3. 获取玩家脚本并调用扣血方法
        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null && !player.isDead)
        {
            player.TakeDamage(damage); // 调用你PlayerController里的扣血方法
            lastHitTime = Time.time; // 更新冷却时间
        }
    }
}