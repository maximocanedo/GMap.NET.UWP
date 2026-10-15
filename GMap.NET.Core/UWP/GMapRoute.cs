using System.Collections.Generic;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace GMap.NET.WindowsPresentation
{
    public interface IShapable
    {
        List<PointLatLng> Points {
            get;
            set;
        }

        Path CreatePath(List<Point> localPath, bool addBlurEffect);
    }

    public class GMapRoute : GMapMarker, IShapable
    {
        public List<PointLatLng> Points { get; set; }

        public GMapRoute(IEnumerable<PointLatLng> points)
        {
            Points = new List<PointLatLng>(points);
        }

        public override void Clear()
        {
            base.Clear();
            Points.Clear();
        }

        /// <summary>
        /// Crea un Path a partir de una lista de puntos. Para mayor rendimiento, establezca addBlurEffect en falso.
        /// </summary>
        /// <returns></returns>
        public virtual Path CreatePath(List<Point> localPath, bool addBlurEffect)
        {
            // En UWP se utiliza PathGeometry, PathFigure y PolyLineSegment en lugar de StreamGeometry
            var geometry = new PathGeometry();
            var figure = new PathFigure
            {
                StartPoint = localPath[0],
                IsClosed = false,
                IsFilled = false
            };

            var polySegment = new PolyLineSegment();
            for (int i = 0; i < localPath.Count; i++)
            {
                polySegment.Points.Add(localPath[i]);
            }
            figure.Segments.Add(polySegment);
            geometry.Figures.Add(figure);

            // Crear el Path para dibujar la geometría
            var myPath = new Path
            {
                Data = geometry
            };

            /* if (addBlurEffect)
            {
                var ef = new BlurEffect
                {
                    Radius = 3.0
                };
                myPath.Effect = ef;
            } //*/

            myPath.Stroke = new SolidColorBrush(Colors.Navy);
            myPath.StrokeThickness = 5;
            myPath.StrokeLineJoin = PenLineJoin.Round;
            myPath.StrokeStartLineCap = PenLineCap.Triangle;
            myPath.StrokeEndLineCap = PenLineCap.Square;
            myPath.Opacity = 0.6;
            myPath.IsHitTestVisible = false;

            return myPath;
        }
    }
}