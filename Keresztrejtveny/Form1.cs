using System;
using System.Collections.Generic;
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
    public partial class Form1 : Form
    {
        public Button[,] gridButtons;
        public Button btnSolve, btnHint, btnCheck, btnSave, btnLoad;
        public Button btnGenerateRandom;
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
        public ComboBox cmbDifficulty;
        public ComboBox cmbMode;
        public Timer gameTimer;
        public int elapsedSeconds = 0;
        public Label lblTimer;
        public int maxColors = 8;
        public Color[] easyColors =
        {
            Color.Black,
            Color.Brown,
            Color.Blue,
            Color.Green
        };
        public Random rnd = new Random();
        public NonogramGrid grid;
        public NonogramRenderer renderer;
        public UndoRedoManager undoredoManager;
        public GameTimerManager gameTimerManager;
        public Form1()
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
            chkShowX.Location = new Point(200, picPreview.Bottom + 20);
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
            lblWrongClicks.Text = $"Helytelen kattintások: {wrongClicks}";
            lblWrongClicks.Location = new Point(20, 650); // tetszőleges pozíció
            lblWrongClicks.AutoSize = true;
            this.Controls.Add(lblWrongClicks);

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
            btnGenerateRandom.Visible = false;
            grid.ClearGrid();
            gameTimerManager.ResetWrongClicks();
            undoredoManager.ClearHistory();
            picPreview.Image = null;
            wrongClicks = 0;

            // A difficulty szerint generálunk
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
                    }
                    break;
            }

            renderer.ToggleXMarks(chkShowX.Checked);
            renderer.UpdatePreview();
            gameTimerManager.StartTimer();
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

            // Ha az adott cella X-jelölt, ne lehessen kattintani
            if (userXMark[rowIndex, colIndex])
            {
                wrongClicks++;
                lblWrongClicks.Text = $"Helytelen kattintások: {wrongClicks}";

                // GridCell_Click-ben a wrongClicks ellenőrzése
                if (wrongClicks >= 5)
                {
                    MessageBox.Show(
                        "Túl sok helytelen kattintás! Újraindítjuk a játékot.",
                        "Hiba",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );

                    wrongClicks = 0;
                    grid.ClearGrid();

                    // Difficulty szerint generálunk újra
                    switch (cmbDifficulty.SelectedIndex)
                    {
                        case 0: // Könnyű
                            grid.GenerateRandomNonogram(20, 150, 10, 10);
                            break;

                        case 1: // Közepes
                            grid.GenerateMediumNonogram();
                            break;

                        case 2: // Nehéz
                                // Nehéz mód esetén betölthetünk egy véletlen képet, vagy ugyanazt a mechanizmust mint BtnGenerateRandom
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
                            }
                            break;
                    }

                    gameTimerManager.StartTimer();
                    gameTimerManager.ResetWrongClicks();
                    picPreview.Image = null;
                }

                return; // kilépünk, nem engedjük a további feldolgozást
            }

            // Mentés a visszavonáshoz
            undoredoManager.SaveState();

            // Cellamódosítás
            grid.HandleGridClick(btn, selectedColor);

            // Preview frissítés
            renderer.UpdatePreview();
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
            renderer.CheckSolution();
        }

        private void ChkShowX_CheckedChanged(object sender, EventArgs e)
        {
            renderer.ToggleXMarks(((CheckBox)sender).Checked);
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
    }
}