using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
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
        public Color[] nonogramColors;
        public bool[,] userXMark;
        public bool[,] isHintFixed;
        public int colorSimilarityThreshold = 40;
        public bool isDragging = false;
        public bool isDraggingStarted = false;
        public bool aiButtonClicked = false;
        public bool gameFinished = false;
        public Button lastProcessedButton = null;
        public MouseButtons dragButton;
        public int highlightedRow = -1;
        public int highlightedCol = -1;
        public int fixedGridTop;
        public int wrongCellClicks = 0;
        public int wrongColorClicks = 0;
        public int maxWrongCellClicks;
        public int maxWrongColorClicks;
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
            // Username ellenőrzése
            string currentUsername = form.txtUsername.Text.Trim();
            if (string.IsNullOrEmpty(currentUsername))
            {
                MessageBox.Show("Kérlek, add meg a felhasználóneved a játék indítása előtt!", "Hiba", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Username fixálása
            form.username = currentUsername;
            form.txtUsername.Enabled = false;

            gameTimerManager.gameStarted = true;

            isColor = form.cmbMode.SelectedItem?.ToString() == "Színes";
            // Gombok és vezérlők megjelenítése
            form.btnSolve.Visible = true;
            form.btnHint.Visible = true;
            form.btnCheck.Visible = true;
            form.btnUndo.Visible = true;
            form.btnRedo.Visible = true;
            form.cmbDifficulty.Visible = false;
            form.cmbMode.Visible = false;
            form.chkExtraMode.Visible = false;
            form.chkBlackWhiteMode.Visible = false;
            form.chkColorMode.Visible = false;
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
            form.btnLeaderboard.Visible = false;
            form.lblExtra.Visible = false;
            form.btnGenerateRandom.Visible = false;

            // Grid előkészítése
            ClearGrid();
            gameTimerManager.ResetCellCliks();
            gameTimerManager.ResetColorClicks();
            undoredoManager.ClearHistory();
            form.picPreview.Image = null;
            wrongCellClicks = 0;
            form.btnShowExtraSolution.Visible = false;
            form.btnPickColor.Visible = false;
            gameTimerManager.elapsedSeconds = 0;
            gameTimerManager.DifficultyOrModeChanged();
            extraGridManager.ClearAllClueInputs();
            render.ToggleXMarks(form.chkShowX.Checked);
            render.UpdatePreview();
            if (form.cmbDifficulty.SelectedIndex != 2) // nem nehéz
            {
                gameTimerManager.StartTimer();
            }
            form.picSolutionPreview.Image = render.GeneratePreviewImage();
            form.picSolutionPreview.SizeMode = PictureBoxSizeMode.Zoom;
            gameTimerManager.SetMaxWrongClicksByDifficulty();
            gameTimerManager.SetMaxHintsByDifficulty();
        }

        public void GridCell_MouseDown(object sender, MouseEventArgs e)
        {
            Button btn = sender as Button;
            if (btn == null) return;

            Point p = (Point)btn.Tag;
            int row = p.X;
            int col = p.Y;

            // Időzítő indítása az első kattintásnál
            if (!gameTimerManager.gameTimer.Enabled) gameTimerManager.gameTimer.Start();

            // Mentés visszavonáshoz (Undo/Redo)
            if (!isDraggingStarted)
            {
                bool isXAction = (e.Button == MouseButtons.Right);
                undoredoManager.SaveState(isXAction);
                isDraggingStarted = true;
            }

            bool wrongCell = false;
            bool wrongColor = false;

            // bal klikk színezés
            if (e.Button == MouseButtons.Left)
            {
                render.showHints = true;
                render.ClearErrorHighlights();
                // Ha volt rajta X, azt töröljük színezés előtt
                if (userXMark[row, col])
                {
                    render.ClearCell(row, col, btn);
                }

                // Színezés logikája
                if (isColor)
                {
                    if (userColorRGB[row, col].ToArgb() == selectedColor.ToArgb())
                        render.ClearCell(row, col, btn);
                    else
                        render.SetCellColor(row, col, btn, selectedColor);
                }
                else
                {
                    if (userColor[row, col] == 1)
                        render.ClearCell(row, col, btn);
                    else
                        render.SetCellBlack(row, col, btn);
                }

                // hibaellenőrzés (csak színezésnél számolunk hibát)
                bool isEmpty = isColor
                    ? userColorRGB[row, col] == Color.White
                    : userColor[row, col] == 0;

                if (!isEmpty)
                {
                    if (isColor)
                    {
                        if (solutionColorRGB[row, col] == Color.White)
                            wrongCell = true;
                        else if (userColorRGB[row, col].ToArgb() != solutionColorRGB[row, col].ToArgb())
                            wrongColor = true;
                    }
                    else
                    {
                        if (solutionBW[row, col] == 0)
                            wrongCell = true;
                    }
                }
            }
            // jobb klikk X jelzés
            else if (e.Button == MouseButtons.Right)
            {
                render.showHints = true;
                render.ClearErrorHighlights();
                if (userXMark[row, col])
                    render.ClearCell(row, col, btn);
                else
                    render.SetCellX(row, col, btn);
            }

            // UI és preview frissítése
            HandleErrorCounts(wrongCell, wrongColor);
            render.UpdatePreview(row, col);

            if (render.IsSolved() && !gameFinished)
            {
                gameFinished = true;
                gameTimerManager.Stop();

                if (!aiButtonClicked)
                {
                    render.FinalizeGame();
                }
                else
                {
                    MessageBox.Show(
                        "A játék AI segítséggel lett megoldva, ezért nem kerül ranglistára.\nIndíts új játékot a folytatáshoz!",
                        "AI mód",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }

            form.Refresh();
        }
        public void GridCell_MouseEnter(object sender, EventArgs e)
        {
            Button btn = sender as Button;
            if (btn == null) return;

            Point p = (Point)btn.Tag;
            highlightedRow = p.X;
            highlightedCol = p.Y;

            render.RefreshGrid();
        }

        public void GridCell_MouseLeave(object sender, EventArgs e)
        {
            highlightedRow = -1;
            highlightedCol = -1;

            render.RefreshGrid();
        }

        public void GenerateNonogram(int gridLeft, int gridTop, int width, int height, int targetPixels, int maxAttempts)
        {
            Random rnd = form.rnd;

            if (isColor)
                nonogramColors = render.GetTwoRandomColors();

            bool isBoardGood = false;

            while (!isBoardGood)
            {
                int[,] finalBW = new int[height, width];
                int currentPixels = 0;
                int attempts = 0;
                bool error = false;

                // Blokk-alapú generálás
                while (currentPixels < targetPixels && attempts < maxAttempts)
                {
                    attempts++;
                    int r = rnd.Next(height);
                    int c = rnd.Next(width);
                    int len = isColor ? rnd.Next(1, Math.Min(7, width)) : rnd.Next(2, Math.Min(6, width));

                    if (isColor && len == 1 && rnd.NextDouble() < 0.6)
                        continue;

                    if (c + len > width) continue;

                    bool free = true;
                    for (int k = 0; k < len; k++)
                        if (finalBW[r, c + k] == 1) free = false;

                    if (!free) continue;

                    for (int k = 0; k < len && currentPixels < targetPixels; k++)
                    {
                        finalBW[r, c + k] = 1;
                        currentPixels++;
                    }
                }

                if (currentPixels != targetPixels) continue;

                // Kötelező minden sor és oszlop kapjon legalább 1 pixelt
                for (int r = 0; r < height && !error; r++)
                    if (!Enumerable.Range(0, width).Any(c => finalBW[r, c] == 1)) error = true;
                for (int c = 0; c < width && !error; c++)
                    if (!Enumerable.Range(0, height).Any(r => finalBW[r, c] == 1)) error = true;
                if (error) continue;

                // 2x2 sakk minta tiltás
                for (int i = 0; i < height - 1 && !error; i++)
                    for (int j = 0; j < width - 1; j++)
                        if (finalBW[i, j] == finalBW[i + 1, j + 1] &&
                            finalBW[i, j + 1] == finalBW[i + 1, j] &&
                            finalBW[i, j] != finalBW[i, j + 1])
                        { error = true; break; }
                if (error) continue;

                // Identikus sorok tiltása
                for (int i = 0; i < height - 1 && !error; i++)
                    for (int k = i + 1; k < height; k++)
                        if (Enumerable.Range(0, width).All(j => finalBW[i, j] == finalBW[k, j])) error = true;
                if (error) continue;

                // Megoldás rögzítése
                row = height;
                col = width;
                solutionBW = finalBW;
                solutionColorRGB = new Color[height, width];

                render.ApplyColorsToBlocks();
                GenerateClues();

                solver = new NonogramSolver(this);
                if (!solver.IsUniqueSolution()) continue;

                isBoardGood = true;
            }

            // Játékos állapot inicializálása
            userXMark = new bool[row, col];
            userColorRGB = new Color[row, col];
            userColor = new int[row, col];

            for (int i = 0; i < row; i++)
                for (int j = 0; j < col; j++)
                {
                    userXMark[i, j] = false;
                    userColorRGB[i, j] = Color.White;
                    userColor[i, j] = 0;
                }

            CreateGridUI(gridLeft, gridTop);
        }

        private void GenerateClues()
        {
            rowClues = new int[row][];
            rowClueColors = new Color[row][];
            colClues = new int[col][];
            colClueColors = new Color[col][];

            // sorok generálása
            for (int i = 0; i < row; i++)
            {
                List<int> clues = new List<int>();
                List<Color> colors = new List<Color>();
                int j = 0;

                while (j < col)
                {
                    // Ha üres a cella, lépjünk tovább
                    if (solutionBW[i, j] == 0)
                    {
                        j++;
                        continue;
                    }

                    // Új blokk kezdődik
                    int count = 0;
                    Color currentBlockColor = solutionColorRGB[i, j];
                    int k = j;

                    while (k < col && solutionBW[i, k] == 1 &&
                          (!isColor || solutionColorRGB[i, k] == currentBlockColor))
                    {
                        count++;
                        k++;
                    }

                    // Blokk mentése
                    clues.Add(count);
                    colors.Add(isColor ? currentBlockColor : Color.Black);

                    j = k;
                }

                rowClues[i] = clues.ToArray();
                rowClueColors[i] = colors.ToArray();
            }

            // oszlopok generálása
            for (int j = 0; j < col; j++)
            {
                List<int> clues = new List<int>();
                List<Color> colors = new List<Color>();
                int i = 0;

                while (i < row)
                {
                    if (solutionBW[i, j] == 0)
                    {
                        i++;
                        continue;
                    }

                    int count = 0;
                    Color currentBlockColor = solutionColorRGB[i, j];
                    int k = i;

                    while (k < row && solutionBW[k, j] == 1 &&
                          (!isColor || solutionColorRGB[k, j] == currentBlockColor))
                    {
                        count++;
                        k++;
                    }

                    clues.Add(count);
                    colors.Add(isColor ? currentBlockColor : Color.Black);

                    i = k;
                }

                colClues[j] = clues.ToArray();
                colClueColors[j] = colors.ToArray();
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
            int max = 0;
            foreach (int[] arr in clues) if (arr.Length > max) max = arr.Length;
            return max;
        }

        public void InitializeGridPosition()
        {
            int margin = 10;
            fixedGridTop = Math.Max(20, form.chkShowX.Bottom) + margin;
        }

        public void ClearGrid()
        {
            // Vizuális gombok eltávolítása a Form-ról
            if (gridButtons != null)
            {
                foreach (Button b in gridButtons)
                {
                    if (b != null)
                    {
                        if (form.Controls.Contains(b))
                            form.Controls.Remove(b);
                        b.Dispose();
                    }
                }
            }

            // Cluek (számok) eltávolítása
            List<Label> clueLabels = form.Controls.OfType<Label>().Where(l => l.Name.Contains("clueLabel")).ToList();
            foreach (Label label in clueLabels)
            {
                form.Controls.Remove(label);
                label.Dispose();
            }

            // Logikai adatok újrainicializálása
            gridButtons = new Button[row, col];
            userXMark = new bool[row, col];
            userColor = new int[row, col];
            userColorRGB = new Color[row, col];
            isHintFixed = new bool[row, col];

            // userColorRGB feltöltése fehérrel (hogy ne legyen fekete az üres pálya)
            for (int i = 0; i < row; i++)
            {
                for (int j = 0; j < col; j++)
                {
                    userColorRGB[i, j] = Color.White;
                }
            }
        }

        // Gombok, labelek létrehozása
        public void CreateGridUI(int gridLeft, int gridTop)
        {
            AdjustGridSize();
            ClearGrid();

            gridButtons = new Button[row, col];
            int cellSize = userCellSize;

            int maxRowClues = MaxClueLength(rowClues);
            int maxColClues = MaxClueLength(colClues);

            // Fix grid pozíció a formon belül
            int startX = 550;
            int startY = 300;

            // oszlop cluek létrehozása (vertikális) 
            for (int j = 0; j < col; j++)
            {
                int[] clues = colClues[j];

                // Label magasságok számítása
                int totalHeight = 0;
                int[] clueHeights = new int[clues.Length];
                for (int i = 0; i < clues.Length; i++)
                {
                    Size textSize = TextRenderer.MeasureText(clues[i].ToString(), form.Font);
                    clueHeights[i] = Math.Max(cellSize, textSize.Height + 4);
                    totalHeight += clueHeights[i];
                }

                int yPos = startY - totalHeight;
                for (int i = 0; i < clues.Length; i++)
                {
                    Label lbl = new Label();
                    lbl.Name = "clueLabel";
                    lbl.Text = clues[i].ToString();
                    lbl.TextAlign = ContentAlignment.MiddleCenter;
                    lbl.BorderStyle = BorderStyle.FixedSingle;

                    if (isColor)
                    {
                        Color clueColor = colClueColors[j][i];
                        lbl.BackColor = clueColor;
                        lbl.ForeColor = ((clueColor.R + clueColor.G + clueColor.B) / 3 < 128) ? Color.White : Color.Black;
                    }
                    else
                    {
                        lbl.BackColor = Color.Gray;
                        lbl.ForeColor = Color.White;
                    }

                    lbl.Size = new Size(cellSize, clueHeights[i]);
                    lbl.Location = new Point(startX + j * cellSize, yPos);
                    form.Controls.Add(lbl);

                    yPos += clueHeights[i];
                }
            }

            // sor cluek létrehozása (horizontális)
            for (int i = 0; i < row; i++)
            {
                int[] clues = rowClues[i];

                // Label szélességek számítása
                int totalWidth = 0;
                int[] clueWidths = new int[clues.Length];
                for (int j = 0; j < clues.Length; j++)
                {
                    Size textSize = TextRenderer.MeasureText(clues[j].ToString(), form.Font);
                    clueWidths[j] = Math.Max(cellSize, textSize.Width + 4);
                    totalWidth += clueWidths[j];
                }

                int xPos = startX - totalWidth;
                for (int j = 0; j < clues.Length; j++)
                {
                    Label lbl = new Label();
                    lbl.Name = "clueLabel";
                    lbl.Text = clues[j].ToString();
                    lbl.TextAlign = ContentAlignment.MiddleCenter;
                    lbl.BorderStyle = BorderStyle.FixedSingle;

                    if (isColor)
                    {
                        Color clueColor = rowClueColors[i][j];
                        lbl.BackColor = clueColor;
                        lbl.ForeColor = ((clueColor.R + clueColor.G + clueColor.B) / 3 < 128) ? Color.White : Color.Black;
                    }
                    else
                    {
                        lbl.BackColor = Color.Gray;
                        lbl.ForeColor = Color.White;
                    }

                    lbl.Size = new Size(clueWidths[j], cellSize);
                    lbl.Location = new Point(xPos, startY + i * cellSize);
                    form.Controls.Add(lbl);

                    xPos += clueWidths[j];
                }
            }

            // grid gombok létrehozása
            for (int i = 0; i < row; i++)
            {
                for (int j = 0; j < col; j++)
                {
                    Button btn = new Button();
                    btn.Name = "gridCell";
                    btn.Size = new Size(cellSize, cellSize);
                    btn.Location = new Point(startX + j * cellSize, startY + i * cellSize);
                    btn.BackColor = Color.White;
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.FlatAppearance.BorderColor = Color.Gray;
                    btn.FlatAppearance.BorderSize = 1;
                    btn.Tag = new Point(i, j);
                    btn.MouseDown += GridCell_MouseDown;
                    btn.MouseEnter += GridCell_MouseEnter;
                    btn.MouseLeave += GridCell_MouseLeave;
                    btn.Paint += (s, e) =>
                    {
                        Button b = s as Button;
                        Point p = (Point)((Button)s).Tag;
                        int i2 = p.X, j2 = p.Y;
                        if(render.showHints)
                        {
                            if (i2 == highlightedRow || j2 == highlightedCol)
                            {
                                using (Brush br = new SolidBrush(Color.FromArgb(60, Color.LightBlue)))
                                {
                                    e.Graphics.FillRectangle(br, b.ClientRectangle);
                                }
                            }
                        }
                        using (Pen thickPen = new Pen(Color.Black, 2))
                        {
                            if (i2 % 5 == 0) e.Graphics.DrawLine(thickPen, 0, 0, btn.Width, 0);
                            if (j2 % 5 == 0) e.Graphics.DrawLine(thickPen, 0, 0, 0, btn.Height);
                            if (j2 == col - 1) e.Graphics.DrawLine(thickPen, btn.Width - 1, 0, btn.Width - 1, btn.Height);
                            if (i2 == row - 1) e.Graphics.DrawLine(thickPen, 0, btn.Height - 1, btn.Width, btn.Height - 1);
                        }
                    };
                    btn.MouseDown += (s, e) =>
                    {
                        btn = s as Button;
                        if (btn == null) return;

                        if (e.Button == MouseButtons.Left || (e.Button == MouseButtons.Right))
                        {
                            isDragging = false;
                            dragButton = e.Button;
                            lastProcessedButton = btn;
                        }
                    };

                    btn.MouseMove += (s, e) =>
                    {
                        if (Control.MouseButtons != dragButton) return;

                        Point mousePos = form.PointToClient(Control.MousePosition);
                        Control ctrl = form.GetChildAtPoint(mousePos);
                        Button targetBtn = ctrl as Button;

                        if (targetBtn == null || targetBtn == lastProcessedButton) return;

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
                    gridButtons[i, j] = btn;
                }
            }

            // Színpaletta (csak színes módban)
            if (isColor)
            {
                HashSet<int> colorsSet = new HashSet<int>();
                for (int i = 0; i < row; i++)
                    for (int j = 0; j < col; j++)
                    {
                        Color c = solutionColorRGB[i, j];
                        if (c != Color.White) colorsSet.Add(c.ToArgb());
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
                    Button firstColorButton = form.colorPalette.Controls[0] as Button;
                    if (firstColorButton != null)
                    {
                        render.SelectColor(firstColorButton);
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
                form.lblWrongCellClicks.Text = $"Helytelen kattintások száma: {wrongCellClicks} (max: {maxWrongCellClicks})";
            }

            if (wrongColor && isColor)
            {
                wrongColorClicks++;
                form.lblWrongColorClicks.Text = $"Helytelen színek száma: {wrongColorClicks} (max: {maxWrongColorClicks})";
            }

            if (wrongCellClicks >= maxWrongCellClicks || (isColor && wrongColorClicks >= maxWrongColorClicks))
            {
                MessageBox.Show("Elérted a maximális hibaszámot! A játék újraindul.");
                gameTimerManager.RestartGameWithCurrentDifficulty();
            }
        }
        public void GenerateNonogramFromImage(Image img, int gridLeft, int gridTop)
        {
            // Analízis bitmap (30x30-as munkaterület a feldolgozáshoz)
            int tempWidth = 30;
            int tempHeight = 30;
            Bitmap tempBmp = new Bitmap(tempWidth, tempHeight);
            using (Graphics g = Graphics.FromImage(tempBmp))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                g.Clear(Color.White);
                g.DrawImage(img, new Rectangle(0, 0, tempWidth, tempHeight));
            }

            // Crop üres (fehér) szélek keresése és levágása
            int minX = tempWidth, maxX = 0, minY = tempHeight, maxY = 0;
            bool hasContent = false;
            for (int y = 0; y < tempHeight; y++)
            {
                for (int x = 0; x < tempWidth; x++)
                {
                    Color p = tempBmp.GetPixel(x, y);
                    // Ha a pixel nem fehér (240 feletti értékeknél már fehérnek vesszük)
                    if (p.R < 240 || p.G < 240 || p.B < 240)
                    {
                        if (x < minX) minX = x; if (x > maxX) maxX = x;
                        if (y < minY) minY = y; if (y > maxY) maxY = y;
                        hasContent = true;
                    }
                }
            }

            // Ha teljesen üres a kép, egy alap 5x5-ös rácsot adunk
            if (!hasContent) { minX = 0; maxX = 4; minY = 0; maxY = 4; }

            int finalWidth = maxX - minX + 1;
            int finalHeight = maxY - minY + 1;

            // Munka bitmap létrehozása a vágott mérettel
            Bitmap bmp = new Bitmap(finalWidth, finalHeight);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.DrawImage(tempBmp, new Rectangle(0, 0, finalWidth, finalHeight),
                            new Rectangle(minX, minY, finalWidth, finalHeight), GraphicsUnit.Pixel);
            }

            // Adatszerkezetek inicializálása
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

            // Pixeladatok feldolgozása konzisztens threshold (küszöb) használata
            for (int i = 0; i < row; i++)
            {
                for (int j = 0; j < col; j++)
                {
                    Color pixel = bmp.GetPixel(j, i);
                    solutionColorRGB[i, j] = pixel;

                    // Luminancia kiszámítása (Y = 0.299R + 0.587G + 0.114B)
                    double luminance = 0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B;

                    // Konzisztens döntés ha sötétebb, mint 220, akkor kifestendő (1-es)
                    bool isFilled = luminance < 220;
                    fillableCells[i, j] = isFilled;
                    solutionBW[i, j] = isFilled ? 1 : 0;

                    // Szín detektálás ha a csatornák között jelentős eltérés van, színes módba váltunk
                    int maxDiff = Math.Max(Math.Abs(pixel.R - pixel.G),
                                  Math.Max(Math.Abs(pixel.G - pixel.B), Math.Abs(pixel.R - pixel.B)));
                    if (maxDiff > 20) isColor = true;

                    // UI alaphelyzet
                    userColorRGB[i, j] = Color.White;
                    userXMark[i, j] = false;
                }
            }

            // Sorok cluejainak kiszámítása
            rowClues = new int[row][];
            rowClueColors = new Color[row][];
            for (int i = 0; i < row; i++)
            {
                List<int> clues = new List<int>();
                List<Color> colors = new List<Color>();
                int j = 0;
                while (j < col)
                {
                    if (!fillableCells[i, j]) { j++; continue; }

                    int count = 0;
                    Color seed = solutionColorRGB[i, j];
                    int k = j;
                    while (k < col && fillableCells[i, k])
                    {
                        Color c = solutionColorRGB[i, k];
                        // Ha színes a mód, a színeltérésnél új blokkot kezdünk
                        if (isColor && count > 0 && !render.AreColorsSimilar(seed, c, colorSimilarityThreshold)) break;
                        count++; k++;
                    }
                    clues.Add(count);
                    colors.Add(isColor ? seed : Color.Black);
                    j = k;
                }
                rowClues[i] = clues.ToArray();
                rowClueColors[i] = colors.ToArray();
            }

            // Oszlopok cluejainak kiszámítása
            colClues = new int[col][];
            colClueColors = new Color[col][];
            for (int j = 0; j < col; j++)
            {
                List<int> clues = new List<int>();
                List<Color> colors = new List<Color>();
                int i = 0;
                while (i < row)
                {
                    if (!fillableCells[i, j]) { i++; continue; }

                    int count = 0;
                    Color seed = solutionColorRGB[i, j];
                    int k = i;
                    while (k < row && fillableCells[k, j])
                    {
                        Color c = solutionColorRGB[k, j];
                        if (isColor && count > 0 && !render.AreColorsSimilar(seed, c, colorSimilarityThreshold)) break;
                        count++; k++;
                    }
                    clues.Add(count);
                    colors.Add(isColor ? seed : Color.Black);
                    i = k;
                }
                colClues[j] = clues.ToArray();
                colClueColors[j] = colors.ToArray();
            }

            // UI generálása és preview megjelenítése
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

            // Erőforrások felszabadítása
            tempBmp.Dispose();
            bmp.Dispose();
        }

        public async void BtnSmartAIGuide_Click(object sender, EventArgs e)
        {
            aiButtonClicked = true;
            form.btnSmartAI.Enabled = false; // Dupla kattintás megelőzése
            MessageBox.Show("AI logikai elemzés indult.", "AI magyarázat", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await Task.Delay(500);

            bool solved = await SolveWithSpeculation(); // Új metódus, ami backtrackinget is használ

            if (solved)
                MessageBox.Show("AI megoldotta a Nonogramot!", "AI kész", MessageBoxButtons.OK, MessageBoxIcon.Information);
            else
                MessageBox.Show("AI nem talál több biztos lépést.", "AI kész", MessageBoxButtons.OK, MessageBoxIcon.Information);

            form.btnSmartAI.Enabled = true;
        }

        // Rekurzív backtracking + logikai lépések
        private async Task<bool> SolveWithSpeculation()
        {
            // 1. Logikai lépések (ezek a biztos pontok)
            bool changed;
            do
            {
                changed = false;
                for (int r = 0; r < row; r++) if (await AnalyzeLine(r, true)) { changed = true; break; }
                if (changed) continue;
                for (int c = 0; c < col; c++) if (await AnalyzeLine(c, false)) { changed = true; break; }
            } while (changed);

            if (IsBoardCompletelyValid()) return true;

            // 2. Spekuláció (Backtracking)
            for (int r = 0; r < row; r++)
            {
                for (int c = 0; c < col; c++)
                {
                    // Csak üres cellát vizsgálunk
                    if (!IsFilled(r, c) && !IsX(r, c))
                    {
                        // --- EZ AZ A RÉSZ, AMI MEGOLDJA A PROBLÉMÁDAT ---
                        // Kigyűjtjük, mik a legális színek az adott sorban és oszlopban
                        var allowedInRow = rowClueColors[r].Select(clr => clr.ToArgb()).ToHashSet();
                        var allowedInCol = colClueColors[c].Select(clr => clr.ToArgb()).ToHashSet();

                        // CSAK az a szín jöhet szóba, ami MINDKÉT halmazban benne van
                        // Ha a sorban csak barna van (allowedInRow), de az oszlopban van lila is, 
                        // a lila NEM kerül be a validColors listába.
                        var validColors = allowedInRow.Intersect(allowedInCol).ToList();

                        // Ha nincs közös szín, akkor oda matematikai képtelenség bármit festeni -> X
                        if (validColors.Count == 0)
                        {
                            if (await SetX(r, c, "A sor és oszlop szabályai kizárják egymást (nincs közös szín)"))
                                return await SolveWithSpeculation();
                            return false;
                        }

                        // Csak a valid színekhez tartozó blokkokat kérjük le
                        var candidates = GetCandidateBlocks(r, c).ToList();

                        foreach (var (isRow, lineIdx, blockIdx) in candidates)
                        {
                            Color blockColor = GetClueColor(lineIdx, isRow, blockIdx);
                            int blockArgb = blockColor.ToArgb();

                            // DUPLA ELLENŐRZÉS: Ha a blokk színe nem szerepel a cella metszet-színei között, kihagyjuk
                            if (!validColors.Contains(blockArgb)) continue;

                            int length = isRow ? col : row;
                            int[] clues = isRow ? rowClues[lineIdx] : colClues[lineIdx];
                            int blockLen = clues[blockIdx];

                            int[] leftmost = GetLeftmost(lineIdx, isRow, length, clues);
                            int[] rightmost = GetRightmost(lineIdx, isRow, length, clues);
                            if (leftmost == null || rightmost == null) continue;

                            for (int start = leftmost[blockIdx]; start <= rightmost[blockIdx]; start++)
                            {
                                int posInLine = isRow ? c : r;
                                if (posInLine < start || posInLine >= start + blockLen) continue;

                                // Megnézzük, lehelyezhető-e a blokk
                                if (!CanPlaceBlock(lineIdx, isRow, start, blockLen, blockColor)) continue;
                                if (!IsPlacementOrderValid(lineIdx, isRow, blockIdx, start, blockLen)) continue;

                                // Keresztirányú szín-legitimitás ellenőrzése
                                bool crossCheck = true;
                                for (int i = 0; i < blockLen; i++)
                                {
                                    int tr = isRow ? lineIdx : start + i;
                                    int tc = isRow ? start + i : lineIdx;
                                    if (!IsColorLegalAtPosition(tr, tc, blockColor, !isRow))
                                    {
                                        crossCheck = false;
                                        break;
                                    }
                                }
                                if (!crossCheck) continue;

                                // --- MENTÉS ÉS PRÓBA ---
                                var backupColors = (Color[,])userColorRGB.Clone();
                                var backupX = (bool[,])userXMark.Clone();

                                for (int i = 0; i < blockLen; i++)
                                {
                                    int currR = isRow ? lineIdx : start + i;
                                    int currC = isRow ? start + i : lineIdx;
                                    await SetCell(currR, currC, blockColor, "Spekulatív elhelyezés");
                                }

                                if (await SolveWithSpeculation()) return true;

                                // --- VISSZALÉPÉS ---
                                userColorRGB = backupColors;
                                userXMark = backupX;
                                render.UpdatePreview();
                            }
                        }

                        // Ha egyik legális szín sem vezetett megoldáshoz, megpróbáljuk az X-et
                        if (await SetX(r, c, "Egyik lehetséges szín sem működött ezen a ponton"))
                        {
                            if (await SolveWithSpeculation()) return true;
                        }

                        return false; // Zsákutca
                    }
                }
            }
            return IsBoardCompletelyValid();
        }

        private bool IsColorLegalAtPosition(int r, int c, Color colorToPlace, bool checkColumn)
        {
            // Adatok lekérése a vizsgált irány szerint
            int lineIdx = checkColumn ? c : r;
            int posInLine = checkColumn ? r : c;
            int lineLength = checkColumn ? row : col;
            int[] clues = checkColumn ? colClues[lineIdx] : rowClues[lineIdx];
            Color[] clueColors = checkColumn ? colClueColors[lineIdx] : rowClueColors[lineIdx];

            // 1. Határok kiszámítása a JELENLEGI tábla alapján (X-ek és színek számítanak!)
            int[] leftmost = GetLeftmost(lineIdx, !checkColumn, lineLength, clues);
            int[] rightmost = GetRightmost(lineIdx, !checkColumn, lineLength, clues);

            if (leftmost == null || rightmost == null) return false;

            // 2. Megnézzük, hogy a szín bármelyik lehetséges blokk tartományába beleesik-e
            for (int i = 0; i < clues.Length; i++)
            {
                if (clueColors[i].ToArgb() == colorToPlace.ToArgb())
                {
                    // A blokk 'i' potenciális helye a vonalon belül
                    if (posInLine >= leftmost[i] && posInLine <= (rightmost[i] + clues[i] - 1))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // Ez a metódus akadályozza meg a színek felcserélését (pl. 5. oszlop hiba)
        private bool IsPlacementOrderValid(int lineIdx, bool isRow, int blockIdx, int start, int len)
        {
            int[] clues = isRow ? rowClues[lineIdx] : colClues[lineIdx];

            // 1. A blokk előtt nem maradhat "üresen" olyan kifestett cella, ami nem tartozik az előző blokkokhoz
            for (int p = 0; p < start; p++)
            {
                int r = isRow ? lineIdx : p;
                int c = isRow ? p : lineIdx;
                if (IsFilled(r, c))
                {
                    Color pixelColor = userColorRGB[r, c];
                    bool canBePreviousBlock = false;
                    for (int prev = 0; prev < blockIdx; prev++)
                    {
                        if (GetClueColor(lineIdx, isRow, prev).ToArgb() == pixelColor.ToArgb())
                        {
                            canBePreviousBlock = true;
                            break;
                        }
                    }
                    if (!canBePreviousBlock) return false; // Hibás sorrend: korábban van egy szín, ami csak később jöhetne
                }
            }
            return true;
        }
        // Visszaadja a blokkokat, amelyekhez a cella tartozhat
        private IEnumerable<(bool isRow, int lineIdx, int blockIdx)> GetCandidateBlocks(int r, int c)
        {
            // 1. Gyűjtsük ki, milyen színek engedélyezettek a sorban és az oszlopban
            var allowedColorsInRow = rowClueColors[r].Select(clr => clr.ToArgb()).ToHashSet();
            var allowedColorsInCol = colClueColors[c].Select(clr => clr.ToArgb()).ToHashSet();

            // --- Sorhoz tartozó blokkok vizsgálata ---
            int[] rowCluesLine = rowClues[r];
            int lengthRow = col;
            int[] leftRow = GetLeftmost(r, true, lengthRow, rowCluesLine);
            int[] rightRow = GetRightmost(r, true, lengthRow, rowCluesLine);

            if (leftRow != null && rightRow != null)
            {
                for (int i = 0; i < rowCluesLine.Length; i++)
                {
                    // ÚJ: Megnézzük a blokk színét
                    Color blockColor = rowClueColors[r][i];

                    // CSAK AKKOR jelölt, ha:
                    // - A koordináta stimmel
                    // - ÉS a blokk színe szerepel az oszlop szabályai között is!
                    if (c >= leftRow[i] && c <= rightRow[i] + rowCluesLine[i] - 1)
                    {
                        if (allowedColorsInCol.Contains(blockColor.ToArgb()))
                        {
                            yield return (true, r, i);
                        }
                    }
                }
            }

            // --- Oszlophoz tartozó blokkok vizsgálata ---
            int[] colCluesLine = colClues[c];
            int lengthCol = row;
            int[] leftCol = GetLeftmost(c, false, lengthCol, colCluesLine);
            int[] rightCol = GetRightmost(c, false, lengthCol, colCluesLine);

            if (leftCol != null && rightCol != null)
            {
                for (int i = 0; i < colCluesLine.Length; i++)
                {
                    // ÚJ: Megnézzük a blokk színét
                    Color blockColor = colClueColors[c][i];

                    // CSAK AKKOR jelölt, ha:
                    // - A koordináta stimmel
                    // - ÉS a blokk színe szerepel a sor szabályai között is!
                    if (r >= leftCol[i] && r <= rightCol[i] + colCluesLine[i] - 1)
                    {
                        if (allowedColorsInRow.Contains(blockColor.ToArgb()))
                        {
                            yield return (false, c, i);
                        }
                    }
                }
            }
        }

        private bool IsBoardCompletelyValid()
        {
            for (int r = 0; r < row; r++)
                if (!IsLineValid(r, true)) return false;

            for (int c = 0; c < col; c++)
                if (!IsLineValid(c, false)) return false;

            return true;
        }

        private bool IsLineValid(int index, bool isRow)
        {
            int length = isRow ? col : row;
            int[] clues = isRow ? rowClues[index] : colClues[index];

            List<(Color color, int len)> foundBlocks = new List<(Color color, int len)>();

            int i = 0;
            while (i < length)
            {
                int r = isRow ? index : i;
                int c = isRow ? i : index;

                if (IsFilled(r, c))
                {
                    Color currentColor = userColorRGB[r, c];
                    int start = i;

                    // Összegyűjtjük az összes azonos színű, egymás mellett lévő cellát
                    while (i < length)
                    {
                        int currR = isRow ? index : i;
                        int currC = isRow ? i : index;
                        if (IsFilled(currR, currC) && userColorRGB[currR, currC].ToArgb() == currentColor.ToArgb())
                            i++;
                        else
                            break;
                    }

                    foundBlocks.Add((currentColor, i - start));
                }
                else
                {
                    i++;
                }
            }

            // 1. Ellenőrzés: A blokkok száma egyezik-e?
            if (foundBlocks.Count != clues.Length)
                return false;

            // 2. Ellenőrzés: Minden blokk hossza és színe pontosan egyezik-e?
            for (int b = 0; b < foundBlocks.Count; b++)
            {
                if (foundBlocks[b].len != clues[b])
                    return false;

                if (foundBlocks[b].color.ToArgb() != GetClueColor(index, isRow, b).ToArgb())
                    return false;
            }

            return true;
        }
        private async Task<bool> AnalyzeLine(int index, bool isRow)
        {
            int length = isRow ? col : row;
            int[] clues = isRow ? rowClues[index] : colClues[index];
            if (clues == null || clues.Length == 0) return false;

            // --- 0. PRIORITÁS: SZÍN-METSZET ALAPÚ SZŰRÉS ---
            for (int p = 0; p < length; p++)
            {
                int r = isRow ? index : p;
                int c = isRow ? p : index;

                if (IsX(r, c) || IsFilled(r, c)) continue;

                // Javítás: Biztonságosabb színlekérés a metszethez
                var rowColorSet = rowClues[r].Select((_, idx) => GetClueColor(r, true, idx).ToArgb()).ToHashSet();
                var colColorSet = colClues[c].Select((_, idx) => GetClueColor(c, false, idx).ToArgb()).ToHashSet();

                rowColorSet.IntersectWith(colColorSet);
                if (rowColorSet.Count == 0)
                {
                    if (await SetX(r, c, "Szín-összeférhetetlenség (sor/oszlop metszet üres)")) return true;
                }
            }

            // --- 1. PRIORITÁS: TISZTÍTÁS ---
            if (await HandleLogicCleanup(index, isRow, length, clues, clues.Sum())) return true;

            // Határok újraszámítása
            int[] leftmost = GetLeftmost(index, isRow, length, clues);
            int[] rightmost = GetRightmost(index, isRow, length, clues);

            if (leftmost == null || rightmost == null) return false;

            // --- 2. SZIGORÚ SZÍN-OVERLAP ---
            for (int p = 0; p < length; p++)
            {
                int r = isRow ? index : p;
                int c = isRow ? p : index;
                if (IsX(r, c) || IsFilled(r, c)) continue;

                HashSet<int> potentialBlocks = new HashSet<int>();
                for (int i = 0; i < clues.Length; i++)
                {
                    if (p >= rightmost[i] && p <= leftmost[i] + clues[i] - 1)
                    {
                        potentialBlocks.Add(i);
                    }
                }

                if (potentialBlocks.Count > 0)
                {
                    Color firstColor = GetClueColor(index, isRow, potentialBlocks.First());
                    bool allSame = potentialBlocks.All(idx => GetClueColor(index, isRow, idx).ToArgb() == firstColor.ToArgb());

                    if (allSame)
                    {
                        if (await SetCell(r, c, firstColor, "Overlap (Átfedés)")) return true;
                    }
                }
            }

            // --- 3. REACHABILITY ---
            for (int p = 0; p < length; p++)
            {
                int r = isRow ? index : p;
                int c = isRow ? p : index;
                if (IsX(r, c) || IsFilled(r, c)) continue;

                bool canAnyBlockReach = false;
                for (int i = 0; i < clues.Length; i++)
                {
                    if (p >= leftmost[i] && p <= rightmost[i] + clues[i] - 1)
                    {
                        canAnyBlockReach = true;
                        break;
                    }
                }

                if (!canAnyBlockReach)
                {
                    if (await SetX(r, c, "Egyik blokk sem érhet ide")) return true;
                }
            }

            // --- 4. SPECIÁLIS LOGIKÁK ---
            if (await ConnectionLogic(index, isRow, length, clues, leftmost, rightmost)) return true;
            if (await ExtendAnchors(index, isRow, length, clues, leftmost, rightmost)) return true;
            if (await AutoCloseBlocks(index, isRow, length, clues)) return true;

            // --- 5. HOLTPONT FELOLDÁS (Shakedown) ---
            for (int p = 0; p < length; p++)
            {
                int r = isRow ? index : p;
                int c = isRow ? p : index;
                if (IsFilled(r, c) || IsX(r, c)) continue;

                // Ideiglenes X-jelölés a teszteléshez
                userXMark[r, c] = true;
                int[] testLeft = GetLeftmost(index, isRow, length, clues);
                userXMark[r, c] = false;

                if (testLeft == null)
                {
                    // Megkeressük az összes blokkot, ami elméletileg lefedheti ezt a pontot
                    var possibleColors = new HashSet<int>();
                    for (int i = 0; i < clues.Length; i++)
                    {
                        if (p >= leftmost[i] && p <= rightmost[i] + clues[i] - 1)
                        {
                            possibleColors.Add(GetClueColor(index, isRow, i).ToArgb());
                        }
                    }

                    if (!isColor) // fekete-fehér
                    {
                        // csak akkor próbáljuk kitölteni, ha üres a cella
                        if (!IsFilled(r, c))
                        {
                            if (await SetCellSafe(r, c, Color.Black)) return true;
                        }
                    }
                    else
                    {
                        if (possibleColors.Count == 1)
                        {
                            Color targetColor = Color.FromArgb(possibleColors.First());
                            if (await SetCell(r, c, targetColor, "Ellentmondás alapú kitöltés")) return true;
                        }
                    }
                }
            }

            return false;
        }

        private async Task<bool> SetCellSafe(int r, int c, Color colorToSet)
        {
            if (IsFilled(r, c)) return false; // Ha már kitöltve, ne térj true-val
            userColorRGB[r, c] = colorToSet;
            render.SetCellColor(r, c, gridButtons[r, c], colorToSet);
            render.UpdatePreview(r, c);
            form.Refresh();
            await Task.Delay(50);
            return true; // true csak akkor, ha ténylegesen kitöltöttünk
        }

        private async Task<bool> ConnectionLogic(int index, bool isRow, int length, int[] clues, int[] leftmost, int[] rightmost)
        {
            for (int bIdx = 0; bIdx < clues.Length; bIdx++)
            {
                int bLen = clues[bIdx];
                Color bColor = GetClueColor(index, isRow, bIdx);
                int rangeStart = leftmost[bIdx];
                int rangeEnd = rightmost[bIdx] + bLen - 1;

                int firstFound = -1, lastFound = -1;

                for (int p = rangeStart; p <= rangeEnd; p++)
                {
                    int r = isRow ? index : p;
                    int c = isRow ? p : index;

                    if (IsFilled(r, c) && userColorRGB[r, c].ToArgb() == bColor.ToArgb())
                    {
                        // Csak akkor horgony, ha más színű blokk nem érhet ide
                        bool belongsToThis = true;
                        for (int j = 0; j < clues.Length; j++)
                        {
                            if (j == bIdx) continue;
                            if (p >= leftmost[j] && p <= rightmost[j] + clues[j] - 1) { belongsToThis = false; break; }
                        }

                        if (belongsToThis)
                        {
                            if (firstFound == -1) firstFound = p;
                            lastFound = p;
                        }
                    }
                }

                if (firstFound != -1 && lastFound != -1 && lastFound - firstFound > 0)
                {
                    for (int k = firstFound + 1; k < lastFound; k++)
                    {
                        int rG = isRow ? index : k; int cG = isRow ? k : index;
                        // CSAK akkor kötjük össze, ha üres! (Nem X és nem más szín)
                        if (!IsFilled(rG, cG) && !IsX(rG, cG))
                        {
                            if (await SetCell(rG, cG, bColor, "Összekötés")) return true;
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

            for (int i = 0; i < clues.Length; i++)
            {
                Color blockColor = GetClueColor(index, isRow, i);
                bool found = false;

                while (currentPos + clues[i] <= length)
                {
                    if (CanPlaceBlock(index, isRow, currentPos, clues[i], blockColor))
                    {
                        int minGap = 0;
                        if (i > 0)
                        {
                            Color prevColor = GetClueColor(index, isRow, i - 1);
                            // Fekete-fehérnél ez mindig 1 lesz, színesnél 0 vagy 1
                            minGap = (prevColor.ToArgb() == blockColor.ToArgb()) ? 1 : 0;
                        }

                        int prevEnd = (i == 0) ? 0 : leftmost[i - 1] + clues[i - 1] + minGap;

                        if (currentPos >= prevEnd)
                        {
                            // Csak az előző blokk vége és a mostani kezdete között nézzük az üres helyet
                            bool skippedRequired = false;
                            int searchStart = (i == 0) ? 0 : leftmost[i - 1] + clues[i - 1];
                            for (int p = searchStart; p < currentPos; p++)
                            {
                                if (IsFilled(isRow ? index : p, isRow ? p : index)) { skippedRequired = true; break; }
                            }

                            if (!skippedRequired)
                            {
                                leftmost[i] = currentPos;
                                currentPos += clues[i];
                                // Ha a következő azonos színű, egyből ugrunk egyet az X-nek
                                if (i < clues.Length - 1 && GetClueColor(index, isRow, i + 1).ToArgb() == blockColor.ToArgb())
                                    currentPos++;

                                found = true;
                                break;
                            }
                        }
                    }
                    currentPos++;
                }
                if (!found) return null;
            }
            return leftmost;
        }

        private int[] GetRightmost(int index, bool isRow, int length, int[] clues)
        {
            int[] rightmost = new int[clues.Length];
            int currentPos = length;

            for (int i = clues.Length - 1; i >= 0; i--)
            {
                Color blockColor = GetClueColor(index, isRow, i);
                bool found = false;

                while (currentPos - clues[i] >= 0)
                {
                    int testStart = currentPos - clues[i];
                    if (CanPlaceBlock(index, isRow, testStart, clues[i], blockColor))
                    {
                        int minGap = 0;
                        if (i < clues.Length - 1)
                        {
                            Color nextColor = GetClueColor(index, isRow, i + 1);
                            minGap = (nextColor.ToArgb() == blockColor.ToArgb()) ? 1 : 0;
                        }

                        int nextLimit = (i == clues.Length - 1) ? length : rightmost[i + 1] - minGap;

                        if (testStart + clues[i] <= nextLimit)
                        {
                            bool skippedRequired = false;
                            int searchEnd = (i == clues.Length - 1) ? length : rightmost[i + 1];
                            for (int p = testStart + clues[i]; p < searchEnd; p++)
                            {
                                if (IsFilled(isRow ? index : p, isRow ? p : index)) { skippedRequired = true; break; }
                            }

                            if (!skippedRequired)
                            {
                                rightmost[i] = testStart;
                                currentPos = testStart;
                                // Ha az előző azonos színű, hagyunk helyet az X-nek
                                if (i > 0 && GetClueColor(index, isRow, i - 1).ToArgb() == blockColor.ToArgb())
                                    currentPos--;

                                found = true;
                                break;
                            }
                        }
                    }
                    currentPos--;
                }
                if (!found) return null;
            }
            return rightmost;
        }
        // ÚJ: Horgony kiterjesztése - ha egy kifestett cella csak egyféle blokkhoz tartozhat
        private async Task<bool> ExtendAnchors(int index, bool isRow, int length, int[] clues, int[] leftmost, int[] rightmost)
        {
            for (int p = 0; p < length; p++)
            {
                int r = isRow ? index : p;
                int c = isRow ? p : index;

                if (IsFilled(r, c))
                {
                    Color pixelColor = userColorRGB[r, c];
                    int blockIdx = -1;
                    int count = 0;

                    for (int i = 0; i < clues.Length; i++)
                    {
                        if (GetClueColor(index, isRow, i).ToArgb() == pixelColor.ToArgb() &&
                            p >= leftmost[i] && p <= rightmost[i] + clues[i] - 1)
                        {
                            blockIdx = i;
                            count++;
                        }
                    }

                    // Ha ez a kifestett pont fixen csak az i. blokkhoz tartozhat
                    if (count == 1)
                    {
                        int i = blockIdx;
                        int overlapStart = Math.Max(rightmost[i], p - clues[i] + 1);
                        int overlapEnd = Math.Min(leftmost[i] + clues[i] - 1, p + clues[i] - 1);

                        for (int fillP = overlapStart; fillP <= overlapEnd; fillP++)
                        {
                            int fr = isRow ? index : fillP;
                            int fc = isRow ? fillP : index;

                            if (!IsFilled(fr, fc) && !IsX(fr, fc))
                            {
                                // JAVÍTÁS: Ellenőrizzük, hogy a keresztirányú szabály ismeri-e ezt a színt!
                                var crossClues = isRow ? colClues[fc] : rowClues[fr];
                                bool validColorInCross = false;
                                for (int k = 0; k < crossClues.Length; k++)
                                {
                                    if (GetClueColor(isRow ? -1 : fr, !isRow, k).ToArgb() == pixelColor.ToArgb())
                                    {
                                        validColorInCross = true; break;
                                    }
                                }

                                if (validColorInCross)
                                {
                                    if (await SetCell(fr, fc, pixelColor, $"Horgony ({i}. blokk) kiterjesztése")) return true;
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
            int limit = isRow ? col : row;
            if (start < 0 || start + len > limit) return false;

            int targetArgb = color.ToArgb();

            // 1. Ütközés: X vagy MÁS szín nem lehet ott
            for (int i = start; i < start + len; i++)
            {
                int r = isRow ? lineIdx : i;
                int c = isRow ? i : lineIdx;
                if (IsX(r, c)) return false;
                if (IsFilled(r, c) && userColorRGB[r, c].ToArgb() != targetArgb) return false;
            }

            // 2. Színszabály: Csak akkor tilos az érintkezés, ha a szomszéd UGYANOLYAN színű
            // Előtte
            if (start > 0)
            {
                int pr = isRow ? lineIdx : start - 1;
                int pc = isRow ? start - 1 : lineIdx;
                if (IsFilled(pr, pc) && userColorRGB[pr, pc].ToArgb() == targetArgb) return false;
            }
            // Utána
            if (start + len < limit)
            {
                int nr = isRow ? lineIdx : start + len;
                int nc = isRow ? start + len : lineIdx;
                if (IsFilled(nr, nc) && userColorRGB[nr, nc].ToArgb() == targetArgb) return false;
            }

            return true;
        }

        // ÚJ: Ezt a metódust add hozzá, hogy ne a megoldásból puskázzon!
        private Color GetClueColor(int lineIdx, bool isRow, int blockIdx)
        {
            // Itt a te saját adatszerkezetedet kell használnod, 
            // ahol a szabályok színeit tárolod (pl. rowCluesColors[lineIdx][blockIdx])
            return isRow ? rowClueColors[lineIdx][blockIdx] : colClueColors[lineIdx][blockIdx];
        }

        private async Task<bool> AutoCloseBlocks(int index, bool isRow, int length, int[] clues)
        {
            for (int i = 0; i < length; i++)
            {
                int r = isRow ? index : i;
                int c = isRow ? i : index;

                if (IsFilled(r, c))
                {
                    int start = i;
                    Color clusterColor = userColorRGB[r, c];
                    while (i + 1 < length && IsFilled(isRow ? index : i + 1, isRow ? i + 1 : index) &&
                           userColorRGB[isRow ? index : i + 1, isRow ? i + 1 : index].ToArgb() == clusterColor.ToArgb())
                    {
                        i++;
                    }
                    int end = i;
                    int currentLen = end - start + 1;

                    int clueIdx = FindClueIdxForBlock(index, isRow, start, end, clusterColor);

                    // Ha találtunk hozzá passzoló szabályt és a hossza pont annyi
                    if (clueIdx != -1 && clues[clueIdx] == currentLen)
                    {
                        // BAL OLDAL lezárása
                        if (start > 0)
                        {
                            int pr = isRow ? index : start - 1;
                            int pc = isRow ? start - 1 : index;
                            if (!IsX(pr, pc) && !IsFilled(pr, pc))
                            {
                                // CSAK akkor zárunk le X-szel, ha:
                                // 1. Van előző blokk ÉS az azonos színű (ekkor kötelező a szünet)
                                bool mustHaveX = false;
                                if (clueIdx > 0 && GetClueColor(index, isRow, clueIdx - 1).ToArgb() == clusterColor.ToArgb())
                                    mustHaveX = true;

                                // 2. Vagy ha a leftmost/rightmost alapján ott már semmi nem lehet (opcionális, de biztonságos)
                                if (mustHaveX)
                                {
                                    if (await SetX(pr, pc, "Kész blokk kényszerített lezárása (azonos szín miatt)")) return true;
                                }
                            }
                        }

                        // JOBB OLDAL lezárása
                        if (end < length - 1)
                        {
                            int nr = isRow ? index : end + 1;
                            int nc = isRow ? end + 1 : index;
                            if (!IsX(nr, nc) && !IsFilled(nr, nc))
                            {
                                bool mustHaveX = false;
                                if (clueIdx < clues.Length - 1 && GetClueColor(index, isRow, clueIdx + 1).ToArgb() == clusterColor.ToArgb())
                                    mustHaveX = true;

                                if (mustHaveX)
                                {
                                    if (await SetX(nr, nc, "Kész blokk kényszerített lezárása (azonos szín miatt)")) return true;
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
            int length = isRow ? col : row;
            int[] clues = isRow ? rowClues[lineIdx] : colClues[lineIdx];

            // Először kérjük le a határokat
            int[] leftmost = GetLeftmost(lineIdx, isRow, length, clues);
            int[] rightmost = GetRightmost(lineIdx, isRow, length, clues);

            if (leftmost == null || rightmost == null) return -1;

            int currentLen = end - start + 1;
            int foundIdx = -1;
            int possibleCount = 0;
            int targetArgb = color.ToArgb();

            for (int i = 0; i < clues.Length; i++)
            {
                // 1. Alapfeltételek: Szín és hossz egyezik?
                if (clues[i] == currentLen && GetClueColor(lineIdx, isRow, i).ToArgb() == targetArgb)
                {
                    // 2. Tartomány ellenőrzés: 
                    // A kifestett blokk eleje nem lehet előbb, mint a leftmost, 
                    // és a vége nem lehet később, mint a rightmost tartomány vége.
                    if (start >= leftmost[i] && end <= (rightmost[i] + clues[i] - 1))
                    {
                        // 3. Sorrendi kényszer (OPCIONÁLIS de erős): 
                        // Ha van előző/következő blokk már kifestve, ellenőrizhetnénk a sorrendet is,
                        // de a leftmost/rightmost ezt alapból jól kezeli.

                        foundIdx = i;
                        possibleCount++;
                    }
                }
            }

            // Csak akkor adjuk vissza, ha 100%-ig biztos (csak egy clue-ra illik rá)
            return (possibleCount == 1) ? foundIdx : -1;
        }

        // Kezeli a túl szűk helyeket és a kész sorok X-elését
        private async Task<bool> HandleLogicCleanup(int index, bool isRow, int length, int[] clues, int totalBlocks)
        {
            int currentFilled = 0;
            for (int i = 0; i < length; i++)
            {
                if (IsFilled(isRow ? index : i, isRow ? i : index)) currentFilled++;
            }

            // Ha a kifestett cellák száma eléri a szabályok összegét
            if (currentFilled == totalBlocks)
            {
                for (int i = 0; i < length; i++)
                {
                    int r = isRow ? index : i;
                    int c = isRow ? i : index;
                    if (!IsFilled(r, c) && !IsX(r, c))
                    {
                        // Ha találtunk üres helyet, amit X-elni kell
                        if (await SetX(r, c, "Sor kész (Darabszám stimmel)")) return true;
                    }
                }
            }
            return false;
        }

        private bool IsFilled(int r, int c) => isColor ? userColorRGB[r, c] != Color.White : gridButtons[r, c].BackColor == Color.Black;
        private bool IsX(int r, int c) => userXMark[r, c];

        private async Task<bool> SetCell(int r, int c, Color colorToSet, string reason)
        {
            // Ha már X van ott, vagy MÁR UGYANEZ a szín, akkor nincs változás!
            if (IsX(r, c)) return false;
            if (IsFilled(r, c) && userColorRGB[r, c].ToArgb() == colorToSet.ToArgb()) return false;

            // Vizuális kiemelés a MessageBox előtt (hogy lássuk, melyik celláról beszél)
            gridButtons[r, c].FlatAppearance.BorderColor = Color.Yellow;
            gridButtons[r, c].FlatAppearance.BorderSize = 3;

            // Üzenet a felhasználónak
            MessageBox.Show($"Lépés: [{r + 1}. sor, {c + 1} oszlop] kifestése.\n\nIndok: {reason}",
                            "AI Logika", MessageBoxButtons.OK, MessageBoxIcon.Information);

            // Keret visszaállítása és tényleges módosítás
            gridButtons[r, c].FlatAppearance.BorderSize = 1;
            gridButtons[r, c].FlatAppearance.BorderColor = Color.Black;

            render.SetCellColor(r, c, gridButtons[r, c], colorToSet);
            userColorRGB[r, c] = colorToSet;

            render.UpdatePreview(r, c);
            form.Refresh();

            await Task.Delay(100);
            return true;
        }

        private async Task<bool> SetX(int r, int c, string reason)
        {
            if (IsFilled(r, c) || IsX(r, c)) return false;

            // Vizuális kiemelés
            gridButtons[r, c].FlatAppearance.BorderColor = Color.Red;
            gridButtons[r, c].FlatAppearance.BorderSize = 3;

            MessageBox.Show($"Lépés: [{r + 1}. sor, {c + 1} oszlop] helyre X kerül.\n\nIndok: {reason}",
                   "AI Logika", MessageBoxButtons.OK, MessageBoxIcon.Information);

            gridButtons[r, c].FlatAppearance.BorderSize = 1;
            gridButtons[r, c].FlatAppearance.BorderColor = Color.Black;

            render.SetCellX(r, c, gridButtons[r, c]);
            userXMark[r, c] = true;

            render.UpdatePreview(r, c);
            form.Refresh();

            await Task.Delay(50);
            return true;
        }
    }
}