using System;
using System.Collections.Generic;
public class ClueSaveData
{
    public int Rows { get; set; }
    public int Cols { get; set; }

    public List<string> RowCluesRtf { get; set; } = new List<string>();
    public List<string> ColCluesRtf { get; set; } = new List<string>();
}
