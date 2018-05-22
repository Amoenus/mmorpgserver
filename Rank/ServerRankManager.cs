#region using

using System;
using System.Collections;
using System.Collections.Generic;
using DataContract;
using DataTable;
using EventSystem;
using Scorpion;
using NLog;
using Shared;

#endregion

namespace Rank
{
    public interface IServerRankManager
    {
        int CompareRank(int serverId, int rankType, ulong guid, int rankIndex);
        Ranking GetRankByType(int serverId, int rankType);
        long GetRankData(int serverId, int rankType, int rankIndex);
        List<DBRank_One> GetRankDataByServerId(int serverId, int rankType, int minRank, int maxRank);
        List<DBRank_One> GetTotalRankData(int rankType, int minRank, int maxRank);
        IEnumerator Init(Coroutine coroutine);
        IEnumerator RefreshAll(Coroutine coroutine);
        void ResetCityLevel(int serverId, string name, ulong guid, long value);
        void ResetFightPoint(int serverId, string name, ulong guid, long FightPoint);
        void ResetLevel(int serverId, string name, ulong guid, long Value);
        void ResetMoney(int serverId, string name, ulong guid, long money);
        void ResetPetFight(int serverId, string name, ulong guid, long value);
        void ResetWingsFight(int serverId, string name, ulong guid, long value);
		void ResetTotalRecharge(int serverId, string name, ulong guid, long value);
        void ResetGiftRank(int rankType, int serverId, string charName, ulong guid, long value);
        void ResetMountRank(int serverId, string name, ulong guid, long value);

        void ChangePlayerName(int serverId, string name, ulong guid);
        void ShowLog();
        List<DBRank_One> GetFightRankList(int serverid, int rankType);
    }

    public class ServerRankManagerDefaultImpl : IServerRankManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        //����ÿ����������ƽ���ȼ�
        public void NotifyServerAvgLevel()
        {
            foreach (var sr in ServerRankManager.ServerToRnak)
            {
                var list = GetRankDataByServerId(sr.Key, 1, 1, 10);
                if (list.Count == 0)
                {
                    continue;
                }
                var totle = 0;
                foreach (var one in list)
                {
                    var nLevelone = (int) (one.Value/Constants.RankLevelFactor);
                    totle += nLevelone;
                }
                ServerRankManager.ServerAvgLevel[sr.Key] = totle/list.Count;
            }

            CoroutineFactory.NewCoroutine(NotifyServerAvgLevelCo, ServerRankManager.ServerAvgLevel).MoveNext();
        }

        //�洢
        private IEnumerator NotifyServerAvgLevelCo(Coroutine coroutine, Dictionary<int, int> data)
        {
            var dic = new Dict_int_int_Data();
            dic.Data.AddRange(data);
            var toLogic = RankServer.Instance.SceneAgent.MotifyServerAvgLevel(dic);
            yield return toLogic.SendAndWaitUntilDone(coroutine);
        }

