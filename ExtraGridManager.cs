using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace Grafilogika_alkalmazas_keszitese
{
    public class ExtraGridManager
    {
        private int row = 15;
        private int col = 15;
        public int cellSize = 35;
        public Button[,] extraButtons;
        public int[,] extraClues;
        public RichTextBox[] rowClueInputs;
        public RichTextBox[] colClueInputs;
        public List<string> rowCluesRtf;
        public List<string> colCluesRtf;
        public bool suppressTextChanged = false;
        public List<int>[] rowCluesExtra;
        public List<int>[] colCluesExtra;
        private Queue<Point> extraSolutionQueue;
        private Timer extraSolveTimer;
        public Color[,] extraColors;
        public Dictionary<RichTextBox, List<Color>> textBoxColors = new Dictionary<RichTextBox, List<Color>>();
        private RichTextBox currentColorTextBox = null;
        public RichTextBox activeClueTextBox = null;
        private Nonogram form;
        private NonogramGrid grid;
        private NonogramRender render;
        public ExtraGridManager(Nonogram f, NonogramGrid g, NonogramRender r)
        {
            this.form = f;
            this.grid = g;
            this.render = r;
        }

        public void SetRender(NonogramRender r)
        {
            render = r;
        }

        public void BtnSaveClues_Click(object sender, EventArgs e)
        {
            bool hasAnyClue = false;

            for (int rowIndex = 0; rowIndex < row; rowIndex++)
            {
                if (!string.IsNullOrWhiteSpace(rowClueInputs[rowIndex]?.Text))
                {
                    hasAnyClue = true;
                    break;
                }
            }

            if (!hasAnyClue)
            {
                for (int colIndex = 0; colIndex < col; colIndex++)
                {
                    if (!string.IsNullOrWhiteSpace(colClueInputs[colIndex]?.Text))
                    {
                        hasAnyClue = true;
                        break;
                    }
                }
            }

            if (!hasAnyClue)
            {
                MessageBox.Show(
                    "Cannot save empty fields!\nPlease enter at least one row or column.",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                return;
            }

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "Nonogram Clues (*.nono)|*.nono";
                sfd.Title = "Save clues";

                if (sfd.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                // Initialize lists
                rowCluesRtf = new List<string>();
                colCluesRtf = new List<string>();

                for (int rowIndex = 0; rowIndex < row; rowIndex++)
                {
                    rowCluesRtf.Add(rowClueInputs[rowIndex] != null ? rowClueInputs[rowIndex].Rtf : "");
                }

                for (int colIndex = 0; colIndex < col; colIndex++)
                {
                    colCluesRtf.Add(colClueInputs[colIndex] != null ? colClueInputs[colIndex].Rtf : "");
                }

                Dictionary<string, object> data = new Dictionary<string, object>();
                data.Add("Rows", row);
                data.Add("Cols", col);
                data.Add("RowCluesRtf", rowCluesRtf);
                data.Add("ColCluesRtf", colCluesRtf);

                System.Text.Json.JsonSerializerOptions options = new System.Text.Json.JsonSerializerOptions();
                options.WriteIndented = true;

                string json = System.Text.Json.JsonSerializer.Serialize(data, options);

                File.WriteAllText(sfd.FileName, json, Encoding.UTF8);

                MessageBox.Show(
                    "The numbers have been saved successfully!",
                    "Save ready",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
        }

        public void BtnLoadClues_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Nonogram Clues (*.nono)|*.nono";
                ofd.Title = "Loading clues";

                if (ofd.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                string json = File.ReadAllText(ofd.FileName, Encoding.UTF8);

                Dictionary<string, System.Text.Json.JsonElement> data =
                    System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(json);

                if (data == null ||
                    !data.ContainsKey("Rows") ||
                    !data.ContainsKey("Cols") ||
                    !data.ContainsKey("RowCluesRtf") ||
                    !data.ContainsKey("ColCluesRtf"))
                {
                    MessageBox.Show(
                        "Bad file!",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                    return;
                }

                int fileRows = data["Rows"].GetInt32();
                int fileCols = data["Cols"].GetInt32();

                if (fileRows != row || fileCols != col)
                {
                    MessageBox.Show(
                        "The saved grid size does not match!",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                    return;
                }

                // Reset lists
                List<string> loadedRowCluesRtf = System.Text.Json.JsonSerializer
                    .Deserialize<List<string>>(data["RowCluesRtf"].GetRawText());
                List<string> loadedColCluesRtf = System.Text.Json.JsonSerializer
                    .Deserialize<List<string>>(data["ColCluesRtf"].GetRawText());

                if (loadedRowCluesRtf == null || loadedColCluesRtf == null)
                {
                    MessageBox.Show(
                        "Bad file!",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                    return;
                }

                suppressTextChanged = true;

                for (int rowIndex = 0; rowIndex < row; rowIndex++)
                {
                    rowClueInputs[rowIndex].Rtf = loadedRowCluesRtf[rowIndex];
                }

                for (int colIndex = 0; colIndex < col; colIndex++)
                {
                    colClueInputs[colIndex].Rtf = loadedColCluesRtf[colIndex];
                }

                suppressTextChanged = false;

                PrepareTextBoxColors();
                HideUnusedExtraCluesAndButtons();

                form.chkShowX.Checked = false;
                if (grid.solutionBW == null || grid.solutionBW.GetLength(0) != row || grid.solutionBW.GetLength(1) != col)
                {
                    grid.solutionBW = new int[row, col];
                }
                if (grid.solutionColorRGB == null || grid.solutionColorRGB.GetLength(0) != row || 
                    grid.solutionColorRGB.GetLength(1) != col)
                {
                    grid.solutionColorRGB = new Color[row, col];
                }
                for (int rowIndex = 0; rowIndex < row; rowIndex++)
                {
                    for (int colIndex = 0; colIndex < col; colIndex++)
                    {
                        extraColors[rowIndex, colIndex] = Color.White;
                        extraButtons[rowIndex, colIndex].BackColor = Color.White;
                        grid.solutionBW[rowIndex, colIndex] = 0;
                        grid.solutionColorRGB[rowIndex, colIndex] = Color.White;
                    }
                }

                // Dispose and null for the preview
                if (form.picPreview.Image != null)
                {
                    form.picPreview.Image.Dispose();
                    form.picPreview.Image = null;
                }
                form.chkShowX.Enabled = false;
                MessageBox.Show(
                    "The numbers have been successfully loaded!",
                    "Loading ready",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
        }

        public void BtnGenerateExtraGrid_Click(object sender, EventArgs e)
        {
            form.chkShowX.Checked = false;
            form.picPreview.Image = null;
            ClearAllClueInputs();
            InitializeExtraRowInputs();
            InitializeExtraColumnInputs();
            InitializeExtraGrid();
        }

        public void ClueTextBox_TextChanged(object sender, EventArgs e)
        {
            if (suppressTextChanged)
            {
                return;
            }

            RichTextBox rtb = sender as RichTextBox;
            if (rtb == null || !textBoxColors.ContainsKey(rtb))
            {
                return;
            }

            suppressTextChanged = true;

            int cursorPos = rtb.SelectionStart;

            string[] parts = rtb.Text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            List<Color> colors = textBoxColors[rtb];

            // Update color list new number black
            while (colors.Count < parts.Length)
            {
                colors.Add(Color.Black);
            }

            int charPos = 0;
            for (int partIndex = 0; partIndex < parts.Length; partIndex++)
            {
                string part = parts[partIndex];
                int start = rtb.Text.IndexOf(part, charPos);
                if (start < 0)
                {
                    continue;
                }

                rtb.SelectionStart = start;
                rtb.SelectionLength = part.Length;

                // We just set the color to a new number
                if (partIndex >= colors.Count - (parts.Length - colors.Count))
                {
                    rtb.SelectionColor = colors[partIndex];
                }

                charPos = start + part.Length;
            }

            rtb.SelectionStart = cursorPos;
            rtb.SelectionLength = 0;

            suppressTextChanged = false;
        }

        public void BtnPickColor_Click(object sender, EventArgs e)
        {
            if (activeClueTextBox == null)
            {
                MessageBox.Show("Please first click on the field you want to write in!",
                                "No field selected",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                return;
            }

            RichTextBox rtb = activeClueTextBox;

            if (string.IsNullOrWhiteSpace(rtb.Text))
            {
                MessageBox.Show("Please enter at least one number in the field before assigning a color!",
                                "No data",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                return;
            }

            using (ColorDialog colorDialog = new ColorDialog())
            {
                if (colorDialog.ShowDialog() == DialogResult.OK)
                {
                    suppressTextChanged = true;

                    if (rtb.SelectionLength == 0)
                    {
                        string[] parts = rtb.Text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0)
                        {
                            int index = GetCurrentNumberIndex(rtb);
                            int charPos = 0;
                            for (int partIndex = 0; partIndex < index; partIndex++)
                            {
                                charPos += parts[partIndex].Length + 1;
                            }

                            rtb.SelectionStart = charPos;
                            rtb.SelectionLength = parts[index].Length;
                        }
                    }

                    rtb.SelectionColor = colorDialog.Color;

                    if (!textBoxColors.ContainsKey(rtb))
                    {
                        textBoxColors[rtb] = new List<Color>();
                    }

                    int selectionIndex = GetCurrentNumberIndex(rtb);
                    while (textBoxColors[rtb].Count <= selectionIndex)
                    {
                        textBoxColors[rtb].Add(Color.Black);
                    }

                    textBoxColors[rtb][selectionIndex] = colorDialog.Color;

                    suppressTextChanged = false;
                }
            }
        }

        public void BtnShowExtraSolution_Click(object sender, EventArgs e)
        {
            if (!ReadExtraClues())
            {
                return;
            }
            grid.isColor = form.cmbMode.SelectedItem?.ToString() == "Colored";
            if (!grid.isColor)
            {
                grid.solutionBW = new int[row, col];
                bool solvedBW = SolveExtraNonogramSimple(grid.solutionBW, rowCluesExtra, colCluesExtra);
                if (!solvedBW)
                {
                    MessageBox.Show("The Nonogram cannot be solved based on the given clues!", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                form.picPreview.Image = null;
                form.chkShowX.Enabled = true;
                form.chkShowX.Checked = false;
                form.btnShowExtraSolution.Enabled = false;
                form.btnPickColor.Enabled = false;
                form.btnResetExtraGrid.Enabled = false;
                form.btnSaveClues.Enabled = false;
                form.btnLoadClues.Enabled = false;
                form.btnBackToHome.Enabled = false;
                form.btnExtraGenerate.Enabled = false;
                form.rbNumberEntryMode.Enabled = false;
                form.rbImgBlackWhiteMode.Enabled = false;
                form.rbImgColorMode.Enabled = false;
                form.ActiveControl = null;
                if (form.picPreview.Image != null)
                {
                    form.picPreview.Image.Dispose();
                    form.picPreview.Image = null;
                }

                for (int rowIndex = 0; rowIndex < rowClueInputs.Length; rowIndex++)
                {
                    rowClueInputs[rowIndex].Enabled = false;
                }
                for (int colIndex = 0; colIndex < colClueInputs.Length; colIndex++)
                {
                    colClueInputs[colIndex].Enabled = false;
                }
                for (int rowIndex = 0; rowIndex < rowClueInputs.Length; rowIndex++)
                {
                    for (int colIndex = 0; colIndex < colClueInputs.Length; colIndex++)
                    {
                        extraButtons[rowIndex, colIndex].Enabled = false;
                    }
                }

                for (int rowIndex = 0; rowIndex < row; rowIndex++)
                {
                    for (int colIndex = 0; colIndex < col; colIndex++)
                    {
                        extraButtons[rowIndex, colIndex].BackColor = Color.White;
                        extraButtons[rowIndex, colIndex].Text = "";
                        extraColors[rowIndex, colIndex] = Color.White; // also for preview
                    }
                }
                StartExtraSolveAnimation();
            }
            else
            {
                if (!PrepareTextBoxColors())
                {
                    return;
                }

                grid.solutionColorRGB = new Color[row, col];

                bool solvedColor = SolveExtraNonogramColor(
                    grid.solutionColorRGB,
                    rowCluesExtra,
                    colCluesExtra,
                    textBoxColors
                );

                if (!solvedColor)
                {
                    MessageBox.Show("The Nonogram cannot be solved!", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                form.picPreview.Image = null;
                form.chkShowX.Enabled = true;
                form.chkShowX.Checked = false;
                form.btnShowExtraSolution.Enabled = false;
                form.btnPickColor.Enabled = false;
                form.btnResetExtraGrid.Enabled = false;
                form.btnSaveClues.Enabled = false;
                form.btnLoadClues.Enabled = false;
                form.btnBackToHome.Enabled = false;
                form.btnExtraGenerate.Enabled = false;
                form.rbNumberEntryMode.Enabled = false;
                form.rbImgBlackWhiteMode.Enabled = false;
                form.rbImgColorMode.Enabled = false;
                form.ActiveControl = null;
                if (form.picPreview.Image != null)
                {
                    form.picPreview.Image.Dispose();
                    form.picPreview.Image = null;
                }

                for (int rowIndex = 0; rowIndex < rowClueInputs.Length; rowIndex++)
                {
                    rowClueInputs[rowIndex].Enabled = false;
                }
                for (int colIndex = 0; colIndex < colClueInputs.Length; colIndex++)
                {
                    colClueInputs[colIndex].Enabled = false;
                }
                for (int rowIndex = 0; rowIndex < rowClueInputs.Length; rowIndex++)
                {
                    for (int colIndex = 0; colIndex < colClueInputs.Length; colIndex++)
                    {
                        extraButtons[rowIndex, colIndex].Enabled = false;
                    }
                }

                for (int rowIndex = 0; rowIndex < row; rowIndex++)
                {
                    for (int colIndex = 0; colIndex < col; colIndex++)
                    {
                        extraButtons[rowIndex, colIndex].BackColor = Color.White;
                        extraButtons[rowIndex, colIndex].Text = "";
                        extraColors[rowIndex, colIndex] = Color.White; // also for preview
                    }
                }
                StartExtraSolveAnimation();
                HideUnusedExtraCluesAndButtons();
            }
        }

        private void HideUnusedExtraCluesAndButtons()
        {
            int usedRows = rowClueInputs.Count(rtb => !string.IsNullOrWhiteSpace(rtb.Text));
            int usedCols = colClueInputs.Count(rtb => !string.IsNullOrWhiteSpace(rtb.Text));

            for (int rowIndex = 0; rowIndex < rowClueInputs.Length; rowIndex++)
            {
                rowClueInputs[rowIndex].Visible = rowIndex < usedRows;
            }
            for (int colIndex = 0; colIndex < colClueInputs.Length; colIndex++)
            {
                colClueInputs[colIndex].Visible = colIndex < usedCols;
            }

            for (int rowIndex = 0; rowIndex < row; rowIndex++)
            {
                for (int colIndex = 0; colIndex < col; colIndex++)
                {
                    extraButtons[rowIndex, colIndex].Visible = rowIndex < usedRows && colIndex < usedCols;
                }
            }
        }

        public void UpdateExtraPreview(int? rowIndex = null, int? colIndex = null)
        {
            if (rowClueInputs == null || colClueInputs == null)
            {
                return;
            }
            int oldRows = grid.row;
            int oldCols = grid.col;
            int oldCellSize = grid.userCellSize;
            Color[,] oldColors = grid.userColorRGB;

            int usedRows = 0;
            for (int rowInputIndex = 0; rowInputIndex < rowClueInputs.Length; rowInputIndex++)
            {
                if (!string.IsNullOrWhiteSpace(rowClueInputs[rowInputIndex].Text))
                {
                    usedRows++;
                }
            }

            int usedCols = 0;
            for (int colInputIndex = 0; colInputIndex < colClueInputs.Length; colInputIndex++)
            {
                if (!string.IsNullOrWhiteSpace(colClueInputs[colInputIndex].Text))
                {
                    usedCols++;
                }
            }

            if (usedRows == 0 || usedCols == 0)
            {
                return;
            }

            grid.row = usedRows;
            grid.col = usedCols;
            grid.userCellSize = cellSize;
            grid.userColorRGB = extraColors;

            render.UpdatePreview(rowIndex, colIndex);

            grid.row = oldRows;
            grid.col = oldCols;
            grid.userCellSize = oldCellSize;
            grid.userColorRGB = oldColors;
        }

        public void StartExtraSolveAnimation()
        {
            List<Point> solutionCells = new List<Point>();

            for (int rowIndex = 0; rowIndex < row; rowIndex++)
            {
                for (int colIndex = 0; colIndex < col; colIndex++)
                {
                    Color cellColor = grid.isColor ? grid.solutionColorRGB[rowIndex, colIndex] :
                                      grid.solutionBW[rowIndex, colIndex] == 1 ? Color.Black : Color.White;

                    // Only add the cells to be filled to the queue
                    if (cellColor != Color.White)
                    {
                        solutionCells.Add(new Point(rowIndex, colIndex));
                    }
                }
            }

            extraSolutionQueue = new Queue<Point>(solutionCells);

            extraSolveTimer = new Timer();
            extraSolveTimer.Interval = 100;
            extraSolveTimer.Tick += ExtraSolveTimer_Tick;
            extraSolveTimer.Start();
        }

        private void ExtraSolveTimer_Tick(object sender, EventArgs e)
        {
            if (extraSolutionQueue.Count == 0)
            {
                extraSolveTimer.Stop();
                extraSolveTimer.Dispose();

                // End of animation – release buttons
                form.chkShowX.Enabled = true;
                form.btnShowExtraSolution.Enabled = true;
                form.btnPickColor.Enabled = true;
                form.btnResetExtraGrid.Enabled = true;
                form.btnSaveClues.Enabled = true;
                form.btnLoadClues.Enabled = true;
                form.btnBackToHome.Enabled = true;
                form.btnExtraGenerate.Enabled = true;
                form.rbNumberEntryMode.Enabled = true;
                form.rbImgBlackWhiteMode.Enabled = true;
                form.rbImgColorMode.Enabled = true;

                for (int rowIndex = 0; rowIndex < rowClueInputs.Length; rowIndex++)
                {
                    rowClueInputs[rowIndex].Enabled = true;
                }
                for (int colIndex = 0; colIndex < colClueInputs.Length; colIndex++)
                {
                    colClueInputs[colIndex].Enabled = true;
                }
                for (int rowIndex = 0; rowIndex < rowClueInputs.Length; rowIndex++)
                {
                    for (int colIndex = 0; colIndex < colClueInputs.Length; colIndex++)
                    {
                        extraButtons[rowIndex, colIndex].Enabled = false;
                    }
                }

                MessageBox.Show("The nonogram is completely solved!", "Solution ready");
                return;
            }

            // Draw per cell
            Point pos = extraSolutionQueue.Dequeue();
            int row = pos.X;
            int col = pos.Y;

            Color finalColor = grid.isColor ? grid.solutionColorRGB[row, col] :
                               grid.solutionBW[row, col] == 1 ? Color.Black : Color.White;

            extraButtons[row, col].BackColor = finalColor;
            extraColors[row, col] = finalColor;
            UpdateExtraPreview(row, col);
            if (finalColor != Color.White)
            {
                extraButtons[row, col].Text = "";
            }
        }
        public void ClueTextBox_Enter(object sender, EventArgs e)
        {
            currentColorTextBox = sender as RichTextBox;
        }

        public void ClueTextBox_Click(object sender, EventArgs e)
        {
            activeClueTextBox = sender as RichTextBox;
        }

        public void BtnClearGrid_Click(object sender, EventArgs e)
        {
            form.chkShowX.Checked = false;
            form.chkShowX.Enabled = false;
            form.picPreview.Image = null;
            if (!IsAnythingToClear())
            {
                MessageBox.Show("The grid and fields are empty, there is nothing to delete!", "Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
            // Only run if there is something to delete
            ClearExtraGridContents();
        }

        private void SetMode()
        {
            grid.ClearGrid();
            ClearAllClueInputs();
            form.ActiveControl = null;
            // Hide all controls at once
            foreach (Control control in form.Controls)
            {
                control.Visible = false;
            }
            form.groupModes.Visible = true;
            // Always visible checkboxes
            form.rbNumberEntryMode.Visible = true;
            form.rbImgColorMode.Visible = true;
            form.rbImgBlackWhiteMode.Visible = true;
            form.lblWrongCellClicks.Visible = true;
            form.lblWrongColorClicks.Visible = true;
            form.lblHintCount.Visible = true;
            form.lblUndoCount.Visible = true;
            form.chkShowX.Checked = false;
            form.picPreview.Image = null;
            form.picSolutionPreview.Image = null;
            form.cmbMode.SelectedIndex = 0;

            // Extra mode
            if (form.rbNumberEntryMode.Checked)
            {
                form.btnResetExtraGrid.Visible = true;
                form.btnSaveClues.Visible = true;
                form.btnLoadClues.Visible = true;
                form.btnShowExtraSolution.Visible = true;
                form.btnPickColor.Visible = true;
                form.btnExtraGenerate.Visible = true;
                form.lblWrongCellClicks.Visible = false;
                form.lblWrongColorClicks.Visible = false;
                form.lblHintCount.Visible = false;
                form.lblUndoCount.Visible = false;
                form.picPreview.Visible = true;
                form.groupModes.Location = new Point(70, 530);
                form.chkShowX.Visible = true;
                form.chkShowX.Enabled = false;
                form.chkShowX.Location = new Point(100, 200);
                form.btnBackToHome.Visible = true;
                form.btnBackToHome.Location = new Point(840, 740);
                form.cmbMode.SelectedIndex = 1;
                grid.isColor = form.cmbMode.SelectedItem?.ToString() == "Colored";
                form.btnPickColor.Visible = grid.isColor;
                form.Size = new Size(1070, 820);

                InitializeExtraRowInputs();
                InitializeExtraColumnInputs();
                InitializeExtraGrid();
            }
            // Image scanning mode (black and white or color)
            else if (form.rbImgBlackWhiteMode.Checked)
            {
                form.btnSelectImage.Visible = true;
                form.picPreview.Visible = true;
                form.chkShowX.Visible = true;
                form.chkShowX.Enabled = false;
                form.btnSolve.Visible = true;
                form.btnSolve.Enabled = false;
                form.lblWrongCellClicks.Visible = false;
                form.lblWrongColorClicks.Visible = false;
                form.lblHintCount.Visible = false;
                form.lblUndoCount.Visible = false;
                form.groupModes.Location = new Point(60, 530);
                form.chkShowX.Location = new Point(175, 185);
                form.btnBackToHome.Visible = true;
                form.btnBackToHome.Location = new Point(235, 650);
                form.Size = new Size(1000, 800);
            }
            else if (form.rbImgColorMode.Checked)
            {
                form.btnSelectImage.Visible = true;
                form.picPreview.Visible = true;
                form.chkShowX.Visible = true;
                form.chkShowX.Enabled = false;
                form.btnSolve.Visible = true;
                form.btnSolve.Enabled = false;
                form.lblWrongCellClicks.Visible = false;
                form.lblWrongColorClicks.Visible = false;
                form.lblHintCount.Visible = false;
                form.lblUndoCount.Visible = false;
                form.groupModes.Location = new Point(60, 530);
                form.chkShowX.Location = new Point(175, 185);
                form.btnBackToHome.Visible = true;
                form.btnBackToHome.Location = new Point(235, 650);
                form.Size = new Size(1000, 800);
            }
            // Normal mode
            else
            {
                form.cmbDifficulty.Visible = true;
                form.cmbMode.Visible = true;
                form.lblUsername.Visible = true;
                form.txtUsername.Visible = true;
                form.btnGenerateRandom.Visible = true;
                form.btnLeaderboard.Visible = true;
                form.btnTips.Visible = true;
                form.lblExtra.Visible = true;
                form.cmbDifficulty.SelectedIndex = 0;
                form.cmbMode.SelectedIndex = 0;
                form.Size = new Size(900, 800);
            }
        }

        public void RbNumberEntryMode_CheckedChanged(object sender, EventArgs e)
        {
            if (form.rbNumberEntryMode.Checked)
            {
                SetMode();
            }
        }

        public void RbImgBlackWhiteMode_CheckedChanged(object sender, EventArgs e)
        {
            if (form.rbImgBlackWhiteMode.Checked)
            {
                SetMode();
            }
        }

        public void RbImgColorMode_CheckedChanged(object sender, EventArgs e)
        {
            if (form.rbImgColorMode.Checked)
            {
                SetMode();
            }
        }
        public void InitializeExtraGrid()
        {
            int gridLeft = 500;
            int gridTop = 150;

            if (extraButtons == null)
            {
                extraButtons = new Button[row, col];
            }

            for (int rowIndex = 0; rowIndex < row; rowIndex++)
            {
                for (int colIndex = 0; colIndex < col; colIndex++)
                {
                    if (extraButtons[rowIndex, colIndex] == null)
                    {
                        Button btn = new Button();
                        btn.Size = new Size(cellSize, cellSize);
                        btn.Location = new Point(gridLeft + colIndex * cellSize, gridTop + rowIndex * cellSize);
                        btn.BackColor = Color.White;
                        btn.Tag = new Point(rowIndex, colIndex);
                        btn.FlatStyle = FlatStyle.Flat;
                        btn.FlatAppearance.BorderColor = Color.Black;
                        btn.FlatAppearance.BorderSize = 1;
                        btn.Enabled = false;
                        form.Controls.Add(btn);
                        extraButtons[rowIndex, colIndex] = btn;
                    }
                    else
                    {
                        extraButtons[rowIndex, colIndex].Visible = true;
                        extraButtons[rowIndex, colIndex].BackColor = Color.White;
                    }
                }
            }

            // Initialize the extraClues matrix as well
            if (extraClues == null)
            {
                extraClues = new int[row, col];
            }

            extraColors = new Color[row, col];

            for (int rowIndex = 0; rowIndex < row; rowIndex++)
            {
                for (int colIndex = 0; colIndex < col; colIndex++)
                {
                    extraColors[rowIndex, colIndex] = Color.White;
                }
            }
        }

        public void InitializeExtraRowInputs()
        {
            if (rowClueInputs != null)
            {
                foreach (RichTextBox rtb in rowClueInputs)
                {
                    if (rtb != null)
                    {
                        rtb.Text = ""; // delete
                    }
                }
            }

            rowClueInputs = new RichTextBox[row];
            rowCluesExtra = new List<int>[row];

            int startX = 360;
            int startY = 150;

            for (int rowIndex = 0; rowIndex < row; rowIndex++)
            {
                RichTextBox rtb = new RichTextBox();
                rtb.Size = new Size(140, cellSize);
                rtb.Location = new Point(startX, startY + rowIndex * cellSize);
                rtb.Font = new Font("Arial", 14, FontStyle.Bold);
                rtb.Tag = "row";
                rtb.Enter += ClueTextBox_Enter;
                rtb.Click += ClueTextBox_Click;
                rtb.TextChanged += ClueTextBox_TextChanged;
                rtb.KeyPress += RowClueInput_KeyPress;
                form.Controls.Add(rtb);
                rowClueInputs[rowIndex] = rtb;
            }
        }

        public void InitializeExtraColumnInputs()
        {
            if (colClueInputs != null)
            {
                foreach (RichTextBox rtb in colClueInputs)
                {
                    if (rtb != null)
                    {
                        rtb.Text = "";
                    }
                }
            }

            colClueInputs = new RichTextBox[col];
            colCluesExtra = new List<int>[col];

            int startX = 500;
            int startY = 10;

            for (int colIndex = 0; colIndex < col; colIndex++)
            {
                RichTextBox rtb = new RichTextBox();
                rtb.Size = new Size(cellSize, 140);
                rtb.Location = new Point(startX + colIndex * cellSize, startY);
                rtb.Font = new Font("Arial", 14, FontStyle.Bold);
                rtb.Tag = "col";
                rtb.Enter += ClueTextBox_Enter;
                rtb.Click += ClueTextBox_Click;
                rtb.TextChanged += ClueTextBox_TextChanged;
                rtb.KeyPress += RowClueInput_KeyPress;
                rtb.Multiline = true;
                form.Controls.Add(rtb);
                colClueInputs[colIndex] = rtb;
            }
        }

        private void RowClueInput_KeyPress(object sender, KeyPressEventArgs e)
        {
            RichTextBox rtb = sender as RichTextBox;

            if (rtb == null)
            {
                return; // security check
            }

            // Control characters (e.g. Backspace) are always allowed
            if (char.IsControl(e.KeyChar))
            {
                return;
            }

            // Spaces allowed
            if (e.KeyChar == ' ')
            {
                return;
            }

            // Only digits are allowed
            if (!char.IsDigit(e.KeyChar))
            {
                e.Handled = true;
                return;
            }

            // Check that the user does not start the number with a single 0
            int cursorPos = rtb.SelectionStart;
            string text = rtb.Text;

            // If the inserted digit is 0 and:
            // either the field is empty, or
            // there is a space in front of it (start of a new number)
            if (e.KeyChar == '0' && (cursorPos == 0 || text[cursorPos - 1] == ' '))
            {
                e.Handled = true; // ban
            }

            int maxValue;
            if (rtb.Tag?.ToString() == "row")
            {
                // For rows, we use the current column number
                maxValue = 0;
                for (int colIndex = 0; colIndex < extraButtons.GetLength(1); colIndex++)
                {
                    if (extraButtons[0, colIndex] != null && extraButtons[0, colIndex].Visible)
                    {
                        maxValue++;
                    }
                }
            }
            else
            {
                // For columns, we use the current row number
                maxValue = 0;
                for (int rowIndex = 0; rowIndex < extraButtons.GetLength(0); rowIndex++)
                {
                    if (extraButtons[rowIndex, 0] != null && extraButtons[rowIndex, 0].Visible)
                    {
                        maxValue++;
                    }
                }
            }

            string newText = text.Insert(cursorPos, e.KeyChar.ToString());
            string[] numbers = newText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string num in numbers)
            {
                if (int.TryParse(num, out int value) && value > maxValue)
                {
                    e.Handled = true;
                    System.Media.SystemSounds.Beep.Play();
                    return;
                }
            }
        }

        public bool ReadExtraClues()
        {
            try
            {
                bool hasAnyClue = false;

                for (int rowIndex = 0; rowIndex < row; rowIndex++)
                {
                    rowCluesExtra[rowIndex] = ParseClueLine(rowClueInputs[rowIndex].Text);
                    if (rowCluesExtra[rowIndex].Count > 0)
                    {
                        hasAnyClue = true;
                    }
                }

                for (int colIndex = 0; colIndex < col; colIndex++)
                {
                    colCluesExtra[colIndex] = ParseClueLine(colClueInputs[colIndex].Text);
                    if (colCluesExtra[colIndex].Count > 0)
                    {
                        hasAnyClue = true;
                    }
                }

                if (!hasAnyClue)
                {
                    MessageBox.Show(
                        "You have not entered any numbers in the rows or columns! Please enter at least one.",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Wrong clue", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        private List<int> ParseClueLine(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new List<int>();
            }

            return text
                .Split((char[])null, StringSplitOptions.RemoveEmptyEntries)
                .Select(x =>
                {
                    if (!int.TryParse(x, out int n) || n <= 0)
                    {
                        throw new Exception("Only positive numbers are allowed!");
                    }
                    return n;
                })
                .ToList();
        }

        public int GetCurrentNumberIndex(RichTextBox rtb)
        {
            if (string.IsNullOrWhiteSpace(rtb.Text))
            {
                return 0;
            }

            int cursorPos = rtb.SelectionStart;
            string text = rtb.Text;

            // Numbers and spaces are treated separately
            string[] parts = text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

            int runningIndex = 0;
            for (int partIndex = 0; partIndex < parts.Length; partIndex++)
            {
                int partLength = parts[partIndex].Length;
                if (cursorPos <= runningIndex + partLength)
                {
                    return partIndex;
                }
                runningIndex += partLength + 1;
            }

            return parts.Length - 1;
        }

        public bool PrepareTextBoxColors()
        {
            try
            {
                textBoxColors.Clear();

                // Rows
                for (int rowIndex = 0; rowIndex < row; rowIndex++)
                {
                    RichTextBox rtb = rowClueInputs[rowIndex];
                    List<Color> colors = new List<Color>();

                    if (!string.IsNullOrWhiteSpace(rtb.Text))
                    {
                        string[] parts = rtb.Text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                        int charPos = 0;

                        foreach (string part in parts)
                        {
                            // Take the color of the whole number, not just the last one
                            int start = rtb.Text.IndexOf(part, charPos);
                            Color color = GetCharColor(rtb, start);

                            colors.Add(color);
                            charPos = start + part.Length;
                        }
                    }

                    textBoxColors[rtb] = colors;
                }

                // Columns
                for (int colIndex = 0; colIndex < col; colIndex++)
                {
                    RichTextBox rtb = colClueInputs[colIndex];
                    List<Color> colors = new List<Color>();

                    if (!string.IsNullOrWhiteSpace(rtb.Text))
                    {
                        string[] parts = rtb.Text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                        int charPos = 0;

                        foreach (string part in parts)
                        {
                            // Take the color of the whole number, not just the last one
                            int start = rtb.Text.IndexOf(part, charPos);
                            Color color = GetCharColor(rtb, start);

                            colors.Add(color);
                            charPos = start + part.Length;
                        }
                    }

                    textBoxColors[rtb] = colors;
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Incorrect color data: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        private Color GetCharColor(RichTextBox rtb, int index)
        {
            int originalStart = rtb.SelectionStart;
            int originalLength = rtb.SelectionLength;
            rtb.Select(index, 1);
            Color color = rtb.SelectionColor;
            rtb.Select(originalStart, originalLength);
            return color;
        }

        public bool SolveExtraNonogramSimple(int[,] solution, List<int>[] rowClues, List<int>[] colClues)
        {
            int rowCount = solution.GetLength(0);
            int colCount = solution.GetLength(1);

            // Empty grid all 0 (white)
            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                for (int colIndex = 0; colIndex < colCount; colIndex++)
                {
                    solution[rowIndex, colIndex] = 0;
                }
            }

            return SolveRow(solution, 0, rowClues, colClues);
        }

        public bool SolveExtraNonogramColor(Color[,] solution, List<int>[] rowClues, List<int>[] colClues,
            Dictionary<RichTextBox, List<Color>> textBoxColors)
        {
            int rowCount = solution.GetLength(0);
            int colCount = solution.GetLength(1);

            // All cells are white
            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                for (int colIndex = 0; colIndex < colCount; colIndex++)
                {
                    solution[rowIndex, colIndex] = Color.White;
                }
            }

            return SolveColorRow(solution, 0, rowClues, colClues, textBoxColors);
        }

        private bool SolveRow(int[,] solution, int rowIndex, List<int>[] rowClues, List<int>[] colClues)
        {
            int rowCount = solution.GetLength(0);
            int colCount = solution.GetLength(1);

            if (rowIndex == rowCount)
            {
                return CheckColumns(solution, colClues);
            }

            // Generate all possible row variations based on the given row clue
            List<int[]> possibleRows = GenerateRowPossibilities(rowClues[rowIndex], colCount);

            foreach (int[] rowPattern in possibleRows)
            {
                // temporarily copy the line into the solution
                for (int colIndex = 0; colIndex < colCount; colIndex++)
                {
                    solution[rowIndex, colIndex] = rowPattern[colIndex];
                }

                // check the columns based on the rows filled in so far
                if (CheckColumnsPartial(solution, colClues, rowIndex))
                {
                    if (SolveRow(solution, rowIndex + 1, rowClues, colClues))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // Recursive row and column check for colored Nonogram
        private bool SolveColorRow(Color[,] solution, int rowIndex, List<int>[] rowClues, List<int>[] colClues,
            Dictionary<RichTextBox, List<Color>> textBoxColors)
        {
            int rowCount = solution.GetLength(0);
            int colCount = solution.GetLength(1);

            if (rowIndex == rowCount)
            {
                return CheckColorColumns(solution, colClues, textBoxColors);
            }

            List<int> clues = rowClues[rowIndex];
            List<Color> colors = textBoxColors.ContainsKey(rowClueInputs[rowIndex])
                ? textBoxColors[rowClueInputs[rowIndex]]
                : new List<Color>();

            List<Color[]> rowPatterns = GenerateColorRowPossibilities(clues, colors, colCount);

            foreach (Color[] rowPattern in rowPatterns)
            {
                // temporarily insert the line
                for (int colIndex = 0; colIndex < colCount; colIndex++)
                {
                    solution[rowIndex, colIndex] = rowPattern[colIndex];
                }

                if (CheckColorColumnsPartial(solution, colClues, textBoxColors, rowIndex))
                {
                    if (SolveColorRow(solution, rowIndex + 1, rowClues, colClues, textBoxColors))
                    {
                        return true;
                    }
                }

                // reset the row to white
                for (int colIndex = 0; colIndex < colCount; colIndex++)
                {
                    solution[rowIndex, colIndex] = Color.White;
                }
            }

            return false;
        }

        // Generate all possible lines based on a given clue and line length
        private List<int[]> GenerateRowPossibilities(List<int> clues, int length)
        {
            List<int[]> results = new List<int[]>();
            GenerateRowRecursive(clues, 0, new int[length], 0, results);
            return results;
        }

        // Generate all possible row variations according to colors
        private List<Color[]> GenerateColorRowPossibilities(List<int> clues, List<Color> colors, int length)
        {
            List<Color[]> results = new List<Color[]>();

            Color[] row = new Color[length];
            for (int colIndex = 0; colIndex < length; colIndex++)
            {
                row[colIndex] = Color.White;
            }

            GenerateColorRowRecursive(clues, colors, 0, row, 0, results);
            return results;
        }

        private void GenerateRowRecursive(List<int> clues, int clueIndex, int[] row, int pos, List<int[]> results)
        {
            int length = row.Length;

            if (clueIndex == clues.Count)
            {
                // fill the rest with white
                for (int colIndex = pos; colIndex < length; colIndex++)
                {
                    row[colIndex] = 0;
                }
                results.Add((int[])row.Clone());
                return;
            }

            int blockSize = clues[clueIndex];

            for (int startCol = pos; startCol <= length - blockSize; startCol++)
            {
                // fill the block with black
                for (int offset = 0; offset < blockSize; offset++)
                {
                    row[startCol + offset] = 1;
                }

                // at least 1 whitespace after block, if not at the end of the line
                int nextPos = startCol + blockSize + 1;
                GenerateRowRecursive(clues, clueIndex + 1, row, Math.Min(nextPos, length), results);

                // reset the block to white
                for (int offset = 0; offset < blockSize; offset++)
                {
                    row[startCol + offset] = 0;
                }
            }
        }

        private void GenerateColorRowRecursive(List<int> clues, List<Color> colors, int clueIndex, Color[] row,
            int pos, List<Color[]> results)
        {
            int length = row.Length;

            // When all blocks are done
            if (clueIndex == clues.Count)
            {
                for (int colIndex = pos; colIndex < length; colIndex++)
                {
                    row[colIndex] = Color.White;
                }

                results.Add((Color[])row.Clone());
                return;
            }

            int blockSize = clues[clueIndex];

            // Current block color
            Color currentColor =
                clueIndex < colors.Count && colors[clueIndex] != Color.Empty
                    ? colors[clueIndex]
                    : Color.Black;

            for (int startCol = pos; startCol <= length - blockSize; startCol++)
            {
                // fill block
                for (int offset = 0; offset < blockSize; offset++)
                {
                    row[startCol + offset] = currentColor;
                }

                // Is it mandatory to have white?
                bool needWhiteGap = false;

                if (clueIndex + 1 < clues.Count &&
                    clueIndex + 1 < colors.Count &&
                    colors[clueIndex] != Color.Empty &&
                    colors[clueIndex + 1] != Color.Empty &&
                    colors[clueIndex] == colors[clueIndex + 1])
                {
                    needWhiteGap = true;
                }

                int nextPos = startCol + blockSize + (needWhiteGap ? 1 : 0);

                GenerateColorRowRecursive(clues, colors, clueIndex + 1, row, nextPos, results);

                // reset
                for (int offset = 0; offset < blockSize; offset++)
                {
                    row[startCol + offset] = Color.White;
                }
            }
        }

        // Checks if the columns fully match the clue
        private bool CheckColumns(int[,] solution, List<int>[] colClues)
        {
            int rowCount = solution.GetLength(0);
            int colCount = solution.GetLength(1);

            for (int colIndex = 0; colIndex < colCount; colIndex++)
            {
                List<int> expected = colClues[colIndex];
                List<int> actual = new List<int>();

                int count = 0;
                for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
                {
                    if (solution[rowIndex, colIndex] == 1)
                    {
                        count++;
                    }
                    else if (count > 0)
                    {
                        actual.Add(count);
                        count = 0;
                    }
                }
                if (count > 0)
                {
                    actual.Add(count);
                }

                if (!expected.SequenceEqual(actual))
                {
                    return false;
                }
            }

            return true;
        }

        private bool CheckColorColumns(Color[,] solution, List<int>[] colClues, Dictionary<RichTextBox,
            List<Color>> textBoxColors)
        {
            int rowCount = solution.GetLength(0);
            int colCount = solution.GetLength(1);

            for (int colIndex = 0; colIndex < colCount; colIndex++)
            {
                List<int> clues = colClues[colIndex];
                RichTextBox rtb = colClueInputs[colIndex];
                List<Color> colors = textBoxColors.ContainsKey(rtb) ? textBoxColors[rtb] : new List<Color>();

                List<(int length, Color color)> actualBlocks = new List<(int length, Color color)>();
                int count = 0;
                Color currentColor = Color.Empty;

                for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
                {
                    Color cell = solution[rowIndex, colIndex];

                    if (cell != Color.White)
                    {
                        if (count == 0)
                        {
                            currentColor = cell;
                            count = 1;
                        }
                        else if (cell == currentColor)
                        {
                            count++;
                        }
                        else
                        {
                            actualBlocks.Add((count, currentColor));
                            currentColor = cell;
                            count = 1;
                        }
                    }
                    else if (count > 0)
                    {
                        actualBlocks.Add((count, currentColor));
                        count = 0;
                    }
                }
                if (count > 0)
                {
                    actualBlocks.Add((count, currentColor));
                }

                if (actualBlocks.Count != clues.Count)
                {
                    return false;
                }

                for (int clueIndex = 0; clueIndex < clues.Count; clueIndex++)
                {
                    if (actualBlocks[clueIndex].length != clues[clueIndex])
                    {
                        return false;
                    }
                    if (clueIndex < colors.Count && colors[clueIndex] != actualBlocks[clueIndex].color)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        // Checks that the columns are not partially colliding yet
        private bool CheckColumnsPartial(int[,] solution, List<int>[] colClues, int maxRow)
        {
            int colCount = solution.GetLength(1);

            for (int colIndex = 0; colIndex < colCount; colIndex++)
            {
                List<int> clue = colClues[colIndex];
                int clueIndex = 0;
                int count = 0;

                for (int rowIndex = 0; rowIndex <= maxRow; rowIndex++)
                {
                    if (solution[rowIndex, colIndex] == 1)
                    {
                        count++;
                        if (clueIndex >= clue.Count)
                        {
                            return false;
                        }
                    }
                    else
                    {
                        if (count > 0)
                        {
                            if (count > clue[clueIndex])
                            {
                                return false;
                            }
                            clueIndex++;
                            count = 0;
                        }
                    }
                }
            }

            return true;
        }
        private bool CheckColorColumnsPartial(Color[,] solution, List<int>[] colClues, Dictionary<RichTextBox,
            List<Color>> textBoxColors, int maxRow)
        {
            int rowCount = solution.GetLength(0);
            int colCount = solution.GetLength(1);

            for (int colIndex = 0; colIndex < colCount; colIndex++)
            {
                List<int> clues = colClues[colIndex];
                RichTextBox rtb = colClueInputs[colIndex];
                List<Color> colors = textBoxColors.ContainsKey(rtb) ? textBoxColors[rtb] : new List<Color>();

                int clueIndex = 0;
                int count = 0;
                Color currentColor = Color.Empty;

                for (int rowIndex = 0; rowIndex <= maxRow; rowIndex++)
                {
                    Color cell = solution[rowIndex, colIndex];

                    if (cell != Color.White)
                    {
                        if (count == 0)
                        {
                            currentColor = cell;
                            count = 1;
                        }
                        else if (cell == currentColor)
                        {
                            count++;
                        }
                        else
                        {
                            // Check the length of the block
                            if (clueIndex >= clues.Count)
                            {
                                return false;
                            }

                            if (count > clues[clueIndex])
                            {
                                return false;
                            }

                            // Check the color only if the clue color is not empty
                            if (clueIndex < colors.Count && colors[clueIndex] != Color.Empty)
                            {
                                if (currentColor != colors[clueIndex])
                                {
                                    return false;
                                }
                            }

                            clueIndex++;
                            currentColor = cell;
                            count = 1;
                        }
                    }
                    else
                    {
                        if (count > 0)
                        {
                            if (clueIndex >= clues.Count)
                            {
                                return false;
                            }

                            if (count > clues[clueIndex])
                            {
                                return false;
                            }

                            // Color check based on clue color, only if defined
                            if (clueIndex < colors.Count && colors[clueIndex] != Color.Empty)
                            {
                                if (currentColor != colors[clueIndex])
                                {
                                    return false;
                                }
                            }

                            clueIndex++;
                            count = 0;
                            currentColor = Color.Empty;
                        }
                    }
                }

                // Check the remaining block at the end of the line
                if (count > 0)
                {
                    if (clueIndex >= clues.Count)
                    {
                        return false;
                    }

                    if (count > clues[clueIndex])
                    {
                        return false;
                    }

                    if (clueIndex < colors.Count && colors[clueIndex] != Color.Empty)
                    {
                        if (currentColor != colors[clueIndex])
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        public void ClearAllClueInputs()
        {
            if (extraButtons != null)
            {
                for (int rowIndex = 0; rowIndex < extraButtons.GetLength(0); rowIndex++)
                {
                    for (int colIndex = 0; colIndex < extraButtons.GetLength(1); colIndex++)
                    {
                        if (extraButtons[rowIndex, colIndex] != null)
                        {
                            form.Controls.Remove(extraButtons[rowIndex, colIndex]);
                            extraButtons[rowIndex, colIndex].Dispose();
                            extraButtons[rowIndex, colIndex] = null;
                        }
                    }
                }
                extraButtons = null;
            }
            // Delete rows
            if (rowClueInputs != null)
            {
                for (int rowIndex = 0; rowIndex < rowClueInputs.Length; rowIndex++)
                {
                    if (rowClueInputs[rowIndex] != null)
                    {
                        form.Controls.Remove(rowClueInputs[rowIndex]);
                        rowClueInputs[rowIndex].Dispose();
                        rowClueInputs[rowIndex] = null;
                    }
                }
                rowClueInputs = null;
                rowCluesExtra = null;
            }

            // Delete columns
            if (colClueInputs != null)
            {
                for (int colIndex = 0; colIndex < colClueInputs.Length; colIndex++)
                {
                    if (colClueInputs[colIndex] != null)
                    {
                        form.Controls.Remove(colClueInputs[colIndex]);
                        colClueInputs[colIndex].Dispose();
                        colClueInputs[colIndex] = null;
                    }
                }
                colClueInputs = null;
                colCluesExtra = null;
            }

            // Delete from the color storage as well
            textBoxColors.Clear();
        }

        public void ClearExtraGridContents()
        {
            // Clear the contents of the grid (only the cells)
            if (extraButtons != null)
            {
                for (int rowIndex = 0; rowIndex < extraButtons.GetLength(0); rowIndex++)
                {
                    for (int colIndex = 0; colIndex < extraButtons.GetLength(1); colIndex++)
                    {
                        if (extraButtons[rowIndex, colIndex] != null)
                        {
                            extraButtons[rowIndex, colIndex].BackColor = Color.White;
                            extraButtons[rowIndex, colIndex].ForeColor = Color.Gray;
                            extraButtons[rowIndex, colIndex].Text = "";
                            extraButtons[rowIndex, colIndex].Font = new Font("Arial", cellSize / 2, FontStyle.Bold);
                        }
                    }
                }
            }

            // Clear the contents of the RichTextBox of rows
            if (rowClueInputs != null)
            {
                for (int rowIndex = 0; rowIndex < rowClueInputs.Length; rowIndex++)
                {
                    if (rowClueInputs[rowIndex] != null)
                    {
                        rowClueInputs[rowIndex].Text = "";
                    }
                }

                if (rowCluesExtra != null)
                {
                    for (int rowIndex = 0; rowIndex < rowCluesExtra.Length; rowIndex++)
                    {
                        rowCluesExtra[rowIndex]?.Clear();
                    }
                }
            }

            // Clear the contents of the columns RichTextBox
            if (colClueInputs != null)
            {
                for (int colIndex = 0; colIndex < colClueInputs.Length; colIndex++)
                {
                    if (colClueInputs[colIndex] != null)
                    {
                        colClueInputs[colIndex].Text = "";
                    }
                }

                if (colCluesExtra != null)
                {
                    for (int colIndex = 0; colIndex < colCluesExtra.Length; colIndex++)
                    {
                        colCluesExtra[colIndex]?.Clear();
                    }
                }
            }

            // Empty the color container
            textBoxColors.Clear();

            // Delete solutions
            if (grid.solutionBW != null)
            {
                Array.Clear(grid.solutionBW, 0, grid.solutionBW.Length);
            }

            if (grid.solutionColorRGB != null)
            {
                for (int rowIndex = 0; rowIndex < grid.solutionColorRGB.GetLength(0); rowIndex++)
                {
                    for (int colIndex = 0; colIndex < grid.solutionColorRGB.GetLength(1); colIndex++)
                    {
                        grid.solutionColorRGB[rowIndex, colIndex] = Color.White;
                    }
                }
            }
        }

        public bool IsAnythingToClear()
        {
            // Look at the grid (cells)
            if (extraButtons != null)
            {
                foreach (Button btn in extraButtons)
                {
                    if (btn != null && btn.BackColor != Color.White)
                    {
                        return true;
                    }
                }
            }

            // We look at the input fields of the rows
            if (rowClueInputs != null)
            {
                foreach (RichTextBox rtb in rowClueInputs)
                {
                    if (rtb != null && !string.IsNullOrWhiteSpace(rtb.Text))
                    {
                        return true;
                    }
                }
            }

            // We look at the input fields of the columns
            if (colClueInputs != null)
            {
                foreach (RichTextBox rtb in colClueInputs)
                {
                    if (rtb != null && !string.IsNullOrWhiteSpace(rtb.Text))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}