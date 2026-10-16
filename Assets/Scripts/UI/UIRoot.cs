using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Hollow.UI
{
    /// <summary>Screen-space canvas (1920×1080 reference, scales to 1600×900 and 1280×720) with HUD and modal layers.</summary>
    public sealed class UIRoot : MonoBehaviour
    {
        [SerializeField] UITheme theme;

        public UITheme Theme => theme;
        public RectTransform Hud { get; private set; }
        public RectTransform Modal { get; private set; }

        void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }

            Hud = UIFactory.Stretch(transform, "HUD");
            Modal = UIFactory.Stretch(transform, "Modal");
        }
    }
}