        public IEnumerator Init(Coroutine coroutine)
        {
            Table.ForeachServerName(record =>
            {
                ServerRankManager.ServerToRnak.Add(record.Id, record.LogicID);
                return true;
            });

            foreach (var i in ServerRankManager.ServerToRnak)
            {
                List<int> temp;
                if (!ServerRankManager.RnakToServer.TryGetValue(i.Value, out temp))
                {
                    temp = new List<int>();
                    ServerRankManager.RnakToServer[i.Value] = temp;
                    ServerRankManager.Ranks[i.Value] = new RankManager();
                }
                temp.Add(i.Key);
                var rank = ServerRankManager.Ranks[i.Value];
                var co = CoroutineFactory.NewSubroutine(rank.Init, coroutine, i.Key, i.Value);
                if (co.MoveNext())
                {
                    yield return co;
                }
            }

            // �����а�
            ServerRankManager.TotalRank.CheckResetFirstTime();
            ServerRankManager.TotalRank.ServerId = -1;
            ServerRankManager.TotalRank.RankId = -1;
            var co8 = CoroutineFactory.NewSubroutine(ServerRankManager.TotalRank.AddRanking, coroutine,
                -1, (int)RankType.DailyGift, ServerRankManager.DailyGiftRank, 100, true);
            if (co8.MoveNext())
            {
                yield return co8;
            }

            var co9 = CoroutineFactory.NewSubroutine(ServerRankManager.TotalRank.AddRanking, coroutine,
                -1, (int)RankType.WeeklyGift, ServerRankManager.WeeklyGiftRank, 100, true);
            if (co9.MoveNext())
            {
                yield return co9;
            }

            var co10 = CoroutineFactory.NewSubroutine(ServerRankManager.TotalRank.AddRanking, coroutine,
                -1, (int)RankType.TotalGift, ServerRankManager.TotalGiftRank, 100, true);
            if (co10.MoveNext())
            {
                yield return co10;
            }

            ServerRankManager.TotalRank.AddClearTrigger(RankType.DailyGift);
            ServerRankManager.TotalRank.AddClearTrigger(RankType.WeeklyGift);

            foreach (var rank in ServerRankManager.Ranks)
            {
                foreach (var v in rank.Value.rank)
                {
                    var ranking = v.Value;
                    if (v.Key != (int) RankType.FightValue && v.Key != (int) RankType.Level &&
                        v.Key != (int) RankType.Money && v.Key != (int) RankType.Arena)
                        continue;

                    int max = v.Key == 3 ? 1000 : 100;
                    var nowCount = ranking.GetRankCount();
                    if (nowCount < max)
                    {
                        for (var i = nowCount + 1; i <= max; ++i)
                        {
                            var tbRobot = Table.GetJJCRoot(i);
                            if (tbRobot == null)
                                break;

                            int val = 0;
                            switch (v.Key)
                            {
                                case (int) RankType.FightValue: //ս����
                                    val = tbRobot.CombatValue;
                                    break;
                                case (int) RankType.Level: //�ȼ�
                                    val = tbRobot.Level;
                                    break;
                                case (int) RankType.Money: //�Ƹ�
                                    val = MyRandom.Random(100000, 10000000);
                                    break;
                                case (int) RankType.Arena: //������
                                    val = tbRobot.CombatValue;
                                    break;
                            }
                            var rCount = i;
                            ranking.AddRanking(ranking.serverList[0], (ulong) i, val, tbRobot.Name,
                                tbRobot.CombatValue);
                            ranking.RankUUIDList.Add((ulong) i);
                        }
                    }
                    ranking.Sort(true);
                }
            }

            //����ÿ����������ƽ���ȼ�

            var waitServers = true;
            while (waitServers)
            {
                RankServer.Instance.AreAllServersReady(ready =>
                {
                    if (ready)
                    {
                        waitServers = false;
                        NotifyServerAvgLevel();
                    }
                });

                yield return RankServer.Instance.ServerControl.Wait(coroutine, TimeSpan.FromSeconds(5));
            }

            RankServerControl.Timer.CreateTrigger(DateTime.Now.Date.AddDays(1), NotifyServerAvgLevel, 24*3600*1000);
            var time = DateTime.Now.Date + new TimeSpan(23, 59, 0);
            RankServerControl.Timer.CreateTrigger(time,
                () => { ServerRankManager.TotalRank.FightRankListBackUp(); },
                24*3600*1000); //ÿ24Сʱ�洢һ��
            if(ServerRankManager.TotalRank.FightRankServerToRnak == null || ServerRankManager.TotalRank.FightRankServerToRnak.Count == 0)
                ServerRankManager.TotalRank.FightRankListBackUp();

            EventDispatcher.Instance.AddEventListener(ReloadTableEvent.EVENT_TYPE, ReloadTable);
        }
        private void ReloadTable(IEvent ievent)
        {
            var v = ievent as ReloadTableEvent;
            if (v == null)
                return;
            if (v.tableName == "ServerName")
            {
                CoroutineFactory.NewCoroutine(ReloadTableEnumerator).MoveNext();
            }
        }

