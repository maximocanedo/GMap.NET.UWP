using System;
using System.ComponentModel;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace GMap.NET.WindowsPresentation
{
    /// <summary>
    /// GMap.NET marker para UWP
    /// </summary>
    public class GMapMarker : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        protected void OnPropertyChanged(PropertyChangedEventArgs name)
        {
            PropertyChanged?.Invoke(this, name);
        }

        UIElement _shape;
        static readonly PropertyChangedEventArgs ShapePropertyChangedEventArgs = new PropertyChangedEventArgs(nameof(Shape));

        /// <summary>
        /// Visual del marcador
        /// </summary>
        public UIElement Shape {
            get {
                return _shape;
            }
            set {
                if (_shape != value)
                {
                    _shape = value;
                    OnPropertyChanged(ShapePropertyChangedEventArgs);

                    UpdateLocalPosition();
                }
            }
        }

        private PointLatLng _position;

        /// <summary>
        /// Coordenada del marcador
        /// </summary>
        public PointLatLng Position {
            get {
                return _position;
            }
            set {
                if (_position != value)
                {
                    _position = value;
                    UpdateLocalPosition();
                }
            }
        }

        GMapControl _map;

        /// <summary>
        /// El mapa de este marcador
        /// </summary>
        public GMapControl Map {
            get {
                if (Shape != null && _map == null)
                {
                    DependencyObject visual = Shape;
                    while (visual != null && !(visual is GMapControl))
                    {
                        visual = VisualTreeHelper.GetParent(visual);
                    }

                    _map = visual as GMapControl;
                }

                return _map;
            }
            internal set {
                _map = value;
            }
        }

        /// <summary>
        /// Objeto personalizado
        /// </summary>
        public object Tag;

        Point _offset;

        /// <summary>
        /// Desplazamiento (offset) del marcador
        /// </summary>
        public Point Offset {
            get {
                return _offset;
            }
            set {
                if (_offset != value)
                {
                    _offset = value;
                    UpdateLocalPosition();
                }
            }
        }

        int _localPositionX;

        static readonly PropertyChangedEventArgs LocalPositionXPropertyChangedEventArgs =
            new PropertyChangedEventArgs(nameof(LocalPositionX));

        /// <summary>
        /// Posición X local del marcador
        /// </summary>
        public int LocalPositionX {
            get {
                return _localPositionX;
            }
            internal set {
                if (_localPositionX != value)
                {
                    _localPositionX = value;
                    OnPropertyChanged(LocalPositionXPropertyChangedEventArgs);
                }
            }
        }

        int _localPositionY;

        static readonly PropertyChangedEventArgs LocalPositionYPropertyChangedEventArgs =
            new PropertyChangedEventArgs(nameof(LocalPositionY));

        /// <summary>
        /// Posición Y local del marcador
        /// </summary>
        public int LocalPositionY {
            get {
                return _localPositionY;
            }
            internal set {
                if (_localPositionY != value)
                {
                    _localPositionY = value;
                    OnPropertyChanged(LocalPositionYPropertyChangedEventArgs);
                }
            }
        }

        int _zIndex;

        static readonly PropertyChangedEventArgs ZIndexPropertyChangedEventArgs =
            new PropertyChangedEventArgs(nameof(ZIndex));

        /// <summary>
        /// Índice Z, orden de renderizado
        /// </summary>
        public int ZIndex {
            get {
                return _zIndex;
            }
            set {
                if (_zIndex != value)
                {
                    _zIndex = value;
                    OnPropertyChanged(ZIndexPropertyChangedEventArgs);
                }
            }
        }

        public GMapMarker(PointLatLng pos)
        {
            Position = pos;
        }

        internal GMapMarker()
        {
        }

        /// <summary>
        /// Llama a Dispose en la forma si implementa IDisposable, establece la forma en null y limpia
        /// </summary>
        public virtual void Clear()
        {
            if (Shape is IDisposable s)
            {
                s.Dispose();
            }

            Shape = null;
        }

        /// <summary>
        /// Actualiza la posición local del marcador
        /// </summary>
        void UpdateLocalPosition()
        {
            if (Map != null)
            {
                var p = Map.FromLatLngToLocal(Position);
                p.Offset(-(long)Map.MapTranslateTransform.X, -(long)Map.MapTranslateTransform.Y);

                LocalPositionX = (int)(p.X + (long)Offset.X);
                LocalPositionY = (int)(p.Y + (long)Offset.Y);
            }
        }

        /// <summary>
        /// Fuerza la actualización de la posición local del marcador
        /// </summary>
        internal void ForceUpdateLocalPosition(GMapControl m)
        {
            if (m != null)
            {
                _map = m;
            }

            UpdateLocalPosition();
        }
    }
}