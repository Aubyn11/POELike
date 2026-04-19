using System;
using System.Collections.Generic;
using POELike.Game;
using UnityEngine;
using UnityEngine.UI;

namespace POELike.Game.UI
{
    /// <summary>
    /// 传送门面板。
    /// 打开时读取地图关卡配置，并根据条目数量动态创建地图按钮。
    /// </summary>
    public class DoorPanel : UIGamePanel
    {
        [Header("UI 引用")]
        [SerializeField] private ListBox _mapList;

        public event Action<MapLevelData> MapSelected;

        protected override void Awake()
        {
            base.Awake();

            if (_mapList == null)
            {
                var child = transform.Find("MapList");
                if (child != null)
                    _mapList = child.GetComponent<ListBox>();

                if (_mapList == null)
                    Debug.LogWarning("[DoorPanel] 未找到 MapList 或其上没有 ListBox 组件，请检查 DoorPanel 预制体。");
            }
        }

        public new void Open()
        {
            if (IsOpen)
                Close();

            base.Open();
        }

        protected override void OnOpen()
        {
            RefreshMapItems();
        }

        protected override void OnClose_Internal()
        {
            _mapList?.Clear();
        }

        private void RefreshMapItems()
        {
            if (_mapList == null)
            {
                Debug.LogError("[DoorPanel] _mapList 未赋值，无法刷新地图列表。");
                return;
            }

            MapLevelConfigLoader.Reload();
            IReadOnlyList<MapLevelData> maps = MapLevelConfigLoader.Levels;

            _mapList.Clear();

            if (maps == null || maps.Count == 0)
            {
                Debug.LogWarning("[DoorPanel] 当前没有可显示的地图关卡配置。");
                return;
            }

            _mapList.AddItem(0, maps.Count);

            if (_mapList.Count != maps.Count)
            {
                Debug.LogError($"[DoorPanel] 期望创建 {maps.Count} 个地图条目，实际只创建了 {_mapList.Count} 个。请确认 DoorPanel 的 ListBox 已绑定 MapBtn.prefab。");
                return;
            }

            for (int i = 0; i < maps.Count; i++)
                ApplyMapDataToItem(_mapList.GetItemByIndex(i), maps[i]);

            _mapList.RefreshLayout();
        }

        private void ApplyMapDataToItem(ListBoxItem item, MapLevelData mapData)
        {
            if (item == null || mapData == null)
                return;

            var itemGo = item.GetCtrl();
            string mapName = string.IsNullOrWhiteSpace(mapData.MapName)
                ? $"地图 {mapData.MapID}"
                : mapData.MapName;

            var text = itemGo.GetComponentInChildren<Text>(true);
            if (text != null)
                text.text = mapName;
            else
                Debug.LogWarning("[DoorPanel] MapBtn 条目上未找到 Text 组件，无法显示地图名称。");

            var button = itemGo.GetComponent<Button>();
            if (button == null)
                return;

            string mapId = mapData.MapID;
            string sceneId = mapData.SceneID;
            string cfgId = mapData.CfgID;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                Debug.Log($"[DoorPanel] 选择地图：{mapName}（MapID={mapId}, SceneID={sceneId}, CfgID={cfgId}）");
                MapSelected?.Invoke(mapData);
            });
        }

    }
}
