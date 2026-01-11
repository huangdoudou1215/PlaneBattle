using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 敌机生成器——支持"蛇形通道模式"
/// </summary>
public class EnemyGenerator
{
    #region 对外保持原接口不动
    public void UpdateRandomEnemys()
    {
        m_enemyRandom.Clear();
        foreach (var enemy in GameMgr.instance.Level.Enemies)
            m_enemyRandom.Add(enemy, enemy.Weight);
    }

    public void Update()
    {
        /*---------- 模式切换检测 ----------*/
        if (!m_inChannel)
        {
            // 每帧roll一次，概率可调
            if (Random.Range(0f, 1f) < CHANNEL_ENTER_CHANCE * Time.deltaTime)
                EnterChannelMode();
        }
        else
        {
            m_channelTimer += Time.deltaTime;
            if (m_channelTimer >= CHANNEL_DURATION)
                ExitChannelMode();
        }

        /*---------- 刷怪 ----------*/
        m_timer += Time.deltaTime;
        float spawnInterval = m_inChannel ? CHANNEL_SPAWN_INTERVAL   // 通道模式刷更快
                                          : GameMgr.instance.Level.EnemySpawnTime;
        if (m_timer >= spawnInterval)
        {
            if (m_inChannel) 
                ChannelSpawnEnemy();
            else 
                RandomGenerateEnemy();

            m_timer = 0;
        }
    }

    public void ClearAll()
    {
        m_reusePool.Clear();
        m_aliveEnemy.Clear();
    }

    public void KillAllEnemy()
    {
        for (int i = 0; i < m_aliveEnemy.Count; ++i)
            m_aliveEnemy[i].Explode();
        m_aliveEnemy.Clear();
    }
    #endregion

    #region 原有随机生成逻辑（未改动）
    private void RandomGenerateEnemy()
    {
        EnemyAircraft enemy = null;
        var config = m_enemyRandom.Next();
        var aircraftType = (AircraftType)config.Index;
        if (m_reusePool.ContainsKey(aircraftType) && m_reusePool[aircraftType].Count > 0)
        {
            enemy = m_reusePool[aircraftType].Dequeue();
            enemy.ActiveSelf(true);
        }
        else
        {
            enemy = (EnemyAircraft)AircraftFactory.CreateAircraft((AircraftType)config.Index);
            enemy.backToPoolAction = () =>
            {
                if (!m_reusePool.ContainsKey(aircraftType))
                    m_reusePool[aircraftType] = new Queue<EnemyAircraft>();
                m_reusePool[aircraftType].Enqueue(enemy);

                if (m_aliveEnemy.Contains(enemy))
                    m_aliveEnemy.Remove(enemy);
            };
        }
        enemy.blood = config.Blood;
        enemy.moveSpeed = Random.Range(config.MinSpeed, config.MaxSpeed);
        enemy.ResetTimeToFire(1);
        enemy.RandomStartPos();
        if (!m_aliveEnemy.Contains(enemy))
            m_aliveEnemy.Add(enemy);
    }
    #endregion

    #region 蛇形通道模式专属
    private void EnterChannelMode()
    {
        m_inChannel = true;
        m_channelTimer = 0;
        m_currentSide = ChannelSide.Left; // 从左边开始
        m_channelOffset = 0f;             // 初始偏移
        
        Debug.Log("进入蛇形通道模式！");
    }

    private void ExitChannelMode()
    {
        m_inChannel = false;
        Debug.Log("退出蛇形通道模式！");
    }

    /// <summary>
    /// 蛇形通道生成敌机
    /// </summary>
    private void ChannelSpawnEnemy()
    {
        // 生成一对敌机（左右各一个）
        SpawnEnemyPair();
        
        // 更新通道位置
        UpdateChannelPosition();
    }

    /// <summary>
    /// 生成一对敌机
    /// </summary>
    private void SpawnEnemyPair()
    {
        float screenHalfWidth = GetScreenHalfWidth();
        
        // 计算当前生成位置的X坐标
        float currentX = m_channelOffset;
        
        // 限制在屏幕范围内
        if (Mathf.Abs(currentX) > screenHalfWidth - EDGE_MARGIN)
        {
            // 如果接近边缘，反向
            m_channelDirection *= -1;
            currentX = Mathf.Clamp(currentX, -screenHalfWidth + EDGE_MARGIN, screenHalfWidth - EDGE_MARGIN);
        }
        
        // 生成左侧敌机（相对于通道位置向左偏移）
        SpawnSingleEnemy(currentX - CHANNEL_WIDTH * 0.5f);
        
        // 生成右侧敌机（相对于通道位置向右偏移）
        SpawnSingleEnemy(currentX + CHANNEL_WIDTH * 0.5f);
    }

