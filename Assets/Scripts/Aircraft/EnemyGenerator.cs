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
        if (m_inBossMode && m_bossSpawned &&m_boss != null)
        {
            UpdateBossMovement();
        }

    }

    /// <summary>
    /// 重置游戏模式状态
    /// </summary>
    public void ResetGameMode()
    {
        m_inBossMode = false;
        m_inChannel = false;
        m_bossSpawned = false;
        m_channelTimer = 0f;
        m_bossTimer = 0f;
        m_timer = 0f;
        m_boss = null;
        
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


    // 添加阶段枚举
    public enum BossPhase
    {
        Phase1_Patrol,     // 第一阶段：巡逻移动
        Phase2_Attack,     // 第二阶段：发射弹幕
        Phase3_Final       // 第三阶段：狂暴模式（可选）
    }

    private BossPhase m_currentBossPhase = BossPhase.Phase1_Patrol;
    private float m_phase2HealthThreshold = 90f; // 当血量低于15时进入第二阶段
    private float m_bossFireTimer = 0f;
    private const float BOSS_FIRE_INTERVAL_PHASE1 = 1f; // 第一阶段射击间隔
    private const float BOSS_FIRE_INTERVAL_PHASE2 = 0.20f; // 第二阶段射击间隔
    private float m_currentFireInterval = BOSS_FIRE_INTERVAL_PHASE1;


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
        m_currentBossPhase = BossPhase.Phase1_Patrol;
        m_bossFireTimer = 0f;
        m_currentFireInterval = BOSS_FIRE_INTERVAL_PHASE1;
        
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
        
        // 计算目标位置：屏幕顶部往下四分之一的位置
        float screenHeight = Camera.main.orthographicSize * 2;
        float topY = Camera.main.transform.position.y + Camera.main.orthographicSize;
        m_bossTargetY = topY - (screenHeight / 4);
        
        m_bossSpawned = true;
        m_isBossMoving = true;
        
        // 初始化左右移动状态
        m_bossMoveDirection = 1f; // 1表示向右，-1表示向左
        m_isBossPatrolling = false; // 初始时还未开始巡逻
        
        Debug.Log($"Boss已生成！起始位置: {startPos}, 目标高度: {m_bossTargetY}");
    }

    /// <summary>
    /// 更新Boss移动
    /// </summary>
    private void UpdateBossMovement()
    {
        if (m_boss == null) return;

        // 检查阶段转换
        CheckBossPhase();
        
        Vector3 currentPos = m_boss.transform.position;
        
        // 第一阶段：垂直移动到目标位置
        if (m_isBossMoving)
        {
            float distanceToTarget = currentPos.y - m_bossTargetY;
            
            // 检查是否已经到达目标位置
            if (Mathf.Abs(distanceToTarget) < 0.01f)
            {
                currentPos.y = m_bossTargetY;
                m_isBossMoving = false;
                m_boss.moveSpeed = 0f; // 这里应该设置为0，表示垂直速度为0
                m_isBossPatrolling = true; // 开始巡逻
                m_boss.transform.position = currentPos;
                Debug.Log("Boss已到达目标位置，开始左右巡逻！");
                return;
            }
            
            // 如果还在目标位置上方，继续移动
            if (distanceToTarget > 0)
            {
                float moveAmount = m_boss.moveSpeed * Time.deltaTime;
                
                // 防止过度移动（越过目标）
                if (moveAmount >= distanceToTarget)
                {
                    currentPos.y = m_bossTargetY;
                    m_isBossMoving = false;
                    m_boss.moveSpeed = BOSS_PATROL_SPEED; // 设置巡逻速度
                    m_isBossPatrolling = true; // 开始巡逻
                }
                else
                {
                    currentPos.y -= moveAmount;
                }
                
                m_boss.transform.position = currentPos;
            }
            else
            {
                // 如果已经超过目标位置（理论上不应该发生），强制修正
                Debug.LogWarning("Boss位置异常，强制修正！");
                currentPos.y = m_bossTargetY;
                m_isBossMoving = false;
                m_boss.moveSpeed = BOSS_PATROL_SPEED; // 设置巡逻速度
                m_isBossPatrolling = true; // 开始巡逻
                m_boss.transform.position = currentPos;
            }
        }
        // 第二阶段：左右巡逻移动
        else if (m_isBossPatrolling)
        {
            // 如果移动速度为0，设置巡逻速度
            if (m_boss.moveSpeed == 0f)
            {
                m_boss.moveSpeedX = BOSS_PATROL_SPEED;
            }

            
            // 计算水平移动
            float moveAmount = m_boss.moveSpeedX * m_bossMoveDirection * Time.deltaTime;
            currentPos.x += moveAmount;
            
            // 检查屏幕边界
            float screenHalfWidth = GetScreenHalfWidth();
            float screenWidth = screenHalfWidth * 2;
            
            // 转换为屏幕坐标检查边界
            Vector3 screenPos = Camera.main.WorldToScreenPoint(currentPos);
            
            // 获取Boss碰撞体半径（如果没有碰撞体，使用一个估计值）
            float bossRadius = BOSS_RADIUS;
            
            // 计算世界坐标下的边界
            float leftBoundary = -screenHalfWidth + bossRadius;
            float rightBoundary = screenHalfWidth - bossRadius;
            
            // 检查是否到达屏幕边缘
            if (currentPos.x <= leftBoundary)
            {
                currentPos.x = leftBoundary;
                m_bossMoveDirection = 1f; // 向右转
                Debug.Log("Boss到达左边界，开始向右移动");
            }
            else if (currentPos.x >= rightBoundary)
            {
                currentPos.x = rightBoundary;
                m_bossMoveDirection = -1f; // 向左转
                Debug.Log("Boss到达右边界，开始向左移动");
            }
            
            m_boss.transform.position = currentPos;
        }

        // 更新Boss射击
        UpdateBossShooting();
    }

    /// <summary>
    /// 检查并更新Boss阶段
    /// </summary>
    private void CheckBossPhase()
    {
        if (m_boss == null) return;
        
        // 第一阶段：血量高于阈值
        if (m_boss.blood > m_phase2HealthThreshold && m_currentBossPhase != BossPhase.Phase1_Patrol)
        {
            m_currentBossPhase = BossPhase.Phase1_Patrol;
            m_currentFireInterval = BOSS_FIRE_INTERVAL_PHASE1;
            Debug.Log("Boss进入第一阶段：巡逻模式");
            
            // 可以改变颜色
            SpriteRenderer renderer = m_boss.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.color = Color.red;
            }
        }
        // 第二阶段：血量低于阈值
        else if (m_boss.blood <= m_phase2HealthThreshold && m_boss.blood > 0 && m_currentBossPhase != BossPhase.Phase2_Attack)
        {
            m_currentBossPhase = BossPhase.Phase2_Attack;
            m_currentFireInterval = BOSS_FIRE_INTERVAL_PHASE2;
            Debug.Log("Boss进入第二阶段：弹幕攻击模式！");
            
            // 改变颜色为紫色表示狂暴
            SpriteRenderer renderer = m_boss.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.color = new Color(0.8f, 0.2f, 0.8f); // 紫色
            }
            
            // 增加移动速度
            m_boss.moveSpeedX = BOSS_PATROL_SPEED * 3.0f;
        }
    }

    /// <summary>
    /// 更新Boss射击
    /// </summary>
    private void UpdateBossShooting()
    {
        if (m_boss == null || !m_bossSpawned) return;
        
        // 只有到达巡逻位置后才开始射击
        if (!m_isBossPatrolling || m_isBossMoving) return;
        
        m_bossFireTimer += Time.deltaTime;
        
        if (m_bossFireTimer >= m_currentFireInterval)
        {
            m_bossFireTimer = 0f;
            
            // 根据阶段选择不同的射击模式
            switch (m_currentBossPhase)
            {
                case BossPhase.Phase1_Patrol:
                    Phase1Shoot();
                    break;
                case BossPhase.Phase2_Attack:
                    Phase2Shoot();
                    break;
            }
        }
    }

    /// <summary>
    /// 第一阶段射击：简单的三发弹幕
    /// </summary>
    private void Phase1Shoot()
    {
        Vector3 bossPos = m_boss.transform.position;
        
        // 创建3个子弹，角度稍微分散
        int bulletCount = 3;
        float spreadAngle = 30f; // 总散布角度
        
        for (int i = 0; i < bulletCount; i++)
        {
            // 计算角度（中间子弹直下，两边分散）
            float angleOffset = (i - (bulletCount - 1) / 2f) * (spreadAngle / (bulletCount - 1));
            float currentAngle = 180f + angleOffset; // 180度是正下方
            
            // 使用你的子弹生成器生成子弹
            EnemyBulletGenerator.GenerateBossBullet(bossPos, currentAngle, m_currentBossPhase);
        }
    }

    /// <summary>
    /// 第二阶段射击：复杂的弹幕
    /// </summary>
    private void Phase2Shoot()
    {
        Vector3 bossPos = m_boss.transform.position;
        
        // 模式1：环形弹幕（保持原有）
        int circleCount = 8;
        for (int i = 0; i < circleCount; i++)
        {
            float angle = 360f * i / circleCount;
            EnemyBulletGenerator.GenerateBossBullet(bossPos, angle, m_currentBossPhase);
        }
        
        // 模式2：瞄准玩家的扇形弹幕（修改这里！）
        if (Random.value > 0.5f) // 50%概率发射瞄准弹幕
        {
            Vector3 playerPos = GameMgr.instance.GetPlayerPos();
            if (playerPos != Vector3.zero)
            {
                // 获取玩家方向向量
                Vector3 directionToPlayer = playerPos - bossPos;
                
                // 关键修改：使用Atan2计算角度（注意Unity的坐标系）
                // Atan2(y, x) 返回的角度是相对于X轴正方向
                // Unity中transform.up是Y轴正方向，需要调整
                float baseAngle = Mathf.Atan2(directionToPlayer.y, directionToPlayer.x) * Mathf.Rad2Deg;
                
                // Unity中0度是X轴正方向（右），但我们子弹的0度是Y轴正方向（上）
                // 所以需要旋转-90度
                float aimAngle = baseAngle - 90f;
                
                // 扇形分布
                int aimCount = 5;
                float aimSpread = 45f; // 扇形总角度
                
                for (int i = 0; i < aimCount; i++)
                {
                    // 计算每个子弹的角度偏移
                    float angleOffset = (i - (aimCount - 1) / 2f) * (aimSpread / (aimCount - 1));
                    float currentAngle = aimAngle + angleOffset;
                    
                    // 确保角度在0-360范围内
                    currentAngle = NormalizeAngle(currentAngle);
                    
                    EnemyBulletGenerator.GenerateBossBullet(bossPos, currentAngle, m_currentBossPhase);
                }
            }
        }
    }

    // 新增辅助方法：规范化角度到0-360度
    private float NormalizeAngle(float angle)
    {
        while (angle < 0) angle += 360f;
        while (angle >= 360f) angle -= 360f;
        return angle;
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
    private bool m_isBossPatrolling = false; // 新增：是否正在巡逻
    private float m_bossTargetY = 0;
    private float m_bossMoveDirection = 1f; // 新增：Boss移动方向（1右，-1左）

    private const float BOSS_ENTER_CHANCE = 1.00f;      // 每秒进入Boss模式概率
    private const float BOSS_DURATION = 30f;            // Boss模式持续时间
    private const float BOSS_SPAWN_DELAY = 1f;          // 进入Boss模式后延迟生成Boss
    private const int BOSS_HEALTH = 100;                 // Boss血量
    private const float BOSS_MOVE_SPEED = 1f;           // Boss下落速度
    private const float BOSS_PATROL_SPEED = 2f;         // 新增：Boss左右巡逻速度
    private const float BOSS_MARGIN = 2f;               // Boss距离屏幕边缘的留空
    private const float BOSS_RADIUS = 1.5f;             // 新增：Boss碰撞体半径（用于边界检测）
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
}