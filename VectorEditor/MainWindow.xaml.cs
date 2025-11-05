using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.IO;
using Microsoft.Win32;

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

        // last saved path
        private string _lastSavedPath;

        public MainWindow()
        {
            InitializeComponent();
            this.PreviewMouseWheel += Window_PreviewMouseWheel;
        }

        private MainViewModel ViewModel => DataContext as MainViewModel;

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "VectorEditor files (*.vec)|*.vec|All files (*.*)|*.*",
                DefaultExt = ".vec"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    LoadFromFile(dlg.FileName);
                    _lastSavedPath = dlg.FileName;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка загрузки файла: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LoadFromFile(string path)
        {
            var vm = ViewModel;
            if (vm == null) throw new InvalidOperationException("ViewModel не инициализирован");

            var lines = File.ReadAllLines(path);
            if (lines.Length == 0 || lines[0] != "VEC1") throw new InvalidDataException("Файл не является VEC форматом или повреждён.");

            vm.Shapes.Clear();

            int i = 1;
            // read header lines until shapes
            double zoom = 1.0;
            int count = 0;
            for (; i < lines.Length; i++)
            {
                var l = lines[i];
                if (string.IsNullOrWhiteSpace(l)) continue;
                if (l.StartsWith("Zoom:"))
                {
                    double.TryParse(l.Substring(5), out zoom);
                    continue;
                }
                if (l.StartsWith("Count:"))
                {
                    int.TryParse(l.Substring(6), out count);
                    continue;
                }
                if (l == "BEGIN_SHAPE") break;
            }

            vm.Zoom = zoom;

            while (i < lines.Length)
            {
                var l = lines[i++].Trim();
                if (l != "BEGIN_SHAPE") break;

                var dict = new Dictionary<string, string>();
                while (i < lines.Length)
                {
                    var line = lines[i++];
                    if (line == "END_SHAPE") break;
                    var idx = line.IndexOf(':');
                    if (idx <= 0) continue;
                    var key = line.Substring(0, idx);
                    var val = line.Substring(idx + 1);
                    dict[key] = val;
                }

                // Create shape from dict
                ShapeType type = ShapeType.None;
                if (dict.ContainsKey("Type")) Enum.TryParse(dict["Type"], out type);

                double x = GetDouble(dict, "X");
                double y = GetDouble(dict, "Y");
                double width = GetDouble(dict, "Width");
                double height = GetDouble(dict, "Height");
                Brush fill = BrushFromString(GetString(dict, "Fill")) ?? Brushes.Transparent;
                Brush stroke = BrushFromString(GetString(dict, "Stroke")) ?? Brushes.Black;

                var model = new ShapeModel
                {
                    X = x,
                    Y = y,
                    Width = width,
                    Height = height,
                    Fill = fill,
                    Stroke = stroke,
                    Type = type
                };

                if (type == ShapeType.Polygon && dict.ContainsKey("Points"))
                {
                    var pts = dict["Points"].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    var coll = new PointCollection();
                    foreach (var p in pts)
                    {
                        var parts = p.Split(',');
                        if (parts.Length == 2)
                        {
                            if (double.TryParse(parts[0], out double px) && double.TryParse(parts[1], out double py))
                            {
                                coll.Add(new Point(px, py));
                            }
                        }
                    }
                    model.Points = coll;
                }

                if (type == ShapeType.Line)
                {
                    model.Width = GetDouble(dict, "EndX");
                    model.Height = GetDouble(dict, "EndY");
                }

                vm.Shapes.Add(new ShapeViewModel(model));
            }
        }

        private static string GetString(Dictionary<string,string> d, string key)
        {
            return d.ContainsKey(key) ? d[key] : null;
        }

        private static double GetDouble(Dictionary<string,string> d, string key)
        {
            if (!d.ContainsKey(key)) return 0.0;
            double.TryParse(d[key], out double v);
            return v;
        }

        private Brush BrushFromString(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            try
            {
                var c = (Color)ColorConverter.ConvertFromString(s);
                return new SolidColorBrush(c);
            }
            catch { return null; }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_lastSavedPath))
            {
                SaveAsButton_Click(sender, e);
                return;
            }

            try
            {
                SaveToFile(_lastSavedPath);
                MessageBox.Show("Файл сохранён: " + _lastSavedPath, "Сохранение", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка сохранения файла: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveAsButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "VectorEditor files (*.vec)|*.vec|All files (*.*)|*.*",
                DefaultExt = ".vec",
                AddExtension = true
            };

            if (dlg.ShowDialog() == true)
            {
                _lastSavedPath = dlg.FileName;
                try
                {
                    SaveToFile(_lastSavedPath);
                    MessageBox.Show("Файл сохранён: " + _lastSavedPath, "Сохранение", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка сохранения файла: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveToFile(string path)
        {
            var vm = ViewModel;
            if (vm == null) throw new InvalidOperationException("ViewModel не инициализирован");

            using (var sw = new StreamWriter(path, false, System.Text.Encoding.UTF8))
            {
                // header
                sw.WriteLine("VEC1");
                sw.WriteLine($"Zoom:{vm.Zoom}");
                sw.WriteLine($"Count:{vm.Shapes.Count}");

                foreach (var s in vm.Shapes)
                {
                    sw.WriteLine("BEGIN_SHAPE");
                    sw.WriteLine($"Type:{s.Type}");
                    sw.WriteLine($"X:{s.X}");
                    sw.WriteLine($"Y:{s.Y}");
                    sw.WriteLine($"Width:{s.Width}");
                    sw.WriteLine($"Height:{s.Height}");
                    sw.WriteLine($"Fill:{BrushToString(s.Fill)}");
                    sw.WriteLine($"Stroke:{BrushToString(s.Stroke)}");

                    if (s.Type == ShapeType.Polygon && s.Points != null && s.Points.Count > 0)
                    {
                        var pts = string.Join(";", s.Points.Select(p => $"{p.X},{p.Y}"));
                        sw.WriteLine($"Points:{pts}");
                    }

                    // For lines store end relative point too
                    if (s.Type == ShapeType.Line)
                    {
                        sw.WriteLine($"EndX:{s.EndX}");
                        sw.WriteLine($"EndY:{s.EndY}");
                    }

                    sw.WriteLine("END_SHAPE");
                }
            }
        }

        private string BrushToString(Brush brush)
        {
            if (brush is SolidColorBrush scb)
            {
                // Color.ToString returns #AARRGGBB
                return scb.Color.ToString();
            }
            return "#00000000"; // transparent fallback
        }

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(DrawingCanvas);

            // If user clicked a shape while not in drawing mode, select it
            if (e.OriginalSource is Shape clickedShape && clickedShape.DataContext is ShapeViewModel clickedVm)
            {
                if (!ViewModel.IsMovingMode && ViewModel.CurrentTool == ShapeType.None)
                {
                    ViewModel.SelectedShape = clickedVm;
                    return;
                }
            }
            else
            {
                // clicked empty canvas -> clear selection
                ViewModel.SelectedShape = null;
            }

            // moving
            if (ViewModel.IsMovingMode)
            {
                if (e.OriginalSource is Shape shape && shape.DataContext is ShapeViewModel vm)
                {
                    _selectedShape = vm;
                    ViewModel.SelectedShape = vm; // keep VM selection in sync
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

                    // determine chosen brushes from ViewModel (fallbacks)
                    var fillBrush = ViewModel?.SelectedColor?.Brush ?? Brushes.LightBlue;
                    var strokeBrush = ViewModel?.SelectedStrokeColor?.Brush ?? Brushes.Black;

                    var model = new ShapeModel
                    {
                        X = pos.X,
                        Y = pos.Y,
                        Width = 0,
                        Height = 0,
                        Fill = fillBrush,
                        Stroke = strokeBrush,
                        Type = ViewModel.CurrentTool
                    };
                    _currentShape = new ShapeViewModel(model);
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

                            var fillPoly = ViewModel?.SelectedColor?.Brush ?? Brushes.LightYellow;
                            var strokePoly = ViewModel?.SelectedStrokeColor?.Brush ?? Brushes.Black;

                            var polyModel = new ShapeModel
                            {
                                X = minX,
                                Y = minY,
                                Points = relative,
                                Fill = fillPoly,
                                Stroke = strokePoly,
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

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                if (ViewModel?.RemoveSelectedCommand != null && ViewModel.RemoveSelectedCommand.CanExecute(null))
                {
                    ViewModel.RemoveSelectedCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }

        private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (ViewModel != null)
                {
                    double delta = e.Delta > 0 ? 0.1 : -0.1;
                    ViewModel.Zoom = Math.Round(Math.Max(0.2, Math.Min(5.0, ViewModel.Zoom + delta)), 2);
                    e.Handled = true;
                }
            }
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

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void NumberOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Разрешаем только цифры и точку
            e.Handled = !System.Text.RegularExpressions.Regex.IsMatch(e.Text, @"^[0-9.]$");
        }

        private void NumberOnly_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Запрещаем ввод минуса и других управляющих клавиш
            if (e.Key == Key.Subtract || e.Key == Key.OemMinus)
                e.Handled = true;
        }
    }
}
