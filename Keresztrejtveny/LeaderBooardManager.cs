using Nonogram;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text.Json;
using System.Linq;

public static class LeaderboardManager
{
    public static DataTable CreateTable()
    {
        DataTable table = new DataTable("Leaderboard");

        table.Columns.Add("Játékos", typeof(string));
        table.Columns.Add("Nehézség", typeof(string));
        table.Columns.Add("Játékmód", typeof(string));
        table.Columns.Add("Rossz kattintások száma", typeof(int));
        table.Columns.Add("Segítségek száma", typeof(int));
        table.Columns.Add("Eltelt idő (s)", typeof(string));

        return table;
    }

    public static void AddSaveToTable(DataTable table, NonogramSaveData save)
    {
        int score = CalculateScore(save);
        int minutes = save.ElapsedSeconds / 60;
        int seconds = save.ElapsedSeconds % 60;
        string timeFormatted = $"{minutes:D2}:{seconds:D2}"; // pl. 00:35, 01:00

        table.Rows.Add(
            save.Username,
            save.Difficulty,
            save.Mode,
            save.WrongClicks,
            save.HintCount,
            timeFormatted
        );
    }

    private static int CalculateScore(NonogramSaveData save)
    {
        int baseScore = 0;

        switch (save.Difficulty)
        {
            case "Könnyű":
                baseScore = 1000;
                break;
            case "Közepes":
                baseScore = 2000;
                break;
            case "Nehéz":
                baseScore = 3000;
                break;
            default:
                baseScore = 0;
                break;
        }

        if (save.Mode == "Színes")
            baseScore += 500;

        baseScore -= save.WrongClicks * 100;
        baseScore -= save.HintCount * 150;

        return baseScore;
    }

    public static DataTable LoadAllSaves(string folderPath)
    {
        DataTable table = CreateTable();

        JsonSerializerOptions options = new JsonSerializerOptions();

        List<NonogramSaveData> saves = new List<NonogramSaveData>();

        foreach (string file in Directory.GetFiles(folderPath, "*.json"))
        {
            string json = File.ReadAllText(file);
            NonogramSaveData save = JsonSerializer.Deserialize<NonogramSaveData>(json, options);

            if (save != null)
                saves.Add(save);
        }

        // Rangsorolás: idő, segítség, hibák
        List<NonogramSaveData> sorted = saves
            .OrderBy(s => s.ElapsedSeconds)
            .ThenBy(s => s.HintCount)
            .ThenBy(s => s.WrongClicks)
            .ToList();

        foreach (NonogramSaveData save in sorted)
            AddSaveToTable(table, save);

        return table;
    }
}