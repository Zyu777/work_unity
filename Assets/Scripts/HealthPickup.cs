using UnityEngine;

public class HealthPickup : MonoBehaviour
{
    public int healAmount = 5;

    private bool picked;

    void Start()
    {
        // 触发更稳定：血包用 Kinematic Rigidbody
        var rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (picked) return;

        var player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        // 满血不消耗血包
        if (!player.Heal(healAmount)) return;

        picked = true;
        Destroy(gameObject);
    }
}