        public IEnumerator ReloadTableEnumerator(Coroutine coroutine)
        {
            List<int> newServer = new List<int>();
            Table.ForeachServerName(record =>
            {
                if (ServerRankManager.ServerToRnak.ContainsKey(record.Id) == false)
                {
                    ServerRankManager.ServerToRnak.Add(record.Id, record.LogicID);
                    List<int> temp;
                    if (!ServerRankManager.RnakToServer.TryGetValue(record.LogicID, out temp))
                    {
                        temp = new List<int>();
                        ServerRankManager.RnakToServer[record.LogicID] = temp;
                        ServerRankManager.Ranks[record.LogicID] = new RankManager();
                        temp.Add(record.Id);
                        newServer.Add(record.LogicID);
                    }
                }
                return true;
            });
            foreach (var id in newServer)
            {
                var rank = ServerRankManager.Ranks[id];
                var co = CoroutineFactory.NewSubroutine(rank.Init, coroutine, id, id);
                if (co.MoveNext())
                {
                    yield return co;
                }
                foreach (var v in rank.rank)
                {
                    var ranking = v.Value;
                    if (v.Key != (int)RankType.FightValue && v.Key != (int)RankType.Level &&
                        v.Key != (int)RankType.Money && v.Key != (int)RankType.Arena)
                        continue;

                    int max = v.Key == 3 ? 1000 : 100;
                    var nowCount = ranking.GetRankCount();
                    if (nowCount < max)
                    {
                        for (var i = nowCount + 1; i <= max; ++i)
                        {
                            var tbRobot = Table.GetJJCRoot(i);
                            if (tbRobot == null)
                                break;

                            int val = 0;
                            switch (v.Key)
                            {
                                case (int)RankType.FightValue: //ս����
                                    val = tbRobot.CombatValue;
                                    break;
                                case (int)RankType.Level: //�ȼ�
                                    val = tbRobot.Level;
                                    break;
                                case (int)RankType.Money: //�Ƹ�
                                    val = MyRandom.Random(100000, 10000000);
                                    break;
                                case (int)RankType.Arena: //������
                                    val = tbRobot.CombatValue;
                                    break;
                            }
                            var rCount = i;
                            ranking.AddRanking(ranking.serverList[0], (ulong)i, val, tbRobot.Name,
                                tbRobot.CombatValue);
                            ranking.RankUUIDList.Add((ulong)i);
                        }
                    }
                    ranking.Sort(true);
                }



            }
            yield break;
        }
        //�洢
        public IEnumerator RefreshAll(Coroutine coroutine)
        {
            foreach (var rank in ServerRankManager.Ranks)
            {
                var co = CoroutineFactory.NewSubroutine(rank.Value.FlushAll, coroutine);
                if (co.MoveNext())
                {
                    yield return co;
                }
            }

            var co1 = CoroutineFactory.NewSubroutine(ServerRankManager.TotalRank.FlushAll, coroutine);
            if (co1.MoveNext())
            {
                yield return co1;
            }
            //foreach (KeyValuePair<int, DBServerNameList> i in Names)
            //{
            //    CoroutineFactory.NewCoroutine(SaveCharacterName, i.Key).MoveNext();
            //}
        }

        #region ״̬��־

        public void ShowLog()
        {
            Logger.Info("ServerRankManager Servers={0}", ServerRankManager.ServerToRnak.Count);
            if (ServerRankManager.ServerToRnak.Count > 0)
            {
                Logger.Info("{");
                foreach (var rankServer in ServerRankManager.RnakToServer)
                {
                    Logger.Info("    rank id={0},servers={1}", rankServer.Key, rankServer.Value.GetDataString());
                }
                Logger.Info("}");
            }

            Logger.Info("ServerRankManager Ranks={0}", ServerRankManager.Ranks.Count);
            if (ServerRankManager.Ranks.Count > 0)
            {
                Logger.Info("{");
                foreach (var rank in ServerRankManager.Ranks)
                {
                    rank.Value.ShowLog();
                }
                Logger.Info("}");
            }

            Logger.Info("{");
            ServerRankManager.TotalRank.ShowLog();
            Logger.Info("}");
        }

