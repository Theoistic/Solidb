using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Diagnostics;
using System.Data.SqlClient;

namespace Solidb.Tests
{
    public class Product 
    {
        public string Name { get; set; }
        public double Price { get; set; }
    }

    public class Benchmark
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public DateTime Created { get; set; } = DateTime.Now;
        public double Duration { get; set; }
    }

    [TestClass]
    public class ListTests
    {
        [TestInitialize]
        public void Init()
        {
            //Solidbase.Strategy = () => new SqlConnection("Server=.\\SQLEXPRESS;Database=NewSolidb;Trusted_Connection=True;");
        }

        [TestMethod]
        public void Test1()
        {
            using (Solidbase list = new Solidbase("Random"))
            {
                //list.Add(new { count = 1 });
                for (int i = 0; i < 8; i++)
                {
                    list.Add(new { count = i });
                }
            }
            /*list.Add(new { Id = 1, anotherValue = "awesome" });
            var e = list.First();
            e.something = "lol";
            var g = list.FirstOrDefault(x => x.something == "lol");
            Assert.IsTrue(g != null);
            list.Add(new { Id = 40, epic = true });
            list.Remove(list.First());*/
        }

        [TestMethod]
        public void Test2()
        {
            Solidbase list = new Solidbase("Random");
            var e = list.First();
            list.Remove(e);
        }

        [TestMethod]
        public void Test3()
        {
            Solidbase list = new Solidbase("Random");
            var e = list.Last();
            e.lol = "epic";
        }

        [TestMethod]
        public void Test4()
        {
            Solidbase list = new Solidbase("Random");
            //list.Add(new { Id = list.NextId, content = "rainbow" });
            var latest = list.Last();
            latest.newStuff = "something else";
        }

        [TestMethod]
        public void Test5()
        {
            Solidbase<Product> list = new Solidbase<Product>();
            list.Add(new Product { Name = "Something", Price = 520.4 });
            var storedProduct = list.Last(x => x.Name == "Something");
            Assert.IsNotNull(storedProduct);
        }

        [TestMethod]
        public void Benchmark()
        {
            Run("1000 Product Inserts", () => {
                Solidbase list = new Solidbase<Product>();
                for (int i = 0; i < 1000; i++)
                {
                    list.Add(new Product { Name = "Something", Price = i });
                }
                Assert.IsTrue(list.Count > 999);
            });
            Run("Load 1000 Products", () => {
                Solidbase list2 = new Solidbase<Product>();
                Assert.IsTrue(list2.Count > 999);
            });
        }

        public void Run(string name, Action action)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            action();
            sw.Stop();
            ReportBenchmark(name, sw.Elapsed.TotalMilliseconds);
        }

        public static void ReportBenchmark(string name, double duration)
        {
            Solidbase list = new Solidbase<Benchmark>();
            //list.Add(new Benchmark { Id = list.NextId, Name = name, Duration = duration });
        }
    }
}
