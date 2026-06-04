using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace Grafilogika_alkalmazas_keszitese
{
    public class GameTimerManager
    {
        public Timer gameTimer;
        public bool gameStarted = false;
        public int elapsedSeconds = 0;
        public int remainingSeconds = 0;
        private Nonogram form;
        private NonogramGrid grid;
        private NonogramRender render;
        private UndoRedoManager undoredoManager;
        private ExtraGridManager extragridManager;

        public GameTimerManager(Nonogram f, NonogramGrid g, NonogramRender r, UndoRedoManager u, ExtraGridManager e)
        {
            this.form = f;
            this.grid = g;
            this.render = r;
            this.undoredoManager = u;

            // Timer comes from Form1
            gameTimer = new Timer();
            gameTimer.Interval = 1000;
            gameTimer.Tick += Timer_Tick;
            this.extragridManager = e;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (remainingSeconds <= 0)
            {
                gameTimer.Stop();
                MessageBox.Show(
                    "Time is up! Game restarting.",
                    "Time is up",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                RestartGameWithCurrentDifficulty();
                return;
            }
            remainingSeconds--;
            elapsedSeconds++;
            UpdateLabel();
        }

        public void CmbDifficultyOrMode_Changed(object sender, EventArgs e)
        {
            UpdateDifficultyAndModeToolTip();
            if (!gameStarted && !form.rbNumberEntryMode.Checked)
            {
                return;
            }
            grid.isColor = form.cmbMode.SelectedItem?.ToString() == "Colored";
            grid.selectedColor = grid.isColor ? Color.White : Color.Black;
            DifficultyOrModeChanged();
            undoredoManager.ClearHistory();
        }

        public void BtnRestart_Click(object sender, EventArgs e)
        {
            RestartGameWithCurrentDifficulty();
        }

        public void BtnBackToHome_Click(object sender, EventArgs e)
        {
            grid.ClearGrid();
            extragridManager.ClearAllClueInputs();
            ResetCellClicks();
            ResetColorClicks();
            ResetHintClicks();
            gameTimer.Stop();
            form.lblTimer.Text = "0:00";
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
            form.lblWrongCellClicks.Visible = false;
            form.lblWrongColorClicks.Visible = false;
            form.lblHintCount.Visible = false;
            form.lblUndoCount.Visible = false;
            form.btnLeaderboard.Visible = true;
            form.btnGenerateRandom.Visible = true;
            form.btnTips.Visible = true;
            form.lblExtra.Visible = true;
            form.btnRestart.Visible = false;
            form.btnBackToHome.Visible = false;
            form.btnExtraGenerate.Visible = false;
            form.btnSmartAI.Visible = false;
            form.rbNumberEntryMode.Visible = true;
            form.rbImgBlackWhiteMode.Visible = true;
            form.rbImgColorMode.Visible = true;
            form.btnShowExtraSolution.Visible = false;
            form.btnPickColor.Visible = false;
            form.btnResetExtraGrid.Visible = false;
            form.btnSaveClues.Visible = false;
            form.btnLoadClues.Visible = false;
            form.btnSelectImage.Visible = false;
            form.rbNumberEntryMode.Checked = false;
            form.rbImgBlackWhiteMode.Checked = false;
            form.rbImgColorMode.Checked = false;
            form.groupModes.Visible = true;
            form.groupModes.Location = new Point(20, 200);
            form.lblUsername.Location = new Point(20, 100);
            form.txtUsername.Location = new Point(145, 95);
            form.Size = new Size(380, 350);
            form.txtUsername.Enabled = true;
            form.txtUsername.Text = "";
            form.colorPalette.Visible = false;
            form.ActiveControl = null;
            gameStarted = false;
            elapsedSeconds = 0;
            undoredoManager.undoClicks = 0;
            form.cmbDifficulty.SelectedIndex = 0;
            form.cmbMode.SelectedIndex = 0;
            form.lblWrongCellClicks.Text = $"Number of incorrect clicks: {grid.wrongCellClicks}";
            form.lblWrongColorClicks.Text = $"Number of incorrect colors: {grid.wrongColorClicks}";
            form.lblHintCount.Text = $"Number of hints: {render.hintCount}";
            form.lblUndoCount.Text = $"Number of withdrawals: {undoredoManager.undoClicks}";
            return;
        }

        public void BtnTips_Click(object sender, EventArgs e)
        {
            StringBuilder tips = new StringBuilder();

            tips.AppendLine("NONOGRAM TIPS (game modes, NOT extra modes)");
            tips.AppendLine("────────────────────────\n");

            // general hints
            tips.AppendLine("GENERAL TIPS\n");
            tips.AppendLine("If the sum of the numbers in a row or column + the number of required empty cells = the length of the row or column, then the entire row or column must be filled.\n");

            tips.AppendLine("If a number is greater than half of the row or column, the middle cells are guaranteed to be filled.\n");

            tips.AppendLine("The order of the numbers entered cannot be reversed.\n");

            tips.AppendLine("Each filled block must be aligned with the corresponding number.\n");

            tips.AppendLine("Always check the row and column numbers to avoid errors.\n");

            // X
            tips.AppendLine("X (exclusion)\n");

            tips.AppendLine("The purpose of the X marks is to mark cells that definitely do NOT belong to the solution.");
            tips.AppendLine("They help you make logical conclusions and avoid mistakes.\n");

            // Drag
            tips.AppendLine("MOUSEDRAG (mouse drag)\n");
            tips.AppendLine("Left mouse button drag: fill multiple cells at once.");
            tips.AppendLine("Right-click drag: place multiple X at once.\n");

            tips.AppendLine("Dragging the mouse speeds up the game, especially on larger maps.\n");

            // black and white game mode
            tips.AppendLine("BLACK AND WHITE GAME MODE\n");
            tips.AppendLine("Left click: place or remove a black cell.");
            tips.AppendLine("Right click: Place or remove an X.\n");

            tips.AppendLine("Black and white nonogram rules:\n");
            tips.AppendLine("The order of the numbers is always mandatory.");
            tips.AppendLine("There must be at least one empty cell between the blocks.");
            tips.AppendLine("X marks help with exclusion and logical inference.\n");

            // colored game mode
            tips.AppendLine("COLORFUL GAME MODE\n");
            tips.AppendLine("Left click: to place or remove the selected color.");
            tips.AppendLine("Right click: Place or remove an X.\n");

            tips.AppendLine("Color nonogram rules:\n");
            tips.AppendLine("The order and color of the numbers are always mandatory.");
            tips.AppendLine("There is no requirement for an empty cell between blocks of different colors (but it is possible).");
            tips.AppendLine("There must be at least one empty cell between blocks of the same color, otherwise it would be considered a single-color block.");
            tips.AppendLine("X symbols can also be used in color game mode for elimination, but be careful not to always eliminate them.");

            MessageBox.Show(
                tips.ToString(),
                "Solution tips",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }

        public void Start()
        {
            SetTimeByDifficulty();
            gameTimer.Start();
        }

        public void Stop()
        {
            gameTimer.Stop();
        }

        private void UpdateLabel()
        {
            int minutes = remainingSeconds / 60;
            int seconds = remainingSeconds % 60;
            form.lblTimer.Text = $"{minutes:D2}:{seconds:D2}";
        }

        private void SetTimeByDifficulty()
        {
            if (grid.isColor) // Color mode
            {
                switch (form.cmbDifficulty.SelectedIndex)
                {
                    case 0: remainingSeconds = 4 * 60; break;
                    case 1: remainingSeconds = 11 * 60; break;
                    case 2: remainingSeconds = 21 * 60; break;
                    default: remainingSeconds = 4 * 60; break;
                }
            }
            else // Black and white mode
            {
                switch (form.cmbDifficulty.SelectedIndex)
                {
                    case 0: remainingSeconds = 3 * 60; break;
                    case 1: remainingSeconds = 10 * 60; break;
                    case 2: remainingSeconds = 20 * 60; break;
                    default: remainingSeconds = 3 * 60; break;
                }
            }

            UpdateLabel();
        }

        public void DifficultyOrModeChanged()
        {
            form.chkShowX.Checked = false;
            form.btnCheck.Enabled = true;
            form.chkShowX.Enabled = true;

            if (grid == null)
            {
                return;
            }

            ResetCellClicks();
            ResetColorClicks();
            ResetHintClicks();

            grid.ClearGrid();
            form.gameTimerManager.Stop();

            grid.isColor = form.cmbMode.SelectedItem?.ToString() == "Colored";
            form.lblWrongCellClicks.Visible = true;
            form.lblWrongColorClicks.Visible = grid.isColor;
            form.lblHintCount.Visible = true;
            form.lblUndoCount.Visible = true;

            int width = 5, height = 5, targetPixels = 13, maxAttempts = 1000;

            switch (form.cmbDifficulty.SelectedIndex)
            {
                case 0:
                    width = height = 5;
                    targetPixels = grid.isColor ? 11 : 13;
                    maxAttempts = 1000;
                    form.chkShowX.Visible = false;
                    form.btnSelectImage.Visible = false;
                    form.btnSmartAI.Visible = true;
                    break;

                case 1:
                    width = height = 10;
                    targetPixels = grid.isColor ? 40 : 45;
                    maxAttempts = 1500;
                    form.chkShowX.Visible = false;
                    form.btnSelectImage.Visible = false;
                    break;

                case 2:
                    width = height = 15;
                    targetPixels = grid.isColor ? 95 : 100;
                    maxAttempts = 2500;
                    form.chkShowX.Visible = false;
                    form.btnSelectImage.Visible = false;
                    break;

                default:
                    form.cmbDifficulty.SelectedIndex = 0;
                    break;
            }

            form.btnSolve.Enabled = true;
            form.btnHint.Enabled = true;
            form.btnCheck.Enabled = true;
            form.btnRedo.Enabled = true;
            form.btnUndo.Enabled = true;
            form.chkShowX.Enabled = true;
            bool showUndoRedo = form.cmbDifficulty.SelectedIndex != 0;
            form.lblUndoCount.Visible = showUndoRedo;

            // Generate
            grid.GenerateNonogram(20, 150, width, height, targetPixels, maxAttempts);

            form.lblTimer.Visible = true;
            form.picPreview.Visible = true;
            form.picSolutionPreview.Visible = false;

            // Restart timer
            Start();

            if (form.picSolutionPreview != null)
            {
                form.picSolutionPreview.Image = render.GeneratePreviewImage();
                form.picSolutionPreview.SizeMode = PictureBoxSizeMode.Zoom;
            }

            SetMaxWrongClicksByDifficulty();
            SetMaxHintsByDifficulty();
            undoredoManager.UpdateUndoLabel();
            render.UpdatePreview();
        }

        public void ResetCellClicks()
        {
            grid.wrongCellClicks = 0;
            form.lblWrongCellClicks.Text = $"Number of incorrect clicks: 0 (max: {grid.maxWrongCellClicks})";
        }

        public void ResetColorClicks()
        {
            grid.wrongColorClicks = 0;
            form.lblWrongColorClicks.Text = $"Number of incorrect colors: 0 (max: {grid.maxWrongColorClicks})";
        }

        public void ResetHintClicks()
        {
            render.hintCount = 0;
            form.lblHintCount.Text = $"Number of hints: 0 (max: {render.maxHintCount})";
        }

        public void StartTimer()
        {
            form.gameTimerManager.Start();
        }

        public void RestartGameWithCurrentDifficulty()
        {
            grid.ClearGrid();
            undoredoManager.ClearHistory();
            ResetCellClicks();
            ResetColorClicks();
            ResetHintClicks();
            grid.wrongCellClicks = 0;
            grid.wrongColorClicks = 0;
            render.hintCount = 0;

            form.lblWrongCellClicks.Text = $"Number of incorrect clicks: {grid.wrongCellClicks} " +
                $"(max: {grid.maxWrongCellClicks})";
            form.lblWrongColorClicks.Text = $"Number of incorrect colors: {grid.wrongColorClicks} " +
                $"(max: {grid.maxWrongColorClicks})";
            form.lblHintCount.Text = $"Number of hints: {render.hintCount} " +
                $"(max: {render.maxHintCount})";
            form.picPreview.Visible = true;
            form.picPreview.Image = null;

            if (form.txtUsername != null)
            {
                form.txtUsername.Enabled = false;
            }

            form.btnSmartAI.Enabled = true;
            form.btnCheck.Enabled = true;
            form.chkShowX.Enabled = true;
            form.btnSolve.Enabled = true;
            form.btnHint.Enabled = true;
            form.btnCheck.Enabled = true;
            form.btnUndo.Enabled = true;
            form.btnRedo.Enabled = true;
            form.btnBackToHome.Enabled = true;
            grid.aiButtonClicked = false;
            elapsedSeconds = 0;
            undoredoManager.undoClicks = 0;
            undoredoManager.UpdateUndoLabel();

            int width = 5, height = 5, targetPixels = 13, maxAttempts = 1000;

            switch (form.cmbDifficulty.SelectedIndex)
            {
                case 0:
                    width = height = 5;
                    targetPixels = grid.isColor ? 11 : 13;
                    maxAttempts = 1000;
                    break;

                case 1:
                    width = height = 10;
                    targetPixels = grid.isColor ? 40 : 45;
                    maxAttempts = 1500;
                    break;

                case 2:
                    width = height = 15;
                    targetPixels = grid.isColor ? 95 : 100;
                    maxAttempts = 2500;
                    break;
            }

            grid.GenerateNonogram(20, 150, width, height, targetPixels, maxAttempts);

            if (form.picSolutionPreview != null)
            {
                form.picSolutionPreview.Image = render.GeneratePreviewImage();
                form.picSolutionPreview.SizeMode = PictureBoxSizeMode.Zoom;
            }

            Start();
            render.UpdatePreview();
            undoredoManager.SaveState();
        }

        public string GetDifficultyName()
        {
            switch (form.cmbDifficulty.SelectedIndex)
            {
                case 0: return "Easy";
                case 1: return "Medium";
                case 2: return "Hard";
                default: return "Unknown";
            }
        }

        public string GetModeName()
        {
            return grid.isColor ? "Colored" : "Black and white";
        }

        public void SetMaxWrongClicksByDifficulty()
        {
            if (grid.isColor) // Color mode
            {
                switch (form.cmbDifficulty.SelectedIndex)
                {
                    case 0:
                        grid.maxWrongCellClicks = 5;
                        grid.maxWrongColorClicks = 5;
                        break;
                    case 1:
                        grid.maxWrongCellClicks = 15;
                        grid.maxWrongColorClicks = 15;
                        undoredoManager.maxUndoClicks = 8;
                        break;
                    case 2:
                        grid.maxWrongCellClicks = 20;
                        grid.maxWrongColorClicks = 20;
                        undoredoManager.maxUndoClicks = 20;
                        break;
                    default:
                        grid.maxWrongCellClicks = 5;
                        grid.maxWrongColorClicks = 5;
                        break;
                }
            }
            else // Black and white mode
            {
                switch (form.cmbDifficulty.SelectedIndex)
                {
                    case 0:
                        grid.maxWrongCellClicks = 5;
                        grid.maxWrongColorClicks = 5;
                        break;
                    case 1:
                        grid.maxWrongCellClicks = 10;
                        grid.maxWrongColorClicks = 10;
                        undoredoManager.maxUndoClicks = 5;
                        break;
                    case 2:
                        grid.maxWrongCellClicks = 15;
                        grid.maxWrongColorClicks = 15;
                        undoredoManager.maxUndoClicks = 17;
                        break;

                    default:
                        grid.maxWrongCellClicks = 5;
                        grid.maxWrongColorClicks = 5;
                        break;
                }
            }

            // Update the label text
            form.lblWrongCellClicks.Text = $"Number of incorrect clicks: {grid.wrongCellClicks} " +
                $"(max: {grid.maxWrongCellClicks})";
            form.lblWrongColorClicks.Text = $"Number of colors: {grid.wrongColorClicks} " +
                $"(max: {grid.maxWrongColorClicks})";
        }

        public void SetMaxHintsByDifficulty()
        {
            if (grid.isColor) // Color mode
            {
                switch (form.cmbDifficulty.SelectedIndex)
                {
                    case 0:
                        render.maxHintCount = 5;
                        break;
                    case 1:
                        render.maxHintCount = 15;
                        break;
                    case 2:
                        render.maxHintCount = 25;
                        break;
                    default:
                        render.maxHintCount = 5;
                        break;
                }
            }
            else // Black and white mode
            {
                switch (form.cmbDifficulty.SelectedIndex)
                {
                    case 0:
                        render.maxHintCount = 5;
                        break;
                    case 1:
                        render.maxHintCount = 10;
                        break;
                    case 2:
                        render.maxHintCount = 20;
                        break;
                    default:
                        render.maxHintCount = 5;
                        break;
                }
            }

            form.lblHintCount.Text = $"Number of hints: {render.hintCount} (max: {render.maxHintCount})";
        }

        public void UpdateDifficultyAndModeToolTip()
        {
            string difficulty = form.cmbDifficulty.SelectedItem?.ToString()?.Trim();
            string mode = form.cmbMode.SelectedItem?.ToString()?.Trim();

            if (difficulty == null || mode == null)
            {
                return;
            }

            // Remove emoji (only the first word remains)
            int spaceIndex = difficulty.IndexOf(' ');
            if (spaceIndex > 0)
            {
                difficulty = difficulty.Substring(0, spaceIndex);
            }

            string text = "";

            if (mode == "Black and white")
            {
                switch (difficulty)
                {
                    case "Easy":
                        text = "Easy level – Black and white mode\nNumber of incorrect clicks: 5\nNumber of hints: 5" +
                            "\nUnlimited number of withdrawals\nNumber of fields to fill in: 13" +
                            "\nGame time: 3 minutes";
                        break;
                    case "Medium":
                        text = "Medium level – Black and white mode\nNumber of incorrect clicks: 10\nNumber of hints: 10" +
                            "\nNumber of withdrawals: 5\nNumber of fields to fill in: 45" +
                            "\nGame time: 10 minutes";
                        break;
                    case "Hard":
                        text = "Hard level – Black and white mode\nNumber of incorrect clicks: 15\nNumber of hints: 20" +
                            "\nNumber of withdrawals: 17\nNumber of fields to fill in: 100" +
                            "\nGame time: 20 minutes";
                        break;
                }
            }
            else if (mode == "Colored")
            {
                switch (difficulty)
                {
                    case "Easy":
                        text = "Easy level – Colored mode\nNumber of incorrect clicks: 5\n" +
                            "Number of colors: 5\nNumber of hints: 5" +
                            "\nUnlimited number of withdrawals\nNumber of fields to fill in: 11" +
                            "\nGame time: 4 minutes";
                        break;
                    case "Medium":
                        text = "Medium level – Colored mode\nNumber of incorrect clicks: 15" +
                            "\nNumber of colors: 15\nNumber of hints: 15" +
                            "\nNumber of withdrawals: 8\nNumber of fields to fill in: 40" +
                            "\nGame time: 11 minutes";
                        break;
                    case "Hard":
                        text = "Hard level – Colored mode\nNumber of incorrect clicks: 20" +
                            "\nNumber of colors: 20\nNumber of hints: 25" +
                            "\nNumber of withdrawals: 20\nNumber of fields to fill in: 95" +
                            "\nGame time: 21 minutes";
                        break;
                }
            }

            form.toolTip.SetToolTip(form.cmbDifficulty, text);
            form.toolTip.SetToolTip(form.cmbMode, text);
        }
    }
}