        public List<DBRank_One> GetFightRankList(int serverid, int rankType)
        {
            return ServerRankManager.TotalRank.GetFightRankListBackUp(serverid);
        }
        #endregion

        ////��ȡ����
        //public  IEnumerator GetCharacterName(Coroutine coroutine, int serverId)
        //{
        //    var tasks = RankServer.Instance.DB.Get<DBServerNameList>(coroutine, DataCategory.Rank, string.Format("CharacterName_{0}", serverId));
        //    yield return tasks;
        //    if (tasks.Data == null)
        //    {
        //        Names[serverId] = new DBServerNameList();
        //        yield break;
        //    }
        //    Names[serverId] = tasks.Data;
        //}

        ////д���ݿ�
        //public  IEnumerator SaveCharacterName(Coroutine coroutine, int serverId)
        //{
        //    DBServerNameList nameList;
        //    if (!Names.TryGetValue(serverId, out nameList))
        //    {
        //        Logger.Warn("SaveCharacterName faild serverId={0}", serverId);
        //        yield break;
        //    }
        //    var ret = RankServer.Instance.DB.Set(coroutine, DataCategory.Rank, string.Format("CharacterName_{0}", serverId), nameList);
        //    yield return ret;
        //}
        ////�������
        //public  void PushName(int serverId, ulong guid, string name)
        //{
        //    DBServerNameList nameList;
        //    if (!Names.TryGetValue(serverId, out nameList))
        //    {
        //        Logger.Warn("PushName faild serverId={0} guid={1} name={2}", serverId, guid, name);
        //        return;
        //    }
        //    nameList.mData[guid] = name;
        //}
        ////�������
        //public  string GetName(int serverId, ulong guid)
        //{
        //    DBServerNameList nameList;
        //    if (!Names.TryGetValue(serverId, out nameList))
        //    {
        //        Logger.Warn("GetName faild serverId={0} guid={1} ", serverId, guid);
        //        return "";
        //    }
        //    string oldName;
        //    if (!nameList.mData.TryGetValue(guid, out oldName))
        //    {
        //        Logger.Warn("GetName faild serverId={0} guid={1} ", serverId, guid);
        //        return "";
        //    }
        //    return oldName;
        //}
        ////ɾ������Ҫ������
        //public  void DeleteName(int serverId, ulong guid)
        //{
        //    DBServerNameList nameList;
        //    if (!Names.TryGetValue(serverId, out nameList))
        //    {
        //        Logger.Warn("DeleteName faild serverId={0} guid={1}", serverId, guid);
        //        return;
        //    }
        //    nameList.mData.Remove(guid);
        //}

        #region �޸�����

        //�������õȼ�
        public void ResetLevel(int serverId, string name, ulong guid, long Value)
        {
            int rankId;
            if (!ServerRankManager.ServerToRnak.TryGetValue(serverId, out rankId))
            {
                return;
            }
            RankManager rank;
            if (!ServerRankManager.Ranks.TryGetValue(rankId, out rank))
            {
                return;
            }
            rank.ChangeData(serverId, ServerRankManager.LevelRank, guid, Value, name);
        }

        //��������ս����
        public void ResetFightPoint(int serverId, string name, ulong guid, long FightPoint)
        {
            int rankId;
            if (!ServerRankManager.ServerToRnak.TryGetValue(serverId, out rankId))
            {
                return;
            }
            RankManager rank;
            if (!ServerRankManager.Ranks.TryGetValue(rankId, out rank))
            {
                return;
            }
            rank.ChangeData(serverId, ServerRankManager.SwordRank, guid, FightPoint, name);

            //�ҵ����������������޸ĸ����ս����
            var ranking = rank.GetRanking(3);
            var t = ranking.GetPlayerData(guid);
            if (t != null)
            {
                t.FightPoint = (int) FightPoint;
            }
        }

