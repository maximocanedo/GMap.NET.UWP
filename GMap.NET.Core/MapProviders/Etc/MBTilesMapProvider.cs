using System;
using System.Collections.Generic;
using System.IO;
using GMap.NET.Projections;
using SQLite;

namespace GMap.NET.MapProviders
{
#if SQLite && !MONO

    /// <summary>Map provider for MBTiles files (https://github.com/mapbox/mbtiles-spec/blob/master/1.3/spec.md)</summary>
    public class MBTilesMapProvider : GMapProvider
    {
        public static readonly MBTilesMapProvider Instance;

        private class MBTiles
        {
            private SQLiteConnection db = null;
            public Dictionary<string, string> metadata = new Dictionary<string, string>();

            public MBTiles(string file)
            {
                // SQLite-net abre la conexión directamente pasándole la ruta del archivo
                db = new SQLiteConnection(file);

                // Consulta a la tabla metadata usando SQLite-net
                var metaList = db.Query<MetadataRow>("SELECT name, value FROM metadata;");
                foreach (var row in metaList)
                {
                    if (!string.IsNullOrEmpty(row.name))
                    {
                        metadata[row.name] = row.value;
                    }
                }
            }

            // Clase auxiliar para mapear los resultados de la tabla metadata
            public class MetadataRow
            {
                public string name { get; set; }
                public string value { get; set; }
            }

            ~MBTiles()
            {
                if (db != null)
                {
                    db.Dispose();
                    db = null;
                }
            }

            public PureImage GetImage(GPoint pos, int zoom)
            {
                PureImage ret = null;
                try
                {
                    long invertedY = ((long)Math.Pow(2, zoom) - 1) - pos.Y;
                    string query = "SELECT tile_data FROM tiles WHERE zoom_level=? AND tile_column=? AND tile_row=? LIMIT 1";

                    var tileRecord = db.FindWithQuery<TileRow>(query, zoom, pos.X, invertedY);

                    if (tileRecord != null && tileRecord.tile_data != null && tileRecord.tile_data.Length > 0)
                    {
                        if (GMapProvider.TileImageProxy != null)
                        {
                            ret = GMapProvider.TileImageProxy.FromArray(tileRecord.tile_data);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("MBTiles GetImage error: " + ex.Message);
                }
                return ret;
            }

            // Clase auxiliar para mapear los bytes del tile
            public class TileRow
            {
                public byte[] tile_data { get; set; }
            }

            public int GetZoomMin()
            {
                try
                {
                    return db.ExecuteScalar<int>("SELECT MIN(zoom_level) FROM tiles;");
                }
                catch { }
                return -1;
            }

            public int GetZoomMax()
            {
                try
                {
                    return db.ExecuteScalar<int>("SELECT MAX(zoom_level) FROM tiles;");
                }
                catch { }
                return -1;
            }
        }

        private MBTiles source = null;

        MBTilesMapProvider()
        {
        }

        static MBTilesMapProvider()
        {
            Instance = new MBTilesMapProvider();
        }

        #region Properties
        public string DataName { get; private set; }
        public string Format { get; private set; }
        public PointLatLng[] Bounds { get; private set; }
        public PointLatLng CenterLocation { get; private set; }
        public int CenterZoom { get; private set; }
        public int MinZoom { get; private set; }
        public int MaxZoom { get; private set; }
        public string Attribution { get; private set; }
        public string Description { get; private set; }
        public string Type { get; private set; }
        public long Version { get; private set; }

        public Dictionary<string, string> Metadata {
            get {
                return source != null ? source.metadata : new Dictionary<string, string>();
            }
        }
        #endregion

        #region GMapProvider Members

        public override Guid Id {
            get;
        } = new Guid("CD2A114E-188C-423F-BBCC-FB7849333AE4");

        public override string Name {
            get;
        } = "MBTilesMapProvider";

        GMapProvider[] _overlays;

        public override GMapProvider[] Overlays {
            get {
                if (_overlays == null)
                {
                    _overlays = new GMapProvider[] { this };
                }

                return _overlays;
            }
        }

        public override PureImage GetTileImage(GPoint pos, int zoom)
        {
            if (source == null) return null;
            if (zoom < MinZoom || zoom > MaxZoom) return null;
            return source.GetImage(pos, zoom);
        }

        public override PureProjection Projection {
            get {
                return MercatorProjection.Instance;
            }
        }
        #endregion

        public bool Open(string MBTilesFilePath)
        {
            if (!File.Exists(MBTilesFilePath)) return false;
            try
            {
                source = new MBTiles(MBTilesFilePath);
                DataName = String.Empty;
                Format = String.Empty;
                Bounds = null;
                CenterLocation = PointLatLng.Empty;
                CenterZoom = -1;
                MinZoom = -1;
                MaxZoom = -1;
                Attribution = String.Empty;
                Description = String.Empty;
                Type = String.Empty;
                Version = 0;
                foreach (var kvp in source.metadata)
                {
                    switch (kvp.Key.ToLower())
                    {
                        case "name": DataName = kvp.Value; break;
                        case "format": Format = kvp.Value; break;
                        case "bounds":
                            string[] tmp1 = kvp.Value.Split(',');
                            if (tmp1.Length == 4)
                            {
                                Bounds = new PointLatLng[2]
                                {
                                    new PointLatLng(double.Parse(tmp1[3], System.Globalization.CultureInfo.InvariantCulture),double.Parse(tmp1[0], System.Globalization.CultureInfo.InvariantCulture)),
                                    new PointLatLng(double.Parse(tmp1[1], System.Globalization.CultureInfo.InvariantCulture), double.Parse(tmp1[2], System.Globalization.CultureInfo.InvariantCulture))
                                };
                            }
                            break;
                        case "center":
                            string[] tmp2 = kvp.Value.Split(',');
                            if (tmp2.Length == 3)
                            {
                                CenterLocation = new PointLatLng(double.Parse(tmp2[1], System.Globalization.CultureInfo.InvariantCulture), double.Parse(tmp2[0], System.Globalization.CultureInfo.InvariantCulture));
                                CenterZoom = int.Parse(tmp2[2]);
                            }
                            break;
                        case "minzoom": MinZoom = int.Parse(kvp.Value); break;
                        case "maxzoom": MaxZoom = int.Parse(kvp.Value); break;
                        case "attribution": Attribution = kvp.Value; break;
                        case "description": Description = kvp.Value; break;
                        case "type": Type = kvp.Value; break;
                        case "version": Version = long.Parse(kvp.Value); break;
                        default: break;
                    }

                }
                if (string.IsNullOrEmpty(DataName) || string.IsNullOrEmpty(Format))
                {
                    source = null;
#if DEBUG
                    System.Diagnostics.Debug.WriteLine("[MBTilesMapProvider] Metafields 'name' and 'format' are required!");
#endif
                    return false;
                }
                if (Format.ToLower() == "pbf" && !Metadata.ContainsKey("json"))
                {
                    source = null;
#if DEBUG
                    System.Diagnostics.Debug.WriteLine("[MBTilesMapProvider] If the format is pbf, the metadata table MUST contain 'json' row!");
#endif
                    return false;
                }
                if (MinZoom < 0) MinZoom = source.GetZoomMin();
                if (MaxZoom < 0) MaxZoom = source.GetZoomMax();
                if (Bounds != null && CenterLocation == PointLatLng.Empty) CenterLocation = new PointLatLng(Bounds[0].Lat - 0.5 * (Bounds[1].Lat - Bounds[0].Lat), Bounds[0].Lng + 0.5 * (Bounds[1].Lng - Bounds[0].Lng));
                if (CenterZoom < 0) CenterZoom = MinZoom + (MaxZoom - MinZoom) / 2;

                return true;
            }
            catch { return false; }
        }
    }

#endif
}
