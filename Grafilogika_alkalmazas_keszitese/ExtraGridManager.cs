using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Grafilogika_alkalmazas_keszitese
{
    public class ExtraGridManager
    {
        private int row;
        private int col;
        private int cellSize;
        private Button[,] extraButtons;
        public int[,] extraClues;  // a felhasználó által beírt számok
        public RichTextBox[] rowClueInputs;   // sorok
        public RichTextBox[] colClueInputs;   // oszlopok
        public bool suppressTextChanged = false;
        public List<int>[] rowCluesExtra;
        public List<int>[] colCluesExtra;
        public Dictionary<RichTextBox, List<Color>> textBoxColors = new Dictionary<RichTextBox, List<Color>>();
        private RichTextBox currentColorTextBox = null;
        public RichTextBox activeClueTextBox = null;
        private Nonogram form;
        private NonogramGrid grid;
        public ExtraGridManager(Nonogram parentForm, NonogramGrid g, int rows, int cols)
        {
            form = parentForm;
            this.grid = g;
            row = 15;
            col = 15;
            this.cellSize = 35;
        }

        public void BtnSaveClues_Click(object sender, EventArgs e)
        {
            bool hasAnyClue = false;

            // Sor clue-k ellenőrzése
            for (int i = 0; i < row; i++)
            {
                if (!string.IsNullOrWhiteSpace(rowClueInputs[i]?.Text))
                {
                    hasAnyClue = true;
                    break;
                }
            }

            // Ha még nincs clue, oszlopokat is megnézzük
            if (!hasAnyClue)
            {
                for (int j = 0; j < col; j++)
                {
                    if (!string.IsNullOrWhiteSpace(colClueInputs[j]?.Text))
                    {
                        hasAnyClue = true;
                        break;
                    }
                }
            }

            // Ha sehol nincs adat
            if (!hasAnyClue)
            {
                MessageBox.Show(
                    "Nem lehet üres clue-kat menteni!\nÍrj be legalább egy sort vagy oszlopot.",
                    "Hiba",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                return;
            }
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "Nonogram Clues (*.nono)|*.nono";
                sfd.Title = "Clue-k mentése";

                if (sfd.ShowDialog() != DialogResult.OK)
                    return;

                ClueSaveData data = new ClueSaveData
                {
                    Rows = row,
                    Cols = col
                };

                for (int i = 0; i < row; i++)
                    data.RowCluesRtf.Add(rowClueInputs[i]?.Rtf ?? "");

                for (int j = 0; j < col; j++)
                    data.ColCluesRtf.Add(colClueInputs[j]?.Rtf ?? "");

                string json = System.Text.Json.JsonSerializer.Serialize(
                    data,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
                );

                File.WriteAllText(sfd.FileName, json, Encoding.UTF8);

                // Visszajelzés
                MessageBox.Show(
                    "A clue-k sikeresen el lettek mentve!",
                    "Mentés kész",
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
                ofd.Title = "Clue-k betöltése";

                if (ofd.ShowDialog() != DialogResult.OK)
                    return;

                string json = File.ReadAllText(ofd.FileName, Encoding.UTF8);
                ClueSaveData data = System.Text.Json.JsonSerializer.Deserialize<ClueSaveData>(json);

                if (data.Rows != row || data.Cols != col)
                {
                    MessageBox.Show(
                        "A mentett rács mérete nem egyezik!",
                        "Hiba",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                    return;
                }

                suppressTextChanged = true;

                for (int i = 0; i < row; i++)
                    rowClueInputs[i].Rtf = data.RowCluesRtf[i];

                for (int j = 0; j < col; j++)
                    colClueInputs[j].Rtf = data.ColCluesRtf[j];

                suppressTextChanged = false;

                // Színek újraolvasása
                PrepareTextBoxColors();

                // Visszajelzés
                MessageBox.Show(
                    "A clue-k sikeresen be lettek töltve!",
                    "Betöltés kész",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
        }

        public void ClueTextBox_TextChanged(object sender, EventArgs e)
        {
            if (suppressTextChanged) return;

            RichTextBox rtb = sender as RichTextBox;
            if (rtb == null || !textBoxColors.ContainsKey(rtb)) return;

            suppressTextChanged = true;

            int cursorPos = rtb.SelectionStart;

            string[] parts = rtb.Text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            List<Color> colors = textBoxColors[rtb];

            // Színlista frissítése: új szám = fekete
            while (colors.Count < parts.Length)
                colors.Add(Color.Black);

            // Nem piszkáljuk a meglévő színeket
            // Csak a hibás színnel rendelkező részeket javítjuk (például új számok)
            int charPos = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                int start = rtb.Text.IndexOf(part, charPos);
                if (start < 0) continue;

                rtb.SelectionStart = start;
                rtb.SelectionLength = part.Length;

                // Csak új számhoz állítjuk a színt
                if (i >= colors.Count - (parts.Length - colors.Count))
                    rtb.SelectionColor = colors[i];

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
                MessageBox.Show("Kérjük, előbb kattintson arra a mezőre, amelyikbe írni szeretne!",
                                "Nincs kijelölt mező",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                return;
            }

            RichTextBox rtb = activeClueTextBox;

            if (string.IsNullOrWhiteSpace(rtb.Text))
            {
                MessageBox.Show("Kérjük, előbb írjon be legalább egy számot a mezőbe a szín hozzárendelése előtt!",
                                "Nincs adat",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                return;
            }

            using (ColorDialog cd = new ColorDialog())
            {
                if (cd.ShowDialog() == DialogResult.OK)
                {
                    suppressTextChanged = true;

                    if (rtb.SelectionLength == 0)
                    {
                        string[] parts = rtb.Text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0)
                        {
                            int index = GetCurrentNumberIndex(rtb);
                            int charPos = 0;
                            for (int i = 0; i < index; i++)
                                charPos += parts[i].Length + 1;

                            rtb.SelectionStart = charPos;
                            rtb.SelectionLength = parts[index].Length;
                        }
                    }

                    rtb.SelectionColor = cd.Color;

                    if (!textBoxColors.ContainsKey(rtb))
                        textBoxColors[rtb] = new List<Color>();

                    int selectionIndex = GetCurrentNumberIndex(rtb);
                    while (textBoxColors[rtb].Count <= selectionIndex)
                        textBoxColors[rtb].Add(Color.Black);

                    textBoxColors[rtb][selectionIndex] = cd.Color;

                    suppressTextChanged = false;
                }
            }
        }

        public void BtnShowExtraSolution_Click(object sender, EventArgs e)
        {
            if (!ReadExtraClues())
                return;

            if (!form.isColor) // fekete-fehér
            {
                form.solutionBW = new int[row, col];
                bool solvedBW = SolveExtraNonogramSimple(form.solutionBW, rowCluesExtra, colCluesExtra);

                if (!solvedBW)
                {
                    MessageBox.Show("A Nonogram nem oldható meg a megadott clues alapján!", "Hiba", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                ApplySolutionToGridBW();
            }
            else // színes
            {
                if (!PrepareTextBoxColors()) return; // először a színeket összegyűjtjük

                // Meghívjuk a SolveColorNonogram-t, visszatérési értékkel
                form.solutionColorRGB = new Color[row, col]; // inicializáljuk a gridet

                bool solvedColor = SolveExtraNonogramColor(
                    form.solutionColorRGB,
                    rowCluesExtra,
                    colCluesExtra,
                    textBoxColors
                );

                if (!solvedColor)
                {
                    MessageBox.Show("A Nonogram nem oldható meg!", "Hiba",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                ApplySolutionToGridColor(); // alkalmazzuk a gridre
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
            if (!IsAnythingToClear())
            {
                MessageBox.Show("A rács és a mezők üresek, nincs mit törölni!", "Hiba", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            // Ha van mit törölni, csak akkor fut le:
            ClearExtraGridContents();
        }

        private void SetMode()
        {
            grid.ClearGrid();
            ClearAllClueInputs();
            form.ActiveControl = null;
            // Minden control elrejtése egyszerre
            foreach (Control c in form.Controls)
                c.Visible = false;

            // Mindig látható checkboxok
            form.chkExtraMode.Visible = true;
            form.chkColorMode.Visible = true;
            form.chkBlackWhiteMode.Visible = true;
            form.lblWrongCellClicks.Visible = true;
            form.lblWrongColorClicks.Visible = true;
            form.lblHintCount.Visible = true;
            form.lblUndoCount.Visible = true;
            form.lblRedoCount.Visible = true;
            form.chkShowX.Checked = false;
            form.picPreview.Image = null;
            form.picSolutionPreview.Image = null;
            form.cmbMode.SelectedIndex = 0;

            // Extra mód
            if (form.chkExtraMode.Checked)
            {
                form.btnResetExtraGrid.Visible = true;
                form.btnSaveClues.Visible = true;
                form.btnLoadClues.Visible = true;
                form.btnShowExtraSolution.Visible = true;
                form.btnPickColor.Visible = true;
                form.chkBlackWhiteMode.Visible = false;
                form.chkColorMode.Visible = false;
                form.lblWrongCellClicks.Visible = false;
                form.lblWrongColorClicks.Visible = false;
                form.lblHintCount.Visible = false;
                form.lblUndoCount.Visible = false;
                form.lblRedoCount.Visible = false;
                form.cmbMode.SelectedIndex = 1;
                form.isColor = form.cmbMode.SelectedItem?.ToString() == "Színes";
                form.btnPickColor.Visible = form.isColor;

                InitializeExtraRowInputs();
                InitializeExtraColumnInputs();
                InitializeExtraGrid();
            }
            // Import mód (BlackWhite vagy Color)
            else if (form.chkBlackWhiteMode.Checked)
            {
                form.btnSelectImage.Visible = true;
                form.picPreview.Visible = true;
                form.chkShowX.Visible = true;
                form.btnSolve.Visible = true;
                form.chkExtraMode.Visible = false;
                form.chkColorMode.Visible = false;
                form.lblWrongCellClicks.Visible = false;
                form.lblWrongColorClicks.Visible = false;
                form.lblHintCount.Visible = false;
                form.lblUndoCount.Visible = false;
                form.lblRedoCount.Visible = false;
            }
            else if (form.chkColorMode.Checked)
            {
                form.btnSelectImage.Visible = true;
                form.picPreview.Visible = true;
                form.chkShowX.Visible = true;
                form.btnSolve.Visible = true;
                form.chkExtraMode.Visible = false;
                form.chkBlackWhiteMode.Visible = false;
                form.lblWrongCellClicks.Visible = false;
                form.lblWrongColorClicks.Visible = false;
                form.lblHintCount.Visible = false;
                form.lblUndoCount.Visible = false;
                form.lblRedoCount.Visible = false;
            }
            // Normál mód
            else
            {
                form.cmbDifficulty.Visible = true;
                form.cmbMode.Visible = true;
                form.lblUsername.Visible = true;
                form.txtUsername.Visible = true;
                form.btnGenerateRandom.Visible = true;
                form.btnLeaderboard.Visible = true;
                form.btnTips.Visible = true;
            }
        }

        public void ChkExtraMode_CheckedChanged(object sender, EventArgs e)
        {
            SetMode();
        }

        public void ChkBlackWhiteMode_CheckedChanged(object sender, EventArgs e)
        {
            SetMode();
        }

        public void ChkColorMode_CheckedChanged(object sender, EventArgs e)
        {
            SetMode();
        }
        public void InitializeExtraGrid()
        {
            int gridLeft = 500;
            int gridTop = 150;

            if (extraButtons == null)
                extraButtons = new Button[row, col];

            for (int i = 0; i < row; i++)
            {
                for (int j = 0; j < col; j++)
                {
                    if (extraButtons[i, j] == null)
                    {
                        Button btn = new Button();
                        btn.Size = new Size(cellSize, cellSize);
                        btn.Location = new Point(gridLeft + j * cellSize, gridTop + i * cellSize);
                        btn.BackColor = Color.White;
                        btn.Tag = new Point(i, j);
                        btn.FlatStyle = FlatStyle.Flat;
                        btn.FlatAppearance.BorderColor = Color.Black;
                        btn.FlatAppearance.BorderSize = 1;
                        btn.Enabled = false;
                        form.Controls.Add(btn);
                        extraButtons[i, j] = btn;
                    }
                    else
                    {
                        extraButtons[i, j].Visible = true;
                        extraButtons[i, j].BackColor = Color.White;
                    }
                }
            }

            // Inicializáljuk az extraClues mátrixot is
            if (extraClues == null)
                extraClues = new int[row, col];
        }

        public void InitializeExtraRowInputs()
        {
            if (rowClueInputs != null)
            {
                foreach (RichTextBox rtb in rowClueInputs)
                {
                    if (rtb != null)
                        rtb.Text = ""; // törlés
                }
            }

            rowClueInputs = new RichTextBox[row];
            rowCluesExtra = new List<int>[row];

            int startX = 360;
            int startY = 150;

            for (int i = 0; i < row; i++)
            {
                RichTextBox rtb = new RichTextBox(); // <-- TextBox -> RichTextBox
                rtb.Size = new Size(140, cellSize);
                rtb.Location = new Point(startX, startY + i * cellSize);
                rtb.Font = new Font("Arial", 14, FontStyle.Bold);
                rtb.Enter += ClueTextBox_Enter;
                rtb.Click += ClueTextBox_Click;
                rtb.TextChanged += ClueTextBox_TextChanged;
                rtb.KeyPress += RowClueInput_KeyPress;
                form.Controls.Add(rtb);
                rowClueInputs[i] = rtb;
            }
        }

        public void InitializeExtraColumnInputs()
        {
            if (colClueInputs != null)
            {
                foreach (RichTextBox rtb in colClueInputs)
                {
                    if (rtb != null)
                        rtb.Text = ""; // törlés
                }
            }

            colClueInputs = new RichTextBox[col];
            colCluesExtra = new List<int>[col];

            int startX = 500;
            int startY = 10;

            for (int j = 0; j < col; j++)
            {
                RichTextBox rtb = new RichTextBox();
                rtb.Size = new Size(cellSize, 140);
                rtb.Location = new Point(startX + j * cellSize, startY);
                rtb.Font = new Font("Arial", 14, FontStyle.Bold);
                rtb.Enter += ClueTextBox_Enter;
                rtb.Click += ClueTextBox_Click;
                rtb.TextChanged += ClueTextBox_TextChanged;
                rtb.KeyPress += RowClueInput_KeyPress;
                rtb.Multiline = true;
                form.Controls.Add(rtb);
                colClueInputs[j] = rtb;
            }
        }

        private void RowClueInput_KeyPress(object sender, KeyPressEventArgs e)
        {
            // Ha nem vezérlő karakter, nem szám, vagy a szám 0, és nem szóköz, tiltjuk
            if ((!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) || e.KeyChar == '0')
                && e.KeyChar != ' ')
            {
                e.Handled = true; // tiltjuk
            }
        }

        public bool ReadExtraClues()
        {
            try
            {
                bool hasAnyClue = false;

                for (int i = 0; i < row; i++)
                {
                    rowCluesExtra[i] = ParseClueLine(rowClueInputs[i].Text);
                    if (rowCluesExtra[i].Count > 0)
                        hasAnyClue = true;
                }

                for (int j = 0; j < col; j++)
                {
                    colCluesExtra[j] = ParseClueLine(colClueInputs[j].Text);
                    if (colCluesExtra[j].Count > 0)
                        hasAnyClue = true;
                }

                if (!hasAnyClue)
                {
                    MessageBox.Show(
                        "Nem adott meg egyetlen számot sem a sorokban vagy oszlopokban! Kérlek, írj be legalább egyet.",
                        "Hiba",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Hibás clue", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        private List<int> ParseClueLine(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<int>();

            return text
                .Split((char[])null, StringSplitOptions.RemoveEmptyEntries)
                .Select(x =>
                {
                    if (!int.TryParse(x, out int n) || n <= 0)
                        throw new Exception("Csak pozitív számok engedélyezettek!");
                    return n;
                })
                .ToList();
        }

        public int GetCurrentNumberIndex(RichTextBox rtb)
        {
            if (string.IsNullOrWhiteSpace(rtb.Text))
                return 0;

            int cursorPos = rtb.SelectionStart;
            string text = rtb.Text;

            // Számokat és szóközöket külön kezeljük
            string[] parts = text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

            int runningIndex = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                int partLength = parts[i].Length;
                if (cursorPos <= runningIndex + partLength)
                    return i; // a kurzor ebben a számban van
                runningIndex += partLength + 1; // +1 a szóköz miatt
            }

            return parts.Length - 1; // ha a kurzor a végén van, az utolsó számhoz tartozik
        }

        public bool PrepareTextBoxColors()
        {
            try
            {
                textBoxColors.Clear();

                // Sorok
                for (int i = 0; i < row; i++)
                {
                    RichTextBox rtb = rowClueInputs[i];
                    List<Color> colors = new List<Color>();

                    if (!string.IsNullOrWhiteSpace(rtb.Text))
                    {
                        string[] parts = rtb.Text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                        int charPos = 0;

                        foreach (string part in parts)
                        {
                            // Vegyük az egész szám színét, ne csak az utolsót
                            int start = rtb.Text.IndexOf(part, charPos);
                            Color c = GetCharColor(rtb, start);

                            colors.Add(c);
                            charPos = start + part.Length;
                        }
                    }

                    textBoxColors[rtb] = colors;
                }

                // Oszlopok
                for (int j = 0; j < col; j++)
                {
                    RichTextBox rtb = colClueInputs[j];
                    List<Color> colors = new List<Color>();

                    if (!string.IsNullOrWhiteSpace(rtb.Text))
                    {
                        string[] parts = rtb.Text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                        int charPos = 0;

                        foreach (string part in parts)
                        {
                            // Vegyük az egész szám színét, ne csak az utolsót
                            int start = rtb.Text.IndexOf(part, charPos);
                            Color c = GetCharColor(rtb, start);

                            colors.Add(c);
                            charPos = start + part.Length;
                        }
                    }

                    textBoxColors[rtb] = colors;
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hibás szín adat: " + ex.Message, "Hiba", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        private Color GetCharColor(RichTextBox rtb, int index)
        {
            int originalStart = rtb.SelectionStart;
            int originalLength = rtb.SelectionLength;

            rtb.Select(index, 1);
            Color c = rtb.SelectionColor;

            // Visszaállítjuk a kurzort
            rtb.Select(originalStart, originalLength);

            return c;
        }

        public void ApplySolutionToGridBW()
        {
            for (int i = 0; i < row; i++)
            {
                for (int j = 0; j < col; j++)
                {
                    if (extraButtons[i, j] == null) continue;

                    if (form.solutionBW[i, j] == 1)
                    {
                        extraButtons[i, j].BackColor = Color.Black;
                        extraButtons[i, j].Text = "";
                    }
                    else
                    {
                        extraButtons[i, j].BackColor = Color.White;
                        extraButtons[i, j].ForeColor = Color.Gray;
                        extraButtons[i, j].Text = "X"; // nem megoldott cella jelzése
                        extraButtons[i, j].Font = new Font("Arial", cellSize / 2, FontStyle.Bold);
                    }
                }
            }
        }

        public void ApplySolutionToGridColor()
        {
            for (int i = 0; i < row; i++)
            {
                for (int j = 0; j < col; j++)
                {
                    if (extraButtons[i, j] == null) continue;

                    Color cellColor = form.solutionColorRGB[i, j];

                    if (cellColor != Color.White)
                    {
                        extraButtons[i, j].BackColor = cellColor;
                        extraButtons[i, j].Text = "";
                    }
                    else
                    {
                        extraButtons[i, j].BackColor = Color.White;
                        extraButtons[i, j].ForeColor = Color.Gray;
                        extraButtons[i, j].Text = "X"; // nem megoldott cella jelzése
                        extraButtons[i, j].Font = new Font("Arial", cellSize / 2, FontStyle.Bold);
                    }
                }
            }
        }

        public bool SolveExtraNonogramSimple(int[,] solution, List<int>[] rowClues, List<int>[] colClues)
        {
            int rowCount = solution.GetLength(0);
            int colCount = solution.GetLength(1);

            // Üres grid: minden 0 (fehér)
            for (int i = 0; i < rowCount; i++)
                for (int j = 0; j < colCount; j++)
                    solution[i, j] = 0;

            return SolveRow(solution, 0, rowClues, colClues);
        }

        public bool SolveExtraNonogramColor(Color[,] solution, List<int>[] rowClues, List<int>[] colClues, Dictionary<RichTextBox, List<Color>> textBoxColors)
        {
            int rowCount = solution.GetLength(0);
            int colCount = solution.GetLength(1);

            // Minden cella fehér
            for (int i = 0; i < rowCount; i++)
                for (int j = 0; j < colCount; j++)
                    solution[i, j] = Color.White;

            return SolveColorRow(solution, 0, rowClues, colClues, textBoxColors);
        }

        private bool SolveRow(int[,] solution, int rowIndex, List<int>[] rowClues, List<int>[] colClues)
        {
            int rowCount = solution.GetLength(0);
            int colCount = solution.GetLength(1);

            if (rowIndex == rowCount)
                return CheckColumns(solution, colClues); // minden sor kész, ellenőrizze oszlopokat

            // Generáljuk az összes lehetséges sorvariációt az adott row clue alapján
            List<int[]> possibleRows = GenerateRowPossibilities(rowClues[rowIndex], colCount);

            foreach (int[] rowPattern in possibleRows)
            {
                // ideiglenesen másoljuk a sort a megoldásba
                for (int j = 0; j < colCount; j++)
                    solution[rowIndex, j] = rowPattern[j];

                // ellenőrizzük az oszlopokat az eddig kitöltött sorok alapján
                if (CheckColumnsPartial(solution, colClues, rowIndex))
                {
                    if (SolveRow(solution, rowIndex + 1, rowClues, colClues))
                        return true;
                }
            }

            return false; // nem sikerült megoldani ezen a soron
        }

        // Rekurzív sor- és oszlopellenőrzés színes Nonogramhoz
        private bool SolveColorRow(Color[,] solution, int rowIndex, List<int>[] rowClues, List<int>[] colClues, Dictionary<RichTextBox, List<Color>> textBoxColors)
        {
            int rowCount = solution.GetLength(0);
            int colCount = solution.GetLength(1);

            if (rowIndex == rowCount)
                return CheckColorColumns(solution, colClues, textBoxColors);

            List<int> clues = rowClues[rowIndex];
            List<Color> colors = textBoxColors.ContainsKey(rowClueInputs[rowIndex])
                ? textBoxColors[rowClueInputs[rowIndex]]
                : new List<Color>();

            List<Color[]> rowPatterns = GenerateColorRowPossibilities(clues, colors, colCount);

            foreach (Color[] rowPattern in rowPatterns)
            {
                // ideiglenesen beírjuk a sort
                for (int j = 0; j < colCount; j++)
                    solution[rowIndex, j] = rowPattern[j];

                if (CheckColorColumnsPartial(solution, colClues, textBoxColors, rowIndex))
                {
                    if (SolveColorRow(solution, rowIndex + 1, rowClues, colClues, textBoxColors))
                        return true;
                }

                // visszaállítjuk a sort fehérre
                for (int j = 0; j < colCount; j++)
                    solution[rowIndex, j] = Color.White;
            }

            return false;
        }

        // Generál minden lehetséges sort egy adott clue és sorhossz alapján
        private List<int[]> GenerateRowPossibilities(List<int> clues, int length)
        {
            List<int[]> results = new List<int[]>();
            GenerateRowRecursive(clues, 0, new int[length], 0, results);
            return results;
        }

        // Generál minden lehetséges sorvariációt a színek szerint
        private List<Color[]> GenerateColorRowPossibilities(
    List<int> clues,
    List<Color> colors,
    int length)
        {
            List<Color[]> results = new List<Color[]>();

            Color[] row = new Color[length];
            for (int i = 0; i < length; i++)
                row[i] = Color.White;   // KRITIKUS SOR

            GenerateColorRowRecursive(clues, colors, 0, row, 0, results);
            return results;
        }

        private void GenerateRowRecursive(List<int> clues, int clueIndex, int[] row, int pos, List<int[]> results)
        {
            int length = row.Length;

            if (clueIndex == clues.Count)
            {
                // kitöltjük a maradékot fehérrel
                for (int i = pos; i < length; i++)
                    row[i] = 0;
                results.Add((int[])row.Clone());
                return;
            }

            int blockSize = clues[clueIndex];

            for (int i = pos; i <= length - blockSize; i++)
            {
                // blokkot feketével kitöltjük
                for (int k = 0; k < blockSize; k++)
                    row[i + k] = 1;

                // blokk után legalább 1 fehér, ha nem a sor végén
                int nextPos = i + blockSize + 1;
                GenerateRowRecursive(clues, clueIndex + 1, row, Math.Min(nextPos, length), results);

                // visszaállítjuk a blokkot fehérre
                for (int k = 0; k < blockSize; k++)
                    row[i + k] = 0;
            }
        }

        private void GenerateColorRowRecursive(
    List<int> clues,
    List<Color> colors,
    int clueIndex,
    Color[] row,
    int pos,
    List<Color[]> results)
        {
            int length = row.Length;

            // Ha minden blokk kész
            if (clueIndex == clues.Count)
            {
                for (int i = pos; i < length; i++)
                    row[i] = Color.White;

                results.Add((Color[])row.Clone());
                return;
            }

            int blockSize = clues[clueIndex];

            // Aktuális blokk színe
            Color currentColor =
                clueIndex < colors.Count && colors[clueIndex] != Color.Empty
                    ? colors[clueIndex]
                    : Color.Black;

            for (int i = pos; i <= length - blockSize; i++)
            {
                // blokk kitöltése
                for (int k = 0; k < blockSize; k++)
                    row[i + k] = currentColor;

                // Kell-e kötelező fehér?
                bool needWhiteGap = false;

                if (clueIndex + 1 < clues.Count &&
                    clueIndex + 1 < colors.Count &&
                    colors[clueIndex] != Color.Empty &&
                    colors[clueIndex + 1] != Color.Empty &&
                    colors[clueIndex] == colors[clueIndex + 1])
                {
                    needWhiteGap = true;
                }

                int nextPos = i + blockSize + (needWhiteGap ? 1 : 0);

                GenerateColorRowRecursive(
                    clues,
                    colors,
                    clueIndex + 1,
                    row,
                    nextPos,
                    results
                );

                // visszaállítás
                for (int k = 0; k < blockSize; k++)
                    row[i + k] = Color.White;
            }
        }

        // Ellenőrzi, hogy az oszlopok megfelelnek-e teljesen a clue-nak
        private bool CheckColumns(int[,] solution, List<int>[] colClues)
        {
            int rowCount = solution.GetLength(0);
            int colCount = solution.GetLength(1);

            for (int j = 0; j < colCount; j++)
            {
                List<int> expected = colClues[j];
                List<int> actual = new List<int>();

                int count = 0;
                for (int i = 0; i < rowCount; i++)
                {
                    if (solution[i, j] == 1)
                        count++;
                    else if (count > 0)
                    {
                        actual.Add(count);
                        count = 0;
                    }
                }
                if (count > 0)
                    actual.Add(count);

                if (!expected.SequenceEqual(actual))
                    return false;
            }

            return true;
        }

        private bool CheckColorColumns(Color[,] solution, List<int>[] colClues, Dictionary<RichTextBox, List<Color>> textBoxColors)
        {
            int rowCount = solution.GetLength(0);
            int colCount = solution.GetLength(1);

            for (int j = 0; j < colCount; j++)
            {
                List<int> clues = colClues[j];
                RichTextBox rtb = colClueInputs[j];
                List<Color> colors = textBoxColors.ContainsKey(rtb) ? textBoxColors[rtb] : new List<Color>();

                List<(int length, Color color)> actualBlocks = new List<(int length, Color color)>();
                int count = 0;
                Color currentColor = Color.Empty;

                for (int i = 0; i < rowCount; i++)
                {
                    Color cell = solution[i, j];

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
                    actualBlocks.Add((count, currentColor));

                if (actualBlocks.Count != clues.Count)
                    return false;

                for (int k = 0; k < clues.Count; k++)
                {
                    if (actualBlocks[k].length != clues[k])
                        return false;
                    if (k < colors.Count && colors[k] != actualBlocks[k].color)
                        return false;
                }
            }

            return true;
        }

        // Ellenőrzi, hogy az oszlopok részben még nem ütköznek
        private bool CheckColumnsPartial(int[,] solution, List<int>[] colClues, int maxRow)
        {
            int colCount = solution.GetLength(1);

            for (int j = 0; j < colCount; j++)
            {
                List<int> clue = colClues[j];
                int clueIndex = 0;
                int count = 0;

                for (int i = 0; i <= maxRow; i++)
                {
                    if (solution[i, j] == 1)
                    {
                        count++;
                        if (clueIndex >= clue.Count) return false; // túl sok blokk
                    }
                    else
                    {
                        if (count > 0)
                        {
                            if (count > clue[clueIndex]) return false; // túl hosszú blokk
                            clueIndex++;
                            count = 0;
                        }
                    }
                }
            }

            return true;
        }
        private bool CheckColorColumnsPartial(Color[,] solution, List<int>[] colClues, Dictionary<RichTextBox, List<Color>> textBoxColors, int maxRow)
        {
            int rowCount = solution.GetLength(0);
            int colCount = solution.GetLength(1);

            for (int j = 0; j < colCount; j++)
            {
                List<int> clues = colClues[j];
                RichTextBox rtb = colClueInputs[j];
                List<Color> colors = textBoxColors.ContainsKey(rtb) ? textBoxColors[rtb] : new List<Color>();

                int clueIndex = 0;
                int count = 0;
                Color currentColor = Color.Empty;

                for (int i = 0; i <= maxRow; i++)
                {
                    Color cell = solution[i, j];

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
                            // Ellenőrizzük a blokk hosszát
                            if (clueIndex >= clues.Count)
                            {
                                return false;
                            }

                            if (count > clues[clueIndex])
                            {
                                return false;
                            }

                            // Ellenőrizzük a színt csak akkor, ha a clue színe nem Empty
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

                            // Színellenőrzés a clue szín alapján, csak ha van meghatározva
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

                // Ellenőrizni a maradék blokkot a sor végén
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
                for (int i = 0; i < extraButtons.GetLength(0); i++)
                {
                    for (int j = 0; j < extraButtons.GetLength(1); j++)
                    {
                        if (extraButtons[i, j] != null)
                        {
                            form.Controls.Remove(extraButtons[i, j]);
                            extraButtons[i, j].Dispose();
                            extraButtons[i, j] = null;
                        }
                    }
                }
                extraButtons = null;
            }
            // Sorok törlése
            if (rowClueInputs != null)
            {
                for (int i = 0; i < rowClueInputs.Length; i++)
                {
                    if (rowClueInputs[i] != null)
                    {
                        form.Controls.Remove(rowClueInputs[i]);
                        rowClueInputs[i].Dispose(); // felszabadítja a memóriát
                        rowClueInputs[i] = null;
                    }
                }
                rowClueInputs = null;
                rowCluesExtra = null;
            }

            // Oszlopok törlése
            if (colClueInputs != null)
            {
                for (int j = 0; j < colClueInputs.Length; j++)
                {
                    if (colClueInputs[j] != null)
                    {
                        form.Controls.Remove(colClueInputs[j]);
                        colClueInputs[j].Dispose();
                        colClueInputs[j] = null;
                    }
                }
                colClueInputs = null;
                colCluesExtra = null;
            }

            // Törlés a színtárolóból is
            textBoxColors.Clear();
        }

        public void ClearExtraGridContents()
        {
            // Grid tartalmának törlése (csak cellák)
            if (extraButtons != null)
            {
                for (int i = 0; i < extraButtons.GetLength(0); i++)
                {
                    for (int j = 0; j < extraButtons.GetLength(1); j++)
                    {
                        if (extraButtons[i, j] != null)
                        {
                            extraButtons[i, j].BackColor = Color.White;
                            extraButtons[i, j].ForeColor = Color.Gray;
                            extraButtons[i, j].Text = "";
                            extraButtons[i, j].Font = new Font("Arial", cellSize / 2, FontStyle.Bold);
                        }
                    }
                }
            }

            // Sorok RichTextBox tartalmának törlése
            if (rowClueInputs != null)
            {
                for (int i = 0; i < rowClueInputs.Length; i++)
                {
                    if (rowClueInputs[i] != null)
                        rowClueInputs[i].Text = "";
                }

                if (rowCluesExtra != null)
                {
                    for (int i = 0; i < rowCluesExtra.Length; i++)
                        rowCluesExtra[i]?.Clear();
                }
            }

            // Oszlopok RichTextBox tartalmának törlése
            if (colClueInputs != null)
            {
                for (int j = 0; j < colClueInputs.Length; j++)
                {
                    if (colClueInputs[j] != null)
                        colClueInputs[j].Text = "";
                }

                if (colCluesExtra != null)
                {
                    for (int j = 0; j < colCluesExtra.Length; j++)
                        colCluesExtra[j]?.Clear();
                }
            }

            // Színtároló ürítése
            textBoxColors.Clear();

            // Extra megoldások is törölhetők
            if (form.solutionBW != null)
                Array.Clear(form.solutionBW, 0, form.solutionBW.Length);

            if (form.solutionColorRGB != null)
            {
                for (int i = 0; i < form.solutionColorRGB.GetLength(0); i++)
                    for (int j = 0; j < form.solutionColorRGB.GetLength(1); j++)
                        form.solutionColorRGB[i, j] = Color.White;
            }
        }

        public bool IsAnythingToClear()
        {
            // Megnézzük a rácsot (cellákat)
            if (extraButtons != null)
            {
                foreach (Button btn in extraButtons)
                {
                    if (btn != null && btn.BackColor != Color.White)
                        return true; // Találtunk befestett cellát
                }
            }

            // Megnézzük a sorok beviteli mezőit
            if (rowClueInputs != null)
            {
                foreach (RichTextBox rtb in rowClueInputs)
                {
                    if (rtb != null && !string.IsNullOrWhiteSpace(rtb.Text))
                        return true; // Találtunk beírt számot
                }
            }

            // Megnézzük az oszlopok beviteli mezőit
            if (colClueInputs != null)
            {
                foreach (RichTextBox rtb in colClueInputs)
                {
                    if (rtb != null && !string.IsNullOrWhiteSpace(rtb.Text))
                        return true; // Találtunk beírt számot
                }
            }

            return false; // Minden üres
        }
    }
}