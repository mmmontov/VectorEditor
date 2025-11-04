using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
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
        public double Width { get => _model.Width; set { if (_model.Width != value) { _model.Width = value; OnAll(); } } }
        public double Height { get => _model.Height; set { if (_model.Height != value) { _model.Height = value; OnAll(); } } }
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

    public class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<ShapeViewModel> Shapes { get; private set; }

        public RelayCommand StartRectangleCommand { get; private set; }
        public RelayCommand StartEllipseCommand { get; private set; }
        public RelayCommand StartLineCommand { get; private set; }
        public RelayCommand StartPolygonCommand { get; private set; }
        public RelayCommand StartMovingCommand { get; private set; }
        public RelayCommand ClearCommand { get; private set; }
        public RelayCommand UndoCommand { get; private set; }
        public RelayCommand RedoCommand { get; private set; }

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
