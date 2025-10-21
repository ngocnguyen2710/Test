using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIPanelGameOver : MonoBehaviour, IMenu
{
    [SerializeField] private Button btnClose;

    private UIMainManager m_mngr;

    private void Awake()
    {
        btnClose.onClick.AddListener(OnClickClose);
    }

    private void OnDestroy()
    {
        if (btnClose) btnClose.onClick.RemoveAllListeners();
    }

    private void OnClickClose()
    {
        m_mngr.ShowMainMenu();
    }

    public void Hide()
    {
        this.gameObject.SetActive(false);
    }

    public void Setup(UIMainManager mngr)
    {
        m_mngr = mngr;
    }

    public void Show()
    {
        this.gameObject.SetActive(true);
        // Determine message based on current game state
        string message = "GAME OVER";

        var gm = FindObjectOfType<GameManager>();
        var bc = FindObjectOfType<BoardController>();

        bool boardEmpty = (bc != null) ? bc.IsBoardEmpty() : false;

        if (boardEmpty)
        {
            message = "YOU WIN";
        }
        else if (gm != null)
        {
            if (gm.CurrentLevelMode == GameManager.eLevelMode.MOVES)
            {
                // MOVES: if extra row is full and there are no matches -> GAME OVER
                if (bc != null && bc.IsExtraRowFull())
                {
                    message = "GAME OVER";
                }
                else
                {
                    message = "GAME OVER";
                }
            }
            else if (gm.CurrentLevelMode == GameManager.eLevelMode.TIMER)
            {
                // TIMER: if time ended and board still has items -> GAME OVER (handled by caller)
                message = "GAME OVER";
            }
        }

        // Safely set the child Text. Use TextMeshPro if present, otherwise fallback to legacy Text.
        var tmp = this.gameObject.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
        {
            tmp.text = message;
            return;
        }

        var texts = this.gameObject.GetComponentsInChildren<Text>(true);
        if (texts != null && texts.Length > 0)
        {
            // find the first non-empty text element to write to; keep compatibility with existing layout
            texts[1].text = message;
        }
    }

}
