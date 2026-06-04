using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Grafilogika_alkalmazas_keszitese
{
    public class NonogramGrid
    {
        public Button[,] gridButtons;
        public int row;
        public int col;
        public int[][] rowClues;
        public int[][] colClues;
        public Color[][] rowClueColors;
        public Color[][] colClueColors;
        public int[,] userColor;
        public int userCellSize = 50;
        public Color[,] solutionColorRGB;
        public int[,] solutionBW;
        public Color[,] userColorRGB;
        public bool isColor;
        public Color selectedColor = Color.White;
        public Color[] nonogramPalette =
        {
            Color.FromArgb(180, 0, 0),   // dark red
            Color.FromArgb(0, 0, 180),   // dark blue
            Color.FromArgb(0, 120, 0),   // dark green
            Color.FromArgb(160, 80, 0),  // brown
            Color.FromArgb(120, 0, 120), // purple
            Color.FromArgb(0, 130, 130), // turquoise
            Color.FromArgb(200, 100, 0),  // orange
            Color.FromArgb(90, 90, 90)   // dark gray
        };
        public bool[,] userXMark;
        public bool[,] isHintFixed;
        public bool[,] aiXMark;
        public int colorSimilarityThreshold = 40;
        public bool isDragging = false;
        public bool isDraggingStarted = false;
        public bool aiButtonClicked = false;
        public bool isZooming = false;
        public Button lastProcessedButton = null;
        public MouseButtons dragButton;
        public int highlightedRow = -1;
        public int highlightedCol = -1;
        public int fixedGridTop;
        public int wrongCellClicks = 0;
        public int wrongColorClicks = 0;
        public int maxWrongCellClicks;
        public int maxWrongColorClicks;
        private Size originalButtonSize = new Size(50, 50);
        private float zoomFactor = 1.0f; // 1.0 = 100%
        private Nonogram form;
        private NonogramRender render;
        private GameTimerManager gameTimerManager;
        private UndoRedoManager undoredoManager;
        private NonogramSolver solver;
        private ExtraGridManager extraGridManager;

        public NonogramGrid(Nonogram f, GameTimerManager g, NonogramRender r, UndoRedoManager u, ExtraGridManager e)
        {
            this.form = f;
            this.render = r;
            this.gameTimerManager = g;
            this.undoredoManager = u;
            this.extraGridManager = e;
        }

        public void SetRender(NonogramRender r)
        {
            render = r;
        }

        public void SetTimerManager(GameTimerManager g)
        {
            gameTimerManager = g;
        }

        public void SetExtraGridManager(ExtraGridManager e)
        {
            this.extraGridManager = e;
        }

        public void BtnGenerateRandom_Click(object sender, EventArgs e)
        {
            // Check username
            string currentUsername = form.txtUsername.Text.Trim();
            if (string.IsNullOrEmpty(currentUsername))
            {
                MessageBox.Show("Please enter your username before starting the game!", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Fix username
            form.username = currentUsername;
            form.txtUsername.Enabled = false;

            gameTimerManager.gameStarted = true;

            isColor = form.cmbMode.SelectedItem?.ToString() == "Colored";
            // Display buttons and controls
            form.btnSolve.Visible = true;
            form.btnHint.Visible = true;
            form.btnCheck.Visible = true;
            form.btnUndo.Visible = true;
            form.btnRedo.Visible = true;
            form.cmbDifficulty.Visible = false;
            form.cmbMode.Visible = false;
            form.rbNumberEntryMode.Visible = false;
            form.rbImgBlackWhiteMode.Visible = false;
            form.rbImgColorMode.Visible = false;
            form.lblTimer.Visible = true;
            form.picPreview.Visible = true;
            form.chkShowX.Enabled = true;
            form.btnSolve.Enabled = true;
            form.btnHint.Enabled = true;
            form.btnCheck.Enabled = true;
            form.btnUndo.Enabled = true;
            form.btnRedo.Enabled = true;
            form.btnTips.Visible = false;
            form.btnRestart.Visible = true;
            form.btnBackToHome.Visible = true;
            form.btnExtraGenerate.Visible = false;
            form.btnLeaderboard.Visible = false;
            form.lblExtra.Visible = false;
            form.btnGenerateRandom.Visible = false;
            form.groupModes.Visible = false;
            form.lblUsername.Location = new Point(20, 430);
            form.txtUsername.Location = new Point(150, 425);
            form.lblWrongCellClicks.Location = new Point(20, 480);
            form.lblWrongColorClicks.Location = new Point(20, 500);
            form.lblHintCount.Location = new Point(20, 520);
            form.lblUndoCount.Location = new Point(20, 540);
            form.btnBackToHome.Location = new Point(720, 650);
            form.Size = new Size(1000, 800);

            // Prepare Grid
            ClearGrid();
            gameTimerManager.ResetCellClicks();
            gameTimerManager.ResetColorClicks();
            gameTimerManager.ResetHintClicks();
            undoredoManager.ClearHistory();
            form.picPreview.Image = null;
            form.btnShowExtraSolution.Visible = false;
            form.btnPickColor.Visible = false;
            gameTimerManager.elapsedSeconds = 0;
            gameTimerManager.DifficultyOrModeChanged();
            extraGridManager.ClearAllClueInputs();
            render.ToggleXMarks(form.chkShowX.Checked);
            render.UpdatePreview();
            form.picSolutionPreview.Image = render.GeneratePreviewImage();
            form.picSolutionPreview.SizeMode = PictureBoxSizeMode.Zoom;
            gameTimerManager.SetMaxWrongClicksByDifficulty();
            gameTimerManager.SetMaxHintsByDifficulty();
        }

        public void GridCell_MouseDown(object sender, MouseEventArgs e)
        {
            Button btn = sender as Button;
            if (btn == null)
            {
                return;
            }

            Point pos = (Point)btn.Tag;
            int row = pos.X;
            int col = pos.Y;

            if (isHintFixed[row, col])
            {
                return;
            }

            // Save for Undo/Redo
            if (!isDraggingStarted)
            {
                bool isXAction = e.Button == MouseButtons.Right;
                undoredoManager.SaveState(isXAction);
                isDraggingStarted = true;
            }

            bool wrongCell = false;
            bool wrongColor = false;

            // left click coloring
            if (e.Button == MouseButtons.Left)
            {
                render.showHints = true;
                render.ClearErrorHighlights();
                // If there was an X on it, delete it before coloring
                if (userXMark[row, col])
                {
                    render.ClearCell(row, col, btn);
                }

                // Coloring logic
                if (isColor)
                {
                    if (userColorRGB[row, col].ToArgb() == selectedColor.ToArgb())
                    {
                        render.ClearCell(row, col, btn);
                    }
                    else
                    {
                        render.SetCellColor(row, col, btn, selectedColor);
                    }
                }
                else
                {
                    if (userColor[row, col] == 1)
                    {
                        render.ClearCell(row, col, btn);
                    }
                    else
                    {
                        render.SetCellBlack(row, col, btn);
                    }
                }

                // error checking (we only count errors when coloring)
                bool isEmpty = isColor
                    ? userColorRGB[row, col] == Color.White
                    : userColor[row, col] == 0;

                if (!isEmpty)
                {
                    if (isColor)
                    {
                        if (solutionColorRGB[row, col] == Color.White)
                        {
                            wrongCell = true;
                        }
                        else if (userColorRGB[row, col].ToArgb() != solutionColorRGB[row, col].ToArgb())
                        {
                            wrongColor = true;
                        }
                    }
                    else
                    {
                        if (solutionBW[row, col] == 0)
                        {
                            wrongCell = true;
                        }
                    }
                }
            }
            // right click X mark
            else if (e.Button == MouseButtons.Right)
            {
                render.showHints = true;
                render.ClearErrorHighlights();
                if (userXMark[row, col])
                {
                    render.ClearCell(row, col, btn);
                }
                else
                {
                    render.SetCellX(row, col, btn);
                }
            }

            // Update UI and preview
            HandleErrorCounts(wrongCell, wrongColor);
            render.UpdatePreview(row, col);

            if (!aiButtonClicked && render.IsSolved())
            {
                render.FinalizeGame();
            }

            form.Refresh();
        }
        public void GridCell_MouseEnter(object sender, EventArgs e)
        {
            Button btn = sender as Button;
            if (btn == null)
            {
                return;
            }
            Point pos = (Point)btn.Tag;
            highlightedRow = pos.X;
            highlightedCol = pos.Y;
            render.RefreshGrid();
        }
        public void GridCell_MouseLeave(object sender, EventArgs e)
        {
            highlightedRow = -1;
            highlightedCol = -1;
            render.RefreshGrid();
        }

        public void GridCell_MouseWheel(object sender, MouseEventArgs e)
        {
            isZooming = true;
            // Save for colors and X
            Color[,] savedColors = new Color[row, col];
            bool[,] savedX = new bool[row, col];
            bool[,] savedHints = new bool[row, col];
            bool[,] isErrorCell = new bool[row, col];

            for (int rowIndex = 0; rowIndex < row; rowIndex++)
            {
                for (int colIndex = 0; colIndex < col; colIndex++)
                {
                    Button btn = gridButtons[rowIndex, colIndex];
                    savedColors[rowIndex, colIndex] = btn.BackColor;
                    savedX[rowIndex, colIndex] = btn.Text == "X";
                    savedHints[rowIndex, colIndex] = isHintFixed[rowIndex, colIndex];
                    isErrorCell[rowIndex, colIndex] = btn.BackColor == Color.DarkRed;
                }
            }

            // Calculate zoom
            if (e.Delta > 0)
            {
                zoomFactor -= 1.1f; // zoom out
            }
            else
            {
                zoomFactor += 1.1f; // zoom
            }

            float minZoom = 0.5f;
            float maxZoom = 3.0f;
            zoomFactor = Math.Clamp(zoomFactor, minZoom, maxZoom);

            userCellSize = Math.Max(5, (int)(originalButtonSize.Width * zoomFactor));
            form.SuspendLayout();

            // Regenerate Grid
            CreateGridUI(0, 0);
            isZooming = false;

            // Reset state
            for (int rowIndex = 0; rowIndex < row; rowIndex++)
            {
                for (int colIndex = 0; colIndex < col; colIndex++)
                {
                    Button btn = gridButtons[rowIndex, colIndex];

                    if (savedHints[rowIndex, colIndex])
                    {
                        btn.Enabled = false;
                        btn.Text = "";
                        isHintFixed[rowIndex, colIndex] = true;

                        Color hintColor = isColor
                            ? solutionColorRGB[rowIndex, colIndex]
                            : (solutionBW[rowIndex, colIndex] == 1 ? Color.Black : Color.White);

                        btn.BackColor = hintColor;

                        userColorRGB[rowIndex, colIndex] = hintColor;

                        if (!isColor)
                        {
                            userColor[rowIndex, colIndex] =
                                hintColor.ToArgb() == Color.Black.ToArgb() ? 1 : 0;
                        }

                        continue;
                    }
                    if (isErrorCell[rowIndex, colIndex])
                    {
                        btn.BackColor = Color.DarkRed;

                        // Reset X signal even for invalid field
                        if (savedX[rowIndex, colIndex])
                        {
                            btn.Text = "X";
                            float fontSize = userCellSize * 0.3f;
                            btn.Font = new Font("Arial", fontSize, FontStyle.Bold);
                            btn.ForeColor = Color.Gray;
                            btn.TextAlign = ContentAlignment.MiddleCenter;
                            userXMark[rowIndex, colIndex] = true;
                        }
                        else
                        {
                            btn.Text = "";
                            userXMark[rowIndex, colIndex] = false;
                        }

                        continue; // don't overwrite the color
                    }
                    // Reset colors
                    btn.BackColor = savedColors[rowIndex, colIndex];

                    // Reset X signals
                    if (savedX[rowIndex, colIndex])
                    {
                        btn.Text = "X";
                        float fontSize = userCellSize * 0.3f;
                        btn.Font = new Font("Arial", fontSize, FontStyle.Bold);
                        btn.ForeColor = Color.Gray;
                        btn.TextAlign = ContentAlignment.MiddleCenter;
                        userXMark[rowIndex, colIndex] = true;
                    }
                    else
                    {
                        btn.Text = "";
                        userXMark[rowIndex, colIndex] = false;
                        userColorRGB[rowIndex, colIndex] = savedColors[rowIndex, colIndex];
                    }
                    if (!isColor && !isErrorCell[rowIndex, colIndex]) // BW mode
                    {
                        userColor[rowIndex, colIndex] = savedColors[rowIndex, colIndex].ToArgb() == Color.Black.ToArgb() ? 1 : 0;
                        btn.BackColor = userColor[rowIndex, colIndex] == 1 ? Color.Black : Color.White;
                    }
                }
            }
            form.ResumeLayout();
            form.Refresh();
        }
        public void GenerateNonogram(int gridLeft, int gridTop, int width, int height, int targetPixels, int maxAttempts)
        {
            Random rnd = form.rnd;

            Color[] twoColors = null;

            if (isColor)
            {
                twoColors = render.GetTwoRandomColors();
            }

            bool isBoardGood = false;
            int attemptOverall = 0;

            while (!isBoardGood && attemptOverall < maxAttempts)
            {
                attemptOverall++;

                int[,] finalBW = new int[height, width];
                Color[,] finalColors = new Color[height, width];
                int currentPixels = 0;
                bool error = false;

                int attempts = 0;
                while (currentPixels < targetPixels && attempts < maxAttempts)
                {
                    attempts++;

                    int row = form.rnd.Next(height);
                    int maxLen = Math.Min(isColor ? 7 : 6, width);
                    int minLen = 2; // absolutely minimize the 1×1 blocks
                    int len = form.rnd.Next(minLen, maxLen + 1);

                    int startCol = form.rnd.Next(0, width - len + 1);

                    // is the space free?
                    bool free = true;
                    for (int offset = 0; offset < len; offset++)
                    {
                        if (finalBW[row, startCol + offset] == 1)
                        {
                            free = false;
                            break;
                        }
                    }
                    if (!free)
                    {
                        continue;
                    }

                    // If len==1, try to connect to a neighbor
                    if (len == 1)
                    {
                        // Look at the left side
                        if (startCol > 0 && finalBW[row, startCol - 1] == 1)
                        {
                            len = 2;
                        }
                        // Look at the right side
                        else if (startCol + 1 < width && finalBW[row, startCol + 1] == 1)
                        {
                            startCol = startCol - 1;
                            len = 2;
                        }
                        else
                        {
                            continue; // don't allow it to be created
                        }
                    }

                    // Block color
                    Color blockColor = isColor ? nonogramPalette[form.rnd.Next(0, 2)] : Color.Black;

                    // Place block
                    for (int offset = 0; offset < len && currentPixels < targetPixels; offset++)
                    {
                        finalBW[row, startCol + offset] = 1;
                        finalColors[row, startCol + offset] = blockColor;
                        currentPixels++;
                    }
                }

                if (currentPixels != targetPixels)
                {
                    continue;
                }

                // Each row/column should get at least one pixel
                for (int rowIndex = 0; rowIndex < height && !error; rowIndex++)
                {
                    if (!Enumerable.Range(0, width).Any(colIndex => finalBW[rowIndex, colIndex] == 1))
                    {
                        error = true;
                    }
                }
                for (int colIndex = 0; colIndex < width && !error; colIndex++)
                {
                    if (!Enumerable.Range(0, height).Any(rowIndex => finalBW[rowIndex, colIndex] == 1))
                    {
                        error = true;
                    }
                }
                if (error)
                {
                    continue;
                }

                // disable 2x2 chess patterns
                for (int rowIndex = 0; rowIndex < height - 1 && !error; rowIndex++)
                {
                    for (int colIndex = 0; colIndex < width - 1; colIndex++)
                    {
                        if (finalBW[rowIndex, colIndex] == finalBW[rowIndex + 1, colIndex + 1] &&
                            finalBW[rowIndex, colIndex + 1] == finalBW[rowIndex + 1, colIndex] &&
                            finalBW[rowIndex, colIndex] != finalBW[rowIndex, colIndex + 1])
                        {
                            error = true;
                            break;
                        }
                    }
                }

                if (error)
                {
                    continue;
                }

                // Disallow identical rows
                for (int rowIndex = 0; rowIndex < height - 1 && !error; rowIndex++)
                {
                    for (int nextRowIndex = rowIndex + 1; nextRowIndex < height; nextRowIndex++)
                    {
                        if (Enumerable.Range(0, width).All(colIndex => finalBW[rowIndex, colIndex]
                            == finalBW[nextRowIndex, colIndex]))
                        {
                            error = true;
                        }
                    }
                }
                if (error)
                {
                    continue;
                }

                // Record solution
                row = height;
                col = width;
                solutionBW = finalBW;
                solutionColorRGB = finalColors;

                render.ApplyColorsToBlocks(twoColors);
                GenerateClues();

                solver = new NonogramSolver(this);
                if (!solver.IsUniqueSolution())
                {
                    continue;
                }
                if (!solver.HasLogicalStartMoves())
                {
                    continue;
                }

                isBoardGood = true;
            }

            if (!isBoardGood)
            {
                gameTimerManager.RestartGameWithCurrentDifficulty();
                return;
            }

            // Initialize player state
            userXMark = new bool[row, col];
            userColorRGB = new Color[row, col];
            userColor = new int[row, col];
            for (int rowIndex = 0; rowIndex < row; rowIndex++)
            {
                for (int colIndex = 0; colIndex < col; colIndex++)
                {
                    userXMark[rowIndex, colIndex] = false;
                    userColorRGB[rowIndex, colIndex] = Color.White;
                    userColor[rowIndex, colIndex] = 0;
                }
            }
            CreateGridUI(gridLeft, gridTop);
        }

        private void GenerateClues()
        {
            rowClues = new int[row][];
            rowClueColors = new Color[row][];
            colClues = new int[col][];
            colClueColors = new Color[col][];

            // generate rows
            for (int rowIndex = 0; rowIndex < row; rowIndex++)
            {
                List<int> clues = new List<int>();
                List<Color> colors = new List<Color>();
                int colIndex = 0;

                while (colIndex < col)
                {
                    // If the cell is empty, move on
                    if (solutionBW[rowIndex, colIndex] == 0)
                    {
                        colIndex++;
                        continue;
                    }

                    // Start a new block
                    int count = 0;
                    Color currentBlockColor = solutionColorRGB[rowIndex, colIndex];
                    int blockEndIndex = colIndex;

                    while (blockEndIndex < col && solutionBW[rowIndex, blockEndIndex] == 1 &&
                          (!isColor || solutionColorRGB[rowIndex, blockEndIndex] == currentBlockColor))
                    {
                        count++;
                        blockEndIndex++;
                    }

                    // Save block
                    clues.Add(count);
                    colors.Add(isColor ? currentBlockColor : Color.Black);

                    colIndex = blockEndIndex;
                }

                rowClues[rowIndex] = clues.ToArray();
                rowClueColors[rowIndex] = colors.ToArray();
            }

            // generate columns
            for (int colIndex = 0; colIndex < col; colIndex++)
            {
                List<int> clues = new List<int>();
                List<Color> colors = new List<Color>();
                int rowIndex = 0;

                while (rowIndex < row)
                {
                    if (solutionBW[rowIndex, colIndex] == 0)
                    {
                        rowIndex++;
                        continue;
                    }

                    int count = 0;
                    Color currentBlockColor = solutionColorRGB[rowIndex, colIndex];
                    int blockEndIndex = rowIndex;

                    while (blockEndIndex < row && solutionBW[blockEndIndex, colIndex] == 1 &&
                          (!isColor || solutionColorRGB[blockEndIndex, colIndex] == currentBlockColor))
                    {
                        count++;
                        blockEndIndex++;
                    }

                    clues.Add(count);
                    colors.Add(isColor ? currentBlockColor : Color.Black);

                    rowIndex = blockEndIndex;
                }

                colClues[colIndex] = clues.ToArray();
                colClueColors[colIndex] = colors.ToArray();
            }
        }

        public void AdjustGridSize()
        {
            int maxGridWidth = form.ClientSize.Width - 40;
            int maxGridHeight = form.ClientSize.Height - fixedGridTop - 200;

            int maxRowClues = MaxClueLength(rowClues);
            int maxColClues = MaxClueLength(colClues);

            int cellWidth = (maxGridWidth - maxRowClues * userCellSize) / col;
            int cellHeight = (maxGridHeight - maxColClues * userCellSize) / row;

            userCellSize = Math.Min(cellWidth, cellHeight);
            userCellSize = Math.Min(userCellSize, 50);
            userCellSize = Math.Max(userCellSize, 15);
        }

        public int MaxClueLength(int[][] clues)
        {
            if (clues == null)
            {
                return 0;
            }
            int max = 0;
            foreach (int[] array in clues)
            {
                if (array.Length > max)
                {
                    max = array.Length;
                }
            }
            return max;
        }

        public void InitializeGridPosition()
        {
            int margin = 10;
            fixedGridTop = Math.Max(20, form.chkShowX.Bottom) + margin;
        }

        public void ClearGrid()
        {
            // Remove visual buttons from the Form
            if (gridButtons != null)
            {
                foreach (Button btn in gridButtons)
                {
                    if (btn != null)
                    {
                        if (form.Controls.Contains(btn))
                        {
                            form.Controls.Remove(btn);
                        }
                        btn.Dispose();
                    }
                }
            }

            // Remove clues (numbers)
            List<Label> clueLabels = form.Controls.OfType<Label>().Where(l => l.Name.Contains("clueLabel")).ToList();
            foreach (Label label in clueLabels)
            {
                form.Controls.Remove(label);
                label.Dispose();
            }

            // Reinitialize logical data
            gridButtons = new Button[row, col];
            userXMark = new bool[row, col];
            userColor = new int[row, col];
            userColorRGB = new Color[row, col];
            isHintFixed = new bool[row, col];
            aiXMark = new bool[row, col];

            // fill userColorRGB with white (so that the empty track is not black)
            for (int rowIndex = 0; rowIndex < row; rowIndex++)
            {
                for (int colIndex = 0; colIndex < col; colIndex++)
                {
                    userColorRGB[rowIndex, colIndex] = Color.White;
                }
            }
        }

        // Create buttons and labels
        public void CreateGridUI(int gridLeft, int gridTop)
        {
            AdjustGridSize();
            ClearGrid();

            gridButtons = new Button[row, col];
            int cellSize = userCellSize;

            int maxRowClues = MaxClueLength(rowClues);
            int maxColClues = MaxClueLength(colClues);

            // Fix grid position within the form
            int startX = 550;
            int startY = 300;

            // create column clues (vertical)
            for (int colIndex = 0; colIndex < col; colIndex++)
            {
                int[] clues = colClues[colIndex];

                // Calculate label heights
                int totalHeight = 0;
                int[] clueHeights = new int[clues.Length];
                for (int clueIndex = 0; clueIndex < clues.Length; clueIndex++)
                {
                    Size textSize = TextRenderer.MeasureText(clues[clueIndex].ToString(), form.Font);
                    clueHeights[clueIndex] = Math.Max(cellSize, textSize.Height + 4);
                    totalHeight += clueHeights[clueIndex];
                }

                int yPos = startY - totalHeight;
                for (int clueIndex = 0; clueIndex < clues.Length; clueIndex++)
                {
                    Label lbl = new Label();
                    lbl.Name = "clueLabel";
                    lbl.Text = clues[clueIndex].ToString();
                    lbl.TextAlign = ContentAlignment.MiddleCenter;
                    lbl.BorderStyle = BorderStyle.FixedSingle;

                    if (isColor)
                    {
                        Color clueColor = colClueColors[colIndex][clueIndex];
                        lbl.BackColor = clueColor;
                        lbl.ForeColor = ((clueColor.R + clueColor.G + clueColor.B) / 3 < 128) ? Color.White : Color.Black;
                    }
                    else
                    {
                        lbl.BackColor = Color.Gray;
                        lbl.ForeColor = Color.White;
                    }

                    lbl.Size = new Size(cellSize, clueHeights[clueIndex]);
                    lbl.Location = new Point(startX + colIndex * cellSize, yPos);
                    form.Controls.Add(lbl);

                    yPos += clueHeights[clueIndex];
                }
            }

            // create row clues (horizontal)
            for (int rowIndex = 0; rowIndex < row; rowIndex++)
            {
                int[] clues = rowClues[rowIndex];

                // Calculate label widths
                int totalWidth = 0;
                int[] clueWidths = new int[clues.Length];
                for (int clueIndex = 0; clueIndex < clues.Length; clueIndex++)
                {
                    Size textSize = TextRenderer.MeasureText(clues[clueIndex].ToString(), form.Font);
                    clueWidths[clueIndex] = Math.Max(cellSize, textSize.Width + 4);
                    totalWidth += clueWidths[clueIndex];
                }

                int xPos = startX - totalWidth;
                for (int clueIndex = 0; clueIndex < clues.Length; clueIndex++)
                {
                    Label lbl = new Label();
                    lbl.Name = "clueLabel";
                    lbl.Text = clues[clueIndex].ToString();
                    lbl.TextAlign = ContentAlignment.MiddleCenter;
                    lbl.BorderStyle = BorderStyle.FixedSingle;

                    if (isColor)
                    {
                        Color clueColor = rowClueColors[rowIndex][clueIndex];
                        lbl.BackColor = clueColor;
                        lbl.ForeColor = ((clueColor.R + clueColor.G + clueColor.B) / 3 < 128) ? Color.White : Color.Black;
                    }
                    else
                    {
                        lbl.BackColor = Color.Gray;
                        lbl.ForeColor = Color.White;
                    }

                    lbl.Size = new Size(clueWidths[clueIndex], cellSize);
                    lbl.Location = new Point(xPos, startY + rowIndex * cellSize);
                    form.Controls.Add(lbl);

                    xPos += clueWidths[clueIndex];
                }
            }

            // create grid buttons
            for (int rowIndex = 0; rowIndex < row; rowIndex++)
            {
                for (int colIndex = 0; colIndex < col; colIndex++)
                {
                    Button btn = new Button();
                    btn.Name = "gridCell";
                    btn.Size = new Size(cellSize, cellSize);
                    btn.Location = new Point(startX + colIndex * cellSize, startY + rowIndex * cellSize);
                    btn.BackColor = Color.White;
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.FlatAppearance.BorderColor = Color.Gray;
                    btn.FlatAppearance.BorderSize = 1;
                    btn.Tag = new Point(rowIndex, colIndex);
                    btn.MouseDown += GridCell_MouseDown;
                    btn.MouseEnter += GridCell_MouseEnter;
                    btn.MouseLeave += GridCell_MouseLeave;
                    btn.MouseWheel += GridCell_MouseWheel;
                    btn.Paint += (s, e) =>
                    {
                        btn = s as Button;
                        if (btn == null)
                        {
                            return;
                        }
                        Point pos = (Point)btn.Tag;
                        int targetRow = pos.X;
                        int targetCol = pos.Y;
                        if (render.showHints)
                        {
                            if (targetRow == highlightedRow || targetCol == highlightedCol)
                            {
                                using (Brush br = new SolidBrush(Color.FromArgb(60, Color.LightBlue)))
                                {
                                    e.Graphics.FillRectangle(br, btn.ClientRectangle);
                                }
                            }
                        }
                        using (Pen thickPen = new Pen(Color.Black, 2))
                        {
                            if (targetRow % 5 == 0) e.Graphics.DrawLine(thickPen, 0, 0, btn.Width, 0);
                            if (targetCol % 5 == 0) e.Graphics.DrawLine(thickPen, 0, 0, 0, btn.Height);
                            if (targetCol == col - 1) e.Graphics.DrawLine(thickPen, btn.Width - 1, 0,
                                btn.Width - 1, btn.Height);
                            if (targetRow == row - 1) e.Graphics.DrawLine(thickPen, 0, btn.Height - 1,
                                btn.Width, btn.Height - 1);
                        }
                    };
                    btn.MouseDown += (s, e) =>
                    {
                        btn = s as Button;
                        if (btn == null)
                        {
                            return;
                        }

                        if (e.Button == MouseButtons.Left || (e.Button == MouseButtons.Right))
                        {
                            isDragging = false;
                            dragButton = e.Button;
                            lastProcessedButton = btn;
                        }
                    };

                    btn.MouseMove += (s, e) =>
                    {
                        if (Control.MouseButtons != dragButton)
                        {
                            return;
                        }

                        Point mousePos = form.PointToClient(Control.MousePosition);
                        Control ctrl = form.GetChildAtPoint(mousePos);
                        Button targetBtn = ctrl as Button;

                        if (targetBtn == null || targetBtn == lastProcessedButton)
                        {
                            return;
                        }

                        isDragging = true;
                        lastProcessedButton = targetBtn;

                        GridCell_MouseDown(targetBtn, new MouseEventArgs(dragButton, 0, 0, 0, 0));
                    };

                    btn.MouseUp += (s, e) =>
                    {
                        isDragging = false;
                        lastProcessedButton = null;
                        isDraggingStarted = false;
                    };
                    form.Controls.Add(btn);
                    gridButtons[rowIndex, colIndex] = btn;
                }
            }

            // Color palette (color mode only)
            if (isColor)
            {
                HashSet<int> colorsSet = new HashSet<int>();
                for (int rowIndex = 0; rowIndex < row; rowIndex++)
                {
                    for (int colIndex = 0; colIndex < col; colIndex++)
                    {
                        Color color = solutionColorRGB[rowIndex, colIndex];
                        if (color != Color.White)
                        {
                            colorsSet.Add(color.ToArgb());
                        }
                    }
                }

                form.colorPalette.Controls.Clear();
                form.colorPalette.Visible = true;
                form.colorPalette.AutoScroll = false;
                form.colorPalette.WrapContents = true;
                form.colorPalette.FlowDirection = FlowDirection.LeftToRight;

                int btnSize = 30;
                int margin = 2;
                int maxCols = 10;
                int totalColors = colorsSet.Count;
                int rowsNeeded = (int)Math.Ceiling(totalColors / (double)maxCols);
                form.colorPalette.Size = new Size(maxCols * (btnSize + margin * 2), rowsNeeded * (btnSize + margin * 2));

                foreach (int argb in colorsSet)
                {
                    Color color = Color.FromArgb(argb);
                    Button colorBtn = new Button();
                    colorBtn.BackColor = color;
                    colorBtn.Size = new Size(btnSize, btnSize);
                    colorBtn.Margin = new Padding(margin);
                    colorBtn.FlatStyle = FlatStyle.Flat;
                    colorBtn.FlatAppearance.BorderSize = 1;
                    colorBtn.FlatAppearance.BorderColor = Color.Gray;

                    colorBtn.Click += (s, e) =>
                    {
                        render.SelectColor(colorBtn);
                    };

                    form.colorPalette.Controls.Add(colorBtn);
                }
                if (isColor && form.colorPalette.Controls.Count > 0)
                {
                    if (isZooming)
                    {
                        // When zooming, the selected color should be highlighted
                        foreach (Control ctrl in form.colorPalette.Controls)
                        {
                            Button colorBtn = ctrl as Button;
                            if (colorBtn != null && colorBtn.BackColor == selectedColor)
                            {
                                render.SelectColor(colorBtn);
                                break;
                            }
                        }
                    }
                    else
                    {
                        // Initially, the first color should be selected
                        Button firstColorButton = form.colorPalette.Controls[0] as Button;
                        if (firstColorButton != null)
                        {
                            render.SelectColor(firstColorButton);
                            selectedColor = firstColorButton.BackColor;
                        }
                    }
                }
            }
            else
            {
                form.colorPalette.Visible = false;
            }
        }

        public void HandleErrorCounts(bool wrongCell, bool wrongColor)
        {
            if (wrongCell)
            {
                wrongCellClicks++;
                form.lblWrongCellClicks.Text = $"Number of incorrect clicks: {wrongCellClicks} (max: {maxWrongCellClicks})";
            }

            if (wrongColor && isColor)
            {
                wrongColorClicks++;
                form.lblWrongColorClicks.Text = $"Number of colors: {wrongColorClicks} (max: {maxWrongColorClicks})";
            }

            if (wrongCellClicks >= maxWrongCellClicks || (isColor && wrongColorClicks >= maxWrongColorClicks))
            {
                MessageBox.Show("You have reached the maximum number of errors! The game will restart.");
                gameTimerManager.RestartGameWithCurrentDifficulty();
            }
        }
        public void GenerateNonogramFromImage(Image img, int gridLeft, int gridTop)
        {
            // Analysis bitmap (30x30 workspace for processing)
            int tempWidth = 30;
            int tempHeight = 30;
            Bitmap tempBmp = new Bitmap(tempWidth, tempHeight);
            using (Graphics graphics = Graphics.FromImage(tempBmp))
            {
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                graphics.Clear(Color.White);
                graphics.DrawImage(img, new Rectangle(0, 0, tempWidth, tempHeight));
            }

            // Crop Find and trim empty (white) edges
            int minX = tempWidth, maxX = 0, minY = tempHeight, maxY = 0;
            bool hasContent = false;
            for (int rowIndex = 0; rowIndex < tempHeight; rowIndex++)
            {
                for (int colIndex = 0; colIndex < tempWidth; colIndex++)
                {
                    Color pixelColor = tempBmp.GetPixel(colIndex, rowIndex);
                    // If the pixel is not white (for values ​​above 240 it is considered white)
                    if (pixelColor.R < 240 || pixelColor.G < 240 || pixelColor.B < 240)
                    {
                        if (colIndex < minX) minX = colIndex; if (colIndex > maxX) maxX = colIndex;
                        if (rowIndex < minY) minY = rowIndex; if (rowIndex > maxY) maxY = rowIndex;
                        hasContent = true;
                    }
                }
            }

            // If the image is completely empty, we give a basic 5x5 grid
            if (!hasContent)
            {
                minX = 0;
                maxX = 4;
                minY = 0;
                maxY = 4;
            }

            int finalWidth = maxX - minX + 1;
            int finalHeight = maxY - minY + 1;

            // Create a working bitmap with the cropped size
            Bitmap bmp = new Bitmap(finalWidth, finalHeight);
            using (Graphics graphics = Graphics.FromImage(bmp))
            {
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                graphics.DrawImage(tempBmp, new Rectangle(0, 0, finalWidth, finalHeight),
                            new Rectangle(minX, minY, finalWidth, finalHeight), GraphicsUnit.Pixel);
            }

            // Initialize data structures
            row = finalHeight;
            col = finalWidth;
            isColor = false;

            solutionColorRGB = new Color[row, col];
            solutionBW = new int[row, col];
            userColorRGB = new Color[row, col];
            userColor = new int[row, col];
            userXMark = new bool[row, col];
            gridButtons = new Button[row, col];
            bool[,] fillableCells = new bool[row, col];

            // Process pixel data using consistent threshold
            for (int rowIndex = 0; rowIndex < row; rowIndex++)
            {
                for (int colIndex = 0; colIndex < col; colIndex++)
                {
                    Color pixel = bmp.GetPixel(colIndex, rowIndex);
                    solutionColorRGB[rowIndex, colIndex] = pixel;

                    // Calculate luminance (Y = 0.299R + 0.587G + 0.114B)
                    double luminance = 0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B;

                    // Consistent decision if darker than 220, then paint (1)
                    bool isFilled = luminance < 220;
                    fillableCells[rowIndex, colIndex] = isFilled;
                    solutionBW[rowIndex, colIndex] = isFilled ? 1 : 0;

                    // Color detection if there is a significant difference between the channels, we switch to color mode
                    int maxDiff = Math.Max(Math.Abs(pixel.R - pixel.G),
                                  Math.Max(Math.Abs(pixel.G - pixel.B), Math.Abs(pixel.R - pixel.B)));
                    if (maxDiff > 20)
                    {
                        isColor = true;
                    }

                    // UI default
                    userColorRGB[rowIndex, colIndex] = Color.White;
                    userXMark[rowIndex, colIndex] = false;
                }
            }

            // Calculate clues for rows
            rowClues = new int[row][];
            rowClueColors = new Color[row][];
            for (int rowIndex = 0; rowIndex < row; rowIndex++)
            {
                List<int> clues = new List<int>();
                List<Color> colors = new List<Color>();
                int colIndex = 0;
                while (colIndex < col)
                {
                    if (!fillableCells[rowIndex, colIndex])
                    {
                        colIndex++;
                        continue;
                    }

                    int count = 0;
                    Color seed = solutionColorRGB[rowIndex, colIndex];
                    int blockEndIndex = colIndex;
                    while (blockEndIndex < col && fillableCells[rowIndex, blockEndIndex])
                    {
                        Color color = solutionColorRGB[rowIndex, blockEndIndex];
                        // If the mode is colored, we start a new block at the color difference
                        if (isColor && count > 0 && !render.AreColorsSimilar(seed, color, colorSimilarityThreshold))
                        {
                            break;
                        }
                        count++;
                        blockEndIndex++;
                    }
                    clues.Add(count);
                    colors.Add(isColor ? seed : Color.Black);
                    colIndex = blockEndIndex;
                }
                rowClues[rowIndex] = clues.ToArray();
                rowClueColors[rowIndex] = colors.ToArray();
            }

            // Calculate column clues
            colClues = new int[col][];
            colClueColors = new Color[col][];
            for (int colIndex = 0; colIndex < col; colIndex++)
            {
                List<int> clues = new List<int>();
                List<Color> colors = new List<Color>();
                int rowIndex = 0;
                while (rowIndex < row)
                {
                    if (!fillableCells[rowIndex, colIndex])
                    {
                        rowIndex++;
                        continue;
                    }

                    int count = 0;
                    Color seed = solutionColorRGB[rowIndex, colIndex];
                    int blockEndIndex = rowIndex;
                    while (blockEndIndex < row && fillableCells[blockEndIndex, colIndex])
                    {
                        Color color = solutionColorRGB[blockEndIndex, colIndex];
                        if (isColor && count > 0 && !render.AreColorsSimilar(seed, color, colorSimilarityThreshold))
                        {
                            break;
                        }
                        count++;
                        blockEndIndex++;
                    }
                    clues.Add(count);
                    colors.Add(isColor ? seed : Color.Black);
                    rowIndex = blockEndIndex;
                }
                colClues[colIndex] = clues.ToArray();
                colClueColors[colIndex] = colors.ToArray();
            }

            // Generate UI and show preview
            CreateGridUI(gridLeft, gridTop);

            if (form.picPreview != null)
            {
                Bitmap finalPreview = render.GeneratePreviewImage();
                form.picPreview.Size = new Size(render.previewSize, render.previewSize);
                form.picPreview.SizeMode = PictureBoxSizeMode.Zoom;
                form.picPreview.Image = finalPreview;
            }
            if (form.picSolutionPreview != null)
            {
                Bitmap finalPreview = render.GeneratePreviewImage();
                form.picSolutionPreview.Size = new Size(render.previewSize, render.previewSize);
                form.picSolutionPreview.SizeMode = PictureBoxSizeMode.Zoom;
                form.picSolutionPreview.Image = finalPreview;
            }

            // Free resources
            tempBmp.Dispose();
            bmp.Dispose();
        }
    }
}