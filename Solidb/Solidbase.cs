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
    public abstract class Solidbase<T> : Solidbase
    {
        public Solidbase() : base(typeof(T).Name) { }
    }

    public delegate void SolidbRemoveEvent(string name, dynamic obj);
    public delegate void SolidbCreateEvent(string name);
    public delegate void SolidbInsertEvent(string name, dynamic obj);
    public delegate IEnumerable<dynamic> SolidbResyncEvent(string name);

    public abstract class Solidbase : ObservableCollection<dynamic>, IDisposable
    {
        private string _name { get; set; }
        private string _connectionString { get; set; } = null;
        private bool IsSyncing { get; set; } = false;

        public SolidbCreateEvent OnCreate { get; set; }
        public SolidbInsertEvent OnInsert { get; set; }
        public SolidbRemoveEvent OnRemove { get; set; }
        public SolidbResyncEvent OnResync { get; set; }

        public Solidbase(string name)
        {
            this._name = name;
            //CreateTable();
            //Resync();
            this.CollectionChanged += new NotifyCollectionChangedEventHandler((sender, e) =>
            {
                if (!IsSyncing)
                {
                    switch (e.Action)
                    {
                        case NotifyCollectionChangedAction.Add:
                            SaveChanges(e.NewItems.Cast<dynamic>());
                            break;
                        case NotifyCollectionChangedAction.Replace:
                            SaveChanges(e.NewItems.Cast<dynamic>());
                            break;
                        case NotifyCollectionChangedAction.Remove:
                            e.OldItems.Cast<dynamic>().ForEach(x => Remove(x));
                            break;
                        default: break;
                    }
                }
            });
        }

        internal void Resync()
        {
            IsSyncing = true;
            Clear();
            IEnumerable<dynamic> items = OnResync(_name);
            foreach (var item in items)
            {
                AttachListener(item);
                this.Add(item);
            }
            IsSyncing = false;
        }

        internal void SaveChanges(IEnumerable<dynamic> objs = null)
        {
            if (objs != null)
            {
                objs.ForEach(x => OnInsert(_name, x));
            }
        }

        internal new void Remove(dynamic obj) => OnRemove(_name, obj);
        internal void CreateTable() => OnCreate(_name);
        internal void Drop() { }
        internal void AttachListener(dynamic obj) => ((INotifyPropertyChanged)obj).PropertyChanged += new PropertyChangedEventHandler((sender, e) => { SaveChanges(new[] { sender }); });
        internal Guid GetID(dynamic self) => SolidbaseExtensions.PropertyExists(self, "Id") ? (Guid)self.Id : Guid.NewGuid();
        public void Dispose() { }
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
            if (obj is IDictionary<string, object> dict)
            {
                return dict.ContainsKey(name);
            }
            return obj.GetType().GetProperty(name) != null;
        }
    }
}
