using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nonogram
{
    public class SaveLoadManager
    {
        private Nonogram form;

        public SaveLoadManager(Nonogram f)
        {
            form = f;
        }

        // Mentés
        public void SaveGame(string filename, string username)
        {
            string[] difficulties = { "Könnyű", "Közepes", "Nehéz" };
            string[] modes = { "Fekete-fehér", "Színes" };
            bool isColorMode = form.cmbMode.SelectedIndex == 1; // 1-es index a színes

            NonogramSaveData saveData = new NonogramSaveData
            {
                Username = username,
                Difficulty = difficulties[form.cmbDifficulty.SelectedIndex],
                Mode = modes[form.cmbMode.SelectedIndex],
                HintCount = form.hintCount,
                WrongCellClicks = form.wrongCellClicks,
                WrongColorClicks = isColorMode ? (int?)form.wrongColorClicks : null,
                ElapsedSeconds = form.elapsedSeconds
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
            form.hintCount = saveData.HintCount;
            form.wrongCellClicks = saveData.WrongCellClicks;
            //form.wrongColorClicks = saveData.WrongColorClicks;

            // Grid frissítése
            form.grid.CreateGridUI(20, 150);
            form.renderer.UpdatePreview();
            form.renderer.ToggleXMarks(form.chkShowX.Checked);
        }
    }
}