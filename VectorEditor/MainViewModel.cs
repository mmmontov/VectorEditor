using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace VectorEditor
{
    public enum ShapeType
    {
        None,
        Rectangle,
        Ellipse,
        Line,
        Polygon
    }



    public class ShapeViewModel : INotifyPropertyChanged
    {
        private ShapeModel _model;

        public ShapeViewModel(ShapeModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            _model = model;
        }

        public ShapeModel Model => _model;

        public double X { get => _model.X; set { if (_model.X != value) { _model.X = value; OnAll(); } } }
        public double Y { get => _model.Y; set { if (_model.Y != value) { _model.Y = value; OnAll(); } } }
        public double Width
        {
            get => _model.Width;
            set
            {
                if (_model.Width != value)
                {
                    var old = _model.Width;
                    _model.Width = value;

                    // If polygon, scale points horizontally
                    if (Type == ShapeType.Polygon && Points != null && Points.Count > 0)
                    {
                        double oldW = old;
                        if (oldW <= 0)
                        {
                            // compute current bounding width from points
                            double maxX = 0;
                            foreach (var p in Points) if (p.X > maxX) maxX = p.X;
                            oldW = maxX;
                            if (oldW <= 0) oldW = 1.0; // avoid divide by zero
                        }

                        double sx = value / oldW;
                        var newPts = new PointCollection();
                        foreach (var p in Points)
                        {
                            newPts.Add(new Point(p.X * sx, p.Y));
                        }

                        // assign scaled points without triggering recursive scaling (set model directly)
                        _model.Points = newPts;
                        OnPropertyChanged(nameof(Points));
                    }

                    OnAll();
                }
            }
        }
        public double Height
        {
            get => _model.Height;
            set
            {
                if (_model.Height != value)
                {
                    var old = _model.Height;
                    _model.Height = value;

                    // If polygon, scale points vertically
                    if (Type == ShapeType.Polygon && Points != null && Points.Count > 0)
                    {
                        double oldH = old;
                        if (oldH <= 0)
                        {
                            double maxY = 0;
                            foreach (var p in Points) if (p.Y > maxY) maxY = p.Y;
                            oldH = maxY;
                            if (oldH <= 0) oldH = 1.0;
                        }

                        double sy = value / oldH;
                        var newPts = new PointCollection();
                        foreach (var p in Points)
                        {
                            newPts.Add(new Point(p.X, p.Y * sy));
                        }

                        _model.Points = newPts;
                        OnPropertyChanged(nameof(Points));
                    }

                    OnAll();
                }
            }
        }
        public Brush Fill { get => _model.Fill; set { _model.Fill = value; OnPropertyChanged(); } }
        public Brush Stroke { get => _model.Stroke; set { _model.Stroke = value; OnPropertyChanged(); } }
        public ShapeType Type { get => _model.Type; set { _model.Type = value; OnPropertyChanged(); } }
        public PointCollection Points { get => _model.Points; set { _model.Points = value; OnPropertyChanged(); } }

        // For Line template: relative end point
        public double EndX { get { return Width; } }   // X2 = Width (relative to X)
        public double EndY { get { return Height; } }  // Y2 = Height

        private void OnAll()
        {
            OnPropertyChanged(nameof(X));
            OnPropertyChanged(nameof(Y));
            OnPropertyChanged(nameof(Width));
            OnPropertyChanged(nameof(Height));
            OnPropertyChanged(nameof(EndX));
            OnPropertyChanged(nameof(EndY));
        }

        // deep clone model (used by SaveState)
        public ShapeModel CloneModel()
        {
            var copy = new ShapeModel
            {
                X = this.X,
                Y = this.Y,
                Width = this.Width,
                Height = this.Height,
                Fill = this.Fill,
                Stroke = this.Stroke,
                Type = this.Type,
                Points = new PointCollection()
            };

            if (this.Points != null)
            {
                foreach (var p in this.Points)
                    copy.Points.Add(new Point(p.X, p.Y));
            }

            return copy;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            var h = PropertyChanged;
            if (h != null) h(this, new PropertyChangedEventArgs(name));
        }
    }

    // Simple helper to show available colors in UI
    public class ColorItem
    {
        public string Name { get; }
        public Brush Brush { get; }
        public ColorItem(string name, Brush brush)
        {
            Name = name;
            Brush = brush;
        }
        public override string ToString() => Name;
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<ShapeViewModel> Shapes { get; private set; }

        public ObservableCollection<ColorItem> Colors { get; } = new ObservableCollection<ColorItem>();

        private ColorItem _selectedColor;
        public ColorItem SelectedColor
        {
            get => _selectedColor;
            set { _selectedColor = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
        }

        // stroke color selection
        private ColorItem _selectedStrokeColor;
        public ColorItem SelectedStrokeColor
        {
            get => _selectedStrokeColor;
            set { _selectedStrokeColor = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
        }

        private ShapeViewModel _selectedShape;
        public ShapeViewModel SelectedShape
        {
            get => _selectedShape;
            set { _selectedShape = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
        }

        // Zoom (scale) for canvas
        private double _zoom = 1.0;
        public double Zoom
        {
            get => _zoom;
            set
            {
                var v = Math.Max(0.2, Math.Min(5.0, value));
                if (Math.Abs(_zoom - v) > 0.0001)
                {
                    _zoom = v;
                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public RelayCommand ApplyColorToSelectedCommand { get; private set; }
        public RelayCommand ApplyStrokeColorToSelectedCommand { get; private set; }
        public RelayCommand RemoveSelectedCommand { get; private set; }

        public RelayCommand StartRectangleCommand { get; private set; }
        public RelayCommand StartEllipseCommand { get; private set; }
        public RelayCommand StartLineCommand { get; private set; }
        public RelayCommand StartPolygonCommand { get; private set; }
        public RelayCommand StartMovingCommand { get; private set; }
        public RelayCommand ClearCommand { get; private set; }
        public RelayCommand UndoCommand { get; private set; }
        public RelayCommand RedoCommand { get; private set; }

        // zoom commands
        public RelayCommand ZoomInCommand { get; private set; }
        public RelayCommand ZoomOutCommand { get; private set; }
        public RelayCommand ResetZoomCommand { get; private set; }

        private Stack<List<ShapeModel>> _undoStack;
        private Stack<List<ShapeModel>> _redoStack;

        private ShapeType _currentTool = ShapeType.None;
        public ShapeType CurrentTool
        {
            get { return _currentTool; }
            set
            {
                _currentTool = value;
                if (value != ShapeType.None) IsMovingMode = false;
                OnPropertyChanged();
            }
        }

        private bool _isMovingMode;
        public bool IsMovingMode
        {
            get { return _isMovingMode; }
            set
            {
                _isMovingMode = value;
                if (value) CurrentTool = ShapeType.None;
                OnPropertyChanged();
            }
        }

        public MainViewModel()
        {
            Shapes = new ObservableCollection<ShapeViewModel>();

            // populate a small palette
            Colors.Add(new ColorItem("Light Blue", Brushes.LightBlue));
            Colors.Add(new ColorItem("Light Yellow", Brushes.LightYellow));
            Colors.Add(new ColorItem("Light Gray", Brushes.LightGray));
            Colors.Add(new ColorItem("Red", Brushes.Red));
            Colors.Add(new ColorItem("Green", Brushes.Green));
            Colors.Add(new ColorItem("Blue", Brushes.Blue));
            Colors.Add(new ColorItem("Black", Brushes.Black));

            // default selection
            SelectedColor = Colors[0];
            SelectedStrokeColor = Colors[6];

            StartRectangleCommand = new RelayCommand(o => CurrentTool = ShapeType.Rectangle);
            StartEllipseCommand = new RelayCommand(o => CurrentTool = ShapeType.Ellipse);
            StartLineCommand = new RelayCommand(o => CurrentTool = ShapeType.Line);
            StartPolygonCommand = new RelayCommand(o => CurrentTool = ShapeType.Polygon);
            StartMovingCommand = new RelayCommand(o => IsMovingMode = true);
            ClearCommand = new RelayCommand(o => { SaveState(); Shapes.Clear(); });

            _undoStack = new Stack<List<ShapeModel>>();
            _redoStack = new Stack<List<ShapeModel>>();

            UndoCommand = new RelayCommand(o => Undo(), o => _undoStack.Count > 0);
            RedoCommand = new RelayCommand(o => Redo(), o => _redoStack.Count > 0);

            ApplyColorToSelectedCommand = new RelayCommand(o =>
            {
                if (SelectedShape != null && SelectedColor != null)
                {
                    // clone brush to avoid potential shared/frozen brushes
                    Brush b = null;
                    try { b = SelectedColor.Brush.CloneCurrentValue() as Brush; } catch { b = SelectedColor.Brush; }
                    var brushToApply = b ?? SelectedColor.Brush;

                    // apply both fill and stroke
                    SelectedShape.Fill = brushToApply;
                }
            }, o => SelectedShape != null && SelectedColor != null);

            ApplyStrokeColorToSelectedCommand = new RelayCommand(o =>
            {
                if (SelectedShape != null && SelectedStrokeColor != null)
                {
                    Brush b = null;
                    try { b = SelectedStrokeColor.Brush.CloneCurrentValue() as Brush; } catch { b = SelectedStrokeColor.Brush; }
                    SelectedShape.Stroke = b ?? SelectedStrokeColor.Brush;
                }
            }, o => SelectedShape != null && SelectedStrokeColor != null);

            RemoveSelectedCommand = new RelayCommand(o =>
            {
                if (SelectedShape != null)
                {
                    SaveState();
                    Shapes.Remove(SelectedShape);
                    SelectedShape = null;
                }
            }, o => SelectedShape != null);

            // zoom commands
            ZoomInCommand = new RelayCommand(o => Zoom = Math.Round(Math.Min(5.0, Zoom + 0.1), 2), o => Zoom < 5.0);
            ZoomOutCommand = new RelayCommand(o => Zoom = Math.Round(Math.Max(0.2, Zoom - 0.1), 2), o => Zoom > 0.2);
            ResetZoomCommand = new RelayCommand(o => Zoom = 1.0);
        }

        // NOTE: Do NOT clone here — AddShape simply adds the provided VM.
        // SaveState should be called externally at the correct times (after finalization)
        public void AddShape(ShapeViewModel shape)
        {
            if (shape == null) return;
            Shapes.Add(shape);
        }

        // Save deep snapshot for undo
        public void SaveState()
        {
            var snapshot = new List<ShapeModel>();
            foreach (var s in Shapes)
            {
                snapshot.Add(s.CloneModel());
            }
            _undoStack.Push(snapshot);
            _redoStack.Clear();
        }

        private void Undo()
        {
            if (_undoStack.Count == 0) return;

            var current = new List<ShapeModel>();
            foreach (var s in Shapes) current.Add(s.CloneModel());
            _redoStack.Push(current);

            var prev = _undoStack.Pop();
            Shapes.Clear();
            foreach (var m in prev) Shapes.Add(new ShapeViewModel(m));
        }

        private void Redo()
        {
            if (_redoStack.Count == 0) return;

            var current = new List<ShapeModel>();
            foreach (var s in Shapes) current.Add(s.CloneModel());
            _undoStack.Push(current);

            var next = _redoStack.Pop();
            Shapes.Clear();
            foreach (var m in next) Shapes.Add(new ShapeViewModel(m));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            var h = PropertyChanged;
            if (h != null) h(this, new PropertyChangedEventArgs(name));
        }
    }
}
