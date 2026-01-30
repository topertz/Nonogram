using System.Collections.Generic;
using System.Linq;

namespace Nonogram
{
    public class NonogramSolver
    {
        private int rows, cols;
        private int[][] rowClues, colClues;
        private int[,] grid;
        private int solutions;

        public int CountSolutions(int[][] rowClues, int[][] colClues, int max = 2)
        {
            this.rowClues = rowClues;
            this.colClues = colClues;
            rows = rowClues.Length;
            cols = colClues.Length;

            grid = new int[rows, cols];

            // FONTOS: minden cella ismeretlen
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                    grid[i, j] = -1;

            solutions = 0;
            Solve(0, 0, max);
            return solutions;
        }

        private void Solve(int r, int c, int max)
        {
            if (solutions >= max) return;

            if (r == rows)
            {
                if (CheckAll())
                    solutions++;
                return;
            }

            int nr = (c + 1 == cols) ? r + 1 : r;
            int nc = (c + 1) % cols;

            // üres
            grid[r, c] = 0;
            if (PartialCheck(r, c))
                Solve(nr, nc, max);

            // kitöltött
            grid[r, c] = 1;
            if (PartialCheck(r, c))
                Solve(nr, nc, max);

            // visszaállítás
            grid[r, c] = -1;
        }

        private bool PartialCheck(int r, int c)
        {
            return CheckLinePartial(rowClues[r], r, true)
                && CheckLinePartial(colClues[c], c, false);
        }

        private bool CheckAll()
        {
            for (int i = 0; i < rows; i++)
                if (!CheckLine(rowClues[i], i, true)) return false;

            for (int j = 0; j < cols; j++)
                if (!CheckLine(colClues[j], j, false)) return false;

            return true;
        }

        private bool CheckLine(int[] clues, int index, bool isRow)
        {
            List<int> found = new List<int>();
            int run = 0;
            int len = isRow ? cols : rows;

            for (int i = 0; i < len; i++)
            {
                int v = isRow ? grid[index, i] : grid[i, index];

                if (v == 1) run++;
                else if (run > 0)
                {
                    found.Add(run);
                    run = 0;
                }
            }

            if (run > 0) found.Add(run);
            return found.SequenceEqual(clues);
        }

        private bool CheckLinePartial(int[] clues, int index, bool isRow)
        {
            int clueIndex = 0;
            int run = 0;
            int len = isRow ? cols : rows;

            for (int i = 0; i < len; i++)
            {
                int v = isRow ? grid[index, i] : grid[i, index];

                if (v == 1)
                {
                    run++;
                    if (clueIndex >= clues.Length || run > clues[clueIndex])
                        return false;
                }
                else if (v == 0)
                {
                    if (run > 0)
                    {
                        clueIndex++;
                        run = 0;
                    }
                }
                // -1 → ismeretlen → nem zárunk le blokkot
            }

            // minimum szükséges hely a maradék clue-khoz
            int minNeeded = 0;
            for (int i = clueIndex; i < clues.Length; i++)
                minNeeded += clues[i];

            if (clues.Length - clueIndex > 0)
                minNeeded += clues.Length - clueIndex - 1;

            return minNeeded <= len;
        }
    }
}