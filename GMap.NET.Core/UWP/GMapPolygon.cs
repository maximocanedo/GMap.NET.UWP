using System.Collections.Generic;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace GMap.NET.WindowsPresentation
{
    public class GMapPolygon : GMapMarker, IShapable
    {
        public List<PointLatLng> Points { get; set; }

        public GMapPolygon(IEnumerable<PointLatLng> points)
        {
            Points = new List<PointLatLng>(points);
        }

        public override void Clear()
        {
            base.Clear();
            Points.Clear();
        }

        /// <summary>
        /// Crea un Path a partir de una lista de puntos para el polígono.
        /// </summary>
        /// <returns></returns>
        public virtual Path CreatePath(List<Point> localPath, bool addBlurEffect)
        {
            // En UWP se utiliza PathGeometry, PathFigure y PolyLineSegment en lugar de StreamGeometry
            var geometry = new PathGeometry();
            var figure = new PathFigure
            {
                StartPoint = localPath[0],
                IsClosed = true,  // El polígono debe cerrarse
                IsFilled = true   // El polígono debe rellenarse
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

            if (addBlurEffect)
            {
                // Opcional: Se puede integrar el CommunityToolkit.Uwp.UI.Media.BlurEffect si se requiere
            }

            myPath.Stroke = new SolidColorBrush(Colors.MidnightBlue);
            myPath.StrokeThickness = 5;
            myPath.StrokeLineJoin = PenLineJoin.Round;
            myPath.StrokeStartLineCap = PenLineCap.Triangle;
            myPath.StrokeEndLineCap = PenLineCap.Square;
            myPath.Fill = new SolidColorBrush(Colors.AliceBlue);
            myPath.Opacity = 0.6;
            myPath.IsHitTestVisible = false;

            return myPath;
        }
    }
}