        //�������ý�Ǯ
        public void ResetMoney(int serverId, string name, ulong guid, long money)
        {
            int rankId;
            if (!ServerRankManager.ServerToRnak.TryGetValue(serverId, out rankId))
            {
                return;
            }
            RankManager rank;
            if (!ServerRankManager.Ranks.TryGetValue(rankId, out rank))
            {
                return;
            }
            rank.ChangeData(serverId, ServerRankManager.MoneyRank, guid, money, name);
        }

        //�������ü�԰�ȼ�
        public void ResetCityLevel(int serverId, string name, ulong guid, long value)
        {
            int rankId;
            if (!ServerRankManager.ServerToRnak.TryGetValue(serverId, out rankId))
            {
                return;
            }
            RankManager rank;
            if (!ServerRankManager.Ranks.TryGetValue(rankId, out rank))
            {
                return;
            }
            rank.ChangeData(serverId, ServerRankManager.HomeRank, guid, value, name);
        }

        //�������ó��ս��
        public void ResetWingsFight(int serverId, string name, ulong guid, long value)
        {
            int rankId;
            if (!ServerRankManager.ServerToRnak.TryGetValue(serverId, out rankId))
            {
                return;
            }
            RankManager rank;
            if (!ServerRankManager.Ranks.TryGetValue(rankId, out rank))
            {
                return;
            }
            rank.ChangeData(serverId, ServerRankManager.WingRank, guid, value, name);
        }

        //�������þ���ս��
        public void ResetPetFight(int serverId, string name, ulong guid, long value)
        {
            int rankId;
            if (!ServerRankManager.ServerToRnak.TryGetValue(serverId, out rankId))
            {
                return;
            }
            RankManager rank;
            if (!ServerRankManager.Ranks.TryGetValue(rankId, out rank))
            {
                return;
            }
            rank.ChangeData(serverId, ServerRankManager.PetRank, guid, value, name);
        }

        public void ResetMountRank(int serverId, string name, ulong guid, long value)
        {
            int rankId;
            if (!ServerRankManager.ServerToRnak.TryGetValue(serverId, out rankId))
            {
                return;
            }
            RankManager rank;
            if (!ServerRankManager.Ranks.TryGetValue(rankId, out rank))
            {
                return;
            }
            rank.ChangeData(serverId, ServerRankManager.MountRank, guid, value, name);
        }
        public void ChangePlayerName(int serverId, string name, ulong guid)
        {
            int rankId;
            if (!ServerRankManager.ServerToRnak.TryGetValue(serverId, out rankId))
            {
                return;
            }
            RankManager rank;
            if (!ServerRankManager.Ranks.TryGetValue(rankId, out rank))
            {
                return;
            }

                rank.ChangePlayerName(serverId, ServerRankManager.SwordRank, guid, name);
                rank.ChangePlayerName(serverId, ServerRankManager.LevelRank, guid, name);
                rank.ChangePlayerName(serverId, ServerRankManager.HomeRank, guid, name);
                rank.ChangePlayerName(serverId, ServerRankManager.MoneyRank, guid, name);
                rank.ChangePlayerName(serverId, ServerRankManager.WingRank, guid, name);
                rank.ChangePlayerName(serverId, ServerRankManager.P1vp1Rank, guid, name);
                rank.ChangePlayerName(serverId, ServerRankManager.PetRank, guid, name);
                rank.ChangePlayerName(serverId, ServerRankManager.TotalChargeDimaondRank, guid, name);
                rank.ChangePlayerName(serverId, ServerRankManager.DailyGiftRank, guid, name);
                rank.ChangePlayerName(serverId, ServerRankManager.WeeklyGiftRank, guid, name);
                rank.ChangePlayerName(serverId, ServerRankManager.TotalGiftRank, guid, name);
                rank.ChangePlayerName(serverId, ServerRankManager.MountRank, guid, name);
               
        }
        //�����ܳ�ֵ
		public void ResetTotalRecharge(int serverId, string name, ulong guid, long value)
        {
            int rankId;
            if (!ServerRankManager.ServerToRnak.TryGetValue(serverId, out rankId))
            {
                return;
            }
            RankManager rank;
            if (!ServerRankManager.Ranks.TryGetValue(rankId, out rank))
            {
                return;
            }
            rank.ChangeData(serverId, ServerRankManager.TotalChargeDimaondRank, guid, value, name);
        }

