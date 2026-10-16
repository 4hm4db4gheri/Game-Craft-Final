using System;
using UnityEngine;

namespace Hollow.Sim
{
    public static class ArenaLayoutStore
    {
        public const string ResourcePath = "Arena/ArenaLayout";

        public static ArenaLayout LoadFromResources()
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null) throw new InvalidOperationException($"Missing Resources/{ResourcePath}.json — bake the arena (Hollow > Arena > Bake Layout).");
            return FromJson(asset.text);
        }

        public static ArenaLayout FromJson(string json) => JsonUtility.FromJson<ArenaLayout>(json);
        public static string ToJson(ArenaLayout layout) => JsonUtility.ToJson(layout, true);
    }
}
