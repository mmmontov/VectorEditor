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

        public MainWindow()
        {
            InitializeComponent();
        }

        private MainViewModel ViewModel => DataContext as MainViewModel;

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(DrawingCanvas);

            // --- Рисование нового прямоугольника ---
            if (ViewModel.IsDrawingMode)
            {
                _startPoint = pos;

                var model = new ShapeModel
                {
                    X = pos.X,
                    Y = pos.Y,
                    Width = 0,
                    Height = 0,
                    Fill = Brushes.LightBlue,
                    Stroke = Brushes.Black
                };

                _currentShape = new ShapeViewModel(model);
                ViewModel.AddShape(_currentShape);
                DrawingCanvas.CaptureMouse();
            }
            // --- Перемещение существующей фигуры ---
            else if (ViewModel.IsMovingMode)
            {
                if (e.OriginalSource is Rectangle rect)
                {
                    _selectedShape = rect.DataContext as ShapeViewModel;
                    if (_selectedShape != null)
                    {
                        ViewModel.SaveState(); // [НОВОЕ]
                        _dragOffset = new Point(pos.X - _selectedShape.X, pos.Y - _selectedShape.Y);
                        DrawingCanvas.CaptureMouse();
                    }
                }
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(DrawingCanvas);

            // Рисование
            if (_startPoint != null && _currentShape != null && ViewModel.IsDrawingMode)
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
            // Перемещение
            else if (_selectedShape != null && ViewModel.IsMovingMode && e.LeftButton == MouseButtonState.Pressed)
            {
                _selectedShape.X = pos.X - _dragOffset.X;
                _selectedShape.Y = pos.Y - _dragOffset.Y;
            }
        }

        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            DrawingCanvas.ReleaseMouseCapture();
            _startPoint = null;
            _currentShape = null;
            _selectedShape = null;
        }
    }
}
