using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗界面
/// </summary>
public class MainGamePanel : BasePanel
{
    private class UpgradeChoiceView
    {
        public Button button;
        public Text titleText;
        public Text descText;
    }

    public override void Awake()
    {
        base.Awake();
        m_panelResPath = "Panels/MainGamePanel";
    }

    protected override void OnShow()
    {
        base.OnShow();
        EventDispatcher.instance.Regist(EventDef.EVENT_UPDATE_SCORE, OnEventUpdateScore);
        EventDispatcher.instance.Regist(EventDef.EVENT_UPDATE_BOMB_CNT, OnEventUpdateBombCnt);
        EventDispatcher.instance.Regist(EventDef.EVENT_UPDATE_LIFE_CNT, OnEventUpdateLifeCnt);
        EventDispatcher.instance.Regist(EventDef.EVENT_UPDATE_EXP, OnEventUpdateExp);
        EventDispatcher.instance.Regist(EventDef.EVENT_LEVEL_UP_AVAILABLE, OnEventLevelUpAvailable);

        UpdateScoreText(GameMgr.instance.Score);
        UpdateBombCntText(GameMgr.instance.BombCnt);
        UpdateLifeCntText(GameMgr.instance.LifeCnt);
        UpdateExpUi();
        RefreshUpgradeChoicesUi();
    }

    protected override void OnHide()
    {
        base.OnHide();

        EventDispatcher.instance.UnRegist(EventDef.EVENT_UPDATE_SCORE, OnEventUpdateScore);
        EventDispatcher.instance.UnRegist(EventDef.EVENT_UPDATE_BOMB_CNT, OnEventUpdateBombCnt);
        EventDispatcher.instance.UnRegist(EventDef.EVENT_UPDATE_LIFE_CNT, OnEventUpdateLifeCnt);
        EventDispatcher.instance.UnRegist(EventDef.EVENT_UPDATE_EXP, OnEventUpdateExp);
        EventDispatcher.instance.UnRegist(EventDef.EVENT_LEVEL_UP_AVAILABLE, OnEventLevelUpAvailable);

        m_upgradeChoices.Clear();
        m_upgradeViews.Clear();
        m_expRoot = null;
        m_expSlider = null;
        m_expText = null;
        m_levelText = null;
        m_upgradeRoot = null;
    }

    public override void SetUi(PrefabSlot slot)
    {
        base.SetUi(slot);
        slot.SetButton("BombBtn", (btn) =>
        {
            GameMgr.instance.KillAllEnemy();
        });
        slot.SetButton("PauseBtn", (btn) =>
        {
            GameMgr.instance.PauseGame();
        });

        m_scoreText = slot.GetObj<Text>("ScoreText");
        m_bombCntText = slot.GetObj<Text>("BombCntText");
        m_lifeCntText = slot.GetObj<Text>("LifeCntText");

        EnsureExpUi();
        EnsureUpgradeUi();
    }

    private void Update()
    {
        if (!GameMgr.instance.HasPendingUpgrade) return;

        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            SelectUpgradeByIndex(0);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            SelectUpgradeByIndex(1);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
        {
            SelectUpgradeByIndex(2);
        }
    }

    private void UpdateScoreText(int score)
    {
        if (m_scoreText != null)
            m_scoreText.text = score.ToString();
    }

    private void UpdateBombCntText(int bombCnt)
    {
        if (m_bombCntText != null)
            m_bombCntText.text = bombCnt.ToString();
    }

    private void UpdateLifeCntText(int lifeCnt)
    {
        if (m_lifeCntText != null)
            m_lifeCntText.text = "生命: " + lifeCnt.ToString();
    }

    private void OnEventUpdateScore(params object[] args) { UpdateScoreText(GameMgr.instance.Score); }
    private void OnEventUpdateBombCnt(params object[] args) { UpdateBombCntText(GameMgr.instance.BombCnt); }
    private void OnEventUpdateLifeCnt(params object[] args) { UpdateLifeCntText(GameMgr.instance.LifeCnt); }
    private void OnEventUpdateExp(params object[] args) { UpdateExpUi(); }
    private void OnEventLevelUpAvailable(params object[] args) { RefreshUpgradeChoicesUi(); }

