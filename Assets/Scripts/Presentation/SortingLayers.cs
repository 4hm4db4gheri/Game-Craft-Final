using UnityEngine;

namespace Hollow.Presentation
{
    /// <summary>
    /// Draw order above the world (set up by Hollow > Setup > Configure Sorting Layers): Effects, then
    /// Telegraphs on top, so particles, flashes and numbers never hide an attack warning (§14).
    /// </summary>
    public static class SortingLayers
    {
        public static readonly int Effects = SortingLayer.NameToID("Effects");
        public static readonly int Telegraphs = SortingLayer.NameToID("Telegraphs");
    }
}
