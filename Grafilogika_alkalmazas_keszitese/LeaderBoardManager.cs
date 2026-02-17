using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace Grafilogika_alkalmazas_keszitese
{
    public partial class LeaderboardManager
    {
        private BindingSource bs;
        private DataGridView dgvLeaderboard;
        private Label lblDifficulty, lblMode;
        private ComboBox cmbDifficultyFilter, cmbModeFilter;
        private Form leaderBoardWindow;
        private Nonogram form;
        private NonogramGrid grid;
        private NonogramRender render;
        private GameTimerManager gameTimerManager;
        public LeaderboardManager(Nonogram f, NonogramGrid g, NonogramRender r, GameTimerManager game)
        {
            this.form = f;
            this.grid = g;
            this.render = r;
            this.gameTimerManager = game;
        }

        public void SetGrid(NonogramGrid g)
        {
            this.grid = g;
        }
        public void SetRender(NonogramRender r)
        {
            render = r;
        }
        public void SetTimerManager(GameTimerManager g)
        {
            gameTimerManager = g;
        }

        // Mentés
        public void SaveGame(string filename, string username)
        {
            string[] difficulties = { "Könnyű", "Közepes", "Nehéz" };
            string[] modes = { "Fekete-fehér", "Színes" };
            bool isColorMode = form.cmbMode.SelectedIndex == 1;

            NonogramSaveData saveData = new NonogramSaveData
            {
                Username = username,
                Difficulty = difficulties[form.cmbDifficulty.SelectedIndex],
                Mode = modes[form.cmbMode.SelectedIndex],
                HintCount = render.hintCount,
                WrongCellClicks = grid.wrongCellClicks,
                WrongColorClicks = isColorMode ? (int?)grid.wrongColorClicks : null,
                ElapsedSeconds = gameTimerManager.elapsedSeconds
            };

            JsonSerializerOptions options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            List<NonogramSaveData> allSaves = new List<NonogramSaveData>();

            // Ha már létezik a fájl, olvassuk be a meglévő játékokat
            if (File.Exists(filename))
            {
                string existingJson = File.ReadAllText(filename);
                List<NonogramSaveData> existingSaves = JsonSerializer.Deserialize<List<NonogramSaveData>>(existingJson, options);
                if (existingSaves != null)
                    allSaves.AddRange(existingSaves);
            }

            // Adjunk hozzá az új mentést
            allSaves.Add(saveData);

            // Mentsük vissza az összes mentést
            string json = JsonSerializer.Serialize(allSaves, options);
            File.WriteAllText(filename, json, new System.Text.UTF8Encoding(true));
        }
        public void BtnLeaderboard_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Válassz ki egy mentett játékot a ranglistához";
                ofd.Filter = "Nonogram mentés (*.json)|*.json";

                // Projekt főmappa elérése (három szinttel feljebb a bin mappából)
                string projectFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..");
                projectFolder = Path.GetFullPath(projectFolder); // abszolút útvonal
                ofd.InitialDirectory = projectFolder;

                if (ofd.ShowDialog() != DialogResult.OK)
                    return;

                string folder = Path.GetDirectoryName(ofd.FileName);

                // Ellenőrizzük, hogy van-e egyáltalán json a kiválasztott mappában
                string[] jsonFiles = Directory.GetFiles(folder, "*.json");
                if (jsonFiles.Length == 0)
                {
                    MessageBox.Show("Ebben a mappában nincs ranglistázható mentés.", "Hiba", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Betöltjük a mentéseket
                DataTable leaderboard = LoadAllSaves(folder);

                if (leaderboard.Rows.Count == 0)
                {
                    MessageBox.Show("Ebben a mappában nincs ranglistázható mentés.", "Hiba", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Megjelenítjük a ranglistát
                ShowLeaderboard(leaderboard);
            }
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
            int minutes = save.ElapsedSeconds / 60;
            int seconds = save.ElapsedSeconds % 60;
            string timeFormatted = $"{minutes:D2}:{seconds:D2}";

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

            // Rangsorolás idő, segítség, hibák
            List<NonogramSaveData> sorted = saves
                .OrderBy(s => s.ElapsedSeconds)
                .ThenBy(s => s.HintCount)
                .ThenBy(s => s.WrongCellClicks + (s.Mode == "Színes" ? s.WrongColorClicks : 0))
                .ToList();

            for (int i = 0; i < sorted.Count; i++)
            {
                // Meghívjuk az AddSaveToTablet, de átadjuk a helyezést is
                AddSaveToTable(table, sorted[i], "");
            }

            return table;
        }

        public void ShowLeaderboard(DataTable table)
        {
            leaderBoardWindow = new Form();
            leaderBoardWindow.Text = "Ranglista";
            leaderBoardWindow.Size = new Size(600, 500);
            leaderBoardWindow.StartPosition = FormStartPosition.CenterParent;
            leaderBoardWindow.FormBorderStyle = FormBorderStyle.FixedDialog;
            leaderBoardWindow.MaximizeBox = false;

            // Szűrők (nehézség és játékmód)
            lblDifficulty = new Label { Text = "Nehézség:", Location = new Point(20, 5), AutoSize = true };
            leaderBoardWindow.Controls.Add(lblDifficulty);

            cmbDifficultyFilter = new ComboBox();
            cmbDifficultyFilter.Items.AddRange(new string[] { "Könnyű", "Közepes", "Nehéz" });
            cmbDifficultyFilter.SelectedIndex = 0;
            cmbDifficultyFilter.Location = new Point(20, lblDifficulty.Bottom + 3);
            cmbDifficultyFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            leaderBoardWindow.Controls.Add(cmbDifficultyFilter);

            lblMode = new Label { Text = "Játékmód:", Location = new Point(200, 5), AutoSize = true };
            leaderBoardWindow.Controls.Add(lblMode);

            cmbModeFilter = new ComboBox();
            cmbModeFilter.Items.AddRange(new string[] { "Fekete-fehér", "Színes" });
            cmbModeFilter.SelectedIndex = 0;
            cmbModeFilter.Location = new Point(200, lblMode.Bottom + 3);
            cmbModeFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            leaderBoardWindow.Controls.Add(cmbModeFilter);

            bs = new BindingSource();
            bs.DataSource = table;

            // DataGridView inicializálása
            DataGridView dgv = new DataGridView();
            dgv.Top = cmbDifficultyFilter.Bottom + 15;
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

            dgv.DataSource = bs;
            leaderBoardWindow.Controls.Add(dgv);
            dgvLeaderboard = dgv;

            // Események
            cmbDifficultyFilter.SelectedIndexChanged += (s, e) => ApplyFilter();
            cmbModeFilter.SelectedIndexChanged += (s, e) => ApplyFilter();

            // Kezdeti állapot beállítása
            ApplyFilter();
            SetupComboBoxDefaultHighlight(cmbDifficultyFilter);
            SetupComboBoxDefaultHighlight(cmbModeFilter);

            leaderBoardWindow.Shown += (s, e) => {
                ApplyFilter();
                dgv.CurrentCell = null;
                dgv.ClearSelection();
                leaderBoardWindow.ActiveControl = null;
            };

            leaderBoardWindow.ShowDialog();
        }

        // Módosított ApplyFilter, ami átméretezi az ablakot is
        private void ApplyFilter()
        {
            if (dgvLeaderboard == null) return;

            string diff = cmbDifficultyFilter.SelectedItem?.ToString() ?? "Összes";
            string mode = cmbModeFilter.SelectedItem?.ToString() ?? "Összes";

            string filter = "";
            if (diff != "Összes") filter += $"[Nehézség] = '{diff}'";
            if (mode != "Összes")
            {
                if (filter != "") filter += " AND ";
                filter += $"[Játékmód] = '{mode}'";
            }

            bs.Filter = filter;

            // Sorszámozás újragenerálása
            for (int i = 0; i < bs.Count; i++)
            {
                DataRowView rowView = (DataRowView)bs[i];
                rowView["#"] = (i + 1).ToString() + ".";
            }

            // Oszlop láthatóságának kezelése
            bool containsColorMode = bs.Cast<DataRowView>().Any(r => r["Játékmód"].ToString() == "Színes");
            dgvLeaderboard.Columns["Helytelen színek száma"].Visible = (mode == "Színes" || (mode == "Összes" && containsColorMode));

            // Kényszerítjük a szélességek újraszámolását
            dgvLeaderboard.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);

            // Kiszámoljuk az összes látható oszlop szélességét
            int totalWidth = 0;
            foreach (DataGridViewColumn col in dgvLeaderboard.Columns)
            {
                if (col.Visible) totalWidth += col.Width;
            }

            // A DGV szélességét az oszlopokhoz igazítjuk
            dgvLeaderboard.Width = totalWidth + 3;

            leaderBoardWindow.ClientSize = new Size(dgvLeaderboard.Width + 40, leaderBoardWindow.ClientSize.Height);
            dgvLeaderboard.CurrentCell = null;
            dgvLeaderboard.ClearSelection();
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
                    // Fő rész fehér háttér, fekete szöveg (nincs vizuális highlight)
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