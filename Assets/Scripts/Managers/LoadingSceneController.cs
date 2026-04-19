using UnityEngine;
using UnityEngine.UI;

namespace POELike.Managers
{
    /// <summary>
    /// LoadingScene 运行时控制器。
    /// 负责确保场景中存在加载 UI，并根据外部进度更新 Slider。
    /// </summary>
    public class LoadingSceneController : MonoBehaviour
    {
        private const int LoadingCanvasSortingOrder = 5000;
        private const string LoadingPanelName = "LoadingPanel";

        private Slider _progressSlider;

        private void Awake()
        {
            EnsureLoadingUi();
            SetProgress(SceneLoader.CurrentLoadingProgress);
        }

        private void Update()
        {
            SetProgress(SceneLoader.CurrentLoadingProgress);
        }

        public void SetProgress(float normalizedProgress)

        {
            EnsureLoadingUi();

            if (_progressSlider == null)
                return;

            _progressSlider.value = Mathf.Clamp01(normalizedProgress);
        }

        private void EnsureLoadingUi()
        {
            if (_progressSlider != null)
                return;

            if (TryBindExistingUi())
                return;

            CreateFallbackLoadingUi();
        }

        private bool TryBindExistingUi()
        {
            var roots = gameObject.scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var sliders = roots[i].GetComponentsInChildren<Slider>(true);
                for (int j = 0; j < sliders.Length; j++)
                {
                    var slider = sliders[j];
                    if (slider == null || !IsUnderLoadingPanel(slider.transform))
                        continue;

                    _progressSlider = slider;
                    ConfigureSlider(_progressSlider);

                    var canvas = _progressSlider.GetComponentInParent<Canvas>(true);
                    if (canvas != null)
                        ConfigureCanvas(canvas);

                    return true;
                }
            }

            return false;
        }

        private void CreateFallbackLoadingUi()
        {
            var canvasGo = new GameObject("LoadingCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            ConfigureCanvas(canvas);

            var backgroundGo = CreateUiObject("Background", canvasGo.transform, typeof(Image));
            var backgroundRect = backgroundGo.GetComponent<RectTransform>();
            StretchFullScreen(backgroundRect);
            var backgroundImage = backgroundGo.GetComponent<Image>();
            backgroundImage.color = new Color(0.05f, 0.05f, 0.08f, 1f);

            var panelGo = CreateUiObject(LoadingPanelName, canvasGo.transform, typeof(Image));
            var panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(720f, 120f);
            panelRect.anchoredPosition = Vector2.zero;
            var panelImage = panelGo.GetComponent<Image>();
            panelImage.color = new Color(0.10f, 0.10f, 0.15f, 0.92f);

            var sliderGo = CreateUiObject("ProgressSlider", panelGo.transform, typeof(Image), typeof(Slider));
            var sliderRect = sliderGo.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0.5f, 0.5f);
            sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
            sliderRect.pivot = new Vector2(0.5f, 0.5f);
            sliderRect.sizeDelta = new Vector2(600f, 36f);
            sliderRect.anchoredPosition = Vector2.zero;

            var sliderBackgroundImage = sliderGo.GetComponent<Image>();
            sliderBackgroundImage.color = new Color(0.18f, 0.18f, 0.25f, 1f);

            var fillAreaGo = CreateUiObject("Fill Area", sliderGo.transform);
            var fillAreaRect = fillAreaGo.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0f);
            fillAreaRect.anchorMax = new Vector2(1f, 1f);
            fillAreaRect.offsetMin = new Vector2(6f, 6f);
            fillAreaRect.offsetMax = new Vector2(-6f, -6f);

            var fillGo = CreateUiObject("Fill", fillAreaGo.transform, typeof(Image));
            var fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImage = fillGo.GetComponent<Image>();
            fillImage.color = new Color(0.28f, 0.74f, 1f, 1f);

            _progressSlider = sliderGo.GetComponent<Slider>();
            _progressSlider.fillRect = fillRect;
            ConfigureSlider(_progressSlider, fillImage);
            _progressSlider.value = 0f;
        }

        private static void ConfigureCanvas(Canvas canvas)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = LoadingCanvasSortingOrder;

            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            var canvasRect = canvas.GetComponent<RectTransform>();
            if (canvasRect != null)
            {
                canvasRect.localScale = Vector3.one;
                StretchFullScreen(canvasRect);
            }
        }

        private static void ConfigureSlider(Slider slider, Graphic targetGraphic = null)
        {
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.interactable = false;
            slider.direction = Slider.Direction.LeftToRight;

            if (targetGraphic != null)
                slider.targetGraphic = targetGraphic;
        }

        private static bool IsUnderLoadingPanel(Transform current)
        {
            while (current != null)
            {
                if (current.name == LoadingPanelName)
                    return true;

                current = current.parent;
            }

            return false;
        }

        private static GameObject CreateUiObject(string objectName, Transform parent, params System.Type[] components)
        {
            int componentCount = components?.Length ?? 0;
            var allComponents = new System.Type[componentCount + 1];
            allComponents[0] = typeof(RectTransform);

            for (int i = 0; i < componentCount; i++)
                allComponents[i + 1] = components[i];

            var uiObject = new GameObject(objectName, allComponents);
            uiObject.transform.SetParent(parent, false);
            return uiObject;
        }

        private static void StretchFullScreen(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.anchoredPosition = Vector2.zero;
        }
    }
}