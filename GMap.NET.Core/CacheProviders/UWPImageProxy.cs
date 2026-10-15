using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Media.Imaging;

namespace GMap.NET.CacheProviders
{
    public class UWPImageProxy : PureImageProxy
    {
        public static readonly UWPImageProxy Instance = new UWPImageProxy();

        private UWPImageProxy()
        {
        }

        public override PureImage FromStream(Stream stream)
        {
            PureImage ret = null;
            try
            {
                ret = new UwpImage(stream);
}
            catch (Exception ex)
            {
                ret = null;
                System.Diagnostics.Debug.WriteLine("UWPImageProxy from stream: " + ex.Message);
            }
            return ret;
        }

        public override bool Save(Stream stream, PureImage image)
{
    UwpImage img = image as UwpImage;
    if (img != null && img.Data != null)
    {
        img.Data.WriteTo(stream);
        return true;
    }
    return false;
}
    }

    public class UwpImage : PureImage
{
    public BitmapImage Img;

    public UwpImage(Stream stream)
    {
        // Convertir el stream estándar a un MemoryStream accesible para UWP
        MemoryStream ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Position = 0;

        // En el hilo de ejecución principal de la UI se asigna la fuente de la imagen
        Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
            Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                Img = new BitmapImage();
                using (var randomAccessStream = ms.AsRandomAccessStream())
                {
                    await Img.SetSourceAsync(randomAccessStream);
                }
            }).AsTask().Wait();
    }

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
}
