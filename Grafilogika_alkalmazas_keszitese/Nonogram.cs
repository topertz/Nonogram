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
        public Button btnSolve, btnHint, btnCheck, btnGenerateRandom, btnLeaderboard, btnSelectImage, btnShowExtraSolution;
        public Button btnPickColor, btnResetExtraGrid, btnSaveClues, btnLoadClues, btnTips, btnRestart, btnUndo, btnRedo;
        private int clueSize = 50;
        public PictureBox picPreview, picSolutionPreview;
        public CheckBox chkShowX, chkExtraMode, chkColorMode, chkBlackWhiteMode;
        public bool isXMode = false;
        public FlowLayoutPanel colorPalette;
        public Image img;
        public Label lblWrongCellClicks, lblWrongColorClicks, lblUsername, lblExtra, lblHintCount;
        public TextBox txtUsername;
        public ComboBox cmbDifficulty, cmbMode;
        public Label lblTimer, lblUndoCount, lblRedoCount;
        public string username;
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
            undoredoManager = new UndoRedoManager(this, grid);
            saveLoadManager = new SaveLoadManager(this, gameTimerManager);
            grid = new NonogramGrid(this, null, render, undoredoManager, extraGridManager, hintEngine);
            extraGridManager = new ExtraGridManager(this, grid);
            render = new NonogramRender(this, null, null, undoredoManager, saveLoadManager);
            hintEngine = new NonogramHintEngine(grid);
            gameTimerManager = new GameTimerManager(this, grid, render, undoredoManager, extraGridManager, hintEngine);
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
            grid.SetHintEngine(hintEngine);
            undoredoManager.SetGrid(grid);
            solver.SetGrid(grid);
            saveLoadManager.SetGrid(grid);
            saveLoadManager.SetRender(render);
            saveLoadManager.SetTimerManager(gameTimerManager);
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

            // Gombok, grid még nincs
            int btnTop = picPreview.Bottom + 20 + (grid.userCellSize * grid.col) + clueSize; // ideiglenes, majd frissítjük a grid után

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
            btnHint.Click += render.BtnHint_Click;

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
            btnCheck.Click += render.BtnCheck_Click;
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
            btnUndo.Click += undoredoManager.BtnUndo_Click;

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
            btnRedo.Click += undoredoManager.BtnRedo_Click;

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
            btnGenerateRandom.Click += grid.BtnGenerateRandom_Click;
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
            cmbDifficulty.SelectionChangeCommitted += gameTimerManager.CmbDifficultyOrMode_Changed;

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
            lblWrongCellClicks.Text = $"Helytelen kattintások száma: {grid.wrongCellClicks} (max: {grid.maxWrongCellClicks})";
            lblWrongCellClicks.Location = new Point(20, 420); // tetszőleges pozíció
            lblWrongCellClicks.AutoSize = true;
            lblWrongCellClicks.Font = new Font("Arial", 10, FontStyle.Bold);
            this.Controls.Add(lblWrongCellClicks);

            // Helytelen színek számláló Label
            lblWrongColorClicks = new Label();
            lblWrongColorClicks.Text = $"Helytelen színek száma: {grid.wrongColorClicks} (max: {grid.maxWrongColorClicks})";
            lblWrongColorClicks.Location = new Point(20, 440); // tetszőleges pozíció
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
            btnShowExtraSolution.Location = new Point(500, 700); // tetszőleges pozíció
            btnShowExtraSolution.Click += extraGridManager.BtnShowExtraSolution_Click;
            btnShowExtraSolution.FlatStyle = FlatStyle.Flat;
            btnShowExtraSolution.BackColor = Color.LightSkyBlue;
            btnShowExtraSolution.FlatAppearance.BorderSize = 0;
            btnShowExtraSolution.Font = new Font("Arial", 10, FontStyle.Bold);
            this.Controls.Add(btnShowExtraSolution);

            btnPickColor = new Button();
            btnPickColor.Text = "Válassz színt";
            btnPickColor.Size = new Size(110, 25);
            btnPickColor.Location = new Point(700, 700); // tetszőleges hely
            btnPickColor.Click += extraGridManager.BtnPickColor_Click;
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
            btnResetExtraGrid.Click += extraGridManager.BtnClearGrid_Click;

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
            btnSaveClues.Click += extraGridManager.BtnSaveClues_Click;
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
    }
}