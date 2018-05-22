#region using

using System.Collections.Generic;
using DataContract;
using ProtoBuf;

#endregion

namespace Shared
{
    public enum CharacterState
    {
        Created = 0,
        LoadData = 1,
        EnterGame = 2,
        PrepareData = 3,
        Connected = 4
    }

    public interface ICharacterController
    {
        /// <summary>
        ///     �����ɫ�Ƿ�����
        /// </summary>
        bool Online { get; }

        CharacterState State { get; set; }

        /// <summary>
        ///     ��Ӧ�¼�, count ��ʾ��ǰ������ܴ��������ܱ�����
        /// </summary>
        void ApplyEvent(int eventId, string evt, int count);

        /// <summary>
        ///     ÿ����ɫ�Ķ�ʽ�¼�
        /// </summary>
        /// <returns></returns>
        List<TimedTaskItem> GetTimedTasks();
    }

    public interface ICharacterControllerBase<T, ST> : ICharacterController
        where T : IExtensible
        where ST : IExtensible
    {
        T GetData();
        ST GetSimpleData();

        /// <summary>
        ///     ʹ��CharacterId��ʼ��DB������, ����ʼ���Լ�������
        /// </summary>
        /// <param name="characterId"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        T InitByBase(ulong characterId, object[] args = null);

        /// <summary>
        ///     ʹ��DB�����ݳ�ʼ��
        /// </summary>
        /// <param name="characterId"></param>
        /// <param name="dbData"></param>
        /// <returns></returns>
        bool InitByDb(ulong characterId, T dbData);

        /// <summary>
        ///     ����ҵ�ʵ��Ҫ���ӷ�������ɾ����ʱ�����
        ///     ��Ҫ�ѵ�ǰ�����������ʵ�������ö��Ͽ�
        /// </summary>
        void OnDestroy();

        void OnSaveData(T data, ST simpleData);

        /// <summary>
        ///     ����update
        /// </summary>
        void Tick();
    }

    public interface ICharacterControllerBase<T, ST, VT> : ICharacterController
        where T : IExtensible
        where ST : IExtensible
        where VT : IExtensible
    {
        /// <summary>
        ///     ʹ���۵��޸Ķ�CharacterController��Ч
        /// </summary>
        /// <param name="data">���۵��޸�</param>
        void ApplyVolatileData(VT data);

        T GetData();
        ST GetSimpleData();

        /// <summary>
        ///     ʹ��CharacterId��ʼ��DB������, ����ʼ���Լ�������
        /// </summary>
        /// <param name="characterId"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        T InitByBase(ulong characterId, object[] args = null);

        /// <summary>
        ///     ʹ��DB�����ݳ�ʼ��
        /// </summary>
        /// <param name="characterId"></param>
        /// <param name="dbData"></param>
        /// <returns></returns>
        bool InitByDb(ulong characterId, T dbData);

        void LoadFinished();

        /// <summary>
        ///     ����ҵ�ʵ��Ҫ���ӷ�������ɾ����ʱ�����
        ///     ��Ҫ�ѵ�ǰ�����������ʵ�������ö��Ͽ�
        /// </summary>
        void OnDestroy();

        void OnSaveData(T data, ST simpleData);

        /// <summary>
        ///     ����update
        /// </summary>
        void Tick();
    }


    public interface ICharacterSimpleController
    {
        
    }


    public interface ICharacterControllerSimpleBase<SDT, VDT> : ICharacterController
        where SDT : IExtensible
        where VDT : IExtensible
    {
    }
}