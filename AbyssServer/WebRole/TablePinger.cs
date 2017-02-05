using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web;

namespace WebRole
{
    public static class TablePinger
    {
        public static void BeginTablePings()
        {
            Thread pingaling = new Thread(PingTheTable);
            pingaling.Start();
        }
        private static void PingTheTable()
        {
            while(true)
            {
                try
                {
                    PingEntry pe = new PingEntry();
                    pe.RowKey = Guid.NewGuid().ToString("N");
                    TableStore.Set(TableStore.TableName.pingTable, pe);
                    Thread.Sleep(Constant.PingTimeInMs);
                    TableStore.Delete(TableStore.TableName.pingTable, pe);
                }
                catch
                {
                    Thread.Sleep(Constant.PingTimeInMs);
                }
            }
        }
        private class PingEntry : TableEntity
        {
            public PingEntry()
            {
                this.PartitionKey = "ping";
            }
        }
    }
}