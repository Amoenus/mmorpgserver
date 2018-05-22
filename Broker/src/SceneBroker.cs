#region using

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Linq;
using System.Threading;
using DataContract;
using Scorpion;
using ProtoBuf;

using Shared;

#endregion

namespace Broker
{
    public partial class SceneBroker : CommonBroker
    {
//         private void NotifyLost(SocketClient socketClient, ulong characterId, Action<ServiceDesc> callback)
//         {
//             Logger.Info("NotifyLost {0}, {1}.", characterId, socketClient.RemoteEndPoint);
// 
//             ServiceDesc desc = new ServiceDesc();
//             desc.Type = (int)MessageType.Lost;
//             desc.CharacterId = characterId;
//             desc.PacketId = GetUniquePacketId();
// 
//             var act = new Action<bool, ServiceDesc>((b, item) =>
//             {
//                 if (b)
//                 {
//                     if (item.Error == 0)
//                     {
//                         Logger.Info("Scene server Lost replied {0}", characterId);
// 
//                         callback(item);
//                     }
//                     else
//                     {
//                         Logger.Error("NotifyLost failed {0}....", item.Error);
//                     }
//                 }
//                 else
//                 {
//                     Logger.Error("NotifyLost timeout....");
//                 }
//             });
// 
//             RegisterCallback(desc.PacketId, act);
// 
//             socketClient.SendMessage(desc);
// 
//         }


        public void ChangeScene(ulong characterId, SceneInfo sceneInfo)
        {
            var info = GetCharacter(characterId);
            if (info == null)
            {
                return;
            }

            Action changeScene = () =>
            {
                var oldSceneInfo = info.SceneInfo;
                if (oldSceneInfo != sceneInfo)
                {
                    sceneInfo.CharacterIds.Add(characterId);
                    oldSceneInfo.CharacterIds.Remove(characterId);
                    info.SceneInfo = sceneInfo;
                }
                //Logger.Error("change scene 2: {0}, {1}, {2}", characterId, oldSceneInfo.Server.RemoteEndPoint, sceneInfo.Server.RemoteEndPoint);

                DebugCounter[4]++;
                var act = new Action(() =>
                {
                    Logger.Info("Enter Game {0} - ChangeScene - 4 - {1}", characterId,
                        TimeManager.Timer.ElapsedMilliseconds);
                    ChangeSceneOver(info, oldSceneInfo, sceneInfo, b =>
                    {
                        if (b)
                        {
                            // ������˻������Ǿʹ����ݿ����ȡ���꣬���һ�����
                            mSceneManager.NotifyEnterScene(info, 0, new SceneParam());
                        }
                        else
                        {
                            // ���û���������ǾͲ��ô���������
                            mSceneManager.NotifyEnterScene(info, 10, new SceneParam());
                        }

                        info.State = CharacterInfoState.Connected;
                        if (info.WaitingChangeSceneAction != null)
                        {
                            var call = info.WaitingChangeSceneAction;
                            info.WaitingChangeSceneAction = null;
                            call();
                        }
                    });
                });

                if (sceneInfo.Status == SceneStatus.ReadyToEnter)
                {
                    DebugCounter[6]++;
                    act();
                }
                else
                {
                    DebugCounter[7]++;
                    sceneInfo.WaitingActions.Add(act);
                }
            };

            if (info.State == CharacterInfoState.Transfer)
            {
                info.WaitingChangeSceneAction = changeScene;
            }
            else
            {
                info.State = CharacterInfoState.Transfer;
                changeScene();
            }
        }

        /// <summary>
        ///     �л���Ŀ�곡��
        /// </summary>
        /// <param name="info">��ɫ��Ϣ</param>
        /// <param name="oldScene">��ɫ֮ǰ�ĳ�����Ϣ</param>
        /// <param name="newScene">��ɫҪȥ���³�����Ϣ</param>
        /// <param name="callback">��ɺ�Ļص���bool���͵Ĳ�����ʾ�Ƿ��л��˻���</param>
        private void ChangeSceneOver(CharacterSceneInfo info,
                                     SceneInfo oldScene,
                                     SceneInfo newScene,
                                     Action<bool> callback)
        {
            DebugCounter[8]++;
            PlayerLog.WriteLog(888, "ChangeSceneStart characterId={0},oldScene={1},newScene={2}", info.CharacterId,
                oldScene.SceneGuid, newScene.SceneGuid);
            //Logger.Info("Enter Game {0} - ChangeSceneOver - 1 - {1}", info.CharacterId, TimeManager.Timer.ElapsedMilliseconds);
            //Logger.Info("Enter Game {0} - ChangeSceneOver - 2 - {1}", info.CharacterId, TimeManager.Timer.ElapsedMilliseconds);
            // �������ͬһ̨�����ϣ���֪ͨ��һ̨�������ߣ�Ȼ��֪ͨ��һ̨��������
            if (oldScene.Server != newScene.Server)
            {
                DebugCounter[10]++;
                //Logger.Info("Enter Game {0} - ChangeSceneOver - 3 - {1}", info.CharacterId, TimeManager.Timer.ElapsedMilliseconds);
                UnloadData(info.CharacterId, oldScene, serviceDesc2 =>
                {
                    DebugCounter[11]++;
                    NotifyConnect(info, serviceDesc =>
                    {
                        DebugCounter[14]++;

                        if (callback != null)
                        {
                            callback(true);
                        }
                    });
                    //Logger.Info("Enter Game {0} - ChangeSceneOver - 4 - {1}", info.CharacterId, TimeManager.Timer.ElapsedMilliseconds);
                    //ChangeTheShortCut(info, () =>
                    //{
                    //    Logger.Info("Enter Game {0} - ChangeSceneOver - 5 - {1},{2} - {3}", info.CharacterId,
                    //        oldScene.Server.RemoteEndPoint, newScene.Server.RemoteEndPoint, TimeManager.Timer.ElapsedMilliseconds);
                    //});
                });
            }
            else
            {
                Logger.Info("Enter Game {0} - ChangeSceneOver - 2 - {1}", info.CharacterId,
                    TimeManager.Timer.ElapsedMilliseconds);
                DebugCounter[12]++;

                info.SceneInfo = newScene;

                if (callback != null)
                {
                    callback(false);
                }
            }
        }

