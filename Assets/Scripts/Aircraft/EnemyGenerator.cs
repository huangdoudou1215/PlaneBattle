using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 敌机生成器——支持"蛇形通道模式"和"Boss模式"
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
        if (!m_inChannel && !m_inBossMode)
        {
            // 每帧roll一次，概率可调
            if (Random.Range(0f, 1f) < CHANNEL_ENTER_CHANCE * Time.deltaTime)
                EnterChannelMode();
            // Boss模式进入概率
            if (Random.Range(0f, 1f) < BOSS_ENTER_CHANCE * Time.deltaTime)
                EnterBossMode();
        }
        else if (m_inChannel)
        {
            m_channelTimer += Time.deltaTime;
            if (m_channelTimer >= CHANNEL_DURATION)
                ExitChannelMode();
        }
        else if (m_inBossMode)
        {
            if (!m_bossSpawned && m_bossTimer >= BOSS_SPAWN_DELAY)
                SpawnBoss();
            
            m_bossTimer += Time.deltaTime;
            if (m_bossTimer >= BOSS_DURATION || (m_boss != null && m_boss.blood <= 0))
                ExitBossMode();
        }

        /*---------- 刷怪 ----------*/
        if (!m_inBossMode || !m_bossSpawned) // Boss模式下且Boss已生成时停止刷普通怪
        {
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

        /*---------- 更新Boss移动 ----------*/
        if (m_inBossMode && m_bossSpawned)
        {
            UpdateBossMovement();
        }

    }

    public void ClearAll()
    {
        m_reusePool.Clear();
        m_aliveEnemy.Clear();
        if (m_boss != null)
        {
            GameObject.Destroy(m_boss.gameObject);
            m_boss = null;
        }
    }

    public void KillAllEnemy()
    {
        for (int i = 0; i < m_aliveEnemy.Count; ++i)
            m_aliveEnemy[i].Explode();
        m_aliveEnemy.Clear();
        
        if (m_boss != null)
        {
            m_boss.Explode();
            m_boss = null;
        }
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
        m_inBossMode = false;
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
    #endregion

    #region Boss模式专属
    /// <summary>
    /// 进入Boss模式
    /// </summary>
    private void EnterBossMode()
    {
        m_inBossMode = true;
        m_inChannel = false;
        m_bossTimer = 0;
        m_bossSpawned = false;
        m_boss = null;
        
        Debug.Log("进入Boss模式！");
        
        // 清理现有的敌机
        KillAllEnemy();
    }

    /// <summary>
    /// 退出Boss模式
    /// </summary>
    private void ExitBossMode()
    {
        m_inBossMode = false;
        
        if (m_boss != null)
        {
            if (m_boss.blood > 0)
            {
                m_boss.Explode();
            }
            m_boss = null;
        }
        
        m_bossSpawned = false;
        Debug.Log("退出Boss模式！");
    }

    /// <summary>
    /// 生成Boss
    /// </summary>
    private void SpawnBoss()
    {
        // 创建Boss飞机（使用Enemy3类型）
        m_boss = (EnemyAircraft)AircraftFactory.CreateAircraft(AircraftType.Enemy3);
        
        // 设置Boss属性
        m_boss.blood = BOSS_HEALTH;
        m_boss.moveSpeed = BOSS_MOVE_SPEED;
        m_boss.ResetTimeToFire(-1); // 不发射子弹
        
        // 修改颜色为红色
        SpriteRenderer renderer = m_boss.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.color = Color.red;
        }
        
        // 随机起始位置（屏幕上方）
        float screenWidth = GetScreenHalfWidth() * 2;
        float randomX = Random.Range(-GetScreenHalfWidth() + BOSS_MARGIN, GetScreenHalfWidth() - BOSS_MARGIN);
        
        // 设置起始位置（屏幕上方）
        Vector3 startPos = Camera.main.ScreenToWorldPoint(new Vector3(
            (randomX + GetScreenHalfWidth()) / screenWidth * Screen.width,
            Screen.height + 1f,
            5));
        m_boss.transform.position = startPos;
        
        // 目标位置
        m_bossTargetY = 0;
        
        m_bossSpawned = true;
        m_isBossMoving = true;
        
        Debug.Log($"Boss已生成！起始位置: {startPos}, 目标高度: {m_bossTargetY}");
    }

    /// <summary>
    /// 更新Boss移动
    /// </summary>
    private void UpdateBossMovement()
    {
        if (m_boss == null || !m_isBossMoving) return;
        
        Vector3 currentPos = m_boss.transform.position;
        
        // 如果Boss还没到达目标位置
        if (currentPos.y > m_bossTargetY)
        {
            // 缓慢下落到目标位置
            currentPos.y -= m_boss.moveSpeed * Time.deltaTime;
            
            // 如果到达或超过目标位置
            if (currentPos.y <= m_bossTargetY)
            {
                currentPos.y = m_bossTargetY;
                m_isBossMoving = false;
                m_boss.moveSpeed = 0f; // 静止不动
                Debug.Log("Boss已到达目标位置！");
            }
            
            m_boss.transform.position = currentPos;
        }
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

    /*---------- Boss模式参数 ----------*/
    private bool m_inBossMode = false;
    private bool m_bossSpawned = false;
    private float m_bossTimer = 0;
    private EnemyAircraft m_boss = null;
    private bool m_isBossMoving = false;
    private float m_bossTargetY = 0;

    private const float BOSS_ENTER_CHANCE = 1.00f;      // 每秒进入Boss模式概率
    private const float BOSS_DURATION = 30f;            // Boss模式持续时间
    private const float BOSS_SPAWN_DELAY = 1f;          // 进入Boss模式后延迟生成Boss
    private const int BOSS_HEALTH = 30;                 // Boss血量
    private const float BOSS_MOVE_SPEED = 1f;           // Boss下落速度
    private const float BOSS_MARGIN = 2f;               // Boss距离屏幕边缘的留空
    #endregion

    #region 公共方法
    /// <summary>
    /// 获取屏幕半宽（世界坐标）
    /// </summary>
    private float GetScreenHalfWidth()
    {
        return Camera.main.orthographicSize * Camera.main.aspect;
    }

    /// <summary>
    /// 获取当前模式信息
    /// </summary>
    public string GetCurrentMode()
    {
        if (m_inBossMode) return "Boss模式";
        if (m_inChannel) return "蛇形通道模式";
        return "普通模式";
    }

    /// <summary>
    /// 获取Boss信息（如果存在）
    /// </summary>
    public EnemyAircraft GetBoss()
    {
        return m_boss;
    }
    #endregion

    #region 调试绘制
    public void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        
        if (m_inChannel)
        {
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
        
        if (m_inBossMode && m_boss != null)
        {
            // 绘制Boss位置和目标位置
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(m_boss.transform.position, 0.5f);
            
            // 绘制目标位置线
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(
                new Vector3(-GetScreenHalfWidth(), m_bossTargetY, 0),
                new Vector3(GetScreenHalfWidth(), m_bossTargetY, 0)
            );
        }
    }
    #endregion
}