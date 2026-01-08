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
        private Button[,] gridButtons;
        private Button btnLoadImage;
        private Button btnSolve, btnHint, btnCheck, btnSave, btnLoad;
        private Button btnGenerateRandom;
        private int[][] rowClues;
        private int[][] colClues;
        private int row = 10;
        private int col = 10;
        private int[,] userColor;
        private int userCellSize = 50;
        private int clueSize = 50;
        private Queue<Point> solutionQueue;
        private Timer solveTimer;
        private PictureBox picPreview;
        private PictureBox picSolutionPreview;
        private int previewSize = 150;
        private Color[,] solutionColorRGB; // a kép eredeti színe
        private int[,] solutionBW;          // fekete-fehér logikai mátrix (clues és ellenőrzéshez)
        private Color[,] userColorRGB;      // felhasználó által kiválasztott szín
        private bool isColor;
        private bool[,] userXMark;
        private CheckBox chkGrayscale;
        private CheckBox chkShowX;
        private CheckBox chkRandom;
        private Color[][] rowClueColors;
        private Color[][] colClueColors;
        private int colorSimilarityThreshold = 40; // hangolható: minél nagyobb, annál toleránsabb a "azonos" színekre
        private bool[,] hintActive;
        private bool hintShown = false;
        private int fixedGridTop;
        private FlowLayoutPanel colorPalette;
        private Color selectedColor = Color.Black; // alapértelmezett szín
        int maxColors = 8;
        private Button btnUndo, btnRedo;
        private Stack<Color[,]> undoStack = new Stack<Color[,]>();
        private Stack<Color[,]> redoStack = new Stack<Color[,]>();
        Image img;
        private int wrongClicks = 0;
        private Label lblWrongClicks;
        private ComboBox cmbDifficulty;
        private ComboBox cmbMode;
        public Form1()
        {
            InitializeComponent();
            InitializeCustomComponents();
            InitializeGridPosition();
            this.Size = new Size(1000, 1300);
            this.AutoScroll = true;
            btnLoadImage.Visible = false;
            cmbDifficulty.SelectionChangeCommitted += CmbDifficultyOrMode_Changed;
            cmbMode.SelectionChangeCommitted += CmbDifficultyOrMode_Changed;
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
            int btnLeft = 20;
            int btnTop = picPreview.Bottom + 20 + (userCellSize * col) + clueSize; // ideiglenes, majd frissítjük a grid után

            // Kép betöltő gomb
            btnLoadImage = new Button();
            btnLoadImage.Text = "Kép betöltése";
            btnLoadImage.Location = new Point(850, 700);
            btnLoadImage.Click += BtnLoadImage_Click;
            this.Controls.Add(btnLoadImage);

            // Solve gomb
            btnSolve = new Button();
            btnSolve.Text = "Megoldás";
            btnSolve.Location = new Point(130, 700);
            btnSolve.Click += BtnSolve_Click;
            this.Controls.Add(btnSolve);

            // Hint gomb
            btnHint = new Button();
            btnHint.Text = "Segítség";
            btnHint.Location = new Point(260, 700);
            btnHint.Click += BtnHint_Click;
            this.Controls.Add(btnHint);

            // Check gomb
            btnCheck = new Button();
            btnCheck.Text = "Ellenőrzés";
            btnCheck.Location = new Point(390, 700);
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
            chkShowX.Text = "X-ek megjelenítése";
            chkShowX.Location = new Point(200, picPreview.Bottom + 20);
            chkShowX.AutoSize = true;
            chkShowX.Checked = false; // alapból ki van kapcsolva
            chkShowX.CheckedChanged += ChkShowX_CheckedChanged;
            this.Controls.Add(chkShowX);

            // Új CheckBox hozzáadása InitializeCustomComponents-ben
            chkRandom = new CheckBox();
            chkRandom.Text = "Véletlenszerű Nonogram";
            chkRandom.Location = new Point(400, picPreview.Bottom + 20);
            chkRandom.AutoSize = true;
            this.Controls.Add(chkRandom);

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
            btnGenerateRandom.Text = "Véletlen Nonogram generálása";
            btnGenerateRandom.Location = new Point(20, btnCheck.Top);
            btnGenerateRandom.Click += BtnGenerateRandom_Click;
            this.Controls.Add(btnGenerateRandom);

            // --- Nehézségi szint választó ---
            cmbDifficulty = new ComboBox();
            cmbDifficulty.Items.AddRange(new string[] { "Könnyű", "Közepes", "Nehéz" });
            cmbDifficulty.SelectedIndex = 0; // alapértelmezett: Könnyű
            cmbDifficulty.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbDifficulty.Location = new Point(20, chkRandom.Bottom + 20);
            cmbDifficulty.Width = 150;
            this.Controls.Add(cmbDifficulty);

            // --- Játék mód választó (fekete-fehér / színes) ---
            cmbMode = new ComboBox();
            cmbMode.Items.AddRange(new string[] { "Fekete-fehér", "Színes" });
            cmbMode.SelectedIndex = 0; // alapértelmezett: fekete-fehér
            cmbMode.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbMode.Location = new Point(cmbDifficulty.Right + 20, chkRandom.Bottom + 20);
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
        }

        List<Bitmap> GetResourceImages()
        {
            return new List<Bitmap>()
            {
                Properties.Resources.pelda1,
                Properties.Resources.pelda2,
                Properties.Resources.pelda3,
                Properties.Resources.pelda4
            };
        }

        Bitmap PickRandomResourceImage()
        {
            List<Bitmap> images = GetResourceImages();
            if (images.Count == 0) return null;
            Random rnd = new Random();
            return images[rnd.Next(images.Count)];
        }

        private void BtnLoadImage_Click(object sender, EventArgs e)
        {
            int gridLeft = 20;
            int gridTop = Math.Max(chkGrayscale.Bottom, chkShowX.Bottom) + 20;
            InitializeGridPosition();

            Bitmap img = null;

            if (chkRandom.Checked)
            {
                img = PickRandomResourceImage();
                if (img == null)
                {
                    MessageBox.Show("Nincsenek képek a Resources-ban!", "Hiba",
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            else
            {
                using (OpenFileDialog openFileDialog = new OpenFileDialog())
                {
                    openFileDialog.Title = "Válassz egy képet";
                    openFileDialog.Filter = "Képfájlok|*.jpg;*.jpeg;*.png;*.bmp;*.gif";

                    if (openFileDialog.ShowDialog() != DialogResult.OK) return;

                    try
                    {
                        img = (Bitmap)Image.FromFile(openFileDialog.FileName);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Hiba a kép betöltése közben: " + ex.Message,
                                        "Hiba", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
            }

            // Fekete-fehér mód
            if (chkGrayscale.Checked)
                img = ConvertToBlackAndWhite(img);

            // Megjelenítés a PictureBox-ban
            picSolutionPreview.Image = img;
            picSolutionPreview.SizeMode = PictureBoxSizeMode.Zoom; // hogy jól illeszkedjen

            // Nonogram generálása
            GenerateNonogramFromImage(img, gridLeft, gridTop);

            // Színpaletta feltöltése
            GenerateColorPalette(img);
        }

        private void GenerateNonogramFromImage(Image img, int gridLeft, int gridTop)
        {
            int gridWidth = 25;
            int gridHeight = 25;
            Bitmap bmp;

            if (!isColor)
            {
                bmp = new Bitmap(gridWidth, gridHeight, PixelFormat.Format24bppRgb);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                    g.DrawImage(img, new Rectangle(0, 0, gridWidth, gridHeight));
                }
            }
            else
            {
                bmp = new Bitmap(gridWidth, gridHeight);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                    g.DrawImage(img, new Rectangle(0, 0, gridWidth, gridHeight));
                }
            }

            row = gridHeight;
            col = gridWidth;

            solutionColorRGB = new Color[row, col];
            solutionBW = new int[row, col];
            userXMark = new bool[row, col];
            isColor = false;

            bool[,] fillableCells = new bool[row, col];

            // HELYETTESÍTI A GetPixel-T
            Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            BitmapData bmpData = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            int bytes = Math.Abs(bmpData.Stride) * bmpData.Height;
            byte[] rgbValues = new byte[bytes];
            Marshal.Copy(bmpData.Scan0, rgbValues, 0, bytes);
            int bytesPerPixel = 3;

            // Most a pixeladatokat a memóriából olvassuk
            for (int i = 0; i < row; i++)
            {
                for (int j = 0; j < col; j++)
                {
                    int index = i * bmpData.Stride + j * bytesPerPixel;
                    byte b = rgbValues[index];
                    byte g = rgbValues[index + 1];
                    byte r = rgbValues[index + 2];
                    Color pixel = Color.FromArgb(r, g, b);

                    solutionColorRGB[i, j] = pixel;

                    if (!isColor)
                    {
                        double luminance = 0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B;
                        int lumInt = (int)luminance;
                        solutionBW[i, j] = (lumInt < 128) ? 1 : 0;

                        // Színellenőrzés
                        int maxDiff = Math.Max(Math.Abs(pixel.R - pixel.G),
                            Math.Max(Math.Abs(pixel.G - pixel.B), Math.Abs(pixel.R - pixel.B)));
                        if (maxDiff > 10) isColor = true;

                        fillableCells[i, j] = (lumInt < 128);
                    }
                    else
                    {
                        int gray = (pixel.R + pixel.G + pixel.B) / 3;
                        solutionBW[i, j] = (gray < 128) ? 1 : 0;

                        if (!isColor && (pixel.R != pixel.G || pixel.G != pixel.B))
                            isColor = true;

                        fillableCells[i, j] = isColor ? (pixel.R + pixel.G + pixel.B < 700)
                                                      : (solutionBW[i, j] == 1);
                    }
                }
            }

            bmp.UnlockBits(bmpData);

            // Szín-specifikus clue-ok számítása (sorok)
            rowClues = new int[row][];
            rowClueColors = new Color[row][];
            for (int i = 0; i < row; i++)
            {
                List<int> clues = new List<int>();
                List<Color> clueColors = new List<Color>();

                int j = 0;
                while (j < col)
                {
                    if (!fillableCells[i, j]) { j++; continue; }

                    int sumR = 0, sumG = 0, sumB = 0, count = 0;
                    Color seed = solutionColorRGB[i, j];
                    int k = j;
                    while (k < col && fillableCells[i, k])
                    {
                        Color c = solutionColorRGB[i, k];

                        if (isColor && count > 0 && !AreColorsSimilar(seed, c, colorSimilarityThreshold))
                            break; // új futam (másik szín)

                        // hozzáadjuk a pixelt a jelenlegi futamhoz
                        sumR += c.R; sumG += c.G; sumB += c.B; count++;

                        if (isColor && count == 1)
                            seed = c; // első pixel színe a seed

                        k++;
                    }

                    if (count > 0)
                    {
                        clues.Add(count);
                        Color avg = Color.FromArgb(sumR / count, sumG / count, sumB / count);
                        clueColors.Add(isColor ? avg : Color.Black);
                    }

                    j = k;
                }

                if (clues.Count == 0)
                {
                    rowClues[i] = new int[] { 0 };
                    rowClueColors[i] = new Color[] { isColor ? Color.White : Color.Black };
                }
                else
                {
                    rowClues[i] = clues.ToArray();
                    rowClueColors[i] = clueColors.ToArray();
                }
            }

            // Szín-specifikus clue-ok számítása (oszlopok)
            colClues = new int[col][];
            colClueColors = new Color[col][];
            for (int j = 0; j < col; j++)
            {
                List<int> clues = new List<int>();
                List<Color> clueColors = new List<Color>();

                int i = 0;
                while (i < row)
                {
                    if (!fillableCells[i, j]) { i++; continue; }

                    int sumR = 0, sumG = 0, sumB = 0, count = 0;
                    Color seed = solutionColorRGB[i, j];
                    int k = i;
                    while (k < row && fillableCells[k, j])
                    {
                        Color c = solutionColorRGB[k, j];

                        if (isColor && count > 0 && !AreColorsSimilar(seed, c, colorSimilarityThreshold))
                            break;

                        sumR += c.R; sumG += c.G; sumB += c.B; count++;

                        if (isColor && count == 1)
                            seed = c;

                        k++;
                    }

                    if (count > 0)
                    {
                        clues.Add(count);
                        Color avg = Color.FromArgb(sumR / count, sumG / count, sumB / count);
                        clueColors.Add(isColor ? avg : Color.Black);
                    }

                    i = k;
                }

                if (clues.Count == 0)
                {
                    colClues[j] = new int[] { 0 };
                    colClueColors[j] = new Color[] { isColor ? Color.White : Color.Black };
                }
                else
                {
                    colClues[j] = clues.ToArray();
                    colClueColors[j] = clueColors.ToArray();
                }
            }

            // X-ek beállítása csak a nem-kitöltendő cellákra
            for (int i = 0; i < row; i++)
                for (int j = 0; j < col; j++)
                    userXMark[i, j] = !fillableCells[i, j];

            CreateGridUI(gridLeft, gridTop);
            if (isColor)
                GenerateColorPalette(bmp); // csak színes képnél
        }

        private void BtnGenerateRandom_Click(object sender, EventArgs e)
        {
            int gridLeft = 20;
            int gridTop = Math.Max(chkGrayscale.Bottom, chkShowX.Bottom) + 20;
            picPreview.Image = null;
            wrongClicks = 0;
            // Véletlen Nonogram létrehozása
            GenerateRandomNonogram(30, 50, 10, 10);

            // Kép generálása előnézethez
            int cellSize = 20; // előnézethez kicsi cella
            int width = 10 * cellSize;
            int height = 10 * cellSize;
            img = new Bitmap(width, height);

            using (Graphics g = Graphics.FromImage(img))
            {
                g.Clear(Color.White);

                for (int i = 0; i < 10; i++)
                {
                    for (int j = 0; j < 10; j++)
                    {
                        Color c = solutionColorRGB[i, j]; // a generált Nonogram színei
                        using (Brush b = new SolidBrush(c))
                        {
                            g.FillRectangle(b, j * cellSize, i * cellSize, cellSize, cellSize);
                        }
                    }
                }
            }

            picSolutionPreview.Image = img;
            picSolutionPreview.SizeMode = PictureBoxSizeMode.Zoom;
        }

        private void GenerateRandomNonogram(int gridLeft, int gridTop, int numRow = 10, int numCol = 10)
        {
            row = numRow;
            col = numCol;
            Random rnd = new Random();

            solutionBW = new int[row, col];
            solutionColorRGB = new Color[row, col];
            userXMark = new bool[row, col];

            string mode = cmbMode.SelectedItem.ToString();
            isColor = (mode == "Színes");

            for (int i = 0; i < row; i++)
            {
                for (int j = 0; j < col; j++)
                {
                    bool filled = rnd.NextDouble() < 0.5; // 50% kitöltött
                    solutionBW[i, j] = filled ? 1 : 0;

                    if (filled)
                    {
                        if (isColor)
                            solutionColorRGB[i, j] = Color.FromArgb(rnd.Next(50, 256), rnd.Next(50, 256), rnd.Next(50, 256));
                        else
                            solutionColorRGB[i, j] = Color.Black;
                    }
                    else
                    {
                        solutionColorRGB[i, j] = Color.White;
                    }

                    userXMark[i, j] = !filled;
                }
            }

            // --- Sor clue-ok ---
            rowClues = new int[row][];
            rowClueColors = new Color[row][];
            for (int i = 0; i < row; i++)
            {
                List<int> clues = new List<int>();
                List<Color> clueColors = new List<Color>();

                int j = 0;
                while (j < col)
                {
                    if (solutionBW[i, j] == 0) { j++; continue; }

                    int sumR = 0, sumG = 0, sumB = 0, count = 0;
                    int k = j;
                    while (k < col && solutionBW[i, k] == 1)
                    {
                        Color c = solutionColorRGB[i, k];
                        if (isColor)
                        {
                            sumR += c.R;
                            sumG += c.G;
                            sumB += c.B;
                        }
                        count++;
                        k++;
                    }

                    if (count > 0)
                    {
                        clues.Add(count);
                        clueColors.Add(isColor ? Color.FromArgb(sumR / count, sumG / count, sumB / count) : Color.Black);
                    }
                    j = k;
                }

                if (clues.Count == 0) { clues.Add(0); clueColors.Add(isColor ? Color.White : Color.Black); }

                rowClues[i] = clues.ToArray();
                rowClueColors[i] = clueColors.ToArray();
            }

            // --- Oszlop clue-ok ---
            colClues = new int[col][];
            colClueColors = new Color[col][];
            for (int j = 0; j < col; j++)
            {
                List<int> clues = new List<int>();
                List<Color> clueColors = new List<Color>();

                int i = 0;
                while (i < row)
                {
                    if (solutionBW[i, j] == 0) { i++; continue; }

                    int sumR = 0, sumG = 0, sumB = 0, count = 0;
                    int k = i;
                    while (k < row && solutionBW[k, j] == 1)
                    {
                        Color c = solutionColorRGB[k, j];
                        if (isColor)
                        {
                            sumR += c.R;
                            sumG += c.G;
                            sumB += c.B;
                        }
                        count++;
                        k++;
                    }

                    if (count > 0)
                    {
                        clues.Add(count);
                        clueColors.Add(isColor ? Color.FromArgb(sumR / count, sumG / count, sumB / count) : Color.Black);
                    }
                    i = k;
                }

                if (clues.Count == 0) { clues.Add(0); clueColors.Add(isColor ? Color.White : Color.Black); }

                colClues[j] = clues.ToArray();
                colClueColors[j] = clueColors.ToArray();
            }

            CreateGridUI(gridLeft, gridTop);
            if (isColor)
            {
                // Színpaletta generálása színes Nonogramhoz
                HashSet<int> colorsSet = new HashSet<int>();
                for (int i = 0; i < row; i++)
                {
                    for (int j = 0; j < col; j++)
                    {
                        Color c = solutionColorRGB[i, j];
                        if (c != Color.White) // csak a kitöltött színek
                            colorsSet.Add(c.ToArgb());
                    }
                }

                colorPalette.Controls.Clear();
                colorPalette.Visible = true;
                colorPalette.AutoScroll = false; // nincs görgetés
                colorPalette.WrapContents = true; // több sorban törik
                colorPalette.FlowDirection = FlowDirection.LeftToRight;

                int btnSize = 30; // gombméret
                int margin = 2;   // gomb közti távolság
                int maxCols = 10; // max gomb egy sorban

                int totalColors = colorsSet.Count;
                int rowsNeeded = (int)Math.Ceiling(totalColors / (double)maxCols);

                colorPalette.Size = new Size(maxCols * (btnSize + margin * 2), rowsNeeded * (btnSize + margin * 2));

                foreach (int argb in colorsSet)
                {
                    Color color = Color.FromArgb(argb);
                    Button colorBtn = new Button();
                    colorBtn.BackColor = color;
                    colorBtn.Size = new Size(btnSize, btnSize);
                    colorBtn.Margin = new Padding(margin);
                    colorBtn.FlatStyle = FlatStyle.Flat;
                    colorBtn.FlatAppearance.BorderSize = 1;
                    colorBtn.FlatAppearance.BorderColor = Color.Gray;

                    colorBtn.Click += (s, e) =>
                    {
                        selectedColor = color;
                        foreach (Button b in colorPalette.Controls)
                            b.FlatAppearance.BorderColor = Color.Gray;
                        colorBtn.FlatAppearance.BorderColor = Color.Red;
                    };

                    colorPalette.Controls.Add(colorBtn);
                }
            }
            else
            {
                colorPalette.Visible = false; // fekete-fehérnél elrejtjük
            }
        }

        private void GenerateColorPalette(Bitmap bmp)
        {
            if (!isColor)
            {
                colorPalette.Visible = false;
                return;
            }

            colorPalette.Controls.Clear();
            colorPalette.Visible = true;

            HashSet<Color> uniqueColors = new HashSet<Color>();

            // Gyors beolvasás
            Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            BitmapData bmpData = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            int bytes = Math.Abs(bmpData.Stride) * bmp.Height;
            byte[] rgbValues = new byte[bytes];
            Marshal.Copy(bmpData.Scan0, rgbValues, 0, bytes);

            int bpp = 3;

            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    int idx = y * bmpData.Stride + x * bpp;
                    byte b = rgbValues[idx];
                    byte g = rgbValues[idx + 1];
                    byte r = rgbValues[idx + 2];

                    Color c = Color.FromArgb(r, g, b);

                    if (c != Color.White)   // fehéret kihagyjuk
                        uniqueColors.Add(c);
                }
            }

            bmp.UnlockBits(bmpData);

            // Itt lehetne K-means-re redukálni a színeket, pl. maxColors számúra
            // List<Color> clusteredColors = KMeansColors(uniqueColors.ToList(), maxColors);
            // uniqueColors = new HashSet<Color>(clusteredColors);

            // Gombok létrehozása minden színhez
            foreach (Color col in uniqueColors)
            {
                Button btn = new Button();
                btn.BackColor = col;
                btn.Size = new Size(30, 30);
                btn.Margin = new Padding(2);

                btn.FlatStyle = FlatStyle.Flat;
                btn.FlatAppearance.BorderSize = 1;
                btn.FlatAppearance.BorderColor = Color.Gray;

                btn.Click += (s, e) =>
                {
                    selectedColor = col;

                    foreach (Button b in colorPalette.Controls)
                        b.FlatAppearance.BorderColor = Color.Gray;

                    btn.FlatAppearance.BorderColor = Color.Red;
                };

                colorPalette.Controls.Add(btn);
            }

            // Dinamikus magasság
            int buttonsPerRow = Math.Max(1, colorPalette.Width / (30 + 4));
            int rows = (int)Math.Ceiling((double)uniqueColors.Count / buttonsPerRow);
            colorPalette.Height = rows * (30 + 4);
        }

        // Egyszerű K-means színekre
        private List<Color> KMeansColors(List<Color> colors, int k)
        {
            Random rnd = new Random();
            List<Color> centers = new List<Color>();

            // kezdeti centroidok (véletlenszerű)
            for (int i = 0; i < k; i++)
                centers.Add(colors[rnd.Next(colors.Count)]);

            bool changed = true;
            int iterations = 0;
            while (changed && iterations < 10) // max 10 iteráció
            {
                iterations++;
                changed = false;

                List<List<Color>> clusters = new List<List<Color>>();
                for (int i = 0; i < k; i++) clusters.Add(new List<Color>());

                // hozzárendelés a legközelebbi centroidhoz
                foreach (Color c in colors)
                {
                    int bestIdx = 0;
                    double bestDist = ColorDistance(c, centers[0]);
                    for (int i = 1; i < centers.Count; i++)
                    {
                        double d = ColorDistance(c, centers[i]);
                        if (d < bestDist) { bestDist = d; bestIdx = i; }
                    }
                    clusters[bestIdx].Add(c);
                }

                // centroidok frissítése
                for (int i = 0; i < k; i++)
                {
                    if (clusters[i].Count == 0) continue;
                    int r = (int)clusters[i].Average(c => c.R);
                    int g = (int)clusters[i].Average(c => c.G);
                    int b = (int)clusters[i].Average(c => c.B);
                    Color newCenter = Color.FromArgb(r, g, b);
                    if (newCenter != centers[i]) { centers[i] = newCenter; changed = true; }
                }
            }

            return centers;
        }

        private double ColorDistance(Color c1, Color c2)
        {
            int dr = c1.R - c2.R;
            int dg = c1.G - c2.G;
            int db = c1.B - c2.B;
            return Math.Sqrt(dr * dr + dg * dg + db * db);
        }

        private void AdjustGridSize()
        {
            int maxGridWidth = this.ClientSize.Width - 40;   // oldalsó margó
            int maxGridHeight = this.ClientSize.Height - fixedGridTop - 150; // felső + alsó margó

            // Maximum clue-ok helye
            int maxRowClues = MaxClueLength(rowClues);
            int maxColClues = MaxClueLength(colClues);

            // Cellaméret kiszámolása, hogy a teljes grid beleférjen
            int cellWidth = (maxGridWidth - maxRowClues * clueSize) / col;
            int cellHeight = (maxGridHeight - maxColClues * clueSize) / row;

            // Minimum/maximum korlát
            userCellSize = Math.Min(cellWidth, cellHeight);
            userCellSize = Math.Min(userCellSize, 50);  // max 50px
            userCellSize = Math.Max(userCellSize, 15);  // min 15px, hogy nagy gridnél ne legyen túl kicsi
        }

        private void AdjustCheckboxPositions()
        {
            int margin = 10;
            int checkTop = picPreview.Bottom + margin;

            chkGrayscale.Location = new Point(20, checkTop);
            chkShowX.Location = new Point(chkGrayscale.Right + 20, checkTop);
            chkRandom.Location = new Point(chkShowX.Right + 20, checkTop);
        }
        private void CreateGridUI(int gridLeft, int gridTop)
        {
            AdjustGridSize();

            // Előző grid és clue törlése
            foreach (Control c in this.Controls.Find("gridCell", true))
                this.Controls.Remove(c);
            foreach (Control c in this.Controls.Find("clueLabel", true))
                this.Controls.Remove(c);

            gridButtons = new Button[row, col];
            userColor = new int[row, col];
            userColorRGB = new Color[row, col];

            int cellSize = userCellSize;
            int maxColClues = MaxClueLength(colClues);
            int maxRowClues = MaxClueLength(rowClues);
            int horizontalOffset = 350;
            int startX = gridLeft + cellSize * maxRowClues + horizontalOffset;
            int topMargin = fixedGridTop; // checkboxok alja
            int maxColClueHeight = MaxClueLength(colClues) * cellSize;

            // Ha a clue-ok elférnek, startY = topMargin
            // Ha nem férnek el, startY = topMargin + a szükséges többlet
            int startY = Math.Max(topMargin, topMargin + maxColClueHeight);

            // Oszlop clue-k
            for (int j = 0; j < col; j++)
            {
                int[] clues = colClues[j];
                for (int i = 0; i < clues.Length; i++)
                {
                    Label lbl = new Label();
                    lbl.Name = "clueLabel";
                    lbl.Size = new Size(cellSize, cellSize);
                    lbl.Location = new Point(startX + j * cellSize, startY - (clues.Length - i) * cellSize);
                    lbl.TextAlign = ContentAlignment.MiddleCenter;
                    lbl.BorderStyle = BorderStyle.FixedSingle;
                    lbl.Text = clues[i].ToString();
                    if (isColor)
                    {
                        Color clueColor = colClueColors[j][i];
                        lbl.BackColor = clueColor;
                        int brightness = (clueColor.R + clueColor.G + clueColor.B) / 3;
                        lbl.ForeColor = (brightness < 128) ? Color.White : Color.Black;
                    }
                    else
                    {
                        lbl.BackColor = Color.Black;
                        lbl.ForeColor = Color.White;
                    }
                    this.Controls.Add(lbl);
                }
            }

            // Sor clue-k
            for (int i = 0; i < row; i++)
            {
                int[] clues = rowClues[i];
                for (int j = 0; j < clues.Length; j++)
                {
                    Label lbl = new Label();
                    lbl.Name = "clueLabel";
                    lbl.Size = new Size(cellSize, cellSize);
                    lbl.Location = new Point(startX - (clues.Length - j) * cellSize, startY + i * cellSize);
                    lbl.TextAlign = ContentAlignment.MiddleCenter;
                    lbl.BorderStyle = BorderStyle.FixedSingle;
                    lbl.Text = clues[j].ToString();
                    if (isColor)
                    {
                        Color clueColor = rowClueColors[i][j];
                        lbl.BackColor = clueColor;
                        int brightness = (clueColor.R + clueColor.G + clueColor.B) / 3;
                        lbl.ForeColor = (brightness < 128) ? Color.White : Color.Black;
                    }
                    else
                    {
                        lbl.BackColor = Color.Black;
                        lbl.ForeColor = Color.White;
                    }
                    this.Controls.Add(lbl);
                }
            }

            // Grid cellák
            /*for (int i = 0; i < row; i++)
            {
                for (int j = 0; j < col; j++)
                {
                    Button btn = new Button();
                    btn.Name = "gridCell";
                    btn.Size = new Size(cellSize, cellSize);
                    btn.Location = new Point(startX + j * cellSize, startY + i * cellSize);
                    btn.BackColor = Color.White;
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.FlatAppearance.BorderColor = Color.Gray;
                    btn.FlatAppearance.BorderSize = 1;
                    btn.Tag = new Point(i, j);
                    btn.Click += GridCell_Click;
                    this.Controls.Add(btn);

                    gridButtons[i, j] = btn;
                    userColor[i, j] = 0;
                    userColorRGB[i, j] = Color.White;

                    if (userXMark[i, j])
                    {
                        btn.Text = chkShowX.Checked ? "X" : "";
                        btn.Font = new Font("Arial", 16, FontStyle.Bold);
                        btn.ForeColor = Color.Gray;
                        btn.BackColor = Color.White;
                        btn.Enabled = false;
                    }
                }
            }*/

            for (int i = 0; i < row; i++)
            {
                for (int j = 0; j < col; j++)
                {
                    Button btn = new Button();
                    btn.Name = "gridCell";
                    btn.Size = new Size(cellSize, cellSize);
                    btn.Location = new Point(startX + j * cellSize, startY + i * cellSize);
                    btn.BackColor = Color.White;
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.FlatAppearance.BorderColor = Color.Gray;
                    btn.FlatAppearance.BorderSize = 1;
                    btn.Tag = new Point(i, j);
                    btn.Click += GridCell_Click;

                    // A Paint eseményben csak az X-et rajzoljuk
                    btn.Paint += (s, e) =>
                    {
                        Point p = (Point)((Button)s).Tag;
                        int i2 = p.X;
                        int j2 = p.Y;

                        // --- Rajzolás: X jel, ha van ---
                        /*if (userXMark[i2, j2] && chkShowX.Checked)
                        {
                            using (Pen pen = new Pen(Color.Gray, 2))
                            {
                                int margin = 2;
                                e.Graphics.DrawLine(pen, margin, margin, btn.Width - margin, btn.Height - margin);
                                e.Graphics.DrawLine(pen, btn.Width - margin, margin, margin, btn.Height - margin);
                            }
                        }*/

                        // --- Vastagabb vonalak 5x5-ös rácsokhoz és külső kerethez ---
                        using (Pen thickPen = new Pen(Color.Black, 2))
                        {
                            // Felső vonal minden 5. sor előtt, és a legfelső sorban
                            if (i2 % 5 == 0)
                                e.Graphics.DrawLine(thickPen, 0, 0, btn.Width, 0);

                            // Bal oldali vonal minden 5. oszlop előtt, és a legbaloldalibb oszlopban
                            if (j2 % 5 == 0)
                                e.Graphics.DrawLine(thickPen, 0, 0, 0, btn.Height);

                            // Jobb szél — utolsó oszlop esetén
                            if (j2 == col - 1)
                                e.Graphics.DrawLine(thickPen, btn.Width - 1, 0, btn.Width - 1, btn.Height);

                            // Alsó szél — utolsó sor esetén
                            if (i2 == row - 1)
                                e.Graphics.DrawLine(thickPen, 0, btn.Height - 1, btn.Width, btn.Height - 1);
                        }
                    };

                    this.Controls.Add(btn);
                    gridButtons[i, j] = btn;
                    userColor[i, j] = 0;
                    userColorRGB[i, j] = Color.White;
                    btn.Enabled = true;
                }
            }

            // Checkboxok frissítése
            AdjustCheckboxPositions();

            // --- Gombok maradjanak ugyanott, mint eddig ---
            int gridBottom = startY + row * cellSize;
            int buttonTop = Math.Max(gridBottom + 20, btnLoadImage.Top); // ha már le vannak rakva, ne tolódjanak
            btnLoadImage.Top = btnSolve.Top = btnHint.Top = btnCheck.Top = btnSave.Top = btnLoad.Top = btnRedo.Top = btnUndo.Top = btnGenerateRandom.Top = buttonTop;

            // AutoScrollMinSize: grid vagy gombok alja + margin
            int buttonsBottom = btnLoadImage.Bottom;
            this.AutoScrollMinSize = new Size(
                this.ClientSize.Width,
                Math.Max(gridBottom, buttonsBottom) + 20
            );
            //undoStack.Clear();
            //redoStack.Clear();
            //SaveCurrentState();
        }

        private int MaxClueLength(int[][] clues)
        {
            int max = 0;
            foreach (int[] arr in clues)
                if (arr.Length > max) max = arr.Length;
            return max;
        }

        private void SetGridEnabled(bool enabled)
        {
            for (int i = 0; i < row; i++)
            {
                for (int j = 0; j < col; j++)
                {
                    if (!userXMark[i, j]) // csak a kitölthető cellák
                        gridButtons[i, j].Enabled = enabled;
                }
            }
        }

        // Cellára kattintás esemény
        private void GridCell_Click(object sender, EventArgs e)
        {
            Button btn = sender as Button;
            Point pos = (Point)btn.Tag;
            int x = pos.X;
            int y = pos.Y;

            // Ha X van ezen a cellán → hiba
            if (userXMark != null && userXMark[x, y])
            {
                wrongClicks++;
                lblWrongClicks.Text = $"Helytelen kattintások: {wrongClicks}";

                if (wrongClicks >= 5)
                {
                    MessageBox.Show("Túl sok hibás kattintás! Új véletlenszerű feladvány generálása.",
                        "Figyelem", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                    CmbDifficultyOrMode_Changed(null, EventArgs.Empty);
                    wrongClicks = 0;
                    lblWrongClicks.Text = $"Helytelen kattintások: 0";
                }

                return; // fontos, hogy ne essen tovább más logikába
            }

            // Ha hint aktív ezen a cellán
            if (hintShown && hintActive[x, y])
            {
                if (isColor)
                    userColorRGB[x, y] = solutionColorRGB[x, y];
                else
                {
                    userColor[x, y] = solutionBW[x, y];
                    userColorRGB[x, y] = (solutionBW[x, y] == 1) ? Color.Black : Color.White;
                }

                for (int i = 0; i < row; i++)
                {
                    for (int j = 0; j < col; j++)
                    {
                        if (hintActive[i, j])
                            gridButtons[i, j].BackColor = userColorRGB[i, j];
                    }
                }

                hintActive = null;
                hintShown = false;

                gridButtons[x, y].BackColor = userColorRGB[x, y];
                UpdatePreview();
                return;
            }

            // Ellenőrizzük, hogy ez a cella már helyes-e
            bool isCorrectSolution;
            if (isColor)
                isCorrectSolution = userColorRGB[x, y].ToArgb() == solutionColorRGB[x, y].ToArgb();
            else
                isCorrectSolution = userColor[x, y] == solutionBW[x, y];

            if (isCorrectSolution)
            {
                // Ha már helyes, kattintásra mutatjuk a helyes színt
                if (isColor)
                    btn.BackColor = solutionColorRGB[x, y];
                else
                    btn.BackColor = (solutionBW[x, y] == 1) ? Color.Black : Color.White;

                return; // ne számoljuk hibának, ne változtassunk mást
            } 

            // Normál kattintás: a felhasználó színválasztása
            SaveCurrentState();
            redoStack.Clear();

            if (isColor)
                userColorRGB[x, y] = selectedColor;
            else
            {
                if (userColor[x, y] == 0)
                {
                    userColor[x, y] = 1;
                    userColorRGB[x, y] = Color.Black;
                }
                else
                {
                    userColor[x, y] = 0;
                    userColorRGB[x, y] = Color.White;
                }
            }

            btn.BackColor = userColorRGB[x, y];
            UpdatePreview();
        }

        // Megoldás gomb esemény
        private void BtnSolve_Click(object sender, EventArgs e)
        {
            List<Point> wrongCells = new List<Point>();

            for (int i = 0; i < row; i++)
            {
                for (int j = 0; j < col; j++)
                {
                    if (userXMark[i, j]) continue;
                    bool wrong;

                    if (isColor)
                        wrong = userColorRGB[i, j].ToArgb() != solutionColorRGB[i, j].ToArgb();
                    else
                        wrong = userColor[i, j] != solutionBW[i, j];

                    if (wrong)
                        wrongCells.Add(new Point(i, j));
                }
            }

            if (wrongCells.Count == 0)
            {
                MessageBox.Show("Már minden cella a helyén van!", "Megoldás", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            solutionQueue = new Queue<Point>(wrongCells);

            SetGridEnabled(false);
            solveTimer = new Timer();
            solveTimer.Interval = 100;
            solveTimer.Tick += SolveTimer_Tick;
            solveTimer.Start();
        }

        private void SolveTimer_Tick(object sender, EventArgs e)
        {
            if (solutionQueue.Count == 0)
            {
                solveTimer.Stop();
                solveTimer.Dispose();
                SetGridEnabled(true);
                // X-ek eltüntetése a teljes gridből
                for (int i = 0; i < row; i++)
                {
                    for (int j = 0; j < col; j++)
                    {
                        if (userXMark[i, j])
                        {
                            gridButtons[i, j].Text = "";
                        }
                    }
                }
                MessageBox.Show("A nonogram teljesen kirakva!", "Megoldás kész", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Point p = solutionQueue.Dequeue();
            int x = p.X;
            int y = p.Y;

            if (isColor)
            {
                userColorRGB[x, y] = solutionColorRGB[x, y];
                gridButtons[x, y].BackColor = solutionColorRGB[x, y];
            }
            else
            {
                userColor[x, y] = solutionBW[x, y];
                gridButtons[x, y].BackColor = (solutionBW[x, y] == 1) ? Color.Black : Color.White;
                userColorRGB[x, y] = gridButtons[x, y].BackColor;
            }

            UpdatePreview();
        }

        // Segítség gomb esemény
        private void BtnHint_Click(object sender, EventArgs e)
        {
            // Hibás mezők gyűjtése
            List<Point> wrongCells = new List<Point>();

            for (int i = 0; i < row; i++)
            {
                for (int j = 0; j < col; j++)
                {
                    if (userXMark[i, j]) continue;

                    bool wrong = isColor
                        ? userColorRGB[i, j].ToArgb() != solutionColorRGB[i, j].ToArgb()
                        : userColor[i, j] != solutionBW[i, j];

                    if (wrong)
                        wrongCells.Add(new Point(i, j));
                }
            }

            if (wrongCells.Count == 0)
            {
                MessageBox.Show("Már minden a helyén van — nincs több segítség!", "Segítség");
                return;
            }

            // Véletlenszerűen kiválasztunk egy hibás mezőt
            Random rnd = new Random();
            Point hintCell = wrongCells[rnd.Next(wrongCells.Count)];
            int x = hintCell.X;
            int y = hintCell.Y;

            // Beállítjuk a helyes színt
            if (isColor)
                userColorRGB[x, y] = solutionColorRGB[x, y];
            else
            {
                userColor[x, y] = solutionBW[x, y];
                userColorRGB[x, y] = (solutionBW[x, y] == 1) ? Color.Black : Color.White;
            }

            gridButtons[x, y].BackColor = userColorRGB[x, y];

            // Hint mátrix frissítése csak erre a mezőre
            hintActive = new bool[row, col];
            hintActive[x, y] = true;
            hintShown = true;

            UpdatePreview();
        }

        // Ellenőrzés gomb esemény
        private void BtnCheck_Click(object sender, EventArgs e)
        {
            bool correct = true;

            for (int i = 0; i < row && correct; i++)
            {
                for (int j = 0; j < col; j++)
                {
                    if (isColor)
                    {
                        if (userColorRGB[i, j].ToArgb() != solutionColorRGB[i, j].ToArgb())
                        {
                            correct = false;
                            break;
                        }
                    }
                    else
                    {
                        if (userColor[i, j] != solutionBW[i, j])
                        {
                            correct = false;
                            break;
                        }
                    }
                }
            }

            if (correct)
                MessageBox.Show("Helyes megoldás!", "Ellenőrzés", MessageBoxButtons.OK, MessageBoxIcon.Information);
            else
                MessageBox.Show("Még nem teljesen jó.", "Ellenőrzés", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void UpdatePreview()
        {
            Bitmap bmp = new Bitmap(col, row, PixelFormat.Format24bppRgb);

            // Lockoljuk a bitmapot írásra
            Rectangle rect = new Rectangle(0, 0, col, row);
            BitmapData bmpData = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

            int bytesPerPixel = 3;
            int bytes = Math.Abs(bmpData.Stride) * row;
            byte[] rgbValues = new byte[bytes];

            // userColorRGB tömb beírása a memóriába
            for (int i = 0; i < row; i++)
            {
                for (int j = 0; j < col; j++)
                {
                    Color c = userColorRGB[i, j];
                    int index = i * bmpData.Stride + j * bytesPerPixel;
                    rgbValues[index] = c.B;
                    rgbValues[index + 1] = c.G;
                    rgbValues[index + 2] = c.R;
                }
            }

            // Visszamásolás a bitmapba
            Marshal.Copy(rgbValues, 0, bmpData.Scan0, bytes);
            bmp.UnlockBits(bmpData);

            // Skálázás (változatlan maradhat)
            Bitmap scaled = new Bitmap(previewSize, previewSize);
            using (Graphics g = Graphics.FromImage(scaled))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                g.DrawImage(bmp, new Rectangle(0, 0, previewSize, previewSize));
            }

            picPreview.Image = scaled;
        }

        private Bitmap ConvertToBlackAndWhite(Bitmap original, byte threshold = 200)
        {
            Bitmap bw = new Bitmap(original.Width, original.Height, PixelFormat.Format24bppRgb);

            Rectangle rect = new Rectangle(0, 0, original.Width, original.Height);
            BitmapData srcData = original.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            BitmapData dstData = bw.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

            int bytes = Math.Abs(srcData.Stride) * original.Height;
            byte[] srcBuffer = new byte[bytes];
            byte[] dstBuffer = new byte[bytes];

            Marshal.Copy(srcData.Scan0, srcBuffer, 0, bytes);

            int bytesPerPixel = 3;
            for (int y = 0; y < original.Height; y++)
            {
                for (int x = 0; x < original.Width; x++)
                {
                    int index = y * srcData.Stride + x * bytesPerPixel;
                    byte b = srcBuffer[index];
                    byte g = srcBuffer[index + 1];
                    byte r = srcBuffer[index + 2];

                    // Szürke érték számítása
                    int grayValue = (int)(0.299 * r + 0.587 * g + 0.114 * b);

                    // Küszöbölés fekete-fehérre
                    byte bwValue = (grayValue < threshold) ? (byte)0 : (byte)255;

                    dstBuffer[index] = bwValue;
                    dstBuffer[index + 1] = bwValue;
                    dstBuffer[index + 2] = bwValue;
                }
            }

            Marshal.Copy(dstBuffer, 0, dstData.Scan0, bytes);

            original.UnlockBits(srcData);
            bw.UnlockBits(dstData);

            return bw;
        }

        private void ChkShowX_CheckedChanged(object sender, EventArgs e)
        {
            CheckBox chk = sender as CheckBox;
            bool show = chk.Checked;

            // Ha még nincs grid, semmit nem csinálunk
            if (gridButtons == null) return;

            for (int i = 0; i < row; i++)
            {
                for (int j = 0; j < col; j++)
                {
                    if (userXMark[i, j])
                    {
                        gridButtons[i, j].Text = show ? "X" : "";
                    }
                }
            }
        }

        private bool AreColorsSimilar(Color a, Color b, int threshold)
        {
            int dr = a.R - b.R;
            int dg = a.G - b.G;
            int db = a.B - b.B;
            int dist2 = dr * dr + dg * dg + db * db;
            return dist2 <= threshold * threshold;
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (userColorRGB == null) return;

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Title = "Mentés képként";
                sfd.Filter = "PNG kép|*.png";
                sfd.FileName = "nonogram.png";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    // Létrehozunk egy teljes méretű bitmapet a jelenlegi gridből
                    int scale = 50; // pl. 50 px cellánként, tetszőlegesen állítható
                    Bitmap bmp = new Bitmap(col * scale, row * scale);
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        for (int i = 0; i < row; i++)
                        {
                            for (int j = 0; j < col; j++)
                            {
                                using (SolidBrush brush = new SolidBrush(userColorRGB[i, j]))
                                {
                                    g.FillRectangle(brush, i * scale, j * scale, scale, scale);
                                }
                            }
                        }
                    }

                    bmp.Save(sfd.FileName, System.Drawing.Imaging.ImageFormat.Png);
                    MessageBox.Show("Kép elmentve az eredeti méretben!", "Mentés", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void BtnLoadSaved_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Kép betöltése";
                ofd.Filter = "PNG kép|*.png";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        Bitmap loaded = new Bitmap(ofd.FileName);

                        int gridLeft = 20;
                        int gridTop = Math.Max(chkGrayscale.Bottom, chkShowX.Bottom) + 20;

                        // Új clue-ok és X-ek generálása a betöltött kép alapján
                        GenerateNonogramFromImage(loaded, gridLeft, gridTop);

                        // A grid gombjainak frissítése a betöltött képpel
                        for (int i = 0; i < row; i++)
                        {
                            for (int j = 0; j < col; j++)
                            {
                                gridButtons[i, j].BackColor = solutionColorRGB[i, j];
                                userColorRGB[i, j] = solutionColorRGB[i, j];

                                if (!isColor)
                                    userColor[i, j] = solutionBW[i, j];
                            }
                        }

                        UpdatePreview();

                        MessageBox.Show("Kép betöltve, grid és szám-piramidok frissítve!", "Betöltés", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Hiba a kép betöltése közben: " + ex.Message, "Hiba", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void InitializeGridPosition()
        {
            int margin = 10;
            fixedGridTop = Math.Max(chkGrayscale.Bottom, chkShowX.Bottom) + margin;
        }

        private void SaveCurrentState()
        {
            if (userColorRGB == null) return;

            Color[,] snapshot = new Color[row, col];
            for (int i = 0; i < row; i++)
                for (int j = 0; j < col; j++)
                    snapshot[i, j] = userColorRGB[i, j];

            undoStack.Push(snapshot);
        }

        private void RestoreState(Color[,] state)
        {
            for (int i = 0; i < row; i++)
            {
                for (int j = 0; j < col; j++)
                {
                    userColorRGB[i, j] = state[i, j];
                    gridButtons[i, j].BackColor = state[i, j];

                    if (!isColor)
                        userColor[i, j] = (state[i, j] == Color.Black) ? 1 : 0;
                }
            }

            UpdatePreview();
        }

        private void BtnUndo_Click(object sender, EventArgs e)
        {
            if (undoStack.Count > 0)
            {
                Color[,] current = CloneState(userColorRGB);
                redoStack.Push(current);

                Color[,] prev = undoStack.Pop();
                RestoreState(prev);
            }
            else
            {
                MessageBox.Show("Nincs korábbi állapot!", "Undo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void BtnRedo_Click(object sender, EventArgs e)
        {
            if (redoStack.Count > 0)
            {
                Color[,] current = CloneState(userColorRGB);
                undoStack.Push(current);

                Color[,] next = redoStack.Pop();
                RestoreState(next);
            }
            else
            {
                MessageBox.Show("Nincs későbbi állapot!", "Redo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private Color[,] CloneState(Color[,] source)
        {
            Color[,] clone = new Color[row, col];
            for (int i = 0; i < row; i++)
                for (int j = 0; j < col; j++)
                    clone[i, j] = source[i, j];
            return clone;
        }

        private void CmbDifficultyOrMode_Changed(object sender, EventArgs e)
        {
            ClearGrid(); // előző grid törlése
            if (cmbDifficulty.SelectedIndex == 0) // Könnyű
            {
                btnLoadImage.Visible = false;
                btnGenerateRandom.Visible = true;
                UpdateNonogram(); // Könnyűnél azonnal generálunk
                // Kép előnézet frissítése a véletlenszerű Nonogramhoz
                picSolutionPreview.Image = GeneratePreviewImage();
                picSolutionPreview.SizeMode = PictureBoxSizeMode.Zoom;
            }
            else if (cmbDifficulty.SelectedIndex == 1) // Közepes
            {
                btnLoadImage.Visible = false;
                btnGenerateRandom.Visible = false;

                GenerateMediumNonogram();
            }
            else if (cmbDifficulty.SelectedIndex == 2) // Nehéz
            {
                btnLoadImage.Visible = true;
                btnGenerateRandom.Visible = false;

                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Filter = "Képfájlok|*.png;*.jpg;*.jpeg;*.bmp";
                    ofd.Title = "Kép betöltése Nonogramhoz";

                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        img = Image.FromFile(ofd.FileName);

                        picSolutionPreview.Image = img;
                        picSolutionPreview.SizeMode = PictureBoxSizeMode.Zoom;

                        int gridLeft = 20;
                        int gridTop = Math.Max(chkGrayscale.Bottom, chkShowX.Bottom) + 20;

                        GenerateNonogramFromImage(img, gridLeft, gridTop);
                    }
                    else
                    {
                        cmbDifficulty.SelectedIndex = 0;
                        UpdateNonogram();
                    }
                }
            }
        }

        private void UpdateNonogram()
        {
            ClearGrid(); // előző grid törlése

            bool isColorMode = (cmbMode.SelectedItem.ToString() == "Színes");

            if (cmbDifficulty.SelectedIndex == 0) // Könnyű → véletlen Nonogram
            {
                GenerateRandomNonogram(20, 150, 10, 10);
            }
            else // Közepes/Nehéz → kép alapján
            {
                if (img != null)
                    GenerateNonogramFromImage(img, 20, 150);
                else
                    return; // biztonsági ellenőrzés
            }

            UpdatePreview();
        }

        private void ClearGrid()
        {
            // Grid gombok törlése
            if (gridButtons != null)
            {
                foreach (Button b in gridButtons)
                {
                    if (b != null && this.Controls.Contains(b))
                        this.Controls.Remove(b);
                }
            }

            // Clue Label-ek törlése
            foreach (Control c in this.Controls.Find("clueLabel", true))
            {
                this.Controls.Remove(c);
            }

            // Új tömbök létrehozása a következő generáláshoz
            gridButtons = new Button[row, col];
            // Előnézet kép törlése
            picSolutionPreview.Image = null;
        }

        private Bitmap GeneratePreviewImage()
        {
            int cellSize = 10; // előnézeti méret
            Bitmap bmp = new Bitmap(col * cellSize, row * cellSize);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                for (int i = 0; i < row; i++)
                {
                    for (int j = 0; j < col; j++)
                    {
                        Color c = (isColor) ? solutionColorRGB[i, j] :
                                              (solutionBW[i, j] == 1 ? Color.Black : Color.White);
                        using (SolidBrush brush = new SolidBrush(c))
                        {
                            g.FillRectangle(brush, j * cellSize, i * cellSize, cellSize, cellSize);
                        }
                    }
                }
            }
            return bmp;
        }

        private void GenerateMediumNonogram()
        {
            int gridLeft = 20;
            int gridTop = Math.Max(chkGrayscale.Bottom, chkShowX.Bottom) + 20;

            int w = 25;
            int h = 25;

            Bitmap shape = GenerateMediumShape(w, h);

            // --- ÚJ: pixelperfect nagyítás ---
            Bitmap large = ScaleBitmap(shape, 8); // 8× nagyítás (200×200 px körül)

            picSolutionPreview.Image = large;
            picSolutionPreview.SizeMode = PictureBoxSizeMode.Zoom;

            GenerateNonogramFromImage(shape, gridLeft, gridTop);
        }

        private Bitmap ScaleBitmap(Bitmap src, int scale)
        {
            Bitmap bmp = new Bitmap(src.Width * scale, src.Height * scale);

            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;

                g.DrawImage(src, new Rectangle(0, 0, bmp.Width, bmp.Height),
                                new Rectangle(0, 0, src.Width, src.Height),
                                GraphicsUnit.Pixel);
            }
            return bmp;
        }

        private Bitmap GenerateMediumShape(int w, int h)
        {
            bool isColorMode = (cmbMode.SelectedItem.ToString() == "Színes");

            Bitmap bmp = new Bitmap(w, h);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);

                Random rnd = new Random();

                // Ha fekete-fehér mód → fekete szín
                // Ha színes mód → random élénk szín
                Color drawColor = isColorMode
                    ? Color.FromArgb(rnd.Next(100, 256), rnd.Next(100, 256), rnd.Next(100, 256))
                    : Color.Black;

                Pen p = new Pen(drawColor, 1);

                int shape = rnd.Next(6);

                switch (shape)
                {
                    case 0: // Kör
                        g.DrawEllipse(p, w / 4, h / 4, w / 2, h / 2);
                        break;

                    case 1: // Négyszög
                        g.DrawRectangle(p, w / 4, h / 4, w / 2, h / 2);
                        break;

                    case 2: // Háromszög
                        PointF[] tri =
                        {
                    new PointF(w / 2, h / 6),
                    new PointF(w / 6, h * 5 / 6),
                    new PointF(w * 5 / 6, h * 5 / 6)
                };
                        g.DrawPolygon(p, tri);
                        break;

                    case 3: // Kereszt
                        for (int i = 5; i < w - 5; i++)
                        {
                            bmp.SetPixel(w / 2, i, drawColor);
                            bmp.SetPixel(i, h / 2, drawColor);
                        }
                        break;

                    case 4: // Gyémánt
                        PointF[] diamond =
                        {
                    new PointF(w / 2, 0),
                    new PointF(w - 1, h / 2),
                    new PointF(w / 2, h - 1),
                    new PointF(0, h / 2)
                };
                        g.DrawPolygon(p, diamond);
                        break;

                    case 5: // Csillag / zaj
                        for (int i = 0; i < w; i++)
                        {
                            bmp.SetPixel(w / 2, i, drawColor);
                            bmp.SetPixel(i, h / 2, drawColor);

                            if (i % 4 == 0) bmp.SetPixel(i, i, drawColor);
                            if (i % 4 == 0) bmp.SetPixel(w - 1 - i, i, drawColor);
                        }
                        break;
                }
            }
            return bmp;
        }
    }
}