        private void ChangeTheShortCut(CharacterSceneInfo info, Action callback)
        {
            // ���GateId
            var gateId = ((info.ClientId & 0xFFFF000000000000UL) >> 48);

            Logger.Info("Enter Game {0} {1} - ChangeTheShortCut - 1 - {2}", info.CharacterId, gateId,
                TimeManager.Timer.ElapsedMilliseconds);

            GateProxy gate;
            if (mGates.TryGetValue((int) gateId, out gate))
            {
                Logger.Info("Enter Game {0} - ChangeTheShortCut - 2  - {1}", info.CharacterId,
                    TimeManager.Timer.ElapsedMilliseconds);

                var gateDesc = new ServiceDesc();
                gateDesc.Type = 20;
                gateDesc.CharacterId = info.CharacterId;
                gateDesc.ServiceType = (int) ServiceType.Scene;
                gateDesc.ClientId = info.ClientId;
                gateDesc.Routing.Add(gateId);
                gateDesc.PacketId = GetUniquePacketId();

                Action<bool, ServiceDesc> act = (b, desc) =>
                {
                    if (b)
                    {
                        Logger.Info("Enter Game {0} - ChangeTheShortCut - 3 - {1}", info.CharacterId,
                            TimeManager.Timer.ElapsedMilliseconds);
                        callback();
                    }
                    else
                    {
                        Logger.Error("ChangeTheShortCut {0} error.", info.CharacterId);
                    }
                };

                RegisterCallback(gateDesc.PacketId, act);

                info.SceneInfo.Server.SendMessage(gateDesc);
            }
            else
            {
                Logger.Error("Can not find gate {0}.", gateId);
            }
        }

        #region �����������첽������Gate,  �������з�������

