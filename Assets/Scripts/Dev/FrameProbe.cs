using System.Collections.Generic;
using System.Globalization;
using Unity.Profiling;
using UnityEngine;

namespace Hollow.Dev
{
    /// <summary>
    /// Measures frame times and managed allocations over a fixed real-time window (stress reports, T05/T21).
    /// Start with <see cref="Run"/>; read <see cref="Report"/> once <see cref="Done"/> is true.
    /// </summary>
    public sealed class FrameProbe : MonoBehaviour
    {
        public static FrameProbe Last { get; private set; }

        public bool Done { get; private set; }
        public string Report { get; private set; } = "running";

        readonly List<float> frames = new();
        readonly List<long> allocs = new();
        ProfilerRecorder gcAlloc;
        float until;
        string label;
        int gcStart;

        public static FrameProbe Run(float seconds, string label)
        {
            var go = new GameObject("FrameProbe");
            DontDestroyOnLoad(go);
            var p = go.AddComponent<FrameProbe>();
            p.until = Time.unscaledTime + seconds;
            p.label = label;
            p.gcStart = System.GC.CollectionCount(0);
            p.gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            Last = p;
            return p;
        }

        void Update()
        {
            if (Done) return;
            frames.Add(Time.unscaledDeltaTime);
            if (gcAlloc.Valid) allocs.Add(gcAlloc.LastValue);
            if (Time.unscaledTime >= until) Finish();
        }

        void Finish()
        {
            Done = true;
            gcAlloc.Dispose();
            if (frames.Count > 1) frames.RemoveAt(0); // the first frame includes the setup call
            var sorted = new List<float>(frames);
            sorted.Sort();
            float sum = 0f;
            foreach (var f in frames) sum += f;
            float avg = sum / Mathf.Max(1, frames.Count);
            float P(float q) => sorted.Count == 0 ? 0 : sorted[Mathf.Clamp(Mathf.CeilToInt(q * sorted.Count) - 1, 0, sorted.Count - 1)];
            long allocSum = 0;
            foreach (var a in allocs) allocSum += a;
            var c = CultureInfo.InvariantCulture;
            Report = string.Format(c,
                "{0}: {1} frames, avg {2:F2} ms ({3:F0} fps), p95 {4:F2} ms, p99 {5:F2} ms, max {6:F2} ms; GC alloc avg {7:F0} B/frame, gen0 collections {8}; " +
                "{9}; CPU {10} ({11} threads), GPU {12}, RAM {13} MB, screen {14}x{15}",
                label, frames.Count, avg * 1000f, 1f / Mathf.Max(1e-6f, avg), P(0.95f) * 1000f, P(0.99f) * 1000f, sorted.Count > 0 ? sorted[^1] * 1000f : 0f,
                allocs.Count > 0 ? allocSum / (double)allocs.Count : 0, System.GC.CollectionCount(0) - gcStart,
                Application.isEditor ? "Editor play mode" : Debug.isDebugBuild ? "development build" : "release build",
                SystemInfo.processorType, SystemInfo.processorCount, SystemInfo.graphicsDeviceName, SystemInfo.systemMemorySize, Screen.width, Screen.height);
            Debug.Log("[Hollow perf] " + Report);
        }

        void OnDestroy()
        {
            if (!Done && gcAlloc.Valid) gcAlloc.Dispose();
        }
    }
}
