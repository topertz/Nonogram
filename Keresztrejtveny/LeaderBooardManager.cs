using Nonogram;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace Nonogram
{
    public partial class LeaderboardManager
    {
        private Nonogram form;
        public LeaderboardManager(Nonogram f)
        {
            this.form = f;
        }
        public DataTable CreateTable()
        {
            DataTable table = new DataTable("Leaderboard");

            table.Columns.Add("Játékos", typeof(string));
            table.Columns.Add("Nehézség", typeof(string));
            table.Columns.Add("Játékmód", typeof(string));
            table.Columns.Add("Helytelen cella kattintások száma", typeof(int));
            table.Columns.Add("Helytelen színek száma", typeof(int));
            table.Columns.Add("Segítségek száma", typeof(int));
            table.Columns.Add("Eltelt idő (s)", typeof(string));

            return table;
        }

        public void AddSaveToTable(DataTable table, NonogramSaveData save)
        {
            int score = CalculateScore(save);
            int minutes = save.ElapsedSeconds / 60;
            int seconds = save.ElapsedSeconds % 60;
            string timeFormatted = $"{minutes:D2}:{seconds:D2}"; // pl. 00:35, 01:00

            table.Rows.Add(
                save.Username,
                save.Difficulty,
                save.Mode,
                save.WrongCellClicks,
                save.Mode == "Színes" ? save.WrongColorClicks : (int?)null,
                save.HintCount,
                timeFormatted
            );
        }

        private int CalculateScore(NonogramSaveData save)
        {
            int baseScore = 0;

            switch (save.Difficulty)
            {
                case "Könnyű":
                    baseScore = 1000;
                    break;
                case "Közepes":
                    baseScore = 2000;
                    break;
                case "Nehéz":
                    baseScore = 3000;
                    break;
                default:
                    baseScore = 0;
                    break;
            }

            if (save.Mode == "Színes")
                baseScore += 500;

            baseScore -= save.WrongCellClicks * 100;
            baseScore -= save.HintCount * 150;

            return baseScore;
        }

        public DataTable LoadAllSaves(string folderPath)
        {
            DataTable table = CreateTable();
            JsonSerializerOptions options = new JsonSerializerOptions();
            List<NonogramSaveData> saves = new List<NonogramSaveData>();

            foreach (string file in Directory.GetFiles(folderPath, "*.json"))
            {
                string json = File.ReadAllText(file);

                // Deserialize a list
                List<NonogramSaveData> fileSaves = JsonSerializer.Deserialize<List<NonogramSaveData>>(json, options);
                if (fileSaves != null)
                    saves.AddRange(fileSaves);
            }

            // Rangsorolás: idő, segítség, hibák
            List<NonogramSaveData> sorted = saves
                .OrderBy(s => s.ElapsedSeconds)
                .ThenBy(s => s.HintCount)
                .ThenBy(s => s.WrongCellClicks)
                .ToList();

            foreach (NonogramSaveData save in sorted)
                AddSaveToTable(table, save);

            return table;
        }

        public void ShowLeaderboard(DataTable table)
        {
            form.f = new Form();
            form.f.Text = "Ranglista";
            form.f.Size = new Size(800, 500);

            // Label a nehézség combo fölé
            Label lblDifficulty = new Label();
            lblDifficulty.Text = "Nehézség:";
            lblDifficulty.Location = new Point(20, 0);
            lblDifficulty.AutoSize = true;
            form.f.Controls.Add(lblDifficulty);

            // Szűrő ComboBox a nehézséghez
            form.cmbDifficultyFilter = new ComboBox();
            form.cmbDifficultyFilter.Items.Add("Összes");
            form.cmbDifficultyFilter.Items.AddRange(new string[] { "Könnyű", "Közepes", "Nehéz" });
            form.cmbDifficultyFilter.SelectedIndex = 0;
            form.cmbDifficultyFilter.Location = new Point(20, lblDifficulty.Bottom + 3);
            form.f.Controls.Add(form.cmbDifficultyFilter);

            // Label a játékmód combo fölé
            Label lblMode = new Label();
            lblMode.Text = "Játékmód:";
            lblMode.Location = new Point(200, 0);
            lblMode.AutoSize = true;
            form.f.Controls.Add(lblMode);

            // Szűrő ComboBox a játékmódhoz
            form.cmbModeFilter = new ComboBox();
            form.cmbModeFilter.Items.Add("Összes");
            form.cmbModeFilter.Items.AddRange(new string[] { "Fekete-fehér", "Színes" });
            form.cmbModeFilter.SelectedIndex = 0;
            form.cmbModeFilter.Location = new Point(200, lblMode.Bottom + 3);
            form.f.Controls.Add(form.cmbModeFilter);

            // BindingSource a szűréshez
            form.bs = new BindingSource();
            form.bs.DataSource = table;

            // DataGridView
            DataGridView dgv = new DataGridView();
            dgv.Top = form.cmbDifficultyFilter.Bottom + 10;
            dgv.Left = 20;
            dgv.Width = form.f.ClientSize.Width - 40;
            dgv.Height = form.f.ClientSize.Height - dgv.Top - 20;
            dgv.ReadOnly = true;
            dgv.AutoGenerateColumns = false; // NE automatikus generálás
            dgv.AllowUserToAddRows = false;
            dgv.DataSource = form.bs;
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgv.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            // Kijelölés kikapcsolása
            dgv.CurrentCell = null;
            dgv.ClearSelection();
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.MultiSelect = false;
            // Ha bármikor újrarenderelünk, mindig tartsuk tisztán a kijelölést
            form.bs.ListChanged += (s, e) =>
            {
                dgv.CurrentCell = null;
                dgv.ClearSelection();
            };

            // Oszlopok létrehozása kézzel
            dgv.Columns.Clear();
            dgv.Columns.Add("Játékos", "Játékos");
            dgv.Columns["Játékos"].DataPropertyName = "Játékos";
            dgv.Columns["Játékos"].HeaderCell.Style.Padding = new Padding(15, 0, 0, 0);

            dgv.Columns.Add("Nehézség", "Nehézség");
            dgv.Columns["Nehézség"].DataPropertyName = "Nehézség";
            dgv.Columns["Nehézség"].HeaderCell.Style.Padding = new Padding(15, 0, 0, 0);

            dgv.Columns.Add("Játékmód", "Játékmód");
            dgv.Columns["Játékmód"].DataPropertyName = "Játékmód";
            dgv.Columns["Játékmód"].HeaderCell.Style.Padding = new Padding(15, 0, 0, 0);

            dgv.Columns.Add("Helytelen cella kattintások száma", "Helytelen cella kattintások száma");
            dgv.Columns["Helytelen cella kattintások száma"].DataPropertyName = "Helytelen cella kattintások száma";

            // Csak színeshez jelenítjük meg
            DataGridViewTextBoxColumn colWrongColor = new DataGridViewTextBoxColumn();
            colWrongColor.Name = "Helytelen színek száma";
            colWrongColor.HeaderText = "Helytelen színek száma";
            colWrongColor.DataPropertyName = "Helytelen színek száma";
            colWrongColor.Visible = form.bs.Cast<DataRowView>().Any(r => r["Játékmód"].ToString() == "Színes");
            dgv.Columns.Add(colWrongColor);

            dgv.Columns.Add("Segítségek száma", "Segítségek száma");
            dgv.Columns["Segítségek száma"].DataPropertyName = "Segítségek száma";

            dgv.Columns.Add("Eltelt idő (s)", "Eltelt idő (s)");
            dgv.Columns["Eltelt idő (s)"].DataPropertyName = "Eltelt idő (s)";
            dgv.Columns["Eltelt idő (s)"].HeaderCell.Style.Padding = new Padding(15, 0, 0, 0);

            form.f.Controls.Add(dgv);
            form.dgvLeaderboard = dgv; // ha máshol is használod

            ApplyFilter();
            UpdateWrongColorColumnVisibility();
            UpdateGameModeColumnVisibility();
            SetupComboBoxDefaultHighlight(form.cmbDifficultyFilter);
            SetupComboBoxDefaultHighlight(form.cmbModeFilter);
            form.f.Shown += (s, e) =>
            {
                dgv.CurrentCell = null;
                dgv.ClearSelection();
                form.f.ActiveControl = null;
                foreach (DataGridViewColumn col in dgv.Columns) 
                { 
                    col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter; 
                }
            };
            form.f.ShowDialog();
        }

        // Szűrés logika
        private void ApplyFilter()
        {
            string diff = form.cmbDifficultyFilter.SelectedItem?.ToString() ?? "Összes";
            string mode = form.cmbModeFilter.SelectedItem?.ToString() ?? "Összes";

            string filter = "";

            if (diff != "Összes")
                filter += $"[Nehézség] = '{diff}'";

            if (mode != "Összes")
            {
                if (filter != "") filter += " AND ";
                filter += $"[Játékmód] = '{mode}'";
            }

            form.bs.Filter = filter;
            form.cmbDifficultyFilter.SelectedIndexChanged += (s, e) => ApplyFilter();
            form.cmbModeFilter.SelectedIndexChanged += (s, e) => ApplyFilter();
        }

        private void UpdateWrongColorColumnVisibility()
        {
            if (form.dgvLeaderboard == null) return;

            bool show = false;

            foreach (DataRowView row in form.bs)
            {
                if (row["Játékmód"].ToString() == "Színes")
                {
                    show = true;
                    break;
                }
            }

            form.dgvLeaderboard.Columns["Helytelen színek száma"].Visible = show;
        }

        private void UpdateGameModeColumnVisibility()
        {
            if (form.dgvLeaderboard == null) return;

            bool hideGameMode = true; // alapból elrejteni
            foreach (DataRowView row in form.bs)
            {
                string difficulty = row["Nehézség"].ToString();
                if (difficulty != "Nehéz" && difficulty != "Nagyon nehéz")
                {
                    hideGameMode = false; // van legalább egy könnyebb játék, ne rejtsük el
                    break;
                }
            }

            form.dgvLeaderboard.Columns["Játékmód"].Visible = !hideGameMode;
        }

        private void SetupComboBoxDefaultHighlight(ComboBox combo)
        {
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.DrawMode = DrawMode.OwnerDrawFixed;

            combo.DrawItem += (s, e) =>
            {
                if (e.Index < 0) return;

                bool isComboBoxText = (e.State & DrawItemState.ComboBoxEdit) == DrawItemState.ComboBoxEdit;

                if (isComboBoxText)
                {
                    // Fő rész: fehér háttér, fekete szöveg (nincs vizuális highlight)
                    using (SolidBrush bg = new SolidBrush(Color.White))
                        e.Graphics.FillRectangle(bg, e.Bounds);

                    using (SolidBrush textBrush = new SolidBrush(Color.Black))
                        e.Graphics.DrawString(combo.Items[e.Index].ToString(), e.Font, textBrush, e.Bounds);
                }
                else
                {
                    // Legördülő lista elemei
                    if ((e.State & DrawItemState.Selected) == DrawItemState.Selected)
                    {
                        using (SolidBrush bg = new SolidBrush(SystemColors.Highlight))
                            e.Graphics.FillRectangle(bg, e.Bounds);

                        using (SolidBrush textBrush = new SolidBrush(SystemColors.HighlightText))
                            e.Graphics.DrawString(combo.Items[e.Index].ToString(), e.Font, textBrush, e.Bounds);
                    }
                    else
                    {
                        using (SolidBrush bg = new SolidBrush(Color.White))
                            e.Graphics.FillRectangle(bg, e.Bounds);

                        using (SolidBrush textBrush = new SolidBrush(Color.Black))
                            e.Graphics.DrawString(combo.Items[e.Index].ToString(), e.Font, textBrush, e.Bounds);
                    }
                }
            };
        }
    }
}