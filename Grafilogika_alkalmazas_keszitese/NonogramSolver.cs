using System;
using System.Drawing;
using System.Linq;

namespace Grafilogika_alkalmazas_keszitese
{
    public class NonogramSolver
    {
        private int solutionsFound;
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
            int[,] testBoard = new int[grid.row, grid.col];
            Color[,] testColors = new Color[grid.row, grid.col];

            SolveRecursive(0, 0, testBoard, testColors);

            return solutionsFound == 1;
        }

        private void SolveRecursive(int rowIndex, int colIndex, int[,] board, Color[,] boardColors)
        {
            if (solutionsFound > 1) return;

            if (rowIndex == grid.row)
            {
                solutionsFound++;
                return;
            }

            int nextRow = (colIndex + 1 == grid.col) ? rowIndex + 1 : rowIndex;
            int nextCol = (colIndex + 1 == grid.col) ? 0 : colIndex + 1;

            // Üres cella (0)
            board[rowIndex, colIndex] = 0;
            boardColors[rowIndex, colIndex] = Color.White;
            if (IsValidPartial(rowIndex, colIndex, board, boardColors))
            {
                SolveRecursive(nextRow, nextCol, board, boardColors);
            }

            if (solutionsFound > 1) return;

            // Színes/Fekete cella (1)
            Color[] possibleColors = grid.isColor ? grid.nonogramPalette : new Color[] { Color.Black };
            foreach (Color color in possibleColors)
            {
                board[rowIndex, colIndex] = 1;
                boardColors[rowIndex, colIndex] = color;
                if (IsValidPartial(rowIndex, colIndex, board, boardColors))
                {
                    SolveRecursive(nextRow, nextCol, board, boardColors);
                }
            }
        }

        private bool IsValidPartial(int rowIndex, int colIndex, int[,] board, Color[,] boardColors)
        {
            return IsLineMatchOptimized(rowIndex, colIndex, true, board, boardColors) &&
                   IsLineMatchOptimized(rowIndex, colIndex, false, board, boardColors);
        }

        private bool IsLineMatchOptimized(int rowIndex, int colIndex, bool isRow, int[,] board, Color[,] boardColors)
        {
            int limit = isRow ? colIndex + 1 : rowIndex + 1;
            bool isComplete = isRow ? (colIndex == grid.col - 1) : (rowIndex == grid.row - 1);
            int[] clues = isRow ? grid.rowClues[rowIndex] : grid.colClues[colIndex];
            Color[] clueColors = isRow ? grid.rowClueColors[rowIndex] : grid.colClueColors[colIndex];

            int currentClueIdx = 0;
            int currentBlockSize = 0;
            Color currentBlockColor = Color.Empty;

            for (int cellIndex = 0; cellIndex < limit; cellIndex++)
            {
                int val = isRow ? board[rowIndex, cellIndex] : board[cellIndex, colIndex];
                Color col = isRow ? boardColors[rowIndex, cellIndex] : boardColors[cellIndex, colIndex];

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

        public bool HasLogicalStartMoves()
        {
            int[,] testBoard = new int[grid.row, grid.col];
            Color[,] testColors = new Color[grid.row, grid.col];
            bool progress = false;

            for (int rowIndex = 0; rowIndex < grid.row; rowIndex++)
                if (RowHasOverlapColored(rowIndex, testBoard, testColors))
                    progress = true;

            for (int colIndex = 0; colIndex < grid.col; colIndex++)
                if (ColHasOverlapColored(colIndex, testBoard, testColors))
                    progress = true;

            return progress;
        }

        private bool RowHasOverlapColored(int rowIndex, int[,] board, Color[,] colors)
        {
            int width = grid.col;
            int[] clues = grid.rowClues[rowIndex];
            Color[] clueColors = grid.rowClueColors[rowIndex];

            if (clues.Length == 0) return false;

            bool progress = false;
            int[] leftMost = new int[clues.Length];
            int[] rightMost = new int[clues.Length];

            // balról elhelyezés
            int pos = 0;
            for (int clueIndex = 0; clueIndex < clues.Length; clueIndex++)
            {
                leftMost[clueIndex] = pos;
                pos += clues[clueIndex] + 1;
            }

            // jobbról elhelyezés
            pos = width;
            for (int clueIndex = clues.Length - 1; clueIndex >= 0; clueIndex--)
            {
                pos -= clues[clueIndex];
                rightMost[clueIndex] = pos;
                pos -= 1;
            }

            // átfedés kitöltés színek szerint
            for (int clueIndex = 0; clueIndex < clues.Length; clueIndex++)
            {
                int start = Math.Max(leftMost[clueIndex], rightMost[clueIndex]);
                int end = Math.Min(leftMost[clueIndex] + clues[clueIndex], rightMost[clueIndex] + clues[clueIndex]);
                Color clueColor = clueColors[clueIndex];

                for (int colIndex = start; colIndex < end; colIndex++)
                {
                    if (board[rowIndex, colIndex] == 0)
                    {
                        board[rowIndex, colIndex] = 1;
                        colors[rowIndex, colIndex] = clueColor;
                        progress = true;
                    }
                    else if (board[rowIndex, colIndex] == 1 && colors[rowIndex, colIndex] != clueColor)
                    {
                        // Ütközés más színnel – nem lehet kitölteni
                        continue;
                    }
                }
            }

            return progress;
        }

        private bool ColHasOverlapColored(int colIndex, int[,] board, Color[,] colors)
        {
            int height = grid.row;
            int[] clues = grid.colClues[colIndex];
            Color[] clueColors = grid.colClueColors[colIndex];

            if (clues.Length == 0) return false;

            bool progress = false;
            int[] topMost = new int[clues.Length];
            int[] bottomMost = new int[clues.Length];

            // felülről elhelyezés
            int pos = 0;
            for (int clueIndex = 0; clueIndex < clues.Length; clueIndex++)
            {
                topMost[clueIndex] = pos;
                pos += clues[clueIndex] + 1;
            }

            // alulról elhelyezés 
            pos = height;
            for (int clueIndex = clues.Length - 1; clueIndex >= 0; clueIndex--)
            {
                pos -= clues[clueIndex];
                bottomMost[clueIndex] = pos;
                pos -= 1;
            }

            // átfedés kitöltés színek szerint
            for (int clueIndex = 0; clueIndex < clues.Length; clueIndex++)
            {
                int start = Math.Max(topMost[clueIndex], bottomMost[clueIndex]);
                int end = Math.Min(topMost[clueIndex] + clues[clueIndex], bottomMost[clueIndex] + clues[clueIndex]);
                Color clueColor = clueColors[clueIndex];

                for (int rowIndex = start; rowIndex < end; rowIndex++)
                {
                    if (board[rowIndex, colIndex] == 0)
                    {
                        board[rowIndex, colIndex] = 1;
                        colors[rowIndex, colIndex] = clueColor;
                        progress = true;
                    }
                    else if (board[rowIndex, colIndex] == 1 && colors[rowIndex, colIndex] != clueColor)
                    {
                        // Ütközés más színnel – nem lehet kitölteni
                        continue;
                    }
                }
            }

            return progress;
        }
    }
}