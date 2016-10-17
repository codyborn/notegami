using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebRole;
using WebRole.Models;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace AbyssTest
{
    [TestClass]
    public class WebApiTests
    {
        static string testUserId = "5459e17c33764954a9e4f8469d7e643d";
        [TestMethod]
        public void QueryNotesTest()
        {
            List<long> times = new List<long>();
            Stopwatch sw = new Stopwatch();
            for (int i = 0; i < 10; i++)
            {
                sw.Start();
                IEnumerable<Note> results = IndexerBase.QueryNotes(testUserId, "@bellevue");
                Assert.IsTrue(results.Count() > 0);
                sw.Stop();
                times.Add(sw.ElapsedMilliseconds);
                sw.Reset();
            }
            long avgTime = (long)times.Average<long>((n) => n);
        }
    }
}