    private void EnsureExpUi()
    {
        if (m_expRoot != null) return;

        var rootObj = new GameObject("ExpUiRoot", typeof(RectTransform));
        m_expRoot = rootObj.GetComponent<RectTransform>();
        m_expRoot.SetParent(m_panelObj.transform, false);
        m_expRoot.anchorMin = new Vector2(0.5f, 1f);
        m_expRoot.anchorMax = new Vector2(0.5f, 1f);
        m_expRoot.pivot = new Vector2(0.5f, 1f);
        m_expRoot.anchoredPosition = new Vector2(0f, -8f);
        m_expRoot.sizeDelta = new Vector2(560f, 30f);

        m_levelText = CreateText("LevelText", m_expRoot, new Vector2(0f, -2f), new Vector2(120f, 20f), TextAnchor.MiddleLeft, 13);
        m_expText = CreateText("ExpText", m_expRoot, new Vector2(440f, -2f), new Vector2(120f, 20f), TextAnchor.MiddleRight, 12);

        var sliderObj = new GameObject("ExpSlider", typeof(RectTransform), typeof(Slider));
        var sliderRect = sliderObj.GetComponent<RectTransform>();
        sliderRect.SetParent(m_expRoot, false);
        sliderRect.anchorMin = new Vector2(0f, 0f);
        sliderRect.anchorMax = new Vector2(1f, 0f);
        sliderRect.pivot = new Vector2(0.5f, 0f);
        sliderRect.anchoredPosition = new Vector2(0f, 0f);
        sliderRect.sizeDelta = new Vector2(0f, 8f);

        var slider = sliderObj.GetComponent<Slider>();

        var sliderBgObj = new GameObject("Background", typeof(RectTransform), typeof(Image));
        var sliderBgRect = sliderBgObj.GetComponent<RectTransform>();
        sliderBgRect.SetParent(sliderRect, false);
        sliderBgRect.anchorMin = Vector2.zero;
        sliderBgRect.anchorMax = Vector2.one;
        sliderBgRect.offsetMin = Vector2.zero;
        sliderBgRect.offsetMax = Vector2.zero;
        var sliderBgImage = sliderBgObj.GetComponent<Image>();
        sliderBgImage.color = new Color(0f, 0f, 0f, 0f);

        var fillAreaObj = new GameObject("Fill Area", typeof(RectTransform));
        var fillAreaRect = fillAreaObj.GetComponent<RectTransform>();
        fillAreaRect.SetParent(sliderRect, false);
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = Vector2.zero;
        fillAreaRect.offsetMax = Vector2.zero;

        var fillObj = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        var fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.SetParent(fillAreaRect, false);
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        var fillImage = fillObj.GetComponent<Image>();
        fillImage.color = new Color(0.2f, 0.95f, 1f, 1f);

        slider.fillRect = fillRect;
        slider.targetGraphic = sliderBgImage;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0f;

        m_expSlider = slider;
    }

    private void EnsureUpgradeUi()
    {
        if (m_upgradeRoot != null) return;

        var rootObj = new GameObject("UpgradeChoicesRoot", typeof(RectTransform), typeof(Image));
        m_upgradeRoot = rootObj.GetComponent<RectTransform>();
        m_upgradeRoot.SetParent(m_panelObj.transform, false);
        m_upgradeRoot.anchorMin = new Vector2(0.5f, 0f);
        m_upgradeRoot.anchorMax = new Vector2(0.5f, 0f);
        m_upgradeRoot.pivot = new Vector2(0.5f, 0f);
        m_upgradeRoot.anchoredPosition = new Vector2(0f, 24f);
        m_upgradeRoot.sizeDelta = new Vector2(620f, 286f);

        var bg = rootObj.GetComponent<Image>();
        bg.color = new Color(0.02f, 0.08f, 0.12f, 0.72f);

        var title = CreateText("UpgradeTitle", m_upgradeRoot, new Vector2(16f, -8f), new Vector2(588f, 28f), TextAnchor.MiddleLeft, 16);
        title.text = "升级可选（按 1/2/3）";

        for (int i = 0; i < 3; ++i)
        {
            float y = 188f - i * 78f;
            var view = CreateUpgradeButton(i, y);
            m_upgradeViews.Add(view);
        }
    }

