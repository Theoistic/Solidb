using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Linq;

namespace Solidb
{
    public class Solidbase<T> : Solidbase
    {
        public Solidbase() : base(typeof(T).Name) { }
    }

    public class Solidbase : ObservableCollection<dynamic>, IDisposable
    {
        public static Func<IDbConnection> Strategy { get; set; }
        private string _name { get; set; }
        private string _connectionString { get; set; } = null;
        private bool IsSyncing { get; set; } = false;
        internal string ResolveQuery(string name, dynamic obj) => Strategy != null ? Queries["SQLServer"][name](obj) : Queries["SQLite"][name](obj);

        // [TODO] SQLServer Queries are broken due to the Id property changed from Int32 to Guid
        internal Dictionary<string, Dictionary<string, Func<dynamic, string>>> Queries => new Dictionary<string, Dictionary<string, Func<dynamic, string>>>
        {
            { "SQLServer", new Dictionary<string, Func<dynamic, string>> {
                { "REMOVE", (x) => { return $"DELETE FROM [{_name}] WHERE Id = {GetID(x)}"; } },
                { "CREATE", (x) => { return $"IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='{_name}' AND xtype='U') CREATE TABLE [{_name}] (Id INT UNIQUE, Data VARCHAR(MAX))"; } },
                { "INSERT", (x) => { return $"DECLARE @id INT = {GetID(x)}, @data VARCHAR(MAX) = '{JsonConvert.SerializeObject(x, Formatting.None)}' IF EXISTS(SELECT Id FROM [{_name}] WHERE Id = @id) BEGIN UPDATE [{_name}] SET Data = @data WHERE Id = @id END ELSE BEGIN INSERT INTO [{_name}] (Id, Data) VALUES(@id, @data) END"; } },
                { "SELECT", (x) => { return $"SELECT * from [{_name}]"; } },
                { "RESYNC", (x) => { return $"SELECT Data from [{_name}] WHERE Id = {x}"; } }
            }},
            { "SQLite", new Dictionary<string, Func<dynamic, string>> {
                { "REMOVE", (x) => { return $"DELETE FROM [{_name}] WHERE Id = '{GetID(x)}'"; } },
                { "CREATE", (x) => { return $"CREATE TABLE IF NOT EXISTS {_name} (Id GUID PRIMARY KEY, Data TEXT NOT NULL);"; } },
                { "INSERT", (x) => { return $"INSERT INTO {_name} (Id, Data) VALUES('{GetID(x)}', '{JsonConvert.SerializeObject(x, Formatting.None)}') ON CONFLICT(Id) DO UPDATE SET Data='{JsonConvert.SerializeObject(x, Formatting.None)}';"; } },
                { "SELECT", (x) => { return $"SELECT * from {_name}"; } },
                { "RESYNC", (x) => { return $"SELECT Data from {_name} WHERE Id = '{x}'"; } }
            }}
        };

        public Solidbase(string name)
        {
            this._name = name;
            CreateTable();
            Resync();
            this.CollectionChanged += new NotifyCollectionChangedEventHandler((sender, e) => {
                if (!IsSyncing)
                {
                    switch (e.Action)
                    {
                        case NotifyCollectionChangedAction.Add:
                            SaveChanges(e.NewItems.Cast<dynamic>()); // save the object to db
                            //e.NewItems.Cast<dynamic>().ForEach(x => Resync(GetID(x))); // pull back object as a dynamic and attach the listener
                            break;
                        case NotifyCollectionChangedAction.Replace:
                            SaveChanges(e.NewItems.Cast<dynamic>()); // save to the db
                            break;
                        case NotifyCollectionChangedAction.Remove:
                            e.OldItems.Cast<dynamic>().ForEach(x => Remove(x)); // remove from the db as well
                            break;
                        default: break;
                    }
                }
            });
        }

        private IDbConnection _Connection { get; set; }
        private IDbConnection Connection { 
            get {
                if (_Connection == null || (_Connection.State == ConnectionState.Closed || _Connection.State == ConnectionState.Broken)) {
                    _Connection = Strategy != null ? Strategy() : new SQLiteConnection("Data Source=Solid.base");
                    _Connection.Open();
                }
                return _Connection;
            } 
        } // utility to create a connection based on connection string that was instasiated or backup

        private void ExecuteQuery(string query)
        {
            using (IDbCommand cmd = Connection.CreateCommand())
            {
                cmd.CommandText = query;
                cmd.ExecuteNonQuery();
            }
        }

        // pull from the db and fill the list
        private void Resync()
        {
            IsSyncing = true;
            Clear();
            using (IDbCommand cmd = Connection.CreateCommand())
            {
                cmd.CommandText = ResolveQuery("SELECT", null);
                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    Guid id = reader.GetGuid(0);
                    string str = reader.GetString(1);
                    dynamic theObject = JsonConvert.DeserializeObject(str);
                    theObject.Id = id; // Resync the Id of the row.
                    AttachListener(theObject);
                    this.Add(theObject);
                }
            }
            IsSyncing = false;
        }

        // Save changes (could be set as private since change tracking is implemented)
        private void SaveChanges(IEnumerable<dynamic> objs = null)
        {
            Action<dynamic> InsertOrUpdate = (o) => ExecuteQuery(ResolveQuery("INSERT", o));
            if (objs != null) {
                objs.ForEach(x => InsertOrUpdate(x));
            } else {
                this.ForEach(x => InsertOrUpdate(x));
            }
        }

        private new void Remove(dynamic obj) => ExecuteQuery(ResolveQuery("REMOVE", obj)); 
        private void CreateTable() => ExecuteQuery(ResolveQuery("CREATE", null)); 
        private void AttachListener(dynamic obj) => ((INotifyPropertyChanged)obj).PropertyChanged += new PropertyChangedEventHandler((sender, e) => { SaveChanges(new[] { sender }); }); 
        internal Guid GetID(dynamic self) => SolidbaseExtensions.PropertyExists(self, "Id") ? (Guid)self.Id : Guid.NewGuid();
        public static Solidbase New(string table) => new Solidbase(table);
        public void Dispose() => _Connection.Dispose();

    }

    public static class SolidbaseExtensions
    {
        internal static void ForEach(this IEnumerable<dynamic> self, Action<dynamic> action)
        {
            foreach (dynamic d in self)
            { 
                action(d);
            }
        }

        internal static bool PropertyExists(dynamic obj, string name)
        {
            if (obj == null) return false;
            if (obj is IDictionary<string, object> dict) {
                return dict.ContainsKey(name);
            }
            return obj.GetType().GetProperty(name) != null;
        }
    }
}
