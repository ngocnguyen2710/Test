using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;

public class UIPanelMain : MonoBehaviour, IMenu
{
    [SerializeField] private Button btnTimer;

    [SerializeField] private Button btnMoves;

    [SerializeField] private Button btnAutoWin;

    [SerializeField] private Button btnAutoLose;

    private UIMainManager m_mngr;

    private void Awake()
    {
        btnMoves.onClick.AddListener(OnClickMoves);
        btnTimer.onClick.AddListener(OnClickTimer);
        btnAutoWin.onClick.AddListener(OnClickAutoWin);
        btnAutoLose.onClick.AddListener(OnClickAutoLose);
    }

    private void OnDestroy()
    {
        if (btnMoves) btnMoves.onClick.RemoveAllListeners();
        if (btnTimer) btnTimer.onClick.RemoveAllListeners();
        if (btnAutoWin) btnAutoWin.onClick.RemoveAllListeners();
        if (btnAutoLose) btnAutoLose.onClick.RemoveAllListeners();
    }

    private void OnClickAutoWin()
    {
        // Load MOVES level then start auto-win routine
        StartCoroutine(WaitAndStartAuto(true));
    }

    private void OnClickAutoLose()
    {
        // Load MOVES level then start auto-lose routine
        StartCoroutine(WaitAndStartAuto(false));
    }

    private IEnumerator WaitAndStartAuto(bool winMode)
    {
        // start level (synchronously creates BoardController)
        m_mngr.LoadLevelMoves();
        // wait until BoardController exists
        yield return new WaitUntil(() => FindObjectOfType<BoardController>() != null);
        yield return null;

        var bc = FindObjectOfType<BoardController>();
        if (bc == null)
        {
            UnityEngine.Debug.LogError("BoardController not found after loading level.");
            yield break;
        }
        else
            UnityEngine.Debug.Log("BoardController found.");

        if (winMode)
        {
             UnityEngine.Debug.Log("win");
            // bc.StartAutoWin(0.5f);
        }
        else
        {
             UnityEngine.Debug.Log("lose");
            // bc.StartAutoLose(0.5f);
        }
    }

    public void Setup(UIMainManager mngr)
    {
        m_mngr = mngr;
    }

    private void OnClickTimer()
    {
        m_mngr.LoadLevelTimer();
    }

    private void OnClickMoves()
    {
        m_mngr.LoadLevelMoves();
    }

    public void Show()
    {
        this.gameObject.SetActive(true);
    }

    public void Hide()
    {
        this.gameObject.SetActive(false);
    }
}
