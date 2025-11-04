using System.Windows.Media;

namespace VectorEditor
{
    public class ShapeModel
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; } = 100;
        public double Height { get; set; } = 60;
        public Brush Fill { get; set; } = Brushes.LightGray;
        public Brush Stroke { get; set; } = Brushes.Black;
    }
}
