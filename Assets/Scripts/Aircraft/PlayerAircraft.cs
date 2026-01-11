using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 主角飞机
/// </summary>
public class PlayerAircraft : BaseAircraft
{
    // 移除 m_isPress 变量，因为不再需要鼠标按下检测
    
    // 移动速度
    public float moveSpeed = 10f;
    
    private const float SCREEN_INNER_OFFSET = 20;

    // 子弹生成器
    private PlayerBulletGenerator m_bulletGenerator = new PlayerBulletGenerator();

    // 是否正在爆炸动画中
    private bool m_isExploding = false;

    protected override void Awake()
    {
        base.Awake();
        // 监听动画帧事件
        m_aniEvent.aniEventCb = (msg) =>
        {
            // 爆炸动画播放结束
            if ("explode_finish" == msg && m_isExploding)
            {
                // 减少生命值
                --GameMgr.instance.LifeCnt;

                // 检查是否还有生命
                if (GameMgr.instance.LifeCnt > 0)
                {
                    // 还有生命，复活玩家
                    Revive();
                }
                else
                {
                    // 没有生命了，游戏结束
                    DestroySelf();
                    GameMgr.instance.GameOver();
                }
            }
        };

        m_bulletGenerator.Init(m_selfTrans);
    }

    // 移除 OnMouseDown 和 OnMouseUp 方法，因为不再需要鼠标事件

    protected void Update()
    {
        if (GameState.Pause == GameMgr.instance.gameState) return;

        // 使用方向键控制移动
        float moveX = Input.GetAxis("Horizontal");
        float moveY = Input.GetAxis("Vertical");
        
        if (moveX != 0 || moveY != 0)
        {
            // 计算移动向量
            Vector3 moveDirection = new Vector3(moveX, moveY, 0);
            
            // 根据速度和时间计算移动距离
            Vector3 newPosition = m_selfTrans.position + moveDirection * moveSpeed * Time.deltaTime;
            
            // 将世界坐标转换为屏幕坐标进行边界检测
            Vector3 screenPos = Camera.main.WorldToScreenPoint(newPosition);
            
            // 限制坐标在屏幕内
            if (screenPos.x < SCREEN_INNER_OFFSET)
            {
                screenPos.x = SCREEN_INNER_OFFSET;
            }
            else if(screenPos.x > Screen.width - SCREEN_INNER_OFFSET)
            {
                screenPos.x = Screen.width - SCREEN_INNER_OFFSET;
            }
            if (screenPos.y < SCREEN_INNER_OFFSET)
            {
                screenPos.y = SCREEN_INNER_OFFSET;
            }
            else if (screenPos.y > Screen.height - SCREEN_INNER_OFFSET)
            {
                screenPos.y = Screen.height - SCREEN_INNER_OFFSET;
            }
            
            // 将限制后的屏幕坐标转换回世界坐标
            newPosition = Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 5));
            m_selfTrans.position = newPosition;
        }

        // 按 R 放炸弹
        if (Input.GetKeyDown(KeyCode.R))
        {
            GameMgr.instance.KillAllEnemy();
        }

        // 子弹生成器
        m_bulletGenerator.Update();
    }

    /// <summary>
    /// 碰撞检测
    /// </summary>
    /// <param name="other"></param>
    public override void OnTriggerEnter2D(Collider2D other)
    {
        switch (other.tag)
        {
            case "Enemy":
            case "EnemyBullet":
                {
                    // 爆炸
                    Explode();
                }
                break;
            case "SuperBomb":
                {
                    Destroy(other.gameObject);
                    ++GameMgr.instance.BombCnt;
                }
                break;
        }
    }

    /// <summary>
    /// 爆炸
    /// </summary>
    public override void Explode()
    {
        // 如果已经在爆炸中，不要重复触发
        if (m_isExploding) return;

        m_isExploding = true;
        m_collider.enabled = false;
        ani.SetBool("explode", true);
    }

    /// <summary>
    /// 复活玩家
    /// </summary>
    private void Revive()
    {
        // 重置动画状态
        ani.SetBool("explode", false);
        ani.Rebind();

        // 清空子弹
        m_bulletGenerator.ClearBullets();

        // 先将玩家飞机移回屏幕底部中央
        Vector3 revivePos = Camera.main.ScreenToWorldPoint(new Vector3(Screen.width / 2, 100, 5));
        m_selfTrans.position = revivePos;

        // 强制同步物理系统，确保碰撞体位置更新
        Physics2D.SyncTransforms();

        // 移动到安全位置后再启用碰撞体
        if (m_collider != null)
        {
            m_collider.enabled = true;
        }

        // 复活完成后才重置爆炸标志
        m_isExploding = false;
    }

    public override void DestroySelf()
    {
        Destroy(m_selfGo);
        m_bulletGenerator.ClearBullets();
    }
}