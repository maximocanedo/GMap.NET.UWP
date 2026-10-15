using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using GMap.NET.Internals;
using GMap.NET.MapProviders;
using Windows.UI.Core;

namespace GMap.NET.WindowsPresentation
{
    public sealed partial class TilePrefetcher : ContentDialog
    {
        readonly BackgroundWorker _worker = new BackgroundWorker();
        List<GPoint> _list = new List<GPoint>();
        int _zoom;
        GMapProvider _provider;
        int _sleep;
        int _all;
        public bool ShowCompleteMessage = false;
        RectLatLng _area;
        GSize _maxOfTiles;
        volatile bool _stopped;
        readonly AutoResetEvent _done = new AutoResetEvent(true);

        public TilePrefetcher()
        {
            InitializeComponent();

            GMaps.Instance.OnTileCacheComplete += OnTileCacheComplete;
            GMaps.Instance.OnTileCacheStart += OnTileCacheStart;
            GMaps.Instance.OnTileCacheProgress += OnTileCacheProgress;

            _worker.WorkerReportsProgress = true;
            _worker.WorkerSupportsCancellation = true;
            _worker.ProgressChanged += worker_ProgressChanged;
            _worker.DoWork += worker_DoWork;
            _worker.RunWorkerCompleted += worker_RunWorkerCompleted;
        }

        void OnTileCacheComplete()
        {
            _done.Set();
            Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Label2.Text = "all tiles saved";
            });
        }

        void OnTileCacheStart()
        {
            _done.Reset();
            Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Label2.Text = "saving tiles...";
            });
        }

        void OnTileCacheProgress(int left)
        {
            Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Label2.Text = left + " tile to save...";
            });
        }

        public async void Start(RectLatLng area, int zoom, GMapProvider provider, int sleep)
        {
            if (!_worker.IsBusy)
            {
                Label1.Text = "...";
                ProgressBar1.Value = 0;

                _area = area;
                _zoom = zoom;
                _provider = provider;
                _sleep = sleep;

                GMaps.Instance.UseMemoryCache = false;
                GMaps.Instance.CacheOnIdleRead = false;
                GMaps.Instance.BoostCacheEngine = true;

                _worker.RunWorkerAsync();

                await ShowAsync();
            }
        }

        public void Stop()
        {
            GMaps.Instance.OnTileCacheComplete -= OnTileCacheComplete;
            GMaps.Instance.OnTileCacheStart -= OnTileCacheStart;
            GMaps.Instance.OnTileCacheProgress -= OnTileCacheProgress;

            _done.Set();

            if (_worker.IsBusy)
            {
                _worker.CancelAsync();
            }

            GMaps.Instance.CancelTileCaching();
            _stopped = true;
            _done.Dispose();
        }

        async void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (ShowCompleteMessage)
            {
                string msg = !e.Cancelled ? "Prefetch Complete! => " + e.Result + " of " + _all : "Prefetch Canceled! => " + e.Result + " of " + _all;
                var dialog = new MessageDialog(msg);
                await dialog.ShowAsync();
            }

            _list?.Clear();

            GMaps.Instance.UseMemoryCache = true;
            GMaps.Instance.CacheOnIdleRead = true;
            GMaps.Instance.BoostCacheEngine = false;

            Hide();
        }

        bool CacheTiles(int zoom, GPoint p)
        {
            foreach (var type in _provider.Overlays)
            {
                Exception ex;
                PureImage img = type is TurkeyMapProvider
                    ? GMaps.Instance.GetImageFrom(type, new GPoint(p.X, _maxOfTiles.Height - p.Y), zoom, out ex)
                    : GMaps.Instance.GetImageFrom(type, p, zoom, out ex);

                if (img != null)
                {
                    img.Dispose();
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            _list?.Clear();
            _list = _provider.Projection.GetAreaTileList(_area, _zoom, 0);
            _maxOfTiles = _provider.Projection.GetTileMatrixMaxXY(_zoom);
            _all = _list.Count;

            int countOk = 0;
            int retry = 0;

            Stuff.Shuffle(_list);

            for (int i = 0; i < _all; i++)
            {
                if (_worker.CancellationPending) break;

                var p = _list[i];
                if (CacheTiles(_zoom, p))
                {
                    countOk++;
                    retry = 0;
                }
                else
                {
                    if (++retry <= 1)
                    {
                        i--;
                        Task.Delay(1111);
                        continue;
                    }
                    retry = 0;
                }

                _worker.ReportProgress((i + 1) * 100 / _all, i + 1);
                Task.Delay(_sleep);
            }

            e.Result = countOk;

            if (!_stopped)
            {
                _done.WaitOne();
            }
        }

        void worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            Label1.Text = "Fetching tile at zoom (" + _zoom + "): " + e.UserState + " of " + _all + ", complete: " + e.ProgressPercentage + "%";
            ProgressBar1.Value = e.ProgressPercentage;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            Stop();
        }
    }
}