using Mirror;
using UnityEngine;

/// <summary>
/// 联机飞机玩家：本地输入 -> 服务器移动 -> 同步给所有客户端。
/// </summary>
public class PlaneBattleNetworkPlayer : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnPositionChanged))]
    private Vector3 m_syncedPosition;

    [SyncVar(hook = nameof(OnColorChanged))]
    private Color m_playerColor;

    [HideInInspector]
    public int playerIndex;

    [SerializeField]
    private float moveSpeed = 8f;

    private Renderer m_renderer;

    public override void OnStartServer()
    {
        m_syncedPosition = transform.position;
        m_playerColor = playerIndex == 0 ? Color.cyan : new Color(1f, 0.6f, 0.1f);
    }

    public override void OnStartClient()
    {
        m_renderer = GetComponent<Renderer>();
        ApplyColor(m_playerColor);
        transform.position = m_syncedPosition;
    }

    private void Update()
    {
        if (!isLocalPlayer)
        {
            return;
        }

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        if (Mathf.Approximately(horizontal, 0f) && Mathf.Approximately(vertical, 0f))
        {
            return;
        }

        CmdMove(horizontal, vertical, Time.deltaTime);
    }

    [Command]
    private void CmdMove(float horizontal, float vertical, float deltaTime)
    {
        Vector3 move = new Vector3(horizontal, vertical, 0f).normalized * moveSpeed * deltaTime;
        Vector3 nextPos = transform.position + move;

        nextPos.x = Mathf.Clamp(nextPos.x, -7f, 7f);
        nextPos.y = Mathf.Clamp(nextPos.y, -4f, 4f);
        nextPos.z = 0f;

        transform.position = nextPos;
        m_syncedPosition = nextPos;
    }

    private void OnPositionChanged(Vector3 _, Vector3 newPos)
    {
        if (isServer)
        {
            return;
        }

        transform.position = newPos;
    }

    private void OnColorChanged(Color _, Color newColor)
    {
        ApplyColor(newColor);
    }

    private void ApplyColor(Color color)
    {
        if (m_renderer == null)
        {
            m_renderer = GetComponent<Renderer>();
        }

        if (m_renderer != null)
        {
            m_renderer.material.color = color;
        }
    }
}