    private UpgradeChoiceView CreateUpgradeButton(int index, float y)
    {
        var btnObj = new GameObject("UpgradeBtn" + (index + 1), typeof(RectTransform), typeof(Image), typeof(Button));
        var rect = btnObj.GetComponent<RectTransform>();
        rect.SetParent(m_upgradeRoot, false);
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(588f, 70f);

        var image = btnObj.GetComponent<Image>();
        image.color = new Color(0.08f, 0.2f, 0.3f, 0.95f);

        var button = btnObj.GetComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        var colors = button.colors;
        colors.highlightedColor = new Color(0.76f, 0.95f, 1f, 1f);
        colors.pressedColor = new Color(0.55f, 0.85f, 0.95f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        Text titleText = CreateText("Title", rect, new Vector2(14f, -8f), new Vector2(560f, 28f), TextAnchor.MiddleLeft, 16);
        titleText.fontStyle = FontStyle.Bold;

        Text descText = CreateText("Desc", rect, new Vector2(14f, -38f), new Vector2(560f, 24f), TextAnchor.MiddleLeft, 13);
        descText.color = new Color(0.85f, 0.95f, 1f, 1f);

        button.targetGraphic = image;

        return new UpgradeChoiceView
        {
            button = button,
            titleText = titleText,
            descText = descText,
        };
    }

    private Text CreateText(string name, RectTransform parent, Vector2 anchoredPos, Vector2 size, TextAnchor anchor, int fontSize)
    {
        var textObj = new GameObject(name, typeof(RectTransform), typeof(Text));
        var rect = textObj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        var text = textObj.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = fontSize;
        text.alignment = anchor;
        text.color = Color.white;
        text.text = "";
        text.raycastTarget = false;
        return text;
    }

    private void UpdateExpUi()
    {
        if (m_expSlider == null || m_expText == null || m_levelText == null) return;

        int exp = GameMgr.instance.CurrentExp;
        int nextExp = Mathf.Max(1, GameMgr.instance.NextLevelExp);
        m_expSlider.value = Mathf.Clamp01((float)exp / nextExp);
        m_expText.text = exp + " / " + nextExp;
        m_levelText.text = "Lv." + GameMgr.instance.PlayerLevel;
    }

    private void RefreshUpgradeChoicesUi()
    {
        if (m_upgradeRoot == null) return;

        if (!GameMgr.instance.HasPendingUpgrade)
        {
            m_upgradeRoot.gameObject.SetActive(false);
            return;
        }

        m_upgradeRoot.gameObject.SetActive(true);
        m_upgradeChoices = GameMgr.instance.BuildUpgradeChoices(3);

        for (int i = 0; i < m_upgradeViews.Count; ++i)
        {
            int localIndex = i;
            if (i < m_upgradeChoices.Count)
            {
                UpgradeChoice choice = m_upgradeChoices[i];
                m_upgradeViews[i].titleText.text = "[" + (i + 1) + "] " + choice.title;
                m_upgradeViews[i].descText.text = choice.description;
                m_upgradeViews[i].button.onClick.RemoveAllListeners();
                m_upgradeViews[i].button.onClick.AddListener(() => { SelectUpgradeByIndex(localIndex); });
                m_upgradeViews[i].button.gameObject.SetActive(true);
            }
            else
            {
                m_upgradeViews[i].button.gameObject.SetActive(false);
            }
        }
    }

    private void SelectUpgradeByIndex(int index)
    {
        if (index < 0 || index >= m_upgradeChoices.Count) return;

        if (GameMgr.instance.ApplyUpgrade(m_upgradeChoices[index].type))
        {
            UpdateExpUi();
            RefreshUpgradeChoicesUi();
        }
    }

    private Text m_scoreText;
    private Text m_bombCntText;
    private Text m_lifeCntText;

    private RectTransform m_expRoot;
    private Slider m_expSlider;
    private Text m_expText;
    private Text m_levelText;

    private RectTransform m_upgradeRoot;
    private readonly List<UpgradeChoiceView> m_upgradeViews = new List<UpgradeChoiceView>();
    private List<UpgradeChoice> m_upgradeChoices = new List<UpgradeChoice>();
}
