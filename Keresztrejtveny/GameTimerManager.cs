using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Nonogram
{
    public class GameTimerManager
    {
        private Nonogram form;
        private NonogramGrid grid;
        private NonogramRenderer renderer;
        private UndoRedoManager undoredoManager;

        public GameTimerManager(Nonogram f, NonogramGrid g, NonogramRenderer r, UndoRedoManager u)
        {
            form = f;
            grid = g;
            renderer = r;
            undoredoManager = u;

            // A Timer a Form1-ből jön
            form.gameTimer = new Timer();
            form.gameTimer.Interval = 1000;
            form.gameTimer.Tick += Timer_Tick;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (form.remainingSeconds <= 0)
            {
                form.gameTimer.Stop();

                MessageBox.Show(
                    "Lejárt az idő! A játék újraindul.",
                    "Idő lejárt",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );

                RestartGameWithCurrentDifficulty();
                return;
            }

            form.remainingSeconds--;
            form.elapsedSeconds++;
            UpdateLabel();
        }

        public void Start()
        {
            SetTimeByDifficulty();
            form.gameTimer.Start();
        }

        public void Stop()
        {
            form.gameTimer.Stop();
        }

        private void UpdateLabel()
        {
            int minutes = form.remainingSeconds / 60;
            int seconds = form.remainingSeconds % 60;
            form.lblTimer.Text = $"{minutes:D2}:{seconds:D2}";
        }

        private void SetTimeByDifficulty()
        {
            switch (form.cmbDifficulty.SelectedIndex)
            {
                case 0: // Könnyű
                    form.remainingSeconds = 2 * 60;
                    break;

                case 1: // Közepes
                    form.remainingSeconds = 5 * 60;
                    break;

                case 2: // Nehéz
                    form.remainingSeconds = 10 * 60;
                    break;
            }

            UpdateLabel();
        }

        public void DifficultyOrModeChanged()
        {
            form.chkShowX.Checked = false;
            form.btnCheck.Enabled = true;
            NonogramGrid grid = form.grid;
            NonogramRenderer renderer = form.renderer;
            ComboBox cmbMode = form.cmbMode;
            ComboBox cmbDifficulty = form.cmbDifficulty;
            PictureBox picSolutionPreview = form.picSolutionPreview;
            Button btnGenerateRandom = form.btnGenerateRandom;

            if (grid == null) return;

            ResetWrongClicks();
            ResetHintClicks();

            // Grid törlése
            grid.ClearGrid();
            form.gameTimer.Stop();

            // Színes vagy fekete-fehér mód beállítása
            form.isColor = cmbMode.SelectedItem?.ToString() == "Színes";

            if (cmbDifficulty.SelectedIndex == 0)
            {
                form.chkShowX.Visible = true;
                form.chkGrayscale.Visible = false;
                cmbMode.Visible = true;
                grid.GenerateRandomNonogram(20, 150, 10, 10);
                // Időzítő újraindítása
                Start();

                if (picSolutionPreview != null)
                {
                    picSolutionPreview.Image = grid.GeneratePreviewImage();
                    picSolutionPreview.SizeMode = PictureBoxSizeMode.Zoom;
                }
            }
            else if (cmbDifficulty.SelectedIndex == 1)
            {
                form.chkShowX.Visible = true;
                form.chkGrayscale.Visible = false;
                cmbMode.Visible = true;
                grid.GenerateMediumNonogram();
                // Időzítő újraindítása
                Start();
            }
            else if (cmbDifficulty.SelectedIndex == 2)
            {
                form.chkGrayscale.Visible = true;
                form.chkShowX.Visible = true;
                cmbMode.Visible = false;
                form.lblTimer.Visible = false;
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Filter = "Képfájlok|*.png;*.jpg;*.jpeg;*.bmp";
                    ofd.Title = "Kép betöltése Nonogramhoz";

                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        // Betöltés
                        Image loadedImg = Image.FromFile(ofd.FileName);

                        // Fekete-fehérre alakítás, ha checkbox be van jelölve
                        Bitmap bmpToUse = (form.chkGrayscale.Checked)
                            ? renderer.ConvertToBlackAndWhite(new Bitmap(loadedImg))
                            : new Bitmap(loadedImg);

                        // A feldolgozott képet használjuk mindenhol
                        form.img = bmpToUse;

                        // Előnézet
                        picSolutionPreview.Image = bmpToUse;
                        picSolutionPreview.SizeMode = PictureBoxSizeMode.Zoom;

                        int gridLeft = 20;
                        int gridTop = Math.Max(form.chkGrayscale.Bottom, form.chkShowX.Bottom) + 20;

                        // A Nonogram logikájának feldolgozása
                        grid.GenerateNonogramFromImage(bmpToUse, gridLeft, gridTop);
                        form.lblTimer.Visible = true;
                        // Időzítő újraindítása
                        Start();
                    }
                }
            }
            else
            {
                cmbDifficulty.SelectedIndex = 0;
                grid.GenerateRandomNonogram(20, 150, 10, 10);
            }

            renderer.UpdatePreview();
        }

        public void ResetWrongClicks()
        {
            form.wrongClicks = 0;
            form.lblWrongClicks.Text = "Helytelen kattintások száma: 0 (max: 5)";
        }

        public void ResetHintClicks()
        {
            form.hintCount = 0;
            form.lblHintCount.Text = "Segítségek száma: 0";
        }

        public void StartTimer()
        {
            form.gameTimerManager.Start();
        }

        public void RestartGameWithCurrentDifficulty()
        {
            grid.ClearGrid();
            undoredoManager.ClearHistory();
            ResetWrongClicks();
            form.wrongClicks = 0;
            form.lblWrongClicks.Text = $"Helytelen kattintások száma: {form.wrongClicks} (max: 5)";
            form.hintCount = 0;
            form.lblHintCount.Text = "Segítségek száma: 0";
            form.picPreview.Image = null;
            form.username = "";
            TextBox txtUsername = form.Controls.Find("txtUsername", true).FirstOrDefault() as TextBox;
            if (txtUsername != null)
                txtUsername.Text = "";
            txtUsername.Enabled = true;
            form.btnCheck.Enabled = true;

            switch (form.cmbDifficulty.SelectedIndex)
            {
                case 0: // Könnyű
                    grid.GenerateRandomNonogram(20, 150, 10, 10);
                    if (form.picSolutionPreview != null)
                    {
                        form.picSolutionPreview.Image = grid.GeneratePreviewImage(); // friss kép
                        form.picSolutionPreview.SizeMode = PictureBoxSizeMode.Zoom;
                    }
                    // Időzítő újraindítása
                    Start();
                    break;

                case 1: // Közepes
                    grid.GenerateMediumNonogram();
                    if (form.picSolutionPreview != null)
                    {
                        form.picSolutionPreview.Image = grid.GeneratePreviewImage(); // friss kép
                        form.picSolutionPreview.SizeMode = PictureBoxSizeMode.Zoom;
                    }
                    // Időzítő újraindítása
                    Start();
                    break;

                case 2: // Nehéz
                    OpenFileDialog ofd = new OpenFileDialog();
                    ofd.Filter = "Képfájlok|*.png;*.jpg;*.jpeg;*.bmp";
                    ofd.Title = "Kép betöltése Nonogramhoz";

                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        Image loadedImg = Image.FromFile(ofd.FileName);
                        Bitmap bmpToUse = form.chkGrayscale.Checked
                            ? renderer.ConvertToBlackAndWhite(new Bitmap(loadedImg))
                            : new Bitmap(loadedImg);

                        form.img = bmpToUse;
                        form.picSolutionPreview.Image = bmpToUse;
                        form.picSolutionPreview.SizeMode = PictureBoxSizeMode.Zoom;

                        int gridLeft = 20;
                        int gridTop = Math.Max(form.chkGrayscale.Bottom, form.chkShowX.Bottom) + 20;
                        grid.GenerateNonogramFromImage(bmpToUse, gridLeft, gridTop);
                        // Időzítő újraindítása
                        Start();
                    }
                    break;
            }

            renderer.UpdatePreview();
        }
    }
}