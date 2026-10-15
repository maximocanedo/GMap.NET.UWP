using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using GMap.NET.CacheProviders;
using Windows.Storage;

namespace GMap.NET.Internals
{
    internal class CacheLocator
    {
        private static string _location;

        public static string Location {
            get {
                if (string.IsNullOrEmpty(_location))
                {
                    Reset();
                }

                return _location;
            }
            set {
                if (string.IsNullOrEmpty(value))
                {
                    Reset();
                }
                else
                {
                    _location = value;
                }

                if (Delay)
                {
                    Cache.Instance.CacheLocation = _location;
                }
            }
        }

        static void Reset()
        {
            string appDataLocation = GetApplicationDataFolderPath();

            if (string.IsNullOrEmpty(appDataLocation))
            {
                GMaps.Instance.Mode = AccessMode.ServerOnly;
                GMaps.Instance.UseDirectionsCache = false;
                GMaps.Instance.UseGeocoderCache = false;
                GMaps.Instance.UsePlacemarkCache = false;
                GMaps.Instance.UseRouteCache = false;
                GMaps.Instance.UseUrlCache = false;
            }
            else
            {
                Location = appDataLocation;
            }
        }

        public static string GetApplicationDataFolderPath()
        {
            string path = string.Empty;
            try
            {
                // En UWP utilizamos la carpeta local de la aplicación
                path = ApplicationData.Current.LocalFolder.Path;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("CacheLocator, LocalFolder path error: " + ex);
            }

            if (!string.IsNullOrEmpty(path))
            {
                path += Path.DirectorySeparatorChar + "GMap.NET" + Path.DirectorySeparatorChar;
            }

            return path;
        }

        public static bool Delay;
    }

    internal class Cache
    {
        public PureImageCache ImageCache;
        public PureImageCache ImageCacheSecond;

        string _cache;

        public string CacheLocation {
            get {
                return _cache;
            }
            set {
                _cache = value;
#if SQLite
                if (ImageCache is SQLitePureImageCache)
                {
                    (ImageCache as SQLitePureImageCache).CacheLocation = value;
                }
#endif
                CacheLocator.Delay = true;
            }
        }

        public static Cache Instance { get; } = new Cache();

        private Cache()
        {
#if SQLite
            ImageCache = new SQLitePureImageCache();
#else
            ImageCache = new MsSQLCePureImageCache();
#endif

            {
                string newCache = CacheLocator.Location;
                CacheLocation = newCache;
            }
        }

        #region -- etc cache --

        void ConvertToHash(ref string s)
        {
            using (var sha1 = SHA1.Create())
            {
                s = BitConverter.ToString(sha1.ComputeHash(Encoding.Unicode.GetBytes(s)));
            }
        }

        public void SaveContent(string url, CacheType type, string content)
        {
            try
            {
                ConvertToHash(ref url);

                string dir = Path.Combine(_cache, type.ToString()) + Path.DirectorySeparatorChar;

                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string file = dir + url + ".txt";

                // En UWP StreamWriter(path, append, encoding) requiere manejar flujos o usar File.WriteAllText
                File.WriteAllText(file, content, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SaveContent: " + ex);
            }
        }

        public string GetContent(string url, CacheType type, TimeSpan stayInCache)
        {
            string ret = null;

            try
            {
                ConvertToHash(ref url);

                string dir = Path.Combine(_cache, type.ToString()) + Path.DirectorySeparatorChar;
                string file = dir + url + ".txt";

                if (File.Exists(file))
                {
                    var writeTime = File.GetLastWriteTime(file);
                    if (DateTime.Now - writeTime < stayInCache)
                    {
                        ret = File.ReadAllText(file, Encoding.UTF8);
                    }
                    else
                    {
                        File.Delete(file);
                    }
                }
            }
            catch (Exception ex)
            {
                ret = null;
                Debug.WriteLine("GetContent: " + ex);
            }

            return ret;
        }

        public string GetContent(string url, CacheType type)
        {
            return GetContent(url, type, TimeSpan.FromDays(100));
        }

        #endregion
    }

    internal enum CacheType
    {
        GeocoderCache,
        PlacemarkCache,
        RouteCache,
        UrlCache,
        DirectionsCache,
    }
}