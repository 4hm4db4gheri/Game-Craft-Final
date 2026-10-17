using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Hollow.Data;
using Hollow.Sim;
using Hollow.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Hollow.Tests.Data
{
    /// <summary>T24: every player-facing string exists in Persian, uses Persian letters and the font can draw it.</summary>
    public class LocalizationTests
    {
        const string StringsPath = "Assets/Resources/Localization/fa.json";

        static IEnumerable<string> CodeKeys()
        {
            var literal = new Regex(@"""([a-z][A-Za-z]*(?:\.[A-Za-z0-9]+)+)""");
            foreach (var file in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories))
                foreach (var line in File.ReadLines(file))
                {
                    if (!line.Contains("Loc.")) continue;
                    foreach (Match m in literal.Matches(line)) yield return m.Groups[1].Value;
                }
        }

        [Test]
        public void EveryKeyTheCodeUsesHasAPersianText()
        {
            var missing = CodeKeys().Distinct().Where(k => !Loc.Has(k)).ToList();
            Assert.IsEmpty(missing, string.Join(", ", missing));
        }

        [Test]
        public void EveryNameAndLockReasonHasAPersianText()
        {
            var keys = new List<string>();
            keys.AddRange(Enum.GetNames(typeof(WeaponId)).Select(n => "weapon." + n));
            keys.AddRange(Enum.GetNames(typeof(ItemId)).Select(n => "item." + n));
            keys.AddRange(Enum.GetNames(typeof(ItemId)).Select(n => "itemShort." + n));
            keys.AddRange(Enum.GetNames(typeof(SkillId)).Select(n => "skill." + n));
            keys.AddRange(Enum.GetNames(typeof(BossId)).Select(n => "boss." + n));
            keys.AddRange(Enum.GetNames(typeof(LockReason)).Where(n => n != nameof(LockReason.None)).Select(n => "lock." + n));
            foreach (var row in new[] { "move", "aim", "dash", "reload", "weapons", "equipItem", "cycleItem", "cancelItem", "shop", "startWave", "pause" })
            { keys.Add("controls." + row); keys.Add("controls.key." + row); }
            foreach (var s in new[] { "damage", "rate", "pellets", "magazine", "reload", "range", "spread", "targets", "reach", "arc", "knockback" })
                keys.Add("stat." + s);
            var missing = keys.Where(k => !Loc.Has(k)).ToList();
            Assert.IsEmpty(missing, string.Join(", ", missing));
        }

        [Test]
        public void NumbersUsePersianSeparators()
        {
            Assert.AreEqual("۱٫۵ و ۱۰۸٪ و نسخه.", Loc.PersianSeparators("۱٫۵ و ۱۰۸٪ و نسخه.".Replace('٫', '.').Replace('٪', '%')));
            Assert.AreEqual("1٫5 3٫25 108٪ end.", Loc.PersianSeparators("1.5 3.25 108% end."));
        }

        [Test]
        public void TextsUsePersianLettersNotArabicOnes()
        {
            string text = File.ReadAllText(StringsPath);
            Assert.IsFalse(text.Contains('ي'), "Arabic yeh (U+064A): use ی (U+06CC)");
            Assert.IsFalse(text.Contains('ك'), "Arabic kaf (U+0643): use ک (U+06A9)");
        }

        [Test]
        public void ThePersianFontDrawsEveryCharacter()
        {
            var font = Resources.Load<TMP_FontAsset>("Fonts/Vazirmatn SDF");
            Assert.IsNotNull(font, "Hollow > UI > Build Persian Font");
            string text = new string(File.ReadAllText(StringsPath).Where(c => c >= 0x20).Distinct().ToArray());
            Assert.IsTrue(font.HasCharacters(text, out uint[] missing, true, true),
                "missing: " + string.Join(" ", (missing ?? new uint[0]).Select(c => $"U+{c:X4}")));
        }
    }
}
