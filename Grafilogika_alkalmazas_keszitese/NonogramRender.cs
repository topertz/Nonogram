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
        private Nonogram form;
        private NonogramGrid grid;
        private NonogramRender render;
        private GameTimerManager gameTimerManager;
        private UndoRedoManager undoredoManager;
        private SaveLoadManager saveLoadManager;

        public NonogramRender(Nonogram f, NonogramGrid g, GameTimerManager game, UndoRedoManager u, SaveLoadManager saveLoadManager)
        {
            form = f;
            grid = g;
            this.gameTimerManager = game;
            this.undoredoManager = u;
            this.saveLoadManager = saveLoadManager;
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
            int rows = form.row;
            int cols = form.col;
            int cellSize = form.userCellSize;
            int pSize = form.previewSize;

            // 1. Meglévő kép lekérése vagy új létrehozása
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

                // Ciklus: Ha van konkrét koordináta, csak azt futtatja le egyszer. 
                // Ha nincs, végigmegy az összesen.
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

                        if (form.isXMode && form.userXMark[i, j])
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
                            using (SolidBrush brush = new SolidBrush(form.userColorRGB[i, j]))
                            {
                                g.FillRectangle(brush, cellRect);
                            }
                        }
                    }
                }
            }

            // Memória felszabadítása (fontos!)
            if (form.picPreview.Image != null) form.picPreview.Image.Dispose();

            form.picPreview.Image = bmp;
        }

        // Megoldás animálva
        public void SolveNonogram()
        {
            form.btnSolve.Enabled = false;
            gameTimerManager.Stop();
            ClearErrorHighlights();
            List<Point> cellsToSolve = new List<Point>();

            for (int i = 0; i < form.row; i++)
            {
                for (int j = 0; j < form.col; j++)
                {
                    form.gridButtons[i, j].Enabled = false;
                    if (form.userColorRGB[i, j].ToArgb() != form.solutionColorRGB[i, j].ToArgb())
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

            form.solutionQueue = new Queue<Point>(cellsToSolve);

            form.solveTimer = new Timer();
            form.solveTimer.Interval = 100;
            form.solveTimer.Tick += SolveTimer_Tick;
            form.solveTimer.Start();
        }

        private void SolveTimer_Tick(object sender, EventArgs e)
        {
            if (form.solutionQueue.Count == 0)
            {
                form.solveTimer.Stop();
                form.solveTimer.Dispose();
                SetGridEnabled(true, form.chkColorMode.Checked, form.chkBlackWhiteMode.Checked);
                gameTimerManager.Stop();
                if (MessageBox.Show("A nonogram teljesen kirakva!", "Megoldás kész") == DialogResult.OK)
                {
                    if (!form.chkBlackWhiteMode.Checked && !form.chkColorMode.Checked)
                        gameTimerManager.RestartGameWithCurrentDifficulty();
                }
                return;
            }

            Point p = form.solutionQueue.Dequeue();
            int i = p.X;
            int j = p.Y;
            Button btn = form.gridButtons[i, j];


            // Színes vagy fekete-fehér cella normál kitöltés
            if (form.isColor)
            {
                form.userColorRGB[i, j] = form.solutionColorRGB[i, j];
                btn.BackColor = form.solutionColorRGB[i, j];
            }
            else
            {
                form.userColorRGB[i, j] = form.solutionBW[i, j] == 1 ? Color.Black : Color.White;
                btn.BackColor = form.userColorRGB[i, j];
            }

            btn.Text = "";
            form.userXMark[i, j] = false;


            // Preview frissítése csak az adott cellára
            UpdatePreview(i, j);
        }

        public void ShowHint()
        {
            ClearErrorHighlights();
            List<Point> wrongCells = new List<Point>();

            for (int i = 0; i < form.row; i++)
            {
                for (int j = 0; j < form.col; j++)
                {
                    // CSAK AKKOR ugorjuk át, ha a gomb le van tiltva (tehát már egy korábbi HINT fixálta)
                    if (!form.gridButtons[i, j].Enabled) continue;

                    bool isColorInSolution = form.isColor
                        ? form.solutionColorRGB[i, j].ToArgb() != Color.White.ToArgb()
                        : form.solutionBW[i, j] == 1;

                    bool hasVisualX = (form.gridButtons[i, j].Text == "X");
                    bool hasColor = form.userColorRGB[i, j].ToArgb() != Color.White.ToArgb() &&
                                    form.userColorRGB[i, j].ToArgb() != 0;

                    bool isWrong = false;

                    if (isColorInSolution)
                    {
                        if (!hasColor || hasVisualX) isWrong = true;
                        if (!isWrong && form.isColor && !AreColorsSimilar(form.userColorRGB[i, j], form.solutionColorRGB[i, j], 40))
                            isWrong = true;
                    }
                    else
                    {
                        // FEHÉR hely: hiba, ha szín van ott
                        if (hasColor) isWrong = true;
                    }

                    if (isWrong)
                        wrongCells.Add(new Point(i, j));
                }
            }

            if (wrongCells.Count == 0)
            {
                // Ha ide jut, és még nincs kész a játék, akkor az IsSolved-dal van a gond
                if (grid.IsSolved()) { FinalizeGame(); return; }

                MessageBox.Show("Minden a helyén van a jelenlegi módban!");
                return;
            }

            Point hintCell = wrongCells[form.rnd.Next(wrongCells.Count)];
            int iH = hintCell.X;
            int jH = hintCell.Y;
            Button btn = form.gridButtons[iH, jH];

            bool shouldBeColor = form.isColor
                ? form.solutionColorRGB[iH, jH].ToArgb() != Color.White.ToArgb()
                : form.solutionBW[iH, jH] == 1;

            if (shouldBeColor)
            {
                // Színes cella javítása
                btn.Text = "";
                form.userXMark[iH, jH] = false;
                Color solColor = form.isColor ? form.solutionColorRGB[iH, jH] : Color.Black;
                form.userColorRGB[iH, jH] = solColor;
                btn.BackColor = solColor;
            }
            else
            {
                // Fehér cella: töröljük minden ott lévő színt vagy X-et
                btn.Text = "";
                form.userXMark[iH, jH] = false;
                form.userColorRGB[iH, jH] = Color.White;
                btn.BackColor = Color.White;
            }
            form.isHintFixed[iH, jH] = true;
            btn.Enabled = false;
            UpdatePreview(iH, jH);

            // Fontos: a Hint után azonnal ellenőrizzük, kész-e!
            if (grid.IsSolved())
            {
                FinalizeGame();
            }
        }

        public void CheckSolution()
        {
            form.showHints = false;
            for (int i = 0; i < form.row; i++)
            {
                for (int j = 0; j < form.col; j++)
                {
                    form.gridButtons[i, j].Invalidate();
                }
            }
            //gameTimerManager.Stop();
            undoredoManager.ClearHistory();
            bool correct = true;

            for (int i = 0; i < form.row; i++)
            {
                for (int j = 0; j < form.col; j++)
                {
                    // Megoldás meghatározása
                    bool shouldBeColor = form.isColor
                        ? form.solutionColorRGB[i, j].ToArgb() != Color.White.ToArgb()
                        : form.solutionBW[i, j] == 1;

                    bool hasVisualX = (form.gridButtons[i, j].Text == "X");
                    bool hasColor = form.userColorRGB[i, j].ToArgb() != Color.White.ToArgb() &&
                                    form.userColorRGB[i, j].ToArgb() != 0;

                    if (shouldBeColor)
                    {
                        // SZÍN HIBA (Rossz szín vagy X van ott)
                        bool colorMatch = form.isColor
                            ? AreColorsSimilar(form.userColorRGB[i, j], form.solutionColorRGB[i, j], 40)
                            : (form.userColorRGB[i, j].ToArgb() == Color.Black.ToArgb());

                        if (!colorMatch || hasVisualX)
                        {
                            correct = false;
                            form.gridButtons[i, j].BackColor = Color.DarkRed;
                        }
                    }
                    else
                    {
                        // FEHÉR HIBA
                        if (hasColor)
                        {
                            correct = false;
                            form.gridButtons[i, j].BackColor = Color.DarkRed;
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
                //gameTimerManager.Start();
                MessageBox.Show("Vannak hibás mezők! Kattints a rácsra a javításhoz.", "Ellenőrzés", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        public void ToggleXMarks(bool show)
        {
            if (form.gridButtons == null) return;

            for (int i = 0; i < form.row; i++)
            {
                for (int j = 0; j < form.col; j++)
                {
                    Button btn = form.gridButtons[i, j];
                    if (btn == null) continue;

                    // színes logika: 
                    // Az a cella nem lesz kitöltve, ami fehér (255, 255, 255)
                    // A ToArgb() összehasonlítás a legbiztosabb
                    bool isWhite = form.solutionColorRGB[i, j].ToArgb() == Color.White.ToArgb();

                    if (isWhite)
                    {
                        if (show)
                        {
                            // A generált cellSize-hoz igazítjuk a betűt
                            float fontSize = form.userCellSize * 0.3f;
                            btn.Font = new Font("Arial", fontSize, FontStyle.Bold);
                            btn.ForeColor = Color.Gray; // Az X színe
                            btn.Text = "X";
                            btn.TextAlign = ContentAlignment.MiddleCenter;

                            form.userXMark[i, j] = true;
                        }
                        else
                        {
                            btn.Text = "";
                        }
                    }
                }
            }
        }

        public void SetGridEnabled(bool enabled, bool isColorMode, bool isBlackAndWhiteMode)
        {
            for (int i = 0; i < form.gridButtons.GetLength(0); i++)
            {
                for (int j = 0; j < form.gridButtons.GetLength(1); j++)
                {
                    if (isColorMode || isBlackAndWhiteMode)
                    {
                        // Hard színes módban SEMMI nem kattintható
                        form.gridButtons[i, j].Enabled = false;
                    }
                    else
                    {
                        // Normál módban csak a nem X-es cellák
                        if (!form.userXMark[i, j])
                            form.gridButtons[i, j].Enabled = enabled;
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

                    // Küszöbölés fekete-fehérre
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
            for (int i = 0; i < form.row; i++)
            {
                for (int j = 0; j < form.col; j++)
                {
                    Button btn = form.gridButtons[i, j];

                    // 1. Háttérszín visszaállítása (DarkRed -> Eredeti szín)
                    if (btn.BackColor == Color.DarkRed)
                    {
                        if (form.isColor)
                        {
                            Color c = form.userColorRGB[i, j];
                            btn.BackColor = c.IsEmpty ? Color.White : c;
                        }
                        else
                        {
                            btn.BackColor = (form.userColorRGB[i, j].ToArgb() == Color.Black.ToArgb()) ? Color.Black : Color.White;
                        }
                    }

                    // 2. HIBA-X TÖRLÉSE (Csak ha piros!)
                    // A játékos saját X-e szürke (Gray), azt nem bántjuk!
                    if (btn.Text == "X" && btn.ForeColor == Color.Red)
                    {
                        btn.Text = "";
                        form.userXMark[i, j] = false; // Logikailag is ürítjük
                    }
                }
            }
        }

        public void RefreshGrid()
        {
            for (int i = 0; i < form.row; i++)
                for (int j = 0; j < form.col; j++)
                    form.gridButtons[i, j].Invalidate();
        }
        public void SelectColor(Button colorBtn)
        {
            form.selectedColor = colorBtn.BackColor;

            foreach (Button b in form.colorPalette.Controls)
                b.FlatAppearance.BorderColor = Color.Gray;

            colorBtn.FlatAppearance.BorderColor = Color.Red;
        }

        public void SetCellColor(int row, int col, Button btn, Color color)
        {
            form.userColorRGB[row, col] = color;
            form.userColor[row, col] = 1; // jelzés, hogy kitöltve
            btn.BackColor = color;
            btn.Text = "";
            form.userXMark[row, col] = false;
        }

        public void SetCellBlack(int row, int col, Button btn)
        {
            // X → fekete
            if (form.userXMark[row, col])
            {
                form.userXMark[row, col] = false;
                btn.Text = "";
            }

            btn.BackColor = Color.Black;
            form.userColorRGB[row, col] = Color.Black;
            form.userColor[row, col] = 1;
        }

        public void SetCellX(int row, int col, Button btn)
        {
            // fekete → X
            btn.BackColor = Color.White;
            form.userColorRGB[row, col] = Color.White;
            form.userColor[row, col] = 0;

            form.userXMark[row, col] = true;

            float fontSize = form.userCellSize * 0.3f;
            btn.Font = new Font("Arial", fontSize, FontStyle.Bold);
            btn.ForeColor = Color.Gray;
            btn.Text = "X";
            btn.TextAlign = ContentAlignment.MiddleCenter;
        }

        public void ClearCell(int row, int col, Button btn)
        {
            btn.BackColor = Color.White;
            btn.Text = "";
            form.userXMark[row, col] = false;
            form.userColor[row, col] = 0;
            form.userColorRGB[row, col] = Color.White;
        }
        public bool AreColorsSimilar(Color a, Color b, int threshold)
        {
            int dr = a.R - b.R;
            int dg = a.G - b.G;
            int db = a.B - b.B;
            int dist2 = dr * dr + dg * dg + db * db;
            return dist2 <= threshold * threshold;
        }

        public void ChkShowX_CheckedChanged(object sender, EventArgs e)
        {
            // Biztonsági ellenőrzés: ha a tömb null, megállunk
            if (form.gridButtons == null) return;

            bool show = form.chkShowX.Checked;

            ToggleXMarks(show);

            // Biztonságos végigfuttatás
            for (int i = 0; i < form.gridButtons.GetLength(0); i++)
            {
                for (int j = 0; j < form.gridButtons.GetLength(1); j++)
                {
                    // Ellenőrizzük, hogy a gomb példány létezik-e!
                    if (form.gridButtons[i, j] != null)
                    {
                        //form.gridButtons[i, j].Enabled = !show;
                        form.gridButtons[i, j].TabStop = false;
                    }
                }
            }

            if (show)
            {
                gameTimerManager.Stop();
            }
            else
            {
                // Csak akkor állítjuk, ha a sender (a CheckBox) még engedélyezett
                // és nem egy törlési folyamat része vagyunk
                if (form.chkShowX.Visible)
                {
                    gameTimerManager.Start();
                }
            }
        }

        public void BtnSelectImage_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Képfájlok|*.png;*.jpg;*.jpeg;*.bmp";
            ofd.Title = "Kép kiválasztása Nonogramhoz";

            if (ofd.ShowDialog() != DialogResult.OK)
                return;

            Image loadedImg = Image.FromFile(ofd.FileName);
            Bitmap bmpToUse;

            // Logika meghatározása a játékmód (cmbMode) vagy nehézség alapján
            // Ha a játékmód 0. indexe a "Fekete-fehér"
            bool useGrayscale = (form.chkBlackWhiteMode.Checked);

            if (useGrayscale)
            {
                bmpToUse = ConvertToBlackAndWhite(new Bitmap(loadedImg));
            }
            else
            {
                bmpToUse = new Bitmap(loadedImg);
            }

            // Nagyon nehéz szinten (3) extra feldolgozás (háttér eltávolítás)
            if (form.chkColorMode.Checked)
            {
                bmpToUse = RemoveLightBackground(bmpToUse, 200);
            }

            form.img = bmpToUse;

            // Megjelenítés a megoldás előnézetben
            form.picSolutionPreview.Image = bmpToUse;
            form.picSolutionPreview.SizeMode = PictureBoxSizeMode.Zoom;

            // A grid elhelyezése (mivel a chkGrayscale kikerült, a chkShowX-hez igazítjuk)
            int gridLeft = 20;
            int gridTop = form.chkShowX.Bottom + 20;

            // Korábbi grid törlése
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

            // Időzítő indítása
            if (form.cmbDifficulty.SelectedIndex != 2 || form.cmbDifficulty.SelectedIndex != 3)
                gameTimerManager.Start();

            // Előnézet frissítése
            UpdatePreview();
            SetGridEnabled(true, form.chkColorMode.Checked, form.chkBlackWhiteMode.Checked);
        }

        public void ApplyColorsToBlocks()
        {
            if (!form.isColor)
            {
                for (int i = 0; i < form.row; i++)
                    for (int j = 0; j < form.col; j++)
                        form.solutionColorRGB[i, j] = form.solutionBW[i, j] == 1 ? Color.Black : Color.White;
                return;
            }

            Random rnd = new Random();
            HashSet<Color> usedColors = new HashSet<Color>();

            for (int i = 0; i < form.row; i++)
            {
                Color lastColor = Color.Empty;
                int j = 0;

                while (j < form.col)
                {
                    if (form.solutionBW[i, j] == 0)
                    {
                        form.solutionColorRGB[i, j] = Color.White;
                        lastColor = Color.Empty;
                        j++;
                        continue;
                    }

                    // Új blokk kezdete
                    List<Color> availableColors = form.nonogramColors
                    .Where(c => c != lastColor)
                    .OrderBy(x => rnd.Next())
                    .Take(form.isColor ? 2 : form.nonogramColors.Length)
                    .ToList();

                    Color blockColor = availableColors[rnd.Next(availableColors.Count)];

                    lastColor = blockColor;
                    usedColors.Add(blockColor);

                    // Blokk feltöltése
                    int k = j;
                    while (k < form.col && form.solutionBW[i, k] == 1)
                    {
                        form.solutionColorRGB[i, k] = blockColor;
                        k++;
                    }

                    j = k;
                }
            }

            // Ha csak 1 szín lett használva, cseréljünk ki egy blokkot a második színre
            if (usedColors.Count == 1 && form.nonogramColors.Length > 1)
            {
                Color firstColor = usedColors.First();
                Color secondColor = form.nonogramColors.First(c => c != firstColor);

                // Véletlenszerű blokk módosítása
                for (int i = 0; i < form.row; i++)
                {
                    for (int j = 0; j < form.col; j++)
                    {
                        if (form.solutionBW[i, j] == 1 && form.solutionColorRGB[i, j] == firstColor)
                        {
                            form.solutionColorRGB[i, j] = secondColor;
                            return; // elég egy blokk cseréje
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

                    // nagyon világos = háttér → fehér
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
                string projectFolder = Path.GetFullPath(Path.Combine(exeFolder, @"..\..\.."));
                Directory.CreateDirectory(projectFolder);

                string mode = gameTimerManager.GetModeName();
                string difficulty = gameTimerManager.GetDifficultyName();
                string fileName = "nonogram_saves.json";
                string fullPath = Path.Combine(projectFolder, fileName);
                if (form.txtUsername != null)
                {
                    form.txtUsername.Enabled = false;
                    form.txtUsername.Text = "";
                }
                saveLoadManager.SaveGame(fullPath, form.username);

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
            form.chkExtraMode.Visible = true;
            form.chkBlackWhiteMode.Visible = true;
            form.chkColorMode.Visible = true;
            form.txtUsername.Enabled = true;
            form.txtUsername.Text = "";
            form.colorPalette.Visible = false;
            form.gameStarted = false;
            form.elapsedSeconds = 0;
            form.isXMode = true;
            form.isXMode = false;
            form.lblWrongCellClicks.Text = $"Helytelen kattintások száma: {form.wrongCellClicks} (max: 0)";
            form.lblWrongColorClicks.Text = $"Helytelen színek száma: {form.wrongColorClicks} (max: 0)";
            form.lblHintCount.Text = $"Segítségek száma: {form.hintCount} (max: 0)";
            form.lblUndoCount.Text = $"Visszavonások száma: {form.undoClicks} (max: 0)";
            form.lblRedoCount.Text = $"Előrelépések száma: {form.redoClicks} (max: 0)";
            return;
        }
    }
}