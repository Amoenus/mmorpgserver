#region using

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using NLog;
#endregion

namespace GiftCodeDb
{
    public enum eDbReturn
    {
        None = -1,
        Success = 0,
        Exception = 1,
        ReadError = 2,
        TimeOver = 3
    }

    public class GiftCodeEntry
    {
        /// <summary>
        ///     礼品码
        /// </summary>
        public string Code;

        /// <summary>
        ///     掉落id
        /// </summary>
        public int DropId;

        /// <summary>
        ///     礼品码失效日期
        /// </summary>
        public DateTime EndTime;
    }

    public class GMGetFanKui
    {
        public uint Id; 
        public long CharacterId;
        public string Name;
        public string Title;
        public string Content;
        public string CreateTime;
        public int State;
    }

    public class GMGetFanKuiList
    {
        public GMGetFanKuiList()
        {
            FanKuiList = new List<GMGetFanKui>();
        }
        public List<GMGetFanKui> FanKuiList;
    }

    public class GiftCodeDbConnection
    {
        private const string AddGiftCodeProcedureName = "add_giftcode";
        private const string TableName = "giftcode"; //礼品码表
        private const string UpdateStateProcedureName = "updatestate";

        public GiftCodeDbConnection(string connectionString)
        {
            try
            {
                ConnectionString = connectionString;
                var conn = new MySqlConnection(connectionString);
                conn.Open();
                mConnections.Push(conn);
            }
            catch (Exception ex)
            {
                Logger.Fatal(ex, "DbConnection start failed.");
            }
        }

        private readonly string ConnectionString;
        private readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly ConcurrentStack<MySqlConnection> mConnections = new ConcurrentStack<MySqlConnection>();

        private MySqlConnection CheckConnection()
        {
            MySqlConnection connection = null;
            try
            {
                if (!mConnections.TryPop(out connection))
                {
                    connection = new MySqlConnection(ConnectionString);
                }

                try
                {
                    if (!connection.Ping())
                    {
                        Logger.Error("connect.ping return false!!!");
                        Logger.Error("CheckConnection 1 !!! connect.state={0}", connection.State);
                        connection.Close();
                        Logger.Error("CheckConnection 2 !!! connect.state={0}", connection.State);
                        connection.Open();
                        Logger.Error("CheckConnection 3 !!! connect.state={0}", connection.State);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("CheckConnection 4  Exception!!! connect.state={0}, ex: {1}", connection.State, ex);
                    connection.Close();
                    Logger.Error("CheckConnection 5  Exception!!! connect.state={0}", connection.State);
                    connection.Open();
                    Logger.Error("CheckConnection 6  Exception!!! connect.state={0}", connection.State);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Try to reconnect to database {0} failed.", connection.ConnectionString);
            }

            return connection;
        }

        /// <summary>
        ///     执行多条SQL语句，实现数据库事务。
        /// </summary>
        /// sql2000数据库
        /// <param name="sqls">多条SQL语句</param>
        public async Task<int> ExecuteSqlTranAsync(IEnumerable<string> sqls)
        {
            var ret = 1;
            MySqlConnection conn = null;
            try
            {
                conn = CheckConnection();
                using (var cmd = conn.CreateCommand())
                {
                    foreach (var sql in sqls)
                    {
                        cmd.CommandText = sql;
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                ret = 0;
                Logger.Error("RunProcedure error !! storedProcName: {0} ,ex{1}", UpdateStateProcedureName, ex);
            }
            finally
            {
                if (conn != null)
                {
                    mConnections.Push(conn);
                }
            }
            return ret;
        }

        //同步添加礼品码
        public int NewGiftCode(List<string> codes)
        {
            var t = ExecuteSqlTranAsync(NewGiftCodeSqls(codes));
            return t.Result;
        }

        public int InsertQuestion(string title,string text,string byName,ulong byId)
        {
            var sqls = new List<string>();
            sqls.Add(string.Format("insert into question(Title,Text,ById,ByName) value('{0}','{1}',{2},'{3}');", title, text, byId, byName));
            var t = ExecuteSqlTranAsync(sqls);
            return t.Result;
        }

        private List<string> NewGiftCodeSqls(List<string> codes)
        {
            var i = 0;
            var sb = new StringBuilder();
            var ret = new List<string>();
            foreach (var code in codes)
            {
                if (i%100000 == 0)
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(";");
                        ret.Add(sb.ToString());
                        sb.Clear();
                    }
                    sb.AppendFormat("insert into giftcode(code) values ('{0}')", code);
                }
                else
                {
                    sb.AppendFormat(",('{0}')", code);
                }
                ++i;
            }
            sb.Append(";");
            ret.Add(sb.ToString());
            return ret;
        }

        //同步设置state
        public async Task<eDbReturn> UpdateStateAsync(string code)
        {
            var iData = new MySqlParameter[2];
            iData[0] = new MySqlParameter("@incode", code) {MySqlDbType = MySqlDbType.VarChar};
            iData[1] = new MySqlParameter("@myreturn", MySqlDbType.Int16);
            iData[1].Direction = ParameterDirection.Output;

            MySqlConnection conn = null;
            try
            {
                conn = CheckConnection();

                using (var command = conn.CreateCommand())
                {
                    command.CommandText = UpdateStateProcedureName;
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddRange(iData);
                    using (var reader = (MySqlDataReader) await command.ExecuteReaderAsync())
                    {
                        if (!reader.Read())
                        {
                            return eDbReturn.ReadError;
                        }
                        var myreturn = (eDbReturn) reader.GetInt16("myreturn");
                        return myreturn;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("RunProcedure error !! storedProcName: {0} ,ex{1}", UpdateStateProcedureName, ex);
            }
            finally
            {
                if (conn != null)
                {
                    mConnections.Push(conn);
                }
            }
            return eDbReturn.None;
        }

        // 查看当前礼品码是否可以使用
        public async Task<int> CheckCodeIsActiveAsync(string code)
        {
            var ret = 1;
            var sql = string.Format("SELECT * FROM giftcodedb.giftcode where code = '{0}' and state = 0;", code);
            MySqlConnection conn = null;
            MySqlDataReader reader = null;
            try
            {
                conn = CheckConnection();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = sql;
                    cmd.CommandType = CommandType.Text;
                    using (reader = cmd.ExecuteReader())
                    {
                        if (reader != null)
                        {
                            if (!reader.Read())
                            {
                                ret = 0;
                            }
                            else
                            {
                                ret = 1;
                            }
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                ret = 0;
                Logger.Error("CheckCodeIsActiveAsync error !! {0}", ex);
            }
            finally
            {
                if (conn != null)
                {
                    mConnections.Push(conn);
                }
            }
            return ret;
        }

        // 返回玩家反馈消息
        public async Task<GMGetFanKuiList> GetCharacterFanKuiAsync(string TimeBegin, string TimeEnd, int StartIndex, int EndIndex, int State)
        {
            var result = new GMGetFanKuiList();
            result.FanKuiList.Clear();
            var sql = string.Format("SELECT * FROM giftcodedb.question where createtime >= '{0}' and createtime <= '{1}' and state = {2} limit {3},{4};",
                TimeBegin, TimeEnd, State,StartIndex, EndIndex);
            MySqlConnection conn = null;
            MySqlDataReader reader = null;
            try
            {
                conn = CheckConnection();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = sql;
                    cmd.CommandType = CommandType.Text;
                    using (reader = cmd.ExecuteReader())
                    {
                        if (reader != null)
                        {
                            while (reader.Read())
                            {
//                                 result += " CharacterId: " + reader.GetUInt32("ById").ToString();
//                                 result += " CharacterName: " + reader.GetString("ByName");
//                                 result += " Title: " + reader.GetString("Title");
//                                 result += " Content: " + reader.GetString("Text");
//                                 result += " createTime: " + reader.GetString("createtime");
//                                 result += " state " + reader.GetInt16("state") + ";   ";
                                var tmp = new GMGetFanKui();
                                tmp.Id = reader.GetUInt32("id");
                                tmp.CharacterId = reader.GetInt64("ById");
                                tmp.Name = reader.GetString("ByName");
                                tmp.Title = reader.GetString("Title");
                                tmp.Content = reader.GetString("Text");
                                tmp.CreateTime = reader.GetString("createtime");
                                tmp.State = reader.GetInt32("state");
                                result.FanKuiList.Add(tmp);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("GetCharacterFanKuiAsync error !! {0}", ex);
                return result;
            }
            finally
            {
                if (conn != null)
                {
                    mConnections.Push(conn);
                }
            }
            return result;
        }

        // 修改玩家反馈消息状态
        public async Task<GMGetFanKuiList> SetCharacterFanKuiStateAsync(List<int> Ids, int State)
        {
            var result = new GMGetFanKuiList();
            result.FanKuiList.Clear();

            var ids = string.Empty;
            for (int i = 0; i < Ids.Count; i++)
            {
                if (i != Ids.Count - 1)
                {
                    ids = ids + Ids[i] + ","; 
                }
                else
                {
                    ids = ids + Ids[i];
                }
            }

            var sql = string.Format("Update giftcodedb.question set state = {0} where id IN({1});", State, ids);
            MySqlConnection conn = null;
            MySqlDataReader reader = null;
            try
            {
                conn = CheckConnection();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = sql;
                    cmd.CommandType = CommandType.Text;
                    using (reader = cmd.ExecuteReader())
                    {

                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("GetCharacterFanKuiAsync error !! {0}", ex);
                return result;
            }
            finally
            {
                if (conn != null)
                {
                    mConnections.Push(conn);
                }
            }
            return result;
        }
    }
}
