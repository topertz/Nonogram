using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Nonogram
{
    public class ExtraGridManager
    {
        private Nonogram form;
        private int row;
        private int col;
        public ExtraGridManager(Nonogram parentForm, int rows, int cols, int cellSize)
        {
            form = parentForm;
            row = rows;
            col = cols;
            cellSize = form.userCellSize;
        }

        public void InitializeExtraGrid()
        {
            int gridLeft = 500;
            int gridTop = 300;

            if (form.gridButtons == null)
                form.gridButtons = new Button[row, col];

            for (int i = 0; i < row; i++)
            {
                for (int j = 0; j < col; j++)
                {
                    if (form.gridButtons[i, j] == null)
                    {
                        Button btn = new Button();
                        btn.Size = new Size(form.userCellSize, form.userCellSize);
                        btn.Location = new Point(gridLeft + j * form.userCellSize, gridTop + i * form.userCellSize);
                        btn.BackColor = Color.White;
                        btn.Tag = new Point(i, j);
                        btn.FlatStyle = FlatStyle.Flat;
                        btn.FlatAppearance.BorderColor = Color.Black;
                        btn.FlatAppearance.BorderSize = 1;
                        btn.Enabled = false;
                        form.Controls.Add(btn);
                        form.gridButtons[i, j] = btn;
                    }
                }
            }

            // Inicializáljuk az extraClues mátrixot is
            if (form.extraClues == null)
                form.extraClues = new int[row, col];
        }

        public void InitializeExtraRowInputs()
        {
            if (form.rowClueInputs != null)
            {
                foreach (var rtb in form.rowClueInputs)
                {
                    if (rtb != null)
                        rtb.Text = ""; // törlés
                }
            }

            form.rowClueInputs = new RichTextBox[row];
            form.rowCluesExtra = new List<int>[row];

            int startX = 360;
            int startY = 300;

            for (int i = 0; i < row; i++)
            {
                RichTextBox rtb = new RichTextBox(); // <-- TextBox -> RichTextBox
                rtb.Size = new Size(140, form.userCellSize);
                rtb.Location = new Point(startX, startY + i * form.userCellSize);
                rtb.Font = new Font("Arial", 10, FontStyle.Bold);
                rtb.Enter += form.ClueTextBox_Enter;
                rtb.Click += form.ClueTextBox_Click;
                rtb.TextChanged += form.ClueTextBox_TextChanged;
                form.Controls.Add(rtb);
                form.rowClueInputs[i] = rtb;
            }
        }

        public void InitializeExtraColumnInputs()
        {
            if (form.colClueInputs != null)
            {
                foreach (var rtb in form.colClueInputs)
                {
                    if (rtb != null)
                        rtb.Text = ""; // törlés
                }
            }

            form.colClueInputs = new RichTextBox[col];
            form.colCluesExtra = new List<int>[col];

            int startX = 500;
            int startY = 160;

            for (int j = 0; j < col; j++)
            {
                RichTextBox rtb = new RichTextBox();
                rtb.Size = new Size(form.userCellSize, 140);
                rtb.Location = new Point(startX + j * form.userCellSize, startY);
                rtb.Font = new Font("Arial", 10, FontStyle.Bold);
                rtb.Enter += form.ClueTextBox_Enter;
                rtb.Click += form.ClueTextBox_Click;
                rtb.TextChanged += form.ClueTextBox_TextChanged;
                rtb.Multiline = true;
                form.Controls.Add(rtb);
                form.colClueInputs[j] = rtb;
            }
        }

        public bool ReadExtraClues()
        {
            try
            {
                bool hasAnyClue = false;

                for (int i = 0; i < row; i++)
                {
                    form.rowCluesExtra[i] = ParseClueLine(form.rowClueInputs[i].Text);
                    if (form.rowCluesExtra[i].Count > 0)
                        hasAnyClue = true;
                }

                for (int j = 0; j < col; j++)
                {
                    form.colCluesExtra[j] = ParseClueLine(form.colClueInputs[j].Text);
                    if (form.colCluesExtra[j].Count > 0)
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
                form.textBoxColors.Clear();
                Console.WriteLine("PrepareTextBoxColors: start");

                // Sorok
                for (int i = 0; i < row; i++)
                {
                    var rtb = form.rowClueInputs[i];
                    var colors = new List<Color>();

                    if (!string.IsNullOrWhiteSpace(rtb.Text))
                    {
                        string[] parts = rtb.Text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                        int charPos = 0;

                        foreach (string part in parts)
                        {
                            if (charPos < rtb.TextLength)
                                colors.Add(GetCharColor(rtb, charPos));
                            else
                                colors.Add(Color.Black);

                            charPos += part.Length + 1;
                        }
                    }

                    // Ha üres, hagyjuk üres listának (később a default Color.Black lesz)
                    form.textBoxColors[rtb] = colors;
                }

                // Oszlopok
                for (int j = 0; j < col; j++)
                {
                    var rtb = form.colClueInputs[j];
                    var colors = new List<Color>();

                    if (!string.IsNullOrWhiteSpace(rtb.Text))
                    {
                        string[] parts = rtb.Text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                        int charPos = 0;

                        foreach (string part in parts)
                        {
                            if (charPos < rtb.TextLength)
                                colors.Add(GetCharColor(rtb, charPos));
                            else
                                colors.Add(Color.Black);

                            charPos += part.Length + 1;
                        }
                    }

                    form.textBoxColors[rtb] = colors;
                }

                Console.WriteLine("PrepareTextBoxColors: done");
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
                    if (form.gridButtons[i, j] == null) continue;

                    if (form.solutionBW[i, j] == 1)
                    {
                        form.gridButtons[i, j].BackColor = Color.Black;
                        form.gridButtons[i, j].Text = "";
                    }
                    else
                    {
                        form.gridButtons[i, j].BackColor = Color.White;
                        form.gridButtons[i, j].ForeColor = Color.Gray;
                        form.gridButtons[i, j].Text = "X"; // nem megoldott cella jelzése
                        form.gridButtons[i, j].Font = new Font("Arial", form.userCellSize / 2, FontStyle.Bold);
                    }
                }
            }
        }

        private void ApplySolutionToGridColor()
        {
            for (int i = 0; i < row; i++)
            {
                for (int j = 0; j < col; j++)
                {
                    if (form.gridButtons[i, j] == null) continue;

                    Color cellColor = form.solutionColorRGB[i, j];

                    if (cellColor != Color.White)
                    {
                        form.gridButtons[i, j].BackColor = cellColor;
                        form.gridButtons[i, j].Text = "";
                    }
                    else
                    {
                        form.gridButtons[i, j].BackColor = Color.White;
                        form.gridButtons[i, j].ForeColor = Color.Gray;
                        form.gridButtons[i, j].Text = "X"; // nem megoldott cella jelzése
                        form.gridButtons[i, j].Font = new Font("Arial", form.userCellSize / 2, FontStyle.Bold);
                    }
                }
            }
        }

        public bool SolveNonogram(int[,] solution, List<int>[] rowClues, List<int>[] colClues)
        {
            int rowCount = solution.GetLength(0);
            int colCount = solution.GetLength(1);

            // Üres grid: minden 0 (fehér)
            for (int i = 0; i < rowCount; i++)
                for (int j = 0; j < colCount; j++)
                    solution[i, j] = 0;

            return SolveRow(solution, 0, rowClues, colClues);
        }

        public void SolveExtraNonogramColor()
        {
            form.solutionColorRGB = new Color[row, col];

            // minden fehér
            for (int i = 0; i < row; i++)
                for (int j = 0; j < col; j++)
                    form.solutionColorRGB[i, j] = Color.White;

            if (!SolveColorRow(0))
            {
                MessageBox.Show("A színes Nonogram nem oldható meg!", "Hiba",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ApplySolutionToGridColor();
        }

        public void SolveExtraNonogramSimple()
        {
            form.solutionBW = new int[row, col];

            // Minden cella alapból fehér
            for (int i = 0; i < row; i++)
                for (int j = 0; j < col; j++)
                    form.solutionBW[i, j] = 0;

            // Sorok
            for (int i = 0; i < row; i++)
            {
                var clues = form.rowCluesExtra[i];
                int pos = 0;
                foreach (int block in clues)
                {
                    for (int k = 0; k < block && pos < col; k++)
                        form.solutionBW[i, pos++] = 1;

                    if (pos < col) pos++; // kötelező fehér blokk után
                }
            }

            // Oszlopok
            for (int j = 0; j < col; j++)
            {
                var clues = form.colCluesExtra[j];
                int pos = 0;
                foreach (int block in clues)
                {
                    for (int k = 0; k < block && pos < row; k++)
                        form.solutionBW[pos++, j] = 1;

                    if (pos < row) pos++; // kötelező fehér blokk után
                }
            }
            ApplySolutionToGridBW();
        }

        private bool CheckColorColumnsPartial(int maxRow)
        {
            for (int j = 0; j < col; j++)
            {
                var clues = form.colCluesExtra[j];
                var rtb = form.colClueInputs[j];
                var colors = form.textBoxColors.ContainsKey(rtb) ? form.textBoxColors[rtb] : new List<Color>();

                int clueIndex = 0;
                int count = 0;
                Color currentColor = Color.Empty;

                for (int i = 0; i <= maxRow; i++)
                {
                    Color cell = form.solutionColorRGB[i, j];

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
                            // színváltás → új blokk
                            if (clueIndex >= clues.Count) return false;
                            if (count != clues[clueIndex]) return false;
                            if (clueIndex < colors.Count && colors[clueIndex] != currentColor) return false;

                            clueIndex++;
                            currentColor = cell;
                            count = 1;
                        }
                    }
                    else
                    {
                        if (count > 0)
                        {
                            if (clueIndex >= clues.Count) return false;
                            if (count > clues[clueIndex]) return false;
                            clueIndex++;
                            count = 0;
                        }
                    }
                }

                // folyamatban lévő blokk túl hosszú?
                if (count > 0)
                {
                    if (clueIndex >= clues.Count) return false;
                    if (count > clues[clueIndex]) return false;
                }
            }

            return true;
        }

        // Rekurzív sor- és oszlopellenőrzés színes Nonogramhoz
        private bool SolveColorRow(int rowIndex)
        {
            if (rowIndex == row)
                return true; // minden sor kész

            var clues = form.rowCluesExtra[rowIndex];
            var rtb = form.rowClueInputs[rowIndex];
            var colors = form.textBoxColors.ContainsKey(rtb) ? form.textBoxColors[rtb] : new List<Color>();

            var rowPatterns = GenerateColorRowPossibilities(clues, colors, col);

            foreach (var rowPattern in rowPatterns)
            {
                // ideiglenesen beírjuk a sort
                for (int j = 0; j < col; j++)
                    form.solutionColorRGB[rowIndex, j] = rowPattern[j];

                if (CheckColorColumnsPartial(rowIndex))
                {
                    if (SolveColorRow(rowIndex + 1))
                        return true;
                }

                // ha nem jó, visszaállítjuk fehérre
                for (int j = 0; j < col; j++)
                    form.solutionColorRGB[rowIndex, j] = Color.White;
            }

            return false;
        }

        // Generál minden lehetséges sorvariációt a színek szerint
        private List<Color[]> GenerateColorRowPossibilities(List<int> clues, List<Color> colors, int length)
        {
            var results = new List<Color[]>();
            GenerateColorRowRecursive(clues, colors, 0, new Color[length], 0, results);
            return results;
        }

        private void GenerateColorRowRecursive(List<int> clues, List<Color> colors, int clueIndex, Color[] row, int pos, List<Color[]> results)
        {
            int length = row.Length;

            if (clueIndex == clues.Count)
            {
                for (int i = pos; i < length; i++)
                    row[i] = Color.White; // maradék
                results.Add((Color[])row.Clone());
                return;
            }

            int blockSize = clues[clueIndex];
            // Ha nincs szín, használjunk fekete színt
            Color color = (clueIndex < colors.Count && colors[clueIndex] != Color.Empty) ? colors[clueIndex] : Color.Black;

            for (int i = pos; i <= length - blockSize; i++)
            {
                for (int k = 0; k < blockSize; k++)
                    row[i + k] = color;

                GenerateColorRowRecursive(clues, colors, clueIndex + 1, row, i + blockSize, results);

                for (int k = 0; k < blockSize; k++)
                    row[i + k] = Color.White;
            }
        }

        private bool SolveRow(int[,] solution, int rowIndex, List<int>[] rowClues, List<int>[] colClues)
        {
            int rowCount = solution.GetLength(0);
            int colCount = solution.GetLength(1);

            if (rowIndex == rowCount)
                return CheckColumns(solution, colClues); // minden sor kész, ellenőrizze oszlopokat

            // Generáljuk az összes lehetséges sorvariációt az adott row clue alapján
            var possibleRows = GenerateRowPossibilities(rowClues[rowIndex], colCount);

            foreach (var rowPattern in possibleRows)
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

        // Ellenőrzi, hogy az oszlopok megfelelnek-e teljesen a clue-nak
        private bool CheckColumns(int[,] solution, List<int>[] colClues)
        {
            int rowCount = solution.GetLength(0);
            int colCount = solution.GetLength(1);

            for (int j = 0; j < colCount; j++)
            {
                var expected = colClues[j];
                var actual = new List<int>();

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

        // Ellenőrzi, hogy az oszlopok részben még nem ütköznek
        private bool CheckColumnsPartial(int[,] solution, List<int>[] colClues, int maxRow)
        {
            int colCount = solution.GetLength(1);

            for (int j = 0; j < colCount; j++)
            {
                var clue = colClues[j];
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

        // Generál minden lehetséges sort egy adott clue és sorhossz alapján
        private List<int[]> GenerateRowPossibilities(List<int> clues, int length)
        {
            var results = new List<int[]>();
            GenerateRowRecursive(clues, 0, new int[length], 0, results);
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
                GenerateRowRecursive(clues, clueIndex + 1, row, nextPos - 1 >= length ? length : nextPos - 1, results);

                // visszaállítjuk a blokkot fehérre
                for (int k = 0; k < blockSize; k++)
                    row[i + k] = 0;
            }
        }

        public void ClearAllClueInputs()
        {
            // Sorok törlése
            if (form.rowClueInputs != null)
            {
                for (int i = 0; i < form.rowClueInputs.Length; i++)
                {
                    if (form.rowClueInputs[i] != null)
                    {
                        form.Controls.Remove(form.rowClueInputs[i]);
                        form.rowClueInputs[i].Dispose(); // felszabadítja a memóriát
                        form.rowClueInputs[i] = null;
                    }
                }
                form.rowClueInputs = null;
                form.rowCluesExtra = null;
            }

            // Oszlopok törlése
            if (form.colClueInputs != null)
            {
                for (int j = 0; j < form.colClueInputs.Length; j++)
                {
                    if (form.colClueInputs[j] != null)
                    {
                        form.Controls.Remove(form.colClueInputs[j]);
                        form.colClueInputs[j].Dispose();
                        form.colClueInputs[j] = null;
                    }
                }
                form.colClueInputs = null;
                form.colCluesExtra = null;
            }

            // Törlés a színtárolóból is
            form.textBoxColors.Clear();
        }
    }
}