        public override void ClientMessageReceived(ServerClient client, ServiceDesc desc)
        {
            try
            {
                var type = (MessageType) desc.Type;
                Logger.Debug("ClientMessageReceived ,type={0},FuncId={1}", type, desc.FuncId);
                switch (type)
                {
                    case MessageType.CS:
                        Logger.Error("CommenBroker ClientMessageReceived MessageType.CS is Error! FuncId={0}",
                            desc.FuncId);
                        //OnSocketListenerMessageReceiveCsSync(client, desc);
                        break;
                    case MessageType.SC:
                        Logger.Error("CommenBroker ClientMessageReceived MessageType.SC is Error! FuncId={0}",
                            desc.FuncId);
                        break;
                    case MessageType.SS:
                        SSfunc(client, desc);
                        //Logger.Error("CommenBroker ClientMessageReceived MessageType.SS is Error! FuncId={0}", desc.FuncId);
                        break;
                    case MessageType.Connect:
                        OnSocketListenerMessageReceiveConnectEx(client, desc);
                        break;
                    case MessageType.Lost:
                        Logger.Error("CommenBroker ClientMessageReceived MessageType.Lost is Error! FuncId={0}",
                            desc.FuncId);
                        break;
                    case MessageType.Sync:
                        Logger.Error("CommenBroker ClientMessageReceived MessageType.Sync is Error! FuncId={0}",
                            desc.FuncId);
                        //OnSocketListenerMessageReceiveCsSync(client, desc);
                        break;
                    case MessageType.Ping:
                        break;
                    case MessageType.SB:
                        OnSocketListenerMessageReceiveSB(client, desc);
                        break;
                    case MessageType.BS:
                        Logger.Error("CommenBroker ClientMessageReceived MessageType.BS is Error! FuncId={0}",
                            desc.FuncId);
                        break;
                    case MessageType.SCAll:
                        Logger.Error("CommenBroker ClientMessageReceived MessageType.SCAll is Error!");
                        break;
                    case MessageType.SCServer:
                        OnSocketListenerMessageReceiveSCServer(client, desc);
                        break;
                    case MessageType.SCList:
                        OnSocketListenerMessageReceiveSCList(client, desc);
                        break;
                    case MessageType.SAS:
                        OnSocketListenerMessageReceiveSAS(client, desc);
                        break;
                    case MessageType.PrepareData:
                        OnSocketListenerMessageReceivePrepareDataEx(client, desc);
                        break;
                    case MessageType.SASReply:
                        Logger.Error("CommenBroker ClientMessageReceived MessageType.SASReply is Error!");
                        break;
                    default:
                        Logger.Error("CommenBroker ClientMessageReceived is Error!type={0}", type);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Some error inside ClientMessageReceived ");
            }
        }

        #endregion

        private void NotifyConnect(CharacterSceneInfo info, Action<ServiceDesc> callback)
        {
            ConnectLostLogger.Info("client {0} - {1} SceneBroker NotifyConnect 1", info.ClientId, info.CharacterId);
            Logger.Info("NotifyConnect {0}, {1}.", info.CharacterId, info.SceneInfo.Server.RemoteEndPoint);
            var desc = new ServiceDesc();
            desc.Type = (int) MessageType.SS;
            desc.FuncId = 3501;
            desc.CharacterId = info.CharacterId;
            desc.ClientId = info.ClientId;
            desc.PacketId = GetUniquePacketId();

            var content = new __RPC_Scene_SSNotifyCharacterOnConnet_ARG_uint64_clientId_uint64_characterId__();
            content.CharacterId = info.CharacterId;
            content.ClientId = info.ClientId;
            desc.Data = ProtocolExtension.Serialize(content);
            var act = new Action<bool, ServiceDesc>((b, item) =>
            {
                if (b)
                {
                    if (item.Error == 0)
                    {
                        ConnectLostLogger.Info("client {0} - {1} SceneBroker NotifyConnect 3", info.ClientId,
                            info.CharacterId);
                        Logger.Info("Scene server Connected replied {0}", info.CharacterId);

                        callback(item);
                    }
                    else
                    {
                        ConnectLostLogger.Error("client {0} - {1} SceneBroker NotifyConnect 4", info.ClientId,
                            info.CharacterId);
                        Logger.Error("NotifyConnect failed {0}....", item.Error);
                    }
                }
                else
                {
                    ConnectLostLogger.Error("client {0} - {1} SceneBroker NotifyConnect 5", info.ClientId,
                        info.CharacterId);
                    Logger.Error("NotifyConnect timeout....");
                }
            });

            ConnectLostLogger.Info("client {0} - {1} SceneBroker NotifyConnect 2", info.ClientId, info.CharacterId);
            RegisterCallback(desc.PacketId, act);
            info.SceneInfo.Server.SendMessage(desc);
        }

        public override void Status(ConcurrentDictionary<string, string> dict)
        {
            if (mFrontEndServer == null)
            {
                return;
            }
            try
            {
                dict.TryAdd("_Listening", mFrontEndServer.IsListening.ToString());
                //dict.TryAdd("ByteReceivedPerSecond", mFrontEndServer.ByteReceivedPerSecond.ToString());
                //dict.TryAdd("ByteSendPerSecond", mFrontEndServer.ByteSendPerSecond.ToString());
                //dict.TryAdd("MessageReceivedPerSecond", mFrontEndServer.MessageReceivedPerSecond.ToString());
                //dict.TryAdd("MessageSendPerSecond", mFrontEndServer.MessageSendPerSecond.ToString());

                //dict.TryAdd("MaxByteReceivedPerSecond", mFrontEndServer.MaxByteReceivedPerSecond.ToString());
                //dict.TryAdd("MaxByteSendPerSecond", mFrontEndServer.MaxByteSendPerSecond.ToString());
                //dict.TryAdd("MaxMessageReceivedPerSecond", mFrontEndServer.MaxMessageReceivedPerSecond.ToString());

                //dict.TryAdd("ConnectionCount", mFrontEndServer.ConnectionCount.ToString());
                //dict.TryAdd("AcceptPoolCount", mFrontEndServer.AcceptPoolCount.ToString());
                //dict.TryAdd("SendRecvPoolCount", mFrontEndServer.SendRecvPoolCount.ToString());
                //dict.TryAdd("WaitingSendMessageCount", mFrontEndServer.WaitingSendMessageCount.ToString());

                //var count = 0;
                //foreach (var sceneInfo in mFromSceneGuid2Server)
                //{
                //    dict.TryAdd("Scene " + sceneInfo.Key,
                //        sceneInfo.Value.CharacterIds.Count + " scene:" + sceneInfo.Value.SceneId + " server:" +
                //        sceneInfo.Value.ServerId);
                //    count += sceneInfo.Value.CharacterIds.Count;
                //}

                //dict.TryAdd("Total Character Count", count.ToString());
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "SceneBroker Status Error1!{0}");
            }

            //foreach (var scene in mBackEnds)
            //{
            //    try
            //    {
            //        if (scene.IsConnected)
            //        {
            //            var sum = 0;
            //            foreach (var i in ((SceneServerUserData) scene.UserData).Scenes)
            //            {
            //                sum += i.CharacterIds.Count;
            //            }
            //            dict.TryAdd("Server " + scene.RemoteEndPoint, sum.ToString());
            //        }
            //    }
            //    catch (Exception)
            //    {
            //        return;
            //    }
            //}

            //try
            //{
            //    var index = 0;
            //    foreach (var agent in mBackEnds)
            //    {
            //        dict.TryAdd("Server" + index + " Latency", agent.Latency.ToString());
            //        dict.TryAdd("Server" + index + " ByteReceivedPerSecond", agent.ByteReceivedPerSecond.ToString());
            //        dict.TryAdd("Server" + index + " ByteSendPerSecond", agent.ByteSendPerSecond.ToString());
            //        dict.TryAdd("Server" + index + " MessageReceivedPerSecond",
            //            agent.MessageReceivedPerSecond.ToString());
            //        dict.TryAdd("Server" + index + " MessageSendPerSecond", agent.MessageSendPerSecond.ToString());

            //        index++;
            //    }
            //}
            //catch (Exception ex)
            //{
            //    Logger.Error("SceneBroker Status Error3!{0}", ex);
            //}

//             for (int i = 0; i < DebugCounter.Length; i++)
//             {
//                 sb.AppendLine("Debug-" + i + ":" + DebugCounter[i]);
//             }
        }

