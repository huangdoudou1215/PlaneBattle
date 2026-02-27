using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 游戏管理器
/// </summary>
public class GameMgr
{
    private const int BASE_NEXT_LEVEL_EXP = 20;

    /// <summary>
    /// 游戏主入口函数
    /// </summary>
    public void Main()
    {
        // 读取配置
        ConfigMgr.instance.Load();


        gameState = GameState.Ready;
        // 显示游戏开始界面
        PanelMgr.instance.ShowPanel<StartGamePanel>();
    }

    /// <summary>
    /// 开始游戏
    /// </summary>
    public void StartGame()
    {
        // 初始从第一关开始
        Level = ConfigMgr.instance.gameConfig.GetLevelConfig(1);
        // 初始化得分
        Score = 0;
        // 初始化核弹
        BombCnt = 0;
        // 初始化生命数（两条命）
        LifeCnt = 2;
        // 初始化成长系统
        ResetProgression();


        // 关闭开始游戏界面
        PanelMgr.instance.HidePanel<StartGamePanel>();

        // 显示游戏战斗界面
        PanelMgr.instance.ShowPanel<MainGamePanel>();

        // 创建主角飞机
        player = AircraftFactory.CreateAircraft(AircraftType.Player);

        // 初始化核弹生成器
        m_superBombGenerator.Init();
        // 初始化子弹补给生成器
        m_superBulletGenerator.Init();
        // 初始化经验球生成器
        m_experienceOrbGenerator.Init();


        gameState = GameState.Playing;
    }

    /// <summary>
    /// 开始双人模式（Mirror）。
    /// </summary>
    public void StartMultiplayerMode()
    {
        gameState = GameState.Ready;

        // 双人模式与单人玩法解耦，不初始化关卡和敌机系统。
        PanelMgr.instance.HidePanel<StartGamePanel>();
        MultiplayerModeRuntime.Enter();
    }

    /// <summary>
    /// 游戏结束
    /// </summary>
    public void GameOver()
    {
        gameState = GameState.End;
        PanelMgr.instance.ShowPanel<GameOverPanel>();
    }

    /// <summary>
    /// 暂停游戏
    /// </summary>
    public void PauseGame()
    {
        gameState = GameState.Pause;
        // 显示暂停界面
        PanelMgr.instance.ShowPanel<PausePanel>();
    }

    /// <summary>
    /// 继续游戏
    /// </summary>
    public void ContinueGame()
    {
        gameState = GameState.Playing;
    }

    /// <summary>
    /// 清理飞机和子弹物体
    /// </summary>
    public void ClearObjs()
    {
        if (null != player)
            player.DestroySelf();
        // 清空所有飞机
        AircraftFactory.DestroyFactoryRoot();
        m_enemyGenerator.ClearAll();
        // 清空所有子弹
        EnemyBulletGenerator.CLear();
        // 清理道具根节点
        m_superBombGenerator.DestroyRoot();
        m_superBulletGenerator.DestroyRoot();
        m_experienceOrbGenerator.DestroyRoot();
    }

    /// <summary>
    /// 重新开始游戏
    /// </summary>
    public void RestartGame()
    {
        ClearObjs();

        // 重置游戏模式状态
        if (m_enemyGenerator != null)
        {
            // 调用EnemyGenerator的重置方法
            m_enemyGenerator.ResetGameMode();
        }
        StartGame();
    }

    /// <summary>
    /// 返回主菜单
    /// </summary>
    public void BackToHomePanel()
    {
        ClearObjs();

        // 关闭游戏战斗界面
        PanelMgr.instance.HidePanel<MainGamePanel>();
        // 显示开始游戏界面
        PanelMgr.instance.ShowPanel<StartGamePanel>();
    }

    public void Update()
    {
        if (GameState.Playing == gameState)
        {
            m_enemyGenerator.Update();
            m_superBombGenerator.Update();
            m_superBulletGenerator.Update();
        }
    }

