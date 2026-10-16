using System.Collections.Generic;
using System.Linq;
using Hollow.Data;
using Hollow.Presentation;
using Hollow.Tests.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hollow.Tests.Presentation
{
    /// <summary>T20 readability rules that can be checked without a human (docs/T20_CHECKLIST.md).</summary>
    public class ReadabilityTests
    {
        const string CatalogPath = "Assets/Art/Placeholders/VisualCatalog.asset";

        static VisualCatalog Catalog() => AssetDatabase.LoadAssetAtPath<VisualCatalog>(CatalogPath);

        [Test]
        public void EachPhaseStartsWithASmallerCrowdThanThePreviousPeak()
        {
            var waves = BalanceCalculator.Waves(TestData.Baseline());
            WaveTotals W(int phase, int wave) => waves.Single(x => x.Phase == phase && x.Wave == wave);

            // §6: robot entry 18 with cap 8 after the animal peak of 96 with cap 24; human entry 24 with cap 10 after 130 with cap 32.
            Assert.AreEqual((18, 8), (W(2, 1).MaxTotal, W(2, 1).ActiveCap));
            Assert.AreEqual((24, 10), (W(3, 1).MaxTotal, W(3, 1).ActiveCap));
            Assert.AreEqual((96, 24), (W(1, 5).MaxTotal, W(1, 5).ActiveCap));
            Assert.AreEqual((130, 32), (W(2, 5).MaxTotal, W(2, 5).ActiveCap));
        }

        [Test]
        public void EveryTelegraphIsDrawnAboveAllEffects()
        {
            var catalog = Catalog();
            Assert.IsNotNull(catalog);
            int checkedParts = 0;
            var views = catalog.Enemies.Select(e => e.Prefab.gameObject).Concat(catalog.Bosses.Select(b => b.Prefab.gameObject));
            foreach (var go in views)
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                {
                    if (!(t.name.StartsWith("Telegraph") || t.name == "Lane" || t.name == "Countdown" || (t.name.StartsWith("PH_Arc") && t.parent != null))) continue;
                    var g = t.GetComponent<SortingGroup>();
                    Assert.IsNotNull(g, $"{go.name}/{t.name} has no sorting group");
                    Assert.IsTrue(g.sortAtRoot, $"{go.name}/{t.name} is sorted inside the character");
                    Assert.AreEqual("Telegraphs", g.sortingLayerName, $"{go.name}/{t.name}");
                    checkedParts++;
                }
            Assert.Greater(checkedParts, 40, "every enemy and boss has its warning parts checked");
            Assert.Greater(SortingLayer.GetLayerValueFromName("Telegraphs"), SortingLayer.GetLayerValueFromName("Effects"));

            foreach (var r in catalog.EnemyProjectile.GetComponentsInChildren<Renderer>(true))
                Assert.AreEqual("Telegraphs", r.sortingLayerName, "enemy shots are attacks and stay on top");
        }

        [Test]
        public void NoTwoEnemiesOfAPhaseShareAShape()
        {
            // A mechanical proxy for "distinct silhouettes": shape sprite, size and accent differ within a phase.
            // Whether they read as distinct at a glance is a human check.
            var catalog = Catalog();
            var byPhase = new Dictionary<char, List<(EnemyId Id, string Key)>>();
            foreach (var e in catalog.Enemies)
            {
                var renderers = e.Prefab.GetComponentsInChildren<SpriteRenderer>(true)
                    .Where(r => r.transform.parent != null && r.transform.parent.name == "Body").ToList();
                string key = string.Join("|", renderers.Select(r =>
                    $"{(r.sprite != null ? r.sprite.name : "-")}:{r.transform.localScale.x:F2}x{r.transform.localScale.y:F2}:{ColorUtility.ToHtmlStringRGB(r.color)}"));
                char phase = e.Id.ToString()[0];
                if (!byPhase.TryGetValue(phase, out var list)) byPhase[phase] = list = new();
                list.Add((e.Id, key));
            }
            foreach (var (phase, list) in byPhase)
                foreach (var g in list.GroupBy(x => x.Key).Where(g => g.Count() > 1))
                    Assert.Fail($"Phase {phase}: {string.Join(", ", g.Select(x => x.Id))} look the same");
        }
    }
}
