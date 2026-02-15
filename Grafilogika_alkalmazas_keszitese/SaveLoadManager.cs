using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Grafilogika_alkalmazas_keszitese
{
    public class SaveLoadManager
    {
        private Nonogram form;
        private NonogramGrid grid;
        private NonogramRender render;
        private GameTimerManager gameTimerManager;
        public SaveLoadManager(Nonogram f, NonogramGrid g, NonogramRender r, GameTimerManager game)
        {
            this.form = f;
            this.grid = g;
            this.render = r;
            this.gameTimerManager = game;
        }

        public void SetGrid(NonogramGrid g)
        {
            this.grid = g;
        }
        public void SetRender(NonogramRender r)
        {
            render = r;
        }
        public void SetTimerManager(GameTimerManager g)
        {
            gameTimerManager = g;
        }

        // Mentés
        public void SaveGame(string filename, string username)
        {
            string[] difficulties = { "Könnyű", "Közepes", "Nehéz" };
            string[] modes = { "Fekete-fehér", "Színes" };
            bool isColorMode = form.cmbMode.SelectedIndex == 1;

            NonogramSaveData saveData = new NonogramSaveData
            {
                Username = username,
                Difficulty = difficulties[form.cmbDifficulty.SelectedIndex],
                Mode = modes[form.cmbMode.SelectedIndex],
                HintCount = render.hintCount,
                WrongCellClicks = grid.wrongCellClicks,
                WrongColorClicks = isColorMode ? (int?)grid.wrongColorClicks : null,
                ElapsedSeconds = gameTimerManager.elapsedSeconds
            };

            JsonSerializerOptions options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            List<NonogramSaveData> allSaves = new List<NonogramSaveData>();

            // Ha már létezik a fájl, olvassuk be a meglévő játékokat
            if (File.Exists(filename))
            {
                string existingJson = File.ReadAllText(filename);
                List<NonogramSaveData> existingSaves = JsonSerializer.Deserialize<List<NonogramSaveData>>(existingJson, options);
                if (existingSaves != null)
                    allSaves.AddRange(existingSaves);
            }

            // Adjunk hozzá az új mentést
            allSaves.Add(saveData);

            // Mentsük vissza az összes mentést
            string json = JsonSerializer.Serialize(allSaves, options);
            File.WriteAllText(filename, json, new System.Text.UTF8Encoding(true));
        }

        // Betöltés
        public void LoadGame(string filename)
        {
            if (!File.Exists(filename)) return;

            string json = File.ReadAllText(filename);
            JsonSerializerOptions options = new JsonSerializerOptions();

            NonogramSaveData saveData = JsonSerializer.Deserialize<NonogramSaveData>(json, options);
            if (saveData == null) return;

            // Combobox index visszaállítása a szöveg alapján
            string[] difficulties = { "Könnyű", "Közepes", "Nehéz" };
            string[] modes = { "Fekete-fehér", "Színes" };

            form.username = saveData.Username;
            form.cmbDifficulty.SelectedIndex = Array.IndexOf(difficulties, saveData.Difficulty);
            form.cmbMode.SelectedIndex = Array.IndexOf(modes, saveData.Mode);
            render.hintCount = saveData.HintCount;
            grid.wrongCellClicks = saveData.WrongCellClicks;
            grid.wrongColorClicks = (int)saveData.WrongColorClicks;

            // Grid frissítése
            grid.CreateGridUI(20, 150);
            render.UpdatePreview();
            render.ToggleXMarks(form.chkShowX.Checked);
        }
    }
}