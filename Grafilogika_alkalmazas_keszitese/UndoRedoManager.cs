using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace Nonogram
{
    public class UndoRedoManager
    {
        private Nonogram form;

        public UndoRedoManager(Nonogram f)
        {
            form = f;
        }

        public void SaveState()
        {
            if (form.gridButtons == null) return;

            form.undoStack.Push(CloneGrid());
            form.redoStack.Clear();
        }

        public void Undo()
        {
            if (form.undoStack.Count == 0)
            {
                MessageBox.Show("Nincs korábbi állapot!", "Undo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            form.redoStack.Push(CloneGrid());
            RestoreState(form.undoStack.Pop());
        }

        public void Redo()
        {
            if (form.redoStack.Count == 0)
            {
                MessageBox.Show("Nincs későbbi állapot!", "Redo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            form.undoStack.Push(CloneGrid());
            RestoreState(form.redoStack.Pop());
        }

        private Color[,] CloneGrid()
        {
            Color[,] clone = new Color[form.row, form.col];
            Color xColorMarker = Color.FromArgb(255, 255, 254); // Speciális jelölő szín

            for (int i = 0; i < form.row; i++)
            {
                for (int j = 0; j < form.col; j++)
                {
                    // Ha van X, akkor a jelölő színt mentjük, egyébként a rendes színt
                    clone[i, j] = form.userXMark[i, j] ? xColorMarker : form.userColorRGB[i, j];
                }
            }
            return clone;
        }

        private void RestoreState(Color[,] state)
        {
            Color xColorMarker = Color.FromArgb(255, 255, 254);

            for (int i = 0; i < form.row; i++)
            {
                for (int j = 0; j < form.col; j++)
                {
                    Color storedColor = state[i, j];

                    if (storedColor.ToArgb() == xColorMarker.ToArgb())
                    {
                        // Ez egy X jelölés volt
                        form.userXMark[i, j] = true;
                        form.userColorRGB[i, j] = Color.White;
                        form.gridButtons[i, j].BackColor = Color.White;

                        // Megjelenítjük az X-et vizuálisan
                        float fontSize = form.userCellSize * 0.3f;
                        form.gridButtons[i, j].Font = new Font("Arial", fontSize, FontStyle.Bold);
                        form.gridButtons[i, j].ForeColor = Color.Gray;
                        form.gridButtons[i, j].Text = "X";
                    }
                    else
                    {
                        // Ez egy sima szín volt
                        form.userXMark[i, j] = false;
                        form.userColorRGB[i, j] = storedColor;
                        form.gridButtons[i, j].BackColor = storedColor;
                        form.gridButtons[i, j].Text = "";
                    }
                }
            }
            form.renderer.UpdatePreview();
        }

        public void ClearHistory()
        {
            form.undoStack.Clear();
            form.redoStack.Clear();
        }
    }
}