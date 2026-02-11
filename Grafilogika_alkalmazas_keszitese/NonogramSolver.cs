using System;
using System.Drawing;
using System.Linq;

namespace Grafilogika_alkalmazas_keszitese
{
    public class NonogramSolver
    {
        private int solutionsFound;
        private DateTime solverStartTime;
        private int rows;
        private int cols;
        private int[][] rowClues;
        private int[][] colClues;
        private Color[][] rowClueColors;
        private Color[][] colClueColors;
        private bool isColor;
        private Color[] nonogramColors;
        private Nonogram form;

        public NonogramSolver(Nonogram f)
        {
            this.form = f;
            this.rows = f.row;
            this.cols = f.col;
            this.rowClues = f.rowClues;
            this.colClues = f.colClues;
            this.rowClueColors = f.rowClueColors;
            this.colClueColors = f.colClueColors;
            this.isColor = f.isColor;
            this.nonogramColors = f.nonogramColors;
        }

        public bool IsUniqueSolution()
        {
            solutionsFound = 0;
            solverStartTime = DateTime.Now;
            int[,] testBoard = new int[rows, cols];
            Color[,] testColors = new Color[rows, cols];

            SolveRecursive(0, 0, testBoard, testColors);

            // Timeout kezelés: ha túl sokáig tart, bizonytalanság miatt false
            if ((DateTime.Now - solverStartTime).TotalMilliseconds > 950)
                return false;

            return solutionsFound == 1;
        }

        private void SolveRecursive(int r, int c, int[,] board, Color[,] boardColors)
        {
            if (solutionsFound > 1) return;
            //if ((DateTime.Now - solverStartTime).TotalMilliseconds > 500) return;

            if (r == rows)
            {
                solutionsFound++;
                return;
            }

            int nextR = (c + 1 == cols) ? r + 1 : r;
            int nextC = (c + 1 == cols) ? 0 : c + 1;

            // Próbálkozás: Üres cella (0)
            board[r, c] = 0;
            boardColors[r, c] = Color.White;
            if (IsValidPartial(r, c, board, boardColors))
            {
                SolveRecursive(nextR, nextC, board, boardColors);
            }

            if (solutionsFound > 1) return;

            // Próbálkozás: Színes/Fekete cella (1)
            Color[] possibleColors = isColor ? nonogramColors : new Color[] { Color.Black };
            foreach (Color color in possibleColors)
            {
                board[r, c] = 1;
                boardColors[r, c] = color;
                if (IsValidPartial(r, c, board, boardColors))
                {
                    SolveRecursive(nextR, nextC, board, boardColors);
                }
            }
        }

        private bool IsValidPartial(int r, int c, int[,] board, Color[,] boardColors)
        {
            return IsLineMatchOptimized(r, c, true, board, boardColors) &&
                   IsLineMatchOptimized(r, c, false, board, boardColors);
        }

        private bool IsLineMatchOptimized(int r, int c, bool isRow, int[,] board, Color[,] boardColors)
        {
            int limit = isRow ? c + 1 : r + 1;
            bool isComplete = isRow ? (c == cols - 1) : (r == rows - 1);
            int[] clues = isRow ? rowClues[r] : colClues[c];
            Color[] clueColors = isRow ? rowClueColors[r] : colClueColors[c];

            int currentClueIdx = 0;
            int currentBlockSize = 0;
            Color currentBlockColor = Color.Empty;

            for (int i = 0; i < limit; i++)
            {
                int val = isRow ? board[r, i] : board[i, c];
                Color col = isRow ? boardColors[r, i] : boardColors[i, c];

                if (val == 1)
                {
                    if (currentBlockSize == 0)
                    {
                        currentBlockColor = col;
                        currentBlockSize = 1;
                    }
                    else if (isColor && col != currentBlockColor)
                    {
                        if (currentClueIdx >= clues.Length || currentBlockSize != clues[currentClueIdx] || currentBlockColor != clueColors[currentClueIdx])
                            return false;
                        currentClueIdx++;
                        currentBlockColor = col;
                        currentBlockSize = 1;
                    }
                    else
                    {
                        currentBlockSize++;
                    }

                    if (currentClueIdx >= clues.Length || currentBlockSize > clues[currentClueIdx])
                        return false;
                }
                else if (currentBlockSize > 0)
                {
                    if (currentBlockSize != clues[currentClueIdx] || currentBlockColor != clueColors[currentClueIdx])
                        return false;
                    currentClueIdx++;
                    currentBlockSize = 0;
                }
            }

            if (isComplete)
            {
                if (currentBlockSize > 0)
                {
                    if (currentClueIdx != clues.Length - 1 || currentBlockSize != clues[currentClueIdx] || currentBlockColor != clueColors[currentClueIdx])
                        return false;
                    currentClueIdx++;
                }
                return currentClueIdx == clues.Length;
            }

            return true;
        }
    }
}