        private class ChangeSceneInfo
        {
            public CharacterSceneInfo Info;
            public SceneInfo NewSceneInfo;
            public SceneInfo OldSceneInfo;
        }

        #region ���ݽṹ

        private int mPacketId;
        private long mUniqueId;
        //private long mPackageCount = 0;

        //private readonly ConcurrentDictionary<ulong, ulong> mFromClientId2CharacterId =new ConcurrentDictionary<ulong, ulong>(); //key=ClientId value = CharacterId

        //private readonly ConcurrentDictionary<ulong, GateProxy> mFromCharacterId2Gate =new ConcurrentDictionary<ulong, GateProxy>(); //key=CharacterId value = Gate

        //private readonly ConcurrentDictionary<ulong, ulong> mFromCharacterId2ClientId =new ConcurrentDictionary<ulong, ulong>(); //key=CharacterId value = ClientId


        public int[] DebugCounter = new int[20]; //����ͳ��


        //public readonly ConcurrentDictionary<ulong, CharacterSceneInfo> mFromCharacterId2Server = new ConcurrentDictionary<ulong, CharacterSceneInfo>();  //��� -> CharacterSceneInfo

        //�����������
        private SceneManagerBroker mSceneManager;


        //SceneGuid -> sceneInfo
        private ConcurrentDictionary<ulong, SceneInfo> mFromSceneGuid2Server
        {
            get { return mSceneManager.mFromSceneGuid2Server; }
        }

        //key = severId + SceneId ,value = List<SceneInfo>
        private ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, SceneInfo>> mFromServerIdAndSceneId2Guid
        {
            get { return mSceneManager.mFromServerIdAndSceneId2Guid; }
        }


        //�������ӵ�Gate
        private readonly Dictionary<int, GateProxy> mGates = new Dictionary<int, GateProxy>();

        //���߳�ת���߳�  �¼�����
        private readonly BlockingCollection<Action> mWaitingEvents = new BlockingCollection<Action>();
        private Thread WorkingThread;

        //�̰߳�ȫ�Ļص�
        private readonly ConcurrentDictionary<uint, CallbackItem> mCallback =
            new ConcurrentDictionary<uint, CallbackItem>();

        //ʱ�����
        public TimeDispatcher mTimeDispatcher = new TimeDispatcher("Broker");
        //�ͻ���ID ���ӵ����пͻ���
        //private readonly ConcurrentDictionary<ulong, ServerClient> mFromClientIdToClient = new ConcurrentDictionary<ulong, ServerClient>();

        //��ɫ����
        //public CharacterManagerBroker mCharacterManager;


        // ׼������ά��һ����ҽ�ɫ���ݣ� CharacterId ->  ��������
        //private ConcurrentDictionary<ulong, CharacterSceneInfo> mCharacterInfo
        //{
        //    get { return mCharacterManager.mCharacterInfo; }
        //}

        #endregion

        #region ��ȡ���ݽӿ�

        public override UserData CreateUserData(int serverId)
        {
            return new SceneServerUserData {Id = serverId};
        }

        public override CharacterInfo CreateCharacter(ulong CharacterId)
        {
            var character = new CharacterSceneInfo();
            character.CharacterId = CharacterId;
            return character;
        }

        public CharacterSceneInfo GetCharacter(ulong characterId)
        {
            var c = GetCharacterInfo(characterId);
            if (c != null)
            {
                return (CharacterSceneInfo) c;
            }
            return null;
        }

        //��ȡ�����ID
        public uint GetUniquePacketId()
        {
            return (uint) Interlocked.Increment(ref mPacketId);
        }

        //��ȡ�����ID
        public uint GetUniqueId()
        {
            return (uint) Interlocked.Increment(ref mUniqueId);
        }


        //����CharacterId ѡ�������
        public SocketClient SelectServerForCharacter(ulong characterId)
        {
            return mBackEnds[0];
        }

        //�ж� �������� ����ҡ� �Ƿ�һ������
        private bool CheckSameMachine(SceneInfo sceneInfo1, CharacterSceneInfo info)
        {
            return sceneInfo1.Server == info.SceneInfo.Server;
        }

        #endregion

        #region ��������

        public override void BackEndsOnConnect(SocketClient client, int index)
        {
            if (index == 0)
            {
                SceneServerManager.PushServer(client, 2);
            }
            else
            {
                SceneServerManager.PushServer(client, 0);
            }
        }

