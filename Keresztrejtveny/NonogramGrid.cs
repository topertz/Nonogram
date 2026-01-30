using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Nonogram
{
    public class NonogramGrid
    {
        private Nonogram form;
        private GameTimerManager gameTimerManager;

        public NonogramGrid(Nonogram form, GameTimerManager g)
        {
            this.form = form;
            this.gameTimerManager = g;
        }

        public void SetGameTimerManager(GameTimerManager gtm)
        {
            gameTimerManager = gtm;
        }

        /*private void GenerateRandomSolutionBWFixedCount(int filledCount)
        {
            Random rnd = form.rnd;

            for (int i = 0; i < form.row; i++)
                for (int j = 0; j < form.col; j++)
                {
                    form.solutionBW[i, j] = 0;
                    form.solutionColorRGB[i, j] = Color.White;
                }

            List<Point> cells = new List<Point>();
            for (int i = 0; i < form.row; i++)
                for (int j = 0; j < form.col; j++)
                    cells.Add(new Point(i, j));

            foreach (Point p in cells.OrderBy(_ => rnd.Next()).Take(filledCount))
            {
                form.solutionBW[p.X, p.Y] = 1;
                form.solutionColorRGB[p.X, p.Y] = Color.Black;
            }
        }*/


        private HashSet<Color> ApplyColorsToBlocks()
        {
            Random rnd = form.rnd;
            HashSet<Color> usedColors = new HashSet<Color>();

            if (!form.isColor)
            {
                for (int i = 0; i < form.row; i++)
                    for (int j = 0; j < form.col; j++)
                        form.solutionColorRGB[i, j] = form.solutionBW[i, j] == 1 ? Color.Black : Color.White;
                usedColors.Add(Color.Black);
                return usedColors;
            }

            // Színes mód
            for (int i = 0; i < form.row; i++)
            {
                int j = 0;
                while (j < form.col)
                {
                    if (form.solutionBW[i, j] == 0) { j++; continue; }

                    Color blockColor = form.easyColors[rnd.Next(form.easyColors.Length)];
                    usedColors.Add(blockColor); // Regisztráljuk, hogy ezt a színt használtuk

                    int k = j;
                    while (k < form.col && form.solutionBW[i, k] == 1)
                    {
                        form.solutionColorRGB[i, k] = blockColor;
                        k++;
                    }
                    j = k;
                }
            }

            // Fehér mezők beállítása
            for (int i = 0; i < form.row; i++)
                for (int j = 0; j < form.col; j++)
                    if (form.solutionBW[i, j] == 0) form.solutionColorRGB[i, j] = Color.White;

            return usedColors;
        }

        // Teljes Easy Nonogram generálás
        public void GenerateEasyNonogram(int gridLeft, int gridTop)
        {
            int w = 10, h = 10, targetPixels = 35;
            Random rnd = form.rnd;
            bool isBoardGood = false;

            while (!isBoardGood)
            {
                int[,] finalBW = new int[h, w];
                int currentPixels = 0, attempts = 0;

                // Pixelek lehelyezése (Fix 35 pixel)
                while (currentPixels < targetPixels && attempts < 1000)
                {
                    attempts++;
                    int r = rnd.Next(h), c = rnd.Next(w), len = rnd.Next(2, 4);
                    if (c + len <= w)
                    {
                        bool spaceFree = true;
                        for (int k = 0; k < len; k++) if (finalBW[r, c + k] == 1) spaceFree = false;
                        if (spaceFree)
                        {
                            for (int k = 0; k < len && currentPixels < targetPixels; k++)
                            {
                                if (finalBW[r, c + k] == 0) { finalBW[r, c + k] = 1; currentPixels++; }
                            }
                        }
                    }
                }
                if (currentPixels < targetPixels) continue;

                // Sűrítés
                var validRows = Enumerable.Range(0, h).Where(r => Enumerable.Range(0, w).Any(c => finalBW[r, c] == 1)).ToList();
                var validCols = Enumerable.Range(0, w).Where(c => Enumerable.Range(0, h).Any(r => finalBW[r, c] == 1)).ToList();
                if (validRows.Count < 5 || validCols.Count < 5) continue;

                int tempH = validRows.Count, tempW = validCols.Count;
                int[,] compressed = new int[tempH, tempW];
                for (int i = 0; i < tempH; i++)
                    for (int j = 0; j < tempW; j++)
                        compressed[i, j] = finalBW[validRows[i], validCols[j]];

                // Szűrők (Egyértelműség)
                bool error = false;
                // Sakk-minta csekk
                for (int i = 0; i < tempH - 1; i++)
                    for (int j = 0; j < tempW - 1; j++)
                        if (compressed[i, j] == compressed[i + 1, j + 1] && compressed[i, j + 1] == compressed[i + 1, j] && compressed[i, j] != compressed[i, j + 1])
                        { error = true; break; }
                if (error) continue;

                // Identikus sor/oszlop csekk
                for (int i = 0; i < tempH - 1; i++)
                    for (int k = i + 1; k < tempH; k++)
                        if (Enumerable.Range(0, tempW).All(j => compressed[i, j] == compressed[k, j])) error = true;
                if (error) continue;

                // ideiglenes beállítás a szín ellenőrzéséhez
                form.row = tempH;
                form.col = tempW;
                form.solutionBW = compressed;
                form.solutionColorRGB = new Color[tempH, tempW];

                // szín ellenőrzés
                HashSet<Color> used = ApplyColorsToBlocks();
                if (form.isColor && used.Count < form.easyColors.Length)
                {
                    continue; // Ha nem használtunk minden színt, új pálya kell
                }

                isBoardGood = true;
            }

            // Inicializálás és UI véglegesítése
            form.userXMark = new bool[form.row, form.col];
            form.userColorRGB = new Color[form.row, form.col];
            form.userColor = new int[form.row, form.col];

            GenerateClues();

            for (int i = 0; i < form.row; i++)
                for (int j = 0; j < form.col; j++)
                {
                    if (form.solutionBW[i, j] == 0) // üres cella
                        form.userXMark[i, j] = true; // X-jelölt, ne lehessen kattintani
                    else
                        form.userXMark[i, j] = false; // kitöltendő

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
            int maxGridHeight = form.ClientSize.Height - form.fixedGridTop - 150;

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
            if (form.gridButtons != null)
            {
                foreach (Button b in form.gridButtons)
                    if (b != null && form.Controls.Contains(b))
                        form.Controls.Remove(b);
            }

            foreach (Control c in form.Controls.Find("clueLabel", true))
                form.Controls.Remove(c);

            form.gridButtons = new Button[form.row, form.col];
        }

        public Bitmap GeneratePreviewImage()
        {
            int rows = form.row;
            int cols = form.col;
            int width = form.picSolutionPreview.Width;
            int height = form.picSolutionPreview.Height;

            Bitmap bmp = new Bitmap(width, height);

            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);

                float cellWidth = (float)width / cols;
                float cellHeight = (float)height / rows;

                for (int i = 0; i < rows; i++)
                {
                    for (int j = 0; j < cols; j++)
                    {
                        Color c = form.isColor ? form.solutionColorRGB[i, j] :
                                   (form.solutionBW[i, j] == 1 ? Color.Black : Color.White);

                        using (SolidBrush brush = new SolidBrush(c))
                        {
                            g.FillRectangle(brush,
                                j * cellWidth,
                                i * cellHeight,
                                cellWidth,
                                cellHeight);
                        }
                    }
                }
            }

            return bmp;
        }

        // Itt jöhetne a CreateGridUI a teljes gombokkal, clue label-ekkel, Checkbox állítással stb.
        public void CreateGridUI(int gridLeft, int gridTop)
        {
            AdjustGridSize();

            // Előző grid törlése
            ClearGrid();

            form.gridButtons = new Button[form.row, form.col];
            int cellSize = form.userCellSize;
            int maxRowClues = MaxClueLength(form.rowClues);
            int maxColClues = MaxClueLength(form.colClues);
            // fix grid pozíció
            int startX = 550;  // ide rajzolódik a rács bal felső sarka
            int startY = 300;

            // Clue-ok és gombok létrehozása
            for (int j = 0; j < form.col; j++)
            {
                int[] clues = form.colClues[j];
                for (int i = 0; i < clues.Length; i++)
                {
                    Label lbl = new Label();
                    lbl.Name = "clueLabel";
                    lbl.Size = new Size(cellSize, cellSize);
                    lbl.Location = new Point(startX + j * cellSize, startY - (clues.Length - i) * cellSize);
                    lbl.Text = clues[i].ToString();
                    lbl.TextAlign = ContentAlignment.MiddleCenter;
                    lbl.BorderStyle = BorderStyle.FixedSingle;
                    if (form.isColor)
                    {
                        Color clueColor = form.colClueColors[j][i];
                        lbl.BackColor = clueColor;
                        lbl.ForeColor = ((clueColor.R + clueColor.G + clueColor.B) / 3 < 128) ? Color.White : Color.Black;
                    }
                    else { lbl.BackColor = Color.Black; lbl.ForeColor = Color.White; }
                    form.Controls.Add(lbl);
                }
            }

            for (int i = 0; i < form.row; i++)
            {
                int[] clues = form.rowClues[i];
                for (int j = 0; j < clues.Length; j++)
                {
                    Label lbl = new Label();
                    lbl.Name = "clueLabel";
                    lbl.Size = new Size(cellSize, cellSize);
                    lbl.Location = new Point(startX - (clues.Length - j) * cellSize, startY + i * cellSize);
                    lbl.Text = clues[j].ToString();
                    lbl.TextAlign = ContentAlignment.MiddleCenter;
                    lbl.BorderStyle = BorderStyle.FixedSingle;
                    if (form.isColor)
                    {
                        Color clueColor = form.rowClueColors[i][j];
                        lbl.BackColor = clueColor;
                        lbl.ForeColor = ((clueColor.R + clueColor.G + clueColor.B) / 3 < 128) ? Color.White : Color.Black;
                    }
                    else { lbl.BackColor = Color.Black; lbl.ForeColor = Color.White; }
                    form.Controls.Add(lbl);
                }
            }

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
                    btn.Click += form.GridCell_Click;
                    btn.Paint += (s, e) =>
                    {
                        Point p = (Point)((Button)s).Tag;
                        int i2 = p.X, j2 = p.Y;
                        using (Pen thickPen = new Pen(Color.Black, 2))
                        {
                            if (i2 % 5 == 0) e.Graphics.DrawLine(thickPen, 0, 0, btn.Width, 0);
                            if (j2 % 5 == 0) e.Graphics.DrawLine(thickPen, 0, 0, 0, btn.Height);
                            if (j2 == form.col - 1) e.Graphics.DrawLine(thickPen, btn.Width - 1, 0, btn.Width - 1, btn.Height);
                            if (i2 == form.row - 1) e.Graphics.DrawLine(thickPen, 0, btn.Height - 1, btn.Width, btn.Height - 1);
                        }
                    };
                    form.Controls.Add(btn);
                    form.gridButtons[i, j] = btn;
                }
            }

            if (form.isColor)
            {
                // Színpaletta generálása színes Nonogramhoz
                HashSet<int> colorsSet = new HashSet<int>();
                for (int i = 0; i < form.row; i++)
                {
                    for (int j = 0; j < form.col; j++)
                    {
                        Color c = form.solutionColorRGB[i, j];
                        if (c != Color.White) // csak a kitöltött színek
                            colorsSet.Add(c.ToArgb());
                    }
                }

                form.colorPalette.Controls.Clear();
                form.colorPalette.Visible = true;
                form.colorPalette.AutoScroll = false; // nincs görgetés
                form.colorPalette.WrapContents = true; // több sorban törik
                form.colorPalette.FlowDirection = FlowDirection.LeftToRight;

                int btnSize = 30; // gombméret
                int margin = 2;   // gomb közti távolság
                int maxCols = 10; // max gomb egy sorban

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
                        form.selectedColor = color;
                        foreach (Button b in form.colorPalette.Controls)
                            b.FlatAppearance.BorderColor = Color.Gray;
                        colorBtn.FlatAppearance.BorderColor = Color.Red;
                    };

                    form.colorPalette.Controls.Add(colorBtn);
                }
            }
            else
            {
                form.colorPalette.Visible = false; // fekete-fehérnél elrejtjük
            }
        }

        public bool IsCellCorrect(int row, int col)
        {
            if (form.isColor)
                return form.userColorRGB[row, col].ToArgb() == form.solutionColorRGB[row, col].ToArgb();
            return form.userColor[row, col] == form.solutionBW[row, col];
        }

        public bool IsSolved()
        {
            for (int i = 0; i < form.row; i++)
                for (int j = 0; j < form.col; j++)
                    if (!form.userXMark[i, j] && !IsCellCorrect(i, j))
                        return false;
            return true;
        }

        public void HandleGridClick(Button btn, Color selectedColor)
        {
            Point p = (Point)btn.Tag;
            int row = p.X;
            int col = p.Y;

            // Ha az adott cella X-jelölt, ne történjen semmi
            if (form.userXMark[row, col])
                return;

            if (!form.gameTimer.Enabled)
                form.gameTimer.Start();

            // Cellamódosítás
            if (form.isColor)
            {
                btn.BackColor = selectedColor;
                form.userColorRGB[row, col] = selectedColor;
                form.userColor[row, col] = 0;
            }
            else
            {
                if (btn.BackColor == Color.White)
                {
                    btn.BackColor = Color.Black;
                    form.userColorRGB[row, col] = Color.Black;
                    form.userColor[row, col] = 1;
                }
                /*else
                {
                    btn.BackColor = Color.White;
                    form.userColorRGB[row, col] = Color.White;
                    form.userColor[row, col] = 0;
                }*/
            }

            if (IsSolved())
            {
                MessageBox.Show("Gratulálok, kész a Nonogram!");
                form.gameTimer.Stop();
                try
                {
                    // Projekt / exe könyvtár
                    string exeFolder = AppDomain.CurrentDomain.BaseDirectory; // pl: bin\Debug\net6.0
                    string projectFolder = Path.GetFullPath(Path.Combine(exeFolder, @"..\..\..")); // vissza a projekt gyökérhez
                    Directory.CreateDirectory(projectFolder);
                    // Fájlnév generálása (dátummal)
                    string mode = gameTimerManager.GetModeName();
                    string difficulty = gameTimerManager.GetDifficultyName();
                    string fileName = $"nonogram_{mode}_{difficulty}.json";
                    string fullPath = Path.Combine(projectFolder, fileName);

                    // Felhasználónév
                    TextBox txtUsername = form.Controls.Find("txtUsername", true)
                        .FirstOrDefault() as TextBox;
                    form.username = txtUsername?.Text.Trim();

                    // Mentés
                    form.saveLoadManager.SaveGame(fullPath, form.username);

                    MessageBox.Show(
                        "A játék automatikusan elmentve a projekt mappájába!",
                        "Mentés",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "Hiba a játék mentése során:\n" + ex.Message,
                        "Mentés hiba",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                }
                ClearGrid();
                gameTimerManager.ResetCellCliks();
                gameTimerManager.ResetColorClicks();
                gameTimerManager.ResetHintClicks();
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
                form.lblWrongCellClicks.Visible = true;
                form.lblWrongColorClicks.Visible = true;
                form.lblHintCount.Visible = true;
                form.btnLeaderboard.Visible = true;
                form.btnGenerateRandom.Visible = true;
                form.txtUsername.Enabled = true;
                form.txtUsername.Text = "";
                form.colorPalette.Visible = false;
                form.gameStarted = false;
                form.elapsedSeconds = 0;
                return;
            }
        }

        public void GenerateHardNonogram()
        {
            int gridLeft = 20;
            int gridTop = Math.Max(500, form.chkShowX.Bottom) + 20;

            int w = 20, h = 20;
            int targetPixels = 100; // Pontosan 100 pixel
            Random rnd = form.rnd;
            bool isBoardGood = false;

            while (!isBoardGood)
            {
                form.row = h;
                form.col = w;
                form.solutionBW = new int[h, w];
                int currentPixels = 0;
                int attempts = 0;

                // Blokk-alapú generálás a fix pixelszámig (Hard módban 2-5 hosszú blokkok)
                while (currentPixels < targetPixels && attempts < 2000)
                {
                    attempts++;
                    int r = rnd.Next(h), c = rnd.Next(w);
                    int len = rnd.Next(2, 6); // Kicsit hosszabb blokkok a nehéz pályához

                    if (c + len <= w)
                    {
                        bool spaceFree = true;
                        for (int k = 0; k < len; k++) if (form.solutionBW[r, c + k] == 1) spaceFree = false;

                        if (spaceFree)
                        {
                            for (int k = 0; k < len && currentPixels < targetPixels; k++)
                            {
                                if (form.solutionBW[r, c + k] == 0) { form.solutionBW[r, c + k] = 1; currentPixels++; }
                            }
                        }
                    }
                }

                // Ha maradék pixelszám van, azt egyesével rakjuk le (biztonsági szelep)
                while (currentPixels < targetPixels)
                {
                    int r = rnd.Next(h), c = rnd.Next(w);
                    if (form.solutionBW[r, c] == 0) { form.solutionBW[r, c] = 1; currentPixels++; }
                }

                // Szűrők (Hard szinten is fontos az alapvető egyértelműség)
                bool error = false;
                // Sakk-minta (2x2 kétértelműség) csekk
                for (int i = 0; i < h - 1; i++)
                    for (int j = 0; j < w - 1; j++)
                        if (form.solutionBW[i, j] == form.solutionBW[i + 1, j + 1] &&
                            form.solutionBW[i, j + 1] == form.solutionBW[i + 1, j] &&
                            form.solutionBW[i, j] != form.solutionBW[i, j + 1])
                        { error = true; break; }
                if (error) continue;

                // Identikus sor/oszlop csekk (Hard-nál 20x20-on ritkább, de szűrjük)
                for (int i = 0; i < h - 1; i++)
                    for (int k = i + 1; k < h; k++)
                        if (Enumerable.Range(0, w).All(j => form.solutionBW[i, j] == form.solutionBW[k, j])) error = true;
                if (error) continue;

                // Szín ellenőrzés és színezés (Garantáltan minden színnel)
                form.solutionColorRGB = new Color[h, w];
                HashSet<Color> used = ApplyColorsToHardBlocks(); // Külön metódus a hard színekhez

                if (form.isColor && used.Count < form.hardColors.Length) continue;

                isBoardGood = true;
            }

            // Inicializálás és UI véglegesítése
            form.userXMark = new bool[form.row, form.col];
            form.userColorRGB = new Color[form.row, form.col];
            form.userColor = new int[form.row, form.col];

            for (int i = 0; i < form.row; i++)
                for (int j = 0; j < form.col; j++)
                {
                    if (form.solutionBW[i, j] == 0) // üres cella
                        form.userXMark[i, j] = true; // X-jelölt, ne lehessen kattintani
                    else
                        form.userXMark[i, j] = false; // kitöltendő

                    form.userColorRGB[i, j] = Color.White;
                    form.userColor[i, j] = 0;
                }

            // Preview és Clue-k
            Bitmap preview = GeneratePreviewFromSolution();
            form.picSolutionPreview.Image = ScaleBitmap(preview, 4); // 20x20-nál elég a 4x-es skála

            GenerateClues();
            CreateGridUI(gridLeft, gridTop);
        }

        private HashSet<Color> ApplyColorsToHardBlocks()
        {
            Random rnd = form.rnd;
            HashSet<Color> usedColors = new HashSet<Color>();

            for (int i = 0; i < form.row; i++)
            {
                int j = 0;
                while (j < form.col)
                {
                    if (form.solutionBW[i, j] == 0) { form.solutionColorRGB[i, j] = Color.White; j++; continue; }

                    // Hard színeket használunk (vagy form.hardColors, vagy form.mediumColors, amilyen változód van)
                    Color blockColor = form.isColor ? form.hardColors[rnd.Next(form.hardColors.Length)] : Color.Black;
                    usedColors.Add(blockColor);

                    int k = j;
                    while (k < form.col && form.solutionBW[i, k] == 1)
                    {
                        form.solutionColorRGB[i, k] = blockColor;
                        k++;
                    }
                    j = k;
                }
            }
            return usedColors;
        }

        private Bitmap GeneratePreviewFromSolution()
        {
            Bitmap bmp = new Bitmap(form.col, form.row);

            for (int y = 0; y < form.row; y++)
            {
                for (int x = 0; x < form.col; x++)
                {
                    bmp.SetPixel(
                        x,
                        y,
                        form.solutionBW[y, x] == 1
                            ? form.solutionColorRGB[y, x]
                            : Color.White
                    );
                }
            }

            return bmp;
        }

        private Bitmap ScaleBitmap(Bitmap src, int scale)
        {
            Bitmap bmp = new Bitmap(src.Width * scale, src.Height * scale);

            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;

                g.DrawImage(src, new Rectangle(0, 0, bmp.Width, bmp.Height),
                                new Rectangle(0, 0, src.Width, src.Height),
                                GraphicsUnit.Pixel);
            }
            return bmp;
        }

        /*private Bitmap GenerateHardShapeFixedCount(int w, int h, int filledCount)
        {
            bool isColorMode = form.cmbMode.SelectedItem.ToString() == "Színes";
            Color[] colors = isColorMode ? form.mediumColors : new Color[] { Color.Black };

            Bitmap bmp = new Bitmap(w, h);

            using (Graphics g = Graphics.FromImage(bmp))
                g.Clear(Color.White);

            Random rnd = form.rnd;

            // Összes cella
            List<Point> allCells = new List<Point>();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    allCells.Add(new Point(x, y));

            // Véletlenszerű kiválasztás
            List<Point> filledCells = allCells
                .OrderBy(_ => rnd.Next())
                .Take(filledCount)
                .ToList();

            // Kitöltés
            foreach (Point p in filledCells)
            {
                Color c = colors[rnd.Next(colors.Length)];
                bmp.SetPixel(p.X, p.Y, c);
            }

            return bmp;
        }*/


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
                        if (form.isColor && count > 0 && !AreColorsSimilar(seed, c, form.colorSimilarityThreshold)) break;
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
                        if (form.isColor && count > 0 && !AreColorsSimilar(seed, c, form.colorSimilarityThreshold)) break;
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

            // Ha színes, generálunk egy palettát a kép színeiből
            if (form.isColor) GenerateColorPalette(bmp);

            if (form.picSolutionPreview != null)
            {
                Bitmap finalPreview = GeneratePreviewImage(); // A korábban megírt preview generálód
                form.picSolutionPreview.SizeMode = PictureBoxSizeMode.Zoom;
                form.picSolutionPreview.Image = finalPreview;
            }

            // Erőforrások felszabadítása
            tempBmp.Dispose();
            bmp.Dispose();
        }

        public bool AreColorsSimilar(Color a, Color b, int threshold)
        {
            int dr = a.R - b.R;
            int dg = a.G - b.G;
            int db = a.B - b.B;
            int dist2 = dr * dr + dg * dg + db * db;
            return dist2 <= threshold * threshold;
        }

        private void GenerateColorPalette(Bitmap bmp)
        {
            if (!form.isColor)
            {
                form.colorPalette.Visible = false;
                return;
            }

            form.colorPalette.Controls.Clear();
            form.colorPalette.Visible = true;

            HashSet<Color> uniqueColors = new HashSet<Color>();

            // Gyors beolvasás
            Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            BitmapData bmpData = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            int bytes = Math.Abs(bmpData.Stride) * bmp.Height;
            byte[] rgbValues = new byte[bytes];
            Marshal.Copy(bmpData.Scan0, rgbValues, 0, bytes);

            int bpp = 3;

            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    int idx = y * bmpData.Stride + x * bpp;
                    byte b = rgbValues[idx];
                    byte g = rgbValues[idx + 1];
                    byte r = rgbValues[idx + 2];

                    Color c = Color.FromArgb(r, g, b);

                    // Ha nem teljesen fehér és nem hasonlít a fehérhez egy bizonyos threshold alapján
                    if (!AreColorsSimilar(c, Color.White, 35))  // threshold 35-50 körül jó
                    {
                        uniqueColors.Add(c);
                    }
                    /*if (c != Color.White)   // fehéret kihagyjuk
                        uniqueColors.Add(c);*/
                }
            }

            bmp.UnlockBits(bmpData);

            // Itt lehetne K-means-re redukálni a színeket, pl. maxColors számúra
            List<Color> clusteredColors = KMeansColors(uniqueColors.ToList(), form.maxColors);
            uniqueColors = new HashSet<Color>(clusteredColors);

            // Gombok létrehozása minden színhez
            foreach (Color col in uniqueColors)
            {
                Button btn = new Button();
                btn.BackColor = col;
                btn.Size = new Size(30, 30);
                btn.Margin = new Padding(2);

                btn.FlatStyle = FlatStyle.Flat;
                btn.FlatAppearance.BorderSize = 1;
                btn.FlatAppearance.BorderColor = Color.Gray;

                btn.Click += (s, e) =>
                {
                    form.selectedColor = col;

                    foreach (Button b in form.colorPalette.Controls)
                        b.FlatAppearance.BorderColor = Color.Gray;

                    btn.FlatAppearance.BorderColor = Color.Red;
                };

                form.colorPalette.Controls.Add(btn);
            }

            // Dinamikus magasság
            int buttonsPerRow = Math.Max(1, form.colorPalette.Width / (30 + 4));
            int rows = (int)Math.Ceiling((double)uniqueColors.Count / buttonsPerRow);
            form.colorPalette.Height = rows * (30 + 4);
        }

        private List<Color> KMeansColors(List<Color> colors, int k)
        {
            Random rnd = new Random();
            List<Color> centers = new List<Color>();

            // kezdeti centroidok (véletlenszerű)
            for (int i = 0; i < k; i++)
                centers.Add(colors[rnd.Next(colors.Count)]);

            bool changed = true;
            int iterations = 0;
            while (changed && iterations < 10) // max 10 iteráció
            {
                iterations++;
                changed = false;

                List<List<Color>> clusters = new List<List<Color>>();
                for (int i = 0; i < k; i++) clusters.Add(new List<Color>());

                // hozzárendelés a legközelebbi centroidhoz
                foreach (Color c in colors)
                {
                    int bestIdx = 0;
                    double bestDist = ColorDistance(c, centers[0]);
                    for (int i = 1; i < centers.Count; i++)
                    {
                        double d = ColorDistance(c, centers[i]);
                        if (d < bestDist) { bestDist = d; bestIdx = i; }
                    }
                    clusters[bestIdx].Add(c);
                }

                // centroidok frissítése
                for (int i = 0; i < k; i++)
                {
                    if (clusters[i].Count == 0) continue;
                    int r = (int)clusters[i].Average(c => c.R);
                    int g = (int)clusters[i].Average(c => c.G);
                    int b = (int)clusters[i].Average(c => c.B);
                    Color newCenter = Color.FromArgb(r, g, b);
                    if (newCenter != centers[i]) { centers[i] = newCenter; changed = true; }
                }
            }

            return centers;
        }

        private double ColorDistance(Color c1, Color c2)
        {
            int dr = c1.R - c2.R;
            int dg = c1.G - c2.G;
            int db = c1.B - c2.B;
            return Math.Sqrt(dr * dr + dg * dg + db * db);
        }
    }
}