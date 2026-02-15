using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Grafilogika_alkalmazas_keszitese
{
    public class NonogramGrid
    {
        public Button[,] gridButtons;
        public int row = 15;
        public int col = 15;
        public int[][] rowClues;
        public int[][] colClues;
        public Color[][] rowClueColors;
        public Color[][] colClueColors;
        public int[,] userColor;
        public int userCellSize = 50;
        public Color[,] solutionColorRGB; // a kép eredeti színe
        public int[,] solutionBW;          // fekete-fehér logikai mátrix (clues és ellenőrzéshez)
        public Color[,] userColorRGB;      // felhasználó által kiválasztott szín
        public bool isColor;
        public Color selectedColor = Color.White; // alapértelmezett szín
        public Color[] nonogramColors;
        public bool[,] userXMark;
        public bool[,] isHintFixed;
        public int colorSimilarityThreshold = 40;
        public bool isDragging = false;
        public bool isDraggingStarted = false;
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
        private NonogramHintEngine hintEngine;

        public NonogramGrid(Nonogram form, GameTimerManager g, NonogramRender r, UndoRedoManager u, ExtraGridManager e, NonogramHintEngine h)
        {
            this.form = form;
            this.render = r;
            this.gameTimerManager = g;
            this.undoredoManager = u;
            this.extraGridManager = e;
            this.hintEngine = h;
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

        public void SetHintEngine(NonogramHintEngine h)
        {
            this.hintEngine = h;
        }

        public void BtnGenerateRandom_Click(object sender, EventArgs e)
        {
            // Username ellenőrzése
            string currentUsername = form.txtUsername.Text.Trim();
            if (string.IsNullOrEmpty(currentUsername))
            {
                MessageBox.Show("Kérlek, add meg a felhasználóneved a játék indítása előtt!", "Hiba", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return; // gomb nem tűnik el, játék nem indul
            }

            // Username fixálása
            form.username = currentUsername;
            form.txtUsername.Enabled = false;  // teljesen letiltjuk, nem lehet kattintani

            gameTimerManager.gameStarted = true;

            isColor = form.cmbMode.SelectedItem?.ToString() == "Színes";
            //selectedColor = isColor ? Color.White : Color.Black;
            // Gombok és vezérlők megjelenítése
            form.btnSolve.Visible = true;
            form.btnHint.Visible = true;
            form.btnCheck.Visible = true;
            form.btnUndo.Visible = true;
            form.btnRedo.Visible = true;
            form.cmbDifficulty.Visible = false;
            form.cmbMode.Visible = false;
            //chkShowX.Visible = true;
            form.chkExtraMode.Visible = false;
            form.chkBlackWhiteMode.Visible = false;
            form.chkColorMode.Visible = false;
            form.lblTimer.Visible = true;
            form.picPreview.Visible = true;
            //picSolutionPreview.Visible = true;
            //lblWrongColorClicks.Visible = false;
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
            //chkXMode.Visible = false;

            // A Generate gomb már elrejthető, mert név van
            form.btnGenerateRandom.Visible = false;

            // Grid előkészítése
            ClearGrid();
            gameTimerManager.ResetCellCliks();
            //gameTimerManager.ResetColorClicks();
            undoredoManager.ClearHistory();
            form.picPreview.Image = null;
            wrongCellClicks = 0;
            form.btnShowExtraSolution.Visible = false;
            form.btnPickColor.Visible = false;
            gameTimerManager.elapsedSeconds = 0;
            gameTimerManager.DifficultyOrModeChanged();
            extraGridManager.ClearAllClueInputs();
            render.ToggleXMarks(form.chkShowX.Checked);
            form.isXMode = true;
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

            // --- BAL KLIKK: SZÍNEZÉS ---
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
                        render.ClearCell(row, col, btn); // Toggle: ha ugyanaz, töröljük
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

                // HIBAELLENŐRZÉS (csak színezésnél számolunk hibát)
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
            // --- JOBB KLIKK: X JELZÉS ---
            else if (e.Button == MouseButtons.Right)
            {
                render.showHints = true;
                render.ClearErrorHighlights();
                if (userXMark[row, col])
                    render.ClearCell(row, col, btn);
                else
                    render.SetCellX(row, col, btn);

                // X lerakásánál általában nem számolunk hibát a Nonogramban
            }

            // Hint-ek frissítése (ha Easy fokozaton vagyunk, rögtön mutatja a következőt)
            if (form.cmbDifficulty.SelectedIndex == 0)
            {
                hintEngine.UpdateHints();
            }

            // UI és Preview frissítése
            HandleErrorCounts(wrongCell, wrongColor);
            render.UpdatePreview(row, col);

            // Csak akkor hívjuk, ha a hiba nem indította újra a játékot
            if (render.IsSolved())
            {
                render.FinalizeGame();
                gameTimerManager.Stop();
            }

            form.Refresh(); // A gombok Paint eseményének kényszerítése a Hint-ek miatt
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
                nonogramColors = render.GetTwoRandomColors(); // Színes módhoz

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

                // Kötelező: minden sor és oszlop kapjon legalább 1 pixelt
                for (int r = 0; r < height && !error; r++)
                    if (!Enumerable.Range(0, width).Any(c => finalBW[r, c] == 1)) error = true;
                for (int c = 0; c < width && !error; c++)
                    if (!Enumerable.Range(0, height).Any(r => finalBW[r, c] == 1)) error = true;
                if (error) continue;

                // 2x2 sakk-minta tiltás
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

                    // Addig tart a blokk, amíg:
                    // A táblán belül vagyunk
                    // A cella ki van töltve (BW == 1)
                    // és (színes mód esetén) a színe megegyezik a blokk kezdő színével
                    while (k < col && solutionBW[i, k] == 1 &&
                          (!isColor || solutionColorRGB[i, k] == currentBlockColor))
                    {
                        count++;
                        k++;
                    }

                    // Blokk mentése
                    clues.Add(count);
                    colors.Add(isColor ? currentBlockColor : Color.Black);

                    // A belső ciklus végén k a következő blokk vagy üres hely elejére mutat
                    j = k;
                }

                // Ha a sor teljesen üres, opcionálisan adhatunk egy 0-ás jelzést (nem kötelező)
                // if (clues.Count == 0) { clues.Add(0); colors.Add(Color.Black); }

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
            // 1. Vizuális gombok eltávolítása a Form-ról
            if (gridButtons != null)
            {
                foreach (Button b in gridButtons)
                {
                    if (b != null)
                    {
                        if (form.Controls.Contains(b))
                            form.Controls.Remove(b);
                        b.Dispose(); // Memória felszabadítása
                    }
                }
            }

            // 2. Clue-k (számok) eltávolítása
            List<Label> clueLabels = form.Controls.OfType<Label>().Where(l => l.Name.Contains("clueLabel")).ToList();
            foreach (Label label in clueLabels)
            {
                form.Controls.Remove(label);
                label.Dispose();
            }

            // 3. LOGIKAI ADATOK ÚJRAINICIALIZÁLÁSA (Ez hiányzott!)
            gridButtons = new Button[row, col];
            userXMark = new bool[row, col];       // Mindenhol False lesz
            userColor = new int[row, col];       // Mindenhol 0 lesz
            userColorRGB = new Color[row, col]; // Mindenhol Empty/Fekete lesz
            isHintFixed = new bool[row, col];

            // 4. userColorRGB feltöltése fehérrel (hogy ne legyen fekete az üres pálya)
            for (int i = 0; i < row; i++)
            {
                for (int j = 0; j < col; j++)
                {
                    userColorRGB[i, j] = Color.White;
                }
            }
        }

        // Itt jöhetne a CreateGridUI a teljes gombokkal, clue label-ekkel, Checkbox állítással stb.
        public void CreateGridUI(int gridLeft, int gridTop)
        {
            AdjustGridSize(); // Cellaméret frissítése
            ClearGrid();      // Előző grid törlése

            gridButtons = new Button[row, col];
            int cellSize = userCellSize;

            int maxRowClues = MaxClueLength(rowClues);
            int maxColClues = MaxClueLength(colClues);

            // Fix grid pozíció a formon belül
            int startX = 550; // pl. 550
            int startY = 300;  // pl. 300

            // oszlop CLUE-K létrehozása (vertikális) 
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

                int yPos = startY - totalHeight; // a blokk teteje
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

                    yPos += clueHeights[i]; // következő label teteje
                }
            }

            // sor CLUE-K létrehozása (horizontális)
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

                int xPos = startX - totalWidth; // a blokk bal széle
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

                    xPos += clueWidths[j]; // következő label bal széle
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
                            if (hintEngine.hintCells.Contains(p))
                            {
                                Color hintColor;

                                if (!isColor)
                                {
                                    // fekete-fehér Nonogram → fekete
                                    hintColor = Color.Black;
                                }
                                else
                                {
                                    // színes Nonogram → a megoldás színe
                                    hintColor = solutionColorRGB[p.X, p.Y];
                                }

                                using (Brush br = new SolidBrush(Color.FromArgb(80, hintColor))) // átlátszó szín
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
                            isDragging = false;    // még nincs drag
                            dragButton = e.Button; // Left vagy Right
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

                        isDragging = true; // most már drag van
                        lastProcessedButton = targetBtn;

                        // Feldolgozás drag során (hibaszámláló, undo nélkül)
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

            // Crop - Üres (fehér) szélek keresése és levágása
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

            // Munka-bitmap létrehozása a vágott mérettel
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

            // Pixeladatok feldolgozása - Konzisztens threshold (küszöb) használata
            for (int i = 0; i < row; i++)
            {
                for (int j = 0; j < col; j++)
                {
                    Color pixel = bmp.GetPixel(j, i);
                    solutionColorRGB[i, j] = pixel;

                    // Luminancia kiszámítása (Y = 0.299R + 0.587G + 0.114B)
                    double luminance = 0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B;

                    // Konzisztens döntés: Ha sötétebb, mint 220, akkor kifestendő (1-es)
                    bool isFilled = luminance < 220;
                    fillableCells[i, j] = isFilled;
                    solutionBW[i, j] = isFilled ? 1 : 0;

                    // Szín detektálás: Ha a csatornák között jelentős eltérés van, színes módba váltunk
                    int maxDiff = Math.Max(Math.Abs(pixel.R - pixel.G),
                                  Math.Max(Math.Abs(pixel.G - pixel.B), Math.Abs(pixel.R - pixel.B)));
                    if (maxDiff > 20) isColor = true;

                    // UI alaphelyzet
                    userColorRGB[i, j] = Color.White;
                    userXMark[i, j] = false;
                }
            }

            // Sorok Clue-jainak kiszámítása
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

            // Oszlopok Clue-jainak kiszámítása
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

            // UI generálása és Preview megjelenítése
            CreateGridUI(gridLeft, gridTop);

            if (form.picPreview != null)
            {
                Bitmap finalPreview = render.GeneratePreviewImage(); // A korábban megírt preview generálód
                form.picPreview.Size = new Size(render.previewSize, render.previewSize);
                form.picPreview.SizeMode = PictureBoxSizeMode.Zoom;
                form.picPreview.Image = finalPreview;
            }
            if (form.picSolutionPreview != null)
            {
                Bitmap finalPreview = render.GeneratePreviewImage(); // A korábban megírt preview generálód
                form.picSolutionPreview.Size = new Size(render.previewSize, render.previewSize);
                form.picSolutionPreview.SizeMode = PictureBoxSizeMode.Zoom;
                form.picSolutionPreview.Image = finalPreview;
            }

            // Erőforrások felszabadítása
            tempBmp.Dispose();
            bmp.Dispose();
        }
    }
}