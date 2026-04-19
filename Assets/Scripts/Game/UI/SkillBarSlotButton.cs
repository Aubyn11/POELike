using UnityEngine;
using UnityEngine.EventSystems;

namespace POELike.Game.UI
{
    /// <summary>
    /// 技能栏槽位点击组件。
    /// 挂在角色主面板的技能槽节点上，负责把点击转发给技能栏控制器。
    /// </summary>
    [DisallowMultipleComponent]
    public class SkillBarSlotButton : MonoBehaviour, IPointerClickHandler
    {
        private CharactorMainPanelController _owner;
        private int _slotIndex = -1;

        public void Bind(CharactorMainPanelController owner, int slotIndex)
        {
            _owner = owner;
            _slotIndex = slotIndex;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
                return;

            if (BagItemView.CurrentDraggingItem != null || eventData.dragging)
                return;

            _owner?.OnSkillSlotClicked(_slotIndex);
            eventData.Use();
        }
    }
}
