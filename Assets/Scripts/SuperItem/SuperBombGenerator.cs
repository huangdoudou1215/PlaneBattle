using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 核弹生成器
/// </summary>
public class SuperBombGenerator
{
    public void Init()
    {
        ResetTargetInterval();
    }

    public void Update()
    {
        m_timer += Time.deltaTime;
        if (m_timer >= m_targetInterval)
        {
            ResetTargetInterval();
            if (Random.Range(0, 100) >= 1)
            {
                // 70%的概率掉落核弹
                GenerateSuperBomb();
            }
        }
    }

    /// <summary>
    /// 销毁根节点
    /// </summary>
    public void DestroyRoot()
    {
        if(null != m_bombRoot)
        {
            Object.Destroy(m_bombRoot.gameObject);
            m_bombRoot = null;
        }
    }

    /// <summary>
    /// 重置目标时间间隔
    /// </summary>
    private void ResetTargetInterval()
    {
        m_timer = 0;
        m_targetInterval = Random.Range(MIN_GEN_INTERVAL, MAX_GEN_INTERVAL);
    }

    private void GenerateSuperBomb()
    {
        if (null == m_bombRoot)
        {
            var rootObj = new GameObject("SuperBombRoot");
            m_bombRoot = rootObj.transform;
        }
        var prefab = ResourceMgr.instance.LoadRes<GameObject>("Bullet/supply_bomb");
        var obj = Object.Instantiate(prefab);
        obj.transform.SetParent(m_bombRoot, false);
        // 随机一个初始坐标
        var randomPos = new Vector3(Random.Range(100, Screen.width - 100), Screen.height + 100, 5);
        obj.transform.position = Camera.main.ScreenToWorldPoint(randomPos);
    }

    private float m_timer;
    private float m_targetInterval = MIN_GEN_INTERVAL;
    /// <summary>
    /// 最小生成间隔
    /// </summary>
    private const float MIN_GEN_INTERVAL = 2f;
    /// <summary>
    /// 最大生成间隔
    /// </summary>
    private const float MAX_GEN_INTERVAL = 10f;

    private Transform m_bombRoot;
}

/// <summary>
/// 子弹补给生成器
/// </summary>
public class SuperBulletGenerator
{
    public void Init()
    {
        ResetTargetInterval();
    }

    public void Update()
    {
        m_timer += Time.deltaTime;
        if (m_timer >= m_targetInterval)
        {
            ResetTargetInterval();
            if (Random.Range(0, 100) >= 1)
            {
                GenerateSuperBullet();
            }
        }
    }

    /// <summary>
    /// 销毁根节点
    /// </summary>
    public void DestroyRoot()
    {
        if (null != m_bulletRoot)
        {
            Object.Destroy(m_bulletRoot.gameObject);
            m_bulletRoot = null;
        }
    }

    /// <summary>
    /// 重置目标时间间隔
    /// </summary>
    private void ResetTargetInterval()
    {
        m_timer = 0;
        m_targetInterval = Random.Range(MIN_GEN_INTERVAL, MAX_GEN_INTERVAL);
    }

    private void GenerateSuperBullet()
    {
        if (null == m_bulletRoot)
        {
            var rootObj = new GameObject("SuperBulletRoot");
            m_bulletRoot = rootObj.transform;
        }

        var prefab = ResourceMgr.instance.LoadRes<GameObject>("Bullet/supply_bullet");
        var obj = Object.Instantiate(prefab);
        obj.transform.SetParent(m_bulletRoot, false);
        // 随机一个初始坐标
        var randomPos = new Vector3(Random.Range(100, Screen.width - 100), Screen.height + 100, 5);
        obj.transform.position = Camera.main.ScreenToWorldPoint(randomPos);

        // supply_bullet 预制体默认没有下落脚本，这里补齐与炸弹一致的下落行为
        if (null == obj.GetComponent<SuperBullet>())
        {
            obj.AddComponent<SuperBullet>();
        }

        var rb = obj.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 0;
            rb.velocity = Vector2.zero;
        }
    }

    private float m_timer;
    private float m_targetInterval = MIN_GEN_INTERVAL;
    /// <summary>
    /// 最小生成间隔
    /// </summary>
    private const float MIN_GEN_INTERVAL = 2f;
    /// <summary>
    /// 最大生成间隔
    /// </summary>
    private const float MAX_GEN_INTERVAL = 10f;

    private Transform m_bulletRoot;
}