    /// <summary>
    /// 全屏炸机
    /// </summary>
    public void KillAllEnemy()
    {
        if (BombCnt <= 0) return;

        --BombCnt;
        m_enemyGenerator.KillAllEnemy(false);
        EnemyBulletGenerator.CLear();
    }

    public void TryDropExperienceOrb(Vector3 worldPos, bool isBoss)
    {
        int expValue = isBoss ? 30 : 5;
        m_experienceOrbGenerator.Generate(worldPos, expValue);
    }

    public void AddExperience(int amount)
    {
        if (amount <= 0) return;

        m_currentExp += amount;
        bool levelChanged = false;
        while (m_currentExp >= m_nextLevelExp)
        {
            m_currentExp -= m_nextLevelExp;
            ++m_playerLevel;
            ++m_pendingUpgradeCount;
            m_nextLevelExp = CalcNextLevelExp(m_playerLevel);
            levelChanged = true;
        }

        EventDispatcher.instance.DispatchEvent(EventDef.EVENT_UPDATE_EXP);
        if (levelChanged)
        {
            EventDispatcher.instance.DispatchEvent(EventDef.EVENT_LEVEL_UP_AVAILABLE);
        }
    }

    public List<UpgradeChoice> BuildUpgradeChoices(int count = 3)
    {
        var allTypes = (UpgradeType[])System.Enum.GetValues(typeof(UpgradeType));
        var pool = new List<UpgradeType>(allTypes);
        for (int i = 0; i < pool.Count; ++i)
        {
            int randomIndex = Random.Range(i, pool.Count);
            UpgradeType tmp = pool[i];
            pool[i] = pool[randomIndex];
            pool[randomIndex] = tmp;
        }

        int pickCount = Mathf.Clamp(count, 1, pool.Count);
        var result = new List<UpgradeChoice>(pickCount);
        for (int i = 0; i < pickCount; ++i)
        {
            result.Add(CreateUpgradeChoice(pool[i]));
        }
        return result;
    }

    public bool ApplyUpgrade(UpgradeType type)
    {
        var playerAircraft = player as PlayerAircraft;
        if (playerAircraft == null) return false;

        switch (type)
        {
            case UpgradeType.FireRate:
                playerAircraft.AddFireRateUpgrade(0.01f);
                break;
            case UpgradeType.ParallelShot:
                playerAircraft.AddParallelBulletUpgrade();
                break;
            case UpgradeType.MoveSpeed:
                playerAircraft.AddMoveSpeedUpgrade(0.8f);
                break;
            case UpgradeType.BulletPower:
                playerAircraft.AddBulletPowerUpgrade();
                break;
            case UpgradeType.Shield:
                playerAircraft.AddShieldUpgrade();
                break;
            default:
                return false;
        }

        if (m_pendingUpgradeCount > 0)
        {
            --m_pendingUpgradeCount;
        }
        EventDispatcher.instance.DispatchEvent(EventDef.EVENT_LEVEL_UP_AVAILABLE);
        return true;
    }

    /// <summary>
    /// 获取主角飞机的坐标
    /// </summary>
    /// <returns></returns>
    public Vector3 GetPlayerPos()
    {
        if (null != player && null != player.gameObject)
        {
            return player.transform.position;
        }
        return Vector3.zero;
    }

    private EnemyGenerator m_enemyGenerator = new EnemyGenerator();
    private SuperBombGenerator m_superBombGenerator = new SuperBombGenerator();
    private SuperBulletGenerator m_superBulletGenerator = new SuperBulletGenerator();
    private ExperienceOrbGenerator m_experienceOrbGenerator = new ExperienceOrbGenerator();

    public BaseAircraft player;

    /// <summary>
    /// 游戏状态
    /// </summary>
    public GameState gameState = GameState.Ready;

    /// <summary>
    /// 获取或设置当前关卡配置。
    /// </summary>
    public LevelConfig Level
    {
        get { return m_level; }
        set
        {
            m_level = value;
            m_nextLevel = ConfigMgr.instance.gameConfig.GetLevelConfig(m_level.ID + 1);
            m_enemyGenerator.UpdateRandomEnemys();
        }
    }

