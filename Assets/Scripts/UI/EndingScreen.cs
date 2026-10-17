using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hollow.UI
{
    /// <summary>
    /// Fixed ending (§3, §14): the world darkens, rows of city lights go out one by one, "You won." appears
    /// with an excessively cheerful jingle, then silence, statistics and the way out (main menu / new game).
    /// </summary>
    public sealed class EndingScreen : MonoBehaviour
    {
        UIRoot root;
        RectTransform layer;
        Image black;
        TextMeshProUGUI won, stats;
        readonly List<Image> lights = new();
        RectTransform buttons;

        public void Init(UIRoot uiRoot)
        {
            root = uiRoot;
            var t = root.Theme;
            layer = UIFactory.Stretch(root.Modal, "Ending");
            black = UIFactory.Panel(layer, t, new Color(0, 0, 0, 0), raycast: true);
            var city = UIFactory.Rect(layer, "City", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(1600, 260));
            var rng = new System.Random(3);
            for (int i = 0; i < 90; i++)
            {
                var r = UIFactory.Rect(city, "Light", new Vector2(0, 0), new Vector2(0.5f, 0.5f),
                    new Vector2(20 + rng.Next(1560), 20 + rng.Next(220)), new Vector2(8 + rng.Next(10), 8 + rng.Next(14)));
                var img = r.gameObject.AddComponent<Image>();
                img.color = new Color(1f, 0.85f, 0.45f, 0f);
                img.raycastTarget = false;
                lights.Add(img);
            }
            var wonRt = UIFactory.Rect(layer, "Won", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 220), new Vector2(1400, 160));
            won = UIFactory.Text(wonRt, "T", "", t, 110, TextAlignmentOptions.Center);
            var statsRt = UIFactory.Rect(layer, "Stats", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -80), new Vector2(1100, 360));
            stats = UIFactory.Text(statsRt, "T", "", t, 28, TextAlignmentOptions.Top, t.TextDim);
            buttons = UIFactory.Rect(layer, "Buttons", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 120), new Vector2(800, 80));
            layer.gameObject.SetActive(false);
        }

        /// <param name="playMontage">False when a completed slot is continued: go straight to the results.</param>
        public void Show(bool playMontage, string statistics, Action jingle, Action mainMenu, Action newGame)
        {
            layer.gameObject.SetActive(true);
            foreach (Transform c in buttons) Destroy(c.gameObject);
            StopAllCoroutines();
            StartCoroutine(Run(playMontage, statistics, jingle, mainMenu, newGame));
        }

        IEnumerator Run(bool montage, string statistics, Action jingle, Action mainMenu, Action newGame)
        {
            won.text = "";
            stats.text = "";
            if (montage)
            {
                foreach (var l in lights) l.color = new Color(1f, 0.85f, 0.45f, 0.9f);
                for (float a = 0; a < 1f; a += Time.unscaledDeltaTime / 2f) { black.color = new Color(0, 0, 0, a); yield return null; }
                black.color = Color.black;
                yield return new WaitForSecondsRealtime(0.8f);
                var order = new List<Image>(lights);
                var rng = new System.Random(5);
                while (order.Count > 0)
                {
                    int i = rng.Next(order.Count);
                    order[i].color = new Color(0.15f, 0.13f, 0.1f, 0.6f);
                    order.RemoveAt(i);
                    yield return new WaitForSecondsRealtime(0.045f);
                }
                yield return new WaitForSecondsRealtime(1.2f);
            }
            else black.color = Color.black;

            won.text = $"<b>{Loc.T("ending.won")}</b>";
            jingle?.Invoke();
            yield return new WaitForSecondsRealtime(montage ? 2.5f : 0.3f);
            stats.text = statistics;
            var t = root.Theme;
            UIFactory.Button(buttons, "Menu", Loc.T("common.mainMenu"), t, new Vector2(0.5f, 0.5f), new Vector2(170, 0), new Vector2(300, 64), mainMenu, out _);
            UIFactory.Button(buttons, "New", Loc.T("menu.newGame"), t, new Vector2(0.5f, 0.5f), new Vector2(-170, 0), new Vector2(300, 64), newGame, out _);
        }
    }
}