        // �����������а�
        public void ResetGiftRank(int rankType, int serverId, string name, ulong guid, long value)
        {
            var rankName = "";
            switch (rankType)
            {
                case (int)RankType.DailyGift:
                    rankName = ServerRankManager.DailyGiftRank;
                    break;
                case (int)RankType.WeeklyGift:
                    rankName = ServerRankManager.WeeklyGiftRank;
                    break;
                case (int)RankType.TotalGift:
                    rankName = ServerRankManager.TotalGiftRank;
                    break;
                default:
                    return;
            }

            ServerRankManager.TotalRank.ChangeData(serverId, rankName, guid, value, name);
        }

        //��ȡ���а�����
        public List<DBRank_One> GetRankDataByServerId(int serverId, int rankType, int minRank, int maxRank)
        {
            int rankId;
            if (!ServerRankManager.ServerToRnak.TryGetValue(serverId, out rankId))
            {
                return null;
            }
            RankManager rank;
            if (!ServerRankManager.Ranks.TryGetValue(rankId, out rank))
            {
                return null;
            }
            return rank.GetRankData(rankType, minRank, maxRank);
        }

        public List<DBRank_One> GetTotalRankData(int rankType, int minRank, int maxRank)
        {
            return ServerRankManager.TotalRank.GetRankData(rankType, minRank, maxRank);
        }

        //���ĳ�����а�
        public Ranking GetRankByType(int serverId, int rankType)
        {
            int rankId;
            if (!ServerRankManager.ServerToRnak.TryGetValue(serverId, out rankId))
            {
                return null;
            }
            RankManager rank;
            if (!ServerRankManager.Ranks.TryGetValue(rankId, out rank))
            {
                return null;
            }
            return rank.GetRanking(rankType);
        }

        //�Ƚ�ĳ�����а����� �� ���ID�Ƿ����
        public int CompareRank(int serverId, int rankType, ulong guid, int rankIndex)
        {
            var ranking = GetRankByType(serverId, rankType);
            if (ranking == null)
            {
                return -1;
            }
            var rankOne = ranking.GetRankOneByIndex(rankIndex);
            if (rankOne == null)
            {
                return -1;
            }
            if (rankOne.Guid == guid)
            {
                return 1;
            }
            return 0;
        }

        public long GetRankData(int serverId, int rankType, int rankIndex)
        {
            var ranking = GetRankByType(serverId, rankType);
            if (ranking == null)
            {
                return -1;
            }
            var maxIdx = ranking.GetRankCount() - 1;
            rankIndex = Math.Min(maxIdx, rankIndex);
            var rankOne = ranking.GetRankOneByIndex(rankIndex);
            if (rankOne == null)
            {
                return -1;
            }
            return rankOne.Value;
        }

