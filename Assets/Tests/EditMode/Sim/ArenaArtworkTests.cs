using Hollow.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Hollow.Tests.Sim
{
    public class ArenaArtworkTests
    {
        [Test]
        public void DetailedGroundShaderImportsWithoutErrors()
        {
            var material = Resources.Load<Material>("ArenaArt/DetailedGround");
            Assert.IsNotNull(material);
            Assert.IsFalse(UnityEditor.ShaderUtil.ShaderHasError(material.shader));
        }
        [TestCase(2172, 724)]
        [TestCase(2048, 683)]
        [TestCase(1024, 341)]
        public void ImportedAtlasSizeCannotBreakSpriteCreationAndGameStartup(int width, int height)
        {
            var texture = new Texture2D(width, height);
            Sprite sprite = null;
            try
            {
                var rect = ArenaAuthoring.ImportedAtlasRect(new Rect(1438, 83, 713, 601), width, height);
                Assert.LessOrEqual(rect.xMax, width);
                Assert.LessOrEqual(rect.yMax, height);
                Assert.DoesNotThrow(() => sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0), rect.width / 3.6f, 0, SpriteMeshType.FullRect));
                Assert.AreEqual(3.6f, sprite.bounds.size.x, 0.001f);
            }
            finally
            {
                if (sprite != null) Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void AuthoredLandmarkFootprintsBlockSimulationCells()
        {
            var layout = WaveTests.RealArena();
            Assert.AreEqual(7, layout.Landmarks.Count);
            foreach (var landmark in layout.Landmarks)
            {
                var grid = Hollow.Sim.ArenaGrid.FromLayout(layout, landmark.Phase);
                for (int y = 0; y < landmark.Footprint.y; y++)
                for (int x = 0; x < landmark.Footprint.x; x++)
                    if (landmark.Occupies(x, y))
                        Assert.IsTrue(grid.IsBlockedCell(landmark.Cell.x + x, landmark.Cell.y + y), landmark.Id);
            }
        }
    }
}
