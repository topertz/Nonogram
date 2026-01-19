using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;

namespace Nonogram
{
    public partial class Nonogram : Form
    {
        public Button[,] gridButtons;
        public Button btnSolve, btnHint, btnCheck, btnSave, btnLoad;
        public Button btnGenerateRandom;
        public Button btnSaveGame, btnLoadGame;
        public Button btnLeaderboard;
        public int[][] rowClues;
        public int[][] colClues;
        public int row = 10;
        public int col = 10;
        public int[,] userColor;
        public int userCellSize = 50;
        private int clueSize = 50;
        public Queue<Point> solutionQueue;
        public Timer solveTimer;
        public PictureBox picPreview;
        public PictureBox picSolutionPreview;
        public int previewSize = 150;
        public Color[,] solutionColorRGB; // a kép eredeti színe
        public int[,] solutionBW;          // fekete-fehér logikai mátrix (clues és ellenőrzéshez)
        public Color[,] userColorRGB;      // felhasználó által kiválasztott szín
        public bool isColor;
        public bool[,] userXMark;
        public CheckBox chkGrayscale;
        public CheckBox chkShowX;
        public Color[][] rowClueColors;
        public Color[][] colClueColors;
        public int colorSimilarityThreshold = 40;
        private bool[,] hintActive;
        private bool hintShown = false;
        public int fixedGridTop;
        public FlowLayoutPanel colorPalette;
        public Color selectedColor = Color.Black; // alapértelmezett szín
        private Button btnUndo, btnRedo;
        public Stack<Color[,]> undoStack = new Stack<Color[,]>();
        public Stack<Color[,]> redoStack = new Stack<Color[,]>();
        public Image img;
        public int wrongClicks = 0;
        public Label lblWrongClicks;
        public int hintCount = 0;
        public Label lblHintCount;
        public ComboBox cmbDifficulty;
        public ComboBox cmbMode;
        private ComboBox cmbDifficultyFilter, cmbModeFilter;
        public Timer gameTimer;
        public int elapsedSeconds = 0;
        public int remainingSeconds = 0;
        public Label lblTimer;
        public int maxColors = 8;
        public Color[] easyColors = new Color[]
        {
            Color.Black,
            Color.Brown,
            Color.Blue,
            Color.Green
        };
        public Color[] mediumColors = new Color[]
        {
            Color.Black,
            Color.Brown,
            Color.Blue,
            Color.Green
        };
        public string username;
        private BindingSource bs;
        public Random rnd = new Random();
        private Form f;
        public NonogramGrid grid;
        public NonogramRenderer renderer;
        public UndoRedoManager undoredoManager;
        public GameTimerManager gameTimerManager;
        public SaveLoadManager saveLoadManager;
        public Nonogram()
        {
            InitializeComponent();
            InitializeCustomComponents();
            this.Size = new Size(1100, 1300);
            this.AutoScroll = true;
            cmbDifficulty.SelectionChangeCommitted += CmbDifficultyOrMode_Changed;
            cmbMode.SelectionChangeCommitted += CmbDifficultyOrMode_Changed;
            grid = new NonogramGrid(this);
            renderer = new NonogramRenderer(this, grid);
            undoredoManager = new UndoRedoManager(this);
            gameTimerManager = new GameTimerManager(this, grid, renderer, undoredoManager);
            saveLoadManager = new SaveLoadManager(this);
            grid.InitializeGridPosition();
            btnSolve.Visible = false;
            btnHint.Visible = false;
            btnCheck.Visible = false;
            btnSave.Visible = false;
            btnLoad.Visible = false;
            btnUndo.Visible = false;
            btnRedo.Visible = false;
            cmbDifficulty.Visible = false;
            cmbMode.Visible = false;
            chkGrayscale.Visible = false;
            chkShowX.Visible = false;
            btnSaveGame.Visible = false;
            btnLoadGame.Visible = false;
            lblTimer.Visible = false;
            picPreview.Visible = false;
            picSolutionPreview.Visible = false;
        }

        private void InitializeCustomComponents()
        {
            // Mini előnézet
            picPreview = new PictureBox();
            picPreview.Size = new Size(previewSize, previewSize);
            picPreview.Location = new Point(20, 20);
            picPreview.BorderStyle = BorderStyle.FixedSingle;
            picPreview.BackColor = Color.White;
            this.Controls.Add(picPreview);

            // --- Megoldás előnézet PictureBox ---
            PictureBox picSolutionPreview = new PictureBox();
            picSolutionPreview.Size = new Size(previewSize, previewSize);
            picSolutionPreview.Location = new Point(picPreview.Right + 20, 20);
            picSolutionPreview.BorderStyle = BorderStyle.FixedSingle;
            picSolutionPreview.BackColor = Color.White;
            this.Controls.Add(picSolutionPreview);

            // Mentsd el tagváltozóként:
            this.picSolutionPreview = picSolutionPreview;

            // Gombok, grid még nincs
            int btnTop = picPreview.Bottom + 20 + (userCellSize * col) + clueSize; // ideiglenes, majd frissítjük a grid után

            // Solve gomb
            btnSolve = new Button();
            btnSolve.Text = "Megoldás";
            btnSolve.Location = new Point(115, 760);
            btnSolve.Click += BtnSolve_Click;
            this.Controls.Add(btnSolve);

            // Hint gomb
            btnHint = new Button();
            btnHint.Text = "Segítség";
            btnHint.Location = new Point(210, 760);
            btnHint.Click += BtnHint_Click;
            this.Controls.Add(btnHint);

            // Check gomb
            btnCheck = new Button();
            btnCheck.Text = "Ellenőrzés";
            btnCheck.Location = new Point(305, 760);
            btnCheck.Click += BtnCheck_Click;
            this.Controls.Add(btnCheck);

            // Fekete-fehér mód checkbox
            chkGrayscale = new CheckBox();
            chkGrayscale.Text = "Fekete-fehér mód";
            chkGrayscale.Location = new Point(20, picPreview.Bottom + 20);
            chkGrayscale.AutoSize = true;
            this.Controls.Add(chkGrayscale);

            // X-ek megjelenítése checkbox
            chkShowX = new CheckBox();
            chkShowX.Text = "Kitöltetlen cellák megjelenítése";
            chkShowX.Location = new Point(180, picPreview.Bottom + 20);
            chkShowX.AutoSize = true;
            chkShowX.Checked = false; // alapból ki van kapcsolva
            chkShowX.CheckedChanged += ChkShowX_CheckedChanged;
            this.Controls.Add(chkShowX);

            // Mentés gomb
            btnSave = new Button();
            btnSave.Text = "Mentés";
            btnSave.Location = new Point(btnCheck.Right + 20, btnCheck.Top);
            btnSave.Click += BtnSave_Click;
            this.Controls.Add(btnSave);

            // Betöltés gomb
            btnLoad = new Button();
            btnLoad.Text = "Betöltés";
            btnLoad.Location = new Point(btnSave.Right + 20, btnCheck.Top);
            btnLoad.Click += BtnLoadSaved_Click;
            this.Controls.Add(btnLoad);

            // Undo gomb
            btnUndo = new Button();
            btnUndo.Text = "Visszavonás";
            btnUndo.Location = new Point(btnLoad.Right + 20, btnCheck.Top);
            btnUndo.Click += BtnUndo_Click;
            this.Controls.Add(btnUndo);

            // Redo gomb
            btnRedo = new Button();
            btnRedo.Text = "Előrelépés";
            btnRedo.Location = new Point(btnUndo.Right + 20, btnCheck.Top);
            btnRedo.Click += BtnRedo_Click;
            this.Controls.Add(btnRedo);

            // Véletlen Nonogram generálás gomb
            btnGenerateRandom = new Button();
            btnGenerateRandom.Text = "Nonogram";
            btnGenerateRandom.Location = new Point(20, btnCheck.Top);
            btnGenerateRandom.Click += BtnGenerateRandom_Click;
            this.Controls.Add(btnGenerateRandom);

            // --- Új mentés gomb (JSON) ---
            btnSaveGame = new Button();
            btnSaveGame.Text = "Játék mentése";
            btnSaveGame.Size = new Size(100, 25);
            btnSaveGame.Location = new Point(btnRedo.Right + 20, btnCheck.Top);
            btnSaveGame.Click += BtnSaveGame_Click;
            this.Controls.Add(btnSaveGame);

            // --- Új betöltés gomb (JSON) ---
            btnLoadGame = new Button();
            btnLoadGame.Text = "Játék betöltése";
            btnLoadGame.Size = new Size(100, 25);
            btnLoadGame.Location = new Point(btnSaveGame.Right + 20, btnCheck.Top);
            btnLoadGame.Click += BtnLoadGame_Click;
            this.Controls.Add(btnLoadGame);

            // --- Nehézségi szint választó ---
            cmbDifficulty = new ComboBox();
            cmbDifficulty.Items.AddRange(new string[] { "Könnyű", "Közepes", "Nehéz" });
            cmbDifficulty.SelectedIndex = 0; // alapértelmezett: Könnyű
            cmbDifficulty.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbDifficulty.Location = new Point(20, 250);
            cmbDifficulty.Width = 150;
            this.Controls.Add(cmbDifficulty);

            // Játék mód választó (fekete-fehér / színes)
            cmbMode = new ComboBox();
            cmbMode.Items.AddRange(new string[] { "Fekete-fehér", "Színes" });
            cmbMode.SelectedIndex = 0; // alapértelmezett: fekete-fehér
            cmbMode.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbMode.Location = new Point(cmbDifficulty.Right + 20, 250);
            cmbMode.Width = 150;
            this.Controls.Add(cmbMode);

            // Színpaletta panel
            colorPalette = new FlowLayoutPanel();
            colorPalette.Size = new Size(350, 200);       // nagyobb magasság
            colorPalette.Location = new Point(20, 200);
            colorPalette.AutoScroll = true;              // görgethető lesz, ha nem fér el
            colorPalette.WrapContents = true;            // több sorba törik a gombok
            colorPalette.FlowDirection = FlowDirection.LeftToRight; // balról jobbra
            colorPalette.AutoSize = false;               // ne próbáljon automatikusan méretezni
            this.Controls.Add(colorPalette);

            // --- Színpaletta helyének beállítása a comboboxok alatt ---
            int paletteTop = Math.Max(cmbDifficulty.Bottom, cmbMode.Bottom) + 20;
            colorPalette.Location = new Point(20, paletteTop);

            // Helytelen kattintások számláló Label
            lblWrongClicks = new Label();
            lblWrongClicks.Text = $"Helytelen kattintások száma: {wrongClicks} (max: 5)";
            lblWrongClicks.Location = new Point(20, 650); // tetszőleges pozíció
            lblWrongClicks.AutoSize = true;
            this.Controls.Add(lblWrongClicks);

            lblHintCount = new Label();
            lblHintCount.Text = "Segítségek száma: 0";
            lblHintCount.Location = new Point(20, lblWrongClicks.Bottom + 10);
            lblHintCount.AutoSize = true;
            this.Controls.Add(lblHintCount);

            btnLeaderboard = new Button();
            btnLeaderboard.Text = "Ranglista";
            btnLeaderboard.Location = new Point(20, 700);
            btnLeaderboard.Click += BtnLeaderboard_Click;
            this.Controls.Add(btnLeaderboard);

            // Felhasználónév Label
            Label lblUsername = new Label();
            lblUsername.Text = "Felhasználónév:";
            lblUsername.Location = new Point(20, 600);
            lblUsername.AutoSize = true;
            this.Controls.Add(lblUsername);

            // Felhasználónév TextBox
            TextBox txtUsername = new TextBox();
            txtUsername.Location = new Point(lblUsername.Right + 10, 595);
            txtUsername.Width = 150;
            txtUsername.Text = username; // alapértelmezett
            txtUsername.Name = "txtUsername";
            this.Controls.Add(txtUsername);

            // Időzítő Label
            lblTimer = new Label();
            lblTimer.Text = "00:00";
            lblTimer.Font = new Font("Arial", 16, FontStyle.Bold);
            lblTimer.Size = new Size(100, 30);
            lblTimer.Location = new Point(600, 50); // tetszőleges pozíció
            lblTimer.AutoSize = true;
            this.Controls.Add(lblTimer);
        }

        private void BtnGenerateRandom_Click(object sender, EventArgs e)
        {
            // --- Username ellenőrzése ---
            TextBox txtUsername = this.Controls.Find("txtUsername", true).FirstOrDefault() as TextBox;
            if (txtUsername == null)
                return;

            string currentUsername = txtUsername.Text.Trim();
            if (string.IsNullOrEmpty(currentUsername))
            {
                MessageBox.Show("Kérlek, add meg a felhasználóneved a játék indítása előtt!", "Hiba", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return; // gomb nem tűnik el, játék nem indul
            }

            // --- Username fixálása ---
            username = currentUsername;
            txtUsername.Enabled = false;  // teljesen letiltjuk, nem lehet kattintani

            // --- Gombok és vezérlők megjelenítése ---
            btnSolve.Visible = true;
            btnHint.Visible = true;
            btnCheck.Visible = true;
            btnSave.Visible = true;
            btnLoad.Visible = true;
            btnUndo.Visible = true;
            btnRedo.Visible = true;
            cmbDifficulty.Visible = true;
            cmbMode.Visible = true;
            chkShowX.Visible = true;
            btnSaveGame.Visible = true;
            btnLoadGame.Visible = true;
            lblTimer.Visible = true;
            picPreview.Visible = true;
            picSolutionPreview.Visible = true;

            // A Generate gomb már elrejthető, mert név van
            btnGenerateRandom.Visible = false;

            // --- Grid előkészítése ---
            grid.ClearGrid();
            gameTimerManager.ResetWrongClicks();
            undoredoManager.ClearHistory();
            picPreview.Image = null;
            wrongClicks = 0;

            // --- A difficulty szerint generálunk ---
            switch (cmbDifficulty.SelectedIndex)
            {
                case 0: // Könnyű
                    grid.GenerateRandomNonogram(20, 150, 10, 10);
                    break;

                case 1: // Közepes
                    grid.GenerateMediumNonogram();
                    break;

                case 2: // Nehéz
                    OpenFileDialog ofd = new OpenFileDialog();
                    ofd.Filter = "Képfájlok|*.png;*.jpg;*.jpeg;*.bmp";
                    ofd.Title = "Kép betöltése Nonogramhoz";

                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        Image loadedImg = Image.FromFile(ofd.FileName);
                        Bitmap bmpToUse = (chkGrayscale.Checked)
                            ? renderer.ConvertToBlackAndWhite(new Bitmap(loadedImg))
                            : new Bitmap(loadedImg);

                        img = bmpToUse;
                        picSolutionPreview.Image = bmpToUse;
                        picSolutionPreview.SizeMode = PictureBoxSizeMode.Zoom;

                        int gridLeft = 20;
                        int gridTop = Math.Max(chkGrayscale.Bottom, chkShowX.Bottom) + 20;
                        grid.GenerateNonogramFromImage(bmpToUse, gridLeft, gridTop);
                        gameTimerManager.Start();
                    }
                    break;
            }

            renderer.ToggleXMarks(chkShowX.Checked);
            renderer.UpdatePreview();
            if (cmbDifficulty.SelectedIndex != 2) // nem nehéz
            {
                gameTimerManager.StartTimer();
            }
            picSolutionPreview.Image = grid.GeneratePreviewImage();
            picSolutionPreview.SizeMode = PictureBoxSizeMode.Zoom;
        }

        // Cellára kattintás esemény
        public void GridCell_Click(object sender, EventArgs e)
        {
            renderer.ClearErrorHighlights();
            Button btn = sender as Button;
            if (btn == null) return;

            Point p = (Point)btn.Tag;
            int rowIndex = p.X;
            int colIndex = p.Y;

            bool wrongClick = false;

            // Ha az adott cella X-jelölt, ez mindig hibás
            if (userXMark[rowIndex, colIndex])
            {
                wrongClick = true;
            }

            // Mentés a visszavonáshoz
            undoredoManager.SaveState();

            // Cellamódosítás
            grid.HandleGridClick(btn, selectedColor);

            if (!userXMark[rowIndex, colIndex])
            {
                // ha fehérre törölt → NEM hiba
                bool isEmpty = isColor
                    ? userColorRGB[rowIndex, colIndex] == Color.White
                    : userColor[rowIndex, colIndex] == 0;

                if (!isEmpty)
                {
                    bool isCorrect = isColor
                        ? userColorRGB[rowIndex, colIndex].ToArgb() == solutionColorRGB[rowIndex, colIndex].ToArgb()
                        : userColor[rowIndex, colIndex] == solutionBW[rowIndex, colIndex];

                    if (!isCorrect)
                        wrongClick = true;
                }
            }

            // Hibák kezelése
            if (wrongClick)
            {
                wrongClicks++;
                lblWrongClicks.Text = $"Helytelen kattintások száma: {wrongClicks} (max: 5)";

                if (wrongClicks >= 5)
                {
                    MessageBox.Show("Túl sok helytelen kattintás! Újraindítjuk a játékot.", "Hiba", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    wrongClicks = 0;
                    hintCount = 0;
                    grid.ClearGrid();
                    gameTimerManager.ResetWrongClicks();
                    gameTimerManager.ResetHintClicks();
                    gameTimerManager.RestartGameWithCurrentDifficulty();
                    picPreview.Image = null;
                }
            }

            // Preview frissítés
            renderer.UpdatePreview();

            // Ellenőrzés, kész-e a Nonogram
            if (grid.IsSolved())
            {
                MessageBox.Show("Gratulálok, kész a Nonogram!");
                gameTimerManager.Stop();
            }
        }

        // Megoldás gomb esemény
        private void BtnSolve_Click(object sender, EventArgs e)
        {
            renderer.SolveNonogram();
        }

        // Segítség gomb esemény
        private void BtnHint_Click(object sender, EventArgs e)
        {
            renderer.ShowHint();
        }

        // Ellenőrzés gomb esemény
        private void BtnCheck_Click(object sender, EventArgs e)
        {
            btnCheck.Enabled = false;
            renderer.CheckSolution();
        }

        private void ChkShowX_CheckedChanged(object sender, EventArgs e)
        {
            bool show = ((CheckBox)sender).Checked;

            renderer.ToggleXMarks(show);

            // Show X mód = teljes grid tiltása
            for (int i = 0; i < gridButtons.GetLength(0); i++)
            {
                for (int j = 0; j < gridButtons.GetLength(1); j++)
                {
                    gridButtons[i, j].Enabled = !show;
                }
            }
            if (chkShowX.Checked)
            {
                gameTimer.Stop();
            } 
            else
            {
                gameTimer.Start();
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (renderer == null) return; // ha még nincs létrehozva a grid, ne csináljunk semmit

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Title = "Mentés képként";
                sfd.Filter = "PNG kép|*.png";
                sfd.FileName = "nonogram.png";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    undoredoManager.SaveGrid(sfd.FileName);
                    MessageBox.Show("Állapot sikeresen elmentve!", "Mentés",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void BtnLoadSaved_Click(object sender, EventArgs e)
        {
            if (renderer == null) return; // nincs grid létrehozva

            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Kép betöltése";
                ofd.Filter = "PNG kép|*.png";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        undoredoManager.LoadGrid(ofd.FileName);
                        MessageBox.Show("Állapot sikeresen betöltve!", "Betöltés",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Hiba a betöltés során:\n" + ex.Message,
                            "Hiba", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void BtnSaveGame_Click(object sender, EventArgs e)
        {
            if (!grid.IsSolved()) // Ha nincs kész
            {
                MessageBox.Show("A játék még nincs kész. Csak kész feladványt lehet menteni!", "Mentés hiba", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            // Kivesszük a felhasználónév TextBoxból a nevet
            TextBox txtUsername = this.Controls.Find("txtUsername", true).FirstOrDefault() as TextBox;
            string currentUsername = txtUsername?.Text.Trim();

            if (string.IsNullOrEmpty(currentUsername))
            {
                MessageBox.Show("Kérlek, add meg a felhasználóneved!", "Hiba", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "Nonogram mentés (*.json)|*.json";
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                saveLoadManager.SaveGame(sfd.FileName, currentUsername);
                MessageBox.Show("A játék sikeresen elmentve!", "Mentés", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void BtnLoadGame_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Nonogram mentés (*.json)|*.json";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                saveLoadManager.LoadGame(ofd.FileName);
                MessageBox.Show("A játék betöltve!", "Betöltés", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void BtnUndo_Click(object sender, EventArgs e)
        {
            undoredoManager.Undo();
        }

        private void BtnRedo_Click(object sender, EventArgs e)
        {
            undoredoManager.Redo();
        }

        private void CmbDifficultyOrMode_Changed(object sender, EventArgs e)
        {
            gameTimerManager.DifficultyOrModeChanged();
            undoredoManager.ClearHistory();
        }

        private void BtnLeaderboard_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Válassz ki egy mentett játékot a ranglistához";
                ofd.Filter = "Nonogram mentés (*.json)|*.json";
                ofd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                if (ofd.ShowDialog() != DialogResult.OK)
                    return; // felhasználó megszakította

                string folder = Path.GetDirectoryName(ofd.FileName);

                // Ellenőrizzük, hogy van-e egyáltalán json a kiválasztott mappában
                string[] jsonFiles = Directory.GetFiles(folder, "*.json");
                if (jsonFiles.Length == 0)
                {
                    MessageBox.Show("Ebben a mappában nincs ranglistázható mentés.", "Hiba", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Betöltjük a mentéseket
                DataTable leaderboard = LeaderboardManager.LoadAllSaves(folder);

                if (leaderboard.Rows.Count == 0)
                {
                    MessageBox.Show("Ebben a mappában nincs ranglistázható mentés.", "Hiba", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                ShowLeaderboard(leaderboard);
            }
        }

        private void ShowLeaderboard(DataTable table)
        {
            f = new Form();
            f.Text = "Ranglista";
            f.Size = new Size(800, 500);

            // --- Label a nehézség combo fölé ---
            Label lblDifficulty = new Label();
            lblDifficulty.Text = "Nehézség:";
            lblDifficulty.Location = new Point(20, 0);
            lblDifficulty.AutoSize = true;
            f.Controls.Add(lblDifficulty);

            // --- Szűrő ComboBox a nehézséghez ---
            cmbDifficultyFilter = new ComboBox();
            cmbDifficultyFilter.Items.Add("Összes");
            cmbDifficultyFilter.Items.AddRange(new string[] { "Könnyű", "Közepes", "Nehéz" });
            cmbDifficultyFilter.SelectedIndex = 0;
            cmbDifficultyFilter.Location = new Point(20, lblDifficulty.Bottom + 3);
            f.Controls.Add(cmbDifficultyFilter);

            // --- Label a játékmód combo fölé ---
            Label lblMode = new Label();
            lblMode.Text = "Játékmód:";
            lblMode.Location = new Point(200, 0);
            lblMode.AutoSize = true;
            f.Controls.Add(lblMode);

            // --- Szűrő ComboBox a játékmódhoz ---
            cmbModeFilter = new ComboBox();
            cmbModeFilter.Items.Add("Összes");
            cmbModeFilter.Items.AddRange(new string[] { "Fekete-fehér", "Színes" });
            cmbModeFilter.SelectedIndex = 0;
            cmbModeFilter.Location = new Point(200, lblMode.Bottom + 3);
            f.Controls.Add(cmbModeFilter);

            // --- BindingSource a szűréshez ---
            bs = new BindingSource();
            bs.DataSource = table;

            // --- DataGridView ---
            DataGridView dgv = new DataGridView();
            dgv.Top = cmbDifficultyFilter.Bottom + 10;
            dgv.Left = 20;
            dgv.Width = f.ClientSize.Width - 40;
            dgv.Height = f.ClientSize.Height - dgv.Top - 20;
            dgv.ReadOnly = true;
            dgv.AutoGenerateColumns = true;
            dgv.DataSource = bs;
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            f.Controls.Add(dgv);

            ApplyFilter();
            f.ShowDialog();
        }

        // --- Szűrés logika ---
        private void ApplyFilter()
        {
            string diff = cmbDifficultyFilter.SelectedItem?.ToString() ?? "Összes";
            string mode = cmbModeFilter.SelectedItem?.ToString() ?? "Összes";

            string filter = "";

            if (diff != "Összes")
                filter += $"[Nehézség] = '{diff}'";

            if (mode != "Összes")
            {
                if (filter != "") filter += " AND ";
                filter += $"[Játékmód] = '{mode}'";
            }

            bs.Filter = filter;
            cmbDifficultyFilter.SelectedIndexChanged += (s, e) => ApplyFilter();
            cmbModeFilter.SelectedIndexChanged += (s, e) => ApplyFilter();
        }  

        public string GetDifficultyName()
        {
            switch (cmbDifficulty.SelectedIndex)
            {
                case 0: return "konnyu";
                case 1: return "kozepes";
                case 2: return "nehez";
                default: return "ismeretlen";
            }
        }

        public string GetModeName()
        {
            return isColor ? "szines" : "fekete-feher";
        }
    }
}