using UnityEngine;

/// <summary>
/// 经验球
/// </summary>
public class ExperienceOrb : MonoBehaviour
{
    public int expValue = 5;
    public float dropSpeed = 1.4f;
    public float attractRange = 2.4f;
    public float attractSpeed = 10f;
    public float lifeTime = 20f;

    private Transform m_selfTrans;
    private float m_lifeTimer;
    private bool m_collected;

    private void Awake()
    {
        m_selfTrans = transform;
    }

    private void Update()
    {
        if (GameState.Pause == GameMgr.instance.gameState) return;

        m_lifeTimer += Time.deltaTime;
        if (m_lifeTimer >= lifeTime)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 playerPos = GameMgr.instance.GetPlayerPos();
        bool hasPlayer = playerPos != Vector3.zero;

        // 默认缓慢下落，进入吸附范围后改为吸向玩家
        if (!hasPlayer)
        {
            m_selfTrans.position += Vector3.down * dropSpeed * Time.deltaTime;
            return;
        }

        float distance = Vector3.Distance(playerPos, m_selfTrans.position);
        if (distance <= attractRange)
        {
            m_selfTrans.position = Vector3.MoveTowards(m_selfTrans.position, playerPos, attractSpeed * Time.deltaTime);
        }
        else
        {
            m_selfTrans.position += Vector3.down * dropSpeed * Time.deltaTime;
        }
    }

    public void Init(int value)
    {
        expValue = Mathf.Max(1, value);
        m_lifeTimer = 0f;
        m_collected = false;
    }

    public void Collect()
    {
        if (m_collected) return;
        m_collected = true;
        GameMgr.instance.AddExperience(expValue);
        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            Collect();
        }
    }
}

/// <summary>
/// 经验球生成器
/// </summary>
public class ExperienceOrbGenerator
{
    private Transform m_orbRoot;

    public void Init()
    {
    }

    public void DestroyRoot()
    {
        if (m_orbRoot != null)
        {
            Object.Destroy(m_orbRoot.gameObject);
            m_orbRoot = null;
        }
    }

    public void Generate(Vector3 worldPos, int expValue)
    {
        if (m_orbRoot == null)
        {
            var rootObj = new GameObject("ExperienceOrbRoot");
            m_orbRoot = rootObj.transform;
        }

        var prefab = ResourceMgr.instance.LoadRes<GameObject>("Bullet/exp_orb");
        if (prefab == null) return;

        var obj = Object.Instantiate(prefab);
        obj.transform.SetParent(m_orbRoot, false);
        obj.transform.position = worldPos;

        var orb = obj.GetComponent<ExperienceOrb>();
        if (orb != null)
        {
            orb.Init(expValue);
        }
    }
}
