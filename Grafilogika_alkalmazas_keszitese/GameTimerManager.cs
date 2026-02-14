using System;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Grafilogika_alkalmazas_keszitese
{
    public class GameTimerManager
    {
        private Nonogram form;
        private NonogramGrid grid;
        private NonogramRender render;
        private UndoRedoManager undoredoManager;
        private ExtraGridManager extraGridManager;
        private NonogramHintEngine hintEngine;

        public GameTimerManager(Nonogram f, NonogramGrid g, NonogramRender r, UndoRedoManager u, ExtraGridManager e, NonogramHintEngine h)
        {
            form = f;
            grid = g;
            render = r;
            undoredoManager = u;
            extraGridManager = e;
            hintEngine = h;

            // A Timer a Form1-ből jön
            form.gameTimer = new Timer();
            form.gameTimer.Interval = 1000;
            form.gameTimer.Tick += Timer_Tick;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (form.remainingSeconds <= 0)
            {
                form.gameTimer.Stop();

                MessageBox.Show(
                    "Lejárt az idő! A játék újraindul.",
                    "Idő lejárt",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );

                RestartGameWithCurrentDifficulty();
                return;
            }

            form.remainingSeconds--;
            form.elapsedSeconds++;
            UpdateLabel();
        }

        public void CmbDifficultyOrMode_Changed(object sender, EventArgs e)
        {
            UpdateDifficultyAndModeToolTip();
            if (!form.gameStarted && !form.chkExtraMode.Checked)
                return;
            form.isColor = form.cmbMode.SelectedItem?.ToString() == "Színes";
            form.selectedColor = form.isColor ? Color.White : Color.Black;
            DifficultyOrModeChanged();
            undoredoManager.ClearHistory();
        }

        public void BtnRestart_Click(object sender, EventArgs e)
        {
            RestartGameWithCurrentDifficulty();
        }

        public void BtnTips_Click(object sender, EventArgs e)
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

        public void Start()
        {
            SetTimeByDifficulty();
            form.gameTimer.Start();
        }

        public void Stop()
        {
            form.gameTimer.Stop();
        }

        private void UpdateLabel()
        {
            int minutes = form.remainingSeconds / 60;
            int seconds = form.remainingSeconds % 60;
            form.lblTimer.Text = $"{minutes:D2}:{seconds:D2}";
        }

        private void SetTimeByDifficulty()
        {
            if (form.isColor) // Színes mód
            {
                switch (form.cmbDifficulty.SelectedIndex)
                {
                    case 0: form.remainingSeconds = 6 * 60; break;   // Könnyű
                    case 1: form.remainingSeconds = 18 * 60; break;   // Közepes
                    case 2: form.remainingSeconds = 35 * 60; break;   // Nehéz
                    default: form.remainingSeconds = 6 * 60; break;
                }
            }
            else // Fekete-fehér mód
            {
                switch (form.cmbDifficulty.SelectedIndex)
                {
                    case 0: form.remainingSeconds = 6 * 60; break;   // Könnyű
                    case 1: form.remainingSeconds = 18 * 60; break;   // Közepes
                    case 2: form.remainingSeconds = 35 * 60; break;   // Nehéz
                    default: form.remainingSeconds = 6 * 60; break;
                }
            }

            UpdateLabel();
        }

        public void DifficultyOrModeChanged()
        {
            form.chkShowX.Checked = false;
            form.btnCheck.Enabled = true;
            form.chkShowX.Enabled = true;

            if (grid == null) return;

            ResetCellCliks();
            ResetColorClicks();
            ResetHintClicks();

            grid.ClearGrid();
            form.gameTimerManager.Stop();

            form.isColor = form.cmbMode.SelectedItem?.ToString() == "Színes";
            form.lblWrongColorClicks.Visible = form.isColor;

            int width = 5, height = 5, targetPixels = 13, maxAttempts = 1000; // alapértékek

            switch (form.cmbDifficulty.SelectedIndex)
            {
                case 0: // Könnyű
                    width = height = 5;
                    targetPixels = form.isColor ? 11 : 13;
                    maxAttempts = 1000;
                    form.chkShowX.Visible = false;
                    form.btnSelectImage.Visible = false;
                    break;

                case 1: // Közepes
                    width = height = 10;
                    targetPixels = form.isColor ? 45 : 50;
                    maxAttempts = 1500;
                    form.chkShowX.Visible = false;
                    form.btnSelectImage.Visible = false;
                    break;

                case 2: // Nehéz
                    width = height = 15;
                    targetPixels = form.isColor ? 102 : 113;
                    maxAttempts = 2500;
                    form.chkShowX.Visible = false;
                    form.btnSelectImage.Visible = false;
                    break;

                default:
                    form.cmbDifficulty.SelectedIndex = 0;
                    break;
            }

            form.btnSolve.Enabled = true;
            form.btnHint.Enabled = true;
            form.btnCheck.Enabled = true;
            form.btnRedo.Enabled = true;
            form.btnUndo.Enabled = true;
            form.chkShowX.Enabled = true;
            form.lblUndoCount.Visible = false;
            form.lblRedoCount.Visible = false;

            // Generálás
            grid.GenerateNonogram(20, 150, width, height, targetPixels, maxAttempts);

            form.lblTimer.Visible = true;
            form.picPreview.Visible = true;
            form.picSolutionPreview.Visible = false;

            // Timer újraindítása
            Start();

            if (form.picSolutionPreview != null)
            {
                form.picSolutionPreview.Image = render.GeneratePreviewImage();
                form.picSolutionPreview.SizeMode = PictureBoxSizeMode.Zoom;
            }

            hintEngine.SetHintEngine(form, form.row, form.col, form.isColor, form.solutionBW, form.solutionColorRGB, form.userColorRGB, form.userXMark);
            hintEngine.UpdateHints();

            SetMaxWrongClicksByDifficulty();
            SetMaxHintsByDifficulty();
            undoredoManager.UpdateUndoRedoLabels();
            render.UpdatePreview();
        }

        public void ResetCellCliks()
        {
            form.wrongCellClicks = 0;
            form.lblWrongCellClicks.Text = $"Helytelen kattintások száma: 0 (max: {form.maxWrongCellClicks})";
        }

        public void ResetColorClicks()
        {
            form.wrongColorClicks = 0;
            form.lblWrongColorClicks.Text = $"Helytelen színek száma: 0 (max: {form.maxWrongColorClicks})";
        }

        public void ResetHintClicks()
        {
            form.hintCount = 0;
            form.lblHintCount.Text = "Segítségek száma: 0";
        }

        public void StartTimer()
        {
            form.gameTimerManager.Start();
        }

        public void RestartGameWithCurrentDifficulty()
        {
            grid.ClearGrid();
            undoredoManager.ClearHistory();
            ResetCellCliks();
            ResetColorClicks();
            form.wrongCellClicks = 0;
            form.wrongColorClicks = 0;
            form.hintCount = 0;

            form.lblWrongCellClicks.Text = $"Helytelen kattintások száma: {form.wrongCellClicks} (max: {form.maxWrongCellClicks})";
            form.lblWrongColorClicks.Text = $"Helytelen színek száma: {form.wrongColorClicks} (max: {form.maxWrongColorClicks})";
            form.lblHintCount.Text = "Segítségek száma: 0";
            form.picPreview.Image = null;

            if (form.txtUsername != null)
            {
                form.txtUsername.Enabled = false;
            }

            form.btnCheck.Enabled = true;
            form.chkShowX.Enabled = true;
            form.btnSolve.Enabled = true;
            form.elapsedSeconds = 0;
            form.undoClicks = 0;
            form.redoClicks = 0;
            undoredoManager.UpdateUndoRedoLabels();

            int width = 5, height = 5, targetPixels = 13, maxAttempts = 1000;

            switch (form.cmbDifficulty.SelectedIndex)
            {
                case 0: // Könnyű
                    width = height = 5;
                    targetPixels = form.isColor ? 11 : 13;
                    maxAttempts = 1000;
                    break;

                case 1: // Közepes
                    width = height = 10;
                    targetPixels = form.isColor ? 45 : 50;
                    maxAttempts = 1500;
                    break;

                case 2: // Nehéz
                    width = height = 15;
                    targetPixels = form.isColor ? 102 : 113;
                    maxAttempts = 2500;
                    break;
            }

            grid.GenerateNonogram(20, 150, width, height, targetPixels, maxAttempts);

            if (form.picSolutionPreview != null)
            {
                form.picSolutionPreview.Image = render.GeneratePreviewImage();
                form.picSolutionPreview.SizeMode = PictureBoxSizeMode.Zoom;
            }

            Start();
            render.UpdatePreview();
        }

        public string GetDifficultyName()
        {
            switch (form.cmbDifficulty.SelectedIndex)
            {
                case 0: return "konnyu";
                case 1: return "kozepes";
                case 2: return "nehez";
                default: return "ismeretlen";
            }
        }

        public string GetModeName()
        {
            return form.isColor ? "szines" : "fekete-feher";
        }

        public void SetMaxWrongClicksByDifficulty()
        {
            if (form.isColor) // Színes mód
            {
                switch (form.cmbDifficulty.SelectedIndex)
                {
                    case 0: // Könnyű
                        form.maxWrongCellClicks = 12;
                        form.maxWrongColorClicks = 12;
                        break;
                    case 1: // Közepes
                        form.maxWrongCellClicks = 9;
                        form.maxWrongColorClicks = 9;
                        form.maxUndoClicks = 3;
                        form.maxRedoClicks = 3;
                        break;
                    case 2: // Nehéz
                        form.maxWrongCellClicks = 6;
                        form.maxWrongColorClicks = 6;
                        form.maxUndoClicks = 2;
                        form.maxRedoClicks = 2;
                        break;
                    default:
                        form.maxWrongCellClicks = 12;
                        form.maxWrongColorClicks = 12;
                        break;
                }
            }
            else // Fekete-fehér mód
            {
                switch (form.cmbDifficulty.SelectedIndex)
                {
                    case 0: // Könnyű
                        form.maxWrongCellClicks = 12;
                        form.maxWrongColorClicks = 12;
                        break;
                    case 1: // Közepes
                        form.maxWrongCellClicks = 9;
                        form.maxWrongColorClicks = 9;
                        form.maxUndoClicks = 3;
                        form.maxRedoClicks = 3;
                        break;
                    case 2: // Nehéz
                        form.maxWrongCellClicks = 6;
                        form.maxWrongColorClicks = 6;
                        form.maxUndoClicks = 2;
                        form.maxRedoClicks = 2;
                        break;

                    default:
                        form.maxWrongCellClicks = 12;
                        form.maxWrongColorClicks = 12;
                        break;
                }
            }

            // Frissítjük a label szöveget
            form.lblWrongCellClicks.Text = $"Helytelen kattintások száma: {form.wrongCellClicks} (max: {form.maxWrongCellClicks})";
            form.lblWrongColorClicks.Text = $"Helytelen színek száma: {form.wrongColorClicks} (max: {form.maxWrongColorClicks})";
        }

        public void SetMaxHintsByDifficulty()
        {
            if (form.isColor) // Színes mód
            {
                switch (form.cmbDifficulty.SelectedIndex)
                {
                    case 0: // Könnyű
                        form.maxHintCount = 5;
                        break;
                    case 1: // Közepes
                        form.maxHintCount = 3;
                        break;
                    case 2: // Nehéz
                        form.maxHintCount = 2;
                        break;
                    default:
                        form.maxHintCount = 5;
                        break;
                }
            }
            else // Fekete-fehér mód
            {
                switch (form.cmbDifficulty.SelectedIndex)
                {
                    case 0: // Könnyű
                        form.maxHintCount = 5;
                        break;
                    case 1: // Közepes
                        form.maxHintCount = 3;
                        break;
                    case 2: // Nehéz
                        form.maxHintCount = 2;
                        break;
                    default:
                        form.maxHintCount = 5;
                        break;
                }
            }

            form.lblHintCount.Text = $"Segítségek száma: {form.hintCount} (max: {form.maxHintCount})";
        }

        public void UpdateDifficultyAndModeToolTip()
        {
            string difficulty = form.cmbDifficulty.SelectedItem?.ToString()?.Trim();
            string mode = form.cmbMode.SelectedItem?.ToString()?.Trim();

            if (difficulty == null || mode == null)
                return;

            // Emoji eltávolítása (csak az első szó marad)
            int spaceIndex = difficulty.IndexOf(' ');
            if (spaceIndex > 0)
                difficulty = difficulty.Substring(0, spaceIndex);

            string text = "";

            if (mode == "Fekete-fehér")
            {
                switch (difficulty)
                {
                    case "Könnyű":
                        text = "Könnyű szint – Fekete-fehér mód\nHelytelen kattintások: 12\nSegítségek száma: 5\nKorlátlan számú visszavonás\nKorlátlan számú előrelépés";
                        break;
                    case "Közepes":
                        text = "Közepes szint – Fekete-fehér mód\nHelytelen kattintások: 6\nSegítségek száma: 3\nVisszavonások száma: 3\nElőrelépések száma: 3";
                        break;
                    case "Nehéz":
                        text = "Nehéz szint – Fekete-fehér mód\nHelytelen kattintások: 3\nSegítségek száma: 1\nVisszavonások száma: 2\nElőrelépések száma: 2";
                        break;
                }
            }
            else if (mode == "Színes")
            {
                switch (difficulty)
                {
                    case "Könnyű":
                        text = "Könnyű szint – Színes mód\nHelytelen kattintások: 10\nSegítségek száma: 4\nKorlátlan számú visszavonás\nKorlátlan számú előrelépés";
                        break;
                    case "Közepes":
                        text = "Közepes szint – Színes mód\nHelytelen kattintások: 5\nSegítségek száma: 2\nVisszavonások száma: 3\nElőrelépések száma: 3";
                        break;
                    case "Nehéz":
                        text = "Nehéz szint – Színes mód\nHelytelen kattintások: 2\nSegítségek száma: 1\nVisszavonások száma: 2\nElőrelépések száma: 2";
                        break;
                }
            }

            form.toolTip.SetToolTip(form.cmbDifficulty, text);
        }
    }
}