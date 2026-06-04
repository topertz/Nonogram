using System.Text.Json.Serialization;

namespace Grafilogika_alkalmazas_keszitese
{
    // The data model to save
    public class NonogramSaveData
    {
        public string Username { get; set; }
        public string Difficulty { get; set; }
        public string Mode { get; set; }
        public int HintCount { get; set; }
        public int WrongCellClicks { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? WrongColorClicks { get; set; }
        public int ElapsedSeconds { get; set; }
    }
}