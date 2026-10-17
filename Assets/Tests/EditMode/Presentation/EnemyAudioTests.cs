using System.Collections.Generic;
using System.Linq;
using Hollow.Data;
using Hollow.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Hollow.Tests.Presentation
{
    /// <summary>T31: every enemy type, spawned type and boss has its own audio profile (GDD v0.19 §20).</summary>
    public class EnemyAudioTests
    {
        static AudioCatalog Catalog() => Resources.Load<AudioCatalog>(AudioCatalog.ResourcePath);

        [Test]
        public void TwentySevenProfilesWithEveryEventGroup()
        {
            var c = Catalog();
            Assert.IsNotNull(c);
            var keys = System.Enum.GetNames(typeof(EnemyId)).Concat(System.Enum.GetNames(typeof(BossId)).Select(n => "Boss_" + n)).ToList();
            Assert.AreEqual(27, keys.Count);
            foreach (var key in keys)
            {
                var p = c.Enemy(key);
                Assert.IsNotNull(p, key);
                foreach (var ev in new[] { EnemySound.Presence, EnemySound.Warning, EnemySound.Attack, EnemySound.Death })
                    Assert.IsNotEmpty(p.Get(ev).Clips, $"{key} {ev}");
                if (key.StartsWith("Boss_")) Assert.IsNotEmpty(p.AreaWarning.Clips, key + " area warning differs from its other warning");
            }
        }

        [Test]
        public void EachTypeSoundsDistinct()
        {
            var c = Catalog();
            foreach (var ev in new[] { EnemySound.Presence, EnemySound.Warning, EnemySound.Attack, EnemySound.Death })
            {
                var seen = new HashSet<AudioClip>();
                foreach (var p in c.Enemies)
                    foreach (var clip in p.Get(ev).Clips)
                        Assert.IsTrue(seen.Add(clip), $"{p.Key} {ev} shares a clip with another type");
            }
            Assert.Greater(c.Enemies.Select(p => p.Hit.Pitch).Distinct().Count(), 10, "hits are pitched per type");
        }

        [Test]
        public void MixDefaultsFollowTheGdd()
        {
            var c = Catalog();
            Assert.AreEqual(24, c.MaxOneShots);
            Assert.AreEqual(4, c.MaxWarnings);
            Assert.AreEqual(0.15f, c.PresenceGap, 1e-4f);
        }
    }
}
