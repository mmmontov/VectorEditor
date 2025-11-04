using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace VectorEditor
{
    public partial class MainWindow : Window
    {
        private Point? _startPoint;
        private ShapeViewModel _currentShape;
        private ShapeViewModel _selectedShape;
        private Point _dragOffset;
        private List<Point> _polygonPoints = new List<Point>();
        private Polyline _previewPolyline = null;

        public MainWindow()
        {
            InitializeComponent();
        }

        private MainViewModel ViewModel => DataContext as MainViewModel;

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(DrawingCanvas);

            // moving
            if (ViewModel.IsMovingMode)
            {
                if (e.OriginalSource is Shape shape && shape.DataContext is ShapeViewModel vm)
                {
                    _selectedShape = vm;
                    _dragOffset = new Point(pos.X - vm.X, pos.Y - vm.Y);
                    DrawingCanvas.CaptureMouse();
                }
                return;
            }

            // drawing
            switch (ViewModel.CurrentTool)
            {
                case ShapeType.Rectangle:
                case ShapeType.Ellipse:
                case ShapeType.Line:
                    _startPoint = pos;
                    var model = new ShapeModel
                    {
                        X = pos.X,
                        Y = pos.Y,
                        Width = 0,
                        Height = 0,
                        Fill = Brushes.LightBlue,
                        Stroke = Brushes.Black,
                        Type = ViewModel.CurrentTool
                    };
                    _currentShape = new ShapeViewModel(model);
                    // Add VM directly — do NOT call VM.AddShape (it used to clone); VM.AddShape now also just adds,
                    // but keeping direct add here avoids confusion.
                    ViewModel.Shapes.Add(_currentShape);
                    DrawingCanvas.CaptureMouse();
                    break;

                case ShapeType.Polygon:
                    // add vertex, double click to finish
                    if (e.ClickCount == 2)
                    {
                        if (_polygonPoints.Count >= 3)
                        {
                            double minX = _polygonPoints.Min(p => p.X);
                            double minY = _polygonPoints.Min(p => p.Y);
                            var relative = new PointCollection(_polygonPoints.Select(p => new Point(p.X - minX, p.Y - minY)));

                            var polyModel = new ShapeModel
                            {
                                X = minX,
                                Y = minY,
                                Points = relative,
                                Fill = Brushes.LightYellow,
                                Stroke = Brushes.Black,
                                Type = ShapeType.Polygon
                            };

                            ViewModel.Shapes.Add(new ShapeViewModel(polyModel));
                            ViewModel.SaveState(); // save after finalize
                        }
                        _polygonPoints.Clear();
                        RemovePolygonPreview();
                    }
                    else
                    {
                        _polygonPoints.Add(pos);
                        UpdatePolygonPreview(pos);
                    }
                    break;
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(DrawingCanvas);

            // moving
            if (_selectedShape != null && ViewModel.IsMovingMode && e.LeftButton == MouseButtonState.Pressed)
            {
                _selectedShape.X = pos.X - _dragOffset.X;
                _selectedShape.Y = pos.Y - _dragOffset.Y;
                return;
            }

            // drawing update
            if (_startPoint != null && _currentShape != null)
            {
                if (_currentShape.Type == ShapeType.Line)
                {
                    // For line: keep start point as-is; Width/Height are deltas (can be negative)
                    _currentShape.Width = pos.X - _startPoint.Value.X;
                    _currentShape.Height = pos.Y - _startPoint.Value.Y;
                    // X and Y remain _startPoint coordinates
                    _currentShape.X = _startPoint.Value.X;
                    _currentShape.Y = _startPoint.Value.Y;
                }
                else
                {
                    double x = System.Math.Min(pos.X, _startPoint.Value.X);
                    double y = System.Math.Min(pos.Y, _startPoint.Value.Y);
                    double width = System.Math.Abs(pos.X - _startPoint.Value.X);
                    double height = System.Math.Abs(pos.Y - _startPoint.Value.Y);

                    _currentShape.X = x;
                    _currentShape.Y = y;
                    _currentShape.Width = width;
                    _currentShape.Height = height;
                }
            }

            // polygon preview
            if (_polygonPoints.Count > 0)
            {
                UpdatePolygonPreview(pos);
            }
        }

        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // finalize
            DrawingCanvas.ReleaseMouseCapture();

            if (_currentShape != null)
            {
                // Save state AFTER finalizing a drawn shape
                ViewModel.SaveState();
            }

            _startPoint = null;
            _currentShape = null;
            _selectedShape = null;
        }

        private void Canvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            _polygonPoints.Clear();
            RemovePolygonPreview();
        }

        // polygon preview helpers
        private void UpdatePolygonPreview(Point? mousePos)
        {
            var pts = new List<Point>(_polygonPoints);
            if (mousePos.HasValue) pts.Add(mousePos.Value);

            if (_previewPolyline == null)
            {
                _previewPolyline = new Polyline
                {
                    Stroke = Brushes.Gray,
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 2, 2 },
                    IsHitTestVisible = false
                };
                DrawingCanvas.Children.Add(_previewPolyline);
            }

            _previewPolyline.Points = new PointCollection(pts);
        }

        private void RemovePolygonPreview()
        {
            if (_previewPolyline != null)
            {
                DrawingCanvas.Children.Remove(_previewPolyline);
                _previewPolyline = null;
            }
        }
    }
}