        public override void Start(int id, int nPort, string type, dynamic[] serverList)
        {
            mSceneManager = new SceneManagerBroker(this, type);
            mUniqueId = (long) (DateTime.Now - DateTime.Parse("2015-01-01")).TotalMilliseconds;
            mTimeDispatcher.Start();

            base.Start(id, nPort, type, serverList);

            mTimeDispatcher.RegisterTimedEvent(TimeSpan.FromMinutes(1), MergeScene);


            //���̼߳���¼�����
            WorkingThread = new Thread(() =>
            {
                mWaitingEvents.GetConsumingEnumerable().ToObservable().Subscribe(act =>
                {
                    if (act != null)
                    {
                        try
                        {
                            act();
                        }
                        catch (Exception exception)
                        {
                            Logger.Error(exception, "Process {0} error.", act.Method.Name);
                        }
                    }
                });
            });

            WorkingThread.Start();
        }

        public override void Stop()
        {
            mWaitingEvents.CompleteAdding();
            WorkingThread.Join();
            mWaitingEvents.Dispose();
            base.Stop();
            mTimeDispatcher.Stop();
        }

        #endregion

        #region δ֪������

        public void RegisterCallback(uint id, Action<bool, ServiceDesc> callback)
        {
            var handle = mTimeDispatcher.RegisterTimedEvent(TimeSpan.FromSeconds(30), () =>
            {
                CallbackItem item;
                mCallback.TryRemove(id, out item);

                if (!mWaitingEvents.IsAddingCompleted)
                {
                    mWaitingEvents.Add(() => { callback(false, null); });
                }
            });

            mCallback.TryAdd(id, new CallbackItem {TimeHandle = handle, Callback = callback});
        }


        public void ServerDisconnected(SocketClient b)
        {
            foreach (var scene in ((SceneServerUserData) b.UserData).Scenes)
            {
                scene.Status = SceneStatus.Crashed;

                // ����Ӧ�ð���������ɾ���Ŀǰ����Ҳ�ܹ�����ÿ�������ߺ󣬻���Լ�������ˢ��
                foreach (var characterId in scene.CharacterIds)
                {
                    CharacterInfo character;
                    ulong cid;
                    if (mCharacterInfoManager.TryRemove(characterId, out character))
                    {
                        var clientId = character.ClientId;
                        mFromClientId2CharacterId.TryRemove(clientId, out cid);
                    }
                }

                SceneInfo sceneInfo;
                ConcurrentDictionary<ulong, SceneInfo> scenes;
                mFromSceneGuid2Server.TryRemove(scene.SceneGuid, out sceneInfo);
                if (sceneInfo != null && mFromServerIdAndSceneId2Guid.TryGetValue(
                    SceneManagerBroker.CalcServerSceneId(sceneInfo.ServerId, sceneInfo.SceneId),
                    out scenes))
                {
                    scenes.TryRemove(scene.SceneGuid, out sceneInfo);
                    //scenes.RemoveAll(item => item.SceneGuid == scene.SceneGuid);
                }

                scene.CharacterIds.Clear();
            }
        }

        #endregion

        #region �ͻ��������첽������Scene��������

        public override void OnSocketClientDisconnected(SocketClient b)
        {
            if (!mWaitingEvents.IsAddingCompleted)
            {
                mWaitingEvents.Add(() => { ServerDisconnected(b); });
            }
            //Logger.Debug("Server {0} at {1}:{2} Disconnected.", mServiceName, serverItem.Ip, serverItem.Port);
        }

        // �������յ���Ϣ

