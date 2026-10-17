using Hollow.EditorTools;
using NUnit.Framework;

namespace Hollow.Tests
{
    /// <summary>Animation sheets from image tools: one-row strips and grids for tools without very wide images.</summary>
    public sealed class ArtSheetTests
    {
        [TestCase(6, 6144, 1024, 6, 1)]
        [TestCase(6, 1536, 1024, 3, 2)]
        [TestCase(4, 1024, 1024, 2, 2)]
        [TestCase(4, 4096, 1024, 4, 1)]
        [TestCase(8, 2048, 1024, 4, 2)]
        [TestCase(1, 1024, 1024, 1, 1)]
        public void SheetLayoutFollowsTheImageShape(int frames, int width, int height, int cols, int rows)
        {
            Assert.AreEqual((cols, rows), ArtRules.Grid(frames, width, height));
        }

        [Test]
        public void FrameCountComesFromTheFileName()
        {
            Assert.IsTrue(ArtName.TryParse("ENM_A1_RunnerWolf_move_f6.png", out var n));
            Assert.AreEqual("move", n.State);
            Assert.AreEqual(6, n.Frames);
            Assert.IsTrue(ArtName.TryParse("BOS_B2_IndustrialRobot_windup_blast_f4.png", out var b));
            Assert.AreEqual("windup_blast", b.State);
            Assert.AreEqual(4, b.Frames);
        }
    }
}
