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

            form.gameStarted = true;

            form.isColor = form.cmbMode.SelectedItem?.ToString() == "Színes";
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
            form.wrongCellClicks = 0;
            form.btnShowExtraSolution.Visible = false;
            form.btnPickColor.Visible = false;
            form.elapsedSeconds = 0;
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
            if (!form.gameTimer.Enabled) form.gameTimer.Start();

            // Mentés visszavonáshoz (Undo/Redo)
            if (!form.isDraggingStarted)
            {
                bool isXAction = (e.Button == MouseButtons.Right);
                undoredoManager.SaveState(isXAction);
                form.isDraggingStarted = true;
            }

            bool wrongCell = false;
            bool wrongColor = false;

            // --- BAL KLIKK: SZÍNEZÉS ---
            if (e.Button == MouseButtons.Left)
            {
                render.ClearErrorHighlights();
                // Ha volt rajta X, azt töröljük színezés előtt
                if (form.userXMark[row, col])
                {
                    render.ClearCell(row, col, btn);
                }

                // Színezés logikája
                if (form.isColor)
                {
                    if (form.userColorRGB[row, col].ToArgb() == form.selectedColor.ToArgb())
                        render.ClearCell(row, col, btn); // Toggle: ha ugyanaz, töröljük
                    else
                        render.SetCellColor(row, col, btn, form.selectedColor);
                }
                else
                {
                    if (form.userColor[row, col] == 1)
                        render.ClearCell(row, col, btn);
                    else
                        render.SetCellBlack(row, col, btn);
                }

                // HIBAELLENŐRZÉS (csak színezésnél számolunk hibát)
                bool isEmpty = form.isColor
                    ? form.userColorRGB[row, col] == Color.White
                    : form.userColor[row, col] == 0;

                if (!isEmpty)
                {
                    if (form.isColor)
                    {
                        if (form.solutionColorRGB[row, col] == Color.White)
                            wrongCell = true;
                        else if (form.userColorRGB[row, col].ToArgb() != form.solutionColorRGB[row, col].ToArgb())
                            wrongColor = true;
                    }
                    else
                    {
                        if (form.solutionBW[row, col] == 0)
                            wrongCell = true;
                    }
                }
            }
            // --- JOBB KLIKK: X JELZÉS ---
            else if (e.Button == MouseButtons.Right)
            {
                render.ClearErrorHighlights();
                if (form.userXMark[row, col])
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
            form.highlightedRow = p.X;
            form.highlightedCol = p.Y;

            render.RefreshGrid();
        }

        public void GridCell_MouseLeave(object sender, EventArgs e)
        {
            form.highlightedRow = -1;
            form.highlightedCol = -1;

            render.RefreshGrid();
        }

        public void GenerateNonogram(int gridLeft, int gridTop, int width, int height, int targetPixels, int maxAttempts)
        {
            Random rnd = form.rnd;

            if (form.isColor)
                form.nonogramColors = render.GetTwoRandomColors(); // Színes módhoz

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
                    int len = form.isColor ? rnd.Next(1, Math.Min(7, width)) : rnd.Next(2, Math.Min(6, width));

                    if (form.isColor && len == 1 && rnd.NextDouble() < 0.6)
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
                form.row = height;
                form.col = width;
                form.solutionBW = finalBW;
                form.solutionColorRGB = new Color[height, width];

                render.ApplyColorsToBlocks();
                GenerateClues();

                solver = new NonogramSolver(form);
                if (!solver.IsUniqueSolution()) continue;

                isBoardGood = true;
            }

            // Játékos állapot inicializálása
            form.userXMark = new bool[form.row, form.col];
            form.userColorRGB = new Color[form.row, form.col];
            form.userColor = new int[form.row, form.col];

            for (int i = 0; i < form.row; i++)
                for (int j = 0; j < form.col; j++)
                {
                    form.userXMark[i, j] = false;
                    form.userColorRGB[i, j] = Color.White;
                    form.userColor[i, j] = 0;
                }

            CreateGridUI(gridLeft, gridTop);
        }

        private void GenerateClues()
        {
            form.rowClues = new int[form.row][];
            form.rowClueColors = new Color[form.row][];
            form.colClues = new int[form.col][];
            form.colClueColors = new Color[form.col][];

            // sorok generálása
            for (int i = 0; i < form.row; i++)
            {
                List<int> clues = new List<int>();
                List<Color> colors = new List<Color>();
                int j = 0;

                while (j < form.col)
                {
                    // Ha üres a cella, lépjünk tovább
                    if (form.solutionBW[i, j] == 0)
                    {
                        j++;
                        continue;
                    }

                    // Új blokk kezdődik
                    int count = 0;
                    Color currentBlockColor = form.solutionColorRGB[i, j];
                    int k = j;

                    // Addig tart a blokk, amíg:
                    // A táblán belül vagyunk
                    // A cella ki van töltve (BW == 1)
                    // és (színes mód esetén) a színe megegyezik a blokk kezdő színével
                    while (k < form.col && form.solutionBW[i, k] == 1 &&
                          (!form.isColor || form.solutionColorRGB[i, k] == currentBlockColor))
                    {
                        count++;
                        k++;
                    }

                    // Blokk mentése
                    clues.Add(count);
                    colors.Add(form.isColor ? currentBlockColor : Color.Black);

                    // A belső ciklus végén k a következő blokk vagy üres hely elejére mutat
                    j = k;
                }

                // Ha a sor teljesen üres, opcionálisan adhatunk egy 0-ás jelzést (nem kötelező)
                // if (clues.Count == 0) { clues.Add(0); colors.Add(Color.Black); }

                form.rowClues[i] = clues.ToArray();
                form.rowClueColors[i] = colors.ToArray();
            }

            // oszlopok generálása
            for (int j = 0; j < form.col; j++)
            {
                List<int> clues = new List<int>();
                List<Color> colors = new List<Color>();
                int i = 0;

                while (i < form.row)
                {
                    if (form.solutionBW[i, j] == 0)
                    {
                        i++;
                        continue;
                    }

                    int count = 0;
                    Color currentBlockColor = form.solutionColorRGB[i, j];
                    int k = i;

                    while (k < form.row && form.solutionBW[k, j] == 1 &&
                          (!form.isColor || form.solutionColorRGB[k, j] == currentBlockColor))
                    {
                        count++;
                        k++;
                    }

                    clues.Add(count);
                    colors.Add(form.isColor ? currentBlockColor : Color.Black);

                    i = k;
                }

                form.colClues[j] = clues.ToArray();
                form.colClueColors[j] = colors.ToArray();
            }
        }

        public void AdjustGridSize()
        {
            int maxGridWidth = form.ClientSize.Width - 40;
            int maxGridHeight = form.ClientSize.Height - form.fixedGridTop - 200;

            int maxRowClues = MaxClueLength(form.rowClues);
            int maxColClues = MaxClueLength(form.colClues);

            int cellWidth = (maxGridWidth - maxRowClues * form.userCellSize) / form.col;
            int cellHeight = (maxGridHeight - maxColClues * form.userCellSize) / form.row;

            form.userCellSize = Math.Min(cellWidth, cellHeight);
            form.userCellSize = Math.Min(form.userCellSize, 50);
            form.userCellSize = Math.Max(form.userCellSize, 15);
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
            form.fixedGridTop = Math.Max(20, form.chkShowX.Bottom) + margin;
        }

        public void ClearGrid()
        {
            // 1. Vizuális gombok eltávolítása a Form-ról
            if (form.gridButtons != null)
            {
                foreach (Button b in form.gridButtons)
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
            form.gridButtons = new Button[form.row, form.col];
            form.userXMark = new bool[form.row, form.col];       // Mindenhol False lesz
            form.userColor = new int[form.row, form.col];       // Mindenhol 0 lesz
            form.userColorRGB = new Color[form.row, form.col]; // Mindenhol Empty/Fekete lesz
            form.isHintFixed = new bool[form.row, form.col];

            // 4. userColorRGB feltöltése fehérrel (hogy ne legyen fekete az üres pálya)
            for (int i = 0; i < form.row; i++)
            {
                for (int j = 0; j < form.col; j++)
                {
                    form.userColorRGB[i, j] = Color.White;
                }
            }
        }

        // Itt jöhetne a CreateGridUI a teljes gombokkal, clue label-ekkel, Checkbox állítással stb.
        public void CreateGridUI(int gridLeft, int gridTop)
        {
            AdjustGridSize(); // Cellaméret frissítése
            ClearGrid();      // Előző grid törlése

            form.gridButtons = new Button[form.row, form.col];
            int cellSize = form.userCellSize;

            int maxRowClues = MaxClueLength(form.rowClues);
            int maxColClues = MaxClueLength(form.colClues);

            // Fix grid pozíció a formon belül
            int startX = 550; // pl. 550
            int startY = 300;  // pl. 300

            // oszlop CLUE-K létrehozása (vertikális) 
            for (int j = 0; j < form.col; j++)
            {
                int[] clues = form.colClues[j];

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

                    if (form.isColor)
                    {
                        Color clueColor = form.colClueColors[j][i];
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
            for (int i = 0; i < form.row; i++)
            {
                int[] clues = form.rowClues[i];

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

                    if (form.isColor)
                    {
                        Color clueColor = form.rowClueColors[i][j];
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
            for (int i = 0; i < form.row; i++)
            {
                for (int j = 0; j < form.col; j++)
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
                        if(form.showHints)
                        {
                            if (i2 == form.highlightedRow || j2 == form.highlightedCol)
                            {
                                using (Brush br = new SolidBrush(Color.FromArgb(60, Color.LightBlue)))
                                {
                                    e.Graphics.FillRectangle(br, b.ClientRectangle);
                                }
                            }
                            if (form.hintEngine.HintCells.Contains(p))
                            {
                                Color hintColor;

                                if (!form.isColor)
                                {
                                    // fekete-fehér Nonogram → fekete
                                    hintColor = Color.Black;
                                }
                                else
                                {
                                    // színes Nonogram → a megoldás színe
                                    hintColor = form.solutionColorRGB[p.X, p.Y];
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
                            if (j2 == form.col - 1) e.Graphics.DrawLine(thickPen, btn.Width - 1, 0, btn.Width - 1, btn.Height);
                            if (i2 == form.row - 1) e.Graphics.DrawLine(thickPen, 0, btn.Height - 1, btn.Width, btn.Height - 1);
                        }
                    };
                    btn.MouseDown += (s, e) =>
                    {
                        btn = s as Button;
                        if (btn == null) return;

                        if (e.Button == MouseButtons.Left || (e.Button == MouseButtons.Right))
                        {
                            form.isDragging = false;    // még nincs drag
                            form.dragButton = e.Button; // Left vagy Right
                            form.lastProcessedButton = btn;
                        }
                    };

                    btn.MouseMove += (s, e) =>
                    {
                        if (Control.MouseButtons != form.dragButton) return;

                        Point mousePos = form.PointToClient(Control.MousePosition);
                        Control ctrl = form.GetChildAtPoint(mousePos);
                        Button targetBtn = ctrl as Button;

                        if (targetBtn == null || targetBtn == form.lastProcessedButton) return;

                        form.isDragging = true; // most már drag van
                        form.lastProcessedButton = targetBtn;

                        // Feldolgozás drag során (hibaszámláló, undo nélkül)
                        GridCell_MouseDown(targetBtn, new MouseEventArgs(form.dragButton, 0, 0, 0, 0));
                    };

                    btn.MouseUp += (s, e) =>
                    {
                        form.isDragging = false;
                        form.lastProcessedButton = null;
                        form.isDraggingStarted = false;
                    };
                    form.Controls.Add(btn);
                    form.gridButtons[i, j] = btn;
                }
            }

            // Színpaletta (csak színes módban)
            if (form.isColor)
            {
                HashSet<int> colorsSet = new HashSet<int>();
                for (int i = 0; i < form.row; i++)
                    for (int j = 0; j < form.col; j++)
                    {
                        Color c = form.solutionColorRGB[i, j];
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
                if (form.isColor && form.colorPalette.Controls.Count > 0)
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
                form.wrongCellClicks++;
                form.lblWrongCellClicks.Text = $"Helytelen kattintások száma: {form.wrongCellClicks} (max: {form.maxWrongCellClicks})";
            }

            if (wrongColor && form.isColor)
            {
                form.wrongColorClicks++;
                form.lblWrongColorClicks.Text = $"Helytelen színek száma: {form.wrongColorClicks} (max: {form.maxWrongColorClicks})";
            }

            if (form.wrongCellClicks >= form.maxWrongCellClicks || (form.isColor && form.wrongColorClicks >= form.maxWrongColorClicks))
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
            form.row = finalHeight;
            form.col = finalWidth;
            form.isColor = false;

            form.solutionColorRGB = new Color[form.row, form.col];
            form.solutionBW = new int[form.row, form.col];
            form.userColorRGB = new Color[form.row, form.col];
            form.userColor = new int[form.row, form.col];
            form.userXMark = new bool[form.row, form.col];
            form.gridButtons = new Button[form.row, form.col];
            bool[,] fillableCells = new bool[form.row, form.col];

            // Pixeladatok feldolgozása - Konzisztens threshold (küszöb) használata
            for (int i = 0; i < form.row; i++)
            {
                for (int j = 0; j < form.col; j++)
                {
                    Color pixel = bmp.GetPixel(j, i);
                    form.solutionColorRGB[i, j] = pixel;

                    // Luminancia kiszámítása (Y = 0.299R + 0.587G + 0.114B)
                    double luminance = 0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B;

                    // Konzisztens döntés: Ha sötétebb, mint 220, akkor kifestendő (1-es)
                    bool isFilled = luminance < 220;
                    fillableCells[i, j] = isFilled;
                    form.solutionBW[i, j] = isFilled ? 1 : 0;

                    // Szín detektálás: Ha a csatornák között jelentős eltérés van, színes módba váltunk
                    int maxDiff = Math.Max(Math.Abs(pixel.R - pixel.G),
                                  Math.Max(Math.Abs(pixel.G - pixel.B), Math.Abs(pixel.R - pixel.B)));
                    if (maxDiff > 20) form.isColor = true;

                    // UI alaphelyzet
                    form.userColorRGB[i, j] = Color.White;
                    form.userXMark[i, j] = false;
                }
            }

            // Sorok Clue-jainak kiszámítása
            form.rowClues = new int[form.row][];
            form.rowClueColors = new Color[form.row][];
            for (int i = 0; i < form.row; i++)
            {
                List<int> clues = new List<int>();
                List<Color> colors = new List<Color>();
                int j = 0;
                while (j < form.col)
                {
                    if (!fillableCells[i, j]) { j++; continue; }

                    int count = 0;
                    Color seed = form.solutionColorRGB[i, j];
                    int k = j;
                    while (k < form.col && fillableCells[i, k])
                    {
                        Color c = form.solutionColorRGB[i, k];
                        // Ha színes a mód, a színeltérésnél új blokkot kezdünk
                        if (form.isColor && count > 0 && !render.AreColorsSimilar(seed, c, form.colorSimilarityThreshold)) break;
                        count++; k++;
                    }
                    clues.Add(count);
                    colors.Add(form.isColor ? seed : Color.Black);
                    j = k;
                }
                form.rowClues[i] = clues.ToArray();
                form.rowClueColors[i] = colors.ToArray();
            }

            // Oszlopok Clue-jainak kiszámítása
            form.colClues = new int[form.col][];
            form.colClueColors = new Color[form.col][];
            for (int j = 0; j < form.col; j++)
            {
                List<int> clues = new List<int>();
                List<Color> colors = new List<Color>();
                int i = 0;
                while (i < form.row)
                {
                    if (!fillableCells[i, j]) { i++; continue; }

                    int count = 0;
                    Color seed = form.solutionColorRGB[i, j];
                    int k = i;
                    while (k < form.row && fillableCells[k, j])
                    {
                        Color c = form.solutionColorRGB[k, j];
                        if (form.isColor && count > 0 && !render.AreColorsSimilar(seed, c, form.colorSimilarityThreshold)) break;
                        count++; k++;
                    }
                    clues.Add(count);
                    colors.Add(form.isColor ? seed : Color.Black);
                    i = k;
                }
                form.colClues[j] = clues.ToArray();
                form.colClueColors[j] = colors.ToArray();
            }

            // UI generálása és Preview megjelenítése
            CreateGridUI(gridLeft, gridTop);

            if (form.picPreview != null)
            {
                Bitmap finalPreview = render.GeneratePreviewImage(); // A korábban megírt preview generálód
                form.picPreview.Size = new Size(form.previewSize, form.previewSize);
                form.picPreview.SizeMode = PictureBoxSizeMode.Zoom;
                form.picPreview.Image = finalPreview;
            }
            if (form.picSolutionPreview != null)
            {
                Bitmap finalPreview = render.GeneratePreviewImage(); // A korábban megírt preview generálód
                form.picSolutionPreview.Size = new Size(form.previewSize, form.previewSize);
                form.picSolutionPreview.SizeMode = PictureBoxSizeMode.Zoom;
                form.picSolutionPreview.Image = finalPreview;
            }

            // Erőforrások felszabadítása
            tempBmp.Dispose();
            bmp.Dispose();
        }
    }
}