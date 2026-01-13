using UnityEngine;
using Mirror;

public class PlayerNetworking : NetworkBehaviour
{
    [Header("Refs")]
    public PlayerController controller;
    public Rigidbody rb;
    public Camera playerCamera;

    [Header("Sync Data")]
    [SyncVar(hook = nameof(OnHPChanged))]
    public int syncHP = 20;

    void Awake()
    {
        // --- 核心修改：防止联机时出现两个玩家 ---
        // 如果网络已经激活（说明是点击 Host/Client 进来的）
        // 并且这个物体是手动摆放在场景里的（sceneId != 0 表示它不是被 NetworkManager 克隆出来的）
        if (NetworkClient.active && GetComponent<NetworkIdentity>().sceneId != 0)
        {
            Debug.Log("检测到联机状态，自动删除手动放置的单机版 Player，使用 NetworkManager 生成的克隆体。");
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        // 情况 A：如果没有连接到服务器（纯单机运行）
        // 情况 B：连接到了服务器，且这个角色是我自己的本地玩家 (isLocalPlayer)
        if (!NetworkClient.active || isLocalPlayer)
        {
            // 激活控制和相机
            if (controller != null) controller.enabled = true;
            if (playerCamera != null) playerCamera.gameObject.SetActive(true);

            // 单机或本地玩家需要真实的物理模拟
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }

            // 锁定鼠标
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (!NetworkClient.active)
                Debug.Log("<color=cyan>单机模式启动：已手动开启玩家控制权</color>");
            else
                Debug.Log("<color=green>联机模式启动：本地玩家已激活</color>");
        }
        // 情况 C：联机模式下的远程玩家（别人）
        else
        {
            // 禁用他人控制和相机
            if (controller != null) controller.enabled = false;
            if (playerCamera != null) playerCamera.gameObject.SetActive(false);

            // 别人的位置由 NetworkTransform 同步，物理应设为运动学(isKinematic)以减少计算
            if (rb != null) rb.isKinematic = true;

            Debug.Log("远程玩家接入：已禁用其相机和控制脚本");
        }

        // 初始化血量（单机直接取 syncHP 默认值，联机则由服务器同步）
        if (controller != null) controller.HP = syncHP;
    }

    void OnHPChanged(int oldHP, int newHP)
    {
        // 只有在联机状态下这个 Hook 才会生效
        if (controller != null)
        {
            controller.HP = newHP;
            Debug.Log($"血量同步更新: {newHP}");
        }
    }

    [Command]
    public void CmdTakeDamage(int amount)
    {
        syncHP -= amount;
        if (syncHP <= 0) syncHP = 0;
    }
}