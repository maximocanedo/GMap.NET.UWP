using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;
using GMap.NET.Internals;
using GMap.NET.MapProviders;
using GMap.NET.Projections;

namespace GMap.NET.WindowsPresentation
{
    /// <summary>
    /// Control GMap.NET para UWP / Windows Mobile
    /// </summary>
    public partial class GMapControl : ItemsControl, Interface, IDisposable
    {
        #region DependencyProperties y propiedades relacionadas

        [Browsable(false)]
        public GMapProvider MapProvider {
            get { return GetValue(MapProviderProperty) as GMapProvider; }
            set { SetValue(MapProviderProperty, value); }
        }

        public Point MapPoint {
            get { return (Point)GetValue(MapPointProperty); }
            set { SetValue(MapPointProperty, value); }
        }

        public static readonly DependencyProperty CenterPositionProperty = DependencyProperty.Register("CenterPosition",
            typeof(PointLatLng),
            typeof(GMapControl),
            new PropertyMetadata(new PointLatLng(), CenterPositionPropertyChanged));

        public static readonly DependencyProperty MapPointProperty = DependencyProperty.Register("MapPoint",
            typeof(Point),
            typeof(GMapControl),
            new PropertyMetadata(new Point(), OnMapPointPropertyChanged));

        private static void OnMapPointPropertyChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            var temp = (Point)e.NewValue;
            if (source is GMapControl ctrl)
            {
                ctrl.Position = new PointLatLng(temp.X, temp.Y);
            }
        }

        public static readonly DependencyProperty MapProviderProperty = DependencyProperty.Register("MapProvider",
            typeof(GMapProvider),
            typeof(GMapControl),
            new PropertyMetadata(EmptyProvider.Instance, MapProviderPropertyChanged));

        public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register("Zoom",
            typeof(double),
            typeof(GMapControl),
            new PropertyMetadata(0.0, ZoomPropertyChanged));

        public static readonly DependencyProperty ZoomXProperty = DependencyProperty.Register("ZoomX",
            typeof(double),
            typeof(GMapControl),
            new PropertyMetadata(0.0, ZoomXPropertyChanged));

        public static readonly DependencyProperty ZoomYProperty = DependencyProperty.Register("ZoomY",
            typeof(double),
            typeof(GMapControl),
            new PropertyMetadata(0.0, ZoomYPropertyChanged));

        public static readonly DependencyProperty MultiTouchEnabledProperty = DependencyProperty.Register(
            "MultiTouchEnabled",
            typeof(bool),
            typeof(GMapControl),
            new PropertyMetadata(false, OnMultiTouchEnabledChanged));

        public static readonly DependencyProperty TouchEnabledProperty = DependencyProperty.Register("TouchEnabled",
            typeof(bool),
            typeof(GMapControl),
            new PropertyMetadata(false));

        [Category("GMap.NET")]
        public double Zoom {
            get { return (double)GetValue(ZoomProperty); }
            set { SetValue(ZoomProperty, value); }
        }

        [Category("GMap.NET")]
        public double ZoomX {
            get { return (double)GetValue(ZoomXProperty); }
            set { SetValue(ZoomXProperty, value); }
        }

        [Category("GMap.NET")]
        public double ZoomY {
            get { return (double)GetValue(ZoomYProperty); }
            set { SetValue(ZoomYProperty, value); }
        }

        [Category("GMap.NET")]
        public PointLatLng CenterPosition {
            get { return (PointLatLng)GetValue(CenterPositionProperty); }
            set { SetValue(CenterPositionProperty, value); }
        }

        [Category("GMap.NET")]
        [Description("map scale type")]
        public ScaleModes ScaleMode {
            get { return _scaleMode; }
            set {
                _scaleMode = value;
                InvalidateVisual();
            }
        }

        [Category("GMap.NET")]
        [Description("Enable pinch map zoom")]
        public bool MultiTouchEnabled {
            get { return (bool)GetValue(MultiTouchEnabledProperty); }
            set { SetValue(MultiTouchEnabledProperty, value); }
        }

