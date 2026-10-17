using TMPro;
using UnityEngine;

namespace Hollow.Presentation
{
    /// <summary>World-space numbers (damage, countdowns) in Persian digits with the Persian UI font (D64).</summary>
    public static class PersianText
    {
        static TMP_FontAsset font;
        static bool loaded;
        static readonly string[] Cache = new string[1000];

        /// <summary>Enter Play Mode without Domain Reload keeps statics between sessions: start every session clean.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { font = null; loaded = false; }

        public static TMP_FontAsset Font
        {
            get
            {
                if (!loaded) { font = Resources.Load<TMP_FontAsset>("Fonts/Vazirmatn SDF"); loaded = true; }
                return font;
            }
        }

        /// <summary>Persian digits; small numbers are cached so combat creates no string garbage.</summary>
        public static string Number(int n)
        {
            if (n >= 0 && n < Cache.Length) return Cache[n] ??= Convert(n);
            return Convert(n);
        }

        static string Convert(int n)
        {
            var c = n.ToString(System.Globalization.CultureInfo.InvariantCulture).ToCharArray();
            for (int i = 0; i < c.Length; i++) if (c[i] >= '0' && c[i] <= '9') c[i] = (char)('۰' + (c[i] - '0'));
            return new string(c);
        }
    }
}