        public override void ServerOnMessageReceived(ServiceDesc desc)
        {
            try
            {
                var type = (MessageType) desc.Type;
                Logger.Debug("ServerOnMessageReceived ,type={0},FuncId={1}", type, desc.FuncId);
                switch (type)
                {
                    case MessageType.CS:
                        Logger.Error("SceneBroker ServerOnMessageReceived MessageType.CS is Error! FuncId={0}",
                            desc.FuncId);
                        break;
                    case MessageType.SC:
                        Logger.Error("SceneBroker ServerOnMessageReceived MessageType.SC is Error! FuncId={0}",
                            desc.FuncId);
                        break;
                    case MessageType.Connect:
                        Logger.Error("SceneBroker ServerOnMessageReceived MessageType.Connect is Error! FuncId={0}",
                            desc.FuncId);
                        break;
                    case MessageType.Lost:
                        Logger.Error("SceneBroker ServerOnMessageReceived MessageType.Lost is Error! FuncId={0}",
                            desc.FuncId);
                        break;
                    case MessageType.Sync:
                        Logger.Error("SceneBroker ServerOnMessageReceived MessageType.Sync is Error! FuncId={0}",
                            desc.FuncId);
                        break;
                    case MessageType.Ping:
                        break;
                    case MessageType.SB:
                        Logger.Error("SceneBroker ServerOnMessageReceived MessageType.SB is Error! FuncId={0}",
                            desc.FuncId);
                        break;
                    case MessageType.BS:
                        OnSocketListenerMessageReceiveBS(desc);
                        return;
                    case MessageType.SCAll:
                        Logger.Error("SceneBroker ServerOnMessageReceived MessageType.SCAll is Error!");
                        break;
                    case MessageType.SCServer:
                        OnSocketClientMessageReceivedSCServer(desc);
                        return;
                    case MessageType.SCList:
                        OnSocketClientMessageReceivedSCList(desc);
                        return;
                    case MessageType.SS:
                    case MessageType.SAS:
                    case MessageType.PrepareData:
                        var routing = GetRouting(desc);
                        if (routing == ulong.MaxValue)
                        {
                            if (desc.FuncId == 3501)
                            {
                                OnNotifyConnected(desc);
                                return;
                            }
                            return;
                        }
                        if (desc.FuncId == 3051)
                        {
                            var chara = GetCharacter(desc.CharacterId);
                            //Logger.Fatal("PrepareDataForEnterGame  sceneGuid = {0}", chara.SceneInfo.SceneGuid);
                            if (chara != null)
                            {
                                var content = new __RPC_Scene_PrepareDataForEnterGame_RET_uint64__();
                                content.ReturnValue = chara.SceneInfo.SceneGuid;
                                desc.Data = ProtocolExtension.Serialize(content);
                            }
                        }
                        mFrontEndServer.Clients[routing].SendMessage(desc);
                        return;
                    case MessageType.SASReply:
                        Logger.Error("SceneBroker ServerOnMessageReceived MessageType.SASReply is Error! FuncId={0}",
                            desc.FuncId);
                        break;
                    default:
                        Logger.Error("SceneBroker ServerOnMessageReceived MessageType.SB is Error! FuncId={0}",
                            desc.FuncId);
                        return;
                }
                var character = GetCharacterInfo(desc.CharacterId);
                if (character == null)
                {
                    Logger.Error(
                        "SceneBroker ServerOnMessageReceived character = null desc.CharacterId :{0} ,funcId={1},ServiceType={2},clientId={3},type={4}",
                        desc.CharacterId, desc.FuncId, desc.ServiceType, desc.ClientId, desc.Type);
                    return;
                }
                if (character.Gate == null)
                {
                    Logger.Error("Can not reply message for character 9 = null desc.CharacterI0d :{0} ",
                        desc.CharacterId);
                    return;
                }
                // desc.ClientId = character.ClientId;
                character.Gate.Gate.SendMessage(desc);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Some error inside ClientMessageReceived ");
            }
        }

        #endregion

        #region ���̺߳�������

        private void OnSceneFinished(ServiceDesc desc)
        {
            using (var ms = new MemoryStream(desc.Data, false))
            {
                var msg = Serializer.Deserialize<__RPC_Scene_NotifySceneFinished_ARG_uint64_guid__>(ms);
                SceneInfo info;
                if (mFromSceneGuid2Server.TryGetValue(msg.Guid, out info))
                {
                    info.Status = SceneStatus.Finished;
                }
            }
        }


        //֪ͨ���볡��
        public void NotifyLoginEnterScene(ServerClient client,
                                          CharacterSceneInfo info,
                                          ulong sceneGuid,
                                          ServiceDesc desc)
        {
            Logger.Info("Enter Game {0} - NotifyEnterScene - 1 - {1}", info.CharacterId,
                TimeManager.Timer.ElapsedMilliseconds);
            Logger.Info("NotifyEnterScene {0}, {1}.", info.CharacterId, info.SceneInfo.Server.RemoteEndPoint);

            var content = new __RPC_Scene_PrepareDataForEnterGame_RET_uint64__();
            content.ReturnValue = sceneGuid;

            var message = new ServiceDesc();
            message.FuncId = 3051;
            message.ServiceType = (int) ServiceType.Login;
            message.PacketId = desc.PacketId;
            message.Data = ProtocolExtension.Serialize(content);
            message.Type = desc.Type;
            message.CharacterId = desc.CharacterId;
            message.ClientId = desc.ClientId;

            client.SendMessage(message);
        }

