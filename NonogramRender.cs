using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace Grafilogika_alkalmazas_keszitese
{
    public class NonogramRender
    {
        public Queue<Point> solutionQueue;
        public Timer solveTimer;
        public int previewSize = 150;
        public int hintCount = 0;
        public int maxHintCount;
        public bool showHints = true;
        public Image img;
        private Nonogram form;
        private NonogramGrid grid;
        private GameTimerManager gameTimerManager;
        private UndoRedoManager undoredoManager;
        private LeaderboardManager leaderboardManager;
        private ExtraGridManager extraGridManager;

        public NonogramRender(Nonogram f, NonogramGrid g, GameTimerManager game, UndoRedoManager u, LeaderboardManager l, ExtraGridManager e)
        {
            this.form = f;
            this.grid = g;
            this.gameTimerManager = game;
            this.undoredoManager = u;
            this.leaderboardManager = l;
            this.extraGridManager = e;
        }

        public void SetGrid(NonogramGrid g)
        {
            this.grid = g;
        }

        public void SetTimerManager(GameTimerManager gtm)
        {
            this.gameTimerManager = gtm;
        }

        public void SetExtraGridManager(ExtraGridManager egm)
        {
            this.extraGridManager = egm;
        }

        // Update Preview
        public void UpdatePreview(int? row = null, int? col = null)
        {
            int rows = grid.row;
            int cols = grid.col;
            int cellSize = grid.userCellSize;
            int pSize = previewSize;

            // Get an existing image or create a new one
            Bitmap bmp;
            if (row.HasValue && col.HasValue && form.picPreview.Image != null)
            {
                bmp = new Bitmap(form.picPreview.Image);
            }
            else
            {
                bmp = new Bitmap(pSize, pSize);
            }

            using (Graphics graphics = Graphics.FromImage(bmp))
            {
                // If we update the whole thing, we clear the canvas
                if (!row.HasValue)
                {
                    graphics.Clear(Color.White);
                }

                // Interpolation for nice scaling
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;

                // Calculate the scaling ratio (to fit the previewSize)
                float scaleX = (float)pSize / (cols * cellSize);
                float scaleY = (float)pSize / (rows * cellSize);

                int startRow = row ?? 0;
                int endRow = row ?? rows - 1;
                int startCol = col ?? 0;
                int endCol = col ?? cols - 1;

                for (int rowIndex = startRow; rowIndex <= endRow; rowIndex++)
                {
                    for (int colIndex = startCol; colIndex <= endCol; colIndex++)
                    {
                        RectangleF cellRect = new RectangleF(
                            colIndex * cellSize * scaleX,
                            rowIndex * cellSize * scaleY,
                            cellSize * scaleX,
                            cellSize * scaleY
                        );
                       
                        using (SolidBrush brush = new SolidBrush(grid.userColorRGB[rowIndex, colIndex]))
                        {
                            graphics.FillRectangle(brush, cellRect);
                        }
                    }
                }
            }

            // Free memory
            if (form.picPreview.Image != null)
            {
                form.picPreview.Image.Dispose();
            }

            form.picPreview.Image = bmp;
        }

        public Bitmap GeneratePreviewImage()
        {
            int rows = grid.row;
            int cols = grid.col;
            int width = form.picSolutionPreview.Width;
            int height = form.picSolutionPreview.Height;

            Bitmap bmp = new Bitmap(width, height);

            using (Graphics graphics = Graphics.FromImage(bmp))
            {
                graphics.Clear(Color.White);

                float cellWidth = (float)width / cols;
                float cellHeight = (float)height / rows;

                for (int rowIndex = 0; rowIndex < rows; rowIndex++)
                {
                    for (int colIndex = 0; colIndex < cols; colIndex++)
                    {
                        RectangleF cellRect = new RectangleF(
                            colIndex * cellWidth,
                            rowIndex * cellHeight,
                            cellWidth,
                            cellHeight
                        );

                        Color color = grid.isColor
                            ? grid.solutionColorRGB[rowIndex, colIndex]
                            : (grid.solutionBW[rowIndex, colIndex] == 1 ? Color.Black : Color.White);

                        using (SolidBrush brush = new SolidBrush(color))
                        {
                            graphics.FillRectangle(brush, cellRect);
                        }
                    }
                }
            }

            return bmp;
        }

        // Solution animated
        public void SolveNonogram()
        {
            form.btnBackToHome.Enabled = false;
            form.btnSelectImage.Enabled = false;
            form.btnSolve.Enabled = false;
            form.btnHint.Enabled = false;
            form.btnCheck.Enabled = false;
            form.btnUndo.Enabled = false;
            form.btnRedo.Enabled = false;
            form.btnSmartAI.Enabled = false;
            form.rbNumberEntryMode.Enabled = false;
            form.rbImgBlackWhiteMode.Enabled = false;
            form.rbImgColorMode.Enabled = false;
            form.chkShowX.Enabled = true;
            gameTimerManager.Stop();
            ClearErrorHighlights();
            List<Point> cellsToSolve = new List<Point>();

            for (int rowIndex = 0; rowIndex < grid.row; rowIndex++)
            {
                for (int colIndex = 0; colIndex < grid.col; colIndex++)
                {
                    grid.gridButtons[rowIndex, colIndex].Enabled = false;
                    if (grid.userColorRGB[rowIndex, colIndex].ToArgb() != grid.solutionColorRGB[rowIndex, colIndex].ToArgb())
                    {
                        // Color cells where the user has not yet filled in a good color
                        cellsToSolve.Add(new Point(rowIndex, colIndex));
                    }
                }
            }

            if (cellsToSolve.Count == 0)
            {
                MessageBox.Show("All the cells are in place now!", "Solution");
                return;
            }

            solutionQueue = new Queue<Point>(cellsToSolve);

            solveTimer = new Timer();
            solveTimer.Interval = 100;
            solveTimer.Tick += SolveTimer_Tick;
            solveTimer.Start();
        }

        private void SolveTimer_Tick(object sender, EventArgs e)
        {
            if (solutionQueue.Count == 0)
            {
                solveTimer.Stop();
                solveTimer.Dispose();
                SetGridEnabled(true, form.rbImgColorMode.Checked, form.rbImgBlackWhiteMode.Checked);
                gameTimerManager.Stop();
                if (MessageBox.Show("The nonogram is completely solved!", "Solution ready") == DialogResult.OK)
                {
                    form.btnBackToHome.Enabled = true;
                    form.btnSelectImage.Enabled = true;
                    form.rbNumberEntryMode.Enabled = true;
                    form.rbImgBlackWhiteMode.Enabled = true;
                    form.rbImgColorMode.Enabled = true;
                    form.btnHint.Enabled = true;
                    form.btnCheck.Enabled = true;
                    form.btnUndo.Enabled = true;
                    form.btnRedo.Enabled = true;
                    form.btnSmartAI.Enabled = true;
                    if (!form.rbImgBlackWhiteMode.Checked && !form.rbImgColorMode.Checked)
                    {
                        gameTimerManager.RestartGameWithCurrentDifficulty();
                    }
                }
                return;
            }

            Point pos = solutionQueue.Dequeue();
            int row = pos.X;
            int col = pos.Y;
            Button btn = grid.gridButtons[row, col];


            // Color or black and white cell normal fill
            if (grid.isColor)
            {
                grid.userColorRGB[row, col] = grid.solutionColorRGB[row, col];
                btn.BackColor = grid.solutionColorRGB[row, col];
            }
            else
            {
                grid.userColorRGB[row, col] = grid.solutionBW[row, col] == 1 ? Color.Black : Color.White;
                btn.BackColor = grid.userColorRGB[row, col];
            }

            btn.Text = "";
            grid.userXMark[row, col] = false;

            UpdatePreview(row, col);
        }

        public void ShowHint()
        {
            ClearErrorHighlights();
            List<Point> wrongCells = new List<Point>();

            for (int rowIndex = 0; rowIndex < grid.row; rowIndex++)
            {
                for (int colIndex = 0; colIndex < grid.col; colIndex++)
                {
                    if (!grid.gridButtons[rowIndex, colIndex].Enabled)
                    {
                        continue;
                    }

                    bool isColorInSolution = grid.isColor
                        ? grid.solutionColorRGB[rowIndex, colIndex].ToArgb() != Color.White.ToArgb()
                        : grid.solutionBW[rowIndex, colIndex] == 1;

                    bool hasVisualX = grid.gridButtons[rowIndex, colIndex].Text == "X";
                    bool hasColor = grid.userColorRGB[rowIndex, colIndex].ToArgb() != Color.White.ToArgb() &&
                                    grid.userColorRGB[rowIndex, colIndex].ToArgb() != 0;

                    bool isWrong = false;

                    if (isColorInSolution)
                    {
                        if (!hasColor || hasVisualX)
                        {
                            isWrong = true;
                        }
                        if (!isWrong && grid.isColor && !AreColorsSimilar(grid.userColorRGB[rowIndex, colIndex],
                            grid.solutionColorRGB[rowIndex, colIndex], 40))
                        {
                            isWrong = true;
                        }
                    }
                    else
                    {
                        // white space error if there is color there
                        if (hasColor)
                        {
                            isWrong = true;
                        }
                    }

                    if (isWrong)
                    {
                        wrongCells.Add(new Point(rowIndex, colIndex));
                    }
                }
            }

            if (wrongCells.Count == 0)
            {
                if (IsSolved())
                {
                    FinalizeGame();
                    return;
                }

                MessageBox.Show("Everything is in place in the current mode!");
                return;
            }

            Point hintCell = wrongCells[form.rnd.Next(wrongCells.Count)];
            int hintRow = hintCell.X;
            int hintCol = hintCell.Y;
            Button btn = grid.gridButtons[hintRow, hintCol];

            bool shouldBeColor = grid.isColor
                ? grid.solutionColorRGB[hintRow, hintCol].ToArgb() != Color.White.ToArgb()
                : grid.solutionBW[hintRow, hintCol] == 1;

            if (shouldBeColor)
            {
                // Fix colored cell
                btn.Text = "";
                grid.userXMark[hintRow, hintCol] = false;
                Color solColor = grid.isColor ? grid.solutionColorRGB[hintRow, hintCol] : Color.Black;
                grid.userColorRGB[hintRow, hintCol] = solColor;
                btn.BackColor = solColor;
            }
            else
            {
                // White cell delete any color or X there
                btn.Text = "";
                grid.userXMark[hintRow, hintCol] = false;
                grid.userColorRGB[hintRow, hintCol] = Color.White;
                btn.BackColor = Color.White;
            }
            grid.isHintFixed[hintRow, hintCol] = true;
            btn.Enabled = false;
            UpdatePreview(hintRow, hintCol);

            // we check immediately after the Hint if it is ready!
            if (IsSolved())
            {
                FinalizeGame();
            }
        }

        public void CheckSolution()
        {
            showHints = false;
            for (int rowIndex = 0; rowIndex < grid.row; rowIndex++)
            {
                for (int colIndex = 0; colIndex < grid.col; colIndex++)
                {
                    grid.gridButtons[rowIndex, colIndex].Invalidate();
                }
            }
            bool correct = true;

            for (int rowIndex = 0; rowIndex < grid.row; rowIndex++)
            {
                for (int colIndex = 0; colIndex < grid.col; colIndex++)
                {
                    // Solution definition
                    bool shouldBeColor = grid.isColor
                        ? grid.solutionColorRGB[rowIndex, colIndex].ToArgb() != Color.White.ToArgb()
                        : grid.solutionBW[rowIndex, colIndex] == 1;

                    bool hasVisualX = grid.gridButtons[rowIndex, colIndex].Text == "X";
                    bool hasColor = grid.userColorRGB[rowIndex, colIndex].ToArgb() != Color.White.ToArgb() &&
                                    grid.userColorRGB[rowIndex, colIndex].ToArgb() != 0;

                    if (shouldBeColor)
                    {
                        // color error (wrong color or X (blank space) there)
                        bool colorMatch = grid.isColor
                            ? AreColorsSimilar(grid.userColorRGB[rowIndex, colIndex], grid.solutionColorRGB[rowIndex,
                            colIndex], 40)
                            : (grid.userColorRGB[rowIndex, colIndex].ToArgb() == Color.Black.ToArgb());

                        if (!colorMatch || hasVisualX)
                        {
                            correct = false;
                            grid.gridButtons[rowIndex, colIndex].BackColor = Color.DarkRed;
                            grid.userColorRGB[rowIndex, colIndex] = Color.White;
                            grid.userColor[rowIndex, colIndex] = 0;
                        }
                    }
                    else
                    {
                        // white error
                        if (hasColor)
                        {
                            correct = false;
                            grid.gridButtons[rowIndex, colIndex].BackColor = Color.DarkRed;
                            grid.userColorRGB[rowIndex, colIndex] = Color.White;
                            grid.userColor[rowIndex, colIndex] = 0;
                        }
                    }
                }
            }
            if (correct || form.rbImgBlackWhiteMode.Checked || form.rbImgColorMode.Checked)
            {
                form.btnCheck.Enabled = true;
                MessageBox.Show("Congratulations, correct solution!", "Check", MessageBoxButtons.OK);
                return;
            }
            if (correct)
            {
                if (MessageBox.Show("Congratulations, correct solution!", "Check", MessageBoxButtons.OK) == DialogResult.OK)
                {
                    gameTimerManager.RestartGameWithCurrentDifficulty();
                }
            }
            else
            {
                UpdatePreview();
                MessageBox.Show("There are some incorrect fields! Click on the grid to correct them.", "Check",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        public void ToggleXMarks(bool show)
        {
            if (grid.gridButtons == null)
            {
                return;
            }

            for (int rowIndex = 0; rowIndex < grid.row; rowIndex++)
            {
                for (int colIndex = 0; colIndex < grid.col; colIndex++)
                {
                    Button btn = grid.gridButtons[rowIndex, colIndex];
                    if (btn == null)
                    {
                        continue;
                    }

                    bool isWhite = grid.solutionColorRGB[rowIndex, colIndex].ToArgb() == Color.White.ToArgb();

                    if (isWhite)
                    {
                        if (show)
                        {
                            // Adjust the font to the generated cellSize
                            float fontSize = grid.userCellSize * 0.3f;
                            btn.Font = new Font("Arial", fontSize, FontStyle.Bold);
                            btn.ForeColor = Color.Gray; // Az X színe
                            btn.Text = "X";
                            btn.TextAlign = ContentAlignment.MiddleCenter;

                            grid.userXMark[rowIndex, colIndex] = true;
                        }
                        else
                        {
                            // Delete user X, but leave AI X
                            if (!grid.aiXMark[rowIndex, colIndex])
                            {
                                btn.Text = "";
                            }

                            grid.userXMark[rowIndex, colIndex] = false;
                        }
                    }
                }
            }
        }

        public void ToggleNumberEntryXMarks(bool show)
        {
            if (extraGridManager.extraButtons == null)
            {
                return;
            }

            int rows = extraGridManager.extraButtons.GetLength(0);
            int cols = extraGridManager.extraButtons.GetLength(1);

            for (int rowIndex = 0; rowIndex < rows; rowIndex++)
            {
                for (int colIndex = 0; colIndex < cols; colIndex++)
                {
                    Button btn = extraGridManager.extraButtons[rowIndex, colIndex];
                    if (btn == null)
                    {
                        continue;
                    }

                    // We only write X on the white buttons
                    if (btn.BackColor == Color.White)
                    {
                        if (show)
                        {
                            float fontSize = extraGridManager.cellSize * 0.5f;
                            btn.Font = new Font("Arial", fontSize, FontStyle.Bold);
                            btn.ForeColor = Color.Gray;
                            btn.Text = "X";
                            btn.TextAlign = ContentAlignment.MiddleCenter;
                        }
                        else
                        {
                            btn.Text = "";
                        }
                    }
                }
            }
        }

        public void SetGridEnabled(bool enabled, bool isColorImgMode, bool isBlackAndWhiteImgMode)
        {
            for (int rowIndex = 0; rowIndex < grid.gridButtons.GetLength(0); rowIndex++)
            {
                for (int colIndex = 0; colIndex < grid.gridButtons.GetLength(1); colIndex++)
                {
                    if (isColorImgMode || isBlackAndWhiteImgMode)
                    {
                        // nothing is clickable in black and white and color image scanning mode
                        grid.gridButtons[rowIndex, colIndex].Enabled = false;
                    }
                    else
                    {
                        // In normal mode only non-X cells
                        if (!grid.userXMark[rowIndex, colIndex])
                        {
                            grid.gridButtons[rowIndex, colIndex].Enabled = enabled;
                        }
                    }
                }
            }
        }

        public Bitmap ConvertToBlackAndWhite(Bitmap original, byte threshold = 200)
        {
            Bitmap bw = new Bitmap(original.Width, original.Height, PixelFormat.Format24bppRgb);

            Rectangle rect = new Rectangle(0, 0, original.Width, original.Height);
            BitmapData srcData = original.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            BitmapData dstData = bw.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

            int bytes = Math.Abs(srcData.Stride) * original.Height;
            byte[] srcBuffer = new byte[bytes];
            byte[] dstBuffer = new byte[bytes];

            Marshal.Copy(srcData.Scan0, srcBuffer, 0, bytes);

            int bytesPerPixel = 3;
            for (int rowIndex = 0; rowIndex < original.Height; rowIndex++)
            {
                for (int colIndex = 0; colIndex < original.Width; colIndex++)
                {
                    int index = rowIndex * srcData.Stride + colIndex * bytesPerPixel;
                    byte blue = srcBuffer[index];
                    byte green = srcBuffer[index + 1];
                    byte red = srcBuffer[index + 2];

                    // Calculate gray value
                    int grayValue = (int)(0.299 * red + 0.587 * green + 0.114 * blue);

                    // Elimination black to white
                    byte bwValue = (grayValue < threshold) ? (byte)0 : (byte)255;

                    dstBuffer[index] = bwValue;
                    dstBuffer[index + 1] = bwValue;
                    dstBuffer[index + 2] = bwValue;
                }
            }

            Marshal.Copy(dstBuffer, 0, dstData.Scan0, bytes);

            original.UnlockBits(srcData);
            bw.UnlockBits(dstData);

            return bw;
        }

        public void ClearErrorHighlights()
        {
            for (int rowIndex = 0; rowIndex < grid.row; rowIndex++)
            {
                for (int colIndex = 0; colIndex < grid.col; colIndex++)
                {
                    Button btn = grid.gridButtons[rowIndex, colIndex];

                    // Reset background color (DarkRed and original color)
                    if (btn.BackColor == Color.DarkRed)
                    {
                        if (grid.isColor)
                        {
                            Color color = grid.userColorRGB[rowIndex, colIndex];
                            btn.BackColor = color.IsEmpty ? Color.White : color;
                        }
                        else
                        {
                            btn.BackColor = (grid.userColorRGB[rowIndex, colIndex].ToArgb() == Color.Black.ToArgb())
                                ? Color.Black : Color.White;
                        }
                    }

                    // clear error X
                    if (btn.Text == "X" && btn.ForeColor == Color.Red)
                    {
                        btn.Text = "";
                        grid.userXMark[rowIndex, colIndex] = false;
                    }
                }
            }
        }

        public void RefreshGrid()
        {
            for (int rowIndex = 0; rowIndex < grid.row; rowIndex++)
            {
                for (int colIndex = 0; colIndex < grid.col; colIndex++)
                {
                    grid.gridButtons[rowIndex, colIndex].Invalidate();
                }
            }
        }
        public void SelectColor(Button colorBtn)
        {
            grid.selectedColor = colorBtn.BackColor;

            foreach (Button button in form.colorPalette.Controls)
            {
                button.FlatAppearance.BorderColor = Color.Gray;
            }

            colorBtn.FlatAppearance.BorderColor = Color.Red;
        }

        public void SetCellColor(int row, int col, Button btn, Color color)
        {
            grid.userColorRGB[row, col] = color;
            grid.userColor[row, col] = 1;
            btn.BackColor = color;
            btn.Text = "";
            grid.userXMark[row, col] = false;
        }

        public void SetCellBlack(int row, int col, Button btn)
        {
            // X → black
            if (grid.userXMark[row, col])
            {
                grid.userXMark[row, col] = false;
                btn.Text = "";
            }

            btn.BackColor = Color.Black;
            grid.userColorRGB[row, col] = Color.Black;
            grid.userColor[row, col] = 1;
        }

        public void SetCellX(int row, int col, Button btn)
        {
            // black → X
            btn.BackColor = Color.White;
            grid.userColorRGB[row, col] = Color.White;
            grid.userColor[row, col] = 0;

            grid.userXMark[row, col] = true;

            float fontSize = grid.userCellSize * 0.3f;
            btn.Font = new Font("Arial", fontSize, FontStyle.Bold);
            btn.ForeColor = Color.Gray;
            btn.Text = "X";
            btn.TextAlign = ContentAlignment.MiddleCenter;
        }

        public void ClearCell(int row, int col, Button btn)
        {
            btn.BackColor = Color.White;
            btn.Text = "";
            grid.userXMark[row, col] = false;
            grid.userColor[row, col] = 0;
            grid.userColorRGB[row, col] = Color.White;
        }
        public bool AreColorsSimilar(Color firstColor, Color secondColor, int threshold)
        {
            int distanceRed = firstColor.R - secondColor.R;
            int distanceGreen = firstColor.G - secondColor.G;
            int distanceBlue = firstColor.B - secondColor.B;
            int distance = distanceRed * distanceRed + distanceGreen * distanceGreen + distanceBlue * distanceBlue;
            return distance <= threshold * threshold;
        }

        public bool IsCellCorrect(int row, int col)
        {
            if (grid.solutionColorRGB == null ||
                grid.gridButtons == null ||
                row < 0 || row >= grid.row || col < 0 || col >= grid.col)
            {
                return false;
            }
            // Determine solution (color or black)
            bool shouldBeColor = grid.isColor
                ? (grid.solutionColorRGB[row, col].ToArgb() != Color.White.ToArgb())
                : grid.solutionBW[row, col] == 1;

            // Get user's current status
            Color userColor = grid.userColorRGB[row, col];

            // hasColor is considered colored if it is not empty, not transparent, and not white
            bool hasColor = !userColor.IsEmpty &&
                            userColor.ToArgb() != 0 &&
                            userColor.ToArgb() != Color.White.ToArgb();

            bool hasX = grid.gridButtons[row, col].Text == "X";

            // control logic
            if (shouldBeColor)
            {
                // Error if there is no color, or if there is an X there by chance
                if (!hasColor || hasX)
                {
                    return false;
                }

                // In color mode, we also look at the specific shade
                if (grid.isColor)
                {
                    return AreColorsSimilar(userColor, grid.solutionColorRGB[row, col], 40);
                }

                return true;
            }
            else
            {
                return !hasColor;
            }
        }

        public bool IsSolved()
        {
            if (grid.solutionColorRGB == null || grid.gridButtons == null)
            {
                return false;
            }
            for (int rowIndex = 0; rowIndex < grid.row; rowIndex++)
            {
                for (int colIndex = 0; colIndex < grid.col; colIndex++)
                {
                    // If even one cell is bad, it's not done yet
                    if (!IsCellCorrect(rowIndex, colIndex))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public void ChkShowX_CheckedChanged(object sender, EventArgs e)
        {
            // Safety check if the array is null, stop
            if (grid.gridButtons == null)
            {
                return;
            }

            bool show = form.chkShowX.Checked;

            ToggleXMarks(show);
            ToggleNumberEntryXMarks(show);
            UpdatePreview();
            if (extraGridManager != null && form.rbNumberEntryMode.Checked)
            {
                extraGridManager.UpdateExtraPreview();
            }
        }

        public void BtnSelectImage_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp";
            ofd.Title = "Select an image for Nonogram";
            ofd.InitialDirectory = AppDomain.CurrentDomain.BaseDirectory;

            if (ofd.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            Image loadedImg = Image.FromFile(ofd.FileName);
            Bitmap bmpToUse;

            bool useGrayscale = form.rbImgBlackWhiteMode.Checked;

            if (useGrayscale)
            {
                bmpToUse = ConvertToBlackAndWhite(new Bitmap(loadedImg));
            }
            else
            {
                bmpToUse = new Bitmap(loadedImg);
            }

            if (form.rbImgColorMode.Checked)
            {
                bmpToUse = RemoveLightBackground(bmpToUse, 200);
            }

            img = bmpToUse;

            // Display in solution preview
            form.picSolutionPreview.Image = bmpToUse;
            form.picSolutionPreview.SizeMode = PictureBoxSizeMode.Zoom;

            int gridLeft = 20;
            int gridTop = form.chkShowX.Bottom + 20;

            grid.ClearGrid();

            // Generate a new grid from the selected image
            grid.GenerateNonogramFromImage(bmpToUse, gridLeft, gridTop);

            // Enable buttons and controls
            form.btnSolve.Enabled = true;
            form.btnHint.Enabled = true;
            form.btnCheck.Enabled = true;
            form.btnRedo.Enabled = true;
            form.btnUndo.Enabled = true;
            form.chkShowX.Checked = false;
            form.chkShowX.Enabled = false;
            form.colorPalette.Visible = false;

            // Update preview
            UpdatePreview();
            SetGridEnabled(true, form.rbImgColorMode.Checked, form.rbImgBlackWhiteMode.Checked);
        }

        // Solution button event
        public void BtnSolve_Click(object sender, EventArgs e)
        {
            SolveNonogram();
        }

        // Help button event
        public void BtnHint_Click(object sender, EventArgs e)
        {
            ShowHint();
            hintCount++;
            form.lblHintCount.Text = $"Number of hints: {hintCount} (max: {maxHintCount})";
            if (hintCount >= maxHintCount)
            {
                MessageBox.Show(
                    "You have reached the maximum number of assists! The game will restart.",
                    "Attention",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );

                gameTimerManager.RestartGameWithCurrentDifficulty();
            }
        }

        // Check button event
        public void BtnCheck_Click(object sender, EventArgs e)
        {
            form.btnCheck.Enabled = false;
            CheckSolution();
        }

        public Color[] GetTwoRandomColors()
        {
            Random rnd = form.rnd;
            int first = rnd.Next(grid.nonogramPalette.Length);
            int second;
            do
            {
                second = rnd.Next(grid.nonogramPalette.Length);
            } while (second == first);

            return new Color[] 
            {
                grid.nonogramPalette[first], grid.nonogramPalette[second] 
            };
        }

        public void ApplyColorsToBlocks(Color[] twoColors)
        {
            if (!grid.isColor)
            {
                for (int rowIndex = 0; rowIndex < grid.row; rowIndex++)
                {
                    for (int colIndex = 0; colIndex < grid.col; colIndex++)
                    {
                        grid.solutionColorRGB[rowIndex, colIndex] =
                            grid.solutionBW[rowIndex, colIndex] == 1 ? Color.Black : Color.White;
                    }
                }
                return;
            }

            Random rnd = form.rnd;
            HashSet<Color> usedColors = new HashSet<Color>();

            for (int rowIndex = 0; rowIndex < grid.row; rowIndex++)
            {
                Color lastColor = Color.Empty;
                int colIndex = 0;

                while (colIndex < grid.col)
                {
                    if (grid.solutionBW[rowIndex, colIndex] == 0)
                    {
                        grid.solutionColorRGB[rowIndex, colIndex] = Color.White;
                        lastColor = Color.Empty;
                        colIndex++;
                        continue;
                    }

                    // Choose a block color from two random colors
                    Color blockColor = twoColors[rnd.Next(2)];
                    lastColor = blockColor;
                    usedColors.Add(blockColor);

                    // Fill block
                    int blockEndIndex = colIndex;
                    while (blockEndIndex < grid.col && grid.solutionBW[rowIndex, blockEndIndex] == 1)
                    {
                        grid.solutionColorRGB[rowIndex, blockEndIndex] = blockColor;
                        blockEndIndex++;
                    }

                    colIndex = blockEndIndex;
                }
            }

            // If only 1 color was used, replace a block with the second color
            if (usedColors.Count == 1 && grid.nonogramPalette.Length > 1)
            {
                Color firstColor = usedColors.First();
                Color secondColor = grid.nonogramPalette.First(c => c != firstColor);

                for (int rowIndex = 0; rowIndex < grid.row; rowIndex++)
                {
                    for (int colIndex = 0; colIndex < grid.col; colIndex++)
                    {
                        if (grid.solutionBW[rowIndex, colIndex] == 1 &&
                            grid.solutionColorRGB[rowIndex, colIndex] == firstColor)
                        {
                            grid.solutionColorRGB[rowIndex, colIndex] = secondColor;
                            return;
                        }
                    }
                }
            }
        }
        public Bitmap RemoveLightBackground(Bitmap src, int threshold = 240)
        {
            Bitmap bmp = new Bitmap(src.Width, src.Height, PixelFormat.Format24bppRgb);

            Rectangle rect = new Rectangle(0, 0, src.Width, src.Height);
            BitmapData data = src.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            int bytes = Math.Abs(data.Stride) * src.Height;
            byte[] buffer = new byte[bytes];
            Marshal.Copy(data.Scan0, buffer, 0, bytes);

            int bpp = 3;

            for (int rowIndex = 0; rowIndex < src.Height; rowIndex++)
            {
                for (int colIndex = 0; colIndex < src.Width; colIndex++)
                {
                    int i = rowIndex * data.Stride + colIndex * bpp;

                    byte blue = buffer[i];
                    byte green = buffer[i + 1];
                    byte red = buffer[i + 2];

                    // very light background white
                    if (red > threshold && green > threshold && blue > threshold)
                    {
                        buffer[i] = 255;
                        buffer[i + 1] = 255;
                        buffer[i + 2] = 255;
                    }
                }
            }

            Bitmap result = new Bitmap(src.Width, src.Height, PixelFormat.Format24bppRgb);
            BitmapData dstData = result.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            Marshal.Copy(buffer, 0, dstData.Scan0, bytes);

            src.UnlockBits(data);
            result.UnlockBits(dstData);

            return result;
        }

        public void FinalizeGame()
        {
            gameTimerManager.Stop();
            MessageBox.Show("Congratulations, your Nonogram is ready!");

            try
            {
                string exeFolder = AppDomain.CurrentDomain.BaseDirectory;
                string mode = gameTimerManager.GetModeName();
                string difficulty = gameTimerManager.GetDifficultyName();
                string fileName = "nonogram_saves.json";
                string fullPath = Path.Combine(exeFolder, fileName);
                if (form.txtUsername != null)
                {
                    form.txtUsername.Enabled = false;
                }
                leaderboardManager.SaveGame(fullPath, form.username);

                MessageBox.Show("The game is automatically saved!", "Save",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error while saving:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            // Reset UI
            grid.ClearGrid();
            gameTimerManager.ResetCellClicks();
            gameTimerManager.ResetColorClicks();
            gameTimerManager.ResetHintClicks();
            gameTimerManager.Stop();
            form.lblTimer.Text = "0:00";
            form.picPreview.Visible = false;
            form.picSolutionPreview.Visible = false;
            form.lblTimer.Visible = false;
            form.chkShowX.Visible = false;
            form.cmbDifficulty.Visible = true;
            form.cmbMode.Visible = true;
            form.btnSolve.Visible = false;
            form.btnHint.Visible = false;
            form.btnCheck.Visible = false;
            form.btnUndo.Visible = false;
            form.btnRedo.Visible = false;
            form.lblUsername.Visible = true;
            form.txtUsername.Visible = true;
            form.lblWrongCellClicks.Visible = false;
            form.lblWrongColorClicks.Visible = false;
            form.lblHintCount.Visible = false;
            form.lblUndoCount.Visible = false;
            form.btnLeaderboard.Visible = true;
            form.btnGenerateRandom.Visible = true;
            form.btnTips.Visible = true;
            form.lblExtra.Visible = true;
            form.btnRestart.Visible = false;
            form.btnBackToHome.Visible = false;
            form.btnSmartAI.Visible = false;
            form.rbNumberEntryMode.Visible = true;
            form.rbImgBlackWhiteMode.Visible = true;
            form.rbImgColorMode.Visible = true;
            form.groupModes.Visible = true;
            form.txtUsername.Enabled = true;
            form.txtUsername.Text = "";
            form.colorPalette.Visible = false;
            gameTimerManager.gameStarted = false;
            gameTimerManager.elapsedSeconds = 0;
            undoredoManager.undoClicks = 0;
            form.lblWrongCellClicks.Text = $"Number of incorrect clicks: {grid.wrongCellClicks}";
            form.lblWrongColorClicks.Text = $"Number of colors: {grid.wrongColorClicks}";
            form.lblHintCount.Text = $"Number of hints: {hintCount}";
            form.lblUndoCount.Text = $"Number of withdrawals: {undoredoManager.undoClicks}";
            form.groupModes.Location = new Point(20, 200);
            form.lblUsername.Location = new Point(20, 100);
            form.txtUsername.Location = new Point(145, 95);
            form.Size = new Size(380, 350);
            return;
        }
    }
}