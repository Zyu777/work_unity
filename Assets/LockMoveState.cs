using UnityEngine;

public class LockMoveState : StateMachineBehaviour
{
    private PlayerController pc;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // Animator 常在子物体上，所以用 GetComponentInParent
        pc = animator.GetComponentInParent<PlayerController>();

        // 如果还找不到，至少别崩
        if (pc != null) pc.canMove = false;
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (pc != null) pc.canMove = false;
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (pc != null) pc.canMove = true;
    }
}