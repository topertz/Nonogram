using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Remoting.Lifetime;
using System.Windows.Forms;

namespace Grafilogika_alkalmazas_keszitese
{
    public class UndoRedoManager
    {
        public Stack<Tuple<Color[,], int, int, bool>> undoStack = new Stack<Tuple<Color[,], int, int, bool>>();
        public Stack<Tuple<Color[,], int, int, bool>> redoStack = new Stack<Tuple<Color[,], int, int, bool>>();
        public bool lastActionWasX = false;
        public int undoClicks = 0;
        public int redoClicks = 0;
        public int maxUndoClicks = 0;
        public int maxRedoClicks = 0;
        private Nonogram form;
        private NonogramGrid grid;

        public UndoRedoManager(Nonogram f, NonogramGrid g)
        {
            this.form = f;
            this.grid = g;
        }
        public void SetGrid(NonogramGrid grid)
        {
            this.grid = grid;
        }
        public void BtnUndo_Click(object sender, EventArgs e)
        {
            Undo();
        }

        public void BtnRedo_Click(object sender, EventArgs e)
        {
            Redo();
        }
        public void SaveState(bool wasX = false)
        {
            if (grid.gridButtons == null) return;

            Color[,] clone = CloneGrid();
            undoStack.Push(Tuple.Create(clone, grid.wrongCellClicks, grid.wrongColorClicks, wasX));
            redoStack.Clear();
        }

        public void Undo()
        {
            bool isEasy = form.cmbDifficulty.SelectedIndex == 0;
            if (undoStack.Count == 0 || (!isEasy && undoClicks >= maxUndoClicks))
            {
                MessageBox.Show("Nincs korábbi állapot!", "Undo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Aktuális állapot mentése redo stackbe
            Color[,] currentClone = CloneGrid();
            bool wasXCurrent = false;
            if (undoStack.Count > 0)
                wasXCurrent = undoStack.Peek().Item4;
            redoStack.Push(Tuple.Create(currentClone, grid.wrongCellClicks, grid.wrongColorClicks, wasXCurrent));

            // Előző állapot visszaállítása
            var state = undoStack.Pop();
            RestoreState(state.Item1);  // rács
            grid.wrongCellClicks = state.Item2;
            grid.wrongColorClicks = state.Item3;
            bool wasX = state.Item4;

            // Labelek frissítése
            form.lblWrongCellClicks.Text = $"Helytelen kattintások száma: {grid.wrongCellClicks} (max: {grid.maxWrongCellClicks})";
            form.lblWrongColorClicks.Text = $"Helytelen színek száma: {grid.wrongColorClicks} (max: {grid.maxWrongColorClicks})";

            lastActionWasX = wasX;
            if (!isEasy && !wasX)
            {
                if (undoClicks < maxUndoClicks)
                    undoClicks++;
                else
                    MessageBox.Show("Elérted a maximális visszavonást!");
            }
            UpdateUndoRedoLabels();
        }

        public void Redo()
        {
            bool isEasy = form.cmbDifficulty.SelectedIndex == 0;
            if (redoStack.Count == 0 || (!isEasy && redoClicks >= maxRedoClicks))
            {
                MessageBox.Show("Nincs későbbi állapot!", "Redo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Aktuális állapot mentése undo stackbe
            Color[,] currentClone = CloneGrid();
            bool wasXCurrent = false;
            if (redoStack.Count > 0)
                wasXCurrent = redoStack.Peek().Item4;
            undoStack.Push(Tuple.Create(currentClone, grid.wrongCellClicks, grid.wrongColorClicks, wasXCurrent));

            // Következő állapot visszaállítása
            var state = redoStack.Pop();
            RestoreState(state.Item1);  // rács
            grid.wrongCellClicks = state.Item2;
            grid.wrongColorClicks = state.Item3;
            bool wasX = state.Item4;

            // Labelek frissítése
            form.lblWrongCellClicks.Text = $"Helytelen kattintások száma: {grid.wrongCellClicks} (max: {grid.maxWrongCellClicks})";
            form.lblWrongColorClicks.Text = $"Helytelen színek száma: {grid.wrongColorClicks} (max: {grid.maxWrongColorClicks})";

            lastActionWasX = wasX;

            if (!isEasy && !wasX)
            {
                if (redoClicks < maxRedoClicks)
                    redoClicks++;
                else
                    MessageBox.Show("Elérted a maximális visszavonást!");
            }
            UpdateUndoRedoLabels();
        }

        public void UpdateUndoRedoLabels()
        {
            form.lblUndoCount.Text = $"Visszavonások száma: {undoClicks} (max: {maxUndoClicks})";
            form.lblRedoCount.Text = $"Előrelépések száma: {redoClicks} (max: {maxRedoClicks})";
        }

        private Color[,] CloneGrid()
        {
            Color[,] clone = new Color[grid.row, grid.col];
            Color xColorMarker = Color.FromArgb(255, 255, 254);

            for (int i = 0; i < grid.row; i++)
            {
                for (int j = 0; j < grid.col; j++)
                {
                    // Ha van X, akkor a jelölő színt mentjük, egyébként a rendes színt
                    clone[i, j] = grid.userXMark[i, j] ? xColorMarker : grid.userColorRGB[i, j];
                }
            }
            return clone;
        }

        private void RestoreState(Color[,] state)
        {
            Color xColorMarker = Color.FromArgb(255, 255, 254);

            for (int i = 0; i < grid.row; i++)
            {
                for (int j = 0; j < grid.col; j++)
                {
                    if (grid.isHintFixed[i, j])
                        continue;
                    Color storedColor = state[i, j];

                    if (storedColor.ToArgb() == xColorMarker.ToArgb())
                    {
                        // Ez egy X jelölés volt
                        grid.userXMark[i, j] = true;
                        grid.userColorRGB[i, j] = Color.White;
                        grid.gridButtons[i, j].BackColor = Color.White;

                        // Megjelenítjük az X jelet vizuálisan
                        float fontSize = grid.userCellSize * 0.3f;
                        grid.gridButtons[i, j].Font = new Font("Arial", fontSize, FontStyle.Bold);
                        grid.gridButtons[i, j].ForeColor = Color.Gray;
                        grid.gridButtons[i, j].Text = "X";
                    }
                    else
                    {
                        // Ez egy sima szín volt
                        grid.userXMark[i, j] = false;
                        grid.userColorRGB[i, j] = storedColor;
                        grid.gridButtons[i, j].BackColor = storedColor;
                        grid.gridButtons[i, j].Text = "";
                    }
                }
            }
            form.render.UpdatePreview();
        }

        public void ClearHistory()
        {
            undoStack.Clear();
            redoStack.Clear();
        }
    }
}