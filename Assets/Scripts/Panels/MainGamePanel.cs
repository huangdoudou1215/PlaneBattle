using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗界面
/// </summary>
public class MainGamePanel : BasePanel
{
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
    }

    protected override void OnHide()
    {
        base.OnHide();

        EventDispatcher.instance.UnRegist(EventDef.EVENT_UPDATE_SCORE, OnEventUpdateScore);
        EventDispatcher.instance.UnRegist(EventDef.EVENT_UPDATE_BOMB_CNT, OnEventUpdateBombCnt);
        EventDispatcher.instance.UnRegist(EventDef.EVENT_UPDATE_LIFE_CNT, OnEventUpdateLifeCnt);
    }

    public override void SetUi(PrefabSlot slot)
    {
        base.SetUi(slot);
        slot.SetButton("BombBtn", (btn) =>
        {
            // 炸弹按钮被点击
            GameMgr.instance.KillAllEnemy();
        });
        slot.SetButton("PauseBtn", (btn) =>
        {
            // 暂停按钮被点击
            GameMgr.instance.PauseGame();
        });
        m_scoreText = slot.GetObj<Text>("ScoreText");
        m_bombCntText = slot.GetObj<Text>("BombCntText");
        m_lifeCntText = slot.GetObj<Text>("LifeCntText");
    }

    /// <summary>
    /// 更新得分显示
    /// </summary>
    /// <param name="score"></param>
    private void UpdateScoreText(int score)
    {
        m_scoreText.text = score.ToString();
    }

    /// <summary>
    /// 更新炸弹数量显示
    /// </summary>
    /// <param name="bombCnt"></param>
    private void UpdateBombCntText(int bombCnt)
    {
        m_bombCntText.text = bombCnt.ToString();
    }

    /// <summary>
    /// 更新生命数显示
    /// </summary>
    /// <param name="lifeCnt"></param>
    private void UpdateLifeCntText(int lifeCnt)
    {
        if (m_lifeCntText != null)
        {
            m_lifeCntText.text = "生命: " + lifeCnt.ToString();
        }
    }

    private void OnEventUpdateScore(params object[] args)
    {
        UpdateScoreText(GameMgr.instance.Score);
    }

    private void OnEventUpdateBombCnt(params object[] args)
    {
        UpdateBombCntText(GameMgr.instance.BombCnt);
    }

    private void OnEventUpdateLifeCnt(params object[] args)
    {
        UpdateLifeCntText(GameMgr.instance.LifeCnt);
    }

    private Text m_scoreText;
    private Text m_bombCntText;
    private Text m_lifeCntText;
}
