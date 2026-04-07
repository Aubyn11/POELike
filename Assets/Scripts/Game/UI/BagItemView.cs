using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace POELike.Game.UI
{
    /// <summary>
    /// 背包道具视图。
    /// 挂载在装备/宝石的可视对象根节点上，负责点击拿起、点击放下、
    /// 以及在背包格子 / 装备槽 / 宝石孔之间切换父节点与布局。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public class BagItemView : MonoBehaviour, IPointerClickHandler
    {
        private const float BagItemPadding = 2f;

        private RectTransform _rt;
        private CanvasGroup   _canvasGroup;
        private Image         _image;
        private EquipmentItem _equipmentItem;

        private RectTransform _dragRoot;
        private bool          _dropHandled;
        private BagBox        _previewBag;
        private float         _lastKnownBagCellSize = -1f;
        private float         _lastKnownBagCellSpacing;
        private readonly Vector3[] _worldCorners = new Vector3[4];
        private static readonly List<RaycastResult> s_raycastResults = new List<RaycastResult>();
        
        /// <summary>当前正在移动的道具（全局唯一）</summary>
        public static BagItemView CurrentDraggingItem { get; private set; }

        private static readonly Dictionary<BagItemData, BagItemView> s_itemViewsByData = new Dictionary<BagItemData, BagItemView>();

        /// <summary>当前道具的数据</summary>
        public BagItemData Data { get; private set; }

        /// <summary>当前所在的背包容器（在背包中时不为 null）</summary>
        public BagBox CurrentBag { get; private set; }

        /// <summary>当前所在的装备槽位（装备栏中时不为 null）</summary>
        public EquipmentSlotView CurrentSlot { get; private set; }

        /// <summary>当前所在的宝石孔（插槽中时不为 null）</summary>
        public SocketItem CurrentSocket { get; private set; }

        private void Awake()
        {
            _rt            = GetComponent<RectTransform>();
            _canvasGroup   = GetComponent<CanvasGroup>();
            _image         = GetComponent<Image>();
            _equipmentItem = GetComponent<EquipmentItem>();
        }

        /// <summary>
        /// 初始化道具视图数据。
        /// </summary>
        public void Setup(BagItemData data)
        {
            Data = data;
            if (data == null)
                return;

            s_itemViewsByData[data] = this;
            gameObject.name = $"BagItem_{data.Name}";

            if (_equipmentItem == null && _image != null)
            {
                if (data.Icon != null)
                    _image.sprite = data.Icon;
                _image.color = data.ItemColor;
            }
        }

        public static BagItemView FindByData(BagItemData data)
        {
            if (data == null)
                return null;

            if (s_itemViewsByData.TryGetValue(data, out var itemView) && itemView != null)
                return itemView;

            return null;
        }

        /// <summary>
        /// 标记本次拖拽已经被目标容器成功处理。
        /// </summary>
        public void MarkDropHandled()
        {
            _dropHandled = true;
        }

        /// <summary>
        /// 绑定到背包格子网格。
        /// </summary>
        public void BindToBag(BagBox bag)
        {
            if (bag == null || Data == null)
                return;

            CurrentBag    = bag;
            CurrentSlot   = null;
            CurrentSocket = null;
            CacheBagLayout(bag);

            transform.SetParent(bag.GridRoot, false);
            ResetLocalScale();

            if (_equipmentItem != null)
            {
                _equipmentItem.SetupInBag(bag.CellSize, bag.CellSpacing, Data.GridCol, Data.GridRow);
            }
            else
            {
                float w = Data.GridWidth  * bag.CellSize + (Data.GridWidth  - 1) * bag.CellSpacing - BagItemPadding * 2f;
                float h = Data.GridHeight * bag.CellSize + (Data.GridHeight - 1) * bag.CellSpacing - BagItemPadding * 2f;

                _rt.anchorMin        = new Vector2(0f, 1f);
                _rt.anchorMax        = new Vector2(0f, 1f);
                _rt.pivot            = new Vector2(0f, 1f);
                _rt.sizeDelta        = new Vector2(w, h);
                _rt.anchoredPosition = new Vector2(
                     Data.GridCol * (bag.CellSize + bag.CellSpacing) + BagItemPadding,
                    -Data.GridRow * (bag.CellSize + bag.CellSpacing) - BagItemPadding
                );
            }

            transform.SetAsLastSibling();
        }

        /// <summary>
        /// 绑定到装备栏槽位。
        /// </summary>
        public void BindToEquipmentSlot(EquipmentSlotView slot)
        {
            if (slot == null)
                return;

            CurrentBag    = null;
            CurrentSlot   = slot;
            CurrentSocket = null;

            transform.SetParent(slot.ContentRoot, false);
            ResetLocalScale();
            ApplyStretchLayout(slot.ItemPadding);
            transform.SetAsLastSibling();
        }

        /// <summary>
        /// 绑定到装备上的宝石孔。
        /// </summary>
        public void BindToSocket(SocketItem socket)
        {
            if (socket == null)
                return;

            CurrentBag    = null;
            CurrentSlot   = null;
            CurrentSocket = socket;

            transform.SetParent(socket.ContentRoot, false);
            ResetLocalScale();
            ApplyStretchLayout(socket.ItemPadding);
            transform.SetAsLastSibling();
        }

        /// <summary>
        /// 尝试将当前移动中的道具放入目标背包坐标。
        /// </summary>
        public bool TryDropToBag(BagBox bag, int col, int row)
        {
            return TryDropToBag(bag, col, row, null);
        }

        public bool TryDropToBag(BagBox bag, int col, int row, PointerEventData eventData)
        {
            if (bag == null || Data == null)
                return false;

            // 同一个背包内移动：允许覆盖自身原位置，直接走背包的移动逻辑。
            if (CurrentBag == bag)
            {
                if (!bag.TryPlaceItem(Data, col, row))
                    return false;

                BindToBag(bag);
                MarkDropHandled();
                CompleteMove();
                return true;
            }

            if (!bag.CanPlaceItem(Data, col, row))
                return false;

            RemoveFromCurrentContainer();

            if (!bag.TryPlaceItem(Data, col, row))
                return false;

            BindToBag(bag);
            MarkDropHandled();
            CompleteMove();
            return true;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            var draggingItem = CurrentDraggingItem;
            if (draggingItem != null && draggingItem != this)
            {
                if (CurrentBag != null)
                {
                    int targetCol = Data.GridCol;
                    int targetRow = Data.GridRow;
                    var eventCamera = ResolveEventCamera(eventData);
                    if (CurrentBag.TryGetCellCoordFromScreenPoint(eventData.position, eventCamera, out int hoveredCol, out int hoveredRow))
                    {
                        targetCol = hoveredCol;
                        targetRow = hoveredRow;
                    }

                    draggingItem.TryDropToBag(CurrentBag, targetCol, targetRow, eventData);
                }
                else if (CurrentSlot != null)
                {
                    CurrentSlot.TryAccept(draggingItem, eventData);
                }
                else if (CurrentSocket != null)
                {
                    CurrentSocket.TryAccept(draggingItem, eventData);
                }

                eventData?.Use();
                return;
            }

            TryBeginMove(eventData);
        }

        public void TryBeginMove(PointerEventData eventData)
        {
            if (Data == null)
                return;

            if (CurrentDraggingItem != null)
            {
                if (CurrentDraggingItem != this)
                    eventData?.Use();
                return;
            }

            _dropHandled = false;
            CurrentDraggingItem = this;

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha          = 0.85f;
                _canvasGroup.blocksRaycasts = false;
            }

            CacheDragRoot();
            ReparentForDragging();

            ClearBagPreview();
            CurrentBag?.ClearAllHighlights();

            UpdateMoveVisual(eventData?.position ?? Vector2.zero, ResolveEventCamera(eventData));
            transform.SetAsLastSibling();
            eventData?.Use();
        }

        public void CompleteMove()
        {
            if (CurrentDraggingItem == this)
                CurrentDraggingItem = null;

            ClearBagPreview();
            CleanupDragVisual();
        }

        private void Update()
        {
            if (CurrentDraggingItem != this)
                return;

            UpdateMoveVisual();
        }

        private void CacheDragRoot()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                _dragRoot = null;
                return;
            }

            _dragRoot = canvas.rootCanvas != null
                ? canvas.rootCanvas.transform as RectTransform
                : canvas.transform as RectTransform;
        }

        private void ReparentForDragging()
        {
            if (_dragRoot == null)
                return;

            Vector2 size     = ResolveDraggingVisualSize();
            Vector3 worldPos = _rt.position;

            transform.SetParent(_dragRoot, true);

            _rt.anchorMin = new Vector2(0.5f, 0.5f);
            _rt.anchorMax = new Vector2(0.5f, 0.5f);
            _rt.pivot     = new Vector2(0.5f, 0.5f);
            _rt.sizeDelta = size;
            _rt.position  = worldPos;
        }

        private void CacheBagLayout(BagBox bag)
        {
            if (bag == null)
                return;

            _lastKnownBagCellSize    = bag.CellSize;
            _lastKnownBagCellSpacing = bag.CellSpacing;
        }

        private Vector2 ResolveDraggingVisualSize()
        {
            if (TryGetBagVisualSize(out var bagSize))
                return bagSize;

            return _rt != null ? _rt.rect.size : Vector2.zero;
        }

        private bool TryGetBagVisualSize(out Vector2 size)
        {
            size = Vector2.zero;
            if (Data == null)
                return false;

            float cellSize;
            float cellSpacing;

            if (CurrentBag != null)
            {
                cellSize = CurrentBag.CellSize;
                cellSpacing = CurrentBag.CellSpacing;
            }
            else if (_lastKnownBagCellSize > 0f)
            {
                cellSize = _lastKnownBagCellSize;
                cellSpacing = _lastKnownBagCellSpacing;
            }
            else
            {
                return false;
            }

            float w = Data.GridWidth  * cellSize + (Data.GridWidth  - 1) * cellSpacing - BagItemPadding * 2f;
            float h = Data.GridHeight * cellSize + (Data.GridHeight - 1) * cellSpacing - BagItemPadding * 2f;
            size = new Vector2(Mathf.Max(0f, w), Mathf.Max(0f, h));
            return true;
        }

        private void UpdateMoveVisual()
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return;

            UpdateMoveVisual(mouse.position.ReadValue(), GetPointerEventCamera());
        }

        private void UpdateMoveVisual(Vector2 screenPosition, Camera eventCamera)
        {
            if (TrySnapToBagPreview(screenPosition))
                return;

            UpdateDragPosition(screenPosition, eventCamera);
        }

        private Camera ResolveEventCamera(PointerEventData eventData)
        {
            return eventData?.pressEventCamera ?? eventData?.enterEventCamera ?? GetPointerEventCamera();
        }

        private Camera GetPointerEventCamera()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
                return null;

            var rootCanvas = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
            if (rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return null;

            return rootCanvas.worldCamera != null ? rootCanvas.worldCamera : Camera.main;
        }

        private void UpdateDragPosition(Vector2 screenPosition, Camera eventCamera)
        {
            if (_dragRoot == null)
            {
                _rt.position = screenPosition;
                return;
            }

            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    _dragRoot,
                    screenPosition,
                    eventCamera,
                    out var worldPos))
            {
                _rt.position = worldPos;
            }
        }

        private bool TrySnapToBagPreview(Vector2 screenPosition)
        {
            if (Data == null)
            {
                ClearBagPreview();
                return false;
            }

            if (!TryGetHoveredBag(screenPosition, out var bag, out int col, out int row))
            {
                ClearBagPreview();
                return false;
            }

            if (_previewBag != null && _previewBag != bag)
                _previewBag.ClearAllHighlights();

            _previewBag = bag;
            _previewBag.ShowPlacementPreview(Data, col, row);
            SnapToBagPreview(bag, col, row);
            return true;
        }

        private bool TryGetHoveredBag(Vector2 screenPosition, out BagBox bag, out int col, out int row)
        {
            bag = null;
            col = -1;
            row = -1;

            var eventSystem = EventSystem.current;
            if (eventSystem == null)
                return false;

            var pointerData = new PointerEventData(eventSystem)
            {
                position = screenPosition
            };

            s_raycastResults.Clear();
            eventSystem.RaycastAll(pointerData, s_raycastResults);

            for (int i = 0; i < s_raycastResults.Count; i++)
            {
                var result = s_raycastResults[i];
                if (result.gameObject == null)
                    continue;

                var candidateBag = result.gameObject.GetComponentInParent<BagBox>();
                if (candidateBag == null)
                    continue;

                var candidateCamera = result.module != null ? result.module.eventCamera : null;
                if (!candidateBag.TryGetCellCoordFromScreenPoint(screenPosition, candidateCamera, out col, out row))
                    continue;

                bag = candidateBag;
                return true;
            }

            return false;
        }

        private void SnapToBagPreview(BagBox bag, int col, int row)
        {
            if (_rt == null || bag == null)
                return;

            Vector3 topLeftWorld = GetBagVisualTopLeftWorld(bag, col, row);

            _rt.GetWorldCorners(_worldCorners);
            Vector3 widthVector  = _worldCorners[2] - _worldCorners[1];
            Vector3 heightVector = _worldCorners[0] - _worldCorners[1];

            _rt.position = topLeftWorld + widthVector * 0.5f + heightVector * 0.5f;
        }

        private Vector3 GetBagVisualTopLeftWorld(BagBox bag, int col, int row)
        {
            float step = bag.CellSize + bag.CellSpacing;
            float xFromLeft = col * step + BagItemPadding;
            float yFromTop  = row * step + BagItemPadding;

            return GetWorldPointFromTopLeft(bag.GridRoot, xFromLeft, yFromTop);
        }

        private Vector3 GetWorldPointFromTopLeft(RectTransform root, float xFromLeft, float yFromTop)
        {
            if (root == null)
                return Vector3.zero;

            var rect = root.rect;
            float localX = xFromLeft - rect.width * root.pivot.x;
            float localY = rect.height * (1f - root.pivot.y) - yFromTop;
            return root.TransformPoint(new Vector3(localX, localY, 0f));
        }

        private void ClearBagPreview()
        {
            if (_previewBag == null)
                return;

            _previewBag.ClearAllHighlights();
            _previewBag = null;
        }

        private void ResetLocalScale()
        {
            if (_rt == null)
                return;

            _rt.localScale = Vector3.one;
        }

        private void ApplyStretchLayout(float padding)
        {
            _rt.anchorMin = Vector2.zero;
            _rt.anchorMax = Vector2.one;
            _rt.pivot     = new Vector2(0.5f, 0.5f);
            _rt.offsetMin = new Vector2(padding, padding);
            _rt.offsetMax = new Vector2(-padding, -padding);
            _rt.anchoredPosition = Vector2.zero;
        }

        private void RemoveFromCurrentContainer()
        {
            if (CurrentBag != null)
            {
                CurrentBag.RemoveItem(Data);
            }
            else if (CurrentSlot != null)
            {
                CurrentSlot.ClearPlacedItem(this);
            }
            else if (CurrentSocket != null)
            {
                CurrentSocket.ClearPlacedGem(this);
            }
        }

        private void ReturnToCurrentContainer()
        {
            if (CurrentBag != null)
            {
                BindToBag(CurrentBag);
                CurrentBag.ClearAllHighlights();
            }
            else if (CurrentSlot != null)
            {
                BindToEquipmentSlot(CurrentSlot);
            }
            else if (CurrentSocket != null)
            {
                BindToSocket(CurrentSocket);
            }
        }

        private void CleanupDragVisual()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha          = 1f;
                _canvasGroup.blocksRaycasts = true;
            }
        }

        private void OnDestroy()
        {
            if (Data != null && s_itemViewsByData.TryGetValue(Data, out var itemView) && itemView == this)
                s_itemViewsByData.Remove(Data);
        }
    }
}