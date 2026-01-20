using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Nonogram
{
    public class NonogramRenderer
    {
        private Nonogram form;
        private NonogramGrid grid;

        public NonogramRenderer(Nonogram f, NonogramGrid g)
        {
            form = f;
            grid = g;
        }

        // Preview frissítése
        public void UpdatePreview()
        {
            int rows = form.row;
            int cols = form.col;
            int cellSize = form.userCellSize;

            Bitmap bmp = new Bitmap(cols * cellSize, rows * cellSize);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                for (int i = 0; i < rows; i++)
                {
                    for (int j = 0; j < cols; j++)
                    {
                        Color c = form.userColorRGB[i, j];
                        using (SolidBrush brush = new SolidBrush(c))
                        {
                            g.FillRectangle(brush, j * cellSize, i * cellSize, cellSize, cellSize);
                        }
                    }
                }
            }

            Bitmap scaled = new Bitmap(form.previewSize, form.previewSize);
            using (Graphics g = Graphics.FromImage(scaled))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.DrawImage(bmp, 0, 0, form.previewSize, form.previewSize);
            }

            form.picPreview.Image = scaled;
            form.picPreview.SizeMode = PictureBoxSizeMode.Normal;
        }

        // Megoldás animálva
        public void SolveNonogram()
        {
            form.gameTimer.Start();
            ClearErrorHighlights();
            List<Point> wrongCells = new List<Point>();

            for (int i = 0; i < form.row; i++)
            {
                for (int j = 0; j < form.col; j++)
                {
                    if (form.userXMark[i, j]) continue;

                    bool wrong = form.isColor
                        ? form.userColorRGB[i, j].ToArgb() != form.solutionColorRGB[i, j].ToArgb()
                        : (form.userColorRGB[i, j].ToArgb() == Color.Black.ToArgb() ? 1 : 0) != form.solutionBW[i, j];

                    if (wrong)
                        wrongCells.Add(new Point(i, j));
                }
            }

            if (wrongCells.Count == 0)
            {
                MessageBox.Show("Már minden cella a helyén van!", "Megoldás");
                return;
            }

            form.solutionQueue = new Queue<Point>(wrongCells);
            SetGridEnabled(false);

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
                SetGridEnabled(true);
                form.gameTimerManager?.Stop();
                if(MessageBox.Show("A nonogram teljesen kirakva!", "Megoldás kész") == DialogResult.OK)
                {
                    form.gameTimerManager.RestartGameWithCurrentDifficulty();
                }
                return;
            }

            Point p = form.solutionQueue.Dequeue();
            int i = p.X;
            int j = p.Y;

            if (form.isColor)
            {
                form.userColorRGB[i, j] = form.solutionColorRGB[i, j];
                form.gridButtons[i, j].BackColor = form.solutionColorRGB[i, j];
            }
            else
            {
                form.userColorRGB[i, j] = form.solutionBW[i, j] == 1 ? Color.Black : Color.White;
                form.gridButtons[i, j].BackColor = form.userColorRGB[i, j];
            }

            UpdatePreview();
        }

        public void ShowHint()
        {
            form.gameTimer.Start();
            ClearErrorHighlights();
            List<Point> wrongCells = new List<Point>();

            for (int i = 0; i < form.row; i++)
            {
                for (int j = 0; j < form.col; j++)
                {
                    if (form.userXMark[i, j]) continue;

                    bool wrong = form.isColor
                        ? form.userColorRGB[i, j].ToArgb() != form.solutionColorRGB[i, j].ToArgb()
                        : (form.userColorRGB[i, j].ToArgb() == Color.Black.ToArgb() ? 1 : 0) != form.solutionBW[i, j];

                    if (wrong)
                        wrongCells.Add(new Point(i, j));
                }
            }

            if (wrongCells.Count == 0)
            {
                form.gameTimerManager?.Stop();
                if(MessageBox.Show("Már minden a helyén van — nincs több segítség!", "Segítség") == DialogResult.OK)
                {
                    form.gameTimerManager.RestartGameWithCurrentDifficulty();
                }
                return;
            }

            Random rnd = new Random();
            Point hintCell = wrongCells[rnd.Next(wrongCells.Count)];
            int iHint = hintCell.X;
            int jHint = hintCell.Y;

            if (form.isColor)
            {
                form.userColorRGB[iHint, jHint] = form.solutionColorRGB[iHint, jHint];
                form.gridButtons[iHint, jHint].BackColor = form.solutionColorRGB[iHint, jHint];
            }
            else
            {
                form.userColorRGB[iHint, jHint] = form.solutionBW[iHint, jHint] == 1 ? Color.Black : Color.White;
                form.gridButtons[iHint, jHint].BackColor = form.userColorRGB[iHint, jHint];
            }
            form.userXMark[iHint, jHint] = true;
            form.gridButtons[iHint, jHint].Enabled = false;
            UpdatePreview();
            form.hintCount++;
            form.lblHintCount.Text = $"Segítségek száma: {form.hintCount}";
        }

        /*public void CheckSolution()
        {
            bool correct = true;

            for (int i = 0; i < form.row && correct; i++)
            {
                for (int j = 0; j < form.col; j++)
                {
                    if (form.isColor)
                    {
                        if (form.userColorRGB[i, j].ToArgb() != form.solutionColorRGB[i, j].ToArgb())
                            correct = false;
                    }
                    else
                    {
                        int userBW = form.userColorRGB[i, j].ToArgb() == Color.Black.ToArgb() ? 1 : 0;
                        if (userBW != form.solutionBW[i, j])
                            correct = false;
                    }
                }
            }

            if (correct)
                MessageBox.Show("Helyes megoldás!", "Ellenőrzés");
            else
                MessageBox.Show("Még nem teljesen jó.", "Ellenőrzés");
        }*/

        public void CheckSolution()
        {
            bool correct = true;

            for (int i = 0; i < form.row; i++)
            {
                for (int j = 0; j < form.col; j++)
                {
                    bool wrong;

                    if (form.isColor)
                    {
                        Color userColor = form.userColorRGB[i, j];
                        if (userColor.IsEmpty)
                            userColor = Color.White;
                        wrong = !grid.AreColorsSimilar(userColor, form.solutionColorRGB[i, j], 40);
                    }
                    else
                    {
                        int userBW = form.userColorRGB[i, j].ToArgb() == Color.Black.ToArgb() ? 1 : 0;
                        wrong = userBW != form.solutionBW[i, j];
                    }

                    if (wrong)
                    {
                        correct = false;

                        // PIROS HÁTTÉR HIBÁS MEZŐNÉL
                        form.gridButtons[i, j].BackColor = Color.DarkRed;
                    }
                    else
                    {
                        // HELYES MEZŐ → visszaállítjuk a felhasználó színét
                        form.gridButtons[i, j].BackColor = form.userColorRGB[i, j];
                    }
                }
            }
            form.gameTimer.Stop();
            MessageBox.Show(
                "Vannak hibás mezők! A pirossal jelöltek nincsenek jól megoldva.",
                "Ellenőrzés",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
                );
            if (correct)
            {
                if (MessageBox.Show("Helyes megoldás!", "Ellenőrzés") == DialogResult.OK)
                {
                    form.gameTimerManager.RestartGameWithCurrentDifficulty();
                }
                else
                {
                    return;
                }
            }
        }

        public void ToggleXMarks(bool show)
        {
            for (int i = 0; i < form.gridButtons.GetLength(0); i++)
            {
                for (int j = 0; j < form.gridButtons.GetLength(1); j++)
                {
                    Button btn = form.gridButtons[i, j];

                    if (form.userXMark[i, j])
                    {
                        btn.Text = show ? "X" : "";

                        if (show)
                        {
                            // A cella méretéhez igazítjuk a betűméretet
                            float fontSize = btn.Height * 0.3f; // kb 30% a cella magasságának
                            btn.Font = new Font("Arial", fontSize, FontStyle.Bold);
                            btn.ForeColor = Color.Gray; // tetszőleges szín
                            btn.TextAlign = ContentAlignment.MiddleCenter;
                        }
                    }
                }
            }
        }

        private void SetGridEnabled(bool enabled)
        {
            for (int i = 0; i < form.gridButtons.GetLength(0); i++)
            {
                for (int j = 0; j < form.gridButtons.GetLength(1); j++)
                {
                    if (!form.userXMark[i, j])
                        form.gridButtons[i, j].Enabled = enabled;
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

                    if (btn.BackColor == Color.DarkRed)
                    {
                        // visszaállítás a logikai állapot alapján
                        if (form.isColor)
                        {
                            Color c = form.userColorRGB[i, j];
                            btn.BackColor = c.IsEmpty ? Color.White : c;
                        }
                        else
                        {
                            btn.BackColor = form.userColor[i, j] == 1
                                ? Color.Black
                                : Color.White;
                        }
                    }
                }
            }
        }
    }
}