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
        // Layouts written by Tools/ArtGen/animate.py (512 px cells)
        [TestCase(3, 1536, 512, 3, 1)]
        [TestCase(5, 1536, 1024, 3, 2)]
        [TestCase(7, 2048, 1024, 4, 2)]
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

        [Test]
        public void HandDrawnSheetsBeatGeneratedSheetsWhichBeatSingleFrames()
        {
            int hand = ApplyIncomingArt.Rank("Assets/Art/Incoming/ENM_A1_RunnerWolf_move_f6.png", 6);
            int generated = ApplyIncomingArt.Rank("Assets/Art/Incoming/Generated/ENM_A1_RunnerWolf_move_f6.png", 6);
            int single = ApplyIncomingArt.Rank("Assets/Art/Incoming/ENM_A1_RunnerWolf_move.png", 1);
            Assert.Greater(hand, generated);
            Assert.Greater(generated, single);
        }

        [Test]
        public void AStateCanPlayAtItsOwnRate()
        {
            var set = UnityEngine.ScriptableObject.CreateInstance<Hollow.Presentation.SpriteSet>();
            set.FramesPerSecond = 10;
            Assert.AreEqual(10f, new Hollow.Presentation.SpriteState().Rate(set));
            Assert.AreEqual(6f, new Hollow.Presentation.SpriteState { FramesPerSecond = 6 }.Rate(set));
            UnityEngine.Object.DestroyImmediate(set);
        }

        /// <summary>3/4 ground: one seamless sprite at 128 px per unit, drawn tiled over the arena (no grid of cells).</summary>
        [Test]
        public void GroundArtIsOneTiledSpriteAt128PixelsPerUnit()
        {
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("_Ground_ground t:Texture2D", new[] { ArtRules.IncomingDir }))
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var imp = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(path);
                Assert.AreEqual(UnityEditor.SpriteImportMode.Single, imp.spriteImportMode, path);
                Assert.AreEqual(ArtRules.GroundPixelsPerUnit, imp.spritePixelsPerUnit, path);
                var settings = new UnityEditor.TextureImporterSettings();
                imp.ReadTextureSettings(settings);
                Assert.AreEqual(UnityEngine.SpriteMeshType.FullRect, settings.spriteMeshType, path);
            }
        }
    }
}
