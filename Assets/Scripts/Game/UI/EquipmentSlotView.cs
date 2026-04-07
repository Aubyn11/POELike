using POELike.ECS.Components;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using POELike.ECS.Systems;
using POELike.Managers;

namespace POELike.Game.UI
{
    /// <summary>
    /// 角色装备栏槽位视图。
    /// 挂载在背包界面右侧的装备槽节点上，负责接收装备拖拽。
    /// </summary>
    public class EquipmentSlotView : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerClickHandler,
        IDropHandler
    {
        [SerializeField] private Image _bgImage;
        [SerializeField] private float _itemPadding = 8f;
        [SerializeField] private Color _normalColor  = new Color(0f, 0f, 0f, 0.45f);
        [SerializeField] private Color _validColor   = new Color(0.25f, 0.75f, 0.25f, 0.75f);
        [SerializeField] private Color _invalidColor = new Color(0.80f, 0.20f, 0.20f, 0.75f);

        public EquipmentSlot SlotType { get; private set; }
        public RectTransform ContentRoot => transform as RectTransform;
        public float ItemPadding => _itemPadding;
        public BagItemView PlacedItem { get; private set; }

        private void Awake()
        {
            if (_bgImage == null)
            {
                if (transform.childCount > 0)
                    _bgImage = transform.GetChild(0).GetComponent<Image>();

                _bgImage ??= GetComponent<Image>();
            }

            RefreshVisual();
        }

        public void Setup(EquipmentSlot slotType)
        {
            SlotType = slotType;
            RefreshVisual();
        }

        public bool CanAccept(BagItemView itemView)
        {
            return itemView != null &&
                   itemView.Data != null &&
                   itemView.Data.IsEquippable &&
                   itemView.Data.CanEquipToSlot(SlotType);
        }

        public bool TryAccept(BagItemView itemView, PointerEventData eventData = null)
        {
            if (!CanAccept(itemView))
                return false;

            var sourceBag    = itemView.CurrentBag;
            var sourceSlot   = itemView.CurrentSlot;
            var sourceSocket = itemView.CurrentSocket;
            var replacedItem = PlacedItem != null && PlacedItem != itemView ? PlacedItem : null;
            var replacedRuntimeItem = replacedItem != null ? GetEquippedItemData() : null;

            if (sourceSlot == this && PlacedItem == itemView)
            {
                itemView.BindToEquipmentSlot(this);
                itemView.MarkDropHandled();
                itemView.CompleteMove();
                RefreshVisual();
                return true;
            }

            if (sourceBag != null)
                sourceBag.RemoveItem(itemView.Data);
            if (sourceSlot != null && sourceSlot != this)
                sourceSlot.ClearPlacedItem(itemView);
            if (sourceSocket != null)
                sourceSocket.ClearPlacedGem(itemView);

            PlacedItem = itemView;
            itemView.BindToEquipmentSlot(this);
            SyncEquippedItem(itemView);
            itemView.MarkDropHandled();
            itemView.CompleteMove();
            RefreshVisual();

            if (replacedItem?.Data != null)
                replacedItem.Data.RuntimeItemData = replacedRuntimeItem;

            if (replacedItem != null)
                replacedItem.TryBeginMove(eventData);

            return true;
        }

        public void ClearPlacedItem(BagItemView itemView = null)
        {
            if (itemView != null && PlacedItem != itemView)
                return;

            var removedItem = PlacedItem;
            PlacedItem = null;
            SyncUnequippedItem(removedItem);
            RefreshVisual();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            var draggingItem = BagItemView.CurrentDraggingItem;
            if (draggingItem == null)
                return;

            SetHighlight(CanAccept(draggingItem));
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            RefreshVisual();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            var movingItem = BagItemView.CurrentDraggingItem;
            if (movingItem == null)
            {
                if (PlacedItem != null)
                    PlacedItem.TryBeginMove(eventData);
                return;
            }

            if (!TryAccept(movingItem, eventData))
                SetHighlight(false);

            eventData?.Use();
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (!TryAccept(BagItemView.CurrentDraggingItem, eventData))
                SetHighlight(false);
        }

        private void SetHighlight(bool valid)
        {
            if (_bgImage == null)
                return;

            _bgImage.color = valid ? _validColor : _invalidColor;
        }

        private void RefreshVisual()
        {
            if (_bgImage == null)
                return;

            _bgImage.color = _normalColor;
        }

        private void SyncEquippedItem(BagItemView itemView)
        {
            if (itemView?.Data == null)
                return;

            var world = GameManager.Instance?.World;
            var playerEntity = world?.FindEntityByTag("Player");
            var equipment = playerEntity?.GetComponent<EquipmentComponent>();
            if (world == null || playerEntity == null || equipment == null)
                return;

            var oldItem = equipment.GetEquipped(SlotType);
            var newItem = itemView.Data.ToItemData();
            equipment.Equip(SlotType, newItem);

            world.EventBus.Publish(new EquipmentChangedEvent
            {
                Entity = playerEntity,
                Slot = SlotType,
                OldItem = oldItem,
                NewItem = newItem,
            });
        }

        private void SyncUnequippedItem(BagItemView itemView)
        {
            var world = GameManager.Instance?.World;
            var playerEntity = world?.FindEntityByTag("Player");
            var equipment = playerEntity?.GetComponent<EquipmentComponent>();
            if (world == null || playerEntity == null || equipment == null)
                return;

            var oldItem = equipment.GetEquipped(SlotType);
            if (oldItem == null)
                return;

            equipment.Unequip(SlotType);
            if (itemView?.Data != null)
                itemView.Data.RuntimeItemData = oldItem;

            world.EventBus.Publish(new EquipmentChangedEvent
            {
                Entity = playerEntity,
                Slot = SlotType,
                OldItem = oldItem,
                NewItem = null,
            });
        }

        private ItemData GetEquippedItemData()
        {
            var world = GameManager.Instance?.World;
            var playerEntity = world?.FindEntityByTag("Player");
            var equipment = playerEntity?.GetComponent<EquipmentComponent>();
            if (world == null || playerEntity == null || equipment == null)
                return null;

            return equipment.GetEquipped(SlotType);
        }
    }
}