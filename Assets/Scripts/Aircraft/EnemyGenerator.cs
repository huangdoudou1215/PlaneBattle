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


            // 🆕 检查 Boss 是否死亡
            if (m_boss != null && m_boss.blood <= 0)
            {
                // Boss 死亡，隐藏血条
                HideBossHealthBar();
            }

            
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

        // 🆕 隐藏血条
        HideBossHealthBar();
        
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

    public void KillAllEnemy(bool includeBoss = true)
    {
        for (int i = 0; i < m_aliveEnemy.Count; ++i)
            m_aliveEnemy[i].Explode();
        m_aliveEnemy.Clear();
        
        if (includeBoss && m_boss != null)
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
        enemy.isBoss = false;
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
        enemy.isBoss = false;
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

    #region Boss血条相关

    private BossHealthBarController bossHealthBar;
    private bool healthBarInitialized = false;

    /// <summary>
    /// 初始化血条控制器
    /// </summary>
    private void InitializeHealthBar()
    {
        if (healthBarInitialized) return;
        
        // 通过 GameMgr 或其他方式获取血条控制器
        // 这里假设 GameMgr 有获取血条的方法
        var mainGamePanel = GameObject.FindObjectOfType<MainGamePanel>();
        if (mainGamePanel != null)
        {
            bossHealthBar = mainGamePanel.GetComponentInChildren<BossHealthBarController>(true);
            if (bossHealthBar == null)
            {
                Debug.LogWarning("未找到 BossHealthBarController，将在 MainGamePanel 下创建");
                // 可以在这里动态创建血条
            }
        }
        
        healthBarInitialized = true;
    }

    /// <summary>
    /// 显示Boss血条
    /// </summary>
    private void ShowBossHealthBar()
    {
        InitializeHealthBar();
        
        if (bossHealthBar != null && m_boss != null)
        {
            bossHealthBar.ShowBossHealthBar(m_boss);
            Debug.Log("显示 Boss 血条");
        }
    }

    /// <summary>
    /// 隐藏Boss血条
    /// </summary>
    private void HideBossHealthBar()
    {
        if (bossHealthBar != null)
        {
            bossHealthBar.HideHealthBar();
            Debug.Log("隐藏 Boss 血条");
        }
    }

    #endregion


    #region Boss模式专属


    // 添加阶段枚举
    public enum BossPhase
    {
        Phase1_Patrol,     // 第一阶段：巡逻移动
        Phase2_Attack,     // 第二阶段：发射弹幕
        Phase3_Charge      // 第三阶段：冲锋追击（不再发弹幕）
    }

    private BossPhase m_currentBossPhase = BossPhase.Phase1_Patrol;
    private float m_phase2HealthThreshold = 90f;
    private float m_phase3HealthThreshold = 20f;
    private float m_bossFireTimer = 0f;
    private const float BOSS_FIRE_INTERVAL_PHASE1 = 1f;
    private const float BOSS_FIRE_INTERVAL_PHASE2 = 0.40f;
    private float m_currentFireInterval = BOSS_FIRE_INTERVAL_PHASE1;
    private int m_phase2PatternIndex = 0;

    private bool m_isBossCharging = false;
    private Vector3 m_chargeDirection = Vector3.down;
    private Vector3 m_chargeTargetPos = Vector3.zero;
    private float m_chargeTimer = 0f;
    private float m_chargeCooldownTimer = 0f;
    private float m_afterimageTimer = 0f;





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
        m_phase2PatternIndex = 0;
        m_isBossCharging = false;
        m_chargeTimer = 0f;
        m_chargeCooldownTimer = 0f;
        m_afterimageTimer = 0f;
        
        Debug.Log("进入Boss模式！");
        
        // 清理现有的敌机
        KillAllEnemy();
    }

    /// <summary>
    /// 退出Boss模式
    /// </summary>
    private void ExitBossMode()
    {

        // 🆕 先记录 Boss 是否存活
        bool bossWasAlive = m_boss != null && m_boss.blood > 0;

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

        // 🆕 隐藏 Boss 血条
        HideBossHealthBar();

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
        m_boss.isBoss = true;
        m_boss.moveSpeed = BOSS_MOVE_SPEED;
        m_boss.moveSpeedX = BOSS_PATROL_SPEED;
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

        // 🆕 显示 Boss 血条
        ShowBossHealthBar();

        
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

        if (m_currentBossPhase == BossPhase.Phase3_Charge)
        {
            UpdateBossChargeMovement();
            UpdateBossShooting();
            return;
        }
        
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
            if (m_boss.moveSpeedX == 0f)
            {
                m_boss.moveSpeedX = BOSS_PATROL_SPEED;
            }

            
            // 计算水平移动
            float moveAmount = m_boss.moveSpeedX * m_bossMoveDirection * Time.deltaTime;
            currentPos.x += moveAmount;
            
            // 检查屏幕边界
            float screenHalfWidth = GetScreenHalfWidth();
            
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

        if (m_boss.blood <= 0) return;
        if (m_currentBossPhase == BossPhase.Phase3_Charge && m_boss.blood <= m_phase3HealthThreshold) return;

        // 第三阶段：低血量冲锋（不再发弹幕）
        if (m_boss.blood <= m_phase3HealthThreshold && m_currentBossPhase != BossPhase.Phase3_Charge)
        {
            m_currentBossPhase = BossPhase.Phase3_Charge;
            m_currentFireInterval = 9999f;
            m_bossFireTimer = 0f;
            m_isBossMoving = false;
            m_isBossPatrolling = false;
            m_isBossCharging = false;
            m_chargeTargetPos = m_boss.transform.position;
            m_chargeCooldownTimer = BOSS_CHARGE_COOLDOWN;
            m_afterimageTimer = 0f;
            m_boss.moveSpeed = 0f;
            m_boss.moveSpeedX = 0f;

            SpriteRenderer renderer = m_boss.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.color = new Color(1.0f, 0.55f, 0.2f);
            }

            Debug.Log("Boss进入第三阶段：冲锋模式（停止弹幕）！");
            return;
        }

        // 第二阶段：低于90%血量，开始多模式弹幕
        if (m_boss.blood <= m_phase2HealthThreshold && m_boss.blood > m_phase3HealthThreshold && m_currentBossPhase != BossPhase.Phase2_Attack)
        {
            m_currentBossPhase = BossPhase.Phase2_Attack;
            m_currentFireInterval = BOSS_FIRE_INTERVAL_PHASE2;
            m_isBossPatrolling = true;
            m_boss.moveSpeedX = BOSS_PATROL_SPEED * 2.4f;
            m_phase2PatternIndex = 0;
            Debug.Log("Boss进入第二阶段：多模式弹幕攻击！");
            
            SpriteRenderer renderer = m_boss.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.color = new Color(0.8f, 0.2f, 0.8f);
            }
            return;
        }

        // 第一阶段：血量高于90%
        if (m_boss.blood > m_phase2HealthThreshold && m_currentBossPhase != BossPhase.Phase1_Patrol)
        {
            m_currentBossPhase = BossPhase.Phase1_Patrol;
            m_currentFireInterval = BOSS_FIRE_INTERVAL_PHASE1;
            m_isBossPatrolling = true;
            m_boss.moveSpeedX = BOSS_PATROL_SPEED;
            Debug.Log("Boss进入第一阶段：巡逻模式");
            
            // 可以改变颜色
            SpriteRenderer renderer = m_boss.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.color = Color.red;
            }
        }
    }

    /// <summary>
    /// 更新Boss射击
    /// </summary>
    private void UpdateBossShooting()
    {
        if (m_boss == null || !m_bossSpawned) return;
        if (m_currentBossPhase == BossPhase.Phase3_Charge) return;
        
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

        switch (m_phase2PatternIndex % 4)
        {
            case 0:
                FireAimedFan(bossPos);
                break;
            case 1:
                FireSpiralBurst(bossPos);
                break;
            case 2:
                FireCrossBurst(bossPos);
                break;
            default:
                FireWaveBarrage(bossPos);
                break;
        }

        m_phase2PatternIndex++;
    }

    private void FireAimedFan(Vector3 bossPos)
    {
        Vector3 playerPos = GameMgr.instance.GetPlayerPos();
        if (playerPos == Vector3.zero)
        {
            playerPos = bossPos + Vector3.down;
        }

        Vector3 directionToPlayer = playerPos - bossPos;
        float baseAngle = Mathf.Atan2(directionToPlayer.y, directionToPlayer.x) * Mathf.Rad2Deg - 90f;

        int bulletCount = 7;
        float spread = 70f;
        for (int i = 0; i < bulletCount; i++)
        {
            float offset = (i - (bulletCount - 1) * 0.5f) * (spread / (bulletCount - 1));
            float angle = NormalizeAngle(baseAngle + offset);
            EnemyBulletGenerator.GenerateBossBulletCustom(
                bossPos, angle, 3.2f, new Color(0.85f, 0.25f, 0.9f), false, false);
        }
    }

    private void FireSpiralBurst(Vector3 bossPos)
    {
        float baseAngle = NormalizeAngle(m_phase2PatternIndex * 22f);
        int bulletCount = 10;
        for (int i = 0; i < bulletCount; i++)
        {
            float angle = NormalizeAngle(baseAngle + i * (360f / bulletCount));
            EnemyBulletGenerator.GenerateBossBulletCustom(
                bossPos, angle, 2.6f, new Color(0.95f, 0.35f, 0.55f), true, false);
        }
    }

    private void FireCrossBurst(Vector3 bossPos)
    {
        float[] angles = { 160f, 180f, 200f, 225f, 135f };
        for (int i = 0; i < angles.Length; i++)
        {
            EnemyBulletGenerator.GenerateBossBulletCustom(
                bossPos, angles[i], 3.0f, new Color(0.35f, 0.8f, 1f), false, false);
        }
    }

    private void FireWaveBarrage(Vector3 bossPos)
    {
        int bulletCount = 8;
        float startAngle = 145f;
        float endAngle = 215f;
        for (int i = 0; i < bulletCount; i++)
        {
            float t = bulletCount == 1 ? 0f : (float)i / (bulletCount - 1);
            float angle = Mathf.Lerp(startAngle, endAngle, t);
            EnemyBulletGenerator.GenerateBossBulletCustom(
                bossPos,
                angle,
                2.8f,
                new Color(1f, 0.75f, 0.25f),
                false,
                false);
        }
    }

    private void UpdateBossChargeMovement()
    {
        if (m_boss == null) return;
        m_boss.moveSpeed = 0f;
        m_boss.moveSpeedX = 0f;

        Vector3 currentPos = m_boss.transform.position;
        float camX = Camera.main.transform.position.x;
        float camY = Camera.main.transform.position.y;
        float leftBoundary = camX - GetScreenHalfWidth() + BOSS_RADIUS;
        float rightBoundary = camX + GetScreenHalfWidth() - BOSS_RADIUS;
        float topBoundary = Camera.main.transform.position.y + Camera.main.orthographicSize - BOSS_RADIUS * 0.5f;
        float bottomBoundary = camY - Camera.main.orthographicSize + BOSS_RADIUS * 1.2f;

        if (!m_isBossCharging)
        {
            m_chargeCooldownTimer += Time.deltaTime;
            if (m_chargeCooldownTimer >= BOSS_CHARGE_COOLDOWN)
            {
                Vector3 playerPos = GameMgr.instance.GetPlayerPos();
                if (playerPos == Vector3.zero)
                {
                    playerPos = currentPos + Vector3.down * 2f;
                }

                m_chargeTargetPos = new Vector3(
                    Mathf.Clamp(playerPos.x, leftBoundary, rightBoundary),
                    Mathf.Clamp(playerPos.y, bottomBoundary, topBoundary),
                    currentPos.z);

                Vector3 targetDir = (m_chargeTargetPos - currentPos).normalized;
                m_chargeDirection = targetDir.sqrMagnitude > 0.0001f ? targetDir : Vector3.down;
                m_isBossCharging = true;
                m_chargeTimer = 0f;
                m_afterimageTimer = BOSS_AFTERIMAGE_INTERVAL;
                m_chargeCooldownTimer = 0f;
            }
            return;
        }

        m_chargeTimer += Time.deltaTime;
        m_afterimageTimer += Time.deltaTime;

        currentPos = Vector3.MoveTowards(currentPos, m_chargeTargetPos, BOSS_CHARGE_SPEED * Time.deltaTime);

        m_boss.transform.position = currentPos;

        if (m_afterimageTimer >= BOSS_AFTERIMAGE_INTERVAL)
        {
            m_afterimageTimer = 0f;
            SpawnBossAfterimage();
        }

        if (m_chargeTimer >= BOSS_CHARGE_DURATION || Vector3.Distance(currentPos, m_chargeTargetPos) <= 0.05f)
        {
            m_isBossCharging = false;
            m_chargeTimer = 0f;
        }
    }

    private void SpawnBossAfterimage()
    {
        if (m_boss == null) return;

        SpriteRenderer bossRenderer = m_boss.GetComponent<SpriteRenderer>();
        if (bossRenderer == null || bossRenderer.sprite == null) return;

        GameObject afterimage = new GameObject("BossAfterimage");
        afterimage.transform.position = m_boss.transform.position;
        afterimage.transform.rotation = m_boss.transform.rotation;
        afterimage.transform.localScale = m_boss.transform.localScale;

        SpriteRenderer renderer = afterimage.AddComponent<SpriteRenderer>();
        renderer.sprite = bossRenderer.sprite;
        renderer.sortingLayerID = bossRenderer.sortingLayerID;
        renderer.sortingOrder = bossRenderer.sortingOrder - 1;
        renderer.color = new Color(1f, 0.5f, 0.2f, 0.45f);

        BossAfterimage fade = afterimage.AddComponent<BossAfterimage>();
        fade.Init(renderer, BOSS_AFTERIMAGE_LIFETIME);
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
    private const float BOSS_CHARGE_SPEED = 8.5f;       // 冲锋速度
    private const float BOSS_CHARGE_DURATION = 1.1f;    // 单次冲锋时长
    private const float BOSS_CHARGE_COOLDOWN = 0.45f;   // 冲锋间隔
    private const float BOSS_AFTERIMAGE_INTERVAL = 0.06f;
    private const float BOSS_AFTERIMAGE_LIFETIME = 0.28f;
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

/// <summary>
/// Boss 冲锋残影：短时间淡出后销毁。
/// </summary>
public class BossAfterimage : MonoBehaviour
{
    private SpriteRenderer m_renderer;
    private float m_lifetime = 0.25f;
    private float m_elapsed = 0f;
    private Color m_initialColor = Color.white;

    public void Init(SpriteRenderer renderer, float lifetime)
    {
        m_renderer = renderer;
        m_lifetime = Mathf.Max(0.01f, lifetime);
        m_elapsed = 0f;
        if (m_renderer != null)
        {
            m_initialColor = m_renderer.color;
        }
    }

    private void Update()
    {
        m_elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(m_elapsed / m_lifetime);

        if (m_renderer != null)
        {
            Color c = m_initialColor;
            c.a = Mathf.Lerp(m_initialColor.a, 0f, t);
            m_renderer.color = c;
        }

        if (m_elapsed >= m_lifetime)
        {
            Destroy(gameObject);
        }
    }
}
