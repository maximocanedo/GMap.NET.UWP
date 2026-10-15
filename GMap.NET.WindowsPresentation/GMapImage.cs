using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using GMap.NET.MapProviders;

namespace GMap.NET.WindowsPresentation
{
    /// <summary>
    /// Abstracción de imagen para UWP
    /// </summary>
    public class GMapImage : PureImage
    {
        public ImageSource Img;

        public override void Dispose()
        {
            if (Img != null)
            {
                Img = null;
            }

            if (Data != null)
            {
                Data.Dispose();
                Data = null;
            }
        }
    }

    /// <summary>
    /// Proxy de abstracción de imagen para UWP
    /// </summary>
    public class GMapImageProxy : PureImageProxy
    {
        private GMapImageProxy()
        {
        }

        public static void Enable()
        {
            GMapProvider.TileImageProxy = Instance;
        }

        public static readonly GMapImageProxy Instance = new GMapImageProxy();

        public override PureImage FromStream(Stream stream)
        {
            if (stream != null)
            {
                try
                {
                    GMapImage ret = null;

                    // UWP requiere que la creación de BitmapImage se realice en el hilo de la UI
                    var dispatcher = CoreApplication.MainView.CoreWindow?.Dispatcher;
                    if (dispatcher != null)
                    {
                        var task = dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                        {
                            var bmp = new BitmapImage();
                            using (var randomAccessStream = stream.AsRandomAccessStream())
                            {
                                await bmp.SetSourceAsync(randomAccessStream);
                            }
                            ret = new GMapImage { Img = bmp };
                        });

                        task.AsTask().Wait();
                    }

                    return ret;
                }
                catch (Exception ex)
                {
                    stream.Position = 0;
                    int type = stream.Length > 0 ? stream.ReadByte() : 0;
                    Debug.WriteLine("WindowsUniversalImageProxy: unknown image format: " + type + " - " + ex.Message);
                }
            }

            return null;
        }

        public override bool Save(Stream stream, PureImage image)
        {
            if (image?.Data != null)
            {
                try
                {
                    image.Data.Position = 0;
                    image.Data.CopyTo(stream);
                    return true;
                }
                catch
                {
                    // ignore
                }
            }

            return false;
        }
    }

    /* 
    // Nota: TileVisual dependía de DrawingVisual y FrameworkElement de WPF. 
    // En UWP se puede omitir o reimplementar utilizando paneles o Win2D (CanvasControl) si fuera necesario renderizar vectores personalizados.
    */
}