        //���������������
        public override CharacterInfo OnSocketListenerMessageReceivePrepareDataEx(ServerClient client,
                                                                                  ServiceDesc desc,
                                                                                  bool isNeedSendReply = true)
        {
            using (var ms = new MemoryStream(desc.Data, false))
            {
                var msg = Serializer.Deserialize<PrepareDataMessage>(ms);
                var characterId = msg.CharacterId;
                var characterInfo = GetCharacter(characterId);
                if (characterInfo == null)
                {
                    //Commborker
                    characterInfo =
                        (CharacterSceneInfo) base.OnSocketListenerMessageReceivePrepareDataEx(client, desc, false);
                    //����Ŀ�곡��

                    //PrepareDataʱ�����ݺϷ�ID����
                    var serverLogicId = SceneExtension.GetServerLogicId(msg.ServerId);
                    var sceneInfo = mSceneManager.SelectOldScene(msg.SceneGuid, serverLogicId, msg.SceneId,
                        msg.CharacterId);
                    //����û�ҵ�����Ҫ�½�����
                    if (sceneInfo == null)
                    {
                        //���ݲ㽨�쳡��
                        sceneInfo = mSceneManager.CreateNewSceneInfo(serverLogicId, msg.SceneId, msg.SceneGuid);
                        sceneInfo.PushCharacter(desc.CharacterId);
                        //֪ͨԶ�˷��������쳡��
                        mSceneManager.CreateNewScene(sceneInfo);
                    }
                    characterInfo.Server = sceneInfo.Server;
                    //���캯���ص�
                    var act = new Action(() =>
                    {
                        //NotifyLoginEnterScene(client, characterInfo, sceneInfo.SceneGuid, desc);
                        //Logger.Fatal("PrepareDataForEnterGame  sceneGuid = {0}", sceneInfo.SceneGuid);
                        //var content = new __RPC_Scene_PrepareDataForEnterGame_RET_uint64__();
                        //content.ReturnValue = sceneInfo.SceneGuid;
                        //desc.Data = ProtocolExtension.Serialize(content);
                        client.SendMessage(desc);
                    });
                    //����ҷ��볡����
                    PutCharacterIntoScene(sceneInfo, characterInfo);
                    //�ж�״̬�����ص� ��ʱִ��
                    if (sceneInfo.Status == SceneStatus.ReadyToEnter)
                    {
                        act();
                    }
                    else
                    {
                        sceneInfo.WaitingActions.Add(act);
                    }
                    return characterInfo;
                }
                Logger.Error("OnSocketListenerMessageReceivePrepareDataEx  {0}", characterId);
                //var sceneInfo = mSceneManager.SelectOldScene(msg.SceneGuid, msg.ServerId, msg.SceneId, msg.CharacterId) ;

                //characterInfo.ClientId = msg.ClientId;

                //Logger.Info("Enter Game {0} - PrepareData - 7 - {1}", characterId, TimeManager.Timer.ElapsedMilliseconds);
                //// ��ǰ��Ҫ�ĳ��������ڣ���Ҫ�����³�����������ɫ���ݷ��䵽����
                //if (sceneInfo == null)
                //{
                //    Logger.Info("Enter Game {0} - PrepareData - 8 - {1}", characterId, TimeManager.Timer.ElapsedMilliseconds);
                //    // ֻ�����Ǹ�������
                //    var scene = Table.GetScene(msg.SceneId);
                //    if (SceneManagerBroker.IsNormalScene(scene))
                //    {
                //        Logger.Info("Enter Game {0} - PrepareData - 9 - {1}", characterId, TimeManager.Timer.ElapsedMilliseconds);
                //        sceneInfo = mSceneManager.CreateNewSceneInfo(msg.ServerId, msg.SceneId, msg.SceneGuid);
                //        sceneInfo.PushCharacter(msg.CharacterId);
                //        // create new scene
                //        mSceneManager.CreateNewScene(sceneInfo);

                //        var oldSceneInfo = characterInfo.SceneInfo;
                //        oldSceneInfo.CharacterIds.Remove(msg.CharacterId);
                //        characterInfo.SceneInfo = sceneInfo;
                //        //Logger.Error("prepare data 6: {0}, {1}, {2}", info.CharacterId, oldSceneInfo.Server.RemoteEndPoint, sceneInfo.Server.RemoteEndPoint);

                //        sceneInfo.WaitingActions.Add(() =>
                //        {
                //            Logger.Info("Enter Game {0} - PrepareData - 10 - {1}", characterId, TimeManager.Timer.ElapsedMilliseconds);
                //            if (!CheckSameMachine(sceneInfo, characterInfo))
                //            {
                //                Logger.Info("Enter Game {0} - PrepareData - 11 - {1}", characterId, TimeManager.Timer.ElapsedMilliseconds);
                //                UnloadData(characterInfo.CharacterId, oldSceneInfo, serviceDesc2 =>
                //                {
                //                    Logger.Info("Enter Game {0} - PrepareData - 12 - {1}", characterId, TimeManager.Timer.ElapsedMilliseconds);
                //                    client.SendMessage(desc);
                //                });
                //            }
                //            else
                //            {
                //                Logger.Info("Enter Game {0} - PrepareData - 13 - {1}", characterId, TimeManager.Timer.ElapsedMilliseconds);
                //                client.SendMessage(desc);
                //            }
                //        });
                //    }
                //    else
                //    {
                //        Logger.Info("Enter Game {0} - PrepareData - 14 - {1}", characterId, TimeManager.Timer.ElapsedMilliseconds);
                //        //����ʱ���ڸ��������Ǹ����Ѿ���������
                //        // create new scene
                //        sceneInfo = mSceneManager.CreateNewSceneInfo(msg.ServerId, 3, 0);
                //        sceneInfo.PushCharacter(msg.CharacterId);
                //        mSceneManager.CreateNewScene(sceneInfo);

                //        var oldSceneInfo = characterInfo.SceneInfo;
                //        oldSceneInfo.CharacterIds.Remove(msg.CharacterId);
                //        characterInfo.SceneInfo = sceneInfo;
                //        //Logger.Error("prepare data 5: {0}, {1}, {2}", info.CharacterId, oldSceneInfo.Server.RemoteEndPoint, sceneInfo.Server.RemoteEndPoint);

                //        sceneInfo.WaitingActions.Add(() =>
                //        {
                //            Logger.Info("Enter Game {0} - PrepareData - 15 - {1}", characterId, TimeManager.Timer.ElapsedMilliseconds);
                //            if (!CheckSameMachine(oldSceneInfo, characterInfo))
                //            {
                //                Logger.Info("Enter Game {0} - PrepareData - 16 - {1}", characterId, TimeManager.Timer.ElapsedMilliseconds);
                //                UnloadData(characterInfo.CharacterId, oldSceneInfo, serviceDesc2 =>
                //                {
                //                    client.SendMessage(desc);
                //                });
                //            }
                //            else
                //            {
                //                Logger.Info("Enter Game {0} - PrepareData - 17 - {1}", characterId, TimeManager.Timer.ElapsedMilliseconds);
                //                client.SendMessage(desc);
                //            }
                //        });
                //    }
                //}
                //// ֮ǰ������ݺͷ���ķ���������ͬһ̨������ϣ�֪֮ͨǰ�Ļ���ж������
                //else if (!CheckSameMachine(sceneInfo, characterInfo))
                //{
                //    var oldSceneInfo = characterInfo.SceneInfo;
                //    oldSceneInfo.CharacterIds.Remove(msg.CharacterId);
                //    characterInfo.SceneInfo = sceneInfo;
                //    //Logger.Error("prepare data 4: {0}, {1}, {2}", info.CharacterId, oldSceneInfo.Server.RemoteEndPoint, sceneInfo.Server.RemoteEndPoint);

                //    var act = new Action(() =>
                //    {
                //        Logger.Info("Enter Game {0} - PrepareData - 18 - {1}", characterId, TimeManager.Timer.ElapsedMilliseconds);
                //        UnloadData(characterInfo.CharacterId, oldSceneInfo, serviceDesc2 =>
                //        {
                //            Logger.Info("Enter Game {0} - PrepareData - 19 - {1}", characterId, TimeManager.Timer.ElapsedMilliseconds);
                //            client.SendMessage(desc);
                //        });
                //    });

                //    if (sceneInfo.Status == SceneStatus.ReadyToEnter)
                //    {
                //        act();
                //    }
                //    else
                //    {
                //        sceneInfo.WaitingActions.Add(act);
                //    }
                //}
                //else
                //{
                //    var oldSceneInfo = characterInfo.SceneInfo;
                //    if (oldSceneInfo != sceneInfo)
                //    {
                //        oldSceneInfo.CharacterIds.Remove(msg.CharacterId);
                //        characterInfo.SceneInfo = sceneInfo;
                //    }

                //    Logger.Info("Enter Game {0} - PrepareData - 20 - {1}", characterId, TimeManager.Timer.ElapsedMilliseconds);

                //    //Logger.Error("prepare data 3: {0}, {1}, {2}", info.CharacterId, oldSceneInfo.Server.RemoteEndPoint, sceneInfo.Server.RemoteEndPoint);
                //    client.SendMessage(desc);
                //    //NotifyEnterScene(sceneInfo.Server, info.CharacterId, sceneInfo.SceneGuid, (int)eScnenChangePostion.Db, new SceneParam());
                //}
                return null;
            }
        }

