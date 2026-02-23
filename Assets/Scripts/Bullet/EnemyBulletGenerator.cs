using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyBulletGenerator
{
    /// <summary>
    /// 根据敌机类型创建子弹
    /// </summary>
    /// <param name="aircraftType">敌机类型</param>
    /// <param name="aircraftPos">敌机坐标</param>
    public static void GenerateBulletByAircraftType(AircraftType aircraftType, Vector3 aircraftPos)
    {
        if (null == s_enemyBulletRoot)
        {
            var rootObj = new GameObject("EnemyBulletRoot");
            s_enemyBulletRoot = rootObj.transform;
        }
        switch (aircraftType)
        {
            case AircraftType.Enemy2:
                {
                    GenerateEnemy2Bullet(aircraftPos);
                }
                break;
            case AircraftType.Enemy3:
                {
                    GenerateEnemy3Bullet(aircraftPos);
                }
                break;
        }
    }

    /// <summary>
    /// 创建敌机2的子弹
    /// </summary>
    /// <param name="aircraftPos"></param>
    private static void GenerateEnemy2Bullet(Vector3 aircraftPos)
    {

        var bullet = CreateBulletObj(aircraftPos);
        bullet.rotateSelf = true;
        var playerPos = GameMgr.instance.GetPlayerPos();
        if(Vector3.zero != playerPos)
            bullet.SetTargetDir(playerPos - aircraftPos);
        else
            bullet.SetTargetDir(-Vector3.up);
    }

    /// <summary>
    /// 创建敌机3的子弹（修改版：5个子弹，合理角度分布）
    /// </summary>
    /// <param name="aircraftPos"></param>
    private static void GenerateEnemy3Bullet(Vector3 aircraftPos)
    {
        // 子弹数量减少到5个
        int bulletCount = 5;
        
        for (int i = 0; i < bulletCount; ++i)
        {
            var bullet = CreateBulletObj(aircraftPos);
            
            // 方法1：对称分布（推荐）
            // 总覆盖角度：90度，中心向前（180度），左右各60度
            float totalAngle = 90f; // 覆盖角度
            float startAngle = 180f - totalAngle / 2; // 起始角度
            float angleStep = totalAngle / (bulletCount - 1); // 每个子弹的角度间隔
            
            float currentAngle = startAngle + angleStep * i;
            
            // 方法2：固定角度（可选）
            // float[] fixedAngles = {150f, 165f, 180f, 195f, 210f}; // 5个固定角度
            // float currentAngle = fixedAngles[i];
            
            bullet.transform.Rotate(0, 0, currentAngle);
            bullet.SetTargetDir(bullet.transform.up);
            bullet.rotateSelf = false;
        }
    }

    /// <summary>
    /// 创建子弹物体
    /// </summary>
    /// <param name="startPos"></param>
    /// <returns></returns>
    private static EnemyBullet CreateBulletObj(Vector3 startPos)
    {
        EnemyBullet bullet = null;
        if (s_reusePool.Count > 0)
        {
            bullet = s_reusePool.Dequeue();
        }
        else
        {
            var prefab = ResourceMgr.instance.LoadRes<GameObject>("Bullet/enemy_bullet");
            var obj = Object.Instantiate(prefab);
            obj.transform.SetParent(s_enemyBulletRoot, false);
            bullet = obj.GetComponent<EnemyBullet>();
            bullet.backToPoolAction = () =>
            {
                s_reusePool.Enqueue(bullet);
            };
        }
        bullet.speed = 5f;
        bullet.SetStartPos(startPos);
        bullet.SetAngles(Vector3.zero);
        bullet.rotateSelf = false;
        bullet.SetCurveMove(false);
        bullet.ActiveSelf(true);
        return bullet;
    }

    /// <summary>
    /// 清理
    /// </summary>
    public static void CLear()
    {
        if(null != s_enemyBulletRoot)
        {
            Object.Destroy(s_enemyBulletRoot.gameObject);
            s_enemyBulletRoot = null;
        }
        s_reusePool.Clear();
    }

        /// <summary>
    /// 生成Boss子弹（新增方法）
    /// </summary>
    public static void GenerateBossBullet(Vector3 bossPos, float angle, EnemyGenerator.BossPhase phase)
    {
        // 根据阶段设置不同的子弹属性
        switch (phase)
        {
            case EnemyGenerator.BossPhase.Phase1_Patrol:
                GenerateBossBulletCustom(bossPos, angle, 1f, Color.red, false, false);
                break;
                
            case EnemyGenerator.BossPhase.Phase2_Attack:
                GenerateBossBulletCustom(bossPos, angle, 3f, new Color(0.8f, 0.2f, 0.8f), false, false);
                break;
        }
    }

    /// <summary>
    /// 自定义Boss子弹参数，供复杂弹幕模式调用
    /// </summary>
    public static void GenerateBossBulletCustom(
        Vector3 bossPos,
        float angle,
        float speed,
        Color color,
        bool rotateSelf,
        bool useCurveMove,
        float curveAmplitude = 0.35f,
        float curveFrequency = 6f)
    {
        if (null == s_enemyBulletRoot)
        {
            var rootObj = new GameObject("EnemyBulletRoot");
            s_enemyBulletRoot = rootObj.transform;
        }

        var bullet = CreateBulletObj(bossPos);
        bullet.speed = speed;
        bullet.transform.Rotate(0, 0, angle);
        bullet.SetTargetDir(bullet.transform.up);
        bullet.rotateSelf = rotateSelf;
        bullet.SetCurveMove(useCurveMove, curveAmplitude, curveFrequency);
        SetBulletColor(bullet, color);
    }

    /// <summary>
    /// 设置子弹颜色（新增辅助方法）
    /// </summary>
    private static void SetBulletColor(EnemyBullet bullet, Color color)
    {
        SpriteRenderer renderer = bullet.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.color = color;
        }
    }


    private static Transform s_enemyBulletRoot;
    private static Queue<EnemyBullet> s_reusePool = new Queue<EnemyBullet>();
}
