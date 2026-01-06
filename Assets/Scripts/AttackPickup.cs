using UnityEngine;

public class AttackPickup : MonoBehaviour
{
    public int addAttack = 1;
    private bool picked;

    void Start()
    {
        // 跟你血包一致：触发更稳定
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

        picked = true;

        player.AddAttack(addAttack);

        Destroy(gameObject);
    }
}