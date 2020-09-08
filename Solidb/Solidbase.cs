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

    public class Solidbase : ObservableCollection<dynamic>
    {
        private string _name { get; set; }
        private string _connectionString { get; set; } = null;
        private bool IsSyncing { get; set; } = false;

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

        internal string ResolveQuery(string name, dynamic obj)
        {
            return Strategy != null ? Queries["SQLServer"][name](obj) : Queries["SQLite"][name](obj);
        }

        public Solidbase(string name)
        {
            this._name = name;
            CreateTable();
            Pull();
            this.CollectionChanged += new NotifyCollectionChangedEventHandler((sender, e) => {
                if (!IsSyncing)
                {
                    switch (e.Action)
                    {
                        case NotifyCollectionChangedAction.Add:
                            SaveChanges(e.NewItems.Cast<dynamic>()); // save the object to db
                            e.NewItems.Cast<dynamic>().ForEach(x => Resync(GetID(x))); // pull back object as a dynamic and attach the listener
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
                if (_Connection == null || _Connection.State == ConnectionState.Closed)
                {
                    _Connection = Strategy != null ? Strategy() : new SQLiteConnection("Data Source=Solid.base");
                    _Connection.Open();
                }
                return _Connection;
            } 
        } // utility to create a connection based on connection string that was instasiated or backup


        private new void Remove(dynamic obj) => ExecuteQuery(ResolveQuery("REMOVE", obj)); // remove from db based on ID 
        //public int NextId { get { return this.Count == 0 ? 0 : (int)(this.OrderByDescending(x => x.Id).First().Id + 1); } } // get the next integer ID available
        private void CreateTable() => ExecuteQuery(ResolveQuery("CREATE", null)); // create table incase it doesnt exist
        private void AttachListener(dynamic obj) => ((INotifyPropertyChanged)obj).PropertyChanged += new PropertyChangedEventHandler((sender, e) => { SaveChanges(new[] { sender }); }); // attach the listener for change tracking

        // A handy reusable snippet.
        private void ExecuteQuery(string query)
        {
            var con = Connection;
            {
                //con.Open();
                using (IDbCommand cmd = con.CreateCommand())
                {
                    cmd.CommandText = query;
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // pull from the db and fill the list
        private void Pull()
        {
            IsSyncing = true;
            Clear();
            var con = Connection;
            {
                //con.Open();
                using (IDbCommand cmd = con.CreateCommand())
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
            }
            IsSyncing = false;
        }

        // pull and replace a single item from db to list based on id
        private void Resync(Guid Id)
        {
            IsSyncing = true;
            var con = Connection;
            {
                //con.Open();
                using (IDbCommand cmd = con.CreateCommand())
                {
                    cmd.CommandText = ResolveQuery("RESYNC", Id);
                    var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        string str = reader.GetString(0);
                        dynamic theObject = JsonConvert.DeserializeObject(str);
                        AttachListener(theObject);
                        dynamic original = this.FirstOrDefault(x => GetID(x) == Id);
                        int _index = this.IndexOf(original);
                        this.SetItem(_index, theObject);
                    }
                }
            }
            IsSyncing = false;
        }

        public void Resync()
        {
            //SaveChanges();
            Pull();
        }

        // Save changes (could be set as private since change tracking is implemented)
        private void SaveChanges(IEnumerable<dynamic> objs = null)
        {
            Action<dynamic> InsertOrUpdate = (o) => ExecuteQuery(ResolveQuery("INSERT", o));
            if (objs != null)
            {
                objs.ForEach(x => InsertOrUpdate(x));
            }
            else
            {
                this.ForEach(x => InsertOrUpdate(x));
            }
        }

        // handy method to get the id property of a type or dynamic object
        internal Guid GetID(dynamic self)
        {
            Guid? id;
            try
            {
                id = (Guid)self.Id;
            }
            catch
            {
                try
                {
                    id = (Guid)self?.GetType().GetProperty("Id")?.GetValue(self, null);
                }
                catch
                {
                    id = Guid.NewGuid();
                    //throw new Exception($"Type or Object does not contain the 'Id' property in a format of an '{typeof(int)}'.");
                }
            }
            return id.Value;
        }

        public static Func<IDbConnection> Strategy { get; set; }

        public static Solidbase New(string table)
        {
            return new Solidbase(table);
        }
    }

    public static class SolidbaseExtensions
    {
        internal static void ForEach(this IEnumerable<dynamic> self, Action<dynamic> action)
        {
            foreach (dynamic d in self)
            { // dont like writing Foreach everywere so linq expressions are handy
                action(d);
            }
        }
    }
}
