#region using

using System;
using DataContract;
using Scorpion;
using NLog;
using Shared;

#endregion

namespace Login
{
    public enum ConnectState
    {
        NotFind = -1, //δ�ҵ�
        Wait = 0, //�����Ŷ�
        Landing = 1, //���ڵ�½
        EnterGame = 2, //���ڽ���Ϸ,����֤EntergameȨ��ͨ��
        //EnterGame1 = 3, //Login���ؽ�ɫ���
        //EnterGame2 = 4, //Login���ؽ�ɫ�ĳ����������
        //EnterGame3 = 5, //Login��ɫNotifyBrokerPrepareData���
        //EnterGame4 = 6, //Login���ؽ�ɫ���
        //EnterGame5 = 7, //Login���ؽ�ɫ���


        InGame = 66, //��Ϸing

        WaitReConnet = 67, //�ȴ�������

        OffLine = 99, //����ing
        WaitOffLine = 100 //�ȴ�������ing
    }


    public interface IPlayerConnect
    {
        //�رն�ʱ��
        void DeleteTrigger(PlayerConnect _this);
        //������
        void OnLost(PlayerConnect _this);
        //��֪�Ŷ�����
        void SendClientQueueIndex(int index, PlayerConnect _this);
        //��֪�Ŷӳɹ���
        void SendClientQueueSuccess(PlayerConnect _this, QueueType type, ulong characterId);
        void StartTrigger(int mins, PlayerConnect _this);
        //ʱ�����
        void TimeOver(PlayerConnect _this);
    }


    public class PlayerConnectDefaultImpl : IPlayerConnect
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public void SendClientQueueIndex(int index, PlayerConnect _this)
        {
            LoginServer.Instance.ServerControl.NotifyQueueIndex(_this.ClientId, index);
        }

        public void SendClientQueueSuccess(PlayerConnect _this, QueueType type, ulong characterId)
        {
            var data = new QueueSuccessData();
            data.Type = (int) type;
            switch (type)
            {
                case QueueType.Login:
                {
                    LoginServerControlDefaultImpl.NotifyGateClientState(LoginServer.Instance.ServerControl,
                        _this.ClientId, 0, GateClientState.Login);
                    data.LastServerId = _this.Player.DbData.LastServerId;

                    _this.Player.DbData.LoginDay++;
                    _this.Player.DbData.LoginTotal++;
                }
                    break;
                case QueueType.EnterGame:
                    data.CharacterId = characterId;
                    break;
            }

            PlayerLog.WriteLog((int) LogType.SendClientQueueSuccess, "SendClientQueueSuccess type = {0}, name = {1}",
                type, _this.Name);
            LoginServer.Instance.ServerControl.NotifyQueueSuccess(_this.ClientId, data);
        }

