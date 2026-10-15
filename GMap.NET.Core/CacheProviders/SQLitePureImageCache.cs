using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Threading;
using GMap.NET.MapProviders;
using SQLite;
using SQLiteConnection = SQLite.SQLiteConnection;
using SQLiteCommand = SQLite.SQLiteCommand;
using Trace = System.Diagnostics.Debug;

namespace GMap.NET.CacheProviders
{
#if SQLite

#if !MONO
#else
   using SQLiteConnection = Mono.Data.Sqlite.SqliteConnection;
   using SQLiteTransaction = Mono.Data.Sqlite.SqliteTransaction;
   using SQLiteCommand = Mono.Data.Sqlite.SqliteCommand;
   using SQLiteDataReader = Mono.Data.Sqlite.SqliteDataReader;
   using SQLiteParameter = Mono.Data.Sqlite.SqliteParameter;
#endif

    /// <summary>
    ///     ultra fast cache system for tiles
    /// </summary>
    public class SQLitePureImageCache : PureImageCache
    {
#if !MONO
#if !NETFX_CORE && !WINDOWS_UWP
        static SQLitePureImageCache()
        {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
        }

        static System.Reflection.Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {            
            return null;
        }

#endif

        static int _ping;

        /// <summary>
        ///     triggers dynamic sqlite loading
        /// </summary>
        public static void Ping()
        {
            if (++_ping == 1)
            {
                Trace.WriteLine("SQLite Initialized (UWP)");
            }
        }
#endif

        string _cache;
        string _dir;
        string _db;
        bool _created;
        string _connectionString;

        public string GtileCache
        {
            get;
            private set;
        }

        /// <summary>
        ///     local cache location
        /// </summary>
        public string CacheLocation {
            get { return _cache; }
            set {
                _cache = value;
                GtileCache = Path.Combine(_cache, "TileDBv5") + Path.DirectorySeparatorChar;
                _dir = GtileCache + GMapProvider.LanguageStr + Path.DirectorySeparatorChar;

                if (!Directory.Exists(_dir))
                {
                    Directory.CreateDirectory(_dir);
                }

                _db = _dir + "Data.gmdb";

                if (!File.Exists(_db))
                {
                    _created = CreateEmptyDB(_db);
                }
                else
                {
                    _created = AlterDBAddTimeColumn(_db);
                }

                CheckPreAllocation();
                _connectionString = _db;

                _attachedCaches.Clear();
                RebuildFinnalSelect();

                string[] dbs = Directory.GetFiles(_dir, "*.gmdb", SearchOption.AllDirectories);
                foreach (string d in dbs)
                {
                    if (d != _db)
                        Attach(d);
                }
            }
        }

        /// <summary>
        ///     pre-allocate 32MB free space 'ahead' if needed,
        ///     decreases fragmentation
        /// </summary>
        void CheckPreAllocation()
        {
            try
            {
                if (File.Exists(_db))
                {
                    FileInfo fi = new FileInfo(_db);
                    Debug.WriteLine("Cache size: " + fi.Length / (1024.0 * 1024.0) + "MB");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("CheckPreAllocation: " + ex.Message);
            }
        }

        #region -- import / export --

        public static bool CreateEmptyDB(string file)
        {
            try
            {
                string dir = Path.GetDirectoryName(file);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                using (var cn = new SQLiteConnection(file))
                {
                    cn.Execute("CREATE TABLE IF NOT EXISTS Tiles (id INTEGER PRIMARY KEY AUTOINCREMENT, X INTEGER, Y INTEGER, Zoom INTEGER, Type INTEGER, CacheTime DATETIME);");
                    cn.Execute("CREATE TABLE IF NOT EXISTS TilesData (id INTEGER, Tile BLOB);");
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("CreateEmptyDB: " + ex.ToString());
                return false;
            }
        }

        public static bool PreAllocateDB(string file, int addSizeInMBytes)
        {
            try
            {
                using (var cn = new SQLiteConnection(file))
                {
                    cn.Execute(string.Format("create table large (a); insert into large values (zeroblob({0})); drop table large;", addSizeInMBytes * 1024 * 1024));
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("PreAllocateDB: " + ex.ToString());
                return false;
            }
        }

        private static bool AlterDBAddTimeColumn(string file)
        {
            try
            {
                if (File.Exists(file))
                {
                    using (var cn = new SQLiteConnection(file))
                    {
                        try
                        {
                            cn.Execute("SELECT CacheTime FROM Tiles LIMIT 1");
                        }
                        catch
                        {
                            cn.Execute("ALTER TABLE Tiles ADD CacheTime DATETIME");
                        }
                    }
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("AlterDBAddTimeColumn: " + ex.ToString());
                return false;
            }
        }

        public static bool VacuumDb(string file)
        {
            try
            {
                using (var cn = new SQLiteConnection(file))
                {
                    cn.Execute("vacuum;");
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("VacuumDb: " + ex.ToString());
                return false;
            }
        }

        public static bool ExportMapDataToDB(string sourceFile, string destFile)
        {
            try
            {
                if (!File.Exists(destFile))
                {
                    CreateEmptyDB(destFile);
                }

                using (var cn2 = new SQLiteConnection(destFile))
                {
                    cn2.Execute(string.Format("ATTACH DATABASE \"{0}\" AS Source", sourceFile));
                    cn2.Execute("INSERT INTO Tiles(X, Y, Zoom, Type, CacheTime) SELECT X, Y, Zoom, Type, CacheTime FROM Source.Tiles WHERE id NOT IN (SELECT id FROM Tiles);");
                    cn2.Execute("DETACH DATABASE Source;");
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ExportMapDataToDB: " + ex.ToString());
                return false;
            }
        }

        #endregion

        static readonly string singleSqlSelect =
            "SELECT Tile FROM main.TilesData WHERE id = (SELECT id FROM main.Tiles WHERE X={0} AND Y={1} AND Zoom={2} AND Type={3})";

        static readonly string singleSqlInsert =
            "INSERT INTO main.Tiles(X, Y, Zoom, Type, CacheTime) VALUES(?, ?, ?, ?, ?)";

        static readonly string singleSqlInsertLast =
            "INSERT INTO main.TilesData(id, Tile) VALUES((SELECT last_insert_rowid()), ?)";

        string _connectionStringStorage;
        readonly List<string> _attachedCaches = new List<string>();
        string _finnalSqlSelect = singleSqlSelect;
        string _attachSqlQuery = string.Empty;
        string _detachSqlQuery = string.Empty;

        void RebuildFinnalSelect()
        {
            _finnalSqlSelect = singleSqlSelect;
            _attachSqlQuery = string.Empty;
            _detachSqlQuery = string.Empty;

            int i = 1;
            foreach (string c in _attachedCaches)
            {
                _finnalSqlSelect +=
                    string.Format(
                        "\nUNION SELECT Tile FROM db{0}.TilesData WHERE id = (SELECT id FROM db{0}.Tiles WHERE X={{0}} AND Y={{1}} AND Zoom={{2}} AND Type={{3}})",
                        i);
                _attachSqlQuery += string.Format("\nATTACH '{0}' as db{1};", c, i);
                _detachSqlQuery += string.Format("\nDETACH DATABASE db{0};", i);
                i++;
            }
        }

        public void Attach(string db)
        {
            if (!_attachedCaches.Contains(db))
            {
                _attachedCaches.Add(db);
                RebuildFinnalSelect();
            }
        }

        public void Detach(string db)
        {
            if (_attachedCaches.Contains(db))
            {
                _attachedCaches.Remove(db);
                RebuildFinnalSelect();
            }
        }

        #region PureImageCache Members

        int _preAllocationPing;

        bool PureImageCache.PutImageToCache(byte[] tile, int type, GPoint pos, int zoom)
        {
            bool ret = true;
            if (_created)
            {
                try
                {
                    using (var cn = new SQLiteConnection(_connectionString))
                    {
                        cn.RunInTransaction(() =>
                        {
                            cn.Execute(singleSqlInsert, pos.X, pos.Y, zoom, type, DateTime.Now);
                            cn.Execute(singleSqlInsertLast, tile);
                        });
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("PutImageToCache: " + ex.ToString());
                    ret = false;
                }
            }
            return ret;
        }

        PureImage PureImageCache.GetImageFromCache(int type, GPoint pos, int zoom)
        {
            PureImage ret = null;
            try
            {
                using (var cn = new SQLiteConnection(_connectionString))
                {
                    if (!string.IsNullOrEmpty(_attachSqlQuery))
                    {
                        cn.Execute(_attachSqlQuery);
                    }

                    string query = string.Format(_finnalSqlSelect, pos.X, pos.Y, zoom, type);
                    var tile = cn.ExecuteScalar<byte[]>(query);

                    if (tile != null && tile.Length > 0)
                    {
                        if (GMapProvider.TileImageProxy != null)
                        {
                            ret = GMapProvider.TileImageProxy.FromArray(tile);
                        }
                    }

                    if (!string.IsNullOrEmpty(_detachSqlQuery))
                    {
                        cn.Execute(_detachSqlQuery);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("GetImageFromCache: " + ex.ToString());
                ret = null;
            }
            return ret;
        }

        int PureImageCache.DeleteOlderThan(DateTime date, int? type)
        {
            int affectedRows = 0;
            try
            {
                using (var cn = new SQLiteConnection(_connectionString))
                {
                    string query = string.Format(
                        "DELETE FROM Tiles WHERE CacheTime is not NULL and CacheTime < datetime('{0}')",
                        date.ToString("s"));
                    if (type.HasValue)
                    {
                        query += " and Type = " + type.Value;
                    }

                    affectedRows = cn.Execute(query);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("DeleteOlderThan: " + ex);
            }
            return affectedRows;
        }

        #endregion
    }
#endif
}
