using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Grafilogika_alkalmazas_keszitese
{
    public partial class NonogramSmartAI
    {
        private Nonogram form;
        private NonogramGrid grid;
        private NonogramRender render;
        private GameTimerManager gameTimerManager;

        public NonogramSmartAI(Nonogram f, NonogramGrid g, NonogramRender r, GameTimerManager game)
        {
            this.form = f;
            this.grid = g;
            this.render = r;
            this.gameTimerManager = game;
        }

        public void SetGrid(NonogramGrid g)
        {
            grid = g;
        }
        public void SetRender(NonogramRender r)
        {
            render = r;
        }

        public void SetTimerManager(GameTimerManager g)
        {
            gameTimerManager = g;
        }

        public async void BtnSmartAIGuide_Click(object sender, EventArgs e)
        {
            render.ClearErrorHighlights();
            gameTimerManager.Stop();
            grid.aiButtonClicked = true;
            form.btnSmartAI.Enabled = false;
            form.picPreview.Visible = true;
            await Task.Delay(500);
            for (int rowIndex = 0; rowIndex < grid.row; rowIndex++)
            {
                for (int colIndex = 0; colIndex < grid.col; colIndex++)
                {
                    grid.gridButtons[rowIndex, colIndex].Enabled = false;
                    grid.userColorRGB[rowIndex, colIndex] = Color.White;
                    grid.userXMark[rowIndex, colIndex] = false;
                    grid.gridButtons[rowIndex, colIndex].Text = "";
                    grid.gridButtons[rowIndex, colIndex].BackColor = Color.White;
                }
            }
            render.UpdatePreview();
            bool solved = await SolveWithSpeculation();

            if (solved)
            {
                MessageBox.Show("AI solved the Nonogram! Restart the game with the Generate New Puzzle button!",
                    "AI ready", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("AI can't find any more safe moves.", "AI ready",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            form.btnSmartAI.Enabled = true;
            form.btnSolve.Enabled = false;
            form.btnHint.Enabled = false;
            form.btnCheck.Enabled = false;
            form.btnUndo.Enabled = false;
            form.btnRedo.Enabled = false;
            form.btnBackToHome.Enabled = true;
        }
        // Recursive backtracking + logical steps
        public async Task<bool> SolveWithSpeculation()
        {
            // Logical steps (these are the sure points)
            bool changed;
            do
            {
                changed = false;
                for (int rowIndex = 0; rowIndex < grid.row; rowIndex++)
                {
                    if (await AnalyzeLine(rowIndex, true))
                    {
                        changed = true;
                        break;
                    }
                }
                if (changed)
                {
                    continue;
                }
                for (int colIndex = 0; colIndex < grid.col; colIndex++)
                {
                    if (await AnalyzeLine(colIndex, false))
                    {
                        changed = true;
                        break;
                    }
                }
            } while (changed);

            if (IsBoardCompletelyValid())
            {
                return true;
            }

            // Speculation (Backtracking)
            for (int rowIndex = 0; rowIndex < grid.row; rowIndex++)
            {
                for (int colIndex = 0; colIndex < grid.col; colIndex++)
                {
                    // We only check empty cells
                    if (!IsFilled(rowIndex, colIndex) && !IsX(rowIndex, colIndex))
                    {
                        // We collect what are the legal colors in the given row and column
                        HashSet<int> allowedInRow = grid.rowClueColors[rowIndex].Select(clr => clr.ToArgb()).ToHashSet();
                        HashSet<int> allowedInCol = grid.colClueColors[colIndex].Select(clr => clr.ToArgb()).ToHashSet();

                        // only the color that is in both sets can be considered
                        // If the row only contains brown (allowedInRow), but the column also contains purple, 
                        // purple will NOT be included in the validColors list.
                        List<int> validColors = allowedInRow.Intersect(allowedInCol).ToList();

                        // If there is no common color, then it is mathematically impossible to paint anything there -> X
                        if (validColors.Count == 0)
                        {
                            if (await SetX(rowIndex, colIndex,
                                "The row and column rules are mutually exclusive (no common color)"))
                            {
                                return await SolveWithSpeculation();
                            }
                            return false;
                        }

                        // Request only blocks with valid colors
                        List<(bool isRow, int lineIdx, int blockIdx)> candidates =
                            GetCandidateBlocks(rowIndex, colIndex).ToList();

                        foreach ((bool isRow, int lineIdx, int blockIdx) in candidates)
                        {
                            Color blockColor = GetClueColor(lineIdx, isRow, blockIdx);
                            int blockArgb = blockColor.ToArgb();

                            // If the block color is not included in the cell's section colors, we skip it
                            if (!validColors.Contains(blockArgb))
                            {
                                continue;
                            }

                            int length = isRow ? grid.col : grid.row;
                            int[] clues = isRow ? grid.rowClues[lineIdx] : grid.colClues[lineIdx];
                            int blockLen = clues[blockIdx];

                            int[] leftmost = GetLeftmost(lineIdx, isRow, length, clues);
                            int[] rightmost = GetRightmost(lineIdx, isRow, length, clues);
                            if (leftmost == null || rightmost == null)
                            {
                                continue;
                            }

                            for (int start = leftmost[blockIdx]; start <= rightmost[blockIdx]; start++)
                            {
                                int posInLine = isRow ? colIndex : rowIndex;
                                if (posInLine < start || posInLine >= start + blockLen)
                                {
                                    continue;
                                }

                                // Check if the block can be placed
                                if (!CanPlaceBlock(lineIdx, isRow, start, blockLen, blockColor))
                                {
                                    continue;
                                }
                                if (!IsPlacementOrderValid(lineIdx, isRow, blockIdx, start, blockLen))
                                {
                                    continue;
                                }

                                // Check cross-color legitimacy
                                bool crossCheck = true;
                                for (int pos = 0; pos < blockLen; pos++)
                                {
                                    int targetRow = isRow ? lineIdx : start + pos;
                                    int targetCol = isRow ? start + pos : lineIdx;
                                    if (!IsColorLegalAtPosition(targetRow, targetCol, blockColor, !isRow))
                                    {
                                        crossCheck = false;
                                        break;
                                    }
                                }
                                if (!crossCheck)
                                {
                                    continue;
                                }

                                // save and test
                                Color[,] backupColors = (Color[,])grid.userColorRGB.Clone();
                                bool[,] backupX = (bool[,])grid.userXMark.Clone();

                                for (int pos = 0; pos < blockLen; pos++)
                                {
                                    int currentRow = isRow ? lineIdx : start + pos;
                                    int currentCol = isRow ? start + pos : lineIdx;
                                    await SetCell(currentRow, currentCol, blockColor,
                                        "Speculative placement (backtracking)");
                                }

                                if (await SolveWithSpeculation())
                                {
                                    return true;
                                }

                                // step back
                                grid.userColorRGB = backupColors;
                                grid.userXMark = backupX;
                                render.UpdatePreview();
                            }
                        }

                        // If none of the legal colors lead to a solution, we try X
                        if (await SetX(rowIndex, colIndex, "None of the possible colors worked at this point."))
                        {
                            if (await SolveWithSpeculation())
                            {
                                return true;
                            }
                        }

                        return false;
                    }
                }
            }
            return IsBoardCompletelyValid();
        }

        private bool IsColorLegalAtPosition(int rowIndex, int colIndex, Color colorToPlace, bool checkColumn)
        {
            // Retrieve data according to the direction being examined
            int lineIdx = checkColumn ? colIndex : rowIndex;
            int posInLine = checkColumn ? rowIndex : colIndex;
            int lineLength = checkColumn ? grid.row : grid.col;
            int[] clues = checkColumn ? grid.colClues[lineIdx] : grid.rowClues[lineIdx];
            Color[] clueColors = checkColumn ? grid.colClueColors[lineIdx] : grid.rowClueColors[lineIdx];

            // Calculate boundaries based on the current board (X's and colors count)
            int[] leftmost = GetLeftmost(lineIdx, !checkColumn, lineLength, clues);
            int[] rightmost = GetRightmost(lineIdx, !checkColumn, lineLength, clues);

            if (leftmost == null || rightmost == null)
            {
                return false;
            }

            // Check if the color falls within any of the possible block ranges
            for (int clueIndex = 0; clueIndex < clues.Length; clueIndex++)
            {
                if (clueColors[clueIndex].ToArgb() == colorToPlace.ToArgb())
                {
                    if (posInLine >= leftmost[clueIndex] && posInLine <= (rightmost[clueIndex] + clues[clueIndex] - 1))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // This method prevents the colors from being swapped
        private bool IsPlacementOrderValid(int lineIdx, bool isRow, int blockIdx, int start, int len)
        {
            int[] clues = isRow ? grid.rowClues[lineIdx] : grid.colClues[lineIdx];

            // There must be no empty painted cell before the block that does not belong to the previous blocks
            for (int pos = 0; pos < start; pos++)
            {
                int rowIndex = isRow ? lineIdx : pos;
                int colIndex = isRow ? pos : lineIdx;
                if (IsFilled(rowIndex, colIndex))
                {
                    Color pixelColor = grid.userColorRGB[rowIndex, colIndex];
                    bool canBePreviousBlock = false;
                    for (int prev = 0; prev < blockIdx; prev++)
                    {
                        if (GetClueColor(lineIdx, isRow, prev).ToArgb() == pixelColor.ToArgb())
                        {
                            canBePreviousBlock = true;
                            break;
                        }
                    }
                    if (!canBePreviousBlock)
                    {
                        return false;
                    }
                }
            }
            return true;
        }
        // Returns the blocks to which the cell may belong
        private IEnumerable<(bool isRow, int lineIdx, int blockIdx)> GetCandidateBlocks(int rowIndex, int colIndex)
        {
            // Collect what colors are allowed in the row and column
            HashSet<int> allowedColorsInRow = grid.rowClueColors[rowIndex].Select(clr => clr.ToArgb()).ToHashSet();
            HashSet<int> allowedColorsInCol = grid.colClueColors[colIndex].Select(clr => clr.ToArgb()).ToHashSet();

            // Check blocks belonging to a row
            int[] rowCluesLine = grid.rowClues[rowIndex];
            int lengthRow = grid.col;
            int[] leftRow = GetLeftmost(rowIndex, true, lengthRow, rowCluesLine);
            int[] rightRow = GetRightmost(rowIndex, true, lengthRow, rowCluesLine);

            if (leftRow != null && rightRow != null)
            {
                for (int clueIndex = 0; clueIndex < rowCluesLine.Length; clueIndex++)
                {
                    // Check the color of the block
                    Color blockColor = grid.rowClueColors[rowIndex][clueIndex];

                    // only marked if:
                    // the coordinate is correct
                    // and the block color is also included in the column rules
                    if (colIndex >= leftRow[clueIndex] && colIndex <= rightRow[clueIndex] + rowCluesLine[clueIndex] - 1)
                    {
                        if (allowedColorsInCol.Contains(blockColor.ToArgb()))
                        {
                            yield return (true, rowIndex, clueIndex);
                        }
                    }
                }
            }

            // Check blocks belonging to a column
            int[] colCluesLine = grid.colClues[colIndex];
            int lengthCol = grid.row;
            int[] leftCol = GetLeftmost(colIndex, false, lengthCol, colCluesLine);
            int[] rightCol = GetRightmost(colIndex, false, lengthCol, colCluesLine);

            if (leftCol != null && rightCol != null)
            {
                for (int clueIndex = 0; clueIndex < colCluesLine.Length; clueIndex++)
                {
                    // Check the color of the block
                    Color blockColor = grid.colClueColors[colIndex][clueIndex];

                    // marked only if:
                    // the coordinate is correct
                    // and the block color is also included in the row rules
                    if (rowIndex >= leftCol[clueIndex] && rowIndex <= rightCol[clueIndex] + colCluesLine[clueIndex] - 1)
                    {
                        if (allowedColorsInRow.Contains(blockColor.ToArgb()))
                        {
                            yield return (false, colIndex, clueIndex);
                        }
                    }
                }
            }
        }

        private bool IsBoardCompletelyValid()
        {
            for (int rowIndex = 0; rowIndex < grid.row; rowIndex++)
            {
                if (!IsLineValid(rowIndex, true))
                {
                    return false;
                }
            }

            for (int colIndex = 0; colIndex < grid.col; colIndex++)
            {
                if (!IsLineValid(colIndex, false))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsLineValid(int index, bool isRow)
        {
            int length = isRow ? grid.col : grid.row;
            int[] clues = isRow ? grid.rowClues[index] : grid.colClues[index];

            List<(Color color, int len)> foundBlocks = new List<(Color color, int len)>();

            int pos = 0;
            while (pos < length)
            {
                int rowIndex = isRow ? index : pos;
                int colIndex = isRow ? pos : index;

                if (IsFilled(rowIndex, colIndex))
                {
                    Color currentColor = grid.userColorRGB[rowIndex, colIndex];
                    int startPos = pos;

                    // Collect all adjacent cells of the same color
                    while (pos < length)
                    {
                        int currentRow = isRow ? index : pos;
                        int currentCol = isRow ? pos : index;
                        if (IsFilled(currentRow, currentCol) && grid.userColorRGB[currentRow, currentCol].ToArgb()
                            == currentColor.ToArgb())
                        {
                            pos++;
                        }
                        else
                        {
                            break;
                        }
                    }

                    foundBlocks.Add((currentColor, pos - startPos));
                }
                else
                {
                    pos++;
                }
            }

            // Do the numbers of blocks match?
            if (foundBlocks.Count != clues.Length)
            {
                return false;
            }

            // Do all blocks have exactly the same length and color?
            for (int blockIndex = 0; blockIndex < foundBlocks.Count; blockIndex++)
            {
                if (foundBlocks[blockIndex].len != clues[blockIndex])
                {
                    return false;
                }

                if (foundBlocks[blockIndex].color.ToArgb() != GetClueColor(index, isRow, blockIndex).ToArgb())
                {
                    return false;
                }
            }

            return true;
        }
        private async Task<bool> AnalyzeLine(int index, bool isRow)
        {
            int length = isRow ? grid.col : grid.row;
            int[] clues = isRow ? grid.rowClues[index] : grid.colClues[index];
            if (clues == null || clues.Length == 0)
            {
                return false;
            }

            // color-intersection based filtering
            for (int pos = 0; pos < length; pos++)
            {
                int rowIndex = isRow ? index : pos;
                int colIndex = isRow ? pos : index;

                if (IsX(rowIndex, colIndex) || IsFilled(rowIndex, colIndex))
                {
                    continue;
                }

                // Safer color retrieval for the section
                HashSet<int> rowColorSet = grid.rowClues[rowIndex].Select((_, idx) =>
                    GetClueColor(rowIndex, true, idx).ToArgb()).ToHashSet();
                HashSet<int> colColorSet = grid.colClues[colIndex].Select((_, idx) =>
                    GetClueColor(colIndex, false, idx).ToArgb()).ToHashSet();

                rowColorSet.IntersectWith(colColorSet);
                if (rowColorSet.Count == 0)
                {
                    if (await SetX(rowIndex, colIndex, "Color mismatch (row or column section is blank)"))
                    {
                        return true;
                    }
                }
            }

            // cleaning
            if (await HandleLogicCleanup(index, isRow, length, clues, clues.Sum()))
            {
                return true;
            }

            // Recalculate boundaries
            int[] leftmost = GetLeftmost(index, isRow, length, clues);
            int[] rightmost = GetRightmost(index, isRow, length, clues);

            if (leftmost == null || rightmost == null)
            {
                return false;
            }

            // strict color-overlap
            for (int pos = 0; pos < length; pos++)
            {
                int rowIndex = isRow ? index : pos;
                int colIndex = isRow ? pos : index;
                if (IsX(rowIndex, colIndex) || IsFilled(rowIndex, colIndex))
                {
                    continue;
                }

                HashSet<int> potentialBlocks = new HashSet<int>();
                for (int clueIndex = 0; clueIndex < clues.Length; clueIndex++)
                {
                    if (pos >= rightmost[clueIndex] && pos <= leftmost[clueIndex] + clues[clueIndex] - 1)
                    {
                        potentialBlocks.Add(clueIndex);
                    }
                }

                if (potentialBlocks.Count > 0)
                {
                    Color firstColor = GetClueColor(index, isRow, potentialBlocks.First());
                    bool allSame = potentialBlocks.All(idx => GetClueColor(index, isRow, idx).ToArgb()
                        == firstColor.ToArgb());

                    if (allSame)
                    {
                        if (await SetCell(rowIndex, colIndex, firstColor, "Overlap"))
                        {
                            return true;
                        }
                    }
                }
            }

            // availability
            for (int pos = 0; pos < length; pos++)
            {
                int rowIndex = isRow ? index : pos;
                int colIndex = isRow ? pos : index;
                if (IsX(rowIndex, colIndex) || IsFilled(rowIndex, colIndex))
                {
                    continue;
                }

                bool canAnyBlockReach = false;
                for (int clueIndex = 0; clueIndex < clues.Length; clueIndex++)
                {
                    if (pos >= leftmost[clueIndex] && pos <= rightmost[clueIndex] + clues[clueIndex] - 1)
                    {
                        canAnyBlockReach = true;
                        break;
                    }
                }

                if (!canAnyBlockReach)
                {
                    if (await SetX(rowIndex, colIndex, "No block can reach here"))
                    {
                        return true;
                    }
                }
            }

            // special logics
            if (await ConnectionLogic(index, isRow, length, clues, leftmost, rightmost))
            {
                return true;
            }
            if (await ExtendAnchors(index, isRow, length, clues, leftmost, rightmost))
            {
                return true;
            }
            if (await AutoCloseBlocks(index, isRow, length, clues))
            {
                return true;
            }

            // shakedown
            for (int pos = 0; pos < length; pos++)
            {
                int rowIndex = isRow ? index : pos;
                int colIndex = isRow ? pos : index;
                if (IsFilled(rowIndex, colIndex) || IsX(rowIndex, colIndex))
                {
                    continue;
                }

                // Temporary X-mark for testing
                grid.userXMark[rowIndex, colIndex] = true;
                int[] testLeft = GetLeftmost(index, isRow, length, clues);
                grid.userXMark[rowIndex, colIndex] = false;

                if (testLeft == null)
                {
                    // Find all blocks that could theoretically cover this point
                    HashSet<int> possibleColors = new HashSet<int>();
                    for (int clueIndex = 0; clueIndex < clues.Length; clueIndex++)
                    {
                        if (pos >= leftmost[clueIndex] && pos <= rightmost[clueIndex] + clues[clueIndex] - 1)
                        {
                            possibleColors.Add(GetClueColor(index, isRow, clueIndex).ToArgb());
                        }
                    }

                    if (!grid.isColor)
                    {
                        // only try to fill if the cell is empty
                        if (!IsFilled(rowIndex, colIndex))
                        {
                            if (await SetCellSafe(rowIndex, colIndex, Color.Black))
                            {
                                return true;
                            }
                        }
                    }
                    else
                    {
                        if (possibleColors.Count == 1)
                        {
                            Color targetColor = Color.FromArgb(possibleColors.First());
                            if (await SetCell(rowIndex, colIndex, targetColor, "Conflict-based completion"))
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        private async Task<bool> SetCellSafe(int rowIndex, int colIndex, Color colorToSet)
        {
            if (IsFilled(rowIndex, colIndex))
            {
                return false;
            }
            grid.userColorRGB[rowIndex, colIndex] = colorToSet;
            render.SetCellColor(rowIndex, colIndex, grid.gridButtons[rowIndex, colIndex], colorToSet);
            render.UpdatePreview(rowIndex, colIndex);
            form.Refresh();
            await Task.Delay(50);
            return true;
        }

        private async Task<bool> ConnectionLogic(int index, bool isRow, int length, int[] clues, int[] leftmost,
            int[] rightmost)
        {
            for (int clueIndex = 0; clueIndex < clues.Length; clueIndex++)
            {
                int clueLength = clues[clueIndex];
                Color clueColor = GetClueColor(index, isRow, clueIndex);
                int rangeStart = leftmost[clueIndex];
                int rangeEnd = rightmost[clueIndex] + clueLength - 1;

                int firstFoundPos = -1, lastFoundPos = -1;

                for (int pos = rangeStart; pos <= rangeEnd; pos++)
                {
                    int rowIndex = isRow ? index : pos;
                    int colIndex = isRow ? pos : index;

                    if (IsFilled(rowIndex, colIndex) && grid.userColorRGB[rowIndex, colIndex].ToArgb() == clueColor.ToArgb())
                    {
                        // Only anchor if a block of a different color cannot reach here
                        bool belongsToThis = true;
                        for (int otherIdx = 0; otherIdx < clues.Length; otherIdx++)
                        {
                            if (otherIdx == clueIndex)
                            {
                                continue;
                            }
                            if (pos >= leftmost[otherIdx] && pos <= rightmost[otherIdx] + clues[otherIdx] - 1)
                            {
                                belongsToThis = false;
                                break;
                            }
                        }

                        if (belongsToThis)
                        {
                            if (firstFoundPos == -1)
                            {
                                firstFoundPos = pos;
                            }
                            lastFoundPos = pos;
                        }
                    }
                }

                if (firstFoundPos != -1 && lastFoundPos != -1 && lastFoundPos - firstFoundPos > 0)
                {
                    for (int fillPos = firstFoundPos + 1; fillPos < lastFoundPos; fillPos++)
                    {
                        int targetRow = isRow ? index : fillPos;
                        int targetCol = isRow ? fillPos : index;
                        // only connect if empty (Not X and not another color)
                        if (!IsFilled(targetRow, targetCol) && !IsX(targetRow, targetCol))
                        {
                            if (await SetCell(targetRow, targetCol, clueColor, "Connection"))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }
        private int[] GetLeftmost(int index, bool isRow, int length, int[] clues)
        {
            int[] leftmost = new int[clues.Length];
            int currentPos = 0;

            for (int clueIndex = 0; clueIndex < clues.Length; clueIndex++)
            {
                Color blockColor = GetClueColor(index, isRow, clueIndex);
                bool found = false;

                while (currentPos + clues[clueIndex] <= length)
                {
                    if (CanPlaceBlock(index, isRow, currentPos, clues[clueIndex], blockColor))
                    {
                        int minGap = 0;
                        if (clueIndex > 0)
                        {
                            Color prevColor = GetClueColor(index, isRow, clueIndex - 1);
                            minGap = (prevColor.ToArgb() == blockColor.ToArgb()) ? 1 : 0;
                        }

                        int prevEnd = (clueIndex == 0) ? 0 : leftmost[clueIndex - 1] + clues[clueIndex - 1] + minGap;

                        if (currentPos >= prevEnd)
                        {
                            // Only look at the empty space between the end of the previous block and the beginning of the current one
                            bool skippedRequired = false;
                            int searchStart = (clueIndex == 0) ? 0 : leftmost[clueIndex - 1] + clues[clueIndex - 1];
                            for (int pos = searchStart; pos < currentPos; pos++)
                            {
                                int rowIndex = isRow ? index : pos;
                                int colIndex = isRow ? pos : index;
                                if (IsFilled(rowIndex, colIndex))
                                {
                                    skippedRequired = true;
                                    break;
                                }
                            }

                            if (!skippedRequired)
                            {
                                leftmost[clueIndex] = currentPos;
                                currentPos += clues[clueIndex];
                                // If the next one is the same color, we immediately jump one to X
                                if (clueIndex < clues.Length - 1 && GetClueColor(index, isRow, clueIndex + 1).ToArgb()
                                    == blockColor.ToArgb())
                                {
                                    currentPos++;
                                }

                                found = true;
                                break;
                            }
                        }
                    }
                    currentPos++;
                }
                if (!found)
                {
                    return null;
                }
            }
            return leftmost;
        }

        private int[] GetRightmost(int index, bool isRow, int length, int[] clues)
        {
            int[] rightmost = new int[clues.Length];
            int currentPos = length;

            for (int clueIndex = clues.Length - 1; clueIndex >= 0; clueIndex--)
            {
                Color blockColor = GetClueColor(index, isRow, clueIndex);
                bool found = false;

                while (currentPos - clues[clueIndex] >= 0)
                {
                    int testStart = currentPos - clues[clueIndex];
                    if (CanPlaceBlock(index, isRow, testStart, clues[clueIndex], blockColor))
                    {
                        int minGap = 0;
                        if (clueIndex < clues.Length - 1)
                        {
                            Color nextColor = GetClueColor(index, isRow, clueIndex + 1);
                            minGap = (nextColor.ToArgb() == blockColor.ToArgb()) ? 1 : 0;
                        }

                        int nextLimit = (clueIndex == clues.Length - 1) ? length : rightmost[clueIndex + 1] - minGap;

                        if (testStart + clues[clueIndex] <= nextLimit)
                        {
                            bool skippedRequired = false;
                            int searchEnd = (clueIndex == clues.Length - 1) ? length : rightmost[clueIndex + 1];
                            for (int pos = testStart + clues[clueIndex]; pos < searchEnd; pos++)
                            {
                                int rowIndex = isRow ? index : pos;
                                int colIndex = isRow ? pos : index;
                                if (IsFilled(rowIndex, colIndex))
                                {
                                    skippedRequired = true;
                                    break;
                                }
                            }

                            if (!skippedRequired)
                            {
                                rightmost[clueIndex] = testStart;
                                currentPos = testStart;
                                // If the previous one is the same color, leave room for X
                                if (clueIndex > 0 && GetClueColor(index, isRow, clueIndex - 1).ToArgb()
                                    == blockColor.ToArgb())
                                {
                                    currentPos--;
                                }

                                found = true;
                                break;
                            }
                        }
                    }
                    currentPos--;
                }
                if (!found)
                {
                    return null;
                }
            }
            return rightmost;
        }
        // anchor extension if a painted cell can only belong to one type of block
        private async Task<bool> ExtendAnchors(int index, bool isRow, int length, int[] clues, int[] leftmost,
            int[] rightmost)
        {
            for (int pos = 0; pos < length; pos++)
            {
                int rowIndex = isRow ? index : pos;
                int colIndex = isRow ? pos : index;

                if (IsFilled(rowIndex, colIndex))
                {
                    Color pixelColor = grid.userColorRGB[rowIndex, colIndex];
                    int blockIdx = -1;
                    int count = 0;

                    for (int i = 0; i < clues.Length; i++)
                    {
                        if (GetClueColor(index, isRow, i).ToArgb() == pixelColor.ToArgb() &&
                            pos >= leftmost[i] && pos <= rightmost[i] + clues[i] - 1)
                        {
                            blockIdx = i;
                            count++;
                        }
                    }

                    if (count == 1)
                    {
                        int i = blockIdx;
                        int overlapStart = Math.Max(rightmost[i], pos - clues[i] + 1);
                        int overlapEnd = Math.Min(leftmost[i] + clues[i] - 1, pos + clues[i] - 1);

                        for (int fillPos = overlapStart; fillPos <= overlapEnd; fillPos++)
                        {
                            int targetRow = isRow ? index : fillPos;
                            int targetCol = isRow ? fillPos : index;

                            if (!IsFilled(targetRow, targetCol) && !IsX(targetRow, targetCol))
                            {
                                // Check if the cross rule knows this color
                                int[] crossClues = isRow ? grid.colClues[targetCol] : grid.rowClues[targetRow];
                                bool validColorInCross = false;
                                for (int crossClueIdx = 0; crossClueIdx < crossClues.Length; crossClueIdx++)
                                {
                                    if (GetClueColor(isRow ? -1 : targetRow, !isRow, crossClueIdx).ToArgb()
                                        == pixelColor.ToArgb())
                                    {
                                        validColorInCross = true;
                                        break;
                                    }
                                }

                                if (validColorInCross)
                                {
                                    if (await SetCell(targetRow, targetCol, pixelColor,
                                        $"Anchor ({i}. block) extension"))
                                    {
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return false;
        }

        private bool CanPlaceBlock(int lineIdx, bool isRow, int start, int len, Color color)
        {
            int limit = isRow ? grid.col : grid.row;
            if (start < 0 || start + len > limit)
            {
                return false;
            }

            int targetArgb = color.ToArgb();

            // Collision X or other color cannot be there
            for (int pos = start; pos < start + len; pos++)
            {
                int rowIndex = isRow ? lineIdx : pos;
                int colIndex = isRow ? pos : lineIdx;
                if (IsX(rowIndex, colIndex))
                {
                    return false;
                }
                if (IsFilled(rowIndex, colIndex) && grid.userColorRGB[rowIndex, colIndex].ToArgb() != targetArgb)
                {
                    return false;
                }
            }

            // color rule: contact is only prohibited if the neighbor is the same color in front of it
            if (start > 0)
            {
                int prevRow = isRow ? lineIdx : start - 1;
                int prevCol = isRow ? start - 1 : lineIdx;
                if (IsFilled(prevRow, prevCol) && grid.userColorRGB[prevRow, prevCol].ToArgb() == targetArgb)
                {
                    return false;
                }
            }
            // After that
            if (start + len < limit)
            {
                int nextRow = isRow ? lineIdx : start + len;
                int nextCol = isRow ? start + len : lineIdx;
                if (IsFilled(nextRow, nextCol) && grid.userColorRGB[nextRow, nextCol].ToArgb() == targetArgb)
                {
                    return false;
                }
            }

            return true;
        }
        private Color GetClueColor(int lineIdx, bool isRow, int blockIdx)
        {
            return isRow ? grid.rowClueColors[lineIdx][blockIdx] : grid.colClueColors[lineIdx][blockIdx];
        }

        private async Task<bool> AutoCloseBlocks(int index, bool isRow, int length, int[] clues)
        {
            for (int pos = 0; pos < length; pos++)
            {
                int rowIndex = isRow ? index : pos;
                int colIndex = isRow ? pos : index;

                if (IsFilled(rowIndex, colIndex))
                {
                    int start = pos;
                    Color clusterColor = grid.userColorRGB[rowIndex, colIndex];
                    while (pos + 1 < length && IsFilled(isRow ? index : pos + 1, isRow ? pos + 1 : index) &&
                           grid.userColorRGB[isRow ? index : pos + 1, isRow ? pos + 1 : index].ToArgb()
                           == clusterColor.ToArgb())
                    {
                        pos++;
                    }
                    int end = pos;
                    int currentLen = end - start + 1;

                    int clueIdx = FindClueIdxForBlock(index, isRow, start, end, clusterColor);

                    // If we found a matching rule and its length is exactly the same
                    if (clueIdx != -1 && clues[clueIdx] == currentLen)
                    {
                        // close left side
                        if (start > 0)
                        {
                            int prevRow = isRow ? index : start - 1;
                            int prevCol = isRow ? start - 1 : index;
                            if (!IsX(prevRow, prevCol) && !IsFilled(prevRow, prevCol))
                            {
                                // only close with X if there is a previous block and it is of the same color
                                // (in this case, a break is required)
                                bool mustHaveX = false;
                                if (clueIdx > 0 && GetClueColor(index, isRow, clueIdx - 1).ToArgb() == clusterColor.ToArgb())
                                {
                                    mustHaveX = true;
                                }

                                // or if there is nothing there based on leftmost and rightmost
                                if (mustHaveX)
                                {
                                    if (await SetX(prevRow, prevCol,
                                        "Forced closing of a completed block (due to the same color)"))
                                    {
                                        return true;
                                    }
                                }
                            }
                        }

                        // close right side
                        if (end < length - 1)
                        {
                            int nextRow = isRow ? index : end + 1;
                            int nextCol = isRow ? end + 1 : index;
                            if (!IsX(nextRow, nextCol) && !IsFilled(nextRow, nextCol))
                            {
                                bool mustHaveX = false;
                                if (clueIdx < clues.Length - 1 && GetClueColor(index, isRow, clueIdx + 1).ToArgb()
                                    == clusterColor.ToArgb())
                                {
                                    mustHaveX = true;
                                }

                                if (mustHaveX)
                                {
                                    if (await SetX(nextRow, nextCol,
                                        "Forced closing of a completed block (due to the same color)"))
                                    {
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return false;
        }

        private int FindClueIdxForBlock(int lineIdx, bool isRow, int start, int end, Color color)
        {
            int length = isRow ? grid.col : grid.row;
            int[] clues = isRow ? grid.rowClues[lineIdx] : grid.colClues[lineIdx];

            // First we request the boundaries
            int[] leftmost = GetLeftmost(lineIdx, isRow, length, clues);
            int[] rightmost = GetRightmost(lineIdx, isRow, length, clues);

            if (leftmost == null || rightmost == null)
            {
                return -1;
            }

            int currentLen = end - start + 1;
            int foundIdx = -1;
            int possibleCount = 0;
            int targetArgb = color.ToArgb();

            for (int clueIndex = 0; clueIndex < clues.Length; clueIndex++)
            {
                // Do the color and length match?
                if (clues[clueIndex] == currentLen && GetClueColor(lineIdx, isRow, clueIndex).ToArgb() == targetArgb)
                {
                    // Range checking
                    // The start of the painted block cannot be earlier than the leftmost, 
                    // and the end cannot be later than the end of the rightmost range.
                    if (start >= leftmost[clueIndex] && end <= (rightmost[clueIndex] + clues[clueIndex] - 1))
                    {
                        // Order constraint
                        foundIdx = clueIndex;
                        possibleCount++;
                    }
                }
            }

            // Only return if 100% sure (only one clue fits)
            return (possibleCount == 1) ? foundIdx : -1;
        }

        // Handles tight spaces and X edges of finished rows
        private async Task<bool> HandleLogicCleanup(int index, bool isRow, int length, int[] clues, int totalBlocks)
        {
            int currentFilled = 0;
            for (int pos = 0; pos < length; pos++)
            {
                int rowIndex = isRow ? index : pos;
                int colIndex = isRow ? pos : index;
                if (IsFilled(rowIndex, colIndex))
                {
                    currentFilled++;
                }
            }

            // If the number of colored cells reaches the sum of the rules
            if (currentFilled == totalBlocks)
            {
                for (int pos = 0; pos < length; pos++)
                {
                    int rowIndex = isRow ? index : pos;
                    int colIndex = isRow ? pos : index;
                    if (!IsFilled(rowIndex, colIndex) && !IsX(rowIndex, colIndex))
                    {
                        // If we found an empty space that needs to be X
                        if (await SetX(rowIndex, colIndex, "Row complete (Quantity correct)"))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private bool IsFilled(int rowIndex, int colIndex)
        {
            return grid.isColor ? grid.userColorRGB[rowIndex, colIndex] != Color.White :
                grid.gridButtons[rowIndex, colIndex].BackColor == Color.Black;
        }

        private bool IsX(int rowIndex, int colIndex)
        {
            return grid.userXMark[rowIndex, colIndex];
        }

        private async Task<bool> SetCell(int rowIndex, int colIndex, Color colorToSet, string reason)
        {
            // If there is already an X there, or it is already the same color, then there is no change
            if (IsX(rowIndex, colIndex))
            {
                return false;
            }
            if (IsFilled(rowIndex, colIndex) && grid.userColorRGB[rowIndex, colIndex].ToArgb()
                == colorToSet.ToArgb())
            {
                return false;
            }

            grid.gridButtons[rowIndex, colIndex].FlatAppearance.BorderColor = Color.Yellow;
            grid.gridButtons[rowIndex, colIndex].FlatAppearance.BorderSize = 3;

            ShowFixedDialog($"Step: [{rowIndex + 1}. row, {colIndex + 1} column] coloring.\n\nReasons: {reason}");

            // Reset frame and actual modification
            grid.gridButtons[rowIndex, colIndex].FlatAppearance.BorderSize = 1;
            grid.gridButtons[rowIndex, colIndex].FlatAppearance.BorderColor = Color.Black;

            render.SetCellColor(rowIndex, colIndex, grid.gridButtons[rowIndex, colIndex], colorToSet);
            grid.userColorRGB[rowIndex, colIndex] = colorToSet;

            render.UpdatePreview(rowIndex, colIndex);
            form.Refresh();

            await Task.Delay(100);
            return true;
        }

        private async Task<bool> SetX(int rowIndex, int colIndex, string reason)
        {
            if (IsFilled(rowIndex, colIndex) || IsX(rowIndex, colIndex))
            {
                return false;
            }

            grid.gridButtons[rowIndex, colIndex].FlatAppearance.BorderColor = Color.Red;
            grid.gridButtons[rowIndex, colIndex].FlatAppearance.BorderSize = 3;

            ShowFixedDialog($"Step: [{rowIndex + 1}. row, {colIndex + 1} column] is replaced by X.\n\nReasons: {reason}");

            grid.gridButtons[rowIndex, colIndex].FlatAppearance.BorderSize = 1;
            grid.gridButtons[rowIndex, colIndex].FlatAppearance.BorderColor = Color.Black;

            render.SetCellX(rowIndex, colIndex, grid.gridButtons[rowIndex, colIndex]);
            grid.aiXMark[rowIndex, colIndex] = true;

            render.UpdatePreview(rowIndex, colIndex);
            form.Refresh();

            await Task.Delay(100);
            return true;
        }

        private void ShowFixedDialog(string text)
        {
            using (Form dialog = new Form())
            {
                dialog.StartPosition = FormStartPosition.Manual;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.Size = new Size(200, 200);
                dialog.Text = "AI Logic";
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;
                dialog.TopMost = true;

                dialog.Shown += (s, e) =>
                {
                    dialog.Location = new Point(
                        form.Left + 250,
                        form.Top + 50
                    );
                };

                Label lbl = new Label();
                lbl.Text = text;
                lbl.Dock = DockStyle.Fill;
                lbl.TextAlign = ContentAlignment.MiddleCenter;

                Button btn = new Button();
                btn.Text = "OK";
                btn.Dock = DockStyle.Bottom;
                btn.Height = 40;
                btn.DialogResult = DialogResult.OK;

                dialog.Controls.Add(lbl);
                dialog.Controls.Add(btn);
                dialog.AcceptButton = btn;

                dialog.ShowDialog();
            }
        }
    }
}