    /// <summary>
    /// 获取或设置游戏的当前分数。
    /// </summary>
    public int Score
    {
        get { return m_score; }
        set
        {
            m_score = value;
            if (m_nextLevel != null && m_nextLevel.Score <= value)
                Level = m_nextLevel;

            // 抛出分数更新时间
            EventDispatcher.instance.DispatchEvent(EventDef.EVENT_UPDATE_SCORE);
        }
    }

    /// <summary>
    /// 核弹数量
    /// </summary>
    public int BombCnt
    {
        get { return m_bombCnt; }
        set
        {
            m_bombCnt = value;
            EventDispatcher.instance.DispatchEvent(EventDef.EVENT_UPDATE_BOMB_CNT);
        }
    }

    /// <summary>
    /// 玩家生命数
    /// </summary>
    public int LifeCnt
    {
        get { return m_lifeCnt; }
        set
        {
            m_lifeCnt = value;
            EventDispatcher.instance.DispatchEvent(EventDef.EVENT_UPDATE_LIFE_CNT);
        }
    }

    public int PlayerLevel
    {
        get { return m_playerLevel; }
    }

    public int CurrentExp
    {
        get { return m_currentExp; }
    }

    public int NextLevelExp
    {
        get { return m_nextLevelExp; }
    }

    public int PendingUpgradeCount
    {
        get { return m_pendingUpgradeCount; }
    }

    public bool HasPendingUpgrade
    {
        get { return m_pendingUpgradeCount > 0; }
    }

    private void ResetProgression()
    {
        m_playerLevel = 1;
        m_currentExp = 0;
        m_nextLevelExp = CalcNextLevelExp(m_playerLevel);
        m_pendingUpgradeCount = 0;
        EventDispatcher.instance.DispatchEvent(EventDef.EVENT_UPDATE_EXP);
        EventDispatcher.instance.DispatchEvent(EventDef.EVENT_LEVEL_UP_AVAILABLE);
    }

    private int CalcNextLevelExp(int level)
    {
        return BASE_NEXT_LEVEL_EXP + (level - 1) * 8;
    }

    private UpgradeChoice CreateUpgradeChoice(UpgradeType type)
    {
        switch (type)
        {
            case UpgradeType.FireRate:
                return new UpgradeChoice(type, "⚡ 急速射击", "射击间隔降低，火力覆盖更密");
            case UpgradeType.ParallelShot:
                return new UpgradeChoice(type, "🔱 并排弹幕", "额外增加 1 路平行子弹");
            case UpgradeType.MoveSpeed:
                return new UpgradeChoice(type, "🪽 极速机动", "移动速度提升，走位更轻快");
            case UpgradeType.BulletPower:
                return new UpgradeChoice(type, "💥 重型弹头", "子弹伤害提高，击杀更快");
            case UpgradeType.Shield:
                return new UpgradeChoice(type, "🛡 护盾发生器", "获得 1 层护盾，抵挡 1 次子弹伤害");
            default:
                return new UpgradeChoice(type, "未知强化", "无描述");
        }
    }

    private LevelConfig m_level;
    private LevelConfig m_nextLevel;
    private int m_score;
    private int m_bombCnt;
    private int m_lifeCnt;
    private int m_playerLevel;
    private int m_currentExp;
    private int m_nextLevelExp;
    private int m_pendingUpgradeCount;

    // 单例模式
    private static GameMgr s_instance;
    public static GameMgr instance
    {
        get
        {
            if (null == s_instance)
                s_instance = new GameMgr();
            return s_instance;
        }
    }
}

/// <summary>
/// 游戏状态
/// </summary>
public enum GameState
{
    Ready,
    Playing,
    Pause,
    End,
}

public enum UpgradeType
{
    FireRate,
    ParallelShot,
    MoveSpeed,
    BulletPower,
    Shield
}

public struct UpgradeChoice
{
    public UpgradeType type;
    public string title;
    public string description;

    public UpgradeChoice(UpgradeType type, string title, string description)
    {
        this.type = type;
        this.title = title;
        this.description = description;
    }
}