    /// <summary>
    /// 生成单个敌机
    /// </summary>
    private void SpawnSingleEnemy(float xPos)
    {
        EnemyAircraft enemy = null;
        AircraftType miniType = AircraftType.Enemy1; // 最小敌机类型
        
        if (m_reusePool.ContainsKey(miniType) && m_reusePool[miniType].Count > 0)
        {
            enemy = m_reusePool[miniType].Dequeue();
            enemy.ActiveSelf(true);
        }
        else
        {
            enemy = (EnemyAircraft)AircraftFactory.CreateAircraft(miniType);
            enemy.backToPoolAction = () =>
            {
                if (!m_reusePool.ContainsKey(miniType))
                    m_reusePool[miniType] = new Queue<EnemyAircraft>();
                m_reusePool[miniType].Enqueue(enemy);
                if (m_aliveEnemy.Contains(enemy))
                    m_aliveEnemy.Remove(enemy);
            };
        }

        // 固定最小飞机数值
        enemy.blood = 1;
        enemy.moveSpeed = 2f;
        enemy.ResetTimeToFire(1);

        // 设置位置
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(
            (xPos + GetScreenHalfWidth()) / (GetScreenHalfWidth() * 2) * Screen.width, 
            Screen.height, 
            5));
        
        enemy.transform.position = worldPos;

        if (!m_aliveEnemy.Contains(enemy))
            m_aliveEnemy.Add(enemy);
    }

    /// <summary>
    /// 更新通道位置
    /// </summary>
    private void UpdateChannelPosition()
    {
        // 每次更新通道偏移量，形成斜线移动
        m_channelOffset += m_channelDirection * CHANNEL_MOVE_SPEED;
        
        float screenHalfWidth = GetScreenHalfWidth();
        
        // 检查边界，到达边界时反向
        if (Mathf.Abs(m_channelOffset) > screenHalfWidth - EDGE_MARGIN)
        {
            m_channelDirection *= -1;
            m_channelOffset = Mathf.Clamp(m_channelOffset, -screenHalfWidth + EDGE_MARGIN, screenHalfWidth - EDGE_MARGIN);
        }
    }

    /// <summary>
    /// 获取屏幕半宽（世界坐标）
    /// </summary>
    private float GetScreenHalfWidth()
    {
        return Camera.main.orthographicSize * Camera.main.aspect;
    }
    #endregion

    #region 字段/常量
    private float m_timer;

    private readonly WeightedRandom<EnemyConfig> m_enemyRandom = new WeightedRandom<EnemyConfig>();
    private readonly Dictionary<AircraftType, Queue<EnemyAircraft>> m_reusePool = new Dictionary<AircraftType, Queue<EnemyAircraft>>();
    private readonly List<EnemyAircraft> m_aliveEnemy = new List<EnemyAircraft>();

    /*---------- 蛇形通道模式参数 ----------*/
    private bool m_inChannel = false;
    private float m_channelTimer = 0;
    private float m_channelOffset = 0f;    // 通道中心偏移量（世界坐标）
    private float m_channelDirection = 1f; // 移动方向：1右，-1左
    private ChannelSide m_currentSide = ChannelSide.Left;

    private const float CHANNEL_ENTER_CHANCE = 0.1f;    // 每秒进入通道概率
    private const float CHANNEL_DURATION = 15f;         // 持续时间
    private const float CHANNEL_WIDTH = 2.0f;           // 通道宽度（左右敌机间距）
    private const float CHANNEL_MOVE_SPEED = 0.15f;      // 通道平移速度
    private const float CHANNEL_SPAWN_INTERVAL = 0.17f;  // 通道模式刷怪间隔
    private const float EDGE_MARGIN = 1.0f;             // 屏幕边缘留空

    private enum ChannelSide
    {
        Left,
        Right
    }
    #endregion

    #region 调试绘制
    public void OnDrawGizmos()
    {
        if (!Application.isPlaying || !m_inChannel) return;
        
        // 绘制当前通道位置
        Vector3 leftPos = new Vector3(m_channelOffset - CHANNEL_WIDTH * 0.5f, 0, 0);
        Vector3 rightPos = new Vector3(m_channelOffset + CHANNEL_WIDTH * 0.5f, 0, 0);
        
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(leftPos, 0.2f);
        Gizmos.DrawWireSphere(rightPos, 0.2f);
        
        // 绘制移动方向指示器
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(new Vector3(m_channelOffset, 1, 0), 
                        new Vector3(m_channelOffset + m_channelDirection, 1, 0));
    }
    #endregion
}