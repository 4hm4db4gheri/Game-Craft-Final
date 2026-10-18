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

            var events = EventSystem.current;
            if (events == null)
            {
                var es = new GameObject("EventSystem");
                events = es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            // Menus and the shop are mouse only: WASD must not move the selection and Space must not press the selected button.
            events.sendNavigationEvents = false;

            Hud = UIFactory.Stretch(transform, "HUD");
            Modal = UIFactory.Stretch(transform, "Modal");
        }
    }
}
