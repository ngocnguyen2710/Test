using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Board
{
    public enum eMatchDirection
    {
        NONE,
        HORIZONTAL,
        VERTICAL,
        ALL
    }

    private int boardSizeX;

    private int boardSizeY;

    private Cell[,] m_cells;

    private Transform m_root;

    private int m_matchMin;
    
    // extra row of cells under the main board (5 cells)
    private List<Cell> m_extraRowCells = new List<Cell>();

    public enum eExtraRowResult
    {
        Moved,
        ExtraFull,
        Failed
    }

    public Board(Transform transform, GameSettings gameSettings)
    {
        m_root = transform;

        m_matchMin = gameSettings.MatchesMin;

        this.boardSizeX = gameSettings.BoardSizeX;
        this.boardSizeY = gameSettings.BoardSizeY;

        m_cells = new Cell[boardSizeX, boardSizeY];

        CreateBoard();
    }

    private void CreateBoard()
    {
        Vector3 origin = new Vector3(-boardSizeX * 0.5f + 0.5f, -boardSizeY * 0.5f + 0.5f, 0f);
        GameObject prefabBG = Resources.Load<GameObject>(Constants.PREFAB_CELL_BACKGROUND);
        for (int x = 0; x < boardSizeX; x++)
        {
            for (int y = 0; y < boardSizeY; y++)
            {
                GameObject go = GameObject.Instantiate(prefabBG);
                go.transform.position = origin + new Vector3(x, y, 0f);
                go.transform.SetParent(m_root);

                Cell cell = go.GetComponent<Cell>();
                cell.Setup(x, y);

                m_cells[x, y] = cell;
            }
        }

        //set neighbours
        for (int x = 0; x < boardSizeX; x++)
        {
            for (int y = 0; y < boardSizeY; y++)
            {
                if (y + 1 < boardSizeY) m_cells[x, y].NeighbourUp = m_cells[x, y + 1];
                if (x + 1 < boardSizeX) m_cells[x, y].NeighbourRight = m_cells[x + 1, y];
                if (y > 0) m_cells[x, y].NeighbourBottom = m_cells[x, y - 1];
                if (x > 0) m_cells[x, y].NeighbourLeft = m_cells[x - 1, y];
            }
        }

        //new cell
        // We create 5 background cell instances positioned one row below the board (y = -1)
        // and add them to m_extraRowCells so they can be managed/cleaned up like board cells.
        int extraCount = 5;
        for (int ex = 0; ex < extraCount; ex++)
        {
            GameObject go = GameObject.Instantiate(prefabBG);
            // place them centered relative to the board origin; origin is at bottom-left offset
            // so x from 0..extraCount-1 -> world x = origin.x + ex
            go.transform.position = origin + new Vector3(ex -0.5f, -1.5f, 0f);
            go.transform.SetParent(m_root);

            Cell cell = go.GetComponent<Cell>();
            // Use negative Y board coordinate for the extra row to avoid colliding with main board indices
            cell.Setup(ex, -1);

            m_extraRowCells.Add(cell);
        }

    }

    internal void Fill()
    {
        // prepare pool size = main board cells ONLY (extra row remains empty initially)
        int extraCount = m_extraRowCells.Count;
        int mainCells = boardSizeX * boardSizeY;

        // get normal types
        var enumValues = Enum.GetValues(typeof(NormalItem.eNormalType)).Cast<NormalItem.eNormalType>().ToArray();

        // build pool in triplets so each chosen type count % 3 == 0
        List<NormalItem.eNormalType> pool = new List<NormalItem.eNormalType>();
        System.Random rnd = new System.Random();
        while (pool.Count < mainCells)
        {
            var t = enumValues[rnd.Next(enumValues.Length)];
            pool.Add(t); pool.Add(t); pool.Add(t);
        }
        if (pool.Count > mainCells) pool = pool.Take(mainCells).ToList();
        pool = pool.OrderBy(x => UnityEngine.Random.value).ToList();

        // place items on main board ensuring no immediate 3-in-row is created
        bool placedSuccessfully = false;
        for (int attempt = 0; attempt < 10 && !placedSuccessfully; attempt++) // multiple attempts
        {
            placedSuccessfully = true;
            // clear any existing items first
            for (int x = 0; x < boardSizeX; x++)
                for (int y = 0; y < boardSizeY; y++)
                    m_cells[x, y].Free();

            var localPool = pool.ToList();

            for (int y = 0; y < boardSizeY && placedSuccessfully; y++)
            {
                for (int x = 0; x < boardSizeX; x++)
                {
                    bool placed = false;
                    for (int i = 0; i < localPool.Count; i++)
                    {
                        var candidate = localPool[i];
                        bool createsMatch = false;
                        // check horizontal (left two)
                        if (x >= 2)
                        {
                            var left1 = m_cells[x - 1, y].Item as NormalItem;
                            var left2 = m_cells[x - 2, y].Item as NormalItem;
                            if (left1 != null && left2 != null && left1.ItemType == candidate && left2.ItemType == candidate)
                            {
                                createsMatch = true;
                            }
                        }
                        // check vertical (down two)
                        if (!createsMatch && y >= 2)
                        {
                            var down1 = m_cells[x, y - 1].Item as NormalItem;
                            var down2 = m_cells[x, y - 2].Item as NormalItem;
                            if (down1 != null && down2 != null && down1.ItemType == candidate && down2.ItemType == candidate)
                            {
                                createsMatch = true;
                            }
                        }

                        if (createsMatch) continue;

                        NormalItem item = new NormalItem();
                        item.SetType(candidate);
                        item.SetView();
                        item.SetViewRoot(m_root);

                        m_cells[x, y].Assign(item);
                        m_cells[x, y].ApplyItemPosition(false);

                        localPool.RemoveAt(i);
                        placed = true;
                        break;
                    }

                    if (!placed)
                    {
                        // failed this attempt
                        placedSuccessfully = false;
                        break;
                    }
                }
            }

            if (placedSuccessfully)
            {
                // ensure extra row is empty initially
                for (int ex = 0; ex < extraCount; ex++)
                {
                    var cell = m_extraRowCells[ex];
                    cell.Free();
                }
                break;
            }

            // reshuffle pool and retry
            pool = pool.OrderBy(x => UnityEngine.Random.value).ToList();
        }

        // fallback: if placement still failed, fill naively from pool (main board) and keep extra row empty
        if (!placedSuccessfully)
        {
            int idx = 0;
            for (int x = 0; x < boardSizeX; x++)
            {
                for (int y = 0; y < boardSizeY; y++)
                {
                    Cell cell = m_cells[x, y];
                    NormalItem item = new NormalItem();
                    item.SetType(pool[idx++]);
                    item.SetView();
                    item.SetViewRoot(m_root);
                    cell.Assign(item);
                    cell.ApplyItemPosition(false);
                }
            }
            // clear extra row
            for (int ex = 0; ex < extraCount; ex++)
            {
                var cell = m_extraRowCells[ex];
                cell.Free();
            }
        }
    }

    internal void Shuffle()
    {
        List<Item> list = new List<Item>();
        for (int x = 0; x < boardSizeX; x++)
        {
            for (int y = 0; y < boardSizeY; y++)
            {
                list.Add(m_cells[x, y].Item);
                m_cells[x, y].Free();
            }
        }

        for (int x = 0; x < boardSizeX; x++)
        {
            for (int y = 0; y < boardSizeY; y++)
            {
                int rnd = UnityEngine.Random.Range(0, list.Count);
                m_cells[x, y].Assign(list[rnd]);
                m_cells[x, y].ApplyItemMoveToPosition();

                list.RemoveAt(rnd);
            }
        }
    }


    internal void FillGapsWithNewItems()
    {
        for (int x = 0; x < boardSizeX; x++)
        {
            for (int y = 0; y < boardSizeY; y++)
            {
                Cell cell = m_cells[x, y];
                if (!cell.IsEmpty) continue;

                NormalItem item = new NormalItem();

                item.SetType(Utils.GetRandomNormalType());
                item.SetView();
                item.SetViewRoot(m_root);

                cell.Assign(item);
                cell.ApplyItemPosition(true);
            }
        }
    }

    

    // Try to move an item from a board cell into the first available extra-row cell.
    // Returns true if the extra row is full after the move/processing (i.e. trigger game over),
    // false otherwise or if move couldn't be performed.
    internal eExtraRowResult TryMoveToExtraRow(Cell fromCell)
    {
        if (fromCell == null) return eExtraRowResult.Failed;
        if (fromCell.IsEmpty) return eExtraRowResult.Failed;

    // if fromCell is already in extra row, treat as failed
    if (fromCell.BoardY == -1) return eExtraRowResult.Failed;

        // find first empty extra cell
        Cell target = m_extraRowCells.FirstOrDefault(c => c.IsEmpty);
    if (target == null) return eExtraRowResult.ExtraFull;

        Item item = fromCell.Item;
        fromCell.Free();

        target.Assign(item);
        item.SetViewRoot(m_root);
        // store origin cell reference on the extra cell
        target.OriginCell = fromCell;

        if (item.View != null)
        {
            item.View.DOMove(target.transform.position, 0.2f);
        }
        else
        {
            target.ApplyItemPosition(false);
        }

        // After placing, check extra row for groups of 3 (or multiples) of same type and remove them.
        List<Cell> processed = new List<Cell>();
        for (int i = 0; i < m_extraRowCells.Count; i++)
        {
            var baseCell = m_extraRowCells[i];
            if (baseCell.IsEmpty) continue;
            if (processed.Contains(baseCell)) continue;

            // collect matching cells by IsSameType
            List<Cell> matches = new List<Cell>();
            for (int j = 0; j < m_extraRowCells.Count; j++)
            {
                var c = m_extraRowCells[j];
                if (c.IsEmpty) continue;
                if (c.Item.IsSameType(baseCell.Item))
                {
                    matches.Add(c);
                }
            }

            if (matches.Count >= 3)
            {
                // remove groups of 3 until less than 3 remain
                while (matches.Count >= 3)
                {
                    var toRemove = matches.Take(3).ToList();
                    foreach (var rc in toRemove)
                    {
                        rc.ExplodeItem();
                        processed.Add(rc);
                        matches.Remove(rc);
                    }
                }
            }
            else
            {
                processed.AddRange(matches);
            }
        }

        // If there is any empty cell in extra row after processing, game continues
        bool anyEmpty = m_extraRowCells.Any(c => c.IsEmpty);

        if (!anyEmpty) return eExtraRowResult.ExtraFull;
        return eExtraRowResult.Moved;
    }

    // Return item from extra row back to its OriginCell if possible.
    internal bool ReturnFromExtraRow(Cell extraCell)
    {
        if (extraCell == null) return false;
        if (extraCell.BoardY != -1) return false;
        if (extraCell.IsEmpty) return false;
        if (extraCell.OriginCell == null) return false;

        Cell origin = extraCell.OriginCell;
        if (!origin.IsEmpty) return false;

        Item item = extraCell.Item;
        extraCell.Free();

        origin.Assign(item);
        item.SetViewRoot(m_root);
        if (item.View != null)
        {
            item.View.DOMove(origin.transform.position, 0.2f);
        }
        else
        {
            origin.ApplyItemPosition(false);
        }

        return true;
    }

    internal void ExplodeAllItems()
    {
        for (int x = 0; x < boardSizeX; x++)
        {
            for (int y = 0; y < boardSizeY; y++)
            {
                Cell cell = m_cells[x, y];
                cell.ExplodeItem();
            }
        }
    }

    public void Swap(Cell cell1, Cell cell2, Action callback)
    {
        Item item = cell1.Item;
        cell1.Free();
        Item item2 = cell2.Item;
        cell1.Assign(item2);
        cell2.Free();
        cell2.Assign(item);

        item.View.DOMove(cell2.transform.position, 0.3f);
        item2.View.DOMove(cell1.transform.position, 0.3f).OnComplete(() => { if (callback != null) callback(); });
    }

    public List<Cell> GetHorizontalMatches(Cell cell)
    {
        List<Cell> list = new List<Cell>();
        list.Add(cell);

        //check horizontal match
        Cell newcell = cell;
        while (true)
        {
            Cell neib = newcell.NeighbourRight;
            if (neib == null) break;

            if (neib.IsSameType(cell))
            {
                list.Add(neib);
                newcell = neib;
            }
            else break;
        }

        newcell = cell;
        while (true)
        {
            Cell neib = newcell.NeighbourLeft;
            if (neib == null) break;

            if (neib.IsSameType(cell))
            {
                list.Add(neib);
                newcell = neib;
            }
            else break;
        }

        return list;
    }


    public List<Cell> GetVerticalMatches(Cell cell)
    {
        List<Cell> list = new List<Cell>();
        list.Add(cell);

        Cell newcell = cell;
        while (true)
        {
            Cell neib = newcell.NeighbourUp;
            if (neib == null) break;

            if (neib.IsSameType(cell))
            {
                list.Add(neib);
                newcell = neib;
            }
            else break;
        }

        newcell = cell;
        while (true)
        {
            Cell neib = newcell.NeighbourBottom;
            if (neib == null) break;

            if (neib.IsSameType(cell))
            {
                list.Add(neib);
                newcell = neib;
            }
            else break;
        }

        return list;
    }

    internal void ConvertNormalToBonus(List<Cell> matches, Cell cellToConvert)
    {
        eMatchDirection dir = GetMatchDirection(matches);

        BonusItem item = new BonusItem();
        switch (dir)
        {
            case eMatchDirection.ALL:
                item.SetType(BonusItem.eBonusType.ALL);
                break;
            case eMatchDirection.HORIZONTAL:
                item.SetType(BonusItem.eBonusType.HORIZONTAL);
                break;
            case eMatchDirection.VERTICAL:
                item.SetType(BonusItem.eBonusType.VERTICAL);
                break;
        }

        if (item != null)
        {
            if (cellToConvert == null)
            {
                int rnd = UnityEngine.Random.Range(0, matches.Count);
                cellToConvert = matches[rnd];
            }

            item.SetView();
            item.SetViewRoot(m_root);

            cellToConvert.Free();
            cellToConvert.Assign(item);
            cellToConvert.ApplyItemPosition(true);
        }
    }


    internal eMatchDirection GetMatchDirection(List<Cell> matches)
    {
        if (matches == null || matches.Count < m_matchMin) return eMatchDirection.NONE;

        var listH = matches.Where(x => x.BoardX == matches[0].BoardX).ToList();
        if (listH.Count == matches.Count)
        {
            return eMatchDirection.VERTICAL;
        }

        var listV = matches.Where(x => x.BoardY == matches[0].BoardY).ToList();
        if (listV.Count == matches.Count)
        {
            return eMatchDirection.HORIZONTAL;
        }

        if (matches.Count > 5)
        {
            return eMatchDirection.ALL;
        }

        return eMatchDirection.NONE;
    }

    internal List<Cell> FindFirstMatch()
    {
        List<Cell> list = new List<Cell>();

        for (int x = 0; x < boardSizeX; x++)
        {
            for (int y = 0; y < boardSizeY; y++)
            {
                Cell cell = m_cells[x, y];

                var listhor = GetHorizontalMatches(cell);
                if (listhor.Count >= m_matchMin)
                {
                    list = listhor;
                    break;
                }

                var listvert = GetVerticalMatches(cell);
                if (listvert.Count >= m_matchMin)
                {
                    list = listvert;
                    break;
                }
            }
        }

        return list;
    }

    public List<Cell> CheckBonusIfCompatible(List<Cell> matches)
    {
        var dir = GetMatchDirection(matches);

        var bonus = matches.Where(x => x.Item is BonusItem).FirstOrDefault();
        if(bonus == null)
        {
            return matches;
        }

        List<Cell> result = new List<Cell>();
        switch (dir)
        {
            case eMatchDirection.HORIZONTAL:
                foreach (var cell in matches)
                {
                    BonusItem item = cell.Item as BonusItem;
                    if (item == null || item.ItemType == BonusItem.eBonusType.HORIZONTAL)
                    {
                        result.Add(cell);
                    }
                }
                break;
            case eMatchDirection.VERTICAL:
                foreach (var cell in matches)
                {
                    BonusItem item = cell.Item as BonusItem;
                    if (item == null || item.ItemType == BonusItem.eBonusType.VERTICAL)
                    {
                        result.Add(cell);
                    }
                }
                break;
            case eMatchDirection.ALL:
                foreach (var cell in matches)
                {
                    BonusItem item = cell.Item as BonusItem;
                    if (item == null || item.ItemType == BonusItem.eBonusType.ALL)
                    {
                        result.Add(cell);
                    }
                }
                break;
        }

        return result;
    }

    internal List<Cell> GetPotentialMatches()
    {
        List<Cell> result = new List<Cell>();
        for (int x = 0; x < boardSizeX; x++)
        {
            for (int y = 0; y < boardSizeY; y++)
            {
                Cell cell = m_cells[x, y];

                //check right
                /* example *\
                  * * * * *
                  * * * * *
                  * * * ? *
                  * & & * ?
                  * * * ? *
                \* example  */

                if (cell.NeighbourRight != null)
                {
                    result = GetPotentialMatch(cell, cell.NeighbourRight, cell.NeighbourRight.NeighbourRight);
                    if (result.Count > 0)
                    {
                        break;
                    }
                }

                //check up
                /* example *\
                  * ? * * *
                  ? * ? * *
                  * & * * *
                  * & * * *
                  * * * * *
                \* example  */
                if (cell.NeighbourUp != null)
                {
                    result = GetPotentialMatch(cell, cell.NeighbourUp, cell.NeighbourUp.NeighbourUp);
                    if (result.Count > 0)
                    {
                        break;
                    }
                }

                //check bottom
                /* example *\
                  * * * * *
                  * & * * *
                  * & * * *
                  ? * ? * *
                  * ? * * *
                \* example  */
                if (cell.NeighbourBottom != null)
                {
                    result = GetPotentialMatch(cell, cell.NeighbourBottom, cell.NeighbourBottom.NeighbourBottom);
                    if (result.Count > 0)
                    {
                        break;
                    }
                }

                //check left
                /* example *\
                  * * * * *
                  * * * * *
                  * ? * * *
                  ? * & & *
                  * ? * * *
                \* example  */
                if (cell.NeighbourLeft != null)
                {
                    result = GetPotentialMatch(cell, cell.NeighbourLeft, cell.NeighbourLeft.NeighbourLeft);
                    if (result.Count > 0)
                    {
                        break;
                    }
                }

                /* example *\
                  * * * * *
                  * * * * *
                  * * ? * *
                  * & * & *
                  * * ? * *
                \* example  */
                Cell neib = cell.NeighbourRight;
                if (neib != null && neib.NeighbourRight != null && neib.NeighbourRight.IsSameType(cell))
                {
                    Cell second = LookForTheSecondCellVertical(neib, cell);
                    if (second != null)
                    {
                        result.Add(cell);
                        result.Add(neib.NeighbourRight);
                        result.Add(second);
                        break;
                    }
                }

                /* example *\
                  * * * * *
                  * & * * *
                  ? * ? * *
                  * & * * *
                  * * * * *
                \* example  */
                neib = null;
                neib = cell.NeighbourUp;
                if (neib != null && neib.NeighbourUp != null && neib.NeighbourUp.IsSameType(cell))
                {
                    Cell second = LookForTheSecondCellHorizontal(neib, cell);
                    if (second != null)
                    {
                        result.Add(cell);
                        result.Add(neib.NeighbourUp);
                        result.Add(second);
                        break;
                    }
                }
            }

            if (result.Count > 0) break;
        }

        return result;
    }

    private List<Cell> GetPotentialMatch(Cell cell, Cell neighbour, Cell target)
    {
        List<Cell> result = new List<Cell>();

        if (neighbour != null && neighbour.IsSameType(cell))
        {
            Cell third = LookForTheThirdCell(target, neighbour);
            if (third != null)
            {
                result.Add(cell);
                result.Add(neighbour);
                result.Add(third);
            }
        }

        return result;
    }

    private Cell LookForTheSecondCellHorizontal(Cell target, Cell main)
    {
        if (target == null) return null;
        if (target.IsSameType(main)) return null;

        //look right
        Cell second = null;
        second = target.NeighbourRight;
        if (second != null && second.IsSameType(main))
        {
            return second;
        }

        //look left
        second = null;
        second = target.NeighbourLeft;
        if (second != null && second.IsSameType(main))
        {
            return second;
        }

        return null;
    }

    private Cell LookForTheSecondCellVertical(Cell target, Cell main)
    {
        if (target == null) return null;
        if (target.IsSameType(main)) return null;

        //look up        
        Cell second = target.NeighbourUp;
        if (second != null && second.IsSameType(main))
        {
            return second;
        }

        //look bottom
        second = null;
        second = target.NeighbourBottom;
        if (second != null && second.IsSameType(main))
        {
            return second;
        }

        return null;
    }

    private Cell LookForTheThirdCell(Cell target, Cell main)
    {
        if (target == null) return null;
        if (target.IsSameType(main)) return null;

        //look up
        Cell third = CheckThirdCell(target.NeighbourUp, main);
        if (third != null)
        {
            return third;
        }

        //look right
        third = null;
        third = CheckThirdCell(target.NeighbourRight, main);
        if (third != null)
        {
            return third;
        }

        //look bottom
        third = null;
        third = CheckThirdCell(target.NeighbourBottom, main);
        if (third != null)
        {
            return third;
        }

        //look left
        third = null;
        third = CheckThirdCell(target.NeighbourLeft, main); ;
        if (third != null)
        {
            return third;
        }

        return null;
    }

    private Cell CheckThirdCell(Cell target, Cell main)
    {
        if (target != null && target != main && target.IsSameType(main))
        {
            return target;
        }

        return null;
    }

    internal void ShiftDownItems()
    {
        for (int x = 0; x < boardSizeX; x++)
        {
            int shifts = 0;
            for (int y = 0; y < boardSizeY; y++)
            {
                Cell cell = m_cells[x, y];
                if (cell.IsEmpty)
                {
                    shifts++;
                    continue;
                }

                if (shifts == 0) continue;

                Cell holder = m_cells[x, y - shifts];

                Item item = cell.Item;
                cell.Free();

                holder.Assign(item);
                item.View.DOMove(holder.transform.position, 0.3f);
            }
        }
    }

    public void Clear()
    {
        for (int x = 0; x < boardSizeX; x++)
        {
            for (int y = 0; y < boardSizeY; y++)
            {
                Cell cell = m_cells[x, y];
                cell.Clear();

                GameObject.Destroy(cell.gameObject);
                m_cells[x, y] = null;
            }
        }
        // Destroy the extra row cells
        foreach (var cell in m_extraRowCells)
        {
            GameObject.Destroy(cell.gameObject);
        }
        m_extraRowCells.Clear();
    }

    // Check whether both main board and extra row contain no items
    internal bool IsEmpty()
    {
        for (int x = 0; x < boardSizeX; x++)
        {
            for (int y = 0; y < boardSizeY; y++)
            {
                if (!m_cells[x, y].IsEmpty) return false;
            }
        }

        foreach (var c in m_extraRowCells)
        {
            if (!c.IsEmpty) return false;
        }

        return true;
    }

    // Returns true if the extra row is present and contains no empty cells
    internal bool IsExtraRowFull()
    {
        if (m_extraRowCells == null || m_extraRowCells.Count == 0) return false;
        return m_extraRowCells.All(c => !c.IsEmpty);
    }
}
