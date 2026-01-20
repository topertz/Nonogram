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
            form.chkShowX.Enabled = true;
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
                form.btnSelectImage.Visible = false;
                form.btnSolve.Enabled = true;
                form.btnHint.Enabled = true;
                form.btnCheck.Enabled = true;
                form.btnSave.Enabled = true;
                form.btnLoad.Enabled = true;
                form.btnRedo.Enabled = true;
                form.btnUndo.Enabled = true;
                form.btnSaveGame.Enabled = true;
                form.btnLoadGame.Enabled = true;
                form.chkShowX.Enabled = true;
                form.chkShowX.Visible = true;
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
                form.btnSelectImage.Visible = false;
                form.btnSolve.Enabled = true;
                form.btnHint.Enabled = true;
                form.btnCheck.Enabled = true;
                form.btnSave.Enabled = true;
                form.btnLoad.Enabled = true;
                form.btnRedo.Enabled = true;
                form.btnUndo.Enabled = true;
                form.btnSaveGame.Enabled = true;
                form.btnLoadGame.Enabled = true;
                form.chkShowX.Enabled = true;
                form.chkShowX.Visible = true;
                cmbMode.Visible = true;
                grid.GenerateMediumNonogram();
                // Időzítő újraindítása
                Start();
            }
            else if (cmbDifficulty.SelectedIndex == 2)
            {
                form.btnSolve.Enabled = false;
                form.btnHint.Enabled = false;
                form.btnCheck.Enabled = false;
                form.btnSave.Enabled = false;
                form.btnLoad.Enabled = false;
                form.btnRedo.Enabled = false;
                form.btnUndo.Enabled = false;
                form.btnSaveGame.Enabled = false;
                form.btnLoadGame.Enabled = false;
                form.chkShowX.Enabled = false;
                form.chkGrayscale.Visible = true;
                form.chkShowX.Visible = true;
                cmbMode.Visible = false;
                form.lblTimer.Visible = false;

                form.btnSelectImage.Visible = true;

                form.gameTimer.Stop();
                form.picSolutionPreview.Image = null;
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
            form.chkShowX.Enabled = true;

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

                case 2:
                    form.btnSelectImage.Visible = true;
                    form.picSolutionPreview.Image = null;
                    form.gameTimer.Stop();
                    break;
            }

            renderer.UpdatePreview();
        }

        public string GetDifficultyName()
        {
            switch (form.cmbDifficulty.SelectedIndex)
            {
                case 0: return "konnyu";
                case 1: return "kozepes";
                case 2: return "nehez";
                default: return "ismeretlen";
            }
        }

        public string GetModeName()
        {
            return form.isColor ? "szines" : "fekete-feher";
        }
    }
}