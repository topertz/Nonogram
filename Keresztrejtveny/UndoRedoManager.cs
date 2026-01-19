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

        public void SaveGrid(string filename, int scale = 50)
        {
            if (form.gridButtons == null) return;

            Bitmap bmp = new Bitmap(form.col * scale, form.row * scale);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                for (int i = 0; i < form.row; i++)
                    for (int j = 0; j < form.col; j++)
                        using (SolidBrush brush = new SolidBrush(form.userColorRGB[i, j]))
                            g.FillRectangle(brush, j * scale, i * scale, scale, scale);
            }

            bmp.Save(filename, ImageFormat.Png);
        }

        public void LoadGrid(string filename)
        {
            if (form.gridButtons == null) return;

            Bitmap loaded = new Bitmap(filename);
            int cellWidth = loaded.Width / form.col;
            int cellHeight = loaded.Height / form.row;

            SaveState(); // Undo támogatás

            for (int i = 0; i < form.row; i++)
            {
                for (int j = 0; j < form.col; j++)
                {
                    int px = j * cellWidth + cellWidth / 2;
                    int py = i * cellHeight + cellHeight / 2;
                    Color c = loaded.GetPixel(px, py);

                    form.userColorRGB[i, j] = c;
                    form.gridButtons[i, j].BackColor = c;
                }
            }

            form.renderer.UpdatePreview();
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
            for (int i = 0; i < form.row; i++)
                for (int j = 0; j < form.col; j++)
                    clone[i, j] = form.userColorRGB[i, j];
            return clone;
        }

        private void RestoreState(Color[,] state)
        {
            for (int i = 0; i < form.row; i++)
            {
                for (int j = 0; j < form.col; j++)
                {
                    form.userColorRGB[i, j] = state[i, j];
                    form.gridButtons[i, j].BackColor = state[i, j];
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