        public void OnLost(PlayerConnect _this)
        {
            switch (_this.State)
            {
                case ConnectState.Wait:
                {
                    QueueManager.TotalList.Remove(_this.ClientId);
                    _this.State = ConnectState.WaitOffLine;
                    var key = _this.GetKey();
                    PlayerConnect oldKey;
                    if (QueueManager.CacheLost.TryGetValue(key, out oldKey))
                    {
                        Logger.Warn("PlayerConnet Onlost Same!! key={0},ClientId{1}", key, _this.ClientId);
                        QueueManager.CacheLost[key] = _this;
                    }
                    else
                    {
                        QueueManager.CacheLost.TryAdd(key, _this);
                    }
                }
                    break;
                case ConnectState.Landing:
                {
                    QueueManager.TotalList.Remove(_this.ClientId);
                    _this.State = ConnectState.OffLine;
                    var key = _this.GetKey();
                    QueueManager.CacheLost.TryAdd(key, _this);
                    if (_this.Player == null)
                    {
                        Logger.Error("PlayerConnect onLost  state is Landing player is null");
                        _this.ClientId = 0;
                        return;
                    }
                    QueueManager.LandingPlayerList.Remove(_this.Player.DbData.Id);
                }
                    break;
                case ConnectState.EnterGame:
                {
                    var key = _this.GetKey();
                    _this.State = ConnectState.OffLine;
                    QueueManager.TotalList.Remove(_this.ClientId);
                    QueueManager.CacheLost.TryAdd(key, _this);
                    if (_this.Player == null)
                    {
                        Logger.Error("PlayerConnect onLost  state is EnterGame player is null");
                        _this.ClientId = 0;
                        return;
                    }
                    QueueManager.EnterGamePlayerList.Remove(_this.Player.DbData.Id);
                }
                    break;
                case ConnectState.InGame:
                {
                    _this.State = ConnectState.OffLine;
                    var key = _this.GetKey();
                    QueueManager.CacheLost.TryAdd(key, _this);
                    if (_this.Player == null)
                    {
                        Logger.Error("PlayerConnect onLost  state is InGame player is null");
                        _this.ClientId = 0;
                        return;
                    }
                    PlayerConnect oldPlayer;
                    if (QueueManager.InGamePlayerList.TryGetValue(_this.Player.DbData.Id, out oldPlayer))
                    {
                        if (oldPlayer != null)
                        {
                            QueueManager.LeaverPlayer(oldPlayer);
                        }
                        QueueManager.InGamePlayerList.Remove(_this.Player.DbData.Id);
                    }
                }
                    break;
                case ConnectState.OffLine:
                {
                    Logger.Error("PlayerConnect onLost  state is OffLine");
                }
                    break;
                case ConnectState.WaitReConnet:
                {
                    _this.State = ConnectState.OffLine;
                    var key = _this.GetKey();
                    QueueManager.CacheLost.TryAdd(key, _this);
                }
                    break;
            }
            _this.ClientId = 0;
            _this.Player = null;
        }

        public void StartTrigger(int mins, PlayerConnect _this)
        {
            if (_this.trigger != null)
            {
                LoginServerControl.Timer.ChangeTime(ref _this.trigger, DateTime.Now.AddMinutes(mins));
            }
            else
            {
                _this.trigger = LoginServerControl.Timer.CreateTrigger(DateTime.Now.AddMinutes(mins), _this.TimeOver);
            }
        }

        public void TimeOver(PlayerConnect _this)
        {
            PlayerLog.WriteLog((int) LogType.PlayerConnect, "TimeOver -----State:{0}", _this.State);
            _this.trigger = null;
            switch (_this.State)
            {
                case ConnectState.Wait:
                    break;
                case ConnectState.Landing:
                {
                    //todo ѡ���ɫʱ��̫��
                    if (_this.Player != null)
                    {
                        _this.Player.Kick(_this.ClientId, KickClientType.LoginTimeOut);
                        CoroutineFactory.NewCoroutine(LoginServer.Instance.ServerControl.PlayerLogout, _this.ClientId)
                            .MoveNext();
                    }
                }
                    break;
                case ConnectState.EnterGame:
                {
                    //todo ������Ϸʱ��̫��
                    if (_this.Player != null)
                    {
                        _this.Player.Kick(_this.ClientId, KickClientType.LoginTimeOut);
                        CoroutineFactory.NewCoroutine(LoginServer.Instance.ServerControl.PlayerLogout, _this.ClientId)
                            .MoveNext();
                    }
                }
                    break;
                case ConnectState.InGame:
                    break;
                case ConnectState.WaitReConnet:
                {
                    if (null == _this.Player || null == _this.Player.DbData)
                    {
                        _this.ClientId = 0;
                        Logger.Error("PlayerConnect TimeOver WaitReConnet InGame player is null");
                        break;
                    }

                    //var character = CharacterManager.Instance.GetCharacterControllerFromMemroy(_this.Player.DbData.SelectChar);
                    //if (character != null)
                    //{
                    //    character.LostLine();
                    //    CharacterManager.PopServerPlayer(character.mDbData.ServerId);
                    //    CoroutineFactory.NewCoroutine(CharacterManager.Instance.RemoveCharacter,
                    //        _this.Player.DbData.SelectChar).MoveNext();
                    //}

                    PlayerLog.PlayerLogger(_this.Player.DbData.Id, "WaitReConnet TimeOver ClientId={0}", _this.ClientId);
                    PlayerConnect oldPlayer;
                    if (QueueManager.InGamePlayerList.TryGetValue(_this.Player.DbData.Id, out oldPlayer))
                    {
                        if (oldPlayer != null)
                        {
                            QueueManager.LeaverPlayer(oldPlayer);
                        }
                        QueueManager.InGamePlayerList.Remove(_this.Player.DbData.Id);
                    }

                    LoginServerControlDefaultImpl.CleanCharacterData(_this.ClientId, _this.Player.DbData.SelectChar);

                    CoroutineFactory.NewCoroutine(LoginServer.Instance.ServerControl.PlayerLogout, _this.ClientId)
                        .MoveNext();
                }
                    break;
                case ConnectState.OffLine:
                    QueueManager.CacheLost.Remove(_this.GetKey());
                    break;
                case ConnectState.WaitOffLine:
                    QueueManager.CacheLost.Remove(_this.GetKey());
                    break;
            }
        }

