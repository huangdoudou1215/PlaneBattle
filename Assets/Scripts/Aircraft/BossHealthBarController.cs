// BossHealthBarController.cs
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Boss 血条控制器
/// </summary>
public class BossHealthBarController : MonoBehaviour
{
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Text healthText;
    [SerializeField] private GameObject healthBarContainer;
    
    private EnemyAircraft currentBoss;
    private bool isShowing = false;
    
    private void Start()
    {
        // 初始隐藏血条
        HideHealthBar();
    }
    
    private void Update()
    {
        if (!isShowing) return;
        
        // 更新血量显示
        if (currentBoss != null)
        {
            UpdateHealthDisplay();
        }
        else
        {
            // Boss 被销毁，隐藏血条
            HideHealthBar();
        }
    }
    
    /// <summary>
    /// 显示 Boss 血条
    /// </summary>
    public void ShowBossHealthBar(EnemyAircraft boss)
    {
        if (boss == null) return;
        
        currentBoss = boss;
        isShowing = true;
        
        // 激活 UI
        healthBarContainer.SetActive(true);
        
        // 初始化血条
        int maxHealth = 100; // 根据你的 Boss 血量设定
        healthSlider.maxValue = maxHealth;
        healthSlider.value = boss.blood;
        
        UpdateHealthDisplay();
        
        Debug.Log("Boss 血条已显示");
    }
    
    /// <summary>
    /// 隐藏 Boss 血条
    /// </summary>
    public void HideHealthBar()
    {
        isShowing = false;
        currentBoss = null;
        healthBarContainer.SetActive(false);
    }
    
    /// <summary>
    /// 更新血量显示
    /// </summary>
    private void UpdateHealthDisplay()
    {
        if (currentBoss == null) return;
        
        // 更新滑块
        healthSlider.value = currentBoss.blood;
        
        // 更新文本
        if (healthText != null)
        {
            healthText.text = $"{currentBoss.blood} / {healthSlider.maxValue}";
        }
    }
    
    /// <summary>
    /// 设置最大血量（用于不同 Boss）
    /// </summary>
    public void SetMaxHealth(int maxHealth)
    {
        healthSlider.maxValue = maxHealth;
    }
}