        //�����װ��scene
        private void PutCharacterIntoScene(SceneInfo sceneInfo, CharacterSceneInfo characterInfo)
        {
            characterInfo.SceneInfo = sceneInfo;
        }

        //��ĳ��������ж���������
        private void UnloadData(ulong characterId, SceneInfo oldSceneInfo, Action<ServiceDesc> callback)
        {
            PlayerLog.WriteLog(888, "UnloadData characterId={0},ServerId={1},SceneId={2},SceneGuid={3}", characterId,
                oldSceneInfo.ServerId, oldSceneInfo.SceneId, oldSceneInfo.SceneGuid);
            ConnectLostLogger.Info("character {0} SceneBroker UnloadData 1", characterId);
            var content = new __RPC_Scene_UnloadData_ARG_uint64_characterId__();
            content.CharacterId = characterId;


            var message = new ServiceDesc();
            message.FuncId = 3020;
            message.ServiceType = (int) ServiceType.Scene;
            message.PacketId = GetUniquePacketId();
            message.Data = ProtocolExtension.Serialize(content);
            message.Type = (int) MessageType.BS;
            message.CharacterId = characterId;

            Logger.Info("Notify Scene server UnloadData {0}", characterId);

            var act = new Action<bool, ServiceDesc>((b, item) =>
            {
                PlayerLog.WriteLog(888, "UnloadData characterId={0},result={1},error={2}", characterId, b, item.Error);
                if (b)
                {
                    if (item.Error == 0)
                    {
                        ConnectLostLogger.Info("character {0} SceneBroker UnloadData 3", characterId);
                        Logger.Info("Scene server UnloadData replied {0}", characterId);
                        callback(item);
                    }
                    else
                    {
                        ConnectLostLogger.Info("character {0} SceneBroker UnloadData 4", characterId);
                        Logger.Error("UnloadData failed {0}....", item.Error);
                    }
                }
                else
                {
                    ConnectLostLogger.Info("character {0} SceneBroker UnloadData 5", characterId);
                    Logger.Error("UnloadData timeout....");
                }
            });

            RegisterCallback(message.PacketId, act);


            ConnectLostLogger.Info("character {0} SceneBroker UnloadData 2", characterId);
            oldSceneInfo.Server.SendMessage(message);
        }


        //�ϲ�����(ÿ30��ִ��һ�Σ�
        public void MergeScene()
        {
            if (!mWaitingEvents.IsAddingCompleted)
            {
                mWaitingEvents.Add(mSceneManager.MergeSceneImpl);
                mTimeDispatcher.RegisterTimedEvent(TimeSpan.FromSeconds(30), MergeScene);
            }
        }

        #endregion
    }
}