        public void DeleteTrigger(PlayerConnect _this)
        {
            if (_this.trigger != null)
            {
                PlayerLog.WriteLog(1111222, "DeleteTrigger-----State:{0}", _this.State);
                LoginServerControl.Timer.DeleteTrigger(_this.trigger);
                _this.trigger = null;
            }
        }
    }

    public class PlayerConnect
    {
        private static IPlayerConnect mImpl;

        static PlayerConnect()
        {
            LoginServer.Instance.UpdateManager.InitStaticImpl(typeof (PlayerConnect), typeof (PlayerConnectDefaultImpl),
                o => { mImpl = (IPlayerConnect) o; });
        }

        public PlayerConnect(string t, ulong c, string n, ConnectState cs = ConnectState.Wait)
        {
            Type = t;
            ClientId = c;
            Name = n;
            State = cs;
        }

        public ulong ClientId; //�ͻ���ID
        public bool IsOnline = true;
        public ConnectState mState;
        public DateTime mStateTime;
        public string Name; //�˺���(���type����-1 ��ô�ǵ�������Key)
        public PlayerController Player;
        //���Ӷ�ʱ��
        public Trigger trigger;
        public string Type; //��ҵ�½���ͣ�-1�����˺ŵ�½)
        public int WaitDesconnetCount = 0;

        public ConnectState State
        {
            get { return mState; }
            set
            {
                PlayerLog.WriteLog((int) LogType.PlayerConnect, "State:{0}---From:{1}",
                    value, mState);
                mState = value;
                mStateTime = DateTime.Now;

                switch (State)
                {
                    case ConnectState.Wait:
                        DeleteTrigger();
                        break;
                    case ConnectState.Landing:
                        StartTrigger();
                        break;
                    case ConnectState.EnterGame:
                        StartTrigger(1);
                        break;
                    case ConnectState.InGame:
                        DeleteTrigger();
                        break;
                    case ConnectState.OffLine:
                        StartTrigger();
                        break;
                    case ConnectState.WaitOffLine:
                        DeleteTrigger();
                        break;
                    case ConnectState.WaitReConnet:
                        StartTrigger(1); //���������ĵȴ�ʱ��
                        break;
                }
            }
        }

        public DateTime StateTime
        {
            get { return mStateTime; }
        }

        //�رն�ʱ��
        public void DeleteTrigger()
        {
            mImpl.DeleteTrigger(this);
        }

        //��õ�½Key
        public string GetKey()
        {
            return GetLandingKey(Type, Name);
        }

        //��õ�½Key
        public static string GetLandingKey(string type, string name)
        {
            return string.Format("LK_{0}_{1}", type, name);
        }

        //������
        public void OnLost()
        {
            mImpl.OnLost(this);
        }

        //��֪�Ŷ�����
        public void SendClientQueueIndex(int index)
        {
            mImpl.SendClientQueueIndex(index, this);
        }

        //��֪�Ŷӳɹ���
        public void SendClientQueueSuccess(QueueType type, ulong characterId)
        {
            mImpl.SendClientQueueSuccess(this, type, characterId);
        }

        public void StartTrigger(int mins = 5)
        {
            mImpl.StartTrigger(mins, this);
        }

        //ʱ�����
        public void TimeOver()
        {
            mImpl.TimeOver(this);
        }
    }
}