using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using FinalBet.Framework;

namespace FinalBet.ViewModel
{
    public class RedGreenViewModel:ViewModelBase
    {
        public ICommand DrawCommand { get; private set; }

        private void Draw(object prm)
        {
            var canvas = (DrawingCanvas) prm;

            canvas.Clear();

            var shift = 4;
            for (int i = 0; i < 50; i++)
            {
                for (int j = 0; j < 30; j++)
                {
                    var visual = new DrawingVisual();
                    canvas.DrawSquare(
                        visual, 
                        new Point(5+ i * (canvas.CellWidth + shift) , 5+ j * (canvas.CellHeight + shift)), 
                        canvas.RedBrush,
                        j.ToString());
                    canvas.AddVisual(visual);
                }
            }
        }


        public void OnTableMouseClick(object sender, MouseEventArgs eventArgs)
        {
            var canvas = (DrawingCanvas)sender;
            canvas.TrySelectCell(eventArgs);
        }

        public RedGreenViewModel()
        {
            base.DisplayName = "Красно-зеленая";

            DrawCommand = new RelayCommand(Draw);
        }
    }
}
