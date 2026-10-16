using Hollow.Data;
using Hollow.Sim;
using UnityEngine;

namespace Hollow.Game
{
    /// <summary>
    /// TEMPORARY development sandbox (Editor and development builds only) so combat can be played before
    /// waves exist: spawns A1/A2 in the spawn zones and hands out the phase-1 weapons. Removed by T10.
    /// </summary>
    public sealed class DevSandboxSpawner : MonoBehaviour
    {
        [SerializeField] GameSession session;
        [SerializeField] bool active = true;
        [SerializeField] float interval = 2.5f;
        [SerializeField] int maxAlive = 6;
        [SerializeField] bool giveWeapons = true;

        float next;
        int count;
        bool given;

        void Update()
        {
            if (!active || (!Application.isEditor && !Debug.isDebugBuild)) return;
            var sim = session != null ? session.Sim : null;
            if (sim == null) return;
            var w = sim.World;

            if (giveWeapons && !given)
            {
                given = true;
                WeaponSystem.Give(w, WeaponId.DoubleBarrel);
                WeaponSystem.Give(w, WeaponId.LightSmg);
            }

            if (Time.time < next || w.Enemies.Count >= maxAlive || w.Layout.SpawnZones.Count == 0) return;
            next = Time.time + interval;
            var zone = w.Layout.SpawnZones[count % w.Layout.SpawnZones.Count];
            var pos = new Vector2(Random.Range(zone.Min.x, zone.Max.x), Random.Range(zone.Min.y, zone.Max.y));
            EnemySystem.Spawn(w, count % 3 == 2 ? EnemyId.A2_QuillShooter : EnemyId.A1_RunnerWolf, pos);
            count++;
        }
    }
}