        private static void CenterPositionPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            if (obj is GMapControl gmapControl && e.NewValue is PointLatLng pt)
            {
                gmapControl.CenterPosition = gmapControl.Position = pt;
            }
        }

        private static void MapProviderPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var map = (GMapControl)d;
            if (map != null && e.NewValue != null)
            {
                var viewarea = map.SelectedArea;
                if (viewarea != RectLatLng.Empty)
                {
                    map.Position = new PointLatLng(viewarea.Lat - viewarea.HeightLat / 2,
                        viewarea.Lng + viewarea.WidthLng / 2);
                }
                else
                {
                    viewarea = map.ViewArea;
                }

                map._core.Provider = e.NewValue as GMapProvider;

                if (map._core.IsStarted && map._core.ZoomToArea)
                {
                    if (viewarea != RectLatLng.Empty && viewarea != map.ViewArea)
                    {
                        int bestZoom = map._core.GetMaxZoomToFitRect(viewarea);
                        if (bestZoom > 0 && map.Zoom != bestZoom)
                            map.Zoom = bestZoom;
                    }
                }
            }
        }

        private static void ZoomPropertyChanged(GMapControl mapControl, double value, double oldValue, ZoomMode zoomMode)
        {
            if (mapControl != null && mapControl.MapProvider.Projection != null)
            {
                double remainder = value % 1;

                if (mapControl.ScaleMode != ScaleModes.Integer && remainder != 0 && mapControl.ActualWidth > 0)
                {
                    bool scaleDown = mapControl.ScaleMode == ScaleModes.ScaleDown || (mapControl.ScaleMode == ScaleModes.Dynamic && remainder > 0.25);
                    if (scaleDown)
                        remainder--;

                    double scaleValue = Math.Pow(2d, remainder);
                    if (mapControl.MapScaleTransform == null)
                    {
                        mapControl.MapScaleTransform = mapControl._lastScaleTransform;
                    }

                    if (zoomMode == ZoomMode.XY || zoomMode == ZoomMode.X)
                    {
                        mapControl.MapScaleTransform.ScaleX = scaleValue;
                        mapControl._core.ScaleX = 1 / scaleValue;
                        mapControl.MapScaleTransform.CenterX = mapControl.ActualWidth / 2;
                    }

                    if (zoomMode == ZoomMode.XY || zoomMode == ZoomMode.Y)
                    {
                        mapControl.MapScaleTransform.ScaleY = scaleValue;
                        mapControl._core.ScaleY = 1 / scaleValue;
                        mapControl.MapScaleTransform.CenterY = mapControl.ActualHeight / 2;
                    }

                    mapControl._core.Zoom = Convert.ToInt32(scaleDown ? Math.Ceiling(value) : value - remainder);
                }
                else
                {
                    mapControl.MapScaleTransform = null;
                    if (zoomMode == ZoomMode.XY || zoomMode == ZoomMode.X) mapControl._core.ScaleX = 1;
                    if (zoomMode == ZoomMode.XY || zoomMode == ZoomMode.Y) mapControl._core.ScaleY = 1;
                    mapControl._core.Zoom = (int)Math.Floor(value);
                }

                if (mapControl._core.IsStarted)
                {
                    mapControl.ForceUpdateOverlays();
                    mapControl.InvalidateVisual(true);
                }
            }
        }

        private static void ZoomPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ZoomPropertyChanged((GMapControl)d, (double)e.NewValue, (double)e.OldValue, ZoomMode.XY);
        }

        private static void ZoomXPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ZoomPropertyChanged((GMapControl)d, (double)e.NewValue, (double)e.OldValue, ZoomMode.X);
        }

        private static void ZoomYPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ZoomPropertyChanged((GMapControl)d, (double)e.NewValue, (double)e.OldValue, ZoomMode.Y);
        }

        private static void OnMultiTouchEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var mapControl = (GMapControl)d;
            mapControl.MultiTitleEnabledOrManipulation = (bool)e.NewValue;
        }

        public bool MultiTitleEnabledOrManipulation { get; set; }

        [Obsolete("Touch Enabled is deprecated, please use MultiTouchEnabled")]
        public bool TouchEnabled {
            get { return (bool)GetValue(TouchEnabledProperty); }
            set { SetValue(TouchEnabledProperty, value); }
        }

        readonly ScaleTransform _lastScaleTransform = new ScaleTransform();
        private ScaleModes _scaleMode = ScaleModes.Integer;

        #endregion

        readonly GMap.NET.Internals.Core _core = new GMap.NET.Internals.Core();
        PointLatLng _selectionStart;
        PointLatLng _selectionEnd;
        bool _showTileGridLines;

        [Browsable(false)]
        public bool FillEmptyTiles {
            get { return _core.FillEmptyTiles; }
            set { _core.FillEmptyTiles = value; }
        }

        [Category("GMap.NET")]
        [Description("maximum zoom level of map")]
        public int MaxZoom {
            get { return _core.MaxZoom; }
            set { _core.MaxZoom = value; }
        }

        [Category("GMap.NET")]
        [Description("minimum zoom level of map")]
        public int MinZoom {
            get { return _core.MinZoom; }
            set { _core.MinZoom = value; }
        }

        [Category("GMap.NET")]
        [Description("map zooming type for mouse wheel")]
        public MouseWheelZoomType MouseWheelZoomType {
            get { return _core.MouseWheelZoomType; }
            set { _core.MouseWheelZoomType = value; }
        }

        [Category("GMap.NET")]
        [Description("enable map zoom on mouse wheel")]
        public bool MouseWheelZoomEnabled {
            get { return _core.MouseWheelZoomEnabled; }
            set { _core.MouseWheelZoomEnabled = value; }
        }

        [Category("GMap.NET")]
        public bool ShowTileGridLines {
            get { return _showTileGridLines; }
            set {
                _showTileGridLines = value;
                InvalidateVisual();
            }
        }

        [Browsable(false)]
        public int RetryLoadTile {
            get { return _core.RetryLoadTile; }
            set { _core.RetryLoadTile = value; }
        }

        [Browsable(false)]
        public int LevelsKeepInMemory {
            get { return _core.LevelsKeepInMemory; }
            set { _core.LevelsKeepInMemory = value; }
        }

        private RectLatLng _selectedArea;

        [Browsable(false)]
        public RectLatLng SelectedArea {
            get { return _selectedArea; }
            set {
                _selectedArea = value;
                InvalidateVisual();
            }
        }

        public RectLatLng? BoundsOfMap = null;
        public event SelectionChange OnSelectionChange;
        public event PositionChanged OnPositionChanged;
        public event TileLoadComplete OnTileLoadComplete;
        public event TileLoadStart OnTileLoadStart;
        public event MapDrag OnMapDrag;
        public event MapZoomChanged OnMapZoomChanged;
        public event MapTypeChanged OnMapTypeChanged;

        private static readonly DependencyPropertyKey MarkersKey =
            DependencyProperty.RegisterReadOnly("Markers",
                typeof(ObservableCollection<GMapMarker>),
                typeof(GMapControl),
                new PropertyMetadata(null));

        public static readonly DependencyProperty MarkersProperty = MarkersKey.DependencyProperty;

        public ObservableCollection<GMapMarker> Markers {
            get { return (ObservableCollection<GMapMarker>)GetValue(MarkersProperty); }
            private set { SetValue(MarkersKey, value); }
        }

        internal readonly TranslateTransform MapTranslateTransform = new TranslateTransform();
        internal readonly TranslateTransform MapOverlayTranslateTransform = new TranslateTransform();
        internal ScaleTransform MapScaleTransform = new ScaleTransform();
        internal RotateTransform MapRotateTransform = new RotateTransform();

        protected bool DesignModeInConstruct => Windows.ApplicationModel.DesignMode.DesignModeEnabled;

        Canvas _mapCanvas;

        internal Canvas MapCanvas {
            get {
                if (_mapCanvas == null && VisualTreeHelper.GetChildrenCount(this) > 0)
                {
                    var border = VisualTreeHelper.GetChild(this, 0) as Border;
                    if (border?.Child is ItemsPresenter items)
                    {
                        var target = VisualTreeHelper.GetChild(items, 0);
                        _mapCanvas = target as Canvas;
                        if (_mapCanvas != null)
                        {
                            _mapCanvas.RenderTransform = MapTranslateTransform;
                        }
                    }
                }
                return _mapCanvas;
            }
        }

        public GMaps Manager => GMaps.Instance;

        public GMapControl()
        {
            if (!DesignModeInConstruct)
            {
                Markers = new ObservableCollection<GMapMarker>();
                UseLayoutRounding = true;

                _core.SystemType = "WindowsUniversal";
                _core.RenderMode = RenderMode.WPF;

                _core.OnMapZoomChanged += ForceUpdateOverlays;
                _core.OnCurrentPositionChanged += CoreOnCurrentPositionChanged;

                Loaded += GMapControl_Loaded;
                SizeChanged += GMapControl_SizeChanged;

                // Eventos de puntero unificados en UWP
                PointerPressed += GMapControl_PointerPressed;
                PointerMoved += GMapControl_PointerMoved;
                PointerReleased += GMapControl_PointerReleased;
                PointerWheelChanged += GMapControl_PointerWheelChanged;

                if (ItemsSource == null)
                    ItemsSource = Markers;

                _core.Zoom = 2;
            }
        }

        private void CoreOnCurrentPositionChanged(PointLatLng pointLatLng)
        {
            Position = pointLatLng;
        }

        static GMapControl()
        {
            GMapImageProxy.Enable();
            GMaps.Instance.SQLitePing();
        }

        void InvalidatorEngage(object sender, ProgressChangedEventArgs e)
        {
            InvalidateVisual();
        }

        public void InvalidateVisual()
        {
            _core.Refresh?.Set();
        }

        public void InvalidateVisual(bool forced)
        {
            if (forced)
            {
                lock (_core.InvalidationLock)
                {
                    _core.LastInvalidation = DateTime.Now;
                }
            }
            InvalidateVisual();
        }

        void GMapControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_core.IsStarted)
            {
                _core.OnMapOpen().ProgressChanged += InvalidatorEngage;
                ForceUpdateOverlays();
            }
        }

        void GMapControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var constraint = e.NewSize;
            _core.OnMapSizeChanged((int)constraint.Width, (int)constraint.Height);

            if (_core.IsStarted)
            {
                if (IsRotated) UpdateRotationMatrix();
                ForceUpdateOverlays();
            }
        }

        void ForceUpdateOverlays()
        {
            ForceUpdateOverlays(ItemsSource);
        }

        public virtual void RegenerateShape(IShapable s)
        {
            if (s is GMapMarker marker && s.Points != null && s.Points.Count > 1)
            {
                marker.Position = s.Points[0];
                var localPath = new List<Point>(s.Points.Count);
                var offset = FromLatLngToLocal(s.Points[0]);

                foreach (var i in s.Points)
                {
                    var p = FromLatLngToLocal(i);
                    localPath.Add(new Point(p.X - offset.X, p.Y - offset.Y));
                }

                var shape = s.CreatePath(localPath, true);
                if (marker.Shape is Path path)
                {
                    path.Data = shape.Data;
                }
                else
                {
                    marker.Shape = shape;
                }
            }
            else if (s is GMapMarker m2)
            {
                m2.Shape = null;
            }
        }

        void ForceUpdateOverlays(System.Collections.IEnumerable items)
        {
            UpdateMarkersOffset();

            foreach (GMapMarker i in items)
            {
                if (i != null)
                {
                    i.ForceUpdateLocalPosition(this);
                    if (i is IShapable shapable)
                        RegenerateShape(shapable);
                }
            }

            InvalidateVisual();
        }

        void UpdateMarkersOffset()
        {
            if (MapCanvas != null)
            {
                if (MapScaleTransform != null)
                {
                    var tp = MapScaleTransform.Transform(new Point(_core.RenderOffset.X, _core.RenderOffset.Y));
                    MapOverlayTranslateTransform.X = tp.X;
                    MapOverlayTranslateTransform.Y = tp.Y;
                }
                MapTranslateTransform.X = _core.RenderOffset.X;
                MapTranslateTransform.Y = _core.RenderOffset.Y;
            }
        }

        public bool SetZoomToFitRect(RectLatLng rect)
        {
            int maxZoom = _core.GetMaxZoomToFitRect(rect);
            if (maxZoom > 0)
            {
                Position = new PointLatLng(rect.Lat - rect.HeightLat / 2, rect.Lng + rect.WidthLng / 2);
                if (maxZoom > MaxZoom) maxZoom = MaxZoom;
                if (_core.Zoom != maxZoom) Zoom = maxZoom;
                return true;
            }
            return false;
        }

        public PointLatLng FromLocalToLatLng(int x, int y)
        {
            if (MapScaleTransform != null)
            {
                var tp = MapScaleTransform.Inverse.Transform(new Point(x, y));
                x = (int)tp.X;
                y = (int)tp.Y;
            }
            if (IsRotated)
            {
                var f = _rotationMatrixInvert.Transform(new Point(x, y));
                x = (int)f.X;
                y = (int)f.Y;
            }
            return _core.FromLocalToLatLng(x, y);
        }

        public GPoint FromLatLngToLocal(PointLatLng point)
        {
            var ret = _core.FromLatLngToLocal(point);
            if (MapScaleTransform != null)
            {
                var tp = MapScaleTransform.Transform(new Point(ret.X, ret.Y));
                ret.X = (int)tp.X;
                ret.Y = (int)tp.Y;
            }
            if (IsRotated)
            {
                var f = _rotationMatrix.Transform(new Point(ret.X, ret.Y));
                ret.X = (int)f.X;
                ret.Y = (int)f.Y;
            }
            return ret;
        }

        readonly RotateTransform _rotationMatrix = new RotateTransform();
        GeneralTransform _rotationMatrixInvert = new RotateTransform();

        void UpdateRotationMatrix()
        {
            var center = new Point(ActualWidth / 2.0, ActualHeight / 2.0);
            _rotationMatrix.Angle = -Bearing;
            _rotationMatrix.CenterY = center.Y;
            _rotationMatrix.CenterX = center.X;
            _rotationMatrixInvert = _rotationMatrix.Inverse;
        }

        public bool IsRotated => _core.IsRotated;

        [Category("GMap.NET")]
        public float Bearing {
            get => _core.Bearing;
            set {
                if (_core.Bearing != value)
                {
                    bool resize = _core.Bearing == 0;
                    _core.Bearing = value;
                    UpdateRotationMatrix();
                    _core.IsRotated = (value != 0 && value % 360 != 0);
                    if (resize) _core.OnMapSizeChanged((int)ActualWidth, (int)ActualHeight);
                    if (_core.IsStarted) ForceUpdateOverlays();
                }
            }
        }

        #region Eventos de Puntero UWP (Reemplazo de Mouse/Touch/Stylus)

        private void GMapControl_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var pt = e.GetCurrentPoint(this);
            if (CanDragMap && pt.Properties.IsRightButtonPressed)
            {
                var pos = pt.Position;
                if (MapScaleTransform != null) pos = MapScaleTransform.Inverse.Transform(pos);

                _core.MouseDown.X = (int)pos.X;
                _core.MouseDown.Y = (int)pos.Y;
                CapturePointer(e.Pointer);
                InvalidateVisual();
            }
        }

        private void GMapControl_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var pt = e.GetCurrentPoint(this);
            if (_core.IsDragging)
            {
                var pos = pt.Position;
                if (MapScaleTransform != null) pos = MapScaleTransform.Inverse.Transform(pos);

                _core.MouseCurrent.X = (int)pos.X;
                _core.MouseCurrent.Y = (int)pos.Y;
                _core.Drag(_core.MouseCurrent);

                UpdateMarkersOffset();
                InvalidateVisual(true);
            }
            else if (!_core.MouseDown.IsEmpty && pt.Properties.IsRightButtonPressed)
            {
                var pos = pt.Position;
                if (MapScaleTransform != null) pos = MapScaleTransform.Inverse.Transform(pos);

                if (Math.Abs(pos.X - _core.MouseDown.X) >= 3 || Math.Abs(pos.Y - _core.MouseDown.Y) >= 3)
                {
                    _core.BeginDrag(_core.MouseDown);
                    IsDragging = true;
                }
            }
        }

        private void GMapControl_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_core.IsDragging)
            {
                IsDragging = false;
                _core.EndDrag();
                ReleasePointerCaptures();
            }
            _core.MouseDown = GPoint.Empty;
            InvalidateVisual();
        }

        private void GMapControl_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var pt = e.GetCurrentPoint(this);
            int delta = pt.Properties.MouseWheelDelta;

            if (MouseWheelZoomEnabled && !_core.IsDragging)
            {
                if (delta > 0) Zoom = (int)Zoom + 1;
                else Zoom = Math.Max(MinZoom, (int)Zoom - 1);
            }
        }

        #endregion

        public static readonly DependencyProperty PositionProperty = DependencyProperty.Register(
            "Position",
            typeof(PointLatLng),
            typeof(GMapControl),
            new PropertyMetadata(default(PointLatLng), PositionChangedCallBack));

        [Browsable(false)]
        public PointLatLng Position {
            get => (PointLatLng)GetValue(PositionProperty);
            set => SetValue(PositionProperty, value);
        }

        private static void PositionChangedCallBack(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            if (source is GMapControl ctrl) ctrl.PositionChanged();
        }

        private void PositionChanged()
        {
            _core.Position = Position;
            if (_core.IsStarted) ForceUpdateOverlays();
        }

        [Browsable(false)] public bool IsDragging { get; private set; }

        [Browsable(false)]
        public RectLatLng ViewArea {
            get {
                if (!IsRotated) return _core.ViewArea;
                var p = FromLocalToLatLng(0, 0);
                var p2 = FromLocalToLatLng((int)Width, (int)Height);
                return RectLatLng.FromLTRB(p.Lng, p.Lat, p2.Lng, p2.Lat);
            }
        }

        [Category("GMap.NET")]
        public bool CanDragMap {
            get => _core.CanDragMap;
            set => _core.CanDragMap = value;
        }

        public RenderMode RenderMode => RenderMode.WPF;

        public GPoint PositionPixel => throw new NotImplementedException();

        public string CacheLocation { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public virtual void Dispose()
        {
            if (_core.IsStarted)
            {
                _core.OnMapZoomChanged -= ForceUpdateOverlays;
                Loaded -= GMapControl_Loaded;
                SizeChanged -= GMapControl_SizeChanged;
                _core.OnMapClose();
            }
        }

        public void ReloadMap()
        {
            throw new NotImplementedException();
        }

        public bool ShowExportDialog()
        {
            throw new NotImplementedException();
        }

        public bool ShowImportDialog()
        {
            throw new NotImplementedException();
        }
    }

    public enum HelperLineOptions { DontShow = 0, ShowAlways = 1, ShowOnModifierKey = 2 }
    public enum ScaleModes { Integer, ScaleUp, ScaleDown, Dynamic }
    public enum ZoomMode { X, Y, XY }
    public delegate void SelectionChange(RectLatLng selection, bool zoomToFit);
}