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
    private bool isShowing;

    private void Start()
    {
        HideHealthBar();
    }

    private void Update()
    {
        if (!isShowing) return;

        if (currentBoss != null)
        {
            UpdateHealthDisplay();
        }
        else
        {
            HideHealthBar();
        }
    }

    public void ShowBossHealthBar(EnemyAircraft boss)
    {
        if (boss == null || healthSlider == null) return;

        currentBoss = boss;
        isShowing = true;

        SetHealthBarVisible(true);

        healthSlider.maxValue = 100;
        healthSlider.value = boss.blood;
        UpdateHealthDisplay();
    }

    public void HideHealthBar()
    {
        isShowing = false;
        currentBoss = null;
        SetHealthBarVisible(false);
    }

    private void UpdateHealthDisplay()
    {
        if (currentBoss == null || healthSlider == null) return;

        healthSlider.value = currentBoss.blood;
        if (healthText != null)
        {
            healthText.text = $"{currentBoss.blood} / {healthSlider.maxValue}";
        }
    }

    public void SetMaxHealth(int maxHealth)
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
        }
    }

    private void SetHealthBarVisible(bool visible)
    {
        // 只控制 Boss 血条自身，避免误隐藏整个 MainGamePanel UI。
        if (healthSlider != null)
        {
            healthSlider.gameObject.SetActive(visible);
        }

        if (healthText != null)
        {
            healthText.gameObject.SetActive(visible);
        }
    }
}
