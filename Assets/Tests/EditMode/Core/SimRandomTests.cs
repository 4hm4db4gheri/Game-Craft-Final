using Hollow.Core;
using NUnit.Framework;

namespace Hollow.Tests.Core
{
    public class SimRandomTests
    {
        [Test]
        public void SameSeedSameSequence()
        {
            var a = new SimRandom(42);
            var b = new SimRandom(42);
            for (int i = 0; i < 1000; i++) Assert.AreEqual(a.NextULong(), b.NextULong());
        }

        [Test]
        public void RestoringStateContinuesTheSequence()
        {
            var a = new SimRandom(7);
            for (int i = 0; i < 37; i++) a.NextULong();
            var saved = new SimRandom { S0 = a.S0, S1 = a.S1 };
            for (int i = 0; i < 100; i++) Assert.AreEqual(a.Range(0, 1000), saved.Range(0, 1000));
        }

        [Test]
        public void RangesStayInBounds()
        {
            var r = new SimRandom(3);
            for (int i = 0; i < 10000; i++)
            {
                float v = r.Value;
                Assert.That(v, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
                int n = r.Range(-3, 4);
                Assert.That(n, Is.InRange(-3, 3));
            }
        }
    }
}
