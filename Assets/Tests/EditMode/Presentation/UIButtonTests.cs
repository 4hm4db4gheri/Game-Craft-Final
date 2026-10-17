using System;
using System.Text.RegularExpressions;
using Hollow.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hollow.Tests.Presentation
{
    /// <summary>
    /// Regression: with Domain Reload off, the click-sound listener could point at a destroyed audio director
    /// from an earlier Play session and throw before the button's action ran, so buttons did nothing.
    /// </summary>
    public class UIButtonTests
    {
        [Test]
        public void AFailingClickListenerNeverSwallowsTheButtonAction()
        {
            var theme = ScriptableObject.CreateInstance<UITheme>();
            var root = new GameObject("Root", typeof(RectTransform));
            bool clicked = false;
            Action broken = () => throw new MissingReferenceException("stale audio director");
            UIFactory.ButtonClicked += broken;
            try
            {
                var b = UIFactory.Button(root.transform, "B", "x", theme, Vector2.zero, Vector2.zero, Vector2.one * 10, () => clicked = true, out _);
                LogAssert.Expect(LogType.Exception, new Regex("stale audio director"));
                b.onClick.Invoke();
                Assert.IsTrue(clicked);
            }
            finally
            {
                UIFactory.ButtonClicked -= broken;
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(theme);
            }
        }
    }
}
