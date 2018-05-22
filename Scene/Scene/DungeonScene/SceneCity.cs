#region using

using System;
using System.Collections;
using System.Collections.Generic;
using DataContract;
using DataTable;
using Scorpion;
using Mono.GameMath;

#endregion

namespace Scene
{
    public class SceneCity : DungeonScene
    {
        private List<BuildingData> BuildingList = new List<BuildingData>();

        public IEnumerator GetCity(Coroutine coroutine, ulong characterId)
        {
            var dbLogicSimple = SceneServer.Instance.LogicAgent.GetLogicSimpleData(characterId, 0);
            yield return dbLogicSimple.SendAndWaitUntilDone(coroutine);

            if (dbLogicSimple.State != MessageState.Reply)
            {
                yield break;
            }

            BuildingList = dbLogicSimple.Response.City.Data;

            //��Npc
            foreach (var buildingData in BuildingList)
            {
                if (buildingData.PetList.Count <= 0)
                {
                    continue;
                }

                var tableArea = Table.GetHomeSence(buildingData.AreaId);
                if (null == tableArea)
                {
                    continue;
                }

                var dataId = buildingData.PetList[0];
                if (dataId == -1)
                {
                    continue;
                }
                var pos = new Vector2(tableArea.RetinuePosX, tableArea.RetinuePosY);
                var dir = new Vector2((float) Math.Cos(tableArea.FaceCorrection),
                    (float) Math.Sin(tableArea.FaceCorrection));
                CreateNpc(null, dataId, pos, dir);
            }
        }

        public override void OnCreate()
        {
            base.OnCreate();

            var characterId = Guid; //����Ǳ����һ�£���԰������Guid���Ǽ�԰ӵ���ߵ�CharacterId
            CoroutineFactory.NewCoroutine(GetCity, characterId).MoveNext();
        }

        public override void OnPlayerEnterOver(ObjPlayer player)
        {
            base.OnPlayerEnterOver(player);
            var msg = new BuildingList();
            msg.Data.AddRange(BuildingList);
            var list = new List<ulong>();
            list.Add(player.ObjId);
            SceneServer.Instance.ServerControl.SyncSceneBuilding(list, msg);
        }

        public void SyncCityData(List<BuildingData> buildings)
        {
            var msg = new BuildingList();
            msg.Data.AddRange(buildings);
            SceneServer.Instance.ServerControl.SyncSceneBuilding(EnumAllPlayerId(), msg);
        }
    }
}