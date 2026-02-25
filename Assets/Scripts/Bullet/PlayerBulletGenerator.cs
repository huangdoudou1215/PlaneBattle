using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 主角子弹生成器
/// </summary>
public class PlayerBulletGenerator
{
    private float m_timer;
    /// <summary>
    /// 开炮间隔
    /// </summary>
    private const float FIRE_INTERVAL = 0.1f;

    /// <summary>
    /// 对象池
    /// </summary>
    private Queue<PlayerBullet> m_reusePool = new Queue<PlayerBullet>();

    /// <summary>
    /// 子弹父节点
    /// </summary>
    private Transform m_bulletRoot;

    /// <summary>
    /// 角色飞机的Transform
    /// </summary>
    public Transform m_playerTrans;

    /// <summary>
    /// 额外并行子弹数量（初始单发，+1 表示双发，以此类推）
    /// </summary>
    private int m_parallelExtraCount;

    /// <summary>
    /// 并行子弹之间的水平间距
    /// </summary>
    private const float PARALLEL_BULLET_X_OFFSET = 0.35f;

    public void Init(Transform player)
    {
        m_playerTrans = player;
    }

    public void Update()
    {
        if (null == m_playerTrans) return;
        m_timer += Time.deltaTime;
        if (m_timer >= FIRE_INTERVAL)
        {
            m_timer = 0;
            CreateBullets();
        }
    }

    /// <summary>
    /// 叠加一层平行子弹
    /// </summary>
    public void AddParallelBullet()
    {
        ++m_parallelExtraCount;
    }

    /// <summary>
    /// 创建子弹（支持并行多发）
    /// </summary>
    private void CreateBullets()
    {
        if (null == m_bulletRoot)
        {
            var rootObj = new GameObject("PlayerBulletRoot");
            m_bulletRoot = rootObj.transform;
        }

        int bulletCount = 1 + m_parallelExtraCount;
        float centerIndex = (bulletCount - 1) * 0.5f;
        Vector3 basePos = m_playerTrans.position + new Vector3(0, 0.7f, 0);
        for (int i = 0; i < bulletCount; ++i)
        {
            float xOffset = (i - centerIndex) * PARALLEL_BULLET_X_OFFSET;
            CreateSingleBullet(basePos + new Vector3(xOffset, 0, 0));
        }
    }

    private void CreateSingleBullet(Vector3 startPos)
    {
        PlayerBullet bullet = null;
        if (m_reusePool.Count > 0)
        {
            // 从对象池中取出子弹重复利用
            bullet = m_reusePool.Dequeue();
            bullet.ActiveSelf(true);
        }
        else
        {
            // 对象池中没有可用的子弹，则实例化一个
            var prefab = ResourceMgr.instance.LoadRes<GameObject>("Bullet/bullet");
            var obj = Object.Instantiate(prefab);
            obj.transform.SetParent(m_bulletRoot, false);
            bullet = obj.GetComponent<PlayerBullet>();
            bullet.power = 1;
            bullet.speed = 7f;
            // 子弹回收
            bullet.backToPoolAction = () =>
            {
                // 塞回对象池中
                m_reusePool.Enqueue(bullet);
            };
        }
        bullet.SetStartPos(startPos);
    }

    /// <summary>
    /// 清空子弹对象
    /// </summary>
    public void ClearBullets()
    {
        if(null != m_bulletRoot)
        {
            Object.Destroy(m_bulletRoot.gameObject);
            m_bulletRoot = null;
        }

        m_reusePool.Clear();
    }
}
