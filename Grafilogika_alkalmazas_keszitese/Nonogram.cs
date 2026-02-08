using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
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
        public Button btnResetExtraGrid;
        private Button btnSaveClues;
        private Button btnLoadClues;
        public Button btnTips;
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
        public CheckBox chkColorMode;
        public CheckBox chkBlackWhiteMode;
        public bool isXMode = false;
        public Color[][] rowClueColors;
        public Color[][] colClueColors;
        public int colorSimilarityThreshold = 40;
        public bool gameStarted = false;
        public int fixedGridTop;
        public FlowLayoutPanel colorPalette;
        public Color selectedColor = Color.White; // alapértelmezett szín
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
        private Dictionary<RichTextBox, string> previousTexts = new Dictionary<RichTextBox, string>();
        private bool suppressTextChanged = false;
        public Color[] nonogramColors;
        public string username;
        public int maxWrongCellClicks;
        public int maxWrongColorClicks;
        public int maxHintCount;
        public bool isDragging = false;
        public Button lastProcessedButton = null;
        public MouseButtons dragButton;
        public bool isDraggingStarted = false;
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
            undoredoManager = new UndoRedoManager(this);
            saveLoadManager = new SaveLoadManager(this);
            leaderBoardManager = new LeaderboardManager(this);
            extraGridManager = new ExtraGridManager(this, row, col);
            gameTimerManager = new GameTimerManager(this, grid, renderer, undoredoManager, extraGridManager);
            renderer = new NonogramRenderer(this, grid, gameTimerManager);
            grid.SetGameTimerManager(gameTimerManager);
            grid.InitializeGridPosition();
            btnSolve.Visible = false;
            btnHint.Visible = false;
            btnCheck.Visible = false;
            btnUndo.Visible = false;
            btnRedo.Visible = false;
            cmbDifficulty.Visible = true;
            cmbMode.Visible = true;
            chkShowX.Visible = false;
            chkExtraMode.Visible = true;
            chkColorMode.Visible = true;
            chkBlackWhiteMode.Visible = true;
            btnSelectImage.Visible = false;
            btnShowExtraSolution.Visible = false;
            btnPickColor.Visible = false;
            btnResetExtraGrid.Visible = false;
            btnSaveClues.Visible = false;
            btnLoadClues.Visible = false;
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
            chkExtraMode.Text = "Beírogatós mód";
            chkExtraMode.Location = new Point(210, 580);
            chkExtraMode.AutoSize = true;
            chkExtraMode.CheckedChanged += ChkExtraMode_CheckedChanged;
            this.Controls.Add(chkExtraMode);

            chkBlackWhiteMode = new CheckBox();
            chkBlackWhiteMode.Text = "Fekete-fehér képbeolvasós mód";
            chkBlackWhiteMode.Location = new Point(210, 605); // a Színes mód fölé
            chkBlackWhiteMode.AutoSize = true;
            chkBlackWhiteMode.CheckedChanged += ChkBlackWhiteMode_CheckedChanged;
            this.Controls.Add(chkBlackWhiteMode);

            chkColorMode = new CheckBox();
            chkColorMode.Text = "Színes képbeolvasós mód";
            chkColorMode.Location = new Point(210, 630);
            chkColorMode.AutoSize = true;
            chkColorMode.CheckedChanged += ChkColorMode_CheckedChanged;
            this.Controls.Add(chkColorMode);

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
            btnGenerateRandom.Location = new Point(20, 650);
            btnGenerateRandom.Click += BtnGenerateRandom_Click;
            this.Controls.Add(btnGenerateRandom);

            // Nehézségi szint választó
            cmbDifficulty = new ComboBox();
            cmbDifficulty.Items.AddRange(new string[] { "Könnyű", "Közepes", "Nehéz" });
            cmbDifficulty.SelectedIndex = 0; // alapértelmezett: Könnyű
            cmbDifficulty.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbDifficulty.Location = new Point(20, 450);
            cmbDifficulty.Width = 150;
            this.Controls.Add(cmbDifficulty);

            // Játék mód választó (fekete-fehér / színes)
            cmbMode = new ComboBox();
            cmbMode.Items.AddRange(new string[] { "Fekete-fehér", "Színes" });
            cmbMode.SelectedIndex = 0; // alapértelmezett: fekete-fehér
            cmbMode.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbMode.Location = new Point(200, 450);
            cmbMode.Width = 150;
            this.Controls.Add(cmbMode);

            // Színpaletta panel
            colorPalette = new FlowLayoutPanel();
            colorPalette.Size = new Size(350, 200);       // nagyobb magasság
            colorPalette.Location = new Point(175, 250);
            colorPalette.AutoScroll = true;              // görgethető lesz, ha nem fér el
            colorPalette.WrapContents = true;            // több sorba törik a gombok
            colorPalette.FlowDirection = FlowDirection.LeftToRight; // balról jobbra
            colorPalette.AutoSize = false;               // ne próbáljon automatikusan méretezni
            this.Controls.Add(colorPalette);

            // Helytelen kattintások számláló Label
            lblWrongCellClicks = new Label();
            lblWrongCellClicks.Text = $"Helytelen kattintások száma: {wrongCellClicks} (max: {maxWrongCellClicks})";
            lblWrongCellClicks.Location = new Point(20, 530); // tetszőleges pozíció
            lblWrongCellClicks.AutoSize = true;
            this.Controls.Add(lblWrongCellClicks);

            // Helytelen színek számláló Label
            lblWrongColorClicks = new Label();
            lblWrongColorClicks.Text = $"Helytelen színek száma: {wrongColorClicks} (max: {maxWrongColorClicks})";
            lblWrongColorClicks.Location = new Point(20, 550); // tetszőleges pozíció
            lblWrongColorClicks.AutoSize = true;
            this.Controls.Add(lblWrongColorClicks);

            lblHintCount = new Label();
            lblHintCount.Text = $"Segítségek száma: {hintCount} (max: {maxHintCount})";
            lblHintCount.Location = new Point(20, 570);
            lblHintCount.AutoSize = true;
            this.Controls.Add(lblHintCount);

            btnLeaderboard = new Button();
            btnLeaderboard.Text = "Ranglista";
            btnLeaderboard.Location = new Point(20, 600);
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
            btnShowExtraSolution.Text = "Megoldás (Beírogatós)";
            btnShowExtraSolution.Size = new Size(140, 25);
            btnShowExtraSolution.Location = new Point(400, 700); // tetszőleges pozíció
            btnShowExtraSolution.Click += BtnShowExtraSolution_Click;
            this.Controls.Add(btnShowExtraSolution);

            btnPickColor = new Button();
            btnPickColor.Text = "Válassz színt";
            btnPickColor.Size = new Size(100, 25);
            btnPickColor.Location = new Point(580, 700); // tetszőleges hely
            btnPickColor.Click += BtnPickColor_Click;
            this.Controls.Add(btnPickColor);

            btnResetExtraGrid = new Button();
            btnResetExtraGrid.Text = "Törlés";
            btnResetExtraGrid.Size = new Size(100, 25); // méret
            btnResetExtraGrid.Location = new Point(720, 700); // hely, igazítsd a formhoz
            btnResetExtraGrid.Click += BtnClearGrid_Click;
            this.Controls.Add(btnResetExtraGrid);

            btnSaveClues = new Button();
            btnSaveClues.Text = "Clue mentése";
            btnSaveClues.Size = new Size(100, 25);
            btnSaveClues.Location = new Point(500, 750);
            btnSaveClues.Click += BtnSaveClues_Click;
            Controls.Add(btnSaveClues);

            btnLoadClues = new Button();
            btnLoadClues.Text = "Clue betöltése";
            btnLoadClues.Size = new Size(100, 25);
            btnLoadClues.Location = new Point(650, 750);
            btnLoadClues.Click += BtnLoadClues_Click;
            Controls.Add(btnLoadClues);

            btnTips = new Button();
            btnTips.Text = "Tippek";
            btnTips.Location = new Point(100, 400);
            btnTips.Click += BtnTips_Click;
            this.Controls.Add(btnTips);

            // Felhasználónév Label
            lblUsername = new Label();
            lblUsername.Text = "Felhasználónév:";
            lblUsername.Location = new Point(20, 500);
            lblUsername.AutoSize = true;
            this.Controls.Add(lblUsername);

            // Felhasználónév TextBox
            txtUsername = new TextBox();
            txtUsername.Location = new Point(110, 495);
            txtUsername.Width = 150;
            txtUsername.Text = username; // alapértelmezett
            txtUsername.Name = "txtUsername";
            this.Controls.Add(txtUsername);

            // Időzítő Label
            lblTimer = new Label();
            lblTimer.Text = "00:00";
            lblTimer.Font = new Font("Arial", 16, FontStyle.Bold);
            lblTimer.Size = new Size(100, 30);
            lblTimer.Location = new Point(600, 10); // tetszőleges pozíció
            lblTimer.AutoSize = true;
            this.Controls.Add(lblTimer);
        }

        private void BtnTips_Click(object sender, EventArgs e)
        {
            StringBuilder tips = new StringBuilder();

            tips.AppendLine("NONOGRAM TIPPEK");
            tips.AppendLine("────────────────────────\n");

            // ÁLTALÁNOS
            tips.AppendLine("ÁLTALÁNOS TIPPEK\n");
            tips.AppendLine("Ha egy sor számainak összege + a kötelező szóközök = a sor hossza,");
            tips.AppendLine("akkor az egész sort ki lehet tölteni.\n");

            tips.AppendLine("Ha egy szám nagyobb, mint a sor fele,");
            tips.AppendLine("a középső cellák biztosan kitöltöttek.\n");

            tips.AppendLine("Először mindig a leghosszabb számokat érdemes vizsgálni.\n");

            // X MÓD
            tips.AppendLine("X MÓD (Kizárás)\n");
            tips.AppendLine("Jobb kattintás: X jel lerakása a cellára.");
            tips.AppendLine("Bal kattintás: a cella kitöltése (színezés).\n");

            tips.AppendLine("X mód célja: megjelölni azokat a cellákat,");
            tips.AppendLine("amelyek biztosan NEM tartoznak a megoldáshoz.\n");

            // DRAG MÓD
            tips.AppendLine("DRAG MÓD (Húzás)\n");
            tips.AppendLine("Bal egérgomb húzása: több cella színezése egyszerre.");
            tips.AppendLine("Jobb egérgomb húzása: több X jel lerakása egyszerre.\n");

            tips.AppendLine("Drag mód jelentősen gyorsítja a játékot,");
            tips.AppendLine("különösen nagyobb rács esetén.\n");

            // FEKETE-FEHÉR
            tips.AppendLine("FEKETE–FEHÉR MÓD\n");
            tips.AppendLine("Bal kattintás: fekete cella lerakása / törlése.");
            tips.AppendLine("Jobb kattintás: X jel lerakása vagy eltávolítása.\n");

            tips.AppendLine("Használd az X-eket a kizáráshoz és a logikai következtetésekhez.\n");

            // SZÍNES
            tips.AppendLine("SZÍNES MÓD\n");
            tips.AppendLine("Bal kattintás vagy húzás: a kiválasztott szín lerakása.");
            tips.AppendLine("Jobb kattintás: színes módban nem használható.\n");

            tips.AppendLine("A színek sorrendje mindig számít!");
            tips.AppendLine("Különböző színek között legalább egy üres cella van.\n");

            MessageBox.Show(
                tips.ToString(),
                "Megoldási tippek",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }

        public void SelectColor(Button colorBtn)
        {
            selectedColor = colorBtn.BackColor;

            foreach (Button b in colorPalette.Controls)
                b.FlatAppearance.BorderColor = Color.Gray;

            colorBtn.FlatAppearance.BorderColor = Color.Red;
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

            gameStarted = true;

            isColor = cmbMode.SelectedItem?.ToString() == "Színes";
            //selectedColor = isColor ? Color.White : Color.Black;
            // Gombok és vezérlők megjelenítése
            btnSolve.Visible = true;
            btnHint.Visible = true;
            btnCheck.Visible = true;
            btnUndo.Visible = true;
            btnRedo.Visible = true;
            cmbDifficulty.Visible = false;
            cmbMode.Visible = false;
            //chkShowX.Visible = true;
            chkExtraMode.Visible = false;
            chkBlackWhiteMode.Visible = false;
            chkColorMode.Visible = false;
            lblTimer.Visible = true;
            picPreview.Visible = true;
            picSolutionPreview.Visible = true;
            //lblWrongColorClicks.Visible = false;
            chkShowX.Enabled = true;
            btnSolve.Enabled = true;
            btnHint.Enabled = true;
            btnCheck.Enabled = true;
            btnUndo.Enabled = true;
            btnRedo.Enabled = true;
            btnTips.Visible = false;
            //chkXMode.Visible = false;

            // A Generate gomb már elrejthető, mert név van
            btnGenerateRandom.Visible = false;

            // Grid előkészítése
            grid.ClearGrid();
            gameTimerManager.ResetCellCliks();
            //gameTimerManager.ResetColorClicks();
            undoredoManager.ClearHistory();
            picPreview.Image = null;
            wrongCellClicks = 0;
            btnShowExtraSolution.Visible = false;
            btnPickColor.Visible = false;
            elapsedSeconds = 0;
            gameTimerManager.DifficultyOrModeChanged();
            extraGridManager.ClearAllClueInputs();
            renderer.ToggleXMarks(chkShowX.Checked);
            isXMode = true;
            renderer.UpdatePreview();
            if (cmbDifficulty.SelectedIndex != 2) // nem nehéz
            {
                gameTimerManager.StartTimer();
            }
            picSolutionPreview.Image = grid.GeneratePreviewImage();
            picSolutionPreview.SizeMode = PictureBoxSizeMode.Zoom;
            gameTimerManager.SetMaxWrongClicksByDifficulty();
            gameTimerManager.SetMaxHintsByDifficulty();
        }
        private void ChkExtraMode_CheckedChanged(object sender, EventArgs e)
        {
            if (chkExtraMode.Checked)
            {
                // Extra módba lépés: UI átállítás, inputok előkészítése
                btnSolve.Visible = false;
                btnHint.Visible = false;
                btnCheck.Visible = false;
                btnUndo.Visible = false;
                btnRedo.Visible = false;
                btnLeaderboard.Visible = false;
                cmbDifficulty.Visible = false;
                chkShowX.Visible = false;
                chkColorMode.Visible = false;
                chkBlackWhiteMode.Visible = false;
                //chkXMode.Visible = false;
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
                cmbMode.Visible = false;
                cmbMode.SelectedIndex = 1;
                isColor = cmbMode.SelectedItem?.ToString() == "Színes";
                btnPickColor.Visible = isColor;
                btnResetExtraGrid.Visible = true;
                btnSaveClues.Visible = true;
                btnLoadClues.Visible = true;
                btnTips.Visible = false;
                // Extra UI
                btnShowExtraSolution.Visible = true;
                colorPalette.Visible = false;

                // Extra grid + clue inputok
                extraGridManager.InitializeExtraRowInputs();
                extraGridManager.InitializeExtraColumnInputs();
                extraGridManager.InitializeExtraGrid();
            }
            else
            {
                extraGridManager.ClearAllClueInputs();
                cmbDifficulty.Visible = true;
                cmbMode.Visible = true;
                chkExtraMode.Visible = true;
                chkColorMode.Visible = true;
                chkBlackWhiteMode.Visible = true;
                //chkXMode.Visible = true;
                btnShowExtraSolution.Visible = false;
                btnPickColor.Visible = false;
                btnResetExtraGrid.Visible = false;
                btnSaveClues.Visible = false;
                btnLoadClues.Visible = false;
                lblUsername.Visible = true;
                txtUsername.Visible = true;
                txtUsername.Enabled = true;
                lblWrongCellClicks.Visible = true;
                lblWrongColorClicks.Visible = true;
                lblHintCount.Visible = true;
                btnLeaderboard.Visible = true;
                btnGenerateRandom.Visible = true;
                btnTips.Visible = true;
                cmbDifficulty.SelectedIndex = 0;
                cmbMode.SelectedIndex = 0;
                isColor = false;
                txtUsername.Text = "";
            }
        }

        private void ChkBlackWhiteMode_CheckedChanged(object sender, EventArgs e)
        {
            if (chkBlackWhiteMode.Checked)
            {
                grid.ClearGrid();
                cmbDifficulty.Visible = false;
                cmbMode.Visible = false;
                lblTimer.Visible = false;
                btnSelectImage.Visible = true;
                picPreview.Visible = true;
                picSolutionPreview.Visible = true;
                picPreview.Image = null;
                picSolutionPreview.Image = null;
                chkShowX.Visible = true;
                btnSolve.Visible = true;
                chkShowX.Enabled = false;
                btnSolve.Enabled = false;
                btnCheck.Enabled = false;
                btnHint.Visible = false;
                btnCheck.Visible = true;
                btnUndo.Visible = false;
                btnRedo.Visible = false;
                chkExtraMode.Visible = false;
                chkColorMode.Visible = false;
                //chkXMode.Visible = false;
                lblUsername.Visible = false;
                txtUsername.Visible = false;
                lblWrongCellClicks.Visible = false;
                lblWrongColorClicks.Visible = false;
                lblHintCount.Visible = false;
                btnLeaderboard.Visible = false;
                btnGenerateRandom.Visible = false;
                btnTips.Visible = false;
            }
            else
            {
                grid.ClearGrid();
                cmbDifficulty.Visible = true;
                cmbMode.Visible = true;
                btnSolve.Visible = false;
                btnHint.Visible = false;
                btnCheck.Visible = false;
                btnUndo.Visible = false;
                btnRedo.Visible = false;
                lblUsername.Visible = true;
                txtUsername.Visible = true;
                txtUsername.Enabled = true;
                lblWrongCellClicks.Visible = true;
                lblWrongColorClicks.Visible = true;
                lblHintCount.Visible = true;
                btnLeaderboard.Visible = true;
                btnGenerateRandom.Visible = true;
                btnSelectImage.Visible = false;
                picPreview.Visible = false;
                picSolutionPreview.Visible = false;
                chkShowX.Visible = false;
                chkShowX.Checked = false;
                chkExtraMode.Visible = true;
                chkColorMode.Visible = true;
                //chkXMode.Visible = true;
                chkBlackWhiteMode.Visible = true;
                btnTips.Visible = true;
                cmbDifficulty.SelectedIndex = 0;
                cmbMode.SelectedIndex = 0;
                isColor = false;
                txtUsername.Text = "";
                colorPalette.Visible = false;
            }
        }

        private void ChkColorMode_CheckedChanged(object sender, EventArgs e)
        {
            if (chkColorMode.Checked)
            {
                grid.ClearGrid();
                cmbDifficulty.Visible = false;
                cmbMode.Visible = false;
                lblTimer.Visible = false;
                btnSelectImage.Visible = true;
                picPreview.Visible = true;
                picSolutionPreview.Visible = true;
                picPreview.Image = null;
                picSolutionPreview.Image = null;
                chkShowX.Visible = true;
                btnSolve.Visible = true;
                chkShowX.Enabled = false;
                btnSolve.Enabled = false;
                btnCheck.Enabled = false;
                btnHint.Visible = false;
                btnCheck.Visible = true;
                btnUndo.Visible = false;
                btnRedo.Visible = false;
                chkExtraMode.Visible = false;
                chkBlackWhiteMode.Visible = false;
                //chkXMode.Visible = false;
                lblUsername.Visible = false;
                txtUsername.Visible = false;
                lblWrongCellClicks.Visible = false;
                lblWrongColorClicks.Visible = false;
                lblHintCount.Visible = false;
                btnLeaderboard.Visible = false;
                btnGenerateRandom.Visible = false;
                btnTips.Visible = false;
            }
            else
            {
                grid.ClearGrid();
                cmbDifficulty.Visible = true;
                cmbMode.Visible = true;
                btnSolve.Visible = false;
                btnHint.Visible = false;
                btnCheck.Visible = false;
                btnUndo.Visible = false;
                btnRedo.Visible = false;
                lblUsername.Visible = true;
                txtUsername.Visible = true;
                txtUsername.Enabled = true;
                lblWrongCellClicks.Visible = true;
                lblWrongColorClicks.Visible = true;
                lblHintCount.Visible = true;
                btnLeaderboard.Visible = true;
                btnGenerateRandom.Visible = true;
                btnSelectImage.Visible = false;
                picPreview.Visible = false;
                picSolutionPreview.Visible = false;
                chkShowX.Visible = false;
                chkShowX.Checked = false;
                chkExtraMode.Visible = true;
                chkColorMode.Visible = true;
                chkBlackWhiteMode.Visible = true;
                btnTips.Visible = true;
                //chkXMode.Visible = true;
                cmbDifficulty.SelectedIndex = 0;
                cmbMode.SelectedIndex = 0;
                isColor = false;
                txtUsername.Text = "";
                colorPalette.Visible = false;
            }
        }

        private void BtnClearGrid_Click(object sender, EventArgs e)
        {
            if (!extraGridManager.IsAnythingToClear())
            {
                MessageBox.Show("A rács és a mezők üresek, nincs mit törölni!", "Hiba", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            // Ha van mit törölni, csak akkor fut le:
            extraGridManager.ClearExtraGridContents();
        }

        // Cellára kattintás esemény
        public void GridCell_Click(object sender, EventArgs e)
        {
            if (isDragging) return;
            if (isXMode)
                return; // X módban SEMMI ne fusson itt
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
                //MessageBox.Show("Gratulálok, kész a Nonogram!");
                gameTimerManager.Stop();
            }
        }

        public void GridCell_MouseDown(object sender, MouseEventArgs e)
        {
            if (!isXMode) return;
            gameTimer.Start();
            Button btn = sender as Button;
            if (btn == null) return;

            Point p = (Point)btn.Tag;
            int row = p.X;
            int col = p.Y;

            renderer.ClearErrorHighlights();

            if (!isDraggingStarted)
            {
                undoredoManager.SaveState();
                isDraggingStarted = true;
            }

            // Megállapítjuk, mi kellene oda a megoldás szerint
            bool shouldBeColor = isColor
                ? solutionColorRGB[row, col].ToArgb() != Color.White.ToArgb()
                : solutionBW[row, col] == 1;

            // BAL KLIKK -> Színezés
            if (e.Button == MouseButtons.Left)
            {
                if (isColor)
                {
                    if (btn.BackColor.ToArgb() != Color.White.ToArgb())
                    {
                        ClearCell(row, col, btn); // törlés
                    }
                    else
                    {
                        SetCellColor(row, col, btn, selectedColor); // színes
                    }
                }
                else // fekete-fehér toggle
                {
                    if (btn.BackColor.ToArgb() == Color.Black.ToArgb())
                    {
                        ClearCell(row, col, btn); // fekete → fehér
                    }
                    else
                    {
                        SetCellBlack(row, col, btn); // fehér → fekete
                    }
                }
                bool wrongCell = false;
                bool wrongColor = false;

                if (userXMark[row, col])
                {
                    wrongCell = true;
                }
                else
                {
                    bool isEmpty = isColor
                        ? userColorRGB[row, col] == Color.White
                        : userColor[row, col] == 0;

                    if (!isEmpty)
                    {
                        if (isColor)
                        {
                            if (solutionColorRGB[row, col] == Color.White)
                                wrongCell = true;
                            else if (userColorRGB[row, col].ToArgb() != solutionColorRGB[row, col].ToArgb())
                                wrongColor = true;
                        }
                        else
                        {
                            if (userColor[row, col] != solutionBW[row, col])
                                wrongCell = true;
                        }
                    }
                }

                // Hibák frissítése UI-n
                if (wrongCell)
                {
                    if (wrongCellClicks < maxWrongCellClicks) wrongCellClicks++;
                    lblWrongCellClicks.Text = $"Helytelen kattintások száma: {wrongCellClicks} (max: {maxWrongCellClicks})";
                }

                if (wrongColor && isColor)
                {
                    if (wrongColorClicks < maxWrongColorClicks) wrongColorClicks++;
                    lblWrongColorClicks.Text = $"Helytelen színek száma: {wrongColorClicks} (max: {maxWrongColorClicks})";
                }

                if (wrongCellClicks >= maxWrongCellClicks)
                {
                    MessageBox.Show("Elérted a maximális hibák számát a helytelen cella kattintásnál! A játék újraindul.");
                    gameTimerManager.RestartGameWithCurrentDifficulty();
                    return;
                }

                if (wrongColorClicks >= maxWrongColorClicks)
                {
                    MessageBox.Show("Elérted a maximális hibák számát a helytelen szín kattintásnál! A játék újraindul.");
                    gameTimerManager.RestartGameWithCurrentDifficulty();
                    return;
                }
            }
            // JOBB KLIKK -> X rakás
            else if (e.Button == MouseButtons.Right)
            {
                if (!isColor) // csak fekete-fehér módban engedjük
                {
                    if (userXMark[row, col])
                    {
                        ClearCell(row, col, btn);
                    }
                    else
                    {
                        SetCellX(row, col, btn);
                        // Ne számolja a hibákat X-nél
                    }
                }
                else
                {
                    // színes módban jobb klikk semmit nem csinál
                    return;
                }
            }

            renderer.UpdatePreviewCell(row, col);

            if (grid.IsSolved())
            {
                renderer.FinalizeGame();
            }
        }

        private void SetCellColor(int row, int col, Button btn, Color color)
        {
            userColorRGB[row, col] = color;
            userColor[row, col] = 1; // jelzés, hogy kitöltve
            btn.BackColor = color;
            btn.Text = "";
            userXMark[row, col] = false;
        }

        private void SetCellBlack(int row, int col, Button btn)
        {
            // X → fekete
            if (userXMark[row, col])
            {
                userXMark[row, col] = false;
                btn.Text = "";
            }

            btn.BackColor = Color.Black;
            userColorRGB[row, col] = Color.Black;
            userColor[row, col] = 1;
        }

        private void SetCellX(int row, int col, Button btn)
        {
            // fekete → X
            btn.BackColor = Color.White;
            userColorRGB[row, col] = Color.White;
            userColor[row, col] = 0;

            userXMark[row, col] = true;

            float fontSize = userCellSize * 0.3f;
            btn.Font = new Font("Arial", fontSize, FontStyle.Bold);
            btn.ForeColor = Color.Gray;
            btn.Text = "X";
            btn.TextAlign = ContentAlignment.MiddleCenter;
        }

        public void ClearCell(int row, int col, Button btn)
        {
            btn.BackColor = Color.White;
            btn.Text = "";
            userXMark[row, col] = false;
            userColor[row, col] = 0;
            userColorRGB[row, col] = Color.White;
        }

        // Megoldás gomb esemény
        private void BtnSolve_Click(object sender, EventArgs e)
        {
            renderer.SolveNonogram();
        }

        // Segítség gomb esemény
        private void BtnHint_Click(object sender, EventArgs e)
        {
            // Ha elérte a maximumot → restart
            if (hintCount >= maxHintCount)
            {
                MessageBox.Show(
                    "Elérted a maximális segítségek számát! A játék újraindul.",
                    "Figyelem",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );

                gameTimerManager.RestartGameWithCurrentDifficulty();
                return;
            }
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
            // Biztonsági ellenőrzés: ha a tömb null, megállunk
            if (gridButtons == null) return;

            bool show = chkShowX.Checked;

            renderer.ToggleXMarks(show);

            // Biztonságos végigfuttatás
            for (int i = 0; i < gridButtons.GetLength(0); i++)
            {
                for (int j = 0; j < gridButtons.GetLength(1); j++)
                {
                    // Ellenőrizzük, hogy a gomb példány létezik-e!
                    if (gridButtons[i, j] != null)
                    {
                        gridButtons[i, j].Enabled = !show;
                        gridButtons[i, j].TabStop = false;
                    }
                }
            }

            if (show)
            {
                gameTimer.Stop();
            }
            else
            {
                // Csak akkor állítjuk, ha a sender (a CheckBox) még engedélyezett
                // és nem egy törlési folyamat része vagyunk
                if (chkShowX.Visible)
                {
                    gameTimer.Start();
                }
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
            if (!gameStarted && !chkExtraMode.Checked)
                return;
            isColor = cmbMode.SelectedItem?.ToString() == "Színes";
            selectedColor = isColor ? Color.White : Color.Black;
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
            bool useGrayscale = (chkBlackWhiteMode.Checked);

            if (useGrayscale)
            {
                bmpToUse = renderer.ConvertToBlackAndWhite(new Bitmap(loadedImg));
            }
            else
            {
                bmpToUse = new Bitmap(loadedImg);
            }

            // Nagyon nehéz szinten (3) extra feldolgozás (háttér eltávolítás)
            if (chkColorMode.Checked)
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
            colorPalette.Visible = false;

            // Időzítő indítása
            if (cmbDifficulty.SelectedIndex != 2 || cmbDifficulty.SelectedIndex != 3)
                gameTimerManager.Start();

            // Előnézet frissítése
            renderer.UpdatePreview();
            renderer.SetGridEnabled(true, chkColorMode.Checked, chkBlackWhiteMode.Checked);
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
                bool solvedBW = extraGridManager.SolveExtraNonogramSimple(solutionBW, rowCluesExtra, colCluesExtra);

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

                bool solvedColor = extraGridManager.SolveExtraNonogramColor(
                    solutionColorRGB,
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
        private void BtnPickColor_Click(object sender, EventArgs e)
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
                            int index = extraGridManager.GetCurrentNumberIndex(rtb);
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

                    int selectionIndex = extraGridManager.GetCurrentNumberIndex(rtb);
                    while (textBoxColors[rtb].Count <= selectionIndex)
                        textBoxColors[rtb].Add(Color.Black);

                    textBoxColors[rtb][selectionIndex] = cd.Color;

                    suppressTextChanged = false;
                }
            }
        }

        private void BtnSaveClues_Click(object sender, EventArgs e)
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

        private void BtnLoadClues_Click(object sender, EventArgs e)
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
                extraGridManager.PrepareTextBoxColors();

                // Visszajelzés
                MessageBox.Show(
                    "A clue-k sikeresen be lettek töltve!",
                    "Betöltés kész",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
        }
    }
}