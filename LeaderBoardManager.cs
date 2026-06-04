using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace Grafilogika_alkalmazas_keszitese
{
    public partial class LeaderboardManager
    {
        private BindingSource bindingSource;
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

        // Save
        public void SaveGame(string filename, string username)
        {
            string[] difficulties = { "Easy", "Medium", "Hard" };
            string[] modes = { "Black and white", "Colored" };
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

            // If the file already exists, load the existing games
            if (File.Exists(filename))
            {
                string existingJson = File.ReadAllText(filename);
                List<NonogramSaveData> existingSaves = JsonSerializer.Deserialize<List<NonogramSaveData>>(existingJson, options);
                if (existingSaves != null)
                {
                    allSaves.AddRange(existingSaves);
                }
            }

            // Add the new save
            allSaves.Add(saveData);

            // Restore all saves
            string json = JsonSerializer.Serialize(allSaves, options);
            File.WriteAllText(filename, json, new System.Text.UTF8Encoding(true));
        }
        public void BtnLeaderboard_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Select a saved game for the leaderboard";
                ofd.Filter = "Nonogram save (*.json)|*.json";

                string exeFolder = AppDomain.CurrentDomain.BaseDirectory;
                ofd.InitialDirectory = AppDomain.CurrentDomain.BaseDirectory;

                if (ofd.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                string folder = Path.GetDirectoryName(ofd.FileName);

                // Check if there is any json in the selected folder
                string[] jsonFiles = Directory.GetFiles(folder, "*.json");
                if (jsonFiles.Length == 0)
                {
                    MessageBox.Show("There are no ranked saves in this folder.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Load the saves
                DataTable leaderboard = LoadAllSaves(folder);

                if (leaderboard.Rows.Count == 0)
                {
                    MessageBox.Show("There are no ranked saves in this folder.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Display the leaderboard
                ShowLeaderboard(leaderboard);
            }
        }
        public DataTable CreateTable()
        {
            DataTable table = new DataTable("Leaderboard");
            table.Columns.Add("#", typeof(string));
            table.Columns.Add("Player", typeof(string));
            table.Columns.Add("Difficulty", typeof(string));
            table.Columns.Add("Game mode", typeof(string));
            table.Columns.Add("Number of incorrect cell clicks", typeof(int));
            table.Columns.Add("Number of incorrect colors", typeof(int));
            table.Columns.Add("Number of hints", typeof(int));
            table.Columns.Add("Elapsed time (s)", typeof(string));
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
                save.Mode == "Colored" ? save.WrongColorClicks : 0,
                save.HintCount,
                timeFormatted
            );
        }

        public DataTable LoadAllSaves(string folderPath)
        {
            DataTable table = CreateTable();
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };

            List<NonogramSaveData> saves = new List<NonogramSaveData>();

            foreach (string file in Directory.GetFiles(folderPath, "*.json"))
            {
                string json = File.ReadAllText(file, Encoding.UTF8).TrimStart('\uFEFF');

                try
                {
                    List<NonogramSaveData> fileSaves = JsonSerializer.Deserialize<List<NonogramSaveData>>(json, options);
                    if (fileSaves != null)
                    {
                        saves.AddRange(fileSaves);
                    }
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Error deserializing file: {file} -> {ex.Message}");
                }
            }

            List<NonogramSaveData> sorted = saves
                .OrderBy(s => s.ElapsedSeconds)
                .ThenBy(s => s.HintCount)
                .ThenBy(s => s.WrongCellClicks + (s.Mode == "Colored" ? s.WrongColorClicks.GetValueOrDefault() : 0))
                .ToList();

            for (int i = 0; i < sorted.Count; i++)
            {
                AddSaveToTable(table, sorted[i], "");
            }

            return table;
        }

        public void ShowLeaderboard(DataTable table)
        {
            leaderBoardWindow = new Form();
            leaderBoardWindow.Text = "Leaderboard";
            leaderBoardWindow.Size = new Size(600, 500);
            leaderBoardWindow.StartPosition = FormStartPosition.CenterParent;
            leaderBoardWindow.FormBorderStyle = FormBorderStyle.FixedDialog;
            leaderBoardWindow.MaximizeBox = false;

            // Filters (difficulty and game mode)
            lblDifficulty = new Label { Text = "Difficulty:", Location = new Point(20, 5), AutoSize = true };
            leaderBoardWindow.Controls.Add(lblDifficulty);

            cmbDifficultyFilter = new ComboBox();
            cmbDifficultyFilter.Items.AddRange(new string[] { "Easy", "Medium", "Hard" });
            cmbDifficultyFilter.SelectedIndex = 0;
            cmbDifficultyFilter.Location = new Point(20, lblDifficulty.Bottom + 3);
            cmbDifficultyFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            leaderBoardWindow.Controls.Add(cmbDifficultyFilter);

            lblMode = new Label { Text = "Game mode:", Location = new Point(200, 5), AutoSize = true };
            leaderBoardWindow.Controls.Add(lblMode);

            cmbModeFilter = new ComboBox();
            cmbModeFilter.Items.AddRange(new string[] { "Black and white", "Colored" });
            cmbModeFilter.SelectedIndex = 0;
            cmbModeFilter.Location = new Point(200, lblMode.Bottom + 3);
            cmbModeFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            leaderBoardWindow.Controls.Add(cmbModeFilter);

            bindingSource = new BindingSource();
            bindingSource.DataSource = table;

            // Initialize DataGridView
            DataGridView dataGridView = new DataGridView();
            dataGridView.Top = cmbDifficultyFilter.Bottom + 15;
            dataGridView.Left = 20;
            dataGridView.AutoGenerateColumns = false;
            dataGridView.RowHeadersVisible = false;
            dataGridView.ReadOnly = true;
            dataGridView.AllowUserToAddRows = false;
            dataGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridView.MultiSelect = false;
            dataGridView.BackgroundColor = Color.White;
            dataGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            dataGridView.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridView.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            dataGridView.Columns.Clear();

            // create columns
            dataGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "#",
                HeaderText = "Placement",
                DataPropertyName = "#"
            });

            dataGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Player",
                HeaderText = "Player",
                DataPropertyName = "Player",
                MinimumWidth = 100
            });

            dataGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Difficulty",
                HeaderText = "Difficulty",
                DataPropertyName = "Difficulty"
            });

            dataGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Game mode",
                HeaderText = "Game mode",
                DataPropertyName = "Game mode"
            });

            dataGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "WrongCells",
                HeaderText = "WrongCells error",
                DataPropertyName = "Number of incorrect cell clicks"
            });

            dataGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Number of incorrect colors",
                HeaderText = "Color error",
                DataPropertyName = "Number of incorrect colors"
            });

            dataGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Hints",
                HeaderText = "Hints",
                DataPropertyName = "Number of hints"
            });

            dataGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Time",
                HeaderText = "Time",
                DataPropertyName = "Elapsed time (s)"
            });

            dataGridView.DataSource = bindingSource;
            leaderBoardWindow.Controls.Add(dataGridView);
            dgvLeaderboard = dataGridView;

            // Events
            cmbDifficultyFilter.SelectedIndexChanged += (s, e) => ApplyFilter();
            cmbModeFilter.SelectedIndexChanged += (s, e) => ApplyFilter();

            // Set initial state
            ApplyFilter();
            SetupComboBoxDefaultHighlight(cmbDifficultyFilter);
            SetupComboBoxDefaultHighlight(cmbModeFilter);

            leaderBoardWindow.Shown += (s, e) =>
            {
                ApplyFilter();
                dataGridView.CurrentCell = null;
                dataGridView.ClearSelection();
                leaderBoardWindow.ActiveControl = null;
            };

            leaderBoardWindow.ShowDialog();
        }

        // Modified ApplyFilter, which also resizes the window
        private void ApplyFilter()
        {
            if (dgvLeaderboard == null)
            {
                return;
            }

            string diff = cmbDifficultyFilter.SelectedItem?.ToString() ?? "All";
            string mode = cmbModeFilter.SelectedItem?.ToString() ?? "All";

            string filter = "";
            if (diff != "All")
            {
                filter += $"[Difficulty] = '{diff}'";
            }
            if (mode != "All")
            {
                if (filter != "")
                {
                    filter += " AND ";
                }
                filter += $"[Game mode] = '{mode}'";
            }

            bindingSource.Filter = filter;

            // Regenerate sequence numbering
            for (int i = 0; i < bindingSource.Count; i++)
            {
                DataRowView rowView = (DataRowView)bindingSource[i];
                rowView["#"] = (i + 1).ToString() + ".";
            }

            // Manage column visibility
            bool containsColorMode = bindingSource.Cast<DataRowView>().Any(r => r["Game mode"].ToString() == "Colored");
            dgvLeaderboard.Columns["Number of incorrect colors"].Visible = (mode == "Colored"
                || (mode == "All" && containsColorMode));

            // Force recalculation of widths
            dgvLeaderboard.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);

            // Calculate the width of all visible columns
            int totalWidth = 0;
            foreach (DataGridViewColumn col in dgvLeaderboard.Columns)
            {
                if (col.Visible)
                {
                    totalWidth += col.Width;
                }
            }

            // Adjust the width of the DGV to the columns
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
                if (e.Index < 0)
                {
                    return;
                }

                bool isComboBoxText = (e.State & DrawItemState.ComboBoxEdit) == DrawItemState.ComboBoxEdit;

                if (isComboBoxText)
                {
                    // Main part white background, black text (no visual highlight)
                    using (SolidBrush bg = new SolidBrush(Color.White))
                    {
                        e.Graphics.FillRectangle(bg, e.Bounds);
                    }

                    using (SolidBrush textBrush = new SolidBrush(Color.Black))
                    {
                        e.Graphics.DrawString(combo.Items[e.Index].ToString(), e.Font, textBrush, e.Bounds);
                    }
                }
                else
                {
                    // Dropdown list items
                    if ((e.State & DrawItemState.Selected) == DrawItemState.Selected)
                    {
                        using (SolidBrush bg = new SolidBrush(SystemColors.Highlight))
                        {
                            e.Graphics.FillRectangle(bg, e.Bounds);
                        }

                        using (SolidBrush textBrush = new SolidBrush(SystemColors.HighlightText))
                        {
                            e.Graphics.DrawString(combo.Items[e.Index].ToString(), e.Font, textBrush, e.Bounds);
                        }
                    }
                    else
                    {
                        using (SolidBrush bg = new SolidBrush(Color.White))
                        {
                            e.Graphics.FillRectangle(bg, e.Bounds);
                        }

                        using (SolidBrush textBrush = new SolidBrush(Color.Black))
                        {
                            e.Graphics.DrawString(combo.Items[e.Index].ToString(), e.Font, textBrush, e.Bounds);
                        }
                    }
                }
            };
        }
    }
}