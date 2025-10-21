using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BoardController : MonoBehaviour
{
    public event Action OnMoveEvent = delegate { };

    public bool IsBusy { get; private set; }

    private Board m_board;

    private GameManager m_gameManager;

    private bool m_isDragging;

    private Camera m_cam;

    private Collider2D m_hitCollider;

    private GameSettings m_gameSettings;

    private List<Cell> m_potentialMatch;

    private float m_timeAfterFill;

    private bool m_hintIsShown;

    private bool m_gameOver;

    public void StartGame(GameManager gameManager, GameSettings gameSettings)
    {
        m_gameManager = gameManager;

        m_gameSettings = gameSettings;

        m_gameManager.StateChangedAction += OnGameStateChange;

        m_cam = Camera.main;

        m_board = new Board(this.transform, gameSettings);

        Fill();
    }

    private void Fill()
    {
        m_board.Fill();
        // Do not auto-collapse matches on initial fill to prevent unexpected initial removals
        // FindMatchesAndCollapse();
    }

    private void OnGameStateChange(GameManager.eStateGame state)
    {
        switch (state)
        {
            case GameManager.eStateGame.GAME_STARTED:
                IsBusy = false;
                break;
            case GameManager.eStateGame.PAUSE:
                IsBusy = true;
                break;
            case GameManager.eStateGame.GAME_OVER:
                m_gameOver = true;
                // StopHints();
                break;
        }
    }


    public void Update()
    {
        if (m_gameOver) return;
        if (IsBusy) return;

        // if (!m_hintIsShown)
        // {
        //     m_timeAfterFill += Time.deltaTime;
        //     if (m_timeAfterFill > m_gameSettings.TimeForHint)
        //     {
        //         m_timeAfterFill = 0f;
        //         ShowHint();
        //     }
        // }

        // Single-click interaction: move clicked item into extra row (no swapping)
        if (Input.GetMouseButtonDown(0))
        {
            var hit = Physics2D.Raycast(m_cam.ScreenToWorldPoint(Input.mousePosition), Vector2.zero);
            if (hit.collider != null)
            {
                Cell clicked = hit.collider.GetComponent<Cell>();
                if (clicked != null)
                {
                    // StopHints();

                    // Clicked on extra row
                    if (clicked.BoardY == -1)
                    {
                        if (m_gameManager != null && m_gameManager.CurrentLevelMode == GameManager.eLevelMode.MOVES)
                        {
                            // MOVES mode: clicking extra row does nothing
                        }
                        else
                        {
                            // TIMER mode: try to return item to its origin cell
                            bool returned = m_board.ReturnFromExtraRow(clicked);
                            if (returned)
                            {
                                m_timeAfterFill = 0f;
                            }
                        }
                    }
                    else
                    {
                        // Clicked on main board cell: try move to extra row
                        var res = m_board.TryMoveToExtraRow(clicked);
                        if (res == Board.eExtraRowResult.ExtraFull)
                        {
                            // MOVES mode: extra full => game over
                            if (m_gameManager != null && m_gameManager.CurrentLevelMode == GameManager.eLevelMode.MOVES)
                            {
                                m_gameManager.GameOver();
                            }
                            else
                            {
                                // TIMER mode: extra full does not cause game over
                                m_timeAfterFill = 0f;
                            }
                        }
                        else if (res == Board.eExtraRowResult.Moved)
                        {
                            // Moved successfully. Do NOT trigger OnMoveEvent for MOVES mode (we're "bỏ lượt").
                            if (m_gameManager == null || m_gameManager.CurrentLevelMode != GameManager.eLevelMode.MOVES)
                            {
                                OnMoveEvent();
                            }

                            m_timeAfterFill = 0f;

                            // If board is empty after this move, end the level
                            if (m_board != null && m_board.IsEmpty() && m_gameManager != null)
                            {
                                m_gameManager.GameOver();
                                return;
                            }
                        }
                        else
                        {
                            // Failed: do nothing
                        }
                    }
                }
            }
        }
    }

    private void ResetRayCast()
    {
        m_isDragging = false;
        m_hitCollider = null;
    }

    private void FindMatchesAndCollapse(Cell cell1, Cell cell2)
    {
        if (cell1.Item is BonusItem)
        {
            cell1.ExplodeItem();
            StartCoroutine(ShiftDownItemsCoroutine());
        }
        else if (cell2.Item is BonusItem)
        {
            cell2.ExplodeItem();
            StartCoroutine(ShiftDownItemsCoroutine());
        }
        else
        {
            List<Cell> cells1 = GetMatches(cell1);
            List<Cell> cells2 = GetMatches(cell2);

            List<Cell> matches = new List<Cell>();
            matches.AddRange(cells1);
            matches.AddRange(cells2);
            matches = matches.Distinct().ToList();

            if (matches.Count < m_gameSettings.MatchesMin)
            {
                m_board.Swap(cell1, cell2, () =>
                {
                    IsBusy = false;
                });
            }
            else
            {
                OnMoveEvent();

                CollapseMatches(matches, cell2);
            }
        }
    }

    private void FindMatchesAndCollapse()
    {
        List<Cell> matches = m_board.FindFirstMatch();

        if (matches.Count > 0)
        {
            CollapseMatches(matches, null);
        }
        else
        {
            m_potentialMatch = m_board.GetPotentialMatches();
            if (m_potentialMatch.Count > 0)
            {
                IsBusy = false;

                m_timeAfterFill = 0f;
            }
            else
            {
                //StartCoroutine(RefillBoardCoroutine());
                StartCoroutine(ShuffleBoardCoroutine());
            }
        }
    }

    private List<Cell> GetMatches(Cell cell)
    {
        List<Cell> listHor = m_board.GetHorizontalMatches(cell);
        if (listHor.Count < m_gameSettings.MatchesMin)
        {
            listHor.Clear();
        }

        List<Cell> listVert = m_board.GetVerticalMatches(cell);
        if (listVert.Count < m_gameSettings.MatchesMin)
        {
            listVert.Clear();
        }

        return listHor.Concat(listVert).Distinct().ToList();
    }

    private void CollapseMatches(List<Cell> matches, Cell cellEnd)
    {
        for (int i = 0; i < matches.Count; i++)
        {
            matches[i].ExplodeItem();
        }

        if(matches.Count > m_gameSettings.MatchesMin)
        {
            m_board.ConvertNormalToBonus(matches, cellEnd);
        }

        // If board is empty after explode => trigger game over
        if (m_board != null && m_board.IsEmpty() && m_gameManager != null)
        {
            m_gameManager.GameOver();
            return;
        }

        StartCoroutine(ShiftDownItemsCoroutine());
    }

    private IEnumerator ShiftDownItemsCoroutine()
    {
        m_board.ShiftDownItems();

        yield return new WaitForSeconds(0.2f);

        m_board.FillGapsWithNewItems();

        yield return new WaitForSeconds(0.2f);

        // if board became empty after fills/explodes -> game over
        if (m_board != null && m_board.IsEmpty() && m_gameManager != null)
        {
            m_gameManager.GameOver();
            yield break;
        }

        FindMatchesAndCollapse();
    }

    private IEnumerator RefillBoardCoroutine()
    {
        m_board.ExplodeAllItems();

        yield return new WaitForSeconds(0.2f);

        m_board.Fill();

        yield return new WaitForSeconds(0.2f);

        FindMatchesAndCollapse();
    }

    private IEnumerator ShuffleBoardCoroutine()
    {
        m_board.Shuffle();

        yield return new WaitForSeconds(0.3f);

        FindMatchesAndCollapse();
    }


    private void SetSortingLayer(Cell cell1, Cell cell2)
    {
        if (cell1.Item != null) cell1.Item.SetSortingLayerHigher();
        if (cell2.Item != null) cell2.Item.SetSortingLayerLower();
    }

    private bool AreItemsNeighbor(Cell cell1, Cell cell2)
    {
        return cell1.IsNeighbour(cell2);
    }

    internal void Clear()
    {
        m_board.Clear();
    }

    // Helper wrappers for UI/GameManager queries
    public bool IsBoardEmpty()
    {
        return m_board != null && m_board.IsEmpty();
    }

    public bool IsExtraRowFull()
    {
        return m_board != null && m_board.IsExtraRowFull();
    }

    private void ShowHint()
    {
        m_hintIsShown = true;
        foreach (var cell in m_potentialMatch)
        {
            cell.AnimateItemForHint();
        }
    }

    private void StopHints()
    {
        m_hintIsShown = false;
        foreach (var cell in m_potentialMatch)
        {
            cell.StopHintAnimation();
        }

        m_potentialMatch.Clear();
    }
    
}
