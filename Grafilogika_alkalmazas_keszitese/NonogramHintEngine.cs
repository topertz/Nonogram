using System;
using System.Collections.Generic;
using System.Drawing;

namespace Grafilogika_alkalmazas_keszitese
{
    public class NonogramHintEngine
    {
        public List<Point> hintCells = new List<Point>();
        public NonogramGrid grid;
        public NonogramHintEngine(NonogramGrid g)
        {
            this.grid = g;
        }
        public bool UpdateHints()
        {
            hintCells.Clear();

            // PRIORITÁS: Hibajavítás
            for (int i = 0; i < grid.row; i++)
            {
                for (int j = 0; j < grid.col; j++)
                {
                    bool shouldBeFilled = !grid.isColor
                        ? grid.solutionBW[i, j] == 1
                        : grid.solutionColorRGB[i, j] != Color.White;

                    bool currentlyFilled = grid.userColorRGB[i, j] != Color.White;

                    if (currentlyFilled && !shouldBeFilled)
                    {
                        hintCells.Add(new Point(i, j));
                        return true;
                    }
                }
            }

            // PRIORITÁS: Overlap logika
            if (CheckOverlapHints(true)) return true;
            if (CheckOverlapHints(false)) return true;

            // PRIORITÁS: Következő hiányzó biztos pont
            for (int i = 0; i < grid.row; i++)
            {
                for (int j = 0; j < grid.col; j++)
                {
                    bool shouldBeFilled = !grid.isColor
                        ? grid.solutionBW[i, j] == 1
                        : grid.solutionColorRGB[i, j] != Color.White;

                    bool currentlyFilled = grid.userColorRGB[i, j] != Color.White;

                    if (shouldBeFilled && !currentlyFilled && !grid.userXMark[i, j])
                    {
                        hintCells.Add(new Point(i, j));
                        return true;
                    }
                }
            }

            return false;
        }
        private bool CheckOverlapHints(bool isRow)
        {
            int limit = isRow ? grid.row : grid.col;
            int length = isRow ? grid.col : grid.row;

            for (int i = 0; i < limit; i++)
            {
                List<int> hints = isRow ? GetRowHints(i) : GetColHints(i);
                if (hints.Count == 0) continue;

                bool[] sureCells = CalculateOverlap(hints, length, isRow, i);

                for (int j = 0; j < length; j++)
                {
                    int r = isRow ? i : j;
                    int c = isRow ? j : i;

                    bool shouldBeFilled = !grid.isColor
                        ? grid.solutionBW[r, c] == 1
                        : grid.solutionColorRGB[r, c] != Color.White;

                    if (sureCells[j] &&
                        shouldBeFilled &&
                        grid.userColorRGB[r, c] == Color.White &&
                        !grid.userXMark[r, c])
                    {
                        hintCells.Add(new Point(r, c));
                        return true;
                    }
                }
            }

            return false;
        }
        private bool[] CalculateOverlap(List<int> hints, int length, bool isRow, int index)
        {
            int[] leftMap = new int[length];
            int[] rightMap = new int[length];

            for (int i = 0; i < length; i++)
            {
                leftMap[i] = -1;
                rightMap[i] = -1;
            }

            // Legbaloldalibb
            int pos = 0;
            for (int b = 0; b < hints.Count; b++)
            {
                int h = hints[b];
                bool found = false;

                while (pos <= length - h)
                {
                    if (CanPlaceBlock(index, pos, h, isRow))
                    {
                        for (int k = 0; k < h; k++)
                            leftMap[pos + k] = b;

                        pos += h + 1;
                        found = true;
                        break;
                    }

                    int r = isRow ? index : pos;
                    int c = isRow ? pos : index;

                    if (grid.userColorRGB[r, c] != Color.White)
                        break;

                    pos++;
                }

                if (!found)
                    return new bool[length];
            }

            // Legjobboldalibb
            pos = length - 1;
            for (int b = hints.Count - 1; b >= 0; b--)
            {
                int h = hints[b];
                bool found = false;

                while (pos >= h - 1)
                {
                    if (CanPlaceBlock(index, pos - h + 1, h, isRow))
                    {
                        for (int k = 0; k < h; k++)
                            rightMap[pos - k] = b;

                        pos -= h + 1;
                        found = true;
                        break;
                    }

                    int r = isRow ? index : pos;
                    int c = isRow ? pos : index;

                    if (grid.userColorRGB[r, c] != Color.White)
                        break;

                    pos--;
                }

                if (!found)
                    return new bool[length];
            }

            bool[] overlap = new bool[length];

            for (int i = 0; i < length; i++)
                overlap[i] = (leftMap[i] != -1 && leftMap[i] == rightMap[i]);

            return overlap;
        }
        private bool CanPlaceBlock(int lineIdx, int start, int len, bool isRow)
        {
            int lineLength = isRow ? grid.col : grid.row;

            // Nem lehet X a blokkban
            for (int i = 0; i < len; i++)
            {
                int r = isRow ? lineIdx : start + i;
                int c = isRow ? start + i : lineIdx;

                if (grid.userXMark[r, c])
                    return false;
            }

            // Előtte nem lehet szín
            if (start > 0)
            {
                int r = isRow ? lineIdx : start - 1;
                int c = isRow ? start - 1 : lineIdx;

                if (grid.userColorRGB[r, c] != Color.White)
                    return false;
            }

            // Utána nem lehet szín
            if (start + len < lineLength)
            {
                int r = isRow ? lineIdx : start + len;
                int c = isRow ? start + len : lineIdx;

                if (grid.userColorRGB[r, c] != Color.White)
                    return false;
            }

            return true;
        }
        private List<int> GetRowHints(int rowIndex)
        {
            List<int> hints = new List<int>();
            int count = 0;

            for (int j = 0; j < grid.col; j++)
            {
                bool filled = !grid.isColor
                    ? grid.solutionBW[rowIndex, j] == 1
                    : grid.solutionColorRGB[rowIndex, j] != Color.White;

                if (filled)
                    count++;
                else if (count > 0)
                {
                    hints.Add(count);
                    count = 0;
                }
            }

            if (count > 0)
                hints.Add(count);

            return hints;
        }

        private List<int> GetColHints(int colIndex)
        {
            List<int> hints = new List<int>();
            int count = 0;

            for (int i = 0; i < grid.row; i++)
            {
                bool filled = !grid.isColor
                    ? grid.solutionBW[i, colIndex] == 1
                    : grid.solutionColorRGB[i, colIndex] != Color.White;

                if (filled)
                    count++;
                else if (count > 0)
                {
                    hints.Add(count);
                    count = 0;
                }
            }

            if (count > 0)
                hints.Add(count);

            return hints;
        }
    }
}