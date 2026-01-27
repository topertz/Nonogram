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
        public Button btnSolve, btnHint, btnCheck;
        public Button btnGenerateRandom;
        public Button btnLeaderboard;
        public Button btnSelectImage;
        public Button btnShowExtraSolution;
        public Button btnPickColor;
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
        public CheckBox chkShowX;
        public CheckBox chkExtraMode;
        public Color[][] rowClueColors;
        public Color[][] colClueColors;
        public int colorSimilarityThreshold = 40;
        private bool[,] hintActive;
        private bool hintShown = false;
        public int fixedGridTop;
        public FlowLayoutPanel colorPalette;
        public Color selectedColor = Color.Black; // alapértelmezett szín
        public Button btnUndo, btnRedo;
        public Stack<Color[,]> undoStack = new Stack<Color[,]>();
        public Stack<Color[,]> redoStack = new Stack<Color[,]>();
        public Image img;
        public int wrongCellClicks = 0;
        public int wrongColorClicks = 0;
        public Label lblWrongCellClicks;
        public Label lblWrongColorClicks;
        public Label lblUsername;
        public TextBox txtUsername;
        public int hintCount = 0;
        public Label lblHintCount;
        public ComboBox cmbDifficulty;
        public ComboBox cmbMode;
        public ComboBox cmbDifficultyFilter, cmbModeFilter;
        public Timer gameTimer;
        public int elapsedSeconds = 0;
        public int remainingSeconds = 0;
        public Label lblTimer;
        public int maxColors = 8;
        // Extra szinthez
        public int[,] extraClues;  // a felhasználó által beírt számok
        public RichTextBox[] rowClueInputs;   // sorok
        public RichTextBox[] colClueInputs;   // oszlopok
        public List<int>[] rowCluesExtra;
        public List<int>[] colCluesExtra;
        private RichTextBox currentColorTextBox = null;
        private RichTextBox activeClueTextBox = null;
        public Dictionary<RichTextBox, List<Color>> textBoxColors = new Dictionary<RichTextBox, List<Color>>();
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
        public int maxWrongCellClicks;
        public int maxWrongColorClicks;
        public BindingSource bs;
        public DataGridView dgvLeaderboard;
        public Random rnd = new Random();
        public Form f;
        public NonogramGrid grid;
        public NonogramRenderer renderer;
        public UndoRedoManager undoredoManager;
        public GameTimerManager gameTimerManager;
        public SaveLoadManager saveLoadManager;
        public LeaderboardManager leaderBoardManager;
        public ExtraGridManager extraGridManager;
        public Nonogram()
        {
            InitializeComponent();
            InitializeCustomComponents();
            this.Size = new Size(1100, 1300);
            this.AutoScroll = true;
            cmbDifficulty.SelectionChangeCommitted += CmbDifficultyOrMode_Changed;
            cmbMode.SelectionChangeCommitted += CmbDifficultyOrMode_Changed;
            grid = new NonogramGrid(this, gameTimerManager);
            renderer = new NonogramRenderer(this, grid);
            undoredoManager = new UndoRedoManager(this);
            saveLoadManager = new SaveLoadManager(this);
            leaderBoardManager = new LeaderboardManager(this);
            extraGridManager = new ExtraGridManager(this, row, col);
            gameTimerManager = new GameTimerManager(this, grid, renderer, undoredoManager, extraGridManager);
            grid.SetGameTimerManager(gameTimerManager);
            grid.InitializeGridPosition();
            btnSolve.Visible = false;
            btnHint.Visible = false;
            btnCheck.Visible = false;
            btnUndo.Visible = false;
            btnRedo.Visible = false;
            cmbDifficulty.Visible = false;
            cmbMode.Visible = false;
            chkShowX.Visible = false;
            chkExtraMode.Visible = false;
            btnSelectImage.Visible = false;
            btnShowExtraSolution.Visible = false;
            btnPickColor.Visible = false;
            lblTimer.Visible = false;
            picPreview.Visible = false;
            picSolutionPreview.Visible = false;
            foreach (Control c in this.Controls)
            {
                c.TabStop = false;
            }
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

            // Megoldás előnézet PictureBox
            picSolutionPreview = new PictureBox();
            picSolutionPreview.Size = new Size(previewSize, previewSize);
            picSolutionPreview.Location = new Point(picPreview.Right + 20, 20);
            picSolutionPreview.BorderStyle = BorderStyle.FixedSingle;
            picSolutionPreview.BackColor = Color.White;
            this.Controls.Add(picSolutionPreview);

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

            // X-ek megjelenítése checkbox
            chkShowX = new CheckBox();
            chkShowX.Text = "Kitöltetlen cellák megjelenítése";
            chkShowX.Location = new Point(180, picPreview.Bottom + 20);
            chkShowX.AutoSize = true;
            chkShowX.Checked = false; // alapból ki van kapcsolva
            chkShowX.CheckedChanged += ChkShowX_CheckedChanged;
            this.Controls.Add(chkShowX);

            chkExtraMode = new CheckBox();
            chkExtraMode.Text = "Extra mód";
            chkExtraMode.Location = new Point(400, 250);
            chkExtraMode.AutoSize = true;
            chkExtraMode.CheckedChanged += ChkExtraMode_CheckedChanged;
            this.Controls.Add(chkExtraMode);

            // Undo gomb
            btnUndo = new Button();
            btnUndo.Text = "Visszavonás";
            btnUndo.Location = new Point(400, btnCheck.Top);
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

            // Nehézségi szint választó
            cmbDifficulty = new ComboBox();
            cmbDifficulty.Items.AddRange(new string[] { "Könnyű", "Közepes", "Nehéz", "Nagyon nehéz" });
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

            // Színpaletta helyének beállítása a comboboxok alatt
            int paletteTop = Math.Max(cmbDifficulty.Bottom, cmbMode.Bottom) + 20;
            colorPalette.Location = new Point(20, paletteTop);

            // Helytelen kattintások számláló Label
            lblWrongCellClicks = new Label();
            lblWrongCellClicks.Text = $"Helytelen kattintások száma: {wrongCellClicks} (max: 15)";
            lblWrongCellClicks.Location = new Point(20, 630); // tetszőleges pozíció
            lblWrongCellClicks.AutoSize = true;
            this.Controls.Add(lblWrongCellClicks);

            // Helytelen színek számláló Label
            lblWrongColorClicks = new Label();
            lblWrongColorClicks.Text = $"Helytelen színek száma: {wrongColorClicks} (max: 15)";
            lblWrongColorClicks.Location = new Point(20, 650); // tetszőleges pozíció
            lblWrongColorClicks.AutoSize = true;
            this.Controls.Add(lblWrongColorClicks);

            lblHintCount = new Label();
            lblHintCount.Text = "Segítségek száma: 0";
            lblHintCount.Location = new Point(20, lblWrongCellClicks.Bottom + 30);
            lblHintCount.AutoSize = true;
            this.Controls.Add(lblHintCount);

            btnLeaderboard = new Button();
            btnLeaderboard.Text = "Ranglista";
            btnLeaderboard.Location = new Point(20, 700);
            btnLeaderboard.Click += BtnLeaderboard_Click;
            this.Controls.Add(btnLeaderboard);

            btnSelectImage = new Button();
            btnSelectImage.Text = "Kép kiválasztása";
            btnSelectImage.Size = new Size(100, 25);
            btnSelectImage.Location = new Point(175, 220);
            btnSelectImage.Visible = false;
            btnSelectImage.Click += BtnSelectImage_Click;
            this.Controls.Add(btnSelectImage);

            btnShowExtraSolution = new Button();
            btnShowExtraSolution.Text = "Megoldás (Extra)";
            btnShowExtraSolution.Size = new Size(100, 25);
            btnShowExtraSolution.Location = new Point(400, 700); // tetszőleges pozíció
            btnShowExtraSolution.Click += BtnShowExtraSolution_Click;
            this.Controls.Add(btnShowExtraSolution);

            btnPickColor = new Button();
            btnPickColor.Text = "Válassz színt";
            btnPickColor.Size = new Size(100, 25);
            btnPickColor.Location = new Point(600, 700); // tetszőleges hely
            btnPickColor.Click += BtnPickColor_Click;
            this.Controls.Add(btnPickColor);

            // Felhasználónév Label
            lblUsername = new Label();
            lblUsername.Text = "Felhasználónév:";
            lblUsername.Location = new Point(20, 600);
            lblUsername.AutoSize = true;
            this.Controls.Add(lblUsername);

            // Felhasználónév TextBox
            txtUsername = new TextBox();
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
            // Username ellenőrzése
            TextBox txtUsername = this.Controls.Find("txtUsername", true).FirstOrDefault() as TextBox;
            if (txtUsername == null)
                return;

            string currentUsername = txtUsername.Text.Trim();
            if (string.IsNullOrEmpty(currentUsername))
            {
                MessageBox.Show("Kérlek, add meg a felhasználóneved a játék indítása előtt!", "Hiba", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return; // gomb nem tűnik el, játék nem indul
            }

            // Username fixálása
            username = currentUsername;
            txtUsername.Enabled = false;  // teljesen letiltjuk, nem lehet kattintani

            // Gombok és vezérlők megjelenítése
            btnSolve.Visible = true;
            btnHint.Visible = true;
            btnCheck.Visible = true;
            btnUndo.Visible = true;
            btnRedo.Visible = true;
            cmbDifficulty.Visible = true;
            cmbMode.Visible = true;
            chkShowX.Visible = true;
            chkExtraMode.Visible = true;
            lblTimer.Visible = true;
            picPreview.Visible = true;
            picSolutionPreview.Visible = true;
            lblWrongColorClicks.Visible = false;
            chkShowX.Enabled = true;
            btnSolve.Enabled = true;
            btnHint.Enabled = true;
            btnCheck.Enabled = true;
            btnUndo.Enabled = true;
            btnRedo.Enabled = true;

            // A Generate gomb már elrejthető, mert név van
            btnGenerateRandom.Visible = false;

            // Grid előkészítése
            grid.ClearGrid();
            gameTimerManager.ResetCellCliks();
            gameTimerManager.ResetColorClicks();
            undoredoManager.ClearHistory();
            picPreview.Image = null;
            wrongCellClicks = 0;

            if (chkExtraMode.Checked)
            {
                // Grid előkészítése
                grid.ClearGrid();
                undoredoManager.ClearHistory();

                btnSolve.Visible = false;
                btnHint.Visible = false;
                btnCheck.Visible = false;
                btnUndo.Visible = false;
                btnRedo.Visible = false;
                btnLeaderboard.Visible = false;
                cmbDifficulty.Visible = false;
                chkShowX.Visible = false;
                lblTimer.Visible = false;
                lblHintCount.Visible = false;
                lblWrongCellClicks.Visible = false;
                lblWrongColorClicks.Visible = false;
                lblUsername.Visible = false;
                txtUsername.Visible = false;
                picPreview.Visible = false;
                picSolutionPreview .Visible = false;
                btnSelectImage.Visible = false;
                // Extra UI
                btnShowExtraSolution.Visible = true;
                btnPickColor.Visible = isColor;
                colorPalette.Visible = false;

                // Extra grid + clue inputok
                extraGridManager.InitializeExtraRowInputs();
                extraGridManager.InitializeExtraColumnInputs();
                extraGridManager.InitializeExtraGrid();
                return;
            }
            // A difficulty szerint generálunk
            switch (cmbDifficulty.SelectedIndex)
            {
                case 0: // Könnyű
                    grid.GenerateRandomNonogram(20, 150, row, col);
                    break;

                case 1: // Közepes
                    grid.GenerateMediumNonogram();
                    break;

                case 2: // Nehéz
                    chkShowX.Visible = true;
                    cmbMode.Visible = false;

                    btnSelectImage.Visible = true;   // fontos
                    MessageBox.Show(
                        "Kattints a 'Kép kiválasztása' gombra a játék indításához.",
                        "Nehéz szint",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                    break;
                case 3: // Nagyon nehéz
                    chkShowX.Visible = true;
                    cmbMode.Visible = true;  // felhasználó dönthet színes vagy fekete-fehér között
                    btnSelectImage.Visible = true;

                    MessageBox.Show(
                        "Kattints a 'Kép kiválasztása' gombra a játék indításához. A kép színesen maradhat, ha színes módot választasz.",
                        "Nagyon nehéz szint",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                    break;
            }
            btnShowExtraSolution.Visible = false;
            btnPickColor.Visible = false;
            extraGridManager.ClearAllClueInputs();
            renderer.ToggleXMarks(chkShowX.Checked);
            renderer.UpdatePreview();
            if (cmbDifficulty.SelectedIndex != 2) // nem nehéz
            {
                gameTimerManager.StartTimer();
            }
            picSolutionPreview.Image = grid.GeneratePreviewImage();
            picSolutionPreview.SizeMode = PictureBoxSizeMode.Zoom;
            gameTimerManager.SetMaxWrongClicksByDifficulty();
        }

        private void ChkExtraMode_CheckedChanged(object sender, EventArgs e)
        {
            if (chkExtraMode.Checked)
            {
                grid.ClearGrid();
                // Extra módba lépés: UI átállítás, inputok előkészítése
                btnSolve.Visible = false;
                btnHint.Visible = false;
                btnCheck.Visible = false;
                btnUndo.Visible = false;
                btnRedo.Visible = false;
                btnLeaderboard.Visible = false;
                cmbDifficulty.Visible = false;
                chkShowX.Visible = false;
                lblTimer.Visible = false;
                lblHintCount.Visible = false;
                lblWrongCellClicks.Visible = false;
                lblWrongColorClicks.Visible = false;
                lblUsername.Visible = false;
                txtUsername.Visible = false;
                picPreview.Visible = false;
                picSolutionPreview.Visible = false;
                btnSelectImage.Visible = false;
                btnGenerateRandom.Visible = false;
                cmbMode.Visible = true;

                // Extra UI
                btnShowExtraSolution.Visible = true;
                btnPickColor.Visible = isColor;
                colorPalette.Visible = false;

                // Extra grid + clue inputok
                extraGridManager.InitializeExtraRowInputs();
                extraGridManager.InitializeExtraColumnInputs();
                extraGridManager.InitializeExtraGrid();
            }
            else
            {
                grid.ClearGrid();
                extraGridManager.ClearAllClueInputs();
                cmbMode.Visible = false;
                chkExtraMode.Visible = false;
                btnShowExtraSolution.Visible = false;
                btnPickColor.Visible = false;
                lblUsername.Visible = true;
                txtUsername.Visible = true;
                txtUsername.Enabled = true;
                lblWrongCellClicks.Visible = true;
                lblWrongColorClicks.Visible = true;
                lblHintCount.Visible = true;
                btnLeaderboard.Visible = true;
                btnGenerateRandom.Visible = true;
                cmbDifficulty.SelectedIndex = 0;
                cmbMode.SelectedIndex = 0;
                isColor = false;
                txtUsername.Text = "";
            }
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

            bool wrongCell = false;
            bool wrongColor = false;

            // Ha az adott cella X-jelölt, ez mindig hibás
            if (userXMark[rowIndex, colIndex])
            {
                wrongCell = true;
            }

            // Mentés a visszavonáshoz
            undoredoManager.SaveState();

            // Cellamódosítás
            grid.HandleGridClick(btn, selectedColor);

            if (!userXMark[rowIndex, colIndex])
            {
                bool isEmpty = isColor
                    ? userColorRGB[rowIndex, colIndex] == Color.White
                    : userColor[rowIndex, colIndex] == 0;

                if (!isEmpty)
                {
                    if (isColor)
                    {
                        // színes: helyes cella, de rossz szín?
                        if (solutionColorRGB[rowIndex, colIndex] == Color.White)
                        {
                            wrongCell = true; // nem kellett volna színezni
                        }
                        else if (userColorRGB[rowIndex, colIndex].ToArgb() !=
                                 solutionColorRGB[rowIndex, colIndex].ToArgb())
                        {
                            wrongColor = true; // rossz szín
                        }
                    }
                    else
                    {
                        // fekete-fehér
                        if (userColor[rowIndex, colIndex] != solutionBW[rowIndex, colIndex])
                        {
                            wrongCell = true;
                        }
                    }
                }
            }

            // Hibák kezelése
            if (wrongCell)
            {
                if (wrongCellClicks < maxWrongCellClicks)
                    wrongCellClicks++;

                lblWrongCellClicks.Text = $"Helytelen kattintások száma: {wrongCellClicks} (max: {maxWrongCellClicks})";
            }

            if (wrongColor && isColor)
            {
                if (wrongColorClicks < maxWrongColorClicks)
                    wrongColorClicks++;

                lblWrongColorClicks.Text = $"Helytelen színek száma: {wrongColorClicks} (max: {maxWrongColorClicks})";
            }

            if (wrongCellClicks >= maxWrongCellClicks)
            {
                MessageBox.Show("Elérted a maximális hibák számát a helytelen cella kattintásnál! A játék újraindul.",
                                "Figyelem", MessageBoxButtons.OK, MessageBoxIcon.Information);

                // Játék újraindítása ugyanazzal a nehézséggel
                gameTimerManager.RestartGameWithCurrentDifficulty();
                return; // kilépünk a click eseményből, hogy ne folytassa
            }

            if (wrongColorClicks >= maxWrongColorClicks)
            {
                MessageBox.Show("Elérted a maximális hibák számát a helytelen szín kattintásnál! A játék újraindul.",
                                "Figyelem", MessageBoxButtons.OK, MessageBoxIcon.Information);

                // Játék újraindítása ugyanazzal a nehézséggel
                gameTimerManager.RestartGameWithCurrentDifficulty();
                return; // kilépünk a click eseményből, hogy ne folytassa
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
                    gridButtons[i, j].TabStop = false;
                }
            }
            if (chkShowX.Checked)
            {
                gameTimer.Stop();
            } 
            else
            {
                chkShowX.Enabled = false;
                gameTimer.Start();
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

        private void BtnSelectImage_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Képfájlok|*.png;*.jpg;*.jpeg;*.bmp";
            ofd.Title = "Kép kiválasztása Nonogramhoz";

            if (ofd.ShowDialog() != DialogResult.OK)
                return;

            Image loadedImg = Image.FromFile(ofd.FileName);
            Bitmap bmpToUse;

            // Logika meghatározása a játékmód (cmbMode) vagy nehézség alapján
            // Ha a játékmód 0. indexe a "Fekete-fehér"
            bool useGrayscale = (cmbDifficulty.SelectedIndex == 2);

            if (useGrayscale)
            {
                bmpToUse = renderer.ConvertToBlackAndWhite(new Bitmap(loadedImg));
            }
            else
            {
                bmpToUse = new Bitmap(loadedImg);
            }

            // Nagyon nehéz szinten (3) extra feldolgozás (háttér eltávolítás)
            if (cmbDifficulty.SelectedIndex == 3)
            {
                bmpToUse = renderer.RemoveLightBackground(bmpToUse, 200);
            }

            img = bmpToUse;

            // Megjelenítés a megoldás előnézetben
            picSolutionPreview.Image = bmpToUse;
            picSolutionPreview.SizeMode = PictureBoxSizeMode.Zoom;

            // A grid elhelyezése (mivel a chkGrayscale kikerült, a chkShowX-hez igazítjuk)
            int gridLeft = 20;
            int gridTop = chkShowX.Bottom + 20;

            // Korábbi grid törlése
            grid.ClearGrid();

            // Új grid generálása a kiválasztott képből
            grid.GenerateNonogramFromImage(bmpToUse, gridLeft, gridTop);

            // Gombok és vezérlők engedélyezése
            btnSolve.Enabled = true;
            btnHint.Enabled = true;
            btnCheck.Enabled = true;
            btnRedo.Enabled = true;
            btnUndo.Enabled = true;
            chkShowX.Enabled = true;
            lblTimer.Visible = true;

            // Időzítő indítása
            if (cmbDifficulty.SelectedIndex != 2 || cmbDifficulty.SelectedIndex != 3)
                gameTimerManager.Start();

            // Előnézet frissítése
            renderer.UpdatePreview();
        }

        private void BtnLeaderboard_Click(object sender, EventArgs e)
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
                DataTable leaderboard = leaderBoardManager.LoadAllSaves(folder);

                if (leaderboard.Rows.Count == 0)
                {
                    MessageBox.Show("Ebben a mappában nincs ranglistázható mentés.", "Hiba", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Megjelenítjük a ranglistát
                leaderBoardManager.ShowLeaderboard(leaderboard);
            }
        }
        public void BtnShowExtraSolution_Click(object sender, EventArgs e)
        {
            if (!extraGridManager.ReadExtraClues())
                return;

            if (!isColor) // fekete-fehér
            {
                solutionBW = new int[row, col];
                bool solvedBW = extraGridManager.SolveNonogram(solutionBW, rowCluesExtra, colCluesExtra);

                if (!solvedBW)
                {
                    MessageBox.Show("A Nonogram nem oldható meg a megadott clues alapján!", "Hiba", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                extraGridManager.ApplySolutionToGridBW();
            }
            else // színes
            {
                if (!extraGridManager.PrepareTextBoxColors()) return; // először a színeket összegyűjtjük

                // Meghívjuk a SolveColorNonogram-t, visszatérési értékkel
                solutionColorRGB = new Color[row, col]; // inicializáljuk a gridet

                bool solvedColor = extraGridManager.SolveColorNonogram(
                    solutionColorRGB,
                    rowCluesExtra,
                    colCluesExtra,
                    textBoxColors
                );

                if (!solvedColor)
                {
                    MessageBox.Show("A színes Nonogram nem oldható meg!", "Hiba",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                extraGridManager.ApplySolutionToGridColor(); // alkalmazzuk a gridre
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

        public void ClueTextBox_TextChanged(object sender, EventArgs e)
        {
            RichTextBox rtb = sender as RichTextBox;
            if (rtb == null || !textBoxColors.ContainsKey(rtb)) return;

            int cursorPos = rtb.SelectionStart;
            string[] parts = rtb.Text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

            int charPos = 0;
            List<Color> colors = textBoxColors[rtb];

            for (int i = 0; i < parts.Length; i++)
            {
                Color color = (i < colors.Count) ? colors[i] : Color.Black;

                rtb.SelectionStart = charPos;
                rtb.SelectionLength = parts[i].Length;
                rtb.SelectionColor = color;

                charPos += parts[i].Length + 1; // szóköz
            }

            // Visszaállítjuk a kurzor helyét
            rtb.SelectionStart = cursorPos;
            rtb.SelectionLength = 0;
        }
        private void BtnPickColor_Click(object sender, EventArgs e)
        {
            if (activeClueTextBox == null) return;

            RichTextBox rtb = activeClueTextBox;

            using (ColorDialog cd = new ColorDialog())
            {
                if (cd.ShowDialog() == DialogResult.OK)
                {
                    // Ha nincs kijelölés, jelöljük ki az első számot
                    if (rtb.SelectionLength == 0)
                    {
                        string[] parts = rtb.Text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0)
                        {
                            rtb.SelectionStart = 0;
                            rtb.SelectionLength = parts[0].Length;
                        }
                    }

                    rtb.SelectionColor = cd.Color;

                    // Mentjük logikailag
                    if (!textBoxColors.ContainsKey(rtb))
                        textBoxColors[rtb] = new List<Color>();

                    List<Color> colors = textBoxColors[rtb];

                    // Találjuk meg, melyik számot színezzük
                    int selectionIndex = extraGridManager.GetCurrentNumberIndex(rtb);
                    while (colors.Count <= selectionIndex)
                        colors.Add(Color.White);
                    colors[selectionIndex] = cd.Color;
                }
            }
        }
    }
}