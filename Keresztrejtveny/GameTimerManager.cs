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
        private ExtraGridManager extraGridManager;

        public GameTimerManager(Nonogram f, NonogramGrid g, NonogramRenderer r, UndoRedoManager u, ExtraGridManager e)
        {
            form = f;
            grid = g;
            renderer = r;
            undoredoManager = u;
            extraGridManager = e;

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
            if (form.isColor) // Színes mód
            {
                switch (form.cmbDifficulty.SelectedIndex)
                {
                    case 0: form.remainingSeconds = 30 * 60; break;   // Könnyű
                    case 1: form.remainingSeconds = 45 * 60; break;   // Nehéz
                    default: form.remainingSeconds = 30 * 60; break;
                }
            }
            else // Fekete-fehér mód
            {
                switch (form.cmbDifficulty.SelectedIndex)
                {
                    case 0: form.remainingSeconds = 15 * 60; break;   // Könnyű
                    case 1: form.remainingSeconds = 30 * 60; break;   // Nehéz
                    default: form.remainingSeconds = 15 * 60; break;
                }
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

            ResetCellCliks();
            ResetColorClicks();
            ResetHintClicks();

            // Grid törlése
            grid.ClearGrid();
            form.gameTimer.Stop();

            // Színes vagy fekete-fehér mód beállítása
            form.isColor = cmbMode.SelectedItem?.ToString() == "Színes";
            form.lblWrongColorClicks.Visible = form.isColor;

            switch (cmbDifficulty.SelectedIndex)
            {
                case 0: // Könnyű
                    form.chkShowX.Visible = true;
                    form.btnSelectImage.Visible = false;
                    form.btnSolve.Enabled = true;
                    form.btnHint.Enabled = true;
                    form.btnCheck.Enabled = true;
                    form.btnRedo.Enabled = true;
                    form.btnUndo.Enabled = true;
                    form.chkShowX.Enabled = true;

                    grid.GenerateRandomNonogram(20, 150);

                    form.lblTimer.Visible = true;
                    form.picPreview.Visible = true;
                    form.picSolutionPreview.Visible = true;
                    // Timer újraindítása
                    Start();

                    if (picSolutionPreview != null)
                    {
                        picSolutionPreview.Image = grid.GeneratePreviewImage();
                        picSolutionPreview.SizeMode = PictureBoxSizeMode.Zoom;
                    }
                    break;

                case 1: // Nehéz
                    form.chkShowX.Visible = true;
                    form.btnSelectImage.Visible = false;
                    form.btnSolve.Enabled = true;
                    form.btnHint.Enabled = true;
                    form.btnCheck.Enabled = true;
                    form.btnRedo.Enabled = true;
                    form.btnUndo.Enabled = true;
                    form.chkShowX.Enabled = true;

                    grid.GenerateHardNonogram();

                    form.lblTimer.Visible = true;
                    form.picPreview.Visible = true;
                    form.picSolutionPreview.Visible = true;
                    // Timer újraindítása
                    Start();
                    break;

                default: // alapértelmezett
                    cmbDifficulty.SelectedIndex = 0;
                    grid.GenerateRandomNonogram(20, 150);
                    Start();
                    break;
            }

            SetMaxWrongClicksByDifficulty();
            renderer.UpdatePreview();
        }

        public void ResetCellCliks()
        {
            form.wrongCellClicks = 0;
            form.lblWrongCellClicks.Text = $"Helytelen kattintások száma: 0 (max: {form.maxWrongCellClicks})";
        }

        public void ResetColorClicks()
        {
            form.wrongColorClicks = 0;
            form.lblWrongColorClicks.Text = $"Helytelen színek száma: 0 (max: {form.maxWrongColorClicks})";
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
            ResetCellCliks();
            ResetColorClicks();
            form.wrongCellClicks = 0;
            form.lblWrongCellClicks.Text = $"Helytelen kattintások száma: {form.wrongCellClicks} (max: {form.maxWrongCellClicks})";
            form.wrongColorClicks = 0;
            form.lblWrongCellClicks.Text = $"Helytelen színek száma: {form.wrongColorClicks} (max: {form.maxWrongColorClicks})";
            form.hintCount = 0;
            form.lblHintCount.Text = "Segítségek száma: 0";
            form.picPreview.Image = null;
            form.username = "";
            TextBox txtUsername = form.Controls.Find("txtUsername", true).FirstOrDefault() as TextBox;
            /*if (txtUsername != null)
                txtUsername.Text = "";*/
            txtUsername.Enabled = false;
            form.btnCheck.Enabled = true;
            form.chkShowX.Enabled = true;
            form.btnSolve.Enabled = true;

            switch (form.cmbDifficulty.SelectedIndex)
            {
                case 0: // Könnyű
                    grid.GenerateRandomNonogram(20, 150);
                    if (form.picSolutionPreview != null)
                    {
                        form.picSolutionPreview.Image = grid.GeneratePreviewImage(); // friss kép
                        form.picSolutionPreview.SizeMode = PictureBoxSizeMode.Zoom;
                    }
                    // Időzítő újraindítása
                    Start();
                    break;

                case 1: // Nehéz
                    grid.GenerateHardNonogram();
                    if (form.picSolutionPreview != null)
                    {
                        form.picSolutionPreview.Image = grid.GeneratePreviewImage(); // friss kép
                        form.picSolutionPreview.SizeMode = PictureBoxSizeMode.Zoom;
                    }
                    // Időzítő újraindítása
                    Start();
                    break;
            }

            //renderer.UpdatePreview();
        }

        public string GetDifficultyName()
        {
            switch (form.cmbDifficulty.SelectedIndex)
            {
                case 0: return "konnyu";
                case 1: return "nehez";
                default: return "ismeretlen";
            }
        }

        public string GetModeName()
        {
            return form.isColor ? "szines" : "fekete-feher";
        }

        public void SetMaxWrongClicksByDifficulty()
        {
            if (form.isColor) // Színes mód
            {
                switch (form.cmbDifficulty.SelectedIndex)
                {
                    case 0: // Könnyű
                        form.maxWrongCellClicks = 30;
                        form.maxWrongColorClicks = 30;
                        break;
                    case 1: // Nehéz
                        form.maxWrongCellClicks = 45;
                        form.maxWrongColorClicks = 45;
                        break;
                    default:
                        form.maxWrongCellClicks = 30;
                        form.maxWrongColorClicks = 30;
                        break;
                }
            }
            else // Fekete-fehér mód
            {
                switch (form.cmbDifficulty.SelectedIndex)
                {
                    case 0: // Könnyű
                        form.maxWrongCellClicks = 15;
                        form.maxWrongColorClicks = 15;
                        break;
                    case 1: // Nehéz
                        form.maxWrongCellClicks = 30;
                        form.maxWrongColorClicks = 30;
                        break;

                    default:
                        form.maxWrongCellClicks = 15;
                        form.maxWrongColorClicks = 15;
                        break;
                }
            }

            // Frissítjük a label szöveget
            form.lblWrongCellClicks.Text = $"Helytelen kattintások száma: {form.wrongCellClicks} (max: {form.maxWrongCellClicks})";
            form.lblWrongColorClicks.Text = $"Helytelen színek száma: {form.wrongColorClicks} (max: {form.maxWrongColorClicks})";
        }
    }
}