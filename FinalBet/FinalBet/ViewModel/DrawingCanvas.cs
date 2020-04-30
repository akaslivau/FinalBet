using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FinalBet.ViewModel
{
    public class DrawingCanvas:Canvas
    {
        #region Variables
        private ObservableCollection<Visual> _visuals = new ObservableCollection<Visual>();

        private DrawingVisual selectedVisual;
        private DrawingVisual bufferVisual = null;
        #endregion

        #region Methods
        protected override int VisualChildrenCount => _visuals.Count;

        protected override Visual GetVisualChild(int index)
        {
            return _visuals[index];
        }

        public void AddVisual(Visual visual)
        {
            _visuals.Add(visual);

            base.AddVisualChild(visual);
            base.AddLogicalChild(visual);
        }

        public void DeleteVisual(Visual visual)
        {
            _visuals.Remove(visual);

            base.RemoveVisualChild(visual);
            base.RemoveLogicalChild(visual);
        }

        public void Clear()
        {
            foreach (var visual in _visuals.ToList())
            {
                DeleteVisual(visual);
            }
        }

        public DrawingVisual GetVisual(Point point)
        {
            var hitResult = VisualTreeHelper.HitTest(this, point);
            return hitResult.VisualHit as DrawingVisual;
        }


        public void TrySelectCell(MouseEventArgs eventArgs)
        {
            var visual = GetVisual(eventArgs.GetPosition(this));
            if (visual != null)
            {
                var topLeftCorner = new Point(
                    visual.ContentBounds.TopLeft.X + CellPen.Thickness / 2,
                    visual.ContentBounds.TopLeft.Y + CellPen.Thickness / 2
                );

                var newVsl = new DrawingVisual();
                DrawSelectedSquare(newVsl, topLeftCorner);
                AddVisual(newVsl);

                if (selectedVisual != null && selectedVisual != visual)
                {
                    DeleteVisual(selectedVisual);
                    selectedVisual = null;
                }

                selectedVisual = newVsl;
            }
            else
            {
                if (selectedVisual == null) return;
                DeleteVisual(selectedVisual);
                selectedVisual = null;
            }
        }

        private void DrawSquare(DrawingVisual visual, Point topLeftCorner, Brush brush, Pen pen, string txt)
        {
            using (var dc = visual.RenderOpen())
            {
                var inText = new FormattedText(txt,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    TxtTypeface,
                    FontSize,
                    ForegroundBrush);

                var txtPoint = new Point(topLeftCorner.X + 0.5*(CellSize.Width - inText.Width), 
                    topLeftCorner.Y + 0.5 * (CellSize.Height - inText.Height));

                dc.DrawRectangle(brush, pen, new Rect(topLeftCorner, CellSize));
                dc.DrawText(inText, txtPoint);
            }
        }

        public void DrawCellSquare(DrawingVisual visual, Point topLeftCorner, string txt)
        {
            DrawSquare(visual, topLeftCorner, RedBrush, CellPen, txt);
        }

        public void DrawSelectedSquare(DrawingVisual visual, Point topLeftCorner)
        {
            DrawSquare(visual, topLeftCorner, Brushes.Transparent, SelectedPen, "");
        }

        #endregion

        #region TextProperties
        private Brush ForegroundBrush = Brushes.Black;
        private double FontSize = 18;

        private Typeface TxtTypeface = new Typeface(
            new FontFamily("Arial"),
            FontStyles.Normal, 
            FontWeights.Bold, 
            FontStretch.FromOpenTypeStretch(1));
        #endregion

        #region CellProperties

        public Brush SelectedCellBrush => Brushes.White;
        public Brush RedBrush => Brushes.IndianRed;

        private Pen CellPen = new Pen(Brushes.BlueViolet, 2);
        private Pen SelectedPen = new Pen(Brushes.Yellow, 4);

        private Size CellSize = new Size(30, 30);

        public double CellWidth => CellSize.Width;
        public double CellHeight => CellSize.Height;


        #endregion



    }

}
