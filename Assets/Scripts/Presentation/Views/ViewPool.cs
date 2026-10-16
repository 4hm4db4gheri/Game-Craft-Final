using System.Collections.Generic;
using UnityEngine;

namespace Hollow.Presentation
{
    /// <summary>Pool of view instances made from one prefab (G7: no instantiate/destroy churn in combat).</summary>
    public sealed class ViewPool<T> where T : Component
    {
        readonly T prefab;
        readonly Transform parent;
        readonly Stack<T> free = new();

        public ViewPool(T prefab, Transform parent)
        {
            this.prefab = prefab;
            this.parent = parent;
        }

        public T Get()
        {
            var v = free.Count > 0 ? free.Pop() : Object.Instantiate(prefab, parent);
            v.gameObject.SetActive(true);
            return v;
        }

        public void Release(T v)
        {
            v.gameObject.SetActive(false);
            free.Push(v);
        }
    }

    /// <summary>Keeps one view per simulation entity id; views of vanished entities go back to their pool.</summary>
    public sealed class EntityViews<T> where T : Component
    {
        readonly Dictionary<int, (T view, ViewPool<T> pool)> live = new();
        readonly HashSet<int> seen = new();
        readonly List<int> stale = new();

        public bool TryGet(int id, out T view)
        {
            if (live.TryGetValue(id, out var entry)) { view = entry.view; return true; }
            view = null;
            return false;
        }

        public T GetOrCreate(int id, ViewPool<T> pool, out bool created)
        {
            seen.Add(id);
            if (live.TryGetValue(id, out var entry)) { created = false; return entry.view; }
            var v = pool.Get();
            live[id] = (v, pool);
            created = true;
            return v;
        }

        /// <summary>Call after a full sync pass: releases every view not touched since the last call.</summary>
        public void ReleaseUnseen(System.Action<int, T> onRelease = null)
        {
            stale.Clear();
            foreach (var id in live.Keys) if (!seen.Contains(id)) stale.Add(id);
            foreach (int id in stale)
            {
                var (view, pool) = live[id];
                onRelease?.Invoke(id, view);
                pool.Release(view);
                live.Remove(id);
            }
            seen.Clear();
        }
    }
}
