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
            table.Columns.Add("#", typeof(string));
            table.Columns.Add("Játékos", typeof(string));
            table.Columns.Add("Nehézség", typeof(string));
            table.Columns.Add("Játékmód", typeof(string));
            table.Columns.Add("Helytelen cella kattintások száma", typeof(int));
            table.Columns.Add("Helytelen színek száma", typeof(int));
            table.Columns.Add("Segítségek száma", typeof(int));
            table.Columns.Add("Eltelt idő (s)", typeof(string));

            return table;
        }

        public void AddSaveToTable(DataTable table, NonogramSaveData save, string rank)
        {
            int score = CalculateScore(save);
            int minutes = save.ElapsedSeconds / 60;
            int seconds = save.ElapsedSeconds % 60;
            string timeFormatted = $"{minutes:D2}:{seconds:D2}"; // pl. 00:35, 01:00

            table.Rows.Add(
                rank,
                save.Username,
                save.Difficulty,
                save.Mode,
                save.WrongCellClicks,
                save.Mode == "Színes" ? save.WrongColorClicks : 0,
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
            {
                baseScore += 500;
                baseScore -= (save.WrongColorClicks ?? 0) * 50; // Színesnél a színhiba is von le pontot
            }

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
                .ThenBy(s => s.WrongCellClicks + (s.Mode == "Színes" ? s.WrongColorClicks : 0))
                .ToList();

            for (int i = 0; i < sorted.Count; i++)
            {
                // Meghívjuk az AddSaveToTable-t, de átadjuk a helyezést is
                AddSaveToTable(table, sorted[i], "");
            }

            return table;
        }

        public void ShowLeaderboard(DataTable table)
        {
            form.f = new Form();
            form.f.Text = "Ranglista";
            // Kezdő méret (később az ApplyFilter finomhangolja)
            form.f.Size = new Size(600, 500);
            form.f.StartPosition = FormStartPosition.CenterParent;
            form.f.FormBorderStyle = FormBorderStyle.FixedDialog; // Megakadályozzuk a manuális átméretezést, ha zavaró
            form.f.MaximizeBox = false;

            // Szűrők (Nehézség és Játékmód)
            Label lblDifficulty = new Label { Text = "Nehézség:", Location = new Point(20, 5), AutoSize = true };
            form.f.Controls.Add(lblDifficulty);

            form.cmbDifficultyFilter = new ComboBox();
            form.cmbDifficultyFilter.Items.AddRange(new string[] { "Könnyű", "Közepes", "Nehéz" });
            form.cmbDifficultyFilter.SelectedIndex = 0;
            form.cmbDifficultyFilter.Location = new Point(20, lblDifficulty.Bottom + 3);
            form.cmbDifficultyFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            form.f.Controls.Add(form.cmbDifficultyFilter);

            Label lblMode = new Label { Text = "Játékmód:", Location = new Point(200, 5), AutoSize = true };
            form.f.Controls.Add(lblMode);

            form.cmbModeFilter = new ComboBox();
            form.cmbModeFilter.Items.AddRange(new string[] { "Fekete-fehér", "Színes" });
            form.cmbModeFilter.SelectedIndex = 0;
            form.cmbModeFilter.Location = new Point(200, lblMode.Bottom + 3);
            form.cmbModeFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            form.f.Controls.Add(form.cmbModeFilter);

            form.bs = new BindingSource();
            form.bs.DataSource = table;

            // DataGridView inicializálása
            DataGridView dgv = new DataGridView();
            dgv.Top = form.cmbDifficultyFilter.Bottom + 15;
            dgv.Left = 20;
            dgv.AutoGenerateColumns = false;
            dgv.RowHeadersVisible = false;
            dgv.ReadOnly = true;
            dgv.AllowUserToAddRows = false;
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.MultiSelect = false;
            dgv.BackgroundColor = Color.White;
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            dgv.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            dgv.Columns.Clear();

            // oszlopok létrehozása
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "#", HeaderText = "Helyezés", DataPropertyName = "#" });

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Játékos",
                HeaderText = "Játékos",
                DataPropertyName = "Játékos",
                MinimumWidth = 100
            });

            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Nehézség", HeaderText = "Nehézség", DataPropertyName = "Nehézség" });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Játékmód", HeaderText = "Játékmód", DataPropertyName = "Játékmód" });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "WrongCells", HeaderText = "Cellahiba", DataPropertyName = "Helytelen cella kattintások száma" });

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Helytelen színek száma",
                HeaderText = "Színhiba",
                DataPropertyName = "Helytelen színek száma"
            });

            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Hints", HeaderText = "Segítség", DataPropertyName = "Segítségek száma" });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Time", HeaderText = "Idő", DataPropertyName = "Eltelt idő (s)" });

            dgv.DataSource = form.bs;
            form.f.Controls.Add(dgv);
            form.dgvLeaderboard = dgv;

            // Események
            form.cmbDifficultyFilter.SelectedIndexChanged += (s, e) => ApplyFilter();
            form.cmbModeFilter.SelectedIndexChanged += (s, e) => ApplyFilter();

            // Kezdeti állapot beállítása
            UpdateWrongColorColumnVisibility();
            UpdateGameModeColumnVisibility();
            ApplyFilter();
            SetupComboBoxDefaultHighlight(form.cmbDifficultyFilter);
            SetupComboBoxDefaultHighlight(form.cmbModeFilter);

            form.f.Shown += (s, e) => {
                ApplyFilter();
                dgv.CurrentCell = null;
                dgv.ClearSelection();
                form.f.ActiveControl = null;
            };

            form.f.ShowDialog();
        }

        // Módosított ApplyFilter, ami átméretezi az ablakot is
        private void ApplyFilter()
        {
            if (form.dgvLeaderboard == null) return;

            string diff = form.cmbDifficultyFilter.SelectedItem?.ToString() ?? "Összes";
            string mode = form.cmbModeFilter.SelectedItem?.ToString() ?? "Összes";

            string filter = "";
            if (diff != "Összes") filter += $"[Nehézség] = '{diff}'";
            if (mode != "Összes")
            {
                if (filter != "") filter += " AND ";
                filter += $"[Játékmód] = '{mode}'";
            }

            form.bs.Filter = filter;

            // Sorszámozás újragenerálása
            for (int i = 0; i < form.bs.Count; i++)
            {
                DataRowView rowView = (DataRowView)form.bs[i];
                rowView["#"] = (i + 1).ToString() + ".";
            }

            // Oszlop láthatóságának kezelése
            bool containsColorMode = form.bs.Cast<DataRowView>().Any(r => r["Játékmód"].ToString() == "Színes");
            form.dgvLeaderboard.Columns["Helytelen színek száma"].Visible = (mode == "Színes" || (mode == "Összes" && containsColorMode));

            // Kényszerítjük a szélességek újraszámolását
            form.dgvLeaderboard.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);

            // Kiszámoljuk az összes látható oszlop szélességét
            int totalWidth = 0;
            foreach (DataGridViewColumn col in form.dgvLeaderboard.Columns)
            {
                if (col.Visible) totalWidth += col.Width;
            }

            // A DGV szélességét az oszlopokhoz igazítjuk
            form.dgvLeaderboard.Width = totalWidth + 3;

            // Az ablak szélességét a DGV-hez igazítjuk (margókkal)
            // ClientSize-ot állítunk, hogy a keret ne zavarjon be a matekba
            form.f.ClientSize = new Size(form.dgvLeaderboard.Width + 40, form.f.ClientSize.Height);
            form.dgvLeaderboard.CurrentCell = null;
            form.dgvLeaderboard.ClearSelection();
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