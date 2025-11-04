using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Collections.Generic;

namespace VectorEditor
{
    public class ShapeViewModel : INotifyPropertyChanged
    {
        private ShapeModel _model;
        public ShapeViewModel(ShapeModel model) => _model = model;

        public double X { get => _model.X; set { _model.X = value; OnPropertyChanged(); } }
        public double Y { get => _model.Y; set { _model.Y = value; OnPropertyChanged(); } }
        public double Width { get => _model.Width; set { _model.Width = value; OnPropertyChanged(); } }
        public double Height { get => _model.Height; set { _model.Height = value; OnPropertyChanged(); } }
        public Brush Fill { get => _model.Fill; set { _model.Fill = value; OnPropertyChanged(); } }
        public Brush Stroke { get => _model.Stroke; set { _model.Stroke = value; OnPropertyChanged(); } }

        public ShapeModel CloneModel() => new ShapeModel
        {
            X = X,
            Y = Y,
            Width = Width,
            Height = Height,
            Fill = Fill,
            Stroke = Stroke
        };

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<ShapeViewModel> Shapes { get; } = new ObservableCollection<ShapeViewModel>();

        public RelayCommand StartDrawingRectangleCommand { get; }
        public RelayCommand StartMovingCommand { get; }
        public RelayCommand ClearCommand { get; }

        // [НОВОЕ] Undo / Redo
        public RelayCommand UndoCommand { get; }
        public RelayCommand RedoCommand { get; }

        // [ИСПРАВЛЕНО] явная инициализация для совместимости с C# 7.3
        private Stack<List<ShapeModel>> _undoStack = new Stack<List<ShapeModel>>(); // [ИСПРАВЛЕНО]
        private Stack<List<ShapeModel>> _redoStack = new Stack<List<ShapeModel>>(); // [ИСПРАВЛЕНО]

        private bool _isDrawingMode;
        public bool IsDrawingMode
        {
            get => _isDrawingMode;
            set
            {
                _isDrawingMode = value;
                if (value) IsMovingMode = false;
                OnPropertyChanged();
            }
        }

        private bool _isMovingMode;
        public bool IsMovingMode
        {
            get => _isMovingMode;
            set
            {
                _isMovingMode = value;
                if (value) IsDrawingMode = false;
                OnPropertyChanged();
            }
        }

        public MainViewModel()
        {
            StartDrawingRectangleCommand = new RelayCommand(_ => IsDrawingMode = true);
            StartMovingCommand = new RelayCommand(_ => IsMovingMode = true);
            ClearCommand = new RelayCommand(_ => { SaveState(); Shapes.Clear(); });

            // [НОВОЕ] Undo / Redo
            UndoCommand = new RelayCommand(_ => Undo(), _ => _undoStack.Count > 0);
            RedoCommand = new RelayCommand(_ => Redo(), _ => _redoStack.Count > 0);
        }

        public void AddShape(ShapeViewModel shape)
        {
            SaveState(); // [НОВОЕ]
            Shapes.Add(shape);
        }

        public void SaveState() // [НОВОЕ]
        {
            var snapshot = new List<ShapeModel>();
            foreach (var s in Shapes)
                snapshot.Add(s.CloneModel());
            _undoStack.Push(snapshot);
            _redoStack.Clear();
        }

        private void Undo() // [НОВОЕ]
        {
            if (_undoStack.Count == 0) return;
            var current = new List<ShapeModel>();
            foreach (var s in Shapes) current.Add(s.CloneModel());
            _redoStack.Push(current);

            var prev = _undoStack.Pop();
            Shapes.Clear();
            foreach (var s in prev)
                Shapes.Add(new ShapeViewModel(s));
        }

        private void Redo() // [НОВОЕ]
        {
            if (_redoStack.Count == 0) return;
            var current = new List<ShapeModel>();
            foreach (var s in Shapes) current.Add(s.CloneModel());
            _undoStack.Push(current);

            var next = _redoStack.Pop();
            Shapes.Clear();
            foreach (var s in next)
                Shapes.Add(new ShapeViewModel(s));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
