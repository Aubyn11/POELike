using POELike.Game.Equipment;
using POELike.Managers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace POELike.Game.UI
{
    /// <summary>
    /// 插槽格子视图，挂载在 Socket.prefab 根节点上。
    /// 由 <see cref="EquipmentItem"/> 动态创建，
    /// 既负责显示插槽颜色，也负责接收宝石拖拽。
    /// </summary>
    public class SocketItem : ListBoxItem, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IDropHandler
    {
        [SerializeField] private Image _icon; // 插槽圆形图标
        [SerializeField] private float _itemPadding = 6f;

        // 各颜色对应的显示色
        private static readonly Color ColorRed   = new Color(0.90f, 0.25f, 0.20f);
        private static readonly Color ColorGreen = new Color(0.20f, 0.80f, 0.30f);
        private static readonly Color ColorBlue  = new Color(0.25f, 0.50f, 1.00f);
        private static readonly Color ColorWhite = new Color(0.90f, 0.90f, 0.90f);

        private SocketColor _socketColor = SocketColor.White;
        private Color       _baseColor   = Color.white;
        private EquipmentItem _ownerEquipment;
        private int           _socketIndex = -1;

        public RectTransform ContentRoot => transform as RectTransform;
        public float ItemPadding => _itemPadding;

        public BagItemView PlacedGem { get; private set; }

        private void Awake()
        {
            _icon ??= GetComponent<Image>();
            RefreshVisual();
        }

        /// <summary>
        /// 初始化插槽显示和拖拽接收能力。
        /// </summary>
        public void SetupSocket(EquipmentItem ownerEquipment, int socketIndex, SocketColor socketColor)
        {
            _ownerEquipment = ownerEquipment;
            _socketIndex = socketIndex;
            SetSocket(socketColor);
            RefreshVisual();
        }

        /// <summary>
        /// 设置插槽颜色
        /// </summary>
        public void SetSocket(SocketColor socketColor)
        {
            _socketColor = socketColor;
            _baseColor = socketColor switch
            {
                SocketColor.Red   => ColorRed,
                SocketColor.Green => ColorGreen,
                SocketColor.Blue  => ColorBlue,
                _                 => ColorWhite,
            };

            if (_icon != null)
                _icon.color = _baseColor;
        }

        public bool CanAccept(BagItemView itemView)
        {
            return itemView != null &&
                   itemView.Data != null &&
                   itemView.Data.CanFitSocket(_socketColor);
        }

        public bool TryAccept(BagItemView itemView, PointerEventData eventData = null)
        {
            if (!CanAccept(itemView))
                return false;

            var sourceBag    = itemView.CurrentBag;
            var sourceSlot   = itemView.CurrentSlot;
            var sourceSocket = itemView.CurrentSocket;
            var replacedGem  = PlacedGem != null && PlacedGem != itemView ? PlacedGem : null;

            if (sourceSocket == this && PlacedGem == itemView)
            {
                itemView.BindToSocket(this);
                itemView.MarkDropHandled();
                itemView.CompleteMove();
                RefreshVisual();
                _ownerEquipment?.SetSocketedGem(_socketIndex, itemView.Data);
                return true;
            }

            PlacedGem = itemView;

            if (sourceBag != null)
                sourceBag.RemoveItem(itemView.Data);
            if (sourceSlot != null)
                sourceSlot.ClearPlacedItem(itemView);
            if (sourceSocket != null && sourceSocket != this)
                sourceSocket.ClearPlacedGem(itemView);

            itemView.BindToSocket(this);
            itemView.MarkDropHandled();
            itemView.CompleteMove();
            RefreshVisual();
            _ownerEquipment?.SetSocketedGem(_socketIndex, itemView.Data);

            if (replacedGem != null)
                replacedGem.TryBeginMove(eventData);

            UIManager.Instance?.RefreshCharactorMainPanel();
            return true;
        }

        public void ClearPlacedGem(BagItemView itemView = null)
        {
            if (itemView != null && PlacedGem != itemView)
                return;

            if (PlacedGem == null)
                return;

            PlacedGem = null;
            _ownerEquipment?.SetSocketedGem(_socketIndex, null);
            RefreshVisual();
            UIManager.Instance?.RefreshCharactorMainPanel();
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
            if (movingItem != null)
            {
                if (!TryAccept(movingItem, eventData))
                    SetHighlight(false);

                eventData?.Use();
                return;
            }

            if (PlacedGem != null)
            {
                PlacedGem.TryBeginMove(eventData);
                return;
            }

            var parentItem = GetComponentInParent<BagItemView>();
            parentItem?.TryBeginMove(eventData);
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (!TryAccept(BagItemView.CurrentDraggingItem, eventData))
                SetHighlight(false);
        }

        private void SetHighlight(bool valid)
        {
            if (_icon == null) return;

            var targetColor = valid
                ? new Color(0.35f, 0.85f, 0.35f)
                : new Color(0.90f, 0.25f, 0.25f);

            _icon.color = Color.Lerp(_baseColor, targetColor, 0.6f);
        }

        private void RefreshVisual()
        {
            if (_icon == null) return;
            _icon.color = _baseColor;
        }
    }
}