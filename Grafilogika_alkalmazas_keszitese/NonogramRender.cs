using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

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
        private Nonogram form;
        private NonogramGrid grid;
        private GameTimerManager gameTimerManager;
        private UndoRedoManager undoredoManager;
        private LeaderboardManager leaderboardManager;

        public NonogramRender(Nonogram f, NonogramGrid g, GameTimerManager game, UndoRedoManager u, LeaderboardManager l)
        {
            this.form = f;
            this.grid = g;
            this.gameTimerManager = game;
            this.undoredoManager = u;
            this.leaderboardManager = l;
        }

        public void SetGrid(NonogramGrid g)
        {
            this.grid = g;
        }

        public void SetTimerManager(GameTimerManager gtm)
        {
            this.gameTimerManager = gtm;
        }

        // Preview frissítése
        public void UpdatePreview(int? row = null, int? col = null)
        {
            int rows = grid.row;
            int cols = grid.col;
            int cellSize = grid.userCellSize;
            int pSize = previewSize;

            // Meglévő kép lekérése vagy új létrehozása
            Bitmap bmp;
            if (row.HasValue && col.HasValue && form.picPreview.Image != null)
            {
                bmp = new Bitmap(form.picPreview.Image);
            }
            else
            {
                bmp = new Bitmap(pSize, pSize);
            }

            using (Graphics g = Graphics.FromImage(bmp))
            {
                // Ha az egészet frissítjük, töröljük a vásznat
                if (!row.HasValue) g.Clear(Color.White);

                // Interpoláció a szép skálázáshoz
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;

                // Kiszámoljuk a skálázási arányt (hogy a previewSize-hoz igazodjon)
                float scaleX = (float)pSize / (cols * cellSize);
                float scaleY = (float)pSize / (rows * cellSize);

                int startRow = row ?? 0;
                int endRow = row ?? rows - 1;
                int startCol = col ?? 0;
                int endCol = col ?? cols - 1;

                for (int i = startRow; i <= endRow; i++)
                {
                    for (int j = startCol; j <= endCol; j++)
                    {
                        RectangleF cellRect = new RectangleF(
                            j * cellSize * scaleX,
                            i * cellSize * scaleY,
                            cellSize * scaleX,
                            cellSize * scaleY
                        );

                        if (grid.userXMark[i, j] || grid.aiXMark[i, j])
                        {
                            g.FillRectangle(Brushes.White, cellRect);
                            float fontSize = Math.Min(cellRect.Width, cellRect.Height) * 0.7f;
                            using (Font font = new Font("Arial", fontSize, FontStyle.Bold, GraphicsUnit.Pixel))
                            using (SolidBrush brush = new SolidBrush(Color.Gray))
                            {
                                StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                                g.DrawString("X", font, brush, cellRect, sf);
                            }
                        }
                        else
                        {
                            using (SolidBrush brush = new SolidBrush(grid.userColorRGB[i, j]))
                            {
                                g.FillRectangle(brush, cellRect);
                            }
                        }
                    }
                }
            }

            // Memória felszabadítása
            if (form.picPreview.Image != null) form.picPreview.Image.Dispose();

            form.picPreview.Image = bmp;
        }

        public Bitmap GeneratePreviewImage()
        {
            int rows = grid.row;
            int cols = grid.col;
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
                        RectangleF cellRect = new RectangleF(
                            j * cellWidth,
                            i * cellHeight,
                            cellWidth,
                            cellHeight
                        );

                        Color c = grid.isColor
                            ? grid.solutionColorRGB[i, j]
                            : (grid.solutionBW[i, j] == 1 ? Color.Black : Color.White);

                        using (SolidBrush brush = new SolidBrush(c))
                        {
                            g.FillRectangle(brush, cellRect);
                        }
                    }
                }
            }

            return bmp;
        }

        // Megoldás animálva
        public void SolveNonogram()
        {
            form.btnSolve.Enabled = false;
            gameTimerManager.Stop();
            ClearErrorHighlights();
            List<Point> cellsToSolve = new List<Point>();

            for (int i = 0; i < grid.row; i++)
            {
                for (int j = 0; j < grid.col; j++)
                {
                    grid.gridButtons[i, j].Enabled = false;
                    if (grid.userColorRGB[i, j].ToArgb() != grid.solutionColorRGB[i, j].ToArgb())
                    {
                        // Színes cellák, ahol a felhasználó még nem jó színnel töltött
                        cellsToSolve.Add(new Point(i, j));
                    }
                }
            }

            if (cellsToSolve.Count == 0)
            {
                MessageBox.Show("Már minden cella a helyén van!", "Megoldás");
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
                SetGridEnabled(true, form.chkColorMode.Checked, form.chkBlackWhiteMode.Checked);
                gameTimerManager.Stop();
                if (MessageBox.Show("A nonogram teljesen kirakva!", "Megoldás kész") == DialogResult.OK)
                {
                    if (!form.chkBlackWhiteMode.Checked && !form.chkColorMode.Checked)
                        gameTimerManager.RestartGameWithCurrentDifficulty();
                }
                return;
            }

            Point p = solutionQueue.Dequeue();
            int i = p.X;
            int j = p.Y;
            Button btn = grid.gridButtons[i, j];


            // Színes vagy fekete fehér cella normál kitöltés
            if (grid.isColor)
            {
                grid.userColorRGB[i, j] = grid.solutionColorRGB[i, j];
                btn.BackColor = grid.solutionColorRGB[i, j];
            }
            else
            {
                grid.userColorRGB[i, j] = grid.solutionBW[i, j] == 1 ? Color.Black : Color.White;
                btn.BackColor = grid.userColorRGB[i, j];
            }

            btn.Text = "";
            grid.userXMark[i, j] = false;

            UpdatePreview(i, j);
        }

        public void ShowHint()
        {
            ClearErrorHighlights();
            List<Point> wrongCells = new List<Point>();

            for (int i = 0; i < grid.row; i++)
            {
                for (int j = 0; j < grid.col; j++)
                {
                    if (!grid.gridButtons[i, j].Enabled) continue;

                    bool isColorInSolution = grid.isColor
                        ? grid.solutionColorRGB[i, j].ToArgb() != Color.White.ToArgb()
                        : grid.solutionBW[i, j] == 1;

                    bool hasVisualX = (grid.gridButtons[i, j].Text == "X");
                    bool hasColor = grid.userColorRGB[i, j].ToArgb() != Color.White.ToArgb() &&
                                    grid.userColorRGB[i, j].ToArgb() != 0;

                    bool isWrong = false;

                    if (isColorInSolution)
                    {
                        if (!hasColor || hasVisualX) isWrong = true;
                        if (!isWrong && grid.isColor && !AreColorsSimilar(grid.userColorRGB[i, j], grid.solutionColorRGB[i, j], 40))
                            isWrong = true;
                    }
                    else
                    {
                        // fehér hely hiba, ha szín van ott
                        if (hasColor) isWrong = true;
                    }

                    if (isWrong)
                        wrongCells.Add(new Point(i, j));
                }
            }

            if (wrongCells.Count == 0)
            {
                if (IsSolved()) { FinalizeGame(); return; }

                MessageBox.Show("Minden a helyén van a jelenlegi módban!");
                return;
            }

            Point hintCell = wrongCells[form.rnd.Next(wrongCells.Count)];
            int iH = hintCell.X;
            int jH = hintCell.Y;
            Button btn = grid.gridButtons[iH, jH];

            bool shouldBeColor = grid.isColor
                ? grid.solutionColorRGB[iH, jH].ToArgb() != Color.White.ToArgb()
                : grid.solutionBW[iH, jH] == 1;

            if (shouldBeColor)
            {
                // Színes cella javítása
                btn.Text = "";
                grid.userXMark[iH, jH] = false;
                Color solColor = grid.isColor ? grid.solutionColorRGB[iH, jH] : Color.Black;
                grid.userColorRGB[iH, jH] = solColor;
                btn.BackColor = solColor;
            }
            else
            {
                // Fehér cella töröljük minden ott lévő színt vagy X-et
                btn.Text = "";
                grid.userXMark[iH, jH] = false;
                grid.userColorRGB[iH, jH] = Color.White;
                btn.BackColor = Color.White;
            }
            grid.isHintFixed[iH, jH] = true;
            btn.Enabled = false;
            UpdatePreview(iH, jH);

            // a Hint után azonnal ellenőrizzük, kész-e!
            if (IsSolved())
            {
                FinalizeGame();
            }
        }

        public void CheckSolution()
        {
            showHints = false;
            for (int i = 0; i < grid.row; i++)
            {
                for (int j = 0; j < grid.col; j++)
                {
                    grid.gridButtons[i, j].Invalidate();
                }
            }
            undoredoManager.ClearHistory();
            bool correct = true;

            for (int i = 0; i < grid.row; i++)
            {
                for (int j = 0; j < grid.col; j++)
                {
                    // Megoldás meghatározása
                    bool shouldBeColor = grid.isColor
                        ? grid.solutionColorRGB[i, j].ToArgb() != Color.White.ToArgb()
                        : grid.solutionBW[i, j] == 1;

                    bool hasVisualX = (grid.gridButtons[i, j].Text == "X");
                    bool hasColor = grid.userColorRGB[i, j].ToArgb() != Color.White.ToArgb() &&
                                    grid.userColorRGB[i, j].ToArgb() != 0;

                    if (shouldBeColor)
                    {
                        // szín hiba (rossz szín vagy X (üres hely) van ott)
                        bool colorMatch = grid.isColor
                            ? AreColorsSimilar(grid.userColorRGB[i, j], grid.solutionColorRGB[i, j], 40)
                            : (grid.userColorRGB[i, j].ToArgb() == Color.Black.ToArgb());

                        if (!colorMatch || hasVisualX)
                        {
                            correct = false;
                            grid.gridButtons[i, j].BackColor = Color.DarkRed;
                        }
                    }
                    else
                    {
                        // fehér hiba
                        if (hasColor)
                        {
                            correct = false;
                            grid.gridButtons[i, j].BackColor = Color.DarkRed;
                        }
                    }
                }
            }
            if (correct || form.chkBlackWhiteMode.Checked || form.chkColorMode.Checked)
            {
                form.btnCheck.Enabled = true;
                MessageBox.Show("Gratulálok, helyes megoldás!", "Ellenőrzés", MessageBoxButtons.OK);
                return;
            }
            if (correct)
            {
                if (MessageBox.Show("Gratulálok, helyes megoldás!", "Ellenőrzés", MessageBoxButtons.OK) == DialogResult.OK)
                    gameTimerManager.RestartGameWithCurrentDifficulty();
            }
            else
            {
                MessageBox.Show("Vannak hibás mezők! Kattints a rácsra a javításhoz.", "Ellenőrzés", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        public void ToggleXMarks(bool show)
        {
            if (grid.gridButtons == null) return;

            for (int i = 0; i < grid.row; i++)
            {
                for (int j = 0; j < grid.col; j++)
                {
                    Button btn = grid.gridButtons[i, j];
                    if (btn == null) continue;

                    bool isWhite = grid.solutionColorRGB[i, j].ToArgb() == Color.White.ToArgb();

                    if (isWhite)
                    {
                        if (show)
                        {
                            // A generált cellSize-hoz igazítjuk a betűt
                            float fontSize = grid.userCellSize * 0.3f;
                            btn.Font = new Font("Arial", fontSize, FontStyle.Bold);
                            btn.ForeColor = Color.Gray; // Az X színe
                            btn.Text = "X";
                            btn.TextAlign = ContentAlignment.MiddleCenter;

                            grid.userXMark[i, j] = true;
                        }
                        else
                        {
                            // Töröljük a felhasználói X-et, de az AI X marad
                            if (!grid.aiXMark[i, j])
                                btn.Text = "";

                            grid.userXMark[i, j] = false;
                        }
                    }
                }
            }
        }

        public void SetGridEnabled(bool enabled, bool isColorImgMode, bool isBlackAndWhiteImgMode)
        {
            for (int i = 0; i < grid.gridButtons.GetLength(0); i++)
            {
                for (int j = 0; j < grid.gridButtons.GetLength(1); j++)
                {
                    if (isColorImgMode || isBlackAndWhiteImgMode)
                    {
                        // fekete fehér és színes képbeolvasós módban semmi nem kattintható
                        grid.gridButtons[i, j].Enabled = false;
                    }
                    else
                    {
                        // Normál módban csak a nem X-es cellák
                        if (!grid.userXMark[i, j])
                            grid.gridButtons[i, j].Enabled = enabled;
                    }
                }
            }
        }

        public void AdjustCheckboxPositions()
        {
            int margin = 10;
            form.chkShowX.Location = new Point(20, form.picPreview.Bottom + margin);
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
            for (int y = 0; y < original.Height; y++)
            {
                for (int x = 0; x < original.Width; x++)
                {
                    int index = y * srcData.Stride + x * bytesPerPixel;
                    byte b = srcBuffer[index];
                    byte g = srcBuffer[index + 1];
                    byte r = srcBuffer[index + 2];

                    // Szürke érték számítása
                    int grayValue = (int)(0.299 * r + 0.587 * g + 0.114 * b);

                    // Küszöbölés fekete fehérre
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
            for (int i = 0; i < grid.row; i++)
            {
                for (int j = 0; j < grid.col; j++)
                {
                    Button btn = grid.gridButtons[i, j];

                    // Háttérszín visszaállítása (DarkRed és eredeti szín)
                    if (btn.BackColor == Color.DarkRed)
                    {
                        if (grid.isColor)
                        {
                            Color c = grid.userColorRGB[i, j];
                            btn.BackColor = c.IsEmpty ? Color.White : c;
                        }
                        else
                        {
                            btn.BackColor = (grid.userColorRGB[i, j].ToArgb() == Color.Black.ToArgb()) ? Color.Black : Color.White;
                        }
                    }

                    // hiba X törlése
                    if (btn.Text == "X" && btn.ForeColor == Color.Red)
                    {
                        btn.Text = "";
                        grid.userXMark[i, j] = false;
                    }
                }
            }
        }

        public void RefreshGrid()
        {
            for (int i = 0; i < grid.row; i++)
                for (int j = 0; j < grid.col; j++)
                    grid.gridButtons[i, j].Invalidate();
        }
        public void SelectColor(Button colorBtn)
        {
            grid.selectedColor = colorBtn.BackColor;

            foreach (Button b in form.colorPalette.Controls)
                b.FlatAppearance.BorderColor = Color.Gray;

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
            // X → fekete
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
            // fekete → X
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
        public bool AreColorsSimilar(Color a, Color b, int threshold)
        {
            int dr = a.R - b.R;
            int dg = a.G - b.G;
            int db = a.B - b.B;
            int dist2 = dr * dr + dg * dg + db * db;
            return dist2 <= threshold * threshold;
        }

        public bool IsCellCorrect(int row, int col)
        {
            if (grid.solutionColorRGB == null ||
                grid.gridButtons == null ||
                row < 0 || row >= grid.row || col < 0 || col >= grid.col)
                return false;
            // Megoldás meghatározása (szín vagy fekete)
            bool shouldBeColor = grid.isColor
                ? (grid.solutionColorRGB[row, col].ToArgb() != Color.White.ToArgb())
                : grid.solutionBW[row, col] == 1;

            // Felhasználó aktuális állapotának lekérése
            Color userC = grid.userColorRGB[row, col];

            // hasColor akkor tekintjük színesnek, ha nem üres, nem átlátszó és nem fehér
            bool hasColor = !userC.IsEmpty &&
                            userC.ToArgb() != 0 &&
                            userC.ToArgb() != Color.White.ToArgb();

            bool hasX = (grid.gridButtons[row, col].Text == "X");

            // ellenőrzési logika
            if (shouldBeColor)
            {
                // Hiba, ha nincs szín, vagy ha véletlenül X van ott
                if (!hasColor || hasX) return false;

                // Színes módnál a konkrét árnyalatot is nézzük
                if (grid.isColor)
                    return AreColorsSimilar(userC, grid.solutionColorRGB[row, col], 40);

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
                return false;
            for (int i = 0; i < grid.row; i++)
            {
                for (int j = 0; j < grid.col; j++)
                {
                    // Ha csak egyetlen cella is rossz, már nincs kész
                    if (!IsCellCorrect(i, j))
                        return false;
                }
            }
            return true;
        }

        public void ChkShowX_CheckedChanged(object sender, EventArgs e)
        {
            // Biztonsági ellenőrzés ha a tömb null, megállunk
            if (grid.gridButtons == null) return;

            bool show = form.chkShowX.Checked;

            ToggleXMarks(show);

            if (show)
            {
                gameTimerManager.Stop();
            }
            else
            {
                if (form.chkShowX.Visible)
                {
                    gameTimerManager.Start();
                }
            }
            UpdatePreview();
        }

        public void BtnSelectImage_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Képfájlok|*.png;*.jpg;*.jpeg;*.bmp";
            ofd.Title = "Kép kiválasztása Nonogramhoz";
            ofd.InitialDirectory = AppDomain.CurrentDomain.BaseDirectory;

            if (ofd.ShowDialog() != DialogResult.OK)
                return;

            Image loadedImg = Image.FromFile(ofd.FileName);
            Bitmap bmpToUse;

            bool useGrayscale = (form.chkBlackWhiteMode.Checked);

            if (useGrayscale)
            {
                bmpToUse = ConvertToBlackAndWhite(new Bitmap(loadedImg));
            }
            else
            {
                bmpToUse = new Bitmap(loadedImg);
            }

            if (form.chkColorMode.Checked)
            {
                bmpToUse = RemoveLightBackground(bmpToUse, 200);
            }

            form.img = bmpToUse;

            // Megjelenítés a megoldás előnézetben
            form.picSolutionPreview.Image = bmpToUse;
            form.picSolutionPreview.SizeMode = PictureBoxSizeMode.Zoom;

            int gridLeft = 20;
            int gridTop = form.chkShowX.Bottom + 20;

            grid.ClearGrid();

            // Új grid generálása a kiválasztott képből
            grid.GenerateNonogramFromImage(bmpToUse, gridLeft, gridTop);

            // Gombok és vezérlők engedélyezése
            form.btnSolve.Enabled = true;
            form.btnHint.Enabled = true;
            form.btnCheck.Enabled = true;
            form.btnRedo.Enabled = true;
            form.btnUndo.Enabled = true;
            form.chkShowX.Enabled = true;
            form.colorPalette.Visible = false;

            // Előnézet frissítése
            UpdatePreview();
            SetGridEnabled(true, form.chkColorMode.Checked, form.chkBlackWhiteMode.Checked);
        }

        // Megoldás gomb esemény
        public void BtnSolve_Click(object sender, EventArgs e)
        {
            SolveNonogram();
        }

        // Segítség gomb esemény
        public void BtnHint_Click(object sender, EventArgs e)
        {
            ShowHint();
            hintCount++;
            form.lblHintCount.Text = $"Segítségek száma: {hintCount} (max: {maxHintCount})";
            if (hintCount >= maxHintCount)
            {
                MessageBox.Show(
                    "Elérted a maximális segítségek számát! A játék újraindul.",
                    "Figyelem",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );

                gameTimerManager.RestartGameWithCurrentDifficulty();
            }
        }

        // Ellenőrzés gomb esemény
        public void BtnCheck_Click(object sender, EventArgs e)
        {
            form.btnCheck.Enabled = false;
            CheckSolution();
        }

        public Color[] GetTwoRandomColors()
        {
            Random rnd = form.rnd;

            int firstIndex = rnd.Next(grid.nonogramPalette.Length);
            int secondIndex;
            do
            {
                secondIndex = rnd.Next(grid.nonogramPalette.Length);
            } while (secondIndex == firstIndex);

            return new Color[] { grid.nonogramPalette[firstIndex], grid.nonogramPalette[secondIndex] };
        }
        public void ApplyColorsToBlocks()
        {
            if (!grid.isColor)
            {
                for (int i = 0; i < grid.row; i++)
                    for (int j = 0; j < grid.col; j++)
                        grid.solutionColorRGB[i, j] = grid.solutionBW[i, j] == 1 ? Color.Black : Color.White;
                return;
            }

            Random rnd = new Random();
            HashSet<Color> usedColors = new HashSet<Color>();

            for (int i = 0; i < grid.row; i++)
            {
                Color lastColor = Color.Empty;
                int j = 0;

                while (j < grid.col)
                {
                    if (grid.solutionBW[i, j] == 0)
                    {
                        grid.solutionColorRGB[i, j] = Color.White;
                        lastColor = Color.Empty;
                        j++;
                        continue;
                    }

                    // Új blokk kezdete
                    List<Color> availableColors = grid.nonogramPalette
                    .Where(c => c != lastColor)
                    .OrderBy(x => rnd.Next())
                    .Take(grid.isColor ? 2 : grid.nonogramPalette.Length)
                    .ToList();

                    Color blockColor = availableColors[rnd.Next(availableColors.Count)];

                    lastColor = blockColor;
                    usedColors.Add(blockColor);

                    // Blokk feltöltése
                    int k = j;
                    while (k < grid.col && grid.solutionBW[i, k] == 1)
                    {
                        grid.solutionColorRGB[i, k] = blockColor;
                        k++;
                    }

                    j = k;
                }
            }

            // Ha csak 1 szín lett használva, cseréljünk ki egy blokkot a második színre
            if (usedColors.Count == 1 && grid.nonogramPalette.Length > 1)
            {
                Color firstColor = usedColors.First();
                Color secondColor = grid.nonogramPalette.First(c => c != firstColor);

                // Véletlenszerű blokk módosítása
                for (int i = 0; i < grid.row; i++)
                {
                    for (int j = 0; j < grid.col; j++)
                    {
                        if (grid.solutionBW[i, j] == 1 && grid.solutionColorRGB[i, j] == firstColor)
                        {
                            grid.solutionColorRGB[i, j] = secondColor;
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

            for (int y = 0; y < src.Height; y++)
            {
                for (int x = 0; x < src.Width; x++)
                {
                    int i = y * data.Stride + x * bpp;

                    byte b = buffer[i];
                    byte g = buffer[i + 1];
                    byte r = buffer[i + 2];

                    // nagyon világos háttér fehér
                    if (r > threshold && g > threshold && b > threshold)
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
            MessageBox.Show("Gratulálok, kész a Nonogram!");

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

                MessageBox.Show("A játék automatikusan elmentve!", "Mentés", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hiba a mentés során:\n" + ex.Message, "Hiba", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            // UI alaphelyzetbe állítása
            grid.ClearGrid();
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
            form.btnTips.Visible = true;
            form.lblUndoCount.Visible = true;
            form.lblRedoCount.Visible = true;
            form.btnRestart.Visible = false;
            form.btnSmartAI.Visible = false;
            form.chkExtraMode.Visible = true;
            form.chkBlackWhiteMode.Visible = true;
            form.chkColorMode.Visible = true;
            form.txtUsername.Enabled = true;
            form.txtUsername.Text = "";
            form.colorPalette.Visible = false;
            gameTimerManager.gameStarted = false;
            gameTimerManager.elapsedSeconds = 0;
            form.lblWrongCellClicks.Text = $"Helytelen kattintások száma: {grid.wrongCellClicks} (max: 0)";
            form.lblWrongColorClicks.Text = $"Helytelen színek száma: {grid.wrongColorClicks} (max: 0)";
            form.lblHintCount.Text = $"Segítségek száma: {hintCount} (max: 0)";
            form.lblUndoCount.Text = $"Visszavonások száma: {undoredoManager.undoClicks} (max: 0)";
            form.lblRedoCount.Text = $"Előrelépések száma: {undoredoManager.redoClicks} (max: 0)";
            return;
        }
    }
}