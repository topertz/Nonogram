using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Grafilogika_alkalmazas_keszitese
{
    public class GameTimerManager
    {
        private Nonogram form;
        private NonogramGrid grid;
        private NonogramRender render;
        private UndoRedoManager undoredoManager;
        private ExtraGridManager extraGridManager;

        public GameTimerManager(Nonogram f, NonogramGrid g, NonogramRender r, UndoRedoManager u, ExtraGridManager e)
        {
            form = f;
            grid = g;
            render = r;
            undoredoManager = u;
            extraGridManager = e;

            // A Timer a Form1-ből jön
            form.gameTimer = new Timer();
            form.gameTimer.Interval = 1000;
            form.gameTimer.Tick += Timer_Tick;
        }

        public void SetGameTimerManager(GameTimerManager gtm)
        {
            form.gameTimerManager = gtm;
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
                    case 0: form.remainingSeconds = 6 * 60; break;   // Könnyű
                    case 1: form.remainingSeconds = 18 * 60; break;   // Közepes
                    case 2: form.remainingSeconds = 35 * 60; break;   // Nehéz
                    default: form.remainingSeconds = 6 * 60; break;
                }
            }
            else // Fekete-fehér mód
            {
                switch (form.cmbDifficulty.SelectedIndex)
                {
                    case 0: form.remainingSeconds = 6 * 60; break;   // Könnyű
                    case 1: form.remainingSeconds = 18 * 60; break;   // Közepes
                    case 2: form.remainingSeconds = 35 * 60; break;   // Nehéz
                    default: form.remainingSeconds = 6 * 60; break;
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
            NonogramRender render = form.render;
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
                    form.chkShowX.Visible = false;
                    form.btnSelectImage.Visible = false;
                    form.btnSolve.Enabled = true;
                    form.btnHint.Enabled = true;
                    form.btnCheck.Enabled = true;
                    form.btnRedo.Enabled = true;
                    form.btnUndo.Enabled = true;
                    form.chkShowX.Enabled = true;

                    grid.GenerateEasyNonogram(20, 150);

                    form.lblTimer.Visible = true;
                    form.picPreview.Visible = true;
                    form.picSolutionPreview.Visible = false;
                    // Timer újraindítása
                    Start();

                    if (picSolutionPreview != null)
                    {
                        picSolutionPreview.Image = grid.GeneratePreviewImage();
                        picSolutionPreview.SizeMode = PictureBoxSizeMode.Zoom;
                    }
                    break;

                case 1: // Közepes
                    form.chkShowX.Visible = false;
                    form.btnSelectImage.Visible = false;
                    form.btnSolve.Enabled = true;
                    form.btnHint.Enabled = true;
                    form.btnCheck.Enabled = true;
                    form.btnRedo.Enabled = true;
                    form.btnUndo.Enabled = true;
                    form.chkShowX.Enabled = true;

                    grid.GenerateMediumNonogram(20, 150);

                    form.lblTimer.Visible = true;
                    form.picPreview.Visible = true;
                    form.picSolutionPreview.Visible = false;
                    // Timer újraindítása
                    Start();
                    break;

                case 2: // Nehéz
                    form.chkShowX.Visible = false;
                    form.btnSelectImage.Visible = false;
                    form.btnSolve.Enabled = true;
                    form.btnHint.Enabled = true;
                    form.btnCheck.Enabled = true;
                    form.btnRedo.Enabled = true;
                    form.btnUndo.Enabled = true;
                    form.chkShowX.Enabled = true;

                    grid.GenerateHardNonogram(20, 150);

                    form.lblTimer.Visible = true;
                    form.picPreview.Visible = true;
                    form.picSolutionPreview.Visible = false;
                    // Timer újraindítása
                    Start();
                    break;

                default: // alapértelmezett
                    cmbDifficulty.SelectedIndex = 0;
                    grid.GenerateEasyNonogram(20, 150);
                    Start();
                    break;
            }

            SetMaxWrongClicksByDifficulty();
            SetMaxHintsByDifficulty();
            render.UpdatePreview();
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
            txtUsername.Enabled = false;
            form.btnCheck.Enabled = true;
            form.chkShowX.Enabled = true;
            form.btnSolve.Enabled = true;
            form.elapsedSeconds = 0;

            switch (form.cmbDifficulty.SelectedIndex)
            {
                case 0: // Könnyű
                    grid.GenerateEasyNonogram(20, 150);
                    if (form.picSolutionPreview != null)
                    {
                        form.picSolutionPreview.Image = grid.GeneratePreviewImage(); // friss kép
                        form.picSolutionPreview.SizeMode = PictureBoxSizeMode.Zoom;
                    }
                    // Időzítő újraindítása
                    Start();
                    break;

                case 1: // Nehéz
                    grid.GenerateMediumNonogram(20, 150);
                    if (form.picSolutionPreview != null)
                    {
                        form.picSolutionPreview.Image = grid.GeneratePreviewImage(); // friss kép
                        form.picSolutionPreview.SizeMode = PictureBoxSizeMode.Zoom;
                    }
                    // Időzítő újraindítása
                    Start();
                    break;

                case 2: // Nehéz
                    grid.GenerateHardNonogram(20, 150);
                    if (form.picSolutionPreview != null)
                    {
                        form.picSolutionPreview.Image = grid.GeneratePreviewImage(); // friss kép
                        form.picSolutionPreview.SizeMode = PictureBoxSizeMode.Zoom;
                    }
                    // Időzítő újraindítása
                    Start();
                    break;
            }

            //render.UpdatePreview();
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

        public void SetMaxWrongClicksByDifficulty()
        {
            if (form.isColor) // Színes mód
            {
                switch (form.cmbDifficulty.SelectedIndex)
                {
                    case 0: // Könnyű
                        form.maxWrongCellClicks = 12;
                        form.maxWrongColorClicks = 12;
                        break;
                    case 1: // Közepes
                        form.maxWrongCellClicks = 9;
                        form.maxWrongColorClicks = 9;
                        break;
                    case 2: // Nehéz
                        form.maxWrongCellClicks = 6;
                        form.maxWrongColorClicks = 6;
                        break;
                    default:
                        form.maxWrongCellClicks = 12;
                        form.maxWrongColorClicks = 12;
                        break;
                }
            }
            else // Fekete-fehér mód
            {
                switch (form.cmbDifficulty.SelectedIndex)
                {
                    case 0: // Könnyű
                        form.maxWrongCellClicks = 12;
                        form.maxWrongColorClicks = 12;
                        break;
                    case 1: // Közepes
                        form.maxWrongCellClicks = 9;
                        form.maxWrongColorClicks = 9;
                        break;
                    case 2: // Nehéz
                        form.maxWrongCellClicks = 6;
                        form.maxWrongColorClicks = 6;
                        break;

                    default:
                        form.maxWrongCellClicks = 12;
                        form.maxWrongColorClicks = 12;
                        break;
                }
            }

            // Frissítjük a label szöveget
            form.lblWrongCellClicks.Text = $"Helytelen kattintások száma: {form.wrongCellClicks} (max: {form.maxWrongCellClicks})";
            form.lblWrongColorClicks.Text = $"Helytelen színek száma: {form.wrongColorClicks} (max: {form.maxWrongColorClicks})";
        }

        public void SetMaxHintsByDifficulty()
        {
            if (form.isColor) // Színes mód
            {
                switch (form.cmbDifficulty.SelectedIndex)
                {
                    case 0: // Könnyű
                        form.maxHintCount = 5;
                        break;
                    case 1: // Közepes
                        form.maxHintCount = 3;
                        break;
                    case 2: // Nehéz
                        form.maxHintCount = 2;
                        break;
                    default:
                        form.maxHintCount = 5;
                        break;
                }
            }
            else // Fekete-fehér mód
            {
                switch (form.cmbDifficulty.SelectedIndex)
                {
                    case 0: // Könnyű
                        form.maxHintCount = 5;
                        break;
                    case 1: // Közepes
                        form.maxHintCount = 3;
                        break;
                    case 2: // Nehéz
                        form.maxHintCount = 2;
                        break;
                    default:
                        form.maxHintCount = 5;
                        break;
                }
            }

            form.lblHintCount.Text = $"Segítségek száma: {form.hintCount} (max: {form.maxHintCount})";
        }

        public void UpdateDifficultyAndModeToolTip()
        {
            string difficulty = form.cmbDifficulty.SelectedItem?.ToString()?.Trim();
            string mode = form.cmbMode.SelectedItem?.ToString()?.Trim();

            if (difficulty == null || mode == null)
                return;

            // Emoji eltávolítása (csak az első szó marad)
            int spaceIndex = difficulty.IndexOf(' ');
            if (spaceIndex > 0)
                difficulty = difficulty.Substring(0, spaceIndex);

            string text = "";

            if (mode == "Fekete-fehér")
            {
                switch (difficulty)
                {
                    case "Könnyű":
                        text = "Könnyű szint – Fekete-fehér mód\nHelytelen kattintások: 12\nSegítségek száma: 5";
                        break;
                    case "Közepes":
                        text = "Közepes szint – Fekete-fehér mód\nHelytelen kattintások: 6\nSegítségek száma: 3";
                        break;
                    case "Nehéz":
                        text = "Nehéz szint – Fekete-fehér mód\nHelytelen kattintások: 3\nSegítségek száma: 1";
                        break;
                }
            }
            else if (mode == "Színes")
            {
                switch (difficulty)
                {
                    case "Könnyű":
                        text = "Könnyű szint – Színes mód\nHelytelen kattintások: 10\nSegítségek száma: 4";
                        break;
                    case "Közepes":
                        text = "Közepes szint – Színes mód\nHelytelen kattintások: 5\nSegítségek száma: 2";
                        break;
                    case "Nehéz":
                        text = "Nehéz szint – Színes mód\nHelytelen kattintások: 2\nSegítségek száma: 0";
                        break;
                }
            }

            form.toolTip.SetToolTip(form.cmbDifficulty, text);
        }
    }
}