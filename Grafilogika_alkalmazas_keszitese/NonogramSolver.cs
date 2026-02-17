using System;
using System.Drawing;
using System.Linq;

namespace Grafilogika_alkalmazas_keszitese
{
    public class NonogramSolver
    {
        private int solutionsFound;
        private DateTime solverStartTime;
        private NonogramGrid grid;

        public NonogramSolver(NonogramGrid g)
        {
            this.grid = g;
        }
        public void SetGrid(NonogramGrid grid)
        {
            this.grid = grid;
        }
        public bool IsUniqueSolution()
        {
            solutionsFound = 0;
            solverStartTime = DateTime.Now;
            int[,] testBoard = new int[grid.row, grid.col];
            Color[,] testColors = new Color[grid.row, grid.col];

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

            if (r == grid.row)
            {
                solutionsFound++;
                return;
            }

            int nextR = (c + 1 == grid.col) ? r + 1 : r;
            int nextC = (c + 1 == grid.col) ? 0 : c + 1;

            // Próbálkozás: Üres cella (0)
            board[r, c] = 0;
            boardColors[r, c] = Color.White;
            if (IsValidPartial(r, c, board, boardColors))
            {
                SolveRecursive(nextR, nextC, board, boardColors);
            }

            if (solutionsFound > 1) return;

            // Próbálkozás: Színes/Fekete cella (1)
            Color[] possibleColors = grid.isColor ? grid.nonogramColors : new Color[] { Color.Black };
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
            bool isComplete = isRow ? (c == grid.col - 1) : (r == grid.row - 1);
            int[] clues = isRow ? grid.rowClues[r] : grid.colClues[c];
            Color[] clueColors = isRow ? grid.rowClueColors[r] : grid.colClueColors[c];

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
                    else if (grid.isColor && col != currentBlockColor)
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