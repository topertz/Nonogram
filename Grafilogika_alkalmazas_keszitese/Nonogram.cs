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

namespace Grafilogika_alkalmazas_keszitese
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
        public Button btnRestart;
        public int[][] rowClues;
        public int[][] colClues;
        public int row = 15;
        public int col = 15;
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
        public Stack<Tuple<Color[,], int, int, bool>> undoStack = new Stack<Tuple<Color[,], int, int, bool>>();
        public Stack<Tuple<Color[,], int, int, bool>> redoStack = new Stack<Tuple<Color[,], int, int, bool>>();
        public Image img;
        public int wrongCellClicks = 0;
        public int wrongColorClicks = 0;
        public Label lblWrongCellClicks;
        public Label lblWrongColorClicks;
        public Label lblUsername;
        public Label lblExtra;
        public TextBox txtUsername;
        public int hintCount = 0;
        public Label lblHintCount;
        public ComboBox cmbDifficulty;
        public ComboBox cmbMode;
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
        public bool[,] isHintFixed;
        public int highlightedRow = -1;
        public int highlightedCol = -1;
        public List<Point> hintCells = new List<Point>();
        public bool showHints = true;
        public int maxUndoClicks = 0;
        public int maxRedoClicks = 0;
        public int undoClicks = 0;
        public int redoClicks = 0;
        public Label lblUndoCount;
        public Label lblRedoCount;
        public bool lastActionWasX = false;
        public Random rnd = new Random();
        public ToolTip toolTip;
        public Form f;
        public NonogramGrid grid;
        public NonogramRender render;
        public UndoRedoManager undoredoManager;
        public GameTimerManager gameTimerManager;
        public SaveLoadManager saveLoadManager;
        public LeaderboardManager leaderBoardManager;
        public ExtraGridManager extraGridManager;
        public NonogramSolver solver;
        public NonogramHintEngine hintEngine;
        public Nonogram()
        {
            InitializeComponent();
            leaderBoardManager = new LeaderboardManager();
            InitializeCustomComponents();
            this.Size = new Size(1100, 850);
            this.AutoScroll = true;
            this.MaximizeBox = false;
            cmbDifficulty.SelectionChangeCommitted += CmbDifficultyOrMode_Changed;
            cmbMode.SelectionChangeCommitted += CmbDifficultyOrMode_Changed;
            hintEngine = new NonogramHintEngine(
            this, row, col, isColor,
            solutionBW, solutionColorRGB, // Feltételezve, hogy ezek public property-k a gridben
            userColorRGB, userXMark
            );
            undoredoManager = new UndoRedoManager(this);
            saveLoadManager = new SaveLoadManager(this);
            extraGridManager = new ExtraGridManager(this, row, col);
            grid = new NonogramGrid(this, gameTimerManager, render);
            gameTimerManager = new GameTimerManager(this, grid, render, undoredoManager, extraGridManager, hintEngine);
            render = new NonogramRender(this, grid, gameTimerManager);
            gameTimerManager.SetGameTimerManager(gameTimerManager);
            grid.SetRender(render);
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
            btnRestart.Visible = false;
            lblTimer.Visible = false;
            picPreview.Visible = false;
            picSolutionPreview.Visible = false;
            foreach (Control c in this.Controls)
            {
                c.TabStop = false;
            }
            toolTip = new ToolTip
            {
                AutoPopDelay = 8000,
                InitialDelay = 400,
                ReshowDelay = 200,
                ShowAlways = true
            };
            gameTimerManager.UpdateDifficultyAndModeToolTip();
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
            btnSolve.Location = new Point(115, 650);
            btnSolve.Size = new Size(100, 25);
            btnSolve.Click += BtnSolve_Click;
            btnSolve.FlatStyle = FlatStyle.Flat;
            btnSolve.BackColor = Color.LightBlue;
            btnSolve.FlatAppearance.BorderSize = 0;
            btnSolve.Font = new Font("Arial", 10, FontStyle.Bold);
            this.Controls.Add(btnSolve);

            // Hint gomb
            btnHint = new Button
            {
                Size = new Size(100, 25),
                Location = new Point(230, 650),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.YellowGreen,
                Font = new Font("Arial", 10, FontStyle.Bold),
                Text = "Segítség"
            };
            btnHint.FlatAppearance.BorderSize = 0;
            btnHint.Click += BtnHint_Click;

            // Ikon betöltése a Resources-ból
            Image hintIcon = Properties.Resources.hint;

            // Középre igazítás szöveg + ikon
            btnHint.Paint += (s, e) =>
            {
                Button btn = s as Button;
                e.Graphics.Clear(btn.BackColor);

                SizeF textSize = e.Graphics.MeasureString(btn.Text, btn.Font);
                int iconSize = 20;
                int spacing = 5;
                float totalWidth = textSize.Width + spacing + iconSize;

                float startX = Math.Max(0, (btn.Width - totalWidth) / 2);
                float textY = (btn.Height - textSize.Height) / 2;
                e.Graphics.DrawString(btn.Text, btn.Font, Brushes.White, startX, textY);

                float iconY = (btn.Height - iconSize) / 2;
                e.Graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver; // átlátszó ikon
                e.Graphics.DrawImage(hintIcon, startX + textSize.Width + spacing, iconY, iconSize, iconSize);

                if (btn.Focused)
                    ControlPaint.DrawFocusRectangle(e.Graphics, btn.ClientRectangle);
            };

            // Hozzáadás a formhoz
            this.Controls.Add(btnHint);

            // Check gomb
            btnCheck = new Button();
            btnCheck.Text = "Ellenőrzés";
            btnCheck.Location = new Point(345, 650);
            btnCheck.Size = new Size(100, 25);
            btnCheck.Click += BtnCheck_Click;
            btnCheck.FlatStyle = FlatStyle.Flat;
            btnCheck.BackColor = Color.Red;
            btnCheck.FlatAppearance.BorderSize = 0;
            btnCheck.Font = new Font("Arial", 10, FontStyle.Bold);
            this.Controls.Add(btnCheck);

            // X-ek megjelenítése checkbox
            chkShowX = new CheckBox();
            chkShowX.Text = "Kitöltetlen cellák megjelenítése";
            chkShowX.Location = new Point(180, picPreview.Bottom + 20);
            chkShowX.AutoSize = true;
            chkShowX.Checked = false; // alapból ki van kapcsolva
            chkShowX.CheckedChanged += ChkShowX_CheckedChanged;
            chkShowX.Font = new Font("Arial", 10, FontStyle.Bold);
            this.Controls.Add(chkShowX);

            lblExtra = new Label();
            lblExtra.Text = "Extra funkciók";
            lblExtra.Location = new Point(170, 530);
            lblExtra.AutoSize = true;
            lblExtra.ForeColor = Color.Red;
            lblExtra.Font = new Font("Arial", 10, FontStyle.Bold);
            this.Controls.Add(lblExtra);

            chkExtraMode = new CheckBox();
            chkExtraMode.Text = "Beírogatós mód";
            chkExtraMode.Location = new Point(170, 550);
            chkExtraMode.AutoSize = true;
            chkExtraMode.CheckedChanged += ChkExtraMode_CheckedChanged;
            chkExtraMode.Font = new Font("Arial", 10, FontStyle.Bold);
            this.Controls.Add(chkExtraMode);

            chkBlackWhiteMode = new CheckBox();
            chkBlackWhiteMode.Text = "Fekete-fehér képbeolvasós mód";
            chkBlackWhiteMode.Location = new Point(170, 575); // a Színes mód fölé
            chkBlackWhiteMode.AutoSize = true;
            chkBlackWhiteMode.CheckedChanged += ChkBlackWhiteMode_CheckedChanged;
            chkBlackWhiteMode.Font = new Font("Arial", 10, FontStyle.Bold);
            this.Controls.Add(chkBlackWhiteMode);

            chkColorMode = new CheckBox();
            chkColorMode.Text = "Színes képbeolvasós mód";
            chkColorMode.Location = new Point(170, 600);
            chkColorMode.AutoSize = true;
            chkColorMode.CheckedChanged += ChkColorMode_CheckedChanged;
            chkColorMode.Font = new Font("Arial", 10, FontStyle.Bold);
            this.Controls.Add(chkColorMode);

            // Undo gomb
            btnUndo = new Button
            {
                Size = new Size(120, 25),
                Location = new Point(460, 650),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Black,
                ForeColor = Color.White,
                Font = new Font("Arial", 10, FontStyle.Bold),
                Text = "Visszavonás"
            };
            btnUndo.FlatAppearance.BorderSize = 0;
            btnUndo.Click += BtnUndo_Click;

            // Redo gomb
            btnRedo = new Button
            {
                Size = new Size(120, 25),
                Location = new Point(590, 650),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Black,
                ForeColor = Color.White,
                Font = new Font("Arial", 10, FontStyle.Bold),
                Text = "Előrelépés"
            };
            btnRedo.FlatAppearance.BorderSize = 0;
            btnRedo.Click += BtnRedo_Click;

            // Undo ikon betöltése a Resources-ból
            Image undoIcon = Properties.Resources.undo;

            // Redo ikon betöltése a Resources-ból
            Image redoIcon = Properties.Resources.redo;

            // Középre igazítás szöveg + ikon: Undo
            btnUndo.Paint += (s, e) =>
            {
                Button btn = s as Button;
                e.Graphics.Clear(btn.BackColor);

                SizeF textSize = e.Graphics.MeasureString(btn.Text, btn.Font);
                int iconWidth = 20;
                int spacing = 5;
                float totalWidth = textSize.Width + spacing + iconWidth;

                float startX = (btn.Width - totalWidth) / 2;
                float textY = (btn.Height - textSize.Height) / 2;
                e.Graphics.DrawString(btn.Text, btn.Font, Brushes.White, startX, textY);

                float iconY = (btn.Height - iconWidth) / 2;
                e.Graphics.DrawImage(undoIcon, startX + textSize.Width + spacing, iconY, iconWidth, iconWidth);

                if (btn.Focused)
                    ControlPaint.DrawFocusRectangle(e.Graphics, btn.ClientRectangle);
            };

            // Középre igazítás szöveg + ikon: Redo
            btnRedo.Paint += (s, e) =>
            {
                Button btn = s as Button;
                e.Graphics.Clear(btn.BackColor);

                SizeF textSize = e.Graphics.MeasureString(btn.Text, btn.Font);
                int iconWidth = 20;
                int spacing = 5;
                float totalWidth = textSize.Width + spacing + iconWidth;

                float startX = (btn.Width - totalWidth) / 2;
                float textY = (btn.Height - textSize.Height) / 2;
                e.Graphics.DrawString(btn.Text, btn.Font, Brushes.White, startX, textY);

                float iconY = (btn.Height - iconWidth) / 2;
                e.Graphics.DrawImage(redoIcon, startX + textSize.Width + spacing, iconY, iconWidth, iconWidth);

                if (btn.Focused)
                    ControlPaint.DrawFocusRectangle(e.Graphics, btn.ClientRectangle);
            };

            // Hozzáadás a formhoz
            this.Controls.Add(btnUndo);
            this.Controls.Add(btnRedo);

            // Véletlen Nonogram generálás gomb
            btnGenerateRandom = new Button();
            btnGenerateRandom.Text = "Indítás";
            btnGenerateRandom.Location = new Point(20, 580);
            btnGenerateRandom.Size = new Size(100, 25);
            btnGenerateRandom.Click += BtnGenerateRandom_Click;
            btnGenerateRandom.FlatStyle = FlatStyle.Flat;
            btnGenerateRandom.BackColor = Color.LightGreen;
            btnGenerateRandom.FlatAppearance.BorderSize = 0;
            btnGenerateRandom.Font = new Font("Arial", 10, FontStyle.Bold);
            this.Controls.Add(btnGenerateRandom);

            // Nehézségi szint választó
            cmbDifficulty = new ComboBox
            {
                Location = new Point(20, 340),
                Width = 150,
                Font = new Font("Arial", 10, FontStyle.Bold),
                DropDownStyle = ComboBoxStyle.DropDownList,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 24
            };

            // Szövegek hozzáadása
            cmbDifficulty.Items.AddRange(new string[] { "Könnyű", "Közepes", "Nehéz" });

            Image[] icons = {
                 Properties.Resources.happy,
                 Properties.Resources.neutral,
                 Properties.Resources.sad
            };

            // Rajzolás: szöveg + kép a végén
            cmbDifficulty.DrawItem += (s, e) =>
            {
                if (e.Index < 0) return;

                bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
                bool isComboBoxEdit = (e.State & DrawItemState.ComboBoxEdit) == DrawItemState.ComboBoxEdit;

                // Ha a legördülő listában van és kiválasztott → sötét kék, egyébként fehér
                Brush background = (isSelected && !isComboBoxEdit) ? new SolidBrush(SystemColors.Highlight) : Brushes.White;
                e.Graphics.FillRectangle(background, e.Bounds);

                // Szöveg színe: kiválasztott listában → fehér, egyébként fekete
                Brush textBrush = (isSelected && !isComboBoxEdit) ? Brushes.White : Brushes.Black;

                string text = cmbDifficulty.Items[e.Index].ToString();
                SizeF textSize = e.Graphics.MeasureString(text, e.Font);
                e.Graphics.DrawString(text, e.Font, textBrush, e.Bounds.Left, e.Bounds.Top + 2);

                // Kép a szöveg után
                int imageX = e.Bounds.Left + (int)textSize.Width + 5;
                e.Graphics.DrawImage(icons[e.Index], imageX, e.Bounds.Top, 20, 20);

                // Fókusz keret csak a legördülő listában
                if (!isComboBoxEdit && (e.State & DrawItemState.Focus) == DrawItemState.Focus)
                    e.DrawFocusRectangle();
            };

            // Esemény hozzácsatolása
            cmbDifficulty.SelectionChangeCommitted += CmbDifficultyOrMode_Changed;

            cmbDifficulty.SelectedIndex = 0; // alapértelmezett
            this.Controls.Add(cmbDifficulty);

            // Játék mód választó (fekete-fehér / színes)
            cmbMode = new ComboBox();
            cmbMode.Items.AddRange(new string[] { "Fekete-fehér", "Színes" });
            cmbMode.SelectedIndex = 0; // alapértelmezett: fekete-fehér
            cmbMode.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbMode.Location = new Point(200, 340);
            cmbMode.Width = 150;
            cmbMode.Font = new Font("Arial", 10, FontStyle.Bold);
            this.Controls.Add(cmbMode);

            // Színpaletta panel
            colorPalette = new FlowLayoutPanel();
            colorPalette.Size = new Size(350, 50);       // nagyobb magasság
            colorPalette.Location = new Point(175, 250);
            colorPalette.AutoScroll = true;              // görgethető lesz, ha nem fér el
            colorPalette.WrapContents = true;            // több sorba törik a gombok
            colorPalette.FlowDirection = FlowDirection.LeftToRight; // balról jobbra
            colorPalette.AutoSize = false;               // ne próbáljon automatikusan méretezni
            this.Controls.Add(colorPalette);

            // Helytelen kattintások számláló Label
            lblWrongCellClicks = new Label();
            lblWrongCellClicks.Text = $"Helytelen kattintások száma: {wrongCellClicks} (max: {maxWrongCellClicks})";
            lblWrongCellClicks.Location = new Point(20, 420); // tetszőleges pozíció
            lblWrongCellClicks.AutoSize = true;
            lblWrongCellClicks.Font = new Font("Arial", 10, FontStyle.Bold);
            this.Controls.Add(lblWrongCellClicks);

            // Helytelen színek számláló Label
            lblWrongColorClicks = new Label();
            lblWrongColorClicks.Text = $"Helytelen színek száma: {wrongColorClicks} (max: {maxWrongColorClicks})";
            lblWrongColorClicks.Location = new Point(20, 440); // tetszőleges pozíció
            lblWrongColorClicks.AutoSize = true;
            lblWrongColorClicks.Font = new Font("Arial", 10, FontStyle.Bold);
            this.Controls.Add(lblWrongColorClicks);

            lblHintCount = new Label();
            lblHintCount.Text = $"Segítségek száma: {hintCount} (max: {maxHintCount})";
            lblHintCount.Location = new Point(20, 460);
            lblHintCount.AutoSize = true;
            lblHintCount.Font = new Font("Arial", 10, FontStyle.Bold);
            this.Controls.Add(lblHintCount);

            lblUndoCount = new Label();
            lblUndoCount.Text = $"Visszavonások száma: {undoClicks} (max: {maxUndoClicks})";
            lblUndoCount.Location = new Point(20, 480);
            lblUndoCount.AutoSize = true;
            lblUndoCount.Font = new Font("Arial", 10, FontStyle.Bold);
            this.Controls.Add(lblUndoCount);

            lblRedoCount = new Label();
            lblRedoCount.Text = $"Előrelépések száma: {redoClicks} (max: {maxRedoClicks})";
            lblRedoCount.Location = new Point(20, 500);
            lblRedoCount.AutoSize = true;
            lblRedoCount.Font = new Font("Arial", 10, FontStyle.Bold);
            this.Controls.Add(lblRedoCount);

            btnLeaderboard = new Button();
            btnLeaderboard.Text = "Ranglista";
            btnLeaderboard.Location = new Point(20, 530);
            btnLeaderboard.Size = new Size(100, 25);
            btnLeaderboard.Click += leaderBoardManager.BtnLeaderboard_Click;
            btnLeaderboard.FlatStyle = FlatStyle.Flat;
            btnLeaderboard.BackColor = Color.LightGray;
            btnLeaderboard.FlatAppearance.BorderSize = 0;
            btnLeaderboard.Font = new Font("Arial", 10, FontStyle.Bold);
            this.Controls.Add(btnLeaderboard);

            btnSelectImage = new Button();
            btnSelectImage.Text = "Kép kiválasztása";
            btnSelectImage.Size = new Size(140, 25);
            btnSelectImage.Location = new Point(175, 220);
            btnSelectImage.Visible = false;
            btnSelectImage.Click += BtnSelectImage_Click;
            btnSelectImage.FlatStyle = FlatStyle.Flat;
            btnSelectImage.BackColor = Color.RosyBrown;
            btnSelectImage.FlatAppearance.BorderSize = 0;
            btnSelectImage.Font = new Font("Arial", 10, FontStyle.Bold);
            this.Controls.Add(btnSelectImage);

            btnShowExtraSolution = new Button();
            btnShowExtraSolution.Text = "Megoldás (Beírogatós)";
            btnShowExtraSolution.Size = new Size(180, 25);
            btnShowExtraSolution.Location = new Point(500, 700); // tetszőleges pozíció
            btnShowExtraSolution.Click += BtnShowExtraSolution_Click;
            btnShowExtraSolution.FlatStyle = FlatStyle.Flat;
            btnShowExtraSolution.BackColor = Color.LightSkyBlue;
            btnShowExtraSolution.FlatAppearance.BorderSize = 0;
            btnShowExtraSolution.Font = new Font("Arial", 10, FontStyle.Bold);
            this.Controls.Add(btnShowExtraSolution);

            btnPickColor = new Button();
            btnPickColor.Text = "Válassz színt";
            btnPickColor.Size = new Size(110, 25);
            btnPickColor.Location = new Point(700, 700); // tetszőleges hely
            btnPickColor.Click += BtnPickColor_Click;
            btnPickColor.FlatStyle = FlatStyle.Flat;
            btnPickColor.BackColor = Color.LightSeaGreen;
            btnPickColor.FlatAppearance.BorderSize = 0;
            btnPickColor.Font = new Font("Arial", 10, FontStyle.Bold);
            this.Controls.Add(btnPickColor);

            // Gomb létrehozása
            btnResetExtraGrid = new Button
            {
                Size = new Size(100, 25),
                Location = new Point(830, 700),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Red,
                Font = new Font("Arial", 10, FontStyle.Bold),
                Text = "Törlés" // ikon mellé külön rajzoljuk
            };
            btnResetExtraGrid.FlatAppearance.BorderSize = 0;
            btnResetExtraGrid.Click += BtnClearGrid_Click;

            // Trash ikon betöltése a Resources-ból
            Image icon = Properties.Resources.trash;

            // Egyedi rajzolás: szöveg + ikon
            btnResetExtraGrid.Paint += (s, e) =>
            {
                Button btn = s as Button;
                e.Graphics.Clear(btn.BackColor);

                // Szöveg
                SizeF textSize = e.Graphics.MeasureString(btn.Text, btn.Font);
                float textX = 5; // balra kicsit beljebb
                float textY = (btn.Height - textSize.Height) / 2;
                e.Graphics.DrawString(btn.Text, btn.Font, Brushes.White, textX, textY);

                // Ikon a szöveg után
                int iconX = (int)(textX + textSize.Width + 5);
                int iconY = (btn.Height - 20) / 2; // 20x20-as ikon
                e.Graphics.DrawImage(icon, iconX, iconY, 20, 20);

                // Fókusz keret (opcionális)
                if ((btn.FlatStyle == FlatStyle.Flat) && btn.Focused)
                    ControlPaint.DrawFocusRectangle(e.Graphics, btn.ClientRectangle);
            };

            // Hozzáadás a formhoz
            this.Controls.Add(btnResetExtraGrid);

            btnSaveClues = new Button();
            btnSaveClues.Text = "Cluek mentése";
            btnSaveClues.Size = new Size(120, 25);
            btnSaveClues.Location = new Point(600, 740);
            btnSaveClues.Click += BtnSaveClues_Click;
            btnSaveClues.FlatStyle = FlatStyle.Flat;
            btnSaveClues.BackColor = Color.Black;
            btnSaveClues.ForeColor = Color.White;
            btnSaveClues.FlatAppearance.BorderSize = 0;
            btnSaveClues.Font = new Font("Arial", 10, FontStyle.Bold);
            Controls.Add(btnSaveClues);

            btnLoadClues = new Button();
            btnLoadClues.Text = "Cluek betöltése";
            btnLoadClues.Size = new Size(130, 25);
            btnLoadClues.Location = new Point(750, 740);
            btnLoadClues.Click += BtnLoadClues_Click;
            btnLoadClues.FlatStyle = FlatStyle.Flat;
            btnLoadClues.BackColor = Color.Black;
            btnLoadClues.ForeColor = Color.White;
            btnLoadClues.FlatAppearance.BorderSize = 0;
            btnLoadClues.Font = new Font("Arial", 10, FontStyle.Bold);
            Controls.Add(btnLoadClues);

            btnTips = new Button();
            btnTips.Text = "Tippek!!!";
            btnTips.Location = new Point(150, 300);
            btnTips.Size = new Size(100, 25);
            btnTips.Click += BtnTips_Click;
            btnTips.FlatStyle = FlatStyle.Flat;
            btnTips.BackColor = Color.Orange;
            btnTips.FlatAppearance.BorderSize = 0;
            btnTips.Font = new Font("Arial", 10, FontStyle.Bold);
            this.Controls.Add(btnTips);

            btnRestart = new Button();
            btnRestart.Text = "Új feladvány generálása";
            btnRestart.Size = new Size(190, 25);
            btnRestart.Location = new Point(110, 310);
            btnRestart.Click += BtnRestart_Click;
            btnRestart.FlatStyle = FlatStyle.Flat;
            btnRestart.BackColor = Color.MediumPurple;
            btnRestart.FlatAppearance.BorderSize = 0;
            btnRestart.Font = new Font("Arial", 10, FontStyle.Bold);
            this.Controls.Add(btnRestart);

            // Felhasználónév Label
            lblUsername = new Label();
            lblUsername.Text = "Felhasználónév:";
            lblUsername.Location = new Point(20, 385);
            lblUsername.AutoSize = true;
            lblUsername.Font = new Font("Arial", 10, FontStyle.Bold);
            this.Controls.Add(lblUsername);

            // Felhasználónév TextBox
            txtUsername = new TextBox();
            txtUsername.Location = new Point(145, 380);
            txtUsername.Width = 150;
            txtUsername.Text = username; // alapértelmezett
            txtUsername.Name = "txtUsername";
            txtUsername.Font = new Font("Arial", 10, FontStyle.Bold);
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
        private void BtnRestart_Click(object sender, EventArgs e)
        {
            gameTimerManager.RestartGameWithCurrentDifficulty();
        }

        private void BtnTips_Click(object sender, EventArgs e)
        {
            StringBuilder tips = new StringBuilder();

            tips.AppendLine("NONOGRAM TIPPEK");
            tips.AppendLine("────────────────────────\n");

            // ÁLTALÁNOS TIPPEK
            tips.AppendLine("ÁLTALÁNOS TIPPEK\n");
            tips.AppendLine("Ha egy sor vagy oszlop számainak összege + a kötelező szóközök = a sor/oszlop hossza,");
            tips.AppendLine("az egész sort/oszlopot ki lehet tölteni.\n");

            tips.AppendLine("Ha egy szám nagyobb, mint a sor/oszlop fele,");
            tips.AppendLine("a középső cellák biztosan kitöltöttek.\n");

            tips.AppendLine("Mindig először a leghosszabb számokat érdemes vizsgálni.\n");

            tips.AppendLine("A számok sorrendje mindig kötelező: a megadott számok sorrendjét nem lehet felcserélni.\n");

            tips.AppendLine("Minden kitöltött blokkot a megfelelő számhoz kell igazítani,");
            tips.AppendLine("és a blokkok között legalább egy üres cella kell legyen (kivéve színes nonogramnál, ha ugyanaz a szín ismétlődik).\n");

            // X MÓD
            tips.AppendLine("X MÓD (Kizárás)\n");
            tips.AppendLine("Jobb kattintás: X jel lerakása a cellára.");
            tips.AppendLine("Bal kattintás: a cella kitöltése (színezés).\n");

            tips.AppendLine("X-ek célja: megjelölni azokat a cellákat, amelyek biztosan NEM tartoznak a megoldáshoz.");
            tips.AppendLine("Segítenek a logikai következtetésekben és a hibák elkerülésében.\n");

            // DRAG MÓD
            tips.AppendLine("DRAG MÓD (Húzás)\n");
            tips.AppendLine("Bal egérgomb húzása: több cella kitöltése egyszerre.");
            tips.AppendLine("Jobb egérgomb húzása: több X jel lerakása egyszerre.\n");

            tips.AppendLine("Drag mód gyorsítja a játékot, különösen nagyobb rácsoknál.\n");

            // FEKETE-FEHÉR MÓD
            tips.AppendLine("FEKETE–FEHÉR MÓD\n");
            tips.AppendLine("Bal kattintás: fekete cella lerakása / törlése.");
            tips.AppendLine("Jobb kattintás: X jel lerakása vagy eltávolítása.\n");

            tips.AppendLine("Fekete-fehér szabályok:");
            tips.AppendLine("A számok sorrendje kötelező.");
            tips.AppendLine("A blokkok között legalább egy üres cella kell legyen.");
            tips.AppendLine("X-ek segítik a kizárást és logikai következtetéseket.\n");

            // SZÍNES MÓD
            tips.AppendLine("SZÍNES MÓD\n");
            tips.AppendLine("Bal kattintás vagy húzás: a kiválasztott szín lerakása.");
            tips.AppendLine("Jobb kattintás: X jel lerakása vagy eltávolítása.\n");

            tips.AppendLine("Színes nonogram szabályok:");
            tips.AppendLine("A számok sorrendje és színe mindig kötelező.");
            tips.AppendLine("Különböző színek között legalább egy üres cella van.");
            tips.AppendLine("Ugyanazon szín ismétlődő blokkja esetén az elválasztó üres cella nem szükséges.");
            tips.AppendLine("X-ek színes módban is használhatók a kizáráshoz, de vigyázat itt nem mindig egyértelmű a kizárás.\n");

            tips.AppendLine("Általános tipp: mindig ellenőrizd a sorok és oszlopok számait, és használd a logikát a hibák elkerülésére.\n");

            MessageBox.Show(
                tips.ToString(),
                "Megoldási tippek",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
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
            //picSolutionPreview.Visible = true;
            //lblWrongColorClicks.Visible = false;
            chkShowX.Enabled = true;
            btnSolve.Enabled = true;
            btnHint.Enabled = true;
            btnCheck.Enabled = true;
            btnUndo.Enabled = true;
            btnRedo.Enabled = true;
            btnTips.Visible = false;
            btnRestart.Visible = true;
            btnLeaderboard.Visible = false;
            lblExtra.Visible = false;
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
            render.ToggleXMarks(chkShowX.Checked);
            isXMode = true;
            render.UpdatePreview();
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
                lblExtra.Visible = false;
                lblUndoCount.Visible = false;
                lblRedoCount.Visible = false;
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
                lblExtra.Visible = true;
                lblUndoCount.Visible = true;
                lblRedoCount.Visible = true;
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
                //picSolutionPreview.Visible = true;
                picPreview.Image = null;
                //picSolutionPreview.Image = null;
                chkShowX.Visible = true;
                btnSolve.Visible = true;
                chkShowX.Enabled = false;
                btnSolve.Enabled = false;
                btnCheck.Enabled = false;
                btnHint.Visible = false;
                //btnCheck.Visible = true;
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
                lblExtra.Visible = false;
                lblUndoCount.Visible = false;
                lblRedoCount.Visible = false;
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
                lblExtra.Visible = true;
                lblUndoCount.Visible = true;
                lblRedoCount.Visible = true;
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
                //picSolutionPreview.Visible = true;
                picPreview.Image = null;
                //picSolutionPreview.Image = null;
                chkShowX.Visible = true;
                btnSolve.Visible = true;
                chkShowX.Enabled = false;
                btnSolve.Enabled = false;
                btnCheck.Enabled = false;
                btnHint.Visible = false;
                //btnCheck.Visible = true;
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
                lblExtra.Visible = false;
                lblUndoCount.Visible = false;
                lblRedoCount.Visible = false;
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
                lblExtra.Visible = true;
                lblUndoCount.Visible = true;
                lblRedoCount.Visible = true;
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

        public void GridCell_MouseDown(object sender, MouseEventArgs e)
        {
            Button btn = sender as Button;
            if (btn == null) return;

            Point p = (Point)btn.Tag;
            int row = p.X;
            int col = p.Y;

            // Időzítő indítása az első kattintásnál
            if (!gameTimer.Enabled) gameTimer.Start();

            // Mentés visszavonáshoz (Undo/Redo)
            if (!isDraggingStarted)
            {
                bool isXAction = (e.Button == MouseButtons.Right);
                undoredoManager.SaveState(isXAction);
                isDraggingStarted = true;
            }

            bool wrongCell = false;
            bool wrongColor = false;

            // --- BAL KLIKK: SZÍNEZÉS ---
            if (e.Button == MouseButtons.Left)
            {
                render.ClearErrorHighlights();
                // Ha volt rajta X, azt töröljük színezés előtt
                if (userXMark[row, col])
                {
                    render.ClearCell(row, col, btn);
                }

                // Színezés logikája
                if (isColor)
                {
                    if (userColorRGB[row, col].ToArgb() == selectedColor.ToArgb())
                        render.ClearCell(row, col, btn); // Toggle: ha ugyanaz, töröljük
                    else
                        render.SetCellColor(row, col, btn, selectedColor);
                }
                else
                {
                    if (userColor[row, col] == 1)
                        render.ClearCell(row, col, btn);
                    else
                        render.SetCellBlack(row, col, btn);
                }

                // HIBAELLENŐRZÉS (csak színezésnél számolunk hibát)
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
                        if (solutionBW[row, col] == 0)
                            wrongCell = true;
                    }
                }
            }
            // --- JOBB KLIKK: X JELZÉS ---
            else if (e.Button == MouseButtons.Right)
            {
                render.ClearErrorHighlights();
                if (userXMark[row, col])
                    render.ClearCell(row, col, btn);
                else
                    render.SetCellX(row, col, btn);

                // X lerakásánál általában nem számolunk hibát a Nonogramban
            }

            // Hint-ek frissítése (ha Easy fokozaton vagyunk, rögtön mutatja a következőt)
            if (cmbDifficulty.SelectedIndex == 0)
            {
                hintEngine.UpdateHints();
            }

            // UI és Preview frissítése
            grid.HandleErrorCounts(wrongCell, wrongColor);
            render.UpdatePreview(row, col);

            // Csak akkor hívjuk, ha a hiba nem indította újra a játékot
            if (grid.IsSolved())
            {
                render.FinalizeGame();
                gameTimerManager.Stop();
            }

            this.Refresh(); // A gombok Paint eseményének kényszerítése a Hint-ek miatt
        }

        public void GridCell_MouseEnter(object sender, EventArgs e)
        {
            Button btn = sender as Button;
            if (btn == null) return;

            Point p = (Point)btn.Tag;
            highlightedRow = p.X;
            highlightedCol = p.Y;

            render.RefreshGrid();
        }

        public void GridCell_MouseLeave(object sender, EventArgs e)
        {
            highlightedRow = -1;
            highlightedCol = -1;

            render.RefreshGrid();
        }

        // Megoldás gomb esemény
        private void BtnSolve_Click(object sender, EventArgs e)
        {
            render.SolveNonogram();
        }

        // Segítség gomb esemény
        private void BtnHint_Click(object sender, EventArgs e)
        {
            hintCount++;
            lblHintCount.Text = $"Segítségek száma: {hintCount} (max: {maxHintCount})";
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
            render.ShowHint();
        }

        // Ellenőrzés gomb esemény
        private void BtnCheck_Click(object sender, EventArgs e)
        {
            btnCheck.Enabled = false;
            render.CheckSolution();
        }

        private void ChkShowX_CheckedChanged(object sender, EventArgs e)
        {
            // Biztonsági ellenőrzés: ha a tömb null, megállunk
            if (gridButtons == null) return;

            bool show = chkShowX.Checked;

            render.ToggleXMarks(show);

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
            gameTimerManager.UpdateDifficultyAndModeToolTip();
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
                bmpToUse = render.ConvertToBlackAndWhite(new Bitmap(loadedImg));
            }
            else
            {
                bmpToUse = new Bitmap(loadedImg);
            }

            // Nagyon nehéz szinten (3) extra feldolgozás (háttér eltávolítás)
            if (chkColorMode.Checked)
            {
                bmpToUse = render.RemoveLightBackground(bmpToUse, 200);
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
            render.UpdatePreview();
            render.SetGridEnabled(true, chkColorMode.Checked, chkBlackWhiteMode.Checked);
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