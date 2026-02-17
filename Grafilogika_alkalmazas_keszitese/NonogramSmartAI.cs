using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Grafilogika_alkalmazas_keszitese
{
    public partial class NonogramSmartAI
    {
        private Nonogram form;
        private NonogramGrid grid;
        private NonogramRender render;

        public NonogramSmartAI(Nonogram f, NonogramGrid g, NonogramRender r)
        {
            this.form = f;
            this.grid = g;
            this.render = r;
        }

        public void SetGrid(NonogramGrid g)
        {
            grid = g;
        }
        public void SetRender(NonogramRender r)
        {
            render = r;
        }
        // Rekurzív backtracking + logikai lépések
        public async Task<bool> SolveWithSpeculation()
        {
            // 1. Logikai lépések (ezek a biztos pontok)
            bool changed;
            do
            {
                changed = false;
                for (int r = 0; r < grid.row; r++) if (await AnalyzeLine(r, true)) { changed = true; break; }
                if (changed) continue;
                for (int c = 0; c < grid.col; c++) if (await AnalyzeLine(c, false)) { changed = true; break; }
            } while (changed);

            if (IsBoardCompletelyValid()) return true;

            // 2. Spekuláció (Backtracking)
            for (int r = 0; r < grid.row; r++)
            {
                for (int c = 0; c < grid.col; c++)
                {
                    // Csak üres cellát vizsgálunk
                    if (!IsFilled(r, c) && !IsX(r, c))
                    {
                        // --- EZ AZ A RÉSZ, AMI MEGOLDJA A PROBLÉMÁDAT ---
                        // Kigyűjtjük, mik a legális színek az adott sorban és oszlopban
                        var allowedInRow = grid.rowClueColors[r].Select(clr => clr.ToArgb()).ToHashSet();
                        var allowedInCol = grid.colClueColors[c].Select(clr => clr.ToArgb()).ToHashSet();

                        // CSAK az a szín jöhet szóba, ami MINDKÉT halmazban benne van
                        // Ha a sorban csak barna van (allowedInRow), de az oszlopban van lila is, 
                        // a lila NEM kerül be a validColors listába.
                        var validColors = allowedInRow.Intersect(allowedInCol).ToList();

                        // Ha nincs közös szín, akkor oda matematikai képtelenség bármit festeni -> X
                        if (validColors.Count == 0)
                        {
                            if (await SetX(r, c, "A sor és oszlop szabályai kizárják egymást (nincs közös szín)"))
                                return await SolveWithSpeculation();
                            return false;
                        }

                        // Csak a valid színekhez tartozó blokkokat kérjük le
                        var candidates = GetCandidateBlocks(r, c).ToList();

                        foreach (var (isRow, lineIdx, blockIdx) in candidates)
                        {
                            Color blockColor = GetClueColor(lineIdx, isRow, blockIdx);
                            int blockArgb = blockColor.ToArgb();

                            // DUPLA ELLENŐRZÉS: Ha a blokk színe nem szerepel a cella metszet-színei között, kihagyjuk
                            if (!validColors.Contains(blockArgb)) continue;

                            int length = isRow ? grid.col : grid.row;
                            int[] clues = isRow ? grid.rowClues[lineIdx] : grid.colClues[lineIdx];
                            int blockLen = clues[blockIdx];

                            int[] leftmost = GetLeftmost(lineIdx, isRow, length, clues);
                            int[] rightmost = GetRightmost(lineIdx, isRow, length, clues);
                            if (leftmost == null || rightmost == null) continue;

                            for (int start = leftmost[blockIdx]; start <= rightmost[blockIdx]; start++)
                            {
                                int posInLine = isRow ? c : r;
                                if (posInLine < start || posInLine >= start + blockLen) continue;

                                // Megnézzük, lehelyezhető-e a blokk
                                if (!CanPlaceBlock(lineIdx, isRow, start, blockLen, blockColor)) continue;
                                if (!IsPlacementOrderValid(lineIdx, isRow, blockIdx, start, blockLen)) continue;

                                // Keresztirányú szín-legitimitás ellenőrzése
                                bool crossCheck = true;
                                for (int i = 0; i < blockLen; i++)
                                {
                                    int tr = isRow ? lineIdx : start + i;
                                    int tc = isRow ? start + i : lineIdx;
                                    if (!IsColorLegalAtPosition(tr, tc, blockColor, !isRow))
                                    {
                                        crossCheck = false;
                                        break;
                                    }
                                }
                                if (!crossCheck) continue;

                                // --- MENTÉS ÉS PRÓBA ---
                                var backupColors = (Color[,])grid.userColorRGB.Clone();
                                var backupX = (bool[,])grid.userXMark.Clone();

                                for (int i = 0; i < blockLen; i++)
                                {
                                    int currR = isRow ? lineIdx : start + i;
                                    int currC = isRow ? start + i : lineIdx;
                                    await SetCell(currR, currC, blockColor, "Spekulatív elhelyezés");
                                }

                                if (await SolveWithSpeculation()) return true;

                                // --- VISSZALÉPÉS ---
                                grid.userColorRGB = backupColors;
                                grid.userXMark = backupX;
                                render.UpdatePreview();
                            }
                        }

                        // Ha egyik legális szín sem vezetett megoldáshoz, megpróbáljuk az X-et
                        if (await SetX(r, c, "Egyik lehetséges szín sem működött ezen a ponton"))
                        {
                            if (await SolveWithSpeculation()) return true;
                        }

                        return false; // Zsákutca
                    }
                }
            }
            return IsBoardCompletelyValid();
        }

        private bool IsColorLegalAtPosition(int r, int c, Color colorToPlace, bool checkColumn)
        {
            // Adatok lekérése a vizsgált irány szerint
            int lineIdx = checkColumn ? c : r;
            int posInLine = checkColumn ? r : c;
            int lineLength = checkColumn ? grid.row : grid.col;
            int[] clues = checkColumn ? grid.colClues[lineIdx] : grid.rowClues[lineIdx];
            Color[] clueColors = checkColumn ? grid.colClueColors[lineIdx] : grid.rowClueColors[lineIdx];

            // 1. Határok kiszámítása a JELENLEGI tábla alapján (X-ek és színek számítanak!)
            int[] leftmost = GetLeftmost(lineIdx, !checkColumn, lineLength, clues);
            int[] rightmost = GetRightmost(lineIdx, !checkColumn, lineLength, clues);

            if (leftmost == null || rightmost == null) return false;

            // 2. Megnézzük, hogy a szín bármelyik lehetséges blokk tartományába beleesik-e
            for (int i = 0; i < clues.Length; i++)
            {
                if (clueColors[i].ToArgb() == colorToPlace.ToArgb())
                {
                    // A blokk 'i' potenciális helye a vonalon belül
                    if (posInLine >= leftmost[i] && posInLine <= (rightmost[i] + clues[i] - 1))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // Ez a metódus akadályozza meg a színek felcserélését (pl. 5. oszlop hiba)
        private bool IsPlacementOrderValid(int lineIdx, bool isRow, int blockIdx, int start, int len)
        {
            int[] clues = isRow ? grid.rowClues[lineIdx] : grid.colClues[lineIdx];

            // 1. A blokk előtt nem maradhat "üresen" olyan kifestett cella, ami nem tartozik az előző blokkokhoz
            for (int p = 0; p < start; p++)
            {
                int r = isRow ? lineIdx : p;
                int c = isRow ? p : lineIdx;
                if (IsFilled(r, c))
                {
                    Color pixelColor = grid.userColorRGB[r, c];
                    bool canBePreviousBlock = false;
                    for (int prev = 0; prev < blockIdx; prev++)
                    {
                        if (GetClueColor(lineIdx, isRow, prev).ToArgb() == pixelColor.ToArgb())
                        {
                            canBePreviousBlock = true;
                            break;
                        }
                    }
                    if (!canBePreviousBlock) return false; // Hibás sorrend: korábban van egy szín, ami csak később jöhetne
                }
            }
            return true;
        }
        // Visszaadja a blokkokat, amelyekhez a cella tartozhat
        private IEnumerable<(bool isRow, int lineIdx, int blockIdx)> GetCandidateBlocks(int r, int c)
        {
            // 1. Gyűjtsük ki, milyen színek engedélyezettek a sorban és az oszlopban
            var allowedColorsInRow = grid.rowClueColors[r].Select(clr => clr.ToArgb()).ToHashSet();
            var allowedColorsInCol = grid.colClueColors[c].Select(clr => clr.ToArgb()).ToHashSet();

            // --- Sorhoz tartozó blokkok vizsgálata ---
            int[] rowCluesLine = grid.rowClues[r];
            int lengthRow = grid.col;
            int[] leftRow = GetLeftmost(r, true, lengthRow, rowCluesLine);
            int[] rightRow = GetRightmost(r, true, lengthRow, rowCluesLine);

            if (leftRow != null && rightRow != null)
            {
                for (int i = 0; i < rowCluesLine.Length; i++)
                {
                    // ÚJ: Megnézzük a blokk színét
                    Color blockColor = grid.rowClueColors[r][i];

                    // CSAK AKKOR jelölt, ha:
                    // - A koordináta stimmel
                    // - ÉS a blokk színe szerepel az oszlop szabályai között is!
                    if (c >= leftRow[i] && c <= rightRow[i] + rowCluesLine[i] - 1)
                    {
                        if (allowedColorsInCol.Contains(blockColor.ToArgb()))
                        {
                            yield return (true, r, i);
                        }
                    }
                }
            }

            // --- Oszlophoz tartozó blokkok vizsgálata ---
            int[] colCluesLine = grid.colClues[c];
            int lengthCol = grid.row;
            int[] leftCol = GetLeftmost(c, false, lengthCol, colCluesLine);
            int[] rightCol = GetRightmost(c, false, lengthCol, colCluesLine);

            if (leftCol != null && rightCol != null)
            {
                for (int i = 0; i < colCluesLine.Length; i++)
                {
                    // ÚJ: Megnézzük a blokk színét
                    Color blockColor = grid.colClueColors[c][i];

                    // CSAK AKKOR jelölt, ha:
                    // - A koordináta stimmel
                    // - ÉS a blokk színe szerepel a sor szabályai között is!
                    if (r >= leftCol[i] && r <= rightCol[i] + colCluesLine[i] - 1)
                    {
                        if (allowedColorsInRow.Contains(blockColor.ToArgb()))
                        {
                            yield return (false, c, i);
                        }
                    }
                }
            }
        }

        private bool IsBoardCompletelyValid()
        {
            for (int r = 0; r < grid.row; r++)
                if (!IsLineValid(r, true)) return false;

            for (int c = 0; c < grid.col; c++)
                if (!IsLineValid(c, false)) return false;

            return true;
        }

        private bool IsLineValid(int index, bool isRow)
        {
            int length = isRow ? grid.col : grid.row;
            int[] clues = isRow ? grid.rowClues[index] : grid.colClues[index];

            List<(Color color, int len)> foundBlocks = new List<(Color color, int len)>();

            int i = 0;
            while (i < length)
            {
                int r = isRow ? index : i;
                int c = isRow ? i : index;

                if (IsFilled(r, c))
                {
                    Color currentColor = grid.userColorRGB[r, c];
                    int start = i;

                    // Összegyűjtjük az összes azonos színű, egymás mellett lévő cellát
                    while (i < length)
                    {
                        int currR = isRow ? index : i;
                        int currC = isRow ? i : index;
                        if (IsFilled(currR, currC) && grid.userColorRGB[currR, currC].ToArgb() == currentColor.ToArgb())
                            i++;
                        else
                            break;
                    }

                    foundBlocks.Add((currentColor, i - start));
                }
                else
                {
                    i++;
                }
            }

            // 1. Ellenőrzés: A blokkok száma egyezik-e?
            if (foundBlocks.Count != clues.Length)
                return false;

            // 2. Ellenőrzés: Minden blokk hossza és színe pontosan egyezik-e?
            for (int b = 0; b < foundBlocks.Count; b++)
            {
                if (foundBlocks[b].len != clues[b])
                    return false;

                if (foundBlocks[b].color.ToArgb() != GetClueColor(index, isRow, b).ToArgb())
                    return false;
            }

            return true;
        }
        private async Task<bool> AnalyzeLine(int index, bool isRow)
        {
            int length = isRow ? grid.col : grid.row;
            int[] clues = isRow ? grid.rowClues[index] : grid.colClues[index];
            if (clues == null || clues.Length == 0) return false;

            // --- 0. PRIORITÁS: SZÍN-METSZET ALAPÚ SZŰRÉS ---
            for (int p = 0; p < length; p++)
            {
                int r = isRow ? index : p;
                int c = isRow ? p : index;

                if (IsX(r, c) || IsFilled(r, c)) continue;

                // Javítás: Biztonságosabb színlekérés a metszethez
                var rowColorSet = grid.rowClues[r].Select((_, idx) => GetClueColor(r, true, idx).ToArgb()).ToHashSet();
                var colColorSet = grid.colClues[c].Select((_, idx) => GetClueColor(c, false, idx).ToArgb()).ToHashSet();

                rowColorSet.IntersectWith(colColorSet);
                if (rowColorSet.Count == 0)
                {
                    if (await SetX(r, c, "Szín-összeférhetetlenség (sor/oszlop metszet üres)")) return true;
                }
            }

            // --- 1. PRIORITÁS: TISZTÍTÁS ---
            if (await HandleLogicCleanup(index, isRow, length, clues, clues.Sum())) return true;

            // Határok újraszámítása
            int[] leftmost = GetLeftmost(index, isRow, length, clues);
            int[] rightmost = GetRightmost(index, isRow, length, clues);

            if (leftmost == null || rightmost == null) return false;

            // --- 2. SZIGORÚ SZÍN-OVERLAP ---
            for (int p = 0; p < length; p++)
            {
                int r = isRow ? index : p;
                int c = isRow ? p : index;
                if (IsX(r, c) || IsFilled(r, c)) continue;

                HashSet<int> potentialBlocks = new HashSet<int>();
                for (int i = 0; i < clues.Length; i++)
                {
                    if (p >= rightmost[i] && p <= leftmost[i] + clues[i] - 1)
                    {
                        potentialBlocks.Add(i);
                    }
                }

                if (potentialBlocks.Count > 0)
                {
                    Color firstColor = GetClueColor(index, isRow, potentialBlocks.First());
                    bool allSame = potentialBlocks.All(idx => GetClueColor(index, isRow, idx).ToArgb() == firstColor.ToArgb());

                    if (allSame)
                    {
                        if (await SetCell(r, c, firstColor, "Overlap (Átfedés)")) return true;
                    }
                }
            }

            // --- 3. REACHABILITY ---
            for (int p = 0; p < length; p++)
            {
                int r = isRow ? index : p;
                int c = isRow ? p : index;
                if (IsX(r, c) || IsFilled(r, c)) continue;

                bool canAnyBlockReach = false;
                for (int i = 0; i < clues.Length; i++)
                {
                    if (p >= leftmost[i] && p <= rightmost[i] + clues[i] - 1)
                    {
                        canAnyBlockReach = true;
                        break;
                    }
                }

                if (!canAnyBlockReach)
                {
                    if (await SetX(r, c, "Egyik blokk sem érhet ide")) return true;
                }
            }

            // --- 4. SPECIÁLIS LOGIKÁK ---
            if (await ConnectionLogic(index, isRow, length, clues, leftmost, rightmost)) return true;
            if (await ExtendAnchors(index, isRow, length, clues, leftmost, rightmost)) return true;
            if (await AutoCloseBlocks(index, isRow, length, clues)) return true;

            // --- 5. HOLTPONT FELOLDÁS (Shakedown) ---
            for (int p = 0; p < length; p++)
            {
                int r = isRow ? index : p;
                int c = isRow ? p : index;
                if (IsFilled(r, c) || IsX(r, c)) continue;

                // Ideiglenes X-jelölés a teszteléshez
                grid.userXMark[r, c] = true;
                int[] testLeft = GetLeftmost(index, isRow, length, clues);
                grid.userXMark[r, c] = false;

                if (testLeft == null)
                {
                    // Megkeressük az összes blokkot, ami elméletileg lefedheti ezt a pontot
                    var possibleColors = new HashSet<int>();
                    for (int i = 0; i < clues.Length; i++)
                    {
                        if (p >= leftmost[i] && p <= rightmost[i] + clues[i] - 1)
                        {
                            possibleColors.Add(GetClueColor(index, isRow, i).ToArgb());
                        }
                    }

                    if (!grid.isColor) // fekete-fehér
                    {
                        // csak akkor próbáljuk kitölteni, ha üres a cella
                        if (!IsFilled(r, c))
                        {
                            if (await SetCellSafe(r, c, Color.Black)) return true;
                        }
                    }
                    else
                    {
                        if (possibleColors.Count == 1)
                        {
                            Color targetColor = Color.FromArgb(possibleColors.First());
                            if (await SetCell(r, c, targetColor, "Ellentmondás alapú kitöltés")) return true;
                        }
                    }
                }
            }

            return false;
        }

        private async Task<bool> SetCellSafe(int r, int c, Color colorToSet)
        {
            if (IsFilled(r, c)) return false; // Ha már kitöltve, ne térj true-val
            grid.userColorRGB[r, c] = colorToSet;
            render.SetCellColor(r, c, grid.gridButtons[r, c], colorToSet);
            render.UpdatePreview(r, c);
            form.Refresh();
            await Task.Delay(50);
            return true; // true csak akkor, ha ténylegesen kitöltöttünk
        }

        private async Task<bool> ConnectionLogic(int index, bool isRow, int length, int[] clues, int[] leftmost, int[] rightmost)
        {
            for (int bIdx = 0; bIdx < clues.Length; bIdx++)
            {
                int bLen = clues[bIdx];
                Color bColor = GetClueColor(index, isRow, bIdx);
                int rangeStart = leftmost[bIdx];
                int rangeEnd = rightmost[bIdx] + bLen - 1;

                int firstFound = -1, lastFound = -1;

                for (int p = rangeStart; p <= rangeEnd; p++)
                {
                    int r = isRow ? index : p;
                    int c = isRow ? p : index;

                    if (IsFilled(r, c) && grid.userColorRGB[r, c].ToArgb() == bColor.ToArgb())
                    {
                        // Csak akkor horgony, ha más színű blokk nem érhet ide
                        bool belongsToThis = true;
                        for (int j = 0; j < clues.Length; j++)
                        {
                            if (j == bIdx) continue;
                            if (p >= leftmost[j] && p <= rightmost[j] + clues[j] - 1) { belongsToThis = false; break; }
                        }

                        if (belongsToThis)
                        {
                            if (firstFound == -1) firstFound = p;
                            lastFound = p;
                        }
                    }
                }

                if (firstFound != -1 && lastFound != -1 && lastFound - firstFound > 0)
                {
                    for (int k = firstFound + 1; k < lastFound; k++)
                    {
                        int rG = isRow ? index : k; int cG = isRow ? k : index;
                        // CSAK akkor kötjük össze, ha üres! (Nem X és nem más szín)
                        if (!IsFilled(rG, cG) && !IsX(rG, cG))
                        {
                            if (await SetCell(rG, cG, bColor, "Összekötés")) return true;
                        }
                    }
                }
            }
            return false;
        }
        private int[] GetLeftmost(int index, bool isRow, int length, int[] clues)
        {
            int[] leftmost = new int[clues.Length];
            int currentPos = 0;

            for (int i = 0; i < clues.Length; i++)
            {
                Color blockColor = GetClueColor(index, isRow, i);
                bool found = false;

                while (currentPos + clues[i] <= length)
                {
                    if (CanPlaceBlock(index, isRow, currentPos, clues[i], blockColor))
                    {
                        int minGap = 0;
                        if (i > 0)
                        {
                            Color prevColor = GetClueColor(index, isRow, i - 1);
                            // Fekete-fehérnél ez mindig 1 lesz, színesnél 0 vagy 1
                            minGap = (prevColor.ToArgb() == blockColor.ToArgb()) ? 1 : 0;
                        }

                        int prevEnd = (i == 0) ? 0 : leftmost[i - 1] + clues[i - 1] + minGap;

                        if (currentPos >= prevEnd)
                        {
                            // Csak az előző blokk vége és a mostani kezdete között nézzük az üres helyet
                            bool skippedRequired = false;
                            int searchStart = (i == 0) ? 0 : leftmost[i - 1] + clues[i - 1];
                            for (int p = searchStart; p < currentPos; p++)
                            {
                                if (IsFilled(isRow ? index : p, isRow ? p : index)) { skippedRequired = true; break; }
                            }

                            if (!skippedRequired)
                            {
                                leftmost[i] = currentPos;
                                currentPos += clues[i];
                                // Ha a következő azonos színű, egyből ugrunk egyet az X-nek
                                if (i < clues.Length - 1 && GetClueColor(index, isRow, i + 1).ToArgb() == blockColor.ToArgb())
                                    currentPos++;

                                found = true;
                                break;
                            }
                        }
                    }
                    currentPos++;
                }
                if (!found) return null;
            }
            return leftmost;
        }

        private int[] GetRightmost(int index, bool isRow, int length, int[] clues)
        {
            int[] rightmost = new int[clues.Length];
            int currentPos = length;

            for (int i = clues.Length - 1; i >= 0; i--)
            {
                Color blockColor = GetClueColor(index, isRow, i);
                bool found = false;

                while (currentPos - clues[i] >= 0)
                {
                    int testStart = currentPos - clues[i];
                    if (CanPlaceBlock(index, isRow, testStart, clues[i], blockColor))
                    {
                        int minGap = 0;
                        if (i < clues.Length - 1)
                        {
                            Color nextColor = GetClueColor(index, isRow, i + 1);
                            minGap = (nextColor.ToArgb() == blockColor.ToArgb()) ? 1 : 0;
                        }

                        int nextLimit = (i == clues.Length - 1) ? length : rightmost[i + 1] - minGap;

                        if (testStart + clues[i] <= nextLimit)
                        {
                            bool skippedRequired = false;
                            int searchEnd = (i == clues.Length - 1) ? length : rightmost[i + 1];
                            for (int p = testStart + clues[i]; p < searchEnd; p++)
                            {
                                if (IsFilled(isRow ? index : p, isRow ? p : index)) { skippedRequired = true; break; }
                            }

                            if (!skippedRequired)
                            {
                                rightmost[i] = testStart;
                                currentPos = testStart;
                                // Ha az előző azonos színű, hagyunk helyet az X-nek
                                if (i > 0 && GetClueColor(index, isRow, i - 1).ToArgb() == blockColor.ToArgb())
                                    currentPos--;

                                found = true;
                                break;
                            }
                        }
                    }
                    currentPos--;
                }
                if (!found) return null;
            }
            return rightmost;
        }
        // ÚJ: Horgony kiterjesztése - ha egy kifestett cella csak egyféle blokkhoz tartozhat
        private async Task<bool> ExtendAnchors(int index, bool isRow, int length, int[] clues, int[] leftmost, int[] rightmost)
        {
            for (int p = 0; p < length; p++)
            {
                int r = isRow ? index : p;
                int c = isRow ? p : index;

                if (IsFilled(r, c))
                {
                    Color pixelColor = grid.userColorRGB[r, c];
                    int blockIdx = -1;
                    int count = 0;

                    for (int i = 0; i < clues.Length; i++)
                    {
                        if (GetClueColor(index, isRow, i).ToArgb() == pixelColor.ToArgb() &&
                            p >= leftmost[i] && p <= rightmost[i] + clues[i] - 1)
                        {
                            blockIdx = i;
                            count++;
                        }
                    }

                    // Ha ez a kifestett pont fixen csak az i. blokkhoz tartozhat
                    if (count == 1)
                    {
                        int i = blockIdx;
                        int overlapStart = Math.Max(rightmost[i], p - clues[i] + 1);
                        int overlapEnd = Math.Min(leftmost[i] + clues[i] - 1, p + clues[i] - 1);

                        for (int fillP = overlapStart; fillP <= overlapEnd; fillP++)
                        {
                            int fr = isRow ? index : fillP;
                            int fc = isRow ? fillP : index;

                            if (!IsFilled(fr, fc) && !IsX(fr, fc))
                            {
                                // JAVÍTÁS: Ellenőrizzük, hogy a keresztirányú szabály ismeri-e ezt a színt!
                                var crossClues = isRow ? grid.colClues[fc] : grid.rowClues[fr];
                                bool validColorInCross = false;
                                for (int k = 0; k < crossClues.Length; k++)
                                {
                                    if (GetClueColor(isRow ? -1 : fr, !isRow, k).ToArgb() == pixelColor.ToArgb())
                                    {
                                        validColorInCross = true; break;
                                    }
                                }

                                if (validColorInCross)
                                {
                                    if (await SetCell(fr, fc, pixelColor, $"Horgony ({i}. blokk) kiterjesztése")) return true;
                                }
                            }
                        }
                    }
                }
            }
            return false;
        }

        private bool CanPlaceBlock(int lineIdx, bool isRow, int start, int len, Color color)
        {
            int limit = isRow ? grid.col : grid.row;
            if (start < 0 || start + len > limit) return false;

            int targetArgb = color.ToArgb();

            // 1. Ütközés: X vagy MÁS szín nem lehet ott
            for (int i = start; i < start + len; i++)
            {
                int r = isRow ? lineIdx : i;
                int c = isRow ? i : lineIdx;
                if (IsX(r, c)) return false;
                if (IsFilled(r, c) && grid.userColorRGB[r, c].ToArgb() != targetArgb) return false;
            }

            // 2. Színszabály: Csak akkor tilos az érintkezés, ha a szomszéd UGYANOLYAN színű
            // Előtte
            if (start > 0)
            {
                int pr = isRow ? lineIdx : start - 1;
                int pc = isRow ? start - 1 : lineIdx;
                if (IsFilled(pr, pc) && grid.userColorRGB[pr, pc].ToArgb() == targetArgb) return false;
            }
            // Utána
            if (start + len < limit)
            {
                int nr = isRow ? lineIdx : start + len;
                int nc = isRow ? start + len : lineIdx;
                if (IsFilled(nr, nc) && grid.userColorRGB[nr, nc].ToArgb() == targetArgb) return false;
            }

            return true;
        }

        // ÚJ: Ezt a metódust add hozzá, hogy ne a megoldásból puskázzon!
        private Color GetClueColor(int lineIdx, bool isRow, int blockIdx)
        {
            // Itt a te saját adatszerkezetedet kell használnod, 
            // ahol a szabályok színeit tárolod (pl. rowCluesColors[lineIdx][blockIdx])
            return isRow ? grid.rowClueColors[lineIdx][blockIdx] : grid.colClueColors[lineIdx][blockIdx];
        }

        private async Task<bool> AutoCloseBlocks(int index, bool isRow, int length, int[] clues)
        {
            for (int i = 0; i < length; i++)
            {
                int r = isRow ? index : i;
                int c = isRow ? i : index;

                if (IsFilled(r, c))
                {
                    int start = i;
                    Color clusterColor = grid.userColorRGB[r, c];
                    while (i + 1 < length && IsFilled(isRow ? index : i + 1, isRow ? i + 1 : index) &&
                           grid.userColorRGB[isRow ? index : i + 1, isRow ? i + 1 : index].ToArgb() == clusterColor.ToArgb())
                    {
                        i++;
                    }
                    int end = i;
                    int currentLen = end - start + 1;

                    int clueIdx = FindClueIdxForBlock(index, isRow, start, end, clusterColor);

                    // Ha találtunk hozzá passzoló szabályt és a hossza pont annyi
                    if (clueIdx != -1 && clues[clueIdx] == currentLen)
                    {
                        // BAL OLDAL lezárása
                        if (start > 0)
                        {
                            int pr = isRow ? index : start - 1;
                            int pc = isRow ? start - 1 : index;
                            if (!IsX(pr, pc) && !IsFilled(pr, pc))
                            {
                                // CSAK akkor zárunk le X-szel, ha:
                                // 1. Van előző blokk ÉS az azonos színű (ekkor kötelező a szünet)
                                bool mustHaveX = false;
                                if (clueIdx > 0 && GetClueColor(index, isRow, clueIdx - 1).ToArgb() == clusterColor.ToArgb())
                                    mustHaveX = true;

                                // 2. Vagy ha a leftmost/rightmost alapján ott már semmi nem lehet (opcionális, de biztonságos)
                                if (mustHaveX)
                                {
                                    if (await SetX(pr, pc, "Kész blokk kényszerített lezárása (azonos szín miatt)")) return true;
                                }
                            }
                        }

                        // JOBB OLDAL lezárása
                        if (end < length - 1)
                        {
                            int nr = isRow ? index : end + 1;
                            int nc = isRow ? end + 1 : index;
                            if (!IsX(nr, nc) && !IsFilled(nr, nc))
                            {
                                bool mustHaveX = false;
                                if (clueIdx < clues.Length - 1 && GetClueColor(index, isRow, clueIdx + 1).ToArgb() == clusterColor.ToArgb())
                                    mustHaveX = true;

                                if (mustHaveX)
                                {
                                    if (await SetX(nr, nc, "Kész blokk kényszerített lezárása (azonos szín miatt)")) return true;
                                }
                            }
                        }
                    }
                }
            }
            return false;
        }

        private int FindClueIdxForBlock(int lineIdx, bool isRow, int start, int end, Color color)
        {
            int length = isRow ? grid.col : grid.row;
            int[] clues = isRow ? grid.rowClues[lineIdx] : grid.colClues[lineIdx];

            // Először kérjük le a határokat
            int[] leftmost = GetLeftmost(lineIdx, isRow, length, clues);
            int[] rightmost = GetRightmost(lineIdx, isRow, length, clues);

            if (leftmost == null || rightmost == null) return -1;

            int currentLen = end - start + 1;
            int foundIdx = -1;
            int possibleCount = 0;
            int targetArgb = color.ToArgb();

            for (int i = 0; i < clues.Length; i++)
            {
                // 1. Alapfeltételek: Szín és hossz egyezik?
                if (clues[i] == currentLen && GetClueColor(lineIdx, isRow, i).ToArgb() == targetArgb)
                {
                    // 2. Tartomány ellenőrzés: 
                    // A kifestett blokk eleje nem lehet előbb, mint a leftmost, 
                    // és a vége nem lehet később, mint a rightmost tartomány vége.
                    if (start >= leftmost[i] && end <= (rightmost[i] + clues[i] - 1))
                    {
                        // 3. Sorrendi kényszer (OPCIONÁLIS de erős): 
                        // Ha van előző/következő blokk már kifestve, ellenőrizhetnénk a sorrendet is,
                        // de a leftmost/rightmost ezt alapból jól kezeli.

                        foundIdx = i;
                        possibleCount++;
                    }
                }
            }

            // Csak akkor adjuk vissza, ha 100%-ig biztos (csak egy clue-ra illik rá)
            return (possibleCount == 1) ? foundIdx : -1;
        }

        // Kezeli a túl szűk helyeket és a kész sorok X-elését
        private async Task<bool> HandleLogicCleanup(int index, bool isRow, int length, int[] clues, int totalBlocks)
        {
            int currentFilled = 0;
            for (int i = 0; i < length; i++)
            {
                if (IsFilled(isRow ? index : i, isRow ? i : index)) currentFilled++;
            }

            // Ha a kifestett cellák száma eléri a szabályok összegét
            if (currentFilled == totalBlocks)
            {
                for (int i = 0; i < length; i++)
                {
                    int r = isRow ? index : i;
                    int c = isRow ? i : index;
                    if (!IsFilled(r, c) && !IsX(r, c))
                    {
                        // Ha találtunk üres helyet, amit X-elni kell
                        if (await SetX(r, c, "Sor kész (Darabszám stimmel)")) return true;
                    }
                }
            }
            return false;
        }

        private bool IsFilled(int r, int c) => grid.isColor ? grid.userColorRGB[r, c] != Color.White : grid.gridButtons[r, c].BackColor == Color.Black;
        private bool IsX(int r, int c) => grid.userXMark[r, c];

        private async Task<bool> SetCell(int r, int c, Color colorToSet, string reason)
        {
            // Ha már X van ott, vagy MÁR UGYANEZ a szín, akkor nincs változás!
            if (IsX(r, c)) return false;
            if (IsFilled(r, c) && grid.userColorRGB[r, c].ToArgb() == colorToSet.ToArgb()) return false;

            // Vizuális kiemelés a MessageBox előtt (hogy lássuk, melyik celláról beszél)
            grid.gridButtons[r, c].FlatAppearance.BorderColor = Color.Yellow;
            grid.gridButtons[r, c].FlatAppearance.BorderSize = 3;

            // Üzenet a felhasználónak
            MessageBox.Show($"Lépés: [{r + 1}. sor, {c + 1} oszlop] kifestése.\n\nIndok: {reason}",
                            "AI Logika", MessageBoxButtons.OK, MessageBoxIcon.Information);

            // Keret visszaállítása és tényleges módosítás
            grid.gridButtons[r, c].FlatAppearance.BorderSize = 1;
            grid.gridButtons[r, c].FlatAppearance.BorderColor = Color.Black;

            render.SetCellColor(r, c, grid.gridButtons[r, c], colorToSet);
            grid.userColorRGB[r, c] = colorToSet;

            render.UpdatePreview(r, c);
            form.Refresh();

            await Task.Delay(100);
            return true;
        }

        private async Task<bool> SetX(int r, int c, string reason)
        {
            if (IsFilled(r, c) || IsX(r, c)) return false;

            // Vizuális kiemelés
            grid.gridButtons[r, c].FlatAppearance.BorderColor = Color.Red;
            grid.gridButtons[r, c].FlatAppearance.BorderSize = 3;

            MessageBox.Show($"Lépés: [{r + 1}. sor, {c + 1} oszlop] helyre X kerül.\n\nIndok: {reason}",
                   "AI Logika", MessageBoxButtons.OK, MessageBoxIcon.Information);

            grid.gridButtons[r, c].FlatAppearance.BorderSize = 1;
            grid.gridButtons[r, c].FlatAppearance.BorderColor = Color.Black;

            render.SetCellX(r, c, grid.gridButtons[r, c]);
            grid.userXMark[r, c] = true;

            render.UpdatePreview(r, c);
            form.Refresh();

            await Task.Delay(50);
            return true;
        }
    }
}
