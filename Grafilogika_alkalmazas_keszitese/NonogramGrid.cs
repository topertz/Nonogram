using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Nonogram
{
    public class NonogramGrid
    {
        private Nonogram form;
        private GameTimerManager gameTimerManager;
        private NonogramSolver solver;

        public NonogramGrid(Nonogram form, GameTimerManager g)
        {
            this.form = form;
            this.gameTimerManager = g;
        }

        public void SetGameTimerManager(GameTimerManager gtm)
        {
            gameTimerManager = gtm;
        }
        private void ApplyColorsToBlocks()
        {
            if (!form.isColor)
            {
                for (int i = 0; i < form.row; i++)
                    for (int j = 0; j < form.col; j++)
                        form.solutionColorRGB[i, j] = form.solutionBW[i, j] == 1 ? Color.Black : Color.White;
                return;
            }

            Random rnd = new Random();
            HashSet<Color> usedColors = new HashSet<Color>();

            for (int i = 0; i < form.row; i++)
            {
                Color lastColor = Color.Empty;
                int j = 0;

                while (j < form.col)
                {
                    if (form.solutionBW[i, j] == 0)
                    {
                        form.solutionColorRGB[i, j] = Color.White;
                        lastColor = Color.Empty;
                        j++;
                        continue;
                    }

                    // Új blokk kezdete
                    Color blockColor;
                    do
                    {
                        blockColor = form.nonogramColors[rnd.Next(form.nonogramColors.Length)];
                    } while (blockColor == lastColor);

                    lastColor = blockColor;
                    usedColors.Add(blockColor);

                    // Blokk feltöltése
                    int k = j;
                    while (k < form.col && form.solutionBW[i, k] == 1)
                    {
                        form.solutionColorRGB[i, k] = blockColor;
                        k++;
                    }

                    j = k;
                }
            }

            // Ha csak 1 szín lett használva, cseréljünk ki egy blokkot a második színre
            if (usedColors.Count == 1 && form.nonogramColors.Length > 1)
            {
                Color firstColor = usedColors.First();
                Color secondColor = form.nonogramColors.First(c => c != firstColor);

                // Véletlenszerű blokk módosítása
                for (int i = 0; i < form.row; i++)
                {
                    for (int j = 0; j < form.col; j++)
                    {
                        if (form.solutionBW[i, j] == 1 && form.solutionColorRGB[i, j] == firstColor)
                        {
                            form.solutionColorRGB[i, j] = secondColor;
                            return; // elég egy blokk cseréje
                        }
                    }
                }
            }
        }

        private Color[] GetTwoRandomColorsEasy()
        {
            Random rnd = form.rnd;

            // Lekérjük az összes rendszer-színt, ami nem átlátszó és nem rendszer-specifikus (mint a 'Control')
            List<Color> allColors = Enum.GetValues(typeof(KnownColor))
                .Cast<KnownColor>()
                .Select(Color.FromKnownColor)
                .Where(c => !c.IsSystemColor && c.A == 255 && c != Color.Transparent && c != Color.White)
                .ToList();

            // Szűrjük a túl világos színeket, hogy látszódjanak a fehér háttéren (Luminance check)
            // 0.2126*R + 0.7152*G + 0.0722*B
            var darkEnoughColors = allColors.Where(c => (c.R * 0.2126 + c.G * 0.7152 + c.B * 0.0722) < 180).ToList();

            // Véletlenszerűen kiválasztunk 2-ot
            return darkEnoughColors.OrderBy(x => rnd.Next()).Take(2).ToArray();
        }

        // Teljes Easy Nonogram generálás
        public void GenerateEasyNonogram(int gridLeft, int gridTop)
        {
            if (form.isColor)
            {
                form.nonogramColors = GetTwoRandomColorsEasy(); // Itt frissítjük a globális tömböt
            }
            int w = 5, h = 5, targetPixels = 10;
            Random rnd = form.rnd;
            bool isBoardGood = false;

            while (!isBoardGood)
            {
                int[,] finalBW = new int[h, w];
                int currentPixels = 0, attempts = 0;

                // Pixelek lehelyezése (Fix 10 pixel)
                while (currentPixels < targetPixels && attempts < 1000)
                {
                    attempts++;
                    int r = rnd.Next(h), c = rnd.Next(w), len = rnd.Next(2, 4);
                    if (c + len <= w)
                    {
                        bool spaceFree = true;
                        for (int k = 0; k < len; k++) if (finalBW[r, c + k] == 1) spaceFree = false;
                        if (spaceFree)
                        {
                            for (int k = 0; k < len && currentPixels < targetPixels; k++)
                            {
                                if (finalBW[r, c + k] == 0) { finalBW[r, c + k] = 1; currentPixels++; }
                            }
                        }
                    }
                }
                if (currentPixels < targetPixels) continue;

                // Sűrítés
                var validRows = Enumerable.Range(0, h).Where(r => Enumerable.Range(0, w).Any(c => finalBW[r, c] == 1)).ToList();
                var validCols = Enumerable.Range(0, w).Where(c => Enumerable.Range(0, h).Any(r => finalBW[r, c] == 1)).ToList();
                if (validRows.Count < 5 || validCols.Count < 5) continue;

                int tempH = validRows.Count, tempW = validCols.Count;
                int[,] compressed = new int[tempH, tempW];
                for (int i = 0; i < tempH; i++)
                    for (int j = 0; j < tempW; j++)
                        compressed[i, j] = finalBW[validRows[i], validCols[j]];

                // Szűrők (Egyértelműség)
                bool error = false;
                // Sakk-minta csekk
                for (int i = 0; i < tempH - 1; i++)
                    for (int j = 0; j < tempW - 1; j++)
                        if (compressed[i, j] == compressed[i + 1, j + 1] && compressed[i, j + 1] == compressed[i + 1, j] && compressed[i, j] != compressed[i, j + 1])
                        { error = true; break; }
                if (error) continue;

                // Identikus sor/oszlop csekk
                for (int i = 0; i < tempH - 1; i++)
                    for (int k = i + 1; k < tempH; k++)
                        if (Enumerable.Range(0, tempW).All(j => compressed[i, j] == compressed[k, j])) error = true;
                if (error) continue;

                // ideiglenes beállítás a szín ellenőrzéséhez
                form.row = tempH;
                form.col = tempW;
                form.solutionBW = compressed;
                form.solutionColorRGB = new Color[tempH, tempW];

                ApplyColorsToBlocks();
                GenerateClues();
                NonogramSolver solver = new NonogramSolver(form);
                // szín ellenőrzés
                if (!solver.IsUniqueSolution())
                {
                    continue; // Nem egyértelmű, új generálás
                }

                isBoardGood = true;
            }

            // Inicializálás és UI véglegesítése
            form.userXMark = new bool[form.row, form.col];
            form.userColorRGB = new Color[form.row, form.col];
            form.userColor = new int[form.row, form.col];

            for (int i = 0; i < form.row; i++)
                for (int j = 0; j < form.col; j++)
                {
                    // MINDEN cella False legyen az induláskor!
                    form.userXMark[i, j] = false;

                    form.userColorRGB[i, j] = Color.White;
                    form.userColor[i, j] = 0;
                }

            CreateGridUI(gridLeft, gridTop);
        }

        private Color[] GetThreeRandomColorsMedium()
        {
            Random rnd = form.rnd;

            // Lekérjük az összes rendszer-színt, ami nem átlátszó és nem rendszer-specifikus (mint a 'Control')
            List<Color> allColors = Enum.GetValues(typeof(KnownColor))
                .Cast<KnownColor>()
                .Select(Color.FromKnownColor)
                .Where(c => !c.IsSystemColor && c.A == 255 && c != Color.Transparent && c != Color.White)
                .ToList();

            // Szűrjük a túl világos színeket, hogy látszódjanak a fehér háttéren (Luminance check)
            // 0.2126*R + 0.7152*G + 0.0722*B
            var darkEnoughColors = allColors.Where(c => (c.R * 0.2126 + c.G * 0.7152 + c.B * 0.0722) < 180).ToList();

            // Véletlenszerűen kiválasztunk 3-at
            return darkEnoughColors.OrderBy(x => rnd.Next()).Take(3).ToArray();
        }

        public void GenerateMediumNonogram(int gridLeft, int gridTop)
        {
            if (form.isColor)
            {
                form.nonogramColors = GetThreeRandomColorsMedium();
            }

            int w = 10, h = 10;
            Random rnd = form.rnd;

            int targetPixels = 40;

            bool isBoardGood = false;

            while (!isBoardGood)
            {
                int[,] finalBW = new int[h, w];

                //int targetPixels = rnd.Next(minPixels, maxPixels + 1);
                int currentPixels = 0;
                int attempts = 0;

                // Blokk-alapú generálás
                while (currentPixels < targetPixels && attempts < 1500)
                {
                    attempts++;

                    int r = rnd.Next(h);
                    int c = rnd.Next(w);
                    int len = (targetPixels - currentPixels < 3) ? 1 : rnd.Next(2, 6);

                    if (c + len > w) continue;

                    bool free = true;
                    for (int k = 0; k < len; k++)
                        if (finalBW[r, c + k] == 1) free = false;

                    if (!free) continue;

                    for (int k = 0; k < len && currentPixels < targetPixels; k++)
                    {
                        finalBW[r, c + k] = 1;
                        currentPixels++;
                    }
                }

                // Kötelező: minden sor kapjon legalább 1 pixelt
                for (int r = 0; r < h; r++)
                {
                    if (!Enumerable.Range(0, w).Any(c => finalBW[r, c] == 1))
                    {
                        int c = rnd.Next(w);
                        finalBW[r, c] = 1;
                        currentPixels++;
                    }
                }

                // Kötelező: minden oszlop kapjon legalább 1 pixelt
                for (int c = 0; c < w; c++)
                {
                    if (!Enumerable.Range(0, h).Any(r => finalBW[r, c] == 1))
                    {
                        int r = rnd.Next(h);
                        finalBW[r, c] = 1;
                        currentPixels++;
                    }
                }

                // Sakk-minta (2×2 kétértelműség) tiltás
                bool error = false;
                for (int i = 0; i < h - 1 && !error; i++)
                    for (int j = 0; j < w - 1; j++)
                        if (finalBW[i, j] == finalBW[i + 1, j + 1] &&
                            finalBW[i, j + 1] == finalBW[i + 1, j] &&
                            finalBW[i, j] != finalBW[i, j + 1])
                        {
                            error = true;
                            break;
                        }

                if (error) continue;

                // Identikus sorok tiltása
                for (int i = 0; i < h - 1 && !error; i++)
                    for (int k = i + 1; k < h; k++)
                        if (Enumerable.Range(0, w).All(j => finalBW[i, j] == finalBW[k, j]))
                        {
                            error = true;
                            break;
                        }

                if (error) continue;

                // Megoldás rögzítése
                form.row = h;
                form.col = w;
                form.solutionBW = finalBW;
                form.solutionColorRGB = new Color[h, w];

                ApplyColorsToBlocks();
                GenerateClues();
                NonogramSolver solver = new NonogramSolver(form);
                // Színezés ellenőrzés
                if (!solver.IsUniqueSolution())
                {
                    continue; // Nem egyértelmű, új generálás
                }

                isBoardGood = true;
            }

            // Játékos állapot inicializálása
            form.userXMark = new bool[form.row, form.col];
            form.userColorRGB = new Color[form.row, form.col];
            form.userColor = new int[form.row, form.col];

            for (int i = 0; i < form.row; i++)
                for (int j = 0; j < form.col; j++)
                {
                    form.userXMark[i, j] = false;
                    form.userColorRGB[i, j] = Color.White;
                    form.userColor[i, j] = 0;
                }

            // Clue-k és UI
            CreateGridUI(gridLeft, gridTop);
        }

        private Color[] GetFourRandomColorsHard()
        {
            Random rnd = form.rnd;

            // Lekérjük az összes rendszer-színt, ami nem átlátszó és nem rendszer-specifikus (mint a 'Control')
            List<Color> allColors = Enum.GetValues(typeof(KnownColor))
                .Cast<KnownColor>()
                .Select(Color.FromKnownColor)
                .Where(c => !c.IsSystemColor && c.A == 255 && c != Color.Transparent && c != Color.White)
                .ToList();

            // Szűrjük a túl világos színeket, hogy látszódjanak a fehér háttéren (Luminance check)
            // 0.2126*R + 0.7152*G + 0.0722*B
            var darkEnoughColors = allColors.Where(c => (c.R * 0.2126 + c.G * 0.7152 + c.B * 0.0722) < 180).ToList();

            // Véletlenszerűen kiválasztunk 4-et
            return darkEnoughColors.OrderBy(x => rnd.Next()).Take(4).ToArray();
        }

        public void GenerateHardNonogram(int gridLeft, int gridTop)
        {
            if (form.isColor)
            {
                form.nonogramColors = GetFourRandomColorsHard();
            }

            int w = 15, h = 15;
            Random rnd = form.rnd;

            int targetPixels = 115;

            bool isBoardGood = false;

            while (!isBoardGood)
            {
                int[,] finalBW = new int[h, w];

                //int targetPixels = rnd.Next(minPixels, maxPixels + 1);
                int currentPixels = 0;
                int attempts = 0;

                // Blokk-alapú generálás (HARD, sok rövid blokk)
                while (currentPixels < targetPixels && attempts < 2500)
                {
                    attempts++;

                    int r = rnd.Next(h);
                    int c = rnd.Next(w);
                    int len = (targetPixels - currentPixels < 3) ? 1 : rnd.Next(2, 6);
                    //int len = (targetPixels - currentPixels < 2) ? 1 : rnd.Next(1, 4);

                    if (c + len > w) continue;

                    bool free = true;
                    for (int k = 0; k < len; k++)
                        if (finalBW[r, c + k] == 1) free = false;

                    if (!free) continue;

                    for (int k = 0; k < len && currentPixels < targetPixels; k++)
                    {
                        finalBW[r, c + k] = 1;
                        currentPixels++;
                    }
                }

                // Kötelező: minden sor kapjon legalább 1 pixelt
                for (int r = 0; r < h; r++)
                {
                    if (!Enumerable.Range(0, w).Any(c => finalBW[r, c] == 1))
                    {
                        int c = rnd.Next(w);
                        finalBW[r, c] = 1;
                        currentPixels++;
                    }
                }

                // Kötelező: minden oszlop kapjon legalább 1 pixelt
                for (int c = 0; c < w; c++)
                {
                    if (!Enumerable.Range(0, h).Any(r => finalBW[r, c] == 1))
                    {
                        int r = rnd.Next(h);
                        finalBW[r, c] = 1;
                        currentPixels++;
                    }
                }

                // 2×2 sakk-minta tiltás
                bool error = false;
                for (int i = 0; i < h - 1 && !error; i++)
                    for (int j = 0; j < w - 1; j++)
                        if (finalBW[i, j] == finalBW[i + 1, j + 1] &&
                            finalBW[i, j + 1] == finalBW[i + 1, j] &&
                            finalBW[i, j] != finalBW[i, j + 1])
                        {
                            error = true;
                            break;
                        }

                if (error) continue;

                // Identikus sorok tiltása
                for (int i = 0; i < h - 1 && !error; i++)
                    for (int k = i + 1; k < h; k++)
                        if (Enumerable.Range(0, w).All(j => finalBW[i, j] == finalBW[k, j]))
                        {
                            error = true;
                            break;
                        }

                if (error) continue;

                // Megoldás rögzítése
                form.row = h;
                form.col = w;
                form.solutionBW = finalBW;
                form.solutionColorRGB = new Color[h, w];

                ApplyColorsToBlocks();
                GenerateClues();
                NonogramSolver solver = new NonogramSolver(form);
                // Színezés ellenőrzése
                if (!solver.IsUniqueSolution())
                {
                    continue; // Nem egyértelmű, új generálás
                }

                isBoardGood = true;
            }

            // Játékos állapot inicializálása
            form.userXMark = new bool[form.row, form.col];
            form.userColorRGB = new Color[form.row, form.col];
            form.userColor = new int[form.row, form.col];

            for (int i = 0; i < form.row; i++)
                for (int j = 0; j < form.col; j++)
                {
                    form.userXMark[i, j] = false;
                    form.userColorRGB[i, j] = Color.White;
                    form.userColor[i, j] = 0;
                }

            // Clue-k és UI
            CreateGridUI(gridLeft, gridTop);
        }

        private void GenerateClues()
        {
            form.rowClues = new int[form.row][];
            form.rowClueColors = new Color[form.row][];
            form.colClues = new int[form.col][];
            form.colClueColors = new Color[form.col][];

            // sorok generálása
            for (int i = 0; i < form.row; i++)
            {
                List<int> clues = new List<int>();
                List<Color> colors = new List<Color>();
                int j = 0;

                while (j < form.col)
                {
                    // Ha üres a cella, lépjünk tovább
                    if (form.solutionBW[i, j] == 0)
                    {
                        j++;
                        continue;
                    }

                    // Új blokk kezdődik
                    int count = 0;
                    Color currentBlockColor = form.solutionColorRGB[i, j];
                    int k = j;

                    // Addig tart a blokk, amíg:
                    // A táblán belül vagyunk
                    // A cella ki van töltve (BW == 1)
                    // és (színes mód esetén) a színe megegyezik a blokk kezdő színével
                    while (k < form.col && form.solutionBW[i, k] == 1 &&
                          (!form.isColor || form.solutionColorRGB[i, k] == currentBlockColor))
                    {
                        count++;
                        k++;
                    }

                    // Blokk mentése
                    clues.Add(count);
                    colors.Add(form.isColor ? currentBlockColor : Color.Black);

                    // A belső ciklus végén k a következő blokk vagy üres hely elejére mutat
                    j = k;
                }

                // Ha a sor teljesen üres, opcionálisan adhatunk egy 0-ás jelzést (nem kötelező)
                // if (clues.Count == 0) { clues.Add(0); colors.Add(Color.Black); }

                form.rowClues[i] = clues.ToArray();
                form.rowClueColors[i] = colors.ToArray();
            }

            // oszlopok generálása
            for (int j = 0; j < form.col; j++)
            {
                List<int> clues = new List<int>();
                List<Color> colors = new List<Color>();
                int i = 0;

                while (i < form.row)
                {
                    if (form.solutionBW[i, j] == 0)
                    {
                        i++;
                        continue;
                    }

                    int count = 0;
                    Color currentBlockColor = form.solutionColorRGB[i, j];
                    int k = i;

                    while (k < form.row && form.solutionBW[k, j] == 1 &&
                          (!form.isColor || form.solutionColorRGB[k, j] == currentBlockColor))
                    {
                        count++;
                        k++;
                    }

                    clues.Add(count);
                    colors.Add(form.isColor ? currentBlockColor : Color.Black);

                    i = k;
                }

                form.colClues[j] = clues.ToArray();
                form.colClueColors[j] = colors.ToArray();
            }
        }

        public void AdjustGridSize()
        {
            int maxGridWidth = form.ClientSize.Width - 40;
            int maxGridHeight = form.ClientSize.Height - form.fixedGridTop - 150;

            int maxRowClues = MaxClueLength(form.rowClues);
            int maxColClues = MaxClueLength(form.colClues);

            int cellWidth = (maxGridWidth - maxRowClues * form.userCellSize) / form.col;
            int cellHeight = (maxGridHeight - maxColClues * form.userCellSize) / form.row;

            form.userCellSize = Math.Min(cellWidth, cellHeight);
            form.userCellSize = Math.Min(form.userCellSize, 50);
            form.userCellSize = Math.Max(form.userCellSize, 15);
        }

        public int MaxClueLength(int[][] clues)
        {
            int max = 0;
            foreach (int[] arr in clues) if (arr.Length > max) max = arr.Length;
            return max;
        }

        public void InitializeGridPosition()
        {
            int margin = 10;
            form.fixedGridTop = Math.Max(20, form.chkShowX.Bottom) + margin;
        }

        public void ClearGrid()
        {
            // 1. Vizuális gombok eltávolítása a Form-ról
            if (form.gridButtons != null)
            {
                foreach (Button b in form.gridButtons)
                {
                    if (b != null)
                    {
                        if (form.Controls.Contains(b))
                            form.Controls.Remove(b);
                        b.Dispose(); // Memória felszabadítása
                    }
                }
            }

            // 2. Clue-k (számok) eltávolítása
            var clueLabels = form.Controls.OfType<Label>().Where(l => l.Name.Contains("clueLabel")).ToList();
            foreach (var label in clueLabels)
            {
                form.Controls.Remove(label);
                label.Dispose();
            }

            // 3. LOGIKAI ADATOK ÚJRAINICIALIZÁLÁSA (Ez hiányzott!)
            //form.gridButtons = new Button[form.row, form.col];
            //form.userXMark = new bool[form.row, form.col];       // Mindenhol False lesz
            form.userColor = new int[form.row, form.col];       // Mindenhol 0 lesz
            form.userColorRGB = new Color[form.row, form.col]; // Mindenhol Empty/Fekete lesz

            // 4. userColorRGB feltöltése fehérrel (hogy ne legyen fekete az üres pálya)
            for (int i = 0; i < form.row; i++)
            {
                for (int j = 0; j < form.col; j++)
                {
                    form.userColorRGB[i, j] = Color.White;
                }
            }
        }

        public Bitmap GeneratePreviewImage()
        {
            int rows = form.row;
            int cols = form.col;
            int width = form.picSolutionPreview.Width;
            int height = form.picSolutionPreview.Height;

            Bitmap bmp = new Bitmap(width, height);

            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);

                float cellWidth = (float)width / cols;
                float cellHeight = (float)height / rows;

                for (int i = 0; i < rows; i++)
                {
                    for (int j = 0; j < cols; j++)
                    {
                        RectangleF cellRect = new RectangleF(
                            j * cellWidth,
                            i * cellHeight,
                            cellWidth,
                            cellHeight
                        );

                        Color c = form.isColor
                            ? form.solutionColorRGB[i, j]
                            : (form.solutionBW[i, j] == 1 ? Color.Black : Color.White);

                        using (SolidBrush brush = new SolidBrush(c))
                        {
                            g.FillRectangle(brush, cellRect);
                        }
                    }
                }
            }

            return bmp;
        }

        // Itt jöhetne a CreateGridUI a teljes gombokkal, clue label-ekkel, Checkbox állítással stb.
        public void CreateGridUI(int gridLeft, int gridTop)
        {
            AdjustGridSize(); // Cellaméret frissítése
            ClearGrid();      // Előző grid törlése

            form.gridButtons = new Button[form.row, form.col];
            int cellSize = form.userCellSize;

            int maxRowClues = MaxClueLength(form.rowClues);
            int maxColClues = MaxClueLength(form.colClues);

            // Fix grid pozíció a formon belül
            int startX = 550; // pl. 550
            int startY = 300;  // pl. 300

            // oszlop CLUE-K létrehozása (vertikális) 
            for (int j = 0; j < form.col; j++)
            {
                int[] clues = form.colClues[j];

                // Label magasságok számítása
                int totalHeight = 0;
                int[] clueHeights = new int[clues.Length];
                for (int i = 0; i < clues.Length; i++)
                {
                    Size textSize = TextRenderer.MeasureText(clues[i].ToString(), form.Font);
                    clueHeights[i] = Math.Max(cellSize, textSize.Height + 4);
                    totalHeight += clueHeights[i];
                }

                int yPos = startY - totalHeight; // a blokk teteje
                for (int i = 0; i < clues.Length; i++)
                {
                    Label lbl = new Label();
                    lbl.Name = "clueLabel";
                    lbl.Text = clues[i].ToString();
                    lbl.TextAlign = ContentAlignment.MiddleCenter;
                    lbl.BorderStyle = BorderStyle.FixedSingle;

                    if (form.isColor)
                    {
                        Color clueColor = form.colClueColors[j][i];
                        lbl.BackColor = clueColor;
                        lbl.ForeColor = ((clueColor.R + clueColor.G + clueColor.B) / 3 < 128) ? Color.White : Color.Black;
                    }
                    else
                    {
                        lbl.BackColor = Color.Gray;
                        lbl.ForeColor = Color.White;
                    }

                    lbl.Size = new Size(cellSize, clueHeights[i]);
                    lbl.Location = new Point(startX + j * cellSize, yPos);
                    form.Controls.Add(lbl);

                    yPos += clueHeights[i]; // következő label teteje
                }
            }

            // sor CLUE-K létrehozása (horizontális)
            for (int i = 0; i < form.row; i++)
            {
                int[] clues = form.rowClues[i];

                // Label szélességek számítása
                int totalWidth = 0;
                int[] clueWidths = new int[clues.Length];
                for (int j = 0; j < clues.Length; j++)
                {
                    Size textSize = TextRenderer.MeasureText(clues[j].ToString(), form.Font);
                    clueWidths[j] = Math.Max(cellSize, textSize.Width + 4);
                    totalWidth += clueWidths[j];
                }

                int xPos = startX - totalWidth; // a blokk bal széle
                for (int j = 0; j < clues.Length; j++)
                {
                    Label lbl = new Label();
                    lbl.Name = "clueLabel";
                    lbl.Text = clues[j].ToString();
                    lbl.TextAlign = ContentAlignment.MiddleCenter;
                    lbl.BorderStyle = BorderStyle.FixedSingle;

                    if (form.isColor)
                    {
                        Color clueColor = form.rowClueColors[i][j];
                        lbl.BackColor = clueColor;
                        lbl.ForeColor = ((clueColor.R + clueColor.G + clueColor.B) / 3 < 128) ? Color.White : Color.Black;
                    }
                    else
                    {
                        lbl.BackColor = Color.Gray;
                        lbl.ForeColor = Color.White;
                    }

                    lbl.Size = new Size(clueWidths[j], cellSize);
                    lbl.Location = new Point(xPos, startY + i * cellSize);
                    form.Controls.Add(lbl);

                    xPos += clueWidths[j]; // következő label bal széle
                }
            }

            // grid gombok létrehozása
            for (int i = 0; i < form.row; i++)
            {
                for (int j = 0; j < form.col; j++)
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
                    btn.Click += form.GridCell_Click;
                    btn.MouseDown += form.GridCell_MouseDown;
                    btn.Paint += (s, e) =>
                    {
                        Point p = (Point)((Button)s).Tag;
                        int i2 = p.X, j2 = p.Y;
                        using (Pen thickPen = new Pen(Color.Black, 2))
                        {
                            if (i2 % 5 == 0) e.Graphics.DrawLine(thickPen, 0, 0, btn.Width, 0);
                            if (j2 % 5 == 0) e.Graphics.DrawLine(thickPen, 0, 0, 0, btn.Height);
                            if (j2 == form.col - 1) e.Graphics.DrawLine(thickPen, btn.Width - 1, 0, btn.Width - 1, btn.Height);
                            if (i2 == form.row - 1) e.Graphics.DrawLine(thickPen, 0, btn.Height - 1, btn.Width, btn.Height - 1);
                        }
                    };
                    btn.MouseDown += (s, e) =>
                    {
                        btn = s as Button;
                        if (btn == null) return;

                        if (e.Button == MouseButtons.Left || (e.Button == MouseButtons.Right && !form.isColor))
                        {
                            form.isDragging = false;    // még nincs drag
                            form.dragButton = e.Button; // Left vagy Right
                            form.lastProcessedButton = btn;
                        }
                    };

                    btn.MouseMove += (s, e) =>
                    {
                        if (Control.MouseButtons != form.dragButton) return;

                        Point mousePos = form.PointToClient(Control.MousePosition);
                        Control ctrl = form.GetChildAtPoint(mousePos);
                        Button targetBtn = ctrl as Button;

                        if (targetBtn == null || targetBtn == form.lastProcessedButton) return;

                        form.isDragging = true; // most már drag van
                        form.lastProcessedButton = targetBtn;

                        // Feldolgozás drag során (hibaszámláló, undo nélkül)
                        form.GridCell_MouseDown(targetBtn, new MouseEventArgs(form.dragButton, 0, 0, 0, 0));
                    };

                    btn.MouseUp += (s, e) =>
                    {
                        form.isDragging = false;
                        form.lastProcessedButton = null;
                        form.isDraggingStarted = false;
                    };
                    form.Controls.Add(btn);
                    form.gridButtons[i, j] = btn;
                }
            }

            // Színpaletta (csak színes módban)
            if (form.isColor)
            {
                HashSet<int> colorsSet = new HashSet<int>();
                for (int i = 0; i < form.row; i++)
                    for (int j = 0; j < form.col; j++)
                    {
                        Color c = form.solutionColorRGB[i, j];
                        if (c != Color.White) colorsSet.Add(c.ToArgb());
                    }

                form.colorPalette.Controls.Clear();
                form.colorPalette.Visible = true;
                form.colorPalette.AutoScroll = false;
                form.colorPalette.WrapContents = true;
                form.colorPalette.FlowDirection = FlowDirection.LeftToRight;

                int btnSize = 30;
                int margin = 2;
                int maxCols = 10;
                int totalColors = colorsSet.Count;
                int rowsNeeded = (int)Math.Ceiling(totalColors / (double)maxCols);
                form.colorPalette.Size = new Size(maxCols * (btnSize + margin * 2), rowsNeeded * (btnSize + margin * 2));

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
                        form.SelectColor(colorBtn);
                    };

                    form.colorPalette.Controls.Add(colorBtn);
                }
                if (form.isColor && form.colorPalette.Controls.Count > 0)
                {
                    Button firstColorButton = form.colorPalette.Controls[0] as Button;
                    if (firstColorButton != null)
                    {
                        form.SelectColor(firstColorButton);
                    }
                }
            }
            else
            {
                form.colorPalette.Visible = false;
            }
        }

        public bool IsCellCorrect(int row, int col)
        {
            // 1. Megoldás meghatározása (szín vagy fekete)
            bool shouldBeColor = form.isColor
                ? (form.solutionColorRGB[row, col].ToArgb() != Color.White.ToArgb())
                : form.solutionBW[row, col] == 1;

            // 2. Felhasználó aktuális állapotának lekérése
            Color userC = form.userColorRGB[row, col];

            // hasColor: Akkor tekintjük színesnek, ha nem üres, nem átlátszó és nem fehér
            bool hasColor = !userC.IsEmpty &&
                            userC.ToArgb() != 0 &&
                            userC.ToArgb() != Color.White.ToArgb();

            bool hasX = (form.gridButtons[row, col].Text == "X");

            // 3. ELLENŐRZÉSI LOGIKA
            if (shouldBeColor)
            {
                // Ha SZÍN KELL:
                // Hiba, ha nincs szín, vagy ha véletlenül X van ott
                if (!hasColor || hasX) return false;

                // Színes módnál a konkrét árnyalatot is nézzük
                if (form.isColor)
                    return AreColorsSimilar(userC, form.solutionColorRGB[row, col], 40);

                return true;
            }
            else
            {
                // Ha ÜRESNEK KELL LENNIE:
                // 1. Ha a játékos színezett ide, az mindenképp hiba
                if (hasColor) return false;
                return true;
            }
        }

        public bool IsSolved()
        {
            for (int i = 0; i < form.row; i++)
            {
                for (int j = 0; j < form.col; j++)
                {
                    // Ha csak egyetlen cella is rossz, már nincs kész
                    if (!IsCellCorrect(i, j))
                        return false;
                }
            }
            return true;
        }

        public void HandleGridClick(Button btn, Color selectedColor)
        {
            Point p = (Point)btn.Tag;
            int row = p.X;
            int col = p.Y;

            if (!form.gameTimer.Enabled)
                form.gameTimer.Start();

            // Cellamódosítás
            if (form.isColor)
            {
                if (form.userColorRGB[row, col].ToArgb() == selectedColor.ToArgb())
                {
                    form.ClearCell(row, col, btn);
                }
                else
                {
                    btn.BackColor = selectedColor;
                    form.userColorRGB[row, col] = selectedColor;
                    form.userColor[row, col] = 0;
                    form.userXMark[row, col] = false;
                }
            }
            else
            {
                if (btn.BackColor == Color.Black)
                {
                    form.ClearCell(row, col, btn);
                }
                else
                {
                    btn.BackColor = Color.Black;
                    form.userColorRGB[row, col] = Color.Black;
                    form.userColor[row, col] = 1;
                    form.userXMark[row, col] = false;
                }
            }

            if (IsSolved())
            {
                MessageBox.Show("Gratulálok, kész a Nonogram!");
                form.gameTimer.Stop();
                try
                {
                    // Projekt / exe könyvtár
                    string exeFolder = AppDomain.CurrentDomain.BaseDirectory; // pl: bin\Debug\net6.0
                    string projectFolder = Path.GetFullPath(Path.Combine(exeFolder, @"..\..\..")); // vissza a projekt gyökérhez
                    Directory.CreateDirectory(projectFolder);
                    // Fájlnév generálása (dátummal)
                    string mode = gameTimerManager.GetModeName();
                    string difficulty = gameTimerManager.GetDifficultyName();
                    string fileName = "nonogram_saves.json";
                    string fullPath = Path.Combine(projectFolder, fileName);

                    // Felhasználónév
                    TextBox txtUsername = form.Controls.Find("txtUsername", true)
                        .FirstOrDefault() as TextBox;
                    form.username = txtUsername?.Text.Trim();

                    // Mentés
                    form.saveLoadManager.SaveGame(fullPath, form.username);

                    MessageBox.Show(
                        "A játék automatikusan elmentve a projekt mappájába!",
                        "Mentés",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "Hiba a játék mentése során:\n" + ex.Message,
                        "Mentés hiba",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                }
                ClearGrid();
                gameTimerManager.ResetCellCliks();
                gameTimerManager.ResetColorClicks();
                gameTimerManager.ResetHintClicks();
                form.picPreview.Visible = false;
                form.picSolutionPreview.Visible = false;
                form.lblTimer.Visible = false;
                form.chkShowX.Visible = false;
                form.cmbDifficulty.Visible = true;
                form.cmbMode.Visible = true;
                form.btnSolve.Visible = false;
                form.btnHint.Visible = false;
                form.btnCheck.Visible = false;
                form.btnUndo.Visible = false;
                form.btnRedo.Visible = false;
                form.lblUsername.Visible = true;
                form.txtUsername.Visible = true;
                form.lblWrongCellClicks.Visible = true;
                form.lblWrongColorClicks.Visible = true;
                form.lblHintCount.Visible = true;
                form.btnLeaderboard.Visible = true;
                form.btnGenerateRandom.Visible = true;
                form.btnTips.Visible = true;
                form.txtUsername.Enabled = true;
                form.txtUsername.Text = "";
                form.colorPalette.Visible = false;
                form.gameStarted = false;
                form.elapsedSeconds = 0;
                form.isXMode = true;
                form.lblWrongCellClicks.Text = $"Helytelen kattintások száma: {form.wrongCellClicks} (max: 0)";
                form.lblWrongColorClicks.Text = $"Helytelen színek száma: {form.wrongColorClicks} (max: 0)";
                form.lblHintCount.Text = $"Segítségek száma: {form.hintCount} (max: 0)";
                return;
            }
        }
        public void GenerateNonogramFromImage(Image img, int gridLeft, int gridTop)
        {
            // Analízis bitmap (30x30-as munkaterület a feldolgozáshoz)
            int tempWidth = 30;
            int tempHeight = 30;
            Bitmap tempBmp = new Bitmap(tempWidth, tempHeight);
            using (Graphics g = Graphics.FromImage(tempBmp))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                g.Clear(Color.White);
                g.DrawImage(img, new Rectangle(0, 0, tempWidth, tempHeight));
            }

            // Crop - Üres (fehér) szélek keresése és levágása
            int minX = tempWidth, maxX = 0, minY = tempHeight, maxY = 0;
            bool hasContent = false;
            for (int y = 0; y < tempHeight; y++)
            {
                for (int x = 0; x < tempWidth; x++)
                {
                    Color p = tempBmp.GetPixel(x, y);
                    // Ha a pixel nem fehér (240 feletti értékeknél már fehérnek vesszük)
                    if (p.R < 240 || p.G < 240 || p.B < 240)
                    {
                        if (x < minX) minX = x; if (x > maxX) maxX = x;
                        if (y < minY) minY = y; if (y > maxY) maxY = y;
                        hasContent = true;
                    }
                }
            }

            // Ha teljesen üres a kép, egy alap 5x5-ös rácsot adunk
            if (!hasContent) { minX = 0; maxX = 4; minY = 0; maxY = 4; }

            int finalWidth = maxX - minX + 1;
            int finalHeight = maxY - minY + 1;

            // Munka-bitmap létrehozása a vágott mérettel
            Bitmap bmp = new Bitmap(finalWidth, finalHeight);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.DrawImage(tempBmp, new Rectangle(0, 0, finalWidth, finalHeight),
                            new Rectangle(minX, minY, finalWidth, finalHeight), GraphicsUnit.Pixel);
            }

            // Adatszerkezetek inicializálása
            form.row = finalHeight;
            form.col = finalWidth;
            form.isColor = false;

            form.solutionColorRGB = new Color[form.row, form.col];
            form.solutionBW = new int[form.row, form.col];
            form.userColorRGB = new Color[form.row, form.col];
            form.userColor = new int[form.row, form.col];
            form.userXMark = new bool[form.row, form.col];
            form.gridButtons = new Button[form.row, form.col];
            bool[,] fillableCells = new bool[form.row, form.col];

            // Pixeladatok feldolgozása - Konzisztens threshold (küszöb) használata
            for (int i = 0; i < form.row; i++)
            {
                for (int j = 0; j < form.col; j++)
                {
                    Color pixel = bmp.GetPixel(j, i);
                    form.solutionColorRGB[i, j] = pixel;

                    // Luminancia kiszámítása (Y = 0.299R + 0.587G + 0.114B)
                    double luminance = 0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B;

                    // Konzisztens döntés: Ha sötétebb, mint 220, akkor kifestendő (1-es)
                    bool isFilled = luminance < 220;
                    fillableCells[i, j] = isFilled;
                    form.solutionBW[i, j] = isFilled ? 1 : 0;

                    // Szín detektálás: Ha a csatornák között jelentős eltérés van, színes módba váltunk
                    int maxDiff = Math.Max(Math.Abs(pixel.R - pixel.G),
                                  Math.Max(Math.Abs(pixel.G - pixel.B), Math.Abs(pixel.R - pixel.B)));
                    if (maxDiff > 20) form.isColor = true;

                    // UI alaphelyzet
                    form.userColorRGB[i, j] = Color.White;
                    form.userXMark[i, j] = false;
                }
            }

            // Sorok Clue-jainak kiszámítása
            form.rowClues = new int[form.row][];
            form.rowClueColors = new Color[form.row][];
            for (int i = 0; i < form.row; i++)
            {
                List<int> clues = new List<int>();
                List<Color> colors = new List<Color>();
                int j = 0;
                while (j < form.col)
                {
                    if (!fillableCells[i, j]) { j++; continue; }

                    int count = 0;
                    Color seed = form.solutionColorRGB[i, j];
                    int k = j;
                    while (k < form.col && fillableCells[i, k])
                    {
                        Color c = form.solutionColorRGB[i, k];
                        // Ha színes a mód, a színeltérésnél új blokkot kezdünk
                        if (form.isColor && count > 0 && !AreColorsSimilar(seed, c, form.colorSimilarityThreshold)) break;
                        count++; k++;
                    }
                    clues.Add(count);
                    colors.Add(form.isColor ? seed : Color.Black);
                    j = k;
                }
                form.rowClues[i] = clues.ToArray();
                form.rowClueColors[i] = colors.ToArray();
            }

            // Oszlopok Clue-jainak kiszámítása
            form.colClues = new int[form.col][];
            form.colClueColors = new Color[form.col][];
            for (int j = 0; j < form.col; j++)
            {
                List<int> clues = new List<int>();
                List<Color> colors = new List<Color>();
                int i = 0;
                while (i < form.row)
                {
                    if (!fillableCells[i, j]) { i++; continue; }

                    int count = 0;
                    Color seed = form.solutionColorRGB[i, j];
                    int k = i;
                    while (k < form.row && fillableCells[k, j])
                    {
                        Color c = form.solutionColorRGB[k, j];
                        if (form.isColor && count > 0 && !AreColorsSimilar(seed, c, form.colorSimilarityThreshold)) break;
                        count++; k++;
                    }
                    clues.Add(count);
                    colors.Add(form.isColor ? seed : Color.Black);
                    i = k;
                }
                form.colClues[j] = clues.ToArray();
                form.colClueColors[j] = colors.ToArray();
            }

            // UI generálása és Preview megjelenítése
            CreateGridUI(gridLeft, gridTop);

            if (form.picSolutionPreview != null)
            {
                Bitmap finalPreview = GeneratePreviewImage(); // A korábban megírt preview generálód
                form.picSolutionPreview.SizeMode = PictureBoxSizeMode.Zoom;
                form.picSolutionPreview.Image = finalPreview;
            }

            // Erőforrások felszabadítása
            tempBmp.Dispose();
            bmp.Dispose();
        }

        public bool AreColorsSimilar(Color a, Color b, int threshold)
        {
            int dr = a.R - b.R;
            int dg = a.G - b.G;
            int db = a.B - b.B;
            int dist2 = dr * dr + dg * dg + db * db;
            return dist2 <= threshold * threshold;
        }
    }
}