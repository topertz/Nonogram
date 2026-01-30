using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Nonogram
{
    public class NonogramRenderer
    {
        private Nonogram form;
        private NonogramGrid grid;
        private GameTimerManager gameTimerManager;

        public NonogramRenderer(Nonogram f, NonogramGrid g, GameTimerManager game)
        {
            form = f;
            grid = g;
            this.gameTimerManager = game;
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
            form.btnSolve.Enabled = false;
            form.gameTimer.Stop();
            ClearErrorHighlights();
            List<Point> wrongCells = new List<Point>();

            for (int i = 0; i < form.row; i++)
            {
                for (int j = 0; j < form.col; j++)
                {
                    form.gridButtons[i, j].Enabled = false;
                    // ha a megoldás fehér -> Azonnal takarítunk és ugrunk
                    if (form.solutionColorRGB[i, j].ToArgb() == Color.White.ToArgb())
                    {
                        form.userColorRGB[i, j] = Color.White;
                        form.gridButtons[i, j].BackColor = Color.White;
                        form.userXMark[i, j] = false;
                        continue; // Nem kerül be a listába, nem lassítja a Timer-t
                    }

                    // ha színes, de már jó -> Csak ugrunk
                    if (form.userColorRGB[i, j].ToArgb() == form.solutionColorRGB[i, j].ToArgb())
                    {
                        continue;
                    }

                    // csak a rossz színes cellák maradnak itt
                    wrongCells.Add(new Point(i, j));
                }
            }

            if (wrongCells.Count == 0)
            {
                MessageBox.Show("Már minden színes cella a helyén van!", "Megoldás");
                return;
            }

            form.solutionQueue = new Queue<Point>(wrongCells);

            // Timer beállítása - mehet bátran gyorsabban, pl. 30-50 ms
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
                SetGridEnabled(true, form.chkColorHardMode.Checked, form.chkBlackWhiteHardMode.Checked);
                form.gameTimerManager?.Stop();
                if(MessageBox.Show("A nonogram teljesen kirakva!", "Megoldás kész") == DialogResult.OK)
                {
                    if(form.chkBlackWhiteHardMode.Checked || form.chkColorHardMode.Checked)
                    {
                        return;
                    } 
                    else
                    {
                        form.gameTimerManager.RestartGameWithCurrentDifficulty();
                    }
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
                // Ez az ág akkor fut le, ha már alapból minden jó volt
                MessageBox.Show("Már minden a helyén van!");
                return;
            }

            // Segítség adása
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

            // Ha a segítség kijavította az utolsó hibát is
            if (grid.IsSolved())
            {
                FinalizeGame();
            }
        }

        public void CheckSolution()
        {
            form.gameTimer.Stop();
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
                        form.gridButtons[i, j].BackColor = Color.DarkRed;
                    }
                    else
                    {
                        form.gridButtons[i, j].BackColor = form.userColorRGB[i, j];
                    }
                }
            }

            // Csak akkor állítjuk meg az órát, ha minden kész, 
            // vagy ha meg akarjuk szakítani a játékot ellenőrzésnél.

            if (correct)
            {
                form.gameTimer.Stop(); // Megállítjuk az időt a győzelemnél
                if (MessageBox.Show("Gratulálok, helyes megoldás!", "Ellenőrzés", MessageBoxButtons.OK) == DialogResult.OK)
                {
                    form.gameTimerManager.RestartGameWithCurrentDifficulty();
                }
            }
            else
            {
                // Csak akkor írjuk ki a hibát, ha a 'correct' értéke false maradt
                MessageBox.Show(
                    "Vannak hibás mezők! A pirossal jelöltek nincsenek jól megoldva.",
                    "Ellenőrzés",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
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

        public void SetGridEnabled(bool enabled, bool isColorHardMode, bool isBlackAndWhiteHardMode)
        {
            for (int i = 0; i < form.gridButtons.GetLength(0); i++)
            {
                for (int j = 0; j < form.gridButtons.GetLength(1); j++)
                {
                    if (isColorHardMode || isBlackAndWhiteHardMode)
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
            form.gameTimer.Stop();
            MessageBox.Show("Gratulálok, kész a Nonogram!");

            try
            {
                string exeFolder = AppDomain.CurrentDomain.BaseDirectory;
                string projectFolder = Path.GetFullPath(Path.Combine(exeFolder, @"..\..\.."));
                Directory.CreateDirectory(projectFolder);

                string mode = gameTimerManager.GetModeName();
                string difficulty = gameTimerManager.GetDifficultyName();
                string fileName = $"nonogram_{mode}_{difficulty}.json";
                string fullPath = Path.Combine(projectFolder, fileName);

                TextBox txtUsername = form.Controls.Find("txtUsername", true).FirstOrDefault() as TextBox;
                form.username = txtUsername?.Text.Trim();

                form.saveLoadManager.SaveGame(fullPath, form.username);

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
            form.txtUsername.Enabled = true;
            form.txtUsername.Text = "";
            form.colorPalette.Visible = false;
            form.gameStarted = false;
            form.elapsedSeconds = 0;
            return;
        }
    }
}