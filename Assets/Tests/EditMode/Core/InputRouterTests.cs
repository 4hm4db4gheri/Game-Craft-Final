using Hollow.Core.Input;
using NUnit.Framework;
using UnityEngine;

namespace Hollow.Tests.Core
{
    public class InputRouterTests
    {
        static InputRouter WorldRouter()
        {
            var r = new InputRouter();
            r.SetWorldAcceptsInput(true);
            return r;
        }

        [Test]
        public void ClickStartingOverUIDoesNotFireWhileHeld()
        {
            var r = WorldRouter();
            r.Sample(new RawInputSample { FireDown = true, FireHeld = true, PointerOverUI = true });
            var c = r.Consume();
            Assert.IsFalse(c.FirePressed);
            Assert.IsFalse(c.FireHeld);

            // Dragging the held button off the panel into the world still must not shoot.
            r.Sample(new RawInputSample { FireHeld = true, PointerOverUI = false });
            Assert.IsFalse(r.Consume().FireHeld);

            // After release, a new click in the world fires.
            r.Sample(new RawInputSample { FireHeld = false });
            r.Sample(new RawInputSample { FireDown = true, FireHeld = true });
            var fired = r.Consume();
            Assert.IsTrue(fired.FirePressed);
            Assert.IsTrue(fired.FireHeld);
        }

        [Test]
        public void ClickThatClosesAModalDoesNotLeakIntoTheWorld()
        {
            var r = new InputRouter();
            r.SetWorldAcceptsInput(false);                         // XP card open
            r.Sample(new RawInputSample { FireDown = true, FireHeld = true, PointerOverUI = true });
            r.SetWorldAcceptsInput(true);                          // card closed by that click
            r.Sample(new RawInputSample { FireHeld = true });
            Assert.IsFalse(r.Consume().FireHeld);
        }

        [Test]
        public void HeldButtonWhenWorldResumesIsLatched()
        {
            var r = new InputRouter();
            r.SetWorldAcceptsInput(false);
            r.Sample(new RawInputSample { FireHeld = true });     // held during a load screen
            r.SetWorldAcceptsInput(true);
            r.Sample(new RawInputSample { FireHeld = true });
            Assert.IsFalse(r.Consume().FireHeld);
        }

        [Test]
        public void NoWorldInputWhileNotAccepting()
        {
            var r = new InputRouter();
            r.SetWorldAcceptsInput(false);
            r.Sample(new RawInputSample { Move = Vector2.up, DashDown = true, ReloadDown = true });
            var c = r.Consume();
            Assert.AreEqual(Vector2.zero, c.Move);
            Assert.IsFalse(c.DashPressed);
            Assert.IsFalse(c.ReloadPressed);
        }

        [Test]
        public void PressesBetweenTicksAreKeptUntilConsumedOnce()
        {
            var r = WorldRouter();
            r.Sample(new RawInputSample { DashDown = true });
            r.Sample(new RawInputSample());                        // next frame, no tick in between
            Assert.IsTrue(r.Consume().DashPressed);
            Assert.IsFalse(r.Consume().DashPressed);
        }

        [Test]
        public void MoveIsClampedSoDiagonalIsNotFaster()
        {
            var r = WorldRouter();
            r.Sample(new RawInputSample { Move = new Vector2(1, 1) });
            Assert.AreEqual(1f, r.Consume().Move.magnitude, 1e-5f);
        }
    }
}
