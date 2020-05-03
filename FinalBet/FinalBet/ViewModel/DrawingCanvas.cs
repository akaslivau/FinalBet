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
        #region Dependency properties

        static DrawingCanvas()
        {
            CellForegroundBrushProperty = DependencyProperty.Register("CellForegroundBrushProperty", 
                typeof(Brush), 
                typeof(DrawingCanvas),
                new PropertyMetadata(Brushes.Black));

            TeamBrushProperty = DependencyProperty.Register(
                "TeamBrushProperty", typeof(Brush), typeof(DrawingCanvas), new PropertyMetadata(default(Brush)));

            FontSizeProperty = DependencyProperty.Register("FontSizeProperty", 
                typeof(double), 
                typeof(DrawingCanvas), 
                new PropertyMetadata(14.5));

            CellSizeProperty = DependencyProperty.Register("CellSizeProperty", 
                typeof(double), 
                typeof(DrawingCanvas), 
                new FrameworkPropertyMetadata(30D));
        }

        public static DependencyProperty CellForegroundBrushProperty;
        public static readonly DependencyProperty TeamBrushProperty;
        
        public static DependencyProperty FontSizeProperty;
        public static DependencyProperty CellSizeProperty;

        public Brush CellForegroundBrush
        {
            get => (Brush)GetValue(CellForegroundBrushProperty);
            set => SetValue(CellForegroundBrushProperty, value);
        }
        
        public Brush TeamBrush
        {
            get => (Brush) GetValue(TeamBrushProperty);
            set => SetValue(TeamBrushProperty, value);
        }

        public double FontSize
        {
            get => (double) GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        public double CellSize
        {
            get => (double) GetValue(CellSizeProperty);
            set => SetValue(CellSizeProperty, value);
        }

        #endregion

        #region Variables
        public int MatchId { get; private set; }

        public double TeamCellWidth { get; set; }

        private readonly ObservableCollection<VisualWithTag> _visuals = new ObservableCollection<VisualWithTag>();

        private VisualWithTag _selectedVisual;
        #endregion
        
        #region Brushes, Pens and other shit
        private readonly Typeface _txtTypeface = new Typeface(
            new FontFamily("Arial"),
            FontStyles.Normal,
            FontWeights.Bold,
            FontStretch.FromOpenTypeStretch(1));

        private Pen CellPen = new Pen(Brushes.White, 2);
        private Pen SelectedPen = new Pen(Brushes.DarkOrchid, 3);
        private Pen TeamPen = new Pen(Brushes.PeachPuff, 2);
        #endregion

        #region Methods
        protected override int VisualChildrenCount => _visuals.Count;

        protected override Visual GetVisualChild(int index)
        {
            return _visuals[index];
        }

        public void AddVisual(VisualWithTag visual)
        {
            _visuals.Add(visual);

            base.AddVisualChild(visual);
            base.AddLogicalChild(visual);
        }

        public void DeleteVisual(VisualWithTag visual)
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

        public VisualWithTag GetVisual(Point point)
        {
            var hitResult = VisualTreeHelper.HitTest(this, point);
            return hitResult.VisualHit as VisualWithTag;
        }
        
        public void TrySelectCell(MouseEventArgs eventArgs)
        {
            if (eventArgs.GetPosition(this).X < TeamCellWidth + CellSize/4) return;
            
            var visual = GetVisual(eventArgs.GetPosition(this));
            if (visual != null)
            {
                var topLeftCorner = new Point(
                    visual.ContentBounds.TopLeft.X + CellPen.Thickness / 2,
                    visual.ContentBounds.TopLeft.Y + CellPen.Thickness / 2
                );

                var newVsl = new VisualWithTag();
                DrawSelectedSquare(newVsl, topLeftCorner);
                AddVisual(newVsl);

                if (_selectedVisual != null)
                {
                    DeleteVisual(_selectedVisual);
                    _selectedVisual = null;
                }

                _selectedVisual = newVsl;

                OnSelectedCellChanged();
                MatchId = (int?) visual.Tag ?? -1;
            }
            else
            {
                if (_selectedVisual == null) return;
                DeleteVisual(_selectedVisual);
                _selectedVisual = null;
            }
        }

        private void DrawSquare(VisualWithTag visual, Point topLeftCorner, Brush brush, Pen pen, string txt)
        {
            using (var dc = visual.RenderOpen())
            {
                var inText = new FormattedText(txt,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    _txtTypeface,
                    FontSize,
                    CellForegroundBrush);

                var txtPoint = new Point(topLeftCorner.X + 0.5*(CellSize - inText.Width), 
                    topLeftCorner.Y + 0.5 * (CellSize - inText.Height));

                dc.DrawRectangle(brush, pen, new Rect(topLeftCorner, new Size(CellSize, CellSize)));
                dc.DrawText(inText, txtPoint);
            }
        }

        public void DrawCellSquare(VisualWithTag visual, Point topLeftCorner, Brush brush, string txt)
        {
            DrawSquare(visual, topLeftCorner, brush, CellPen, txt);
        }

        public void DrawSelectedSquare(VisualWithTag visual, Point topLeftCorner)
        {
            DrawSquare(visual, topLeftCorner, Brushes.Transparent, SelectedPen, "");
        }

        public void DrawTeamCell(VisualWithTag visual, Point topLeftCorner, string txt, Size cellSize)
        {
            using (var dc = visual.RenderOpen())
            {
                var inText = new FormattedText(txt,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    _txtTypeface,
                    FontSize,
                    Brushes.White);

                var txtPoint = new Point(topLeftCorner.X + 0.5 * (cellSize.Width - inText.Width),
                    topLeftCorner.Y + 0.5 * (cellSize.Height - inText.Height));

                dc.DrawRectangle(TeamBrush, TeamPen, new Rect(topLeftCorner, cellSize));
                dc.DrawText(inText, txtPoint);
            }
        }

        #endregion

        #region Events
        public event EventHandler SelectedCellChangedEvent;

        public void OnSelectedCellChanged()
        {
            SelectedCellChangedEvent?.Invoke(this, EventArgs.Empty);
        }
        #endregion

        #region Static
        public static double GetTeamCellSizeWidth(IEnumerable<string> teamNames)
        {
            var txtTypeface = new Typeface(
                new FontFamily("Arial"),
                FontStyles.Normal,
                FontWeights.Bold,
                FontStretch.FromOpenTypeStretch(1));

            return teamNames.Max(x =>
                new FormattedText(x + "AAAAAA", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, txtTypeface, 14,
                    Brushes.White).Width);
        }
        #endregion
        
    }

    public class VisualWithTag : DrawingVisual
    {
        public object Tag { get; set; }
    }
}
