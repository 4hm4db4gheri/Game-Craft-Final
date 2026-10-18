using Hollow.UI;
using NUnit.Framework;
using UnityEngine;

namespace Hollow.Tests.Data
{
    /// <summary>The illustrated story scenes (D98): every trigger has a scene, every line a speaker and a picture.</summary>
    public class CutsceneTests
    {
        static readonly string[] Triggered =
            { "Opening", "Boss1Entrance", "Phase1To2", "Boss2Entrance", "Phase2To3", "Boss3Entrance", "Ending" };

        [Test]
        public void EverySceneTheGamePlaysOpensOnAPictureAndEveryLineHasASpeaker()
        {
            var data = DialogueData.Load();
            foreach (var id in Triggered)
            {
                var scene = data.Scene(id);
                Assert.IsNotNull(scene, id);
                Assert.IsNotEmpty(scene.Lines, id);
                Assert.IsNotEmpty(scene.Lines[0].Shot, id + " must open on a picture");
                foreach (var line in scene.Lines)
                {
                    Assert.IsNotNull(data.Speaker(line.Speaker), $"{id}: speaker {line.Speaker}");
                    Assert.IsNotEmpty(line.Text, id);
                }
            }
        }

        [Test]
        public void EveryShotHasItsPictureAtFullWidth()
        {
            var data = DialogueData.Load();
            foreach (var scene in data.Scenes)
                foreach (var line in scene.Lines)
                {
                    if (string.IsNullOrEmpty(line.Shot)) continue;
                    var shot = data.Shot(line.Shot);
                    Assert.IsNotNull(shot, $"{scene.Id}: shot {line.Shot} has no framing");
                    string image = string.IsNullOrEmpty(shot.Image) ? shot.Id : shot.Image;
                    var picture = Resources.Load<Texture2D>("Cutscenes/" + image);
                    Assert.IsNotNull(picture, $"Resources/Cutscenes/{image} (Tools/ArtGen/cutscenes.py)");
                    Assert.GreaterOrEqual(picture.width, 1600, image + " was shrunk on import");
                    Assert.Greater(shot.ZoomFrom, 0.99f, line.Shot + " would show the frame's edge");
                }
        }
    }
}
