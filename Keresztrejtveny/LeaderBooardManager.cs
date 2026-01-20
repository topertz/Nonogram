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
            table.Columns.Add("Rossz kattintások száma", typeof(int));
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
                save.WrongClicks,
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

            baseScore -= save.WrongClicks * 100;
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
                NonogramSaveData save = JsonSerializer.Deserialize<NonogramSaveData>(json, options);

                if (save != null)
                    saves.Add(save);
            }

            // Rangsorolás: idő, segítség, hibák
            List<NonogramSaveData> sorted = saves
                .OrderBy(s => s.ElapsedSeconds)
                .ThenBy(s => s.HintCount)
                .ThenBy(s => s.WrongClicks)
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
            dgv.AutoGenerateColumns = true;
            dgv.DataSource = form.bs;
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            form.f.Controls.Add(dgv);

            ApplyFilter();
            form.f.Shown += (s, e) =>
            {
                dgv.CurrentCell = null;
                dgv.ClearSelection();
                form.f.ActiveControl = null;
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
    }
}