using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Linq;

namespace Solidb
{
    public class Solidbase<T> : Solidbase
    {
        public Solidbase() : base(typeof(T).Name) { }
    }
     
    public class Solidbase : ObservableCollection<dynamic> {
        private string _name { get; set; }
        private string _connectionString { get; set; } = null;

        public Solidbase(string name) {
            this._name = name;
            CreateTable();
            Pull();
            this.CollectionChanged += new NotifyCollectionChangedEventHandler((sender, e) => {
                switch (e.Action) {
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
            });
        }

        private IDbConnection Connection { get { return Strategy(); } } // utility to create a connection based on connection string that was instasiated or backup
        private new void Remove(dynamic obj) => ExecuteQuery($"DELETE FROM {_name} WHERE Id = {GetID(obj)}"); // remove from db based on ID 
        public int NextId { get { return this.Count == 0 ? 0 : (int)(this.OrderByDescending(x => x.Id).First().Id + 1); } } // get the next integer ID available
        private void CreateTable() => ExecuteQuery($"IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='{_name}' AND xtype='U') CREATE TABLE [{_name}] (Id INT UNIQUE, Data VARCHAR(MAX))"); // create table incase it doesnt exist
        private void AttachListener(dynamic obj) => ((INotifyPropertyChanged)obj).PropertyChanged += new PropertyChangedEventHandler((sender, e) => { SaveChanges(new[] { sender }); }); // attach the listener for change tracking

        // A handy reusable snippet.
        private void ExecuteQuery(string query) { 
            using (var con = Connection) {
                con.Open();
                using (IDbCommand cmd = con.CreateCommand()) {
                    cmd.CommandText = query;
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // pull from the db and fill the list
        private void Pull() {
            Clear();
            using (var con = Connection) {
                con.Open();
                using (IDbCommand cmd = con.CreateCommand()) {
                    cmd.CommandText = $"SELECT * from {_name}";
                    var reader = cmd.ExecuteReader();
                    while (reader.Read()) {
                        int id = reader.GetInt32(0);
                        string str = reader.GetString(1);
                        dynamic theObject = JsonConvert.DeserializeObject(str);
                        theObject.Id = id; // Resync the Id of the row.
                        AttachListener(theObject);
                        this.Add(theObject);
                    }
                }
            }
        }

        // pull and replace a single item from db to list based on id
        private void Resync(int Id) {
            using (var con = Connection) {
                con.Open();
                using (IDbCommand cmd = con.CreateCommand()) {
                    cmd.CommandText = $"SELECT Data from {_name} WHERE Id = {Id}";
                    var reader = cmd.ExecuteReader();
                    while (reader.Read()) {
                        string str = reader.GetString(0);
                        dynamic theObject = JsonConvert.DeserializeObject(str);
                        AttachListener(theObject);
                        dynamic original = this.FirstOrDefault(x => GetID(x) == Id);
                        int _index = this.IndexOf(original);
                        this.SetItem(_index, theObject);
                    }
                }
            }
        }

        public void Resync()
        {
            SaveChanges();
            Pull();
        }

        // Save changes (could be set as private since change tracking is implemented)
        private void SaveChanges(IEnumerable<dynamic> objs = null) {
            Action<dynamic> InsertOrUpdate = (o) => ExecuteQuery($"DECLARE @id INT = {GetID(o)}, @data VARCHAR(MAX) = '{JsonConvert.SerializeObject(o, Formatting.None)}' IF EXISTS(SELECT Id FROM {_name} WHERE Id = @id) BEGIN UPDATE {_name} SET Data = @data WHERE Id = @id END ELSE BEGIN INSERT INTO {_name} (Id, Data) VALUES(@id, @data) END");
            if (objs != null) {
                objs.ForEach(x => InsertOrUpdate(x));
            } else {
                this.ForEach(x => InsertOrUpdate(x));
            }
        }

        // handy method to get the id property of a type or dynamic object
        internal static int GetID(dynamic self) {
            int? id;
            try {
                id = (int)self.Id;
            } catch {
                try {
                    id = (int)self?.GetType().GetProperty("Id")?.GetValue(self, null);
                } catch {
                    throw new Exception($"Type or Object does not contain the 'Id' property in a format of an '{typeof(int)}'.");
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

    public static class SolidbaseExtensions {
        internal static void ForEach(this IEnumerable<dynamic> self, Action<dynamic> action) {
            foreach(dynamic d in self) { // dont like writing Foreach everywere so linq expressions are handy
                action(d);
            }
        }
    }
}
