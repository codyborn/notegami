using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using WebRole;
using WebRole.Models;

namespace AbyssTest
{
    [TestClass]
    public class CopyIndexToNewTable
    {
        [TestMethod]
        public void CopyHistory()
        {
            // foreach user
            IEnumerable<User> allUsers = TableStore.GetAllEntitiesInAPartition<User>(TableStore.TableName.users, Constant.UserPartition);
            foreach (User user in allUsers)
            {
                try
                {
                    IEnumerable<RecentTokenList> allNotes = TableStore.GetAllEntitiesInAPartition<RecentTokenList>(TableStore.TableName.notes, user.UserId);                    
                    List<TableEntity> recentTokenList = new List<TableEntity>();
                    // copy all user indices to new table
                    foreach (TableEntity entity in allNotes)
                    {
                        if (!entity.RowKey.StartsWith("note_"))
                        {
                            if (entity.RowKey.Contains("rec"))
                            {
                                recentTokenList.Add((RecentTokenList)entity);
                            }
                        }
                    }
                    TableStore.BatchInsertOrUpdate(TableStore.TableName.recentTokens, recentTokenList);
                }
                catch
                {
                    continue;
                }
            }
        }
        [TestMethod]
        public void CopyIndexNotes()
        {
            // foreach user
            IEnumerable<User> allUsers = TableStore.GetAllEntitiesInAPartition<User>(TableStore.TableName.users, Constant.UserPartition);
            foreach (User user in allUsers)
            {
                try
                {
                    IEnumerable<TableEntity> allNotes = TableStore.GetAllEntitiesInAPartition<TableEntity>(TableStore.TableName.notes, user.UserId);
                    List<TableEntity> insertUpdateList = new List<TableEntity>();
                    List<TableEntity> recentTokenList = new List<TableEntity>();
                    // copy all user indices to new table
                    foreach (TableEntity entity in allNotes)
                    {
                        if (!entity.RowKey.StartsWith("note_"))
                        {
                            if (entity.RowKey.Contains("rec"))
                            {
                                recentTokenList.Add((RecentTokenList)entity);
                            }
                            Index index = entity as Index;
                            if (index == null)
                            {
                                Console.WriteLine("Cannot convert to index");
                            }
                            else
                            {
                                insertUpdateList.Add(index);
                            }
                        }
                    }
                    TableStore.BatchInsertOrUpdate(TableStore.TableName.indices, insertUpdateList);
                    TableStore.BatchInsertOrUpdate(TableStore.TableName.recentTokens, recentTokenList);
                }
                catch
                {
                    continue;
                }
            }
        }

    }
}
