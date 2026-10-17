using System;
using System.Collections.Generic;
using Hollow.Data;
using Hollow.Sim;
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
        /// <summary>Imported art has its pivot at the grip; placeholders are centred.</summary>
        public bool PivotAtGrip;
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

        [Header("Imported art (Hollow > Art > Apply Incoming Art); empty = placeholder")]
        public Sprite PlayerBulletArt;
        public Sprite PlayerPelletArt;
        /// <summary>Enemy shots per phase: quill (animals), bolt (robots), bullet (humans).</summary>
        public Sprite[] EnemyShotArt = new Sprite[3];
        public SpriteSet MuzzleFlashFx;
        public SpriteSet ImpactFx;
        public SpriteSet ExplosionFx;

        /// <summary>Art for a shot: pellets for weapons with falloff (shotguns), the phase's enemy shot otherwise.</summary>
        public Sprite ProjectileArt(ProjectileState p, int phase)
        {
            if (p.Owner != ProjectileOwner.Enemy) return p.FalloffAtMaxRange < 1f && PlayerPelletArt != null ? PlayerPelletArt : PlayerBulletArt;
            int i = Mathf.Clamp(phase - 1, 0, 2);
            return EnemyShotArt != null && EnemyShotArt.Length > i ? EnemyShotArt[i] : null;
        }

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
