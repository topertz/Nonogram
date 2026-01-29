using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nonogram
{
    // A mentendő adatmodell
    public class NonogramSaveData
    {
        public string Username { get; set; }
        public string Difficulty { get; set; }
        public string Mode { get; set; }
        public int HintCount { get; set; }
        public int WrongCellClicks { get; set; }
        public int WrongColorClicks { get; set; }
        public int ElapsedSeconds { get; set; }
    }
}
