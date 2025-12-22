using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlaerCon : MonoBehaviour
{
    public float speed;
    public float sensitivity;
    public Animator animator;
    public bool isDead;

    public int HP;
    // Start is called before the first frame update
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        animator.SetFloat("MoveX", 0);
        
    }

    // Update is called once per frame
    void Update()
    {
        if (isDead)
        {
            return;
        }
        Moveplayer();
        RotatePlayer();
        Attack();
    }

    private void Moveplayer()
    {
        //获取玩家输入
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        animator.SetFloat("MoveX", horizontal);
        animator.SetFloat("MoveY", vertical);
        if (Input.GetKey(KeyCode.LeftShift))
        {
            animator.SetFloat("MoveState", 0);
            speed = 1.5f;
        }
        else
        {
            animator.SetFloat("MoveState", 1);
            speed = 3.5f;

        }
        
        //Debug.Log("获取到的值："+horizontal+","+vertical);
        //根据玩家的输入改变人物位置
        //transform.position += new Vector3(horizontal, 0, vertical)*Time.deltaTime*speed;
        Vector3 movement = new Vector3(horizontal, 0, vertical)*Time.deltaTime * speed;
        transform.Translate(movement);
    }

    private void RotatePlayer()
    {
        float mouseX = Input.GetAxis("Mouse X") * sensitivity;
        transform.Rotate(Vector3.up * mouseX);
    }
    //攻击
    private void Attack()
    {
        if (Input.GetMouseButtonDown(0))
        {
            animator.SetTrigger("Attack");
        }
    }
    
    //受伤
    public void TakeDamage(int attackValue)
    {
        HP -= attackValue;
        animator.SetTrigger("Hit");
        if (HP <= 0)
        {
            animator.SetTrigger("Dead");
            isDead = true;
        }
    }
}
