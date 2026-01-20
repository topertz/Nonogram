using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
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

        // Véletlen Nonogram generálása (beleértve clue-ok és színek)
        public void GenerateRandomNonogram(int gridLeft, int gridTop, int numRow = 10, int numCol = 10)
        {
            // Sorok és oszlopok száma a paraméterekből
            form.row = numRow;
            form.col = numCol;

            Random rnd = form.rnd;

            form.solutionBW = new int[form.row, form.col];
            form.solutionColorRGB = new Color[form.row, form.col];
            form.userXMark = new bool[form.row, form.col];
            form.userColorRGB = new Color[form.row, form.col];
            form.userColor = new int[form.row, form.col];

            for (int i = 0; i < form.row; i++)
            {
                for (int j = 0; j < form.col; j++)
                {
                    bool filled = rnd.NextDouble() < 0.5;
                    form.solutionBW[i, j] = filled ? 1 : 0;

                    if (form.isColor)
                    {
                        if (filled)
                        {
                            // Könnyű szinten kevesebb szín
                            if (form.cmbDifficulty.SelectedIndex == 0) // Könnyű
                            {
                                form.solutionColorRGB[i, j] =
                                    form.easyColors[rnd.Next(form.easyColors.Length)];
                            }
                            else
                            {
                                form.solutionColorRGB[i, j] =
                                    Color.FromArgb(rnd.Next(50, 256), rnd.Next(50, 256), rnd.Next(50, 256));
                            }
                        }
                        else
                        {
                            form.solutionColorRGB[i, j] = Color.White;
                        }
                    }
                    else
                        form.solutionColorRGB[i, j] = filled ? Color.Black : Color.White;

                    form.userXMark[i, j] = !filled;
                    form.userColorRGB[i, j] = Color.White;
                    form.userColor[i, j] = 0;
                }
            }

            // Clue-ok generálása
            GenerateClues();

            // Grid UI létrehozása a megadott pozíción
            CreateGridUI(gridLeft, gridTop);
        }

        private void GenerateClues()
        {
            form.rowClues = new int[form.row][];
            form.rowClueColors = new Color[form.row][];
            form.colClues = new int[form.col][];
            form.colClueColors = new Color[form.col][];

            // Sor clue-ok
            for (int i = 0; i < form.row; i++)
            {
                List<int> clues = new List<int>();
                List<Color> colors = new List<Color>();
                int j = 0;

                while (j < form.col)
                {
                    if (form.solutionBW[i, j] == 0) { j++; continue; }
                    int count = 0; int sumR = 0, sumG = 0, sumB = 0; int k = j;
                    while (k < form.col && form.solutionBW[i, k] == 1)
                    {
                        count++;
                        if (form.isColor) { Color c = form.solutionColorRGB[i, k]; sumR += c.R; sumG += c.G; sumB += c.B; }
                        k++;
                    }
                    clues.Add(count);
                    if (form.isColor)
                    {
                        if (form.cmbDifficulty.SelectedIndex == 0) // Könnyű
                        {
                            // pontosan a futam színe
                            colors.Add(form.solutionColorRGB[i, j]);
                        }
                        else
                        {
                            colors.Add(Color.FromArgb(sumR / count, sumG / count, sumB / count));
                        }
                    }
                    else
                    {
                        colors.Add(Color.Black);
                    }
                    j = k;
                }
                if (clues.Count == 0) { clues.Add(0); colors.Add(form.isColor ? Color.White : Color.Black); }
                form.rowClues[i] = clues.ToArray();
                form.rowClueColors[i] = colors.ToArray();
            }

            // Oszlop clue-ok
            for (int j = 0; j < form.col; j++)
            {
                List<int> clues = new List<int>();
                List<Color> colors = new List<Color>();
                int i = 0;
                while (i < form.row)
                {
                    if (form.solutionBW[i, j] == 0) { i++; continue; }
                    int count = 0; int sumR = 0, sumG = 0, sumB = 0; int k = i;
                    while (k < form.row && form.solutionBW[k, j] == 1)
                    {
                        count++;
                        if (form.isColor) { Color c = form.solutionColorRGB[k, j]; sumR += c.R; sumG += c.G; sumB += c.B; }
                        k++;
                    }
                    clues.Add(count);
                    if (form.isColor)
                    {
                        if (form.cmbDifficulty.SelectedIndex == 0) // Könnyű
                        {
                            colors.Add(form.solutionColorRGB[i, j]);
                        }
                        else
                        {
                            colors.Add(Color.FromArgb(sumR / count, sumG / count, sumB / count));
                        }
                    }
                    else
                    {
                        colors.Add(Color.Black);
                    }
                    i = k;
                }
                if (clues.Count == 0) { clues.Add(0); colors.Add(form.isColor ? Color.White : Color.Black); }
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
            int size = form.previewSize;
            int cellWidth = size / cols;
            int cellHeight = size / rows;

            Bitmap bmp = new Bitmap(cols * cellWidth, rows * cellHeight);

            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                for (int i = 0; i < rows; i++)
                {
                    for (int j = 0; j < cols; j++)
                    {
                        Color c = form.isColor ? form.solutionColorRGB[i, j] :
                                    (form.solutionBW[i, j] == 1 ? Color.Black : Color.White);
                        using (SolidBrush brush = new SolidBrush(c))
                            g.FillRectangle(brush, j * cellWidth, i * cellHeight, cellWidth, cellHeight);
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
            int maxColClues = MaxClueLength(form.colClues);
            int maxRowClues = MaxClueLength(form.rowClues);
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
                else
                {
                    btn.BackColor = Color.White;
                    form.userColorRGB[row, col] = Color.White;
                    form.userColor[row, col] = 0;
                }
            }

            if (IsSolved())
            {
                MessageBox.Show("Gratulálok, kész a Nonogram!");
                form.gameTimer.Stop();
                try
                {
                    SaveFileDialog sfd = new SaveFileDialog();
                    sfd.Filter = "Nonogram mentés (*.json)|*.json";
                    sfd.Title = "Játék automatikus mentése";
                    string mode = gameTimerManager.GetModeName();
                    string difficulty = gameTimerManager.GetDifficultyName();

                    sfd.FileName = $"nonogram_{mode}_{difficulty}.json";
                    TextBox txtUsername = form.Controls.Find("txtUsername", true).FirstOrDefault() as TextBox;
                    form.username = txtUsername?.Text.Trim();
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        form.saveLoadManager.SaveGame(sfd.FileName, form.username);
                        MessageBox.Show("A játékállás automatikusan elmentve!", "Mentés", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Hiba a játék mentése során:\n" + ex.Message, "Mentés hiba", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                form.gameTimerManager.RestartGameWithCurrentDifficulty();
                return;
            }
        }

        public void GenerateMediumNonogram()
        {
            int gridLeft = 20;
            int gridTop = Math.Max(form.chkGrayscale.Bottom, form.chkShowX.Bottom) + 20;

            int w = 20;
            int h = 20;

            Bitmap shape = GenerateMediumShape(w, h);

            // pixelperfect nagyítás
            Bitmap large = ScaleBitmap(shape, 8); // 8× nagyítás (200×200 px körül)

            form.picSolutionPreview.Image = large;
            form.picSolutionPreview.SizeMode = PictureBoxSizeMode.Zoom;

            GenerateNonogramFromImage(shape, gridLeft, gridTop);
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

        private Bitmap GenerateMediumShape(int w, int h)
        {
            bool isColorMode = (form.cmbMode.SelectedItem.ToString() == "Színes");
            Bitmap bmp = new Bitmap(w, h);

            // ha nincs színes mód, csak fekete
            Color[] colors = isColorMode ? form.mediumColors : new Color[] { Color.Black };

            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                Random rnd = new Random();

                int shape = rnd.Next(6); // 0=Kör, 1=Négyszög, 2=Háromszög, 3=Kereszt, 4=Gyémánt, 5=Csillag/Zaj

                switch (shape)
                {
                    case 0: // Kör
                        int layersCircle = 5;
                        for (int i = 0; i < layersCircle; i++)
                        {
                            Color drawColor = colors[i % colors.Length];
                            Pen p = new Pen(drawColor, 1);
                            float offset = i * (Math.Min(w, h) / 20f);
                            g.DrawEllipse(p, w / 4f + offset, h / 4f + offset, w / 2f - 2 * offset, h / 2f - 2 * offset);
                        }
                        break;

                    case 1: // Négyszög
                        int layersRect = 3;
                        for (int i = 0; i < layersRect; i++)
                        {
                            Color drawColor = colors[i % colors.Length];
                            Pen p = new Pen(drawColor, 1);
                            float offset = i * (Math.Min(w, h) / 20f);
                            g.DrawRectangle(p, w / 4f + offset, h / 4f + offset, w / 2f - 2 * offset, h / 2f - 2 * offset);
                        }
                        break;

                    case 2: // Háromszög
                        int layersTri = 3;
                        for (int i = 0; i < layersTri; i++)
                        {
                            Color drawColor = colors[i % colors.Length];
                            Pen p = new Pen(drawColor, 1);
                            float offset = i * (Math.Min(w, h) / 20f);

                            PointF[] tri =
                            {
                        new PointF(w / 2f, h / 6f + offset),
                        new PointF(w / 6f + offset, h * 5f / 6f - offset),
                        new PointF(w * 5f / 6f - offset, h * 5f / 6f - offset)
                    };
                            g.DrawPolygon(p, tri);
                        }
                        break;

                    case 3: // Kereszt
                        Color vertColor = colors[0];
                        Color horiColor = colors[1 % colors.Length];

                        float marginCross = w / 5f;
                        float centerX = w / 2f;
                        float centerY = h / 2f;

                        using (Pen vertPen = new Pen(vertColor, 1))
                        using (Pen horiPen = new Pen(horiColor, 1))
                        {
                            g.DrawLine(vertPen, centerX, marginCross, centerX, h - marginCross);
                            g.DrawLine(horiPen, marginCross, centerY, w - marginCross, centerY);
                        }
                        break;

                    case 4: // Gyémánt
                        for (int y = 0; y < h; y++)
                        {
                            int distFromCenter = Math.Abs(h / 2 - y - 1);
                            int minX = distFromCenter;
                            int maxX = w - 2 - distFromCenter;

                            if (y == h - 1)
                            {
                                minX = Math.Max(0, minX - 1);
                                maxX = Math.Min(w - 1, maxX + 1);
                            }

                            Color drawColor = colors[y % colors.Length];
                            for (int x = minX; x <= maxX; x++)
                                bmp.SetPixel(x, y, drawColor);
                        }

                        int extraY = h - 11;
                        int extraX = w - 1;
                        if (extraY >= 0 && extraX < w)
                            bmp.SetPixel(extraX, extraY, colors[extraY % colors.Length]);
                        break;

                    case 5: // Csillag / zaj
                        int cx = w / 2; // középpont x
                        int cy = h / 2; // középpont y

                        for (int i = 0; i < Math.Max(w, h); i++)
                        {
                            Color drawColor = colors[i % colors.Length];

                            // Függőleges és vízszintes vonalak középen
                            if (i < h) bmp.SetPixel(cx, i, drawColor);
                            if (i < w) bmp.SetPixel(i, cy, drawColor);

                            // Átlós vonalak a középponton keresztül
                            int left = cx - i;
                            int right = cx + i;
                            int top = cy - i;
                            int bottom = cy + i;

                            // bal-felső -> jobb-alsó átló
                            if (left >= 0 && top >= 0) bmp.SetPixel(left, top, drawColor);
                            if (right < w && bottom < h) bmp.SetPixel(right, bottom, drawColor);

                            // jobb-felső -> bal-alsó átló
                            if (right < w && top >= 0) bmp.SetPixel(right, top, drawColor);
                            if (left >= 0 && bottom < h) bmp.SetPixel(left, bottom, drawColor);
                        }

                        // Sarokpontok
                        Color cornerColor = colors[0];
                        bmp.SetPixel(0, 0, cornerColor);
                        bmp.SetPixel(w - 1, 0, cornerColor);
                        bmp.SetPixel(0, h - 1, cornerColor);
                        bmp.SetPixel(w - 1, h - 1, cornerColor);
                        break;
                }
            }
            return bmp;
        }


        public void GenerateNonogramFromImage(Image img, int gridLeft, int gridTop)
        {
            int gridWidth = 30;
            int gridHeight = 30;
            Bitmap bmp;

            if (!form.isColor)
            {
                bmp = new Bitmap(gridWidth, gridHeight, PixelFormat.Format24bppRgb);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                    g.DrawImage(img, new Rectangle(0, 0, gridWidth, gridHeight));
                }
            }
            else
            {
                bmp = new Bitmap(gridWidth, gridHeight);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                    g.DrawImage(img, new Rectangle(0, 0, gridWidth, gridHeight));
                }
            }

            form.row = gridHeight;
            form.col = gridWidth;

            form.userColorRGB = new Color[form.row, form.col];
            form.userColor = new int[form.row, form.col];
            form.userXMark = new bool[form.row, form.col];
            form.gridButtons = new Button[form.row, form.col];
            form.solutionColorRGB = new Color[form.row, form.col];
            form.solutionBW = new int[form.row, form.col];
            form.userXMark = new bool[form.row, form.col];
            form.isColor = false;

            bool[,] fillableCells = new bool[form.row, form.col];

            // helyettesíti a GetPixel-T
            Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            BitmapData bmpData = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            int bytes = Math.Abs(bmpData.Stride) * bmpData.Height;
            byte[] rgbValues = new byte[bytes];
            Marshal.Copy(bmpData.Scan0, rgbValues, 0, bytes);
            int bytesPerPixel = 3;

            // Most a pixeladatokat a memóriából olvassuk
            for (int i = 0; i < form.row; i++)
            {
                for (int j = 0; j < form.col; j++)
                {
                    int index = i * bmpData.Stride + j * bytesPerPixel;
                    byte b = rgbValues[index];
                    byte g = rgbValues[index + 1];
                    byte r = rgbValues[index + 2];
                    Color pixel = Color.FromArgb(r, g, b);

                    form.solutionColorRGB[i, j] = pixel;

                    if (!form.isColor)
                    {
                        double luminance = 0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B;
                        int lumInt = (int)luminance;
                        form.solutionBW[i, j] = (lumInt < 128) ? 1 : 0;

                        // Színellenőrzés
                        int maxDiff = Math.Max(Math.Abs(pixel.R - pixel.G),
                            Math.Max(Math.Abs(pixel.G - pixel.B), Math.Abs(pixel.R - pixel.B)));
                        if (maxDiff > 10) form.isColor = true;

                        fillableCells[i, j] = (lumInt < 128);
                    }
                    else
                    {
                        int gray = (pixel.R + pixel.G + pixel.B) / 3;
                        form.solutionBW[i, j] = (gray < 128) ? 1 : 0;

                        if (!form.isColor && (pixel.R != pixel.G || pixel.G != pixel.B))
                            form.isColor = true;

                        fillableCells[i, j] = form.isColor ? (pixel.R + pixel.G + pixel.B < 700)
                                                      : (form.solutionBW[i, j] == 1);
                    }
                }
            }

            bmp.UnlockBits(bmpData);

            // Szín-specifikus clue-ok számítása (sorok)
            form.rowClues = new int[form.row][];
            form.rowClueColors = new Color[form.row][];
            for (int i = 0; i < form.row; i++)
            {
                List<int> clues = new List<int>();
                List<Color> clueColors = new List<Color>();

                int j = 0;
                while (j < form.col)
                {
                    if (!fillableCells[i, j]) { j++; continue; }

                    int sumR = 0, sumG = 0, sumB = 0, count = 0;
                    Color seed = form.solutionColorRGB[i, j];
                    int k = j;
                    while (k < form.col && fillableCells[i, k])
                    {
                        Color c = form.solutionColorRGB[i, k];

                        if (form.isColor && count > 0 && !AreColorsSimilar(seed, c, form.colorSimilarityThreshold))
                            break; // új futam (másik szín)

                        // hozzáadjuk a pixelt a jelenlegi futamhoz
                        sumR += c.R; sumG += c.G; sumB += c.B; count++;

                        if (form.isColor && count == 1)
                            seed = c; // első pixel színe a seed

                        k++;
                    }

                    if (count > 0)
                    {
                        clues.Add(count);
                        Color avg = Color.FromArgb(sumR / count, sumG / count, sumB / count);
                        clueColors.Add(form.isColor ? avg : Color.Black);
                    }

                    j = k;
                }

                if (clues.Count == 0)
                {
                    form.rowClues[i] = new int[] { 0 };
                    form.rowClueColors[i] = new Color[] { form.isColor ? Color.White : Color.Black };
                }
                else
                {
                    form.rowClues[i] = clues.ToArray();
                    form.rowClueColors[i] = clueColors.ToArray();
                }
            }

            // Szín-specifikus clue-ok számítása (oszlopok)
            form.colClues = new int[form.col][];
            form.colClueColors = new Color[form.col][];
            for (int j = 0; j < form.col; j++)
            {
                List<int> clues = new List<int>();
                List<Color> clueColors = new List<Color>();

                int i = 0;
                while (i < form.row)
                {
                    if (!fillableCells[i, j]) { i++; continue; }

                    int sumR = 0, sumG = 0, sumB = 0, count = 0;
                    Color seed = form.solutionColorRGB[i, j];
                    int k = i;
                    while (k < form.row && fillableCells[k, j])
                    {
                        Color c = form.solutionColorRGB[k, j];

                        if (form.isColor && count > 0 && !AreColorsSimilar(seed, c, form.colorSimilarityThreshold))
                            break;

                        sumR += c.R; sumG += c.G; sumB += c.B; count++;

                        if (form.isColor && count == 1)
                            seed = c;

                        k++;
                    }

                    if (count > 0)
                    {
                        clues.Add(count);
                        Color avg = Color.FromArgb(sumR / count, sumG / count, sumB / count);
                        clueColors.Add(form.isColor ? avg : Color.Black);
                    }

                    i = k;
                }

                if (clues.Count == 0)
                {
                    form.colClues[j] = new int[] { 0 };
                    form.colClueColors[j] = new Color[] { form.isColor ? Color.White : Color.Black };
                }
                else
                {
                    form.colClues[j] = clues.ToArray();
                    form.colClueColors[j] = clueColors.ToArray();
                }
            }

            // X-ek beállítása csak a nem-kitöltendő cellákra
            for (int i = 0; i < form.row; i++)
                for (int j = 0; j < form.col; j++)
                    form.userXMark[i, j] = !fillableCells[i, j];
            // felhasználói állapot inicializálása
            for (int i = 0; i < form.row; i++)
            {
                for (int j = 0; j < form.col; j++)
                {
                    form.userColorRGB[i, j] = Color.White;
                    form.userColor[i, j] = 0;
                }
            }
            CreateGridUI(gridLeft, gridTop);
            if (form.isColor)
                GenerateColorPalette(bmp); // csak színes képnél
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