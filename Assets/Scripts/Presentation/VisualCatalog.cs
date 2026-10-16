using System;
using System.Collections.Generic;
using Hollow.Data;
using UnityEngine;

namespace Hollow.Presentation
{
    [Serializable]
    public sealed class EnemyVisual
    {
        public EnemyId Id;
        public EnemyView Prefab;
    }

    [Serializable]
    public sealed class BossVisual
    {
        public BossId Id;
        public BossView Prefab;
    }

    [Serializable]
    public sealed class WeaponVisual
    {
        public WeaponId Id;
        public Sprite Sprite;
        public Color Color = Color.white;
        public Vector2 Size = new(0.45f, 0.12f);
        public Color ProjectileColor = Color.white;
    }

    /// <summary>
    /// Maps gameplay ids to visual prefabs and sprites. Artists swap the placeholders here or inside the
    /// referenced prefabs; gameplay code never touches visuals (final rule 3).
    /// </summary>
    [CreateAssetMenu(menuName = "Hollow/Visual Catalog")]
    public sealed class VisualCatalog : ScriptableObject
    {
        public List<EnemyVisual> Enemies = new();
        public List<WeaponVisual> Weapons = new();
        public List<BossVisual> Bosses = new();
        public ProjectileView PlayerProjectile;
        public ProjectileView EnemyProjectile;
        public CoinView Coin;
        public ArcView Arc;

        [Header("Reserved readability colours (D14)")]
        public Color Telegraph = new(1f, 0.29f, 0.11f, 1f);
        public Color HitFlash = Color.white;

        public EnemyView EnemyPrefab(EnemyId id)
        {
            foreach (var e in Enemies) if (e.Id == id) return e.Prefab;
            return Enemies.Count > 0 ? Enemies[0].Prefab : null;
        }

        public BossView BossPrefab(BossId id)
        {
            foreach (var b in Bosses) if (b.Id == id) return b.Prefab;
            return null;
        }

        public WeaponVisual Weapon(WeaponId id)
        {
            foreach (var w in Weapons) if (w.Id == id) return w;
            return null;
        }
    }
}
