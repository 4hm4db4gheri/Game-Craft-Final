using System;
using Hollow.Sim;
using TMPro;
using UnityEngine;

namespace Hollow.UI
{
    /// <summary>Failure screen (§11): names the cause and offers retry, load and main menu.</summary>
    public sealed class FailureScreen : MonoBehaviour
    {
        RectTransform panel;
        TextMeshProUGUI text;
        Action retry, load, menu;

        public void Init(UIRoot root, Action onRetry, Action onLoad, Action onMenu)
        {
            retry = onRetry; load = onLoad; menu = onMenu;
            var t = root.Theme;
            panel = UIFactory.Stretch(root.Modal, "Failure");
            UIFactory.Panel(panel, t, new Color(0.05f, 0, 0, 0.75f), raycast: true);
            text = UIFactory.Text(panel, "Reason", "", t, 44, TextAlignmentOptions.Center);
            text.rectTransform.offsetMin = new Vector2(0, 200);
            var center = new Vector2(0.5f, 0.5f);
            UIFactory.Button(panel, "Retry", Loc.T("failure.retry"), t, center, new Vector2(0, -60), new Vector2(360, 70), () => retry?.Invoke(), out _);
            UIFactory.Button(panel, "Load", Loc.T("slots.load"), t, center, new Vector2(0, -150), new Vector2(360, 70), () => load?.Invoke(), out _);
            UIFactory.Button(panel, "Menu", Loc.T("common.mainMenu"), t, center, new Vector2(0, -240), new Vector2(360, 70), () => menu?.Invoke(), out _);
            panel.gameObject.SetActive(false);
        }

        public void Show(FailureReason reason)
        {
            text.text = reason switch
            {
                FailureReason.CompanionDied => $"<b>{Loc.T("failure.companionTitle")}</b>\n<size=28>{Loc.T("failure.companionBody")}</size>",
                _ => $"<b>{Loc.T("failure.playerTitle")}</b>\n<size=28>{Loc.T("failure.playerBody")}</size>",
            };
            panel.gameObject.SetActive(true);
        }

        public void Hide() => panel.gameObject.SetActive(false);
    }
}