        #endregion
    }

    public static class ServerRankManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public static IServerRankManager mImpl;
		public static string SwordRank = "sword_rank"; //ս����
        public static string LevelRank = "level_rank"; //�ȼ�
		public static string HomeRank = "home_rank"; //��԰�ȼ�
        public static string MoneyRank = "money_rank"; //�Ƹ�
		public static string WingRank = "wing_rank"; //�������
		public static string P1vp1Rank = "p1vp1_rank"; //1v1����
		public static string PetRank = "pet_rank"; //��԰�ȼ�
		public static string TotalChargeDimaondRank = "totalchargediamond_rank"; //��������
        public static string DailyGiftRank = "dailygift_rank"; //ÿ������
        public static string WeeklyGiftRank = "weeklygift_rank"; //ÿ������
        public static string TotalGiftRank = "totalgift_rank"; //�����ܰ�
        public static string MountRank = "mount_rank";  // ��������

        public static Dictionary<int, DBServerNameList> Names = new Dictionary<int, DBServerNameList>();
        public static Dictionary<int, RankManager> Ranks = new Dictionary<int, RankManager>(); //rankId -> rankData
        public static Dictionary<int, List<int>> RnakToServer = new Dictionary<int, List<int>>(); //rankId->serverId
        public static Dictionary<int, int> ServerAvgLevel = new Dictionary<int, int>();
        public static Dictionary<int, int> ServerToRnak = new Dictionary<int, int>(); //serverId -> rankId

        public static RankManager TotalRank = new RankManager();

        static ServerRankManager()
        {
            RankServer.Instance.UpdateManager.InitStaticImpl(typeof (ServerRankManager),
                typeof (ServerRankManagerDefaultImpl),
                o => { mImpl = (IServerRankManager) o; });
        }

        //��ʼ��
        public static IEnumerator Init(Coroutine coroutine)
        {
            return mImpl.Init(coroutine);
        }

        //�洢
        public static IEnumerator RefreshAll(Coroutine coroutine)
        {
            return mImpl.RefreshAll(coroutine);
        }

        //��PvP������ȡ��ĳ����ҵ�����
        public static void ResetPvPLadder(int serverId, ulong guid)
        {
            int rankId;
            if (!ServerToRnak.TryGetValue(serverId, out rankId))
            {
                return;
            }
            RankManager rank;
            if (!Ranks.TryGetValue(rankId, out rank))
            {
                return;
            }

            var ranking = rank.GetRanking(3);
            if (ranking == null)
            {
                return;
            }
            //var t = ranking.GetPlayerData(guid);
            ranking.RemoveCharacter(guid);
        }

        #region ״̬��־

        public static void ShowLog()
        {
            mImpl.ShowLog();
        }

        #endregion

        ////��ȡ����
        //public static IEnumerator GetCharacterName(Coroutine coroutine, int serverId)
        //{
        //    var tasks = RankServer.Instance.DB.Get<DBServerNameList>(coroutine, DataCategory.Rank, string.Format("CharacterName_{0}", serverId));
        //    yield return tasks;
        //    if (tasks.Data == null)
        //    {
        //        Names[serverId] = new DBServerNameList();
        //        yield break;
        //    }
        //    Names[serverId] = tasks.Data;
        //}

        ////д���ݿ�
        //public static IEnumerator SaveCharacterName(Coroutine coroutine, int serverId)
        //{
        //    DBServerNameList nameList;
        //    if (!Names.TryGetValue(serverId, out nameList))
        //    {
        //        Logger.Warn("SaveCharacterName faild serverId={0}", serverId);
        //        yield break;
        //    }
        //    var ret = RankServer.Instance.DB.Set(coroutine, DataCategory.Rank, string.Format("CharacterName_{0}", serverId), nameList);
        //    yield return ret;
        //}
        ////�������
        //public static void PushName(int serverId, ulong guid, string name)
        //{
        //    DBServerNameList nameList;
        //    if (!Names.TryGetValue(serverId, out nameList))
        //    {
        //        Logger.Warn("PushName faild serverId={0} guid={1} name={2}", serverId, guid, name);
        //        return;
        //    }
        //    nameList.mData[guid] = name;
        //}
        ////�������
        //public static string GetName(int serverId, ulong guid)
        //{
        //    DBServerNameList nameList;
        //    if (!Names.TryGetValue(serverId, out nameList))
        //    {
        //        Logger.Warn("GetName faild serverId={0} guid={1} ", serverId, guid);
        //        return "";
        //    }
        //    string oldName;
        //    if (!nameList.mData.TryGetValue(guid, out oldName))
        //    {
        //        Logger.Warn("GetName faild serverId={0} guid={1} ", serverId, guid);
        //        return "";
        //    }
        //    return oldName;
        //}
        ////ɾ������Ҫ������
        //public static void DeleteName(int serverId, ulong guid)
        //{
        //    DBServerNameList nameList;
        //    if (!Names.TryGetValue(serverId, out nameList))
        //    {
        //        Logger.Warn("DeleteName faild serverId={0} guid={1}", serverId, guid);
        //        return;
        //    }
        //    nameList.mData.Remove(guid);
        //}
        public static List<DBRank_One> GetFightRankList(int serverid, int rankType)
        {
            return mImpl.GetFightRankList(serverid,rankType);
        }
        #region �޸�����

        //�������õȼ�
        public static void ResetLevel(int serverId, string name, ulong guid, long value)
        {
            mImpl.ResetLevel(serverId, name, guid, value);
        }


        //��������ս����
        public static void ResetFightPoint(int serverId, string name, ulong guid, long fightPoint)
        {
            mImpl.ResetFightPoint(serverId, name, guid, fightPoint);
        }

        //�������ý�Ǯ
        public static void ResetMoney(int serverId, string name, ulong guid, long money)
        {
            mImpl.ResetMoney(serverId, name, guid, money);
        }

        //�������ü�԰�ȼ�
        public static void ResetCityLevel(int serverId, string name, ulong guid, long value)
        {
            mImpl.ResetCityLevel(serverId, name, guid, value);
        }

        //�������ó��ս��
        public static void ResetWingsFight(int serverId, string name, ulong guid, long value)
        {
            mImpl.ResetWingsFight(serverId, name, guid, value);
        }

        //�������þ���ս��
        public static void ResetPetFight(int serverId, string name, ulong guid, long value)
        {
            mImpl.ResetPetFight(serverId, name, guid, value);
        }

		//�����ܳ�ֵ��
		public static void ResetTotalRecharge(int serverId, string name, ulong guid, long value)
		{
			mImpl.ResetTotalRecharge(serverId, name, guid, value);
		}

        public static void ResetGiftRank(int rankType, int serverId, string charName, ulong guid, long value)
        {
            mImpl.ResetGiftRank(rankType, serverId, charName, guid, value);
        }
        public static void ResetMountRank(int serverId, string name, ulong guid, long value)
        {
            mImpl.ResetMountRank(serverId, name, guid, value);
        }
        public static void ChangePlayerName(int serverId, string name, ulong guid)
        {
            mImpl.ChangePlayerName(serverId, name, guid);
        }
        //��ȡ���а�����
        public static List<DBRank_One> GetRankDataByServerId(int serverId, int rankType, int minRank, int maxRank)
        {
            return mImpl.GetRankDataByServerId(serverId, rankType, minRank, maxRank);
        }

        //���ĳ�����а�
        public static Ranking GetRankByType(int serverId, int rankType)
        {
            return mImpl.GetRankByType(serverId, rankType);
        }

        // ��ȡȫ�����������а�����
        public static List<DBRank_One> GetTotalRankData(int rankType, int minRank, int maxRank)
        {
            return mImpl.GetTotalRankData(rankType, minRank, maxRank);
        }

        //�Ƚ�ĳ�����а����� �� ���ID�Ƿ����
        public static int CompareRank(int serverId, int rankType, ulong guid, int rankIndex)
        {
            return mImpl.CompareRank(serverId, rankType, guid, rankIndex);
        }

        //�Ƚ�ĳ�����а����� �� ���ID�Ƿ����
        public static long GetRankData(int serverId, int rankType, int rankIndex)
        {
            return mImpl.GetRankData(serverId, rankType, rankIndex);
        }

        #endregion
    }
}