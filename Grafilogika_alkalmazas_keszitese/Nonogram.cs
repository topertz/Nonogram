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
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace Grafilogika_alkalmazas_keszitese
{
    public partial class Nonogram : Form
    {
        public Button btnSolve, btnHint, btnCheck, btnGenerateRandom, btnLeaderboard, btnSelectImage, btnShowExtraSolution;
        public Button btnPickColor, btnResetExtraGrid, btnSaveClues, btnLoadClues, btnTips, btnRestart, btnUndo, btnRedo;
        public Button btnSmartAI;
        public PictureBox picPreview, picSolutionPreview;
        public CheckBox chkShowX, chkExtraMode, chkColorMode, chkBlackWhiteMode;
        public FlowLayoutPanel colorPalette;
        public Label lblWrongCellClicks, lblWrongColorClicks, lblUsername, lblExtra, lblHintCount;
        public TextBox txtUsername;
        public ComboBox cmbDifficulty, cmbMode;
        public Label lblTimer, lblUndoCount, lblRedoCount;
        public string username;
        public Random rnd = new Random();
        public ToolTip toolTip;
        public NonogramGrid grid;
        public NonogramRender render;
        public UndoRedoManager undoredoManager;
        public GameTimerManager gameTimerManager;
        public LeaderboardManager leaderBoardManager;
        public ExtraGridManager extraGridManager;
        public NonogramSolver solver;
        public NonogramSmartAI nonogramSmartAI;
        public Nonogram()
        {
            InitializeComponent();
            leaderBoardManager = new LeaderboardManager(this, grid, render, gameTimerManager);
            undoredoManager = new UndoRedoManager(this, grid);
            nonogramSmartAI = new NonogramSmartAI(this, grid, render, gameTimerManager);
            grid = new NonogramGrid(this, null, render, undoredoManager, extraGridManager);
            extraGridManager = new ExtraGridManager(this, grid);
            render = new NonogramRender(this, null, null, undoredoManager, leaderBoardManager);
            gameTimerManager = new GameTimerManager(this, grid, render, undoredoManager);
            solver = new NonogramSolver(grid);
            InitializeCustomComponents();
            this.Size = new Size(1100, 850);
            this.AutoScroll = true;
            this.MaximizeBox = false;
            this.ActiveControl = null;
            cmbDifficulty.SelectionChangeCommitted += gameTimerManager.CmbDifficultyOrMode_Changed;
            cmbMode.SelectionChangeCommitted += gameTimerManager.CmbDifficultyOrMode_Changed;
            render.SetGrid(grid);
            grid.SetRender(render);
            grid.SetTimerManager(gameTimerManager);
            render.SetTimerManager(gameTimerManager);
            grid.SetExtraGridManager(extraGridManager);
            undoredoManager.SetGrid(grid);
            solver.SetGrid(grid);
            leaderBoardManager.SetGrid(grid);
            leaderBoardManager.SetRender(render);
            leaderBoardManager.SetTimerManager(gameTimerManager);
            nonogramSmartAI.SetGrid(grid);
            nonogramSmartAI.SetRender(render);
            nonogramSmartAI.SetTimerManager(gameTimerManager);
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
            btnSmartAI.Visible = false;
            lblTimer.Visible = false;
            picPreview.Visible = false;
            picSolutionPreview.Visible = false;
            toolTip = new ToolTip
            {
                AutoPopDelay = 8000,    // Mennyi ideig marad látható a buborék (8 mp)
                InitialDelay = 400,     // Várakozás a megjelenés előtt, ha rátartod az egeret (0.4 mp)
                ReshowDelay = 200,      // Várakozás, ha egyik elemről a másikra mozgatod az egeret (0.2 mp)
                ShowAlways = true,      // Akkor is látszik, ha az ablak épp nincs fókuszban
                IsBalloon = true        // Modern, lekerekített szövegbuborék forma használata
            };
            gameTimerManager.UpdateDifficultyAndModeToolTip();
        }

        private void InitializeCustomComponents()
        {
            // Mini előnézet
            picPreview = new PictureBox();
            picPreview.Size = new Size(render.previewSize, render.previewSize);
            picPreview.Location = new Point(20, 20);
            picPreview.BorderStyle = BorderStyle.FixedSingle;
            picPreview.BackColor = Color.White;
            this.Controls.Add(picPreview);

            // Megoldás előnézet PictureBox
            picSolutionPreview = new PictureBox();
            picSolutionPreview.Size = new Size(render.previewSize, render.previewSize);
            picSolutionPreview.Location = new Point(picPreview.Right + 20, 20);
            picSolutionPreview.BorderStyle = BorderStyle.FixedSingle;
            picSolutionPreview.BackColor = Color.White;
            this.Controls.Add(picSolutionPreview);

            // Solve gomb
            btnSolve = new Button();
            btnSolve.Text = "Megoldás";
            btnSolve.Location = new Point(115, 650);
            btnSolve.Size = new Size(100, 25);
            btnSolve.Click += render.BtnSolve_Click;
            btnSolve.FlatStyle = FlatStyle.Flat;
            btnSolve.BackColor = Color.LightBlue;
            btnSolve.FlatAppearance.BorderSize = 0;
            btnSolve.Font = new Font("Arial", 10, FontStyle.Bold);
            this.Controls.Add(btnSolve);

            // Hint gomb
            btnHint = new Button();
            btnHint.Size = new Size(100, 25);
            btnHint.Location = new Point(230, 650);
            btnHint.FlatStyle = FlatStyle.Flat;
            btnHint.BackColor = Color.YellowGreen;
            btnHint.Font = new Font("Arial", 10, FontStyle.Bold);
            btnHint.Text = "Segítség";
            btnHint.FlatAppearance.BorderSize = 0;
            btnHint.Click += render.BtnHint_Click;
            this.Controls.Add(btnHint);

            // Check gomb
            btnCheck = new Button();
            btnCheck.Text = "Ellenőrzés";
            btnCheck.Location = new Point(345, 650);
            btnCheck.Size = new Size(100, 25);
            btnCheck.Click += render.BtnCheck_Click;
            btnCheck.FlatStyle = FlatStyle.Flat;
            btnCheck.BackColor = Color.Red;
            btnCheck.FlatAppearance.BorderSize = 0;
            btnCheck.Font = new Font("Arial", 10, FontStyle.Bold);
            this.Controls.Add(btnCheck);

            btnSmartAI = new Button();
            btnSmartAI.Text = "AI";
            btnSmartAI.Location = new Point(345, 700);
            btnSmartAI.Size = new Size(100, 25);
            btnSmartAI.Click += nonogramSmartAI.BtnSmartAIGuide_Click;
            btnSmartAI.FlatStyle = FlatStyle.Flat;
            btnSmartAI.BackColor = Color.LightGreen;
            btnSmartAI.FlatAppearance.BorderSize = 0;
            btnSmartAI.Font = new Font("Arial", 10, FontStyle.Bold);
            this.Controls.Add(btnSmartAI);

            // X-ek megjelenítése checkbox
            chkShowX = new CheckBox();
            chkShowX.Text = "Kitöltetlen cellák megjelenítése";
            chkShowX.Location = new Point(180, picPreview.Bottom + 20);
            chkShowX.AutoSize = true;
            chkShowX.Checked = false;
            chkShowX.CheckedChanged += render.ChkShowX_CheckedChanged;
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
            chkExtraMode.CheckedChanged += extraGridManager.ChkExtraMode_CheckedChanged;
            chkExtraMode.Font = new Font("Arial", 10, FontStyle.Bold);
            this.Controls.Add(chkExtraMode);

            chkBlackWhiteMode = new CheckBox();
            chkBlackWhiteMode.Text = "Fekete-fehér képbeolvasós mód";
            chkBlackWhiteMode.Location = new Point(170, 575); // a Színes mód fölé
            chkBlackWhiteMode.AutoSize = true;
            chkBlackWhiteMode.CheckedChanged += extraGridManager.ChkBlackWhiteMode_CheckedChanged;
            chkBlackWhiteMode.Font = new Font("Arial", 10, FontStyle.Bold);
            this.Controls.Add(chkBlackWhiteMode);

            chkColorMode = new CheckBox();
            chkColorMode.Text = "Színes képbeolvasós mód";
            chkColorMode.Location = new Point(170, 600);
            chkColorMode.AutoSize = true;
            chkColorMode.CheckedChanged += extraGridManager.ChkColorMode_CheckedChanged;
            chkColorMode.Font = new Font("Arial", 10, FontStyle.Bold);
            this.Controls.Add(chkColorMode);

            // Undo gomb
            btnUndo = new Button();
            btnUndo.Size = new Size(120, 25);
            btnUndo.Location = new Point(460, 650);
            btnUndo.FlatStyle = FlatStyle.Flat;
            btnUndo.BackColor = Color.Black;
            btnUndo.ForeColor = Color.White;
            btnUndo.Font = new Font("Arial", 10, FontStyle.Bold);
            btnUndo.Text = "Visszavonás";
            btnUndo.FlatAppearance.BorderSize = 0;
            btnUndo.Click += undoredoManager.BtnUndo_Click;
            this.Controls.Add(btnUndo);

            // Redo gomb
            btnRedo = new Button();
            btnRedo.Size = new Size(120, 25);
            btnRedo.Location = new Point(590, 650);
            btnRedo.FlatStyle = FlatStyle.Flat;
            btnRedo.BackColor = Color.Black;
            btnRedo.ForeColor = Color.White;
            btnRedo.Font = new Font("Arial", 10, FontStyle.Bold);
            btnRedo.Text = "Előrelépés";
            btnRedo.FlatAppearance.BorderSize = 0;
            btnRedo.Click += undoredoManager.BtnRedo_Click;
            this.Controls.Add(btnRedo);

            // Véletlen Nonogram generálás gomb
            btnGenerateRandom = new Button();
            btnGenerateRandom.Text = "Indítás";
            btnGenerateRandom.Location = new Point(20, 580);
            btnGenerateRandom.Size = new Size(100, 25);
            btnGenerateRandom.Click += grid.BtnGenerateRandom_Click;
            btnGenerateRandom.FlatStyle = FlatStyle.Flat;
            btnGenerateRandom.BackColor = Color.LightGreen;
            btnGenerateRandom.FlatAppearance.BorderSize = 0;
            btnGenerateRandom.Font = new Font("Arial", 10, FontStyle.Bold);
            this.Controls.Add(btnGenerateRandom);

            // Nehézségi szint választó
            cmbDifficulty = new ComboBox();
            cmbDifficulty.Location = new Point(20, 340);
            cmbDifficulty.Width = 150;
            cmbDifficulty.Font = new Font("Arial", 10, FontStyle.Bold);
            cmbDifficulty.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbDifficulty.DrawMode = DrawMode.OwnerDrawFixed;
            cmbDifficulty.ItemHeight = 24;

            cmbDifficulty.Items.AddRange(new string[] { "Könnyű", "Közepes", "Nehéz" });

            Image[] icons = {
                 Properties.Resources.happy,
                 Properties.Resources.neutral,
                 Properties.Resources.sad
            };

            cmbDifficulty.DrawItem += (s, e) =>
            {
                if (e.Index < 0) return;

                bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
                bool isComboBoxEdit = (e.State & DrawItemState.ComboBoxEdit) == DrawItemState.ComboBoxEdit;

                Brush background = (isSelected && !isComboBoxEdit) ? new SolidBrush(SystemColors.Highlight) : Brushes.White;
                e.Graphics.FillRectangle(background, e.Bounds);

                Brush textBrush = (isSelected && !isComboBoxEdit) ? Brushes.White : Brushes.Black;

                string text = cmbDifficulty.Items[e.Index].ToString();
                SizeF textSize = e.Graphics.MeasureString(text, e.Font);
                e.Graphics.DrawString(text, e.Font, textBrush, e.Bounds.Left, e.Bounds.Top + 2);

                int imageX = e.Bounds.Left + (int)textSize.Width + 5;
                e.Graphics.DrawImage(icons[e.Index], imageX, e.Bounds.Top, 20, 20);

                if (!isComboBoxEdit && (e.State & DrawItemState.Focus) == DrawItemState.Focus)
                    e.DrawFocusRectangle();
            };
            cmbDifficulty.SelectedIndex = 0;
            this.Controls.Add(cmbDifficulty);

            // Játék mód választó (fekete-fehér / színes)
            cmbMode = new ComboBox();
            cmbMode.Items.AddRange(new string[] { "Fekete-fehér", "Színes" });
            cmbMode.SelectedIndex = 0;
            cmbMode.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbMode.Location = new Point(200, 340);
            cmbMode.Width = 150;
            cmbMode.Font = new Font("Arial", 10, FontStyle.Bold);
            this.Controls.Add(cmbMode);

            // Színpaletta panel
            colorPalette = new FlowLayoutPanel();
            colorPalette.Size = new Size(350, 50);       
            colorPalette.Location = new Point(175, 250);
            colorPalette.AutoScroll = true;              
            colorPalette.WrapContents = true;            
            colorPalette.FlowDirection = FlowDirection.LeftToRight;
            colorPalette.AutoSize = false;               
            this.Controls.Add(colorPalette);

            // Helytelen kattintások számláló Label
            lblWrongCellClicks = new Label();
            lblWrongCellClicks.Text = $"Helytelen kattintások száma: {grid.wrongCellClicks} (max: {grid.maxWrongCellClicks})";
            lblWrongCellClicks.Location = new Point(20, 420);
            lblWrongCellClicks.AutoSize = true;
            lblWrongCellClicks.Font = new Font("Arial", 10, FontStyle.Bold);
            this.Controls.Add(lblWrongCellClicks);

            // Helytelen színek számláló Label
            lblWrongColorClicks = new Label();
            lblWrongColorClicks.Text = $"Helytelen színek száma: {grid.wrongColorClicks} (max: {grid.maxWrongColorClicks})";
            lblWrongColorClicks.Location = new Point(20, 440);
            lblWrongColorClicks.AutoSize = true;
            lblWrongColorClicks.Font = new Font("Arial", 10, FontStyle.Bold);
            this.Controls.Add(lblWrongColorClicks);

            lblHintCount = new Label();
            lblHintCount.Text = $"Segítségek száma: {render.hintCount} (max: {render.maxHintCount})";
            lblHintCount.Location = new Point(20, 460);
            lblHintCount.AutoSize = true;
            lblHintCount.Font = new Font("Arial", 10, FontStyle.Bold);
            this.Controls.Add(lblHintCount);

            lblUndoCount = new Label();
            lblUndoCount.Text = $"Visszavonások száma: {undoredoManager.undoClicks} (max: {undoredoManager.maxUndoClicks})";
            lblUndoCount.Location = new Point(20, 480);
            lblUndoCount.AutoSize = true;
            lblUndoCount.Font = new Font("Arial", 10, FontStyle.Bold);
            this.Controls.Add(lblUndoCount);

            lblRedoCount = new Label();
            lblRedoCount.Text = $"Előrelépések száma: {undoredoManager.redoClicks} (max: {undoredoManager.maxRedoClicks})";
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
            btnSelectImage.Click += render.BtnSelectImage_Click;
            btnSelectImage.FlatStyle = FlatStyle.Flat;
            btnSelectImage.BackColor = Color.RosyBrown;
            btnSelectImage.FlatAppearance.BorderSize = 0;
            btnSelectImage.Font = new Font("Arial", 10, FontStyle.Bold);
            this.Controls.Add(btnSelectImage);

            btnShowExtraSolution = new Button();
            btnShowExtraSolution.Text = "Megoldás (Beírogatós)";
            btnShowExtraSolution.Size = new Size(180, 25);
            btnShowExtraSolution.Location = new Point(500, 700);
            btnShowExtraSolution.Click += extraGridManager.BtnShowExtraSolution_Click;
            btnShowExtraSolution.FlatStyle = FlatStyle.Flat;
            btnShowExtraSolution.BackColor = Color.LightSkyBlue;
            btnShowExtraSolution.FlatAppearance.BorderSize = 0;
            btnShowExtraSolution.Font = new Font("Arial", 10, FontStyle.Bold);
            this.Controls.Add(btnShowExtraSolution);

            btnPickColor = new Button();
            btnPickColor.Text = "Válassz színt";
            btnPickColor.Size = new Size(110, 25);
            btnPickColor.Location = new Point(700, 700);
            btnPickColor.Click += extraGridManager.BtnPickColor_Click;
            btnPickColor.FlatStyle = FlatStyle.Flat;
            btnPickColor.BackColor = Color.LightSeaGreen;
            btnPickColor.FlatAppearance.BorderSize = 0;
            btnPickColor.Font = new Font("Arial", 10, FontStyle.Bold);
            this.Controls.Add(btnPickColor);

            // Gomb létrehozása
            btnResetExtraGrid = new Button();
            btnResetExtraGrid.Size = new Size(100, 25);
            btnResetExtraGrid.Location = new Point(830, 700);
            btnResetExtraGrid.FlatStyle = FlatStyle.Flat;
            btnResetExtraGrid.BackColor = Color.Red;
            btnResetExtraGrid.Font = new Font("Arial", 10, FontStyle.Bold);
            btnResetExtraGrid.Text = "Törlés";
            btnResetExtraGrid.FlatAppearance.BorderSize = 0;
            btnResetExtraGrid.Click += extraGridManager.BtnClearGrid_Click;
            this.Controls.Add(btnResetExtraGrid);

            btnSaveClues = new Button();
            btnSaveClues.Text = "Számok mentése";
            btnSaveClues.Size = new Size(140, 25);
            btnSaveClues.Location = new Point(600, 740);
            btnSaveClues.Click += extraGridManager.BtnSaveClues_Click;
            btnSaveClues.FlatStyle = FlatStyle.Flat;
            btnSaveClues.BackColor = Color.Black;
            btnSaveClues.ForeColor = Color.White;
            btnSaveClues.FlatAppearance.BorderSize = 0;
            btnSaveClues.Font = new Font("Arial", 10, FontStyle.Bold);
            Controls.Add(btnSaveClues);

            btnLoadClues = new Button();
            btnLoadClues.Text = "Számok betöltése";
            btnLoadClues.Size = new Size(140, 25);
            btnLoadClues.Location = new Point(760, 740);
            btnLoadClues.Click += extraGridManager.BtnLoadClues_Click;
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
            btnTips.Click += gameTimerManager.BtnTips_Click;
            btnTips.FlatStyle = FlatStyle.Flat;
            btnTips.BackColor = Color.Orange;
            btnTips.FlatAppearance.BorderSize = 0;
            btnTips.Font = new Font("Arial", 10, FontStyle.Bold);
            this.Controls.Add(btnTips);

            btnRestart = new Button();
            btnRestart.Text = "Új feladvány generálása";
            btnRestart.Size = new Size(190, 25);
            btnRestart.Location = new Point(110, 310);
            btnRestart.Click += gameTimerManager.BtnRestart_Click;
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
            txtUsername.Text = username;
            txtUsername.Name = "txtUsername";
            txtUsername.Font = new Font("Arial", 10, FontStyle.Bold);
            this.Controls.Add(txtUsername);

            // Időzítő Label
            lblTimer = new Label();
            lblTimer.Text = "00:00";
            lblTimer.Font = new Font("Arial", 16, FontStyle.Bold);
            lblTimer.Size = new Size(100, 30);
            lblTimer.Location = new Point(600, 10);
            lblTimer.AutoSize = true;
            this.Controls.Add(lblTimer);
        }
    }
}