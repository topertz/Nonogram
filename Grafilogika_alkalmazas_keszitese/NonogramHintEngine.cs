using System;
using System.Collections.Generic;
using System.Drawing;

namespace Grafilogika_alkalmazas_keszitese
{
    public class NonogramHintEngine
    {
        private int row;
        private int col;
        private bool isColor;

        private int[,] solutionBW;
        private Color[,] solutionColorRGB;

        private Color[,] userColorRGB;
        private bool[,] userXMark;
        private Nonogram form;

        public List<Point> HintCells { get; private set; } = new List<Point>();

        public NonogramHintEngine(
            Nonogram f, int row,
            int col,
            bool isColor,
            int[,] solutionBW,
            Color[,] solutionColorRGB,
            Color[,] userColorRGB,
            bool[,] userXMark)
        {
            this.form = f;
            this.row = row;
            this.col = col;
            this.isColor = isColor;
            this.solutionBW = solutionBW;
            this.solutionColorRGB = solutionColorRGB;
            this.userColorRGB = userColorRGB;
            this.userXMark = userXMark;
        }

        public void SetHintEngine(Nonogram f, int r, int c, bool colorMode, int[,] solBW, Color[,] solColor, Color[,] userColor, bool[,] userX)
        {
            this.form = f;
            this.row = r;
            this.col = c;
            this.isColor = colorMode;
            this.solutionBW = solBW;
            this.solutionColorRGB = solColor;
            this.userColorRGB = userColor;
            this.userXMark = userX;
        }

        // =========================================
        // PUBLIC ENTRY POINT
        // =========================================
        public bool UpdateHints()
        {
            HintCells.Clear();

            // 1. PRIORITÁS: Hibajavítás
            for (int i = 0; i < row; i++)
            {
                for (int j = 0; j < col; j++)
                {
                    bool shouldBeFilled = !isColor
                        ? solutionBW[i, j] == 1
                        : solutionColorRGB[i, j] != Color.White;

                    bool currentlyFilled = userColorRGB[i, j] != Color.White;

                    if (currentlyFilled && !shouldBeFilled)
                    {
                        HintCells.Add(new Point(i, j));
                        return true;
                    }
                }
            }

            // 2. PRIORITÁS: Overlap logika
            if (CheckOverlapHints(true)) return true;
            if (CheckOverlapHints(false)) return true;

            // 3. PRIORITÁS: Következő hiányzó biztos pont
            for (int i = 0; i < row; i++)
            {
                for (int j = 0; j < col; j++)
                {
                    bool shouldBeFilled = !isColor
                        ? solutionBW[i, j] == 1
                        : solutionColorRGB[i, j] != Color.White;

                    bool currentlyFilled = userColorRGB[i, j] != Color.White;

                    if (shouldBeFilled && !currentlyFilled && !userXMark[i, j])
                    {
                        HintCells.Add(new Point(i, j));
                        return true;
                    }
                }
            }

            return false;
        }

        // =========================================
        // OVERLAP CHECK
        // =========================================
        private bool CheckOverlapHints(bool isRow)
        {
            int limit = isRow ? row : col;
            int length = isRow ? col : row;

            for (int i = 0; i < limit; i++)
            {
                List<int> hints = isRow ? GetRowHints(i) : GetColHints(i);
                if (hints.Count == 0) continue;

                bool[] sureCells = CalculateOverlap(hints, length, isRow, i);

                for (int j = 0; j < length; j++)
                {
                    int r = isRow ? i : j;
                    int c = isRow ? j : i;

                    bool shouldBeFilled = !isColor
                        ? solutionBW[r, c] == 1
                        : solutionColorRGB[r, c] != Color.White;

                    if (sureCells[j] &&
                        shouldBeFilled &&
                        userColorRGB[r, c] == Color.White &&
                        !userXMark[r, c])
                    {
                        HintCells.Add(new Point(r, c));
                        return true;
                    }
                }
            }

            return false;
        }

        // =========================================
        // OVERLAP CALCULATION
        // =========================================
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

                    if (userColorRGB[r, c] != Color.White)
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

                    if (userColorRGB[r, c] != Color.White)
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

        // =========================================
        // BLOCK VALIDATION
        // =========================================
        private bool CanPlaceBlock(int lineIdx, int start, int len, bool isRow)
        {
            int lineLength = isRow ? col : row;

            // 1. Nem lehet X a blokkban
            for (int i = 0; i < len; i++)
            {
                int r = isRow ? lineIdx : start + i;
                int c = isRow ? start + i : lineIdx;

                if (userXMark[r, c])
                    return false;
            }

            // 2. Előtte nem lehet szín
            if (start > 0)
            {
                int r = isRow ? lineIdx : start - 1;
                int c = isRow ? start - 1 : lineIdx;

                if (userColorRGB[r, c] != Color.White)
                    return false;
            }

            // 3. Utána nem lehet szín
            if (start + len < lineLength)
            {
                int r = isRow ? lineIdx : start + len;
                int c = isRow ? start + len : lineIdx;

                if (userColorRGB[r, c] != Color.White)
                    return false;
            }

            return true;
        }

        // =========================================
        // HINT GENERATION
        // =========================================
        private List<int> GetRowHints(int rowIndex)
        {
            List<int> hints = new List<int>();
            int count = 0;

            for (int j = 0; j < col; j++)
            {
                bool filled = !isColor
                    ? solutionBW[rowIndex, j] == 1
                    : solutionColorRGB[rowIndex, j] != Color.White;

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

            for (int i = 0; i < row; i++)
            {
                bool filled = !isColor
                    ? solutionBW[i, colIndex] == 1
                    : solutionColorRGB[i, colIndex] != Color.White;

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