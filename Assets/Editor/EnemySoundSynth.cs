using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hollow.EditorTools
{
    /// <summary>
    /// Generates the per-type enemy sounds (T31, GDD v0.19 §20): presence, warning, attack and death for each of
    /// the 21 enemy types, 3 spawned types and 3 bosses (bosses also get an area-attack warning). Sounds are
    /// synthesised from small recipes (growls, breath, servos, beeps, metal rings, whooshes) so every type is
    /// distinct without downloaded files; hits reuse the shared impact sounds pitched per type. They are
    /// placeholders for the audio owner: replace any file in Assets/Audio/Enemies with the same name.
    /// Output: Assets/Audio/Enemies/Enemy_&lt;Key&gt;_&lt;Event&gt;.wav, deterministic for a key.
    /// </summary>
    public static class EnemySoundSynth
    {
        public const string Dir = "Assets/Audio/Enemies";
        const int SR = 22050;

        [MenuItem("Hollow/Audio/Generate Enemy Sounds")]
        public static void GenerateMenu() => Debug.Log("[Hollow audio] " + Generate());

        public static string Generate()
        {
            Directory.CreateDirectory(Dir);
            int files = 0;
            foreach (var (key, events) in Recipes())
                foreach (var (ev, make) in events)
                {
                    var rng = new System.Random(Hash(key + ev));
                    var s = new Synth(rng);
                    float[] samples = make(s);
                    Normalize(samples, 0.9f);
                    WriteWav($"{Dir}/Enemy_{key}_{ev}.wav", samples);
                    files++;
                }
            AssetDatabase.Refresh();
            AudioSetup.BuildCatalog();
            return $"{files} enemy sound(s) written to {Dir}";
        }

        // ------------------------------------------------------------------ recipes

        delegate float[] Make(Synth s);

        static IEnumerable<(string Key, List<(string Event, Make Make)> Events)> Recipes()
        {
            List<(string, Make)> E(Make presence, Make warning, Make attack, Make death, Make area = null)
            {
                var l = new List<(string, Make)> { ("Presence", presence), ("Warning", warning), ("Attack", attack), ("Death", death) };
                if (area != null) l.Add(("AreaWarning", area));
                return l;
            }

            // Animals: breath, growls, quills, shells, howls, clicks
            yield return ("A1_RunnerWolf", E(s => s.Breath(0.4f, 900, 2), s => s.Growl(95, 0.4f, 30), s => s.Mix(s.Crack(0.06f), s.Thud(220, 0.12f)), s => s.Whine(900, 300, 0.5f)));
            yield return ("A2_QuillShooter", E(s => s.Ticks(10, 10, 0.4f, 4000), s => s.Ticks(8, 40, 0.55f, 5000), s => s.Mix(s.Whoosh(0.15f, 3000, 1200), s.Crack(0.04f)), s => s.Mix(s.Thud(140, 0.3f), s.Ticks(20, 4, 0.35f, 3000))));
            yield return ("A3_ChargingBoar", E(s => s.Am(s.Breath(0.3f, 500, 1), 18, 0.8f), s => s.Whoosh(0.8f, 300, 900), s => s.Mix(s.Thud(70, 0.35f), s.Crack(0.05f)), s => s.Mix(s.Thud(55, 0.6f), s.Breath(0.4f, 400, 1))));
            yield return ("A4_ArmoredTortoise", E(s => s.Whoosh(0.5f, 600, 500, 0.4f), s => s.Seq(0.14f, s.Ring(new[] { 310f, 720f }, 0.12f), s.Ring(new[] { 310f, 720f }, 0.12f)), s => s.Ring(new[] { 180f, 410f, 690f }, 0.3f), s => s.Seq(0.12f, s.Ring(new[] { 330f, 760f }, 0.1f), s.Ring(new[] { 290f, 690f }, 0.1f), s.Ring(new[] { 250f, 600f }, 0.15f))));
            yield return ("A5_HowlingWolf", E(s => s.Breath(0.9f, 700, 1), s => s.Whoosh(0.7f, 400, 1600), s => s.Whine(420, 650, 1.2f, 0.05f), s => s.Whine(600, 180, 0.7f, 0.08f)));
            yield return ("A6_ExplosiveBeetle", E(s => s.Ticks(18, 18, 0.35f, 3000), s => s.Beeps(900, 4, 16, 0.8f), s => s.Mix(s.NoiseBurst(0.5f, 600), s.Thud(60, 0.4f)), s => s.Mix(s.Crack(0.08f), s.Ticks(25, 10, 0.15f, 3500))));
            yield return ("A7_MotherSpider", E(s => s.Ticks(26, 26, 0.5f, 2500, 0.5f), s => s.Whine(200, 500, 0.6f), s => s.Mix(s.Thud(300, 0.15f, 0.4f), s.NoiseBurst(0.12f, 1500)), s => s.Mix(s.Ticks(20, 3, 0.6f, 2000), s.Thud(90, 0.4f))));
            yield return ("A7c_Spiderling", E(s => s.Ticks(30, 30, 0.25f, 4500, 0.5f), s => s.Whine(400, 800, 0.3f), s => s.Mix(s.Crack(0.04f), s.Thud(500, 0.08f)), s => s.Whine(1200, 600, 0.2f)));

            // Robots: servos, beeps, motors, saws, metal
            yield return ("R1_Crawler", E(s => s.Servo(220, 260, 0.4f), s => s.Seq(0.1f, s.Ring(new[] { 520f, 1180f }, 0.08f), s.Ring(new[] { 520f, 1180f }, 0.08f)), s => s.Ring(new[] { 340f, 860f, 1500f }, 0.2f), s => s.Servo(200, 40, 0.6f)));
            yield return ("R2_Shooter", E(s => s.Seq(0.25f, s.Ring(new[] { 900f, 2100f }, 0.05f), s.Ring(new[] { 900f, 2100f }, 0.05f)), s => s.Tone(300, 1200, 0.5f), s => s.Zap(1400, 300, 0.12f), s => s.Mix(s.Crackle(0.3f), s.Zap(600, 80, 0.4f))));
            yield return ("R3_SawCharger", E(s => s.Motor(140, 0.5f, 5), s => s.Rev(120, 700, 0.7f), s => s.Mix(s.Rev(700, 900, 0.3f), s.Crack(0.2f)), s => s.Mix(s.Rev(200, 50, 0.6f), s.Crackle(0.4f))));
            yield return ("R4_ShieldRobot", E(s => s.Ticks(8, 8, 0.4f, 2800, 0.6f), s => s.Square(80, 160, 0.5f), s => s.Ring(new[] { 120f, 260f, 530f }, 0.35f), s => s.Mix(s.Whoosh(0.5f, 2000, 200), s.Crackle(0.4f))));
            yield return ("R5_Repair", E(s => s.Ticks(5, 5, 0.4f, 6000, 0.4f), s => s.Beeps(1300, 8, 8, 0.25f), s => s.Warble(600, 100, 8, 0.6f), s => s.Seq(0.09f, s.Ring(new[] { 1800f, 2500f }, 0.06f), s.Ring(new[] { 1600f, 2300f }, 0.06f), s.Ring(new[] { 1400f, 2100f }, 0.08f))));
            yield return ("R6_Suicide", E(s => s.Servo(320, 420, 0.25f), s => s.Beeps(1600, 5, 22, 0.9f), s => s.Mix(s.NoiseBurst(0.5f, 1200), s.Ring(new[] { 200f, 450f }, 0.4f)), s => s.Zap(900, 100, 0.3f)));
            yield return ("R7_DroneCarrier", E(s => s.Motor(60, 0.6f, 2), s => s.Creak(100, 70, 0.65f), s => s.Whoosh(0.4f, 400, 2500), s => s.Mix(s.Rev(120, 30, 0.8f), s.Ring(new[] { 160f, 380f }, 0.4f))));
            yield return ("R7c_Drone", E(s => s.Am(s.Saw(180, 180, 0.35f), 40, 0.6f), s => s.Am(s.Saw(180, 320, 0.35f), 40, 0.5f), s => s.Mix(s.Zap(1000, 500, 0.08f), s.Crack(0.03f)), s => s.Whine(800, 150, 0.4f)));

            // Humans: steps, gear, breath, metal, devices
            yield return ("H1_BatonGuard", E(s => s.Steps(2, 0.5f), s => s.Whoosh(0.35f, 500, 1400, 0.6f), s => s.Whoosh(0.18f, 600, 2400), s => s.Mix(s.Ring(new[] { 600f, 1400f }, 0.08f), s.Thud(110, 0.3f))));
            yield return ("H2_RifleGuard", E(s => s.Mix(s.Steps(2, 0.5f), s.Ticks(6, 6, 0.5f, 3500, 0.3f)), s => s.Seq(0.12f, s.Ring(new[] { 1500f, 3200f }, 0.04f), s.Ring(new[] { 1300f, 2900f }, 0.04f)), s => s.Mix(s.Crack(0.07f), s.Thud(200, 0.06f)), s => s.Mix(s.Ring(new[] { 800f, 1900f }, 0.2f), s.Thud(120, 0.25f))));
            yield return ("H3_FastAssaulter", E(s => s.Steps(3, 0.45f), s => s.Am(s.Whoosh(0.45f, 700, 1200), 6, 0.4f), s => s.Mix(s.Thud(90, 0.3f), s.Crack(0.05f)), s => s.Whoosh(0.5f, 300, 150)));
            yield return ("H4_ShieldGuard", E(s => s.Mix(s.Steps(2, 0.55f), s.Ring(new[] { 700f, 1500f }, 0.1f, 0.3f)), s => s.Ring(new[] { 150f, 330f }, 0.3f), s => s.Mix(s.Ring(new[] { 200f, 470f, 900f }, 0.3f), s.Thud(100, 0.2f)), s => s.Ring(new[] { 260f, 610f }, 0.6f)));
            yield return ("H5_Medic", E(s => s.Mix(s.Steps(2, 0.5f), s.Whoosh(0.4f, 3000, 2500, 0.25f)), s => s.Seq(0.08f, s.Ring(new[] { 2000f }, 0.03f), s.Ring(new[] { 2600f }, 0.04f)), s => s.Chime(new[] { 660f, 880f, 1320f }, 0.5f), s => s.Mix(s.Thud(140, 0.3f), s.Whoosh(0.3f, 2500, 1500, 0.3f))));
            yield return ("H6_Grenadier", E(s => s.Ticks(4, 4, 0.5f, 3000, 0.5f), s => s.Seq(0.1f, s.Ring(new[] { 2500f }, 0.03f), s.Whoosh(0.3f, 500, 1500)), s => s.Whoosh(0.25f, 800, 300), s => s.Mix(s.Ticks(15, 5, 0.3f, 3000), s.Thud(120, 0.3f))));
            yield return ("H7_Engineer", E(s => s.Mix(s.Steps(2, 0.5f), s.Ring(new[] { 1800f, 2700f }, 0.05f, 0.4f)), s => s.Creak(140, 110, 0.6f), s => s.Seq(0.12f, s.Tone(700, 700, 0.1f), s.Tone(900, 900, 0.1f), s.Tone(1200, 1200, 0.15f)), s => s.Mix(s.Seq(0.08f, s.Ring(new[] { 1900f }, 0.05f), s.Ring(new[] { 1500f }, 0.05f)), s.Thud(130, 0.3f))));
            yield return ("H7c_Turret", E(s => s.Servo(400, 480, 0.2f), s => s.Beeps(1100, 7, 7, 0.3f), s => s.Zap(1600, 600, 0.1f), s => s.Mix(s.Crackle(0.35f), s.Thud(80, 0.35f))));

            // Bosses: deeper, longer, and a separate area-attack warning
            yield return ("Boss_LargeBeast", E(s => s.Breath(0.9f, 350, 1), s => s.Growl(60, 0.8f, 22), s => s.Mix(s.Thud(45, 0.6f), s.Crack(0.08f)), s => s.Growl(70, 1.5f, 18, 40), s => s.Mix(s.Whoosh(0.9f, 150, 600), s.Growl(55, 0.9f, 26))));
            yield return ("Boss_IndustrialRobot", E(s => s.Motor(50, 0.8f, 1.5f), s => s.Rev(200, 900, 0.7f), s => s.Zap(1200, 400, 0.1f), s => s.Mix(s.Crackle(0.8f), s.Ring(new[] { 140f, 330f, 610f }, 1f), s.Rev(160, 30, 1.2f)), s => s.Siren(500, 700, 1f)));
            yield return ("Boss_FormerCompanion", E(s => s.Mix(s.Steps(2, 0.55f), s.Ticks(5, 5, 0.5f, 2800, 0.4f)), s => s.Mix(s.Seq(0.1f, s.Ring(new[] { 1500f, 3200f }, 0.04f), s.Ring(new[] { 1300f, 2900f }, 0.04f)), s.Whine(500, 900, 0.7f, 0.01f, 0.3f)), s => s.Mix(s.Crack(0.08f), s.Thud(150, 0.1f)), s => s.Mix(s.Whoosh(0.8f, 600, 150), s.Ring(new[] { 700f, 1600f }, 0.4f), s.Thud(100, 0.5f)), s => s.Seq(0.12f, s.Ring(new[] { 2600f }, 0.03f), s.Whoosh(0.5f, 600, 1600))));
        }

        // ------------------------------------------------------------------ synth

        sealed class Synth
        {
            readonly System.Random rng;
            public Synth(System.Random r) { rng = r; }

            float Rand() => (float)(rng.NextDouble() * 2 - 1);
            static float[] Buf(float sec) => new float[Mathf.Max(1, (int)(sec * SR))];

            static float Env(int i, int n, float attack = 0.01f, float curve = 3f)
            {
                float t = i / (float)SR, len = n / (float)SR;
                float a = attack > 0 ? Mathf.Clamp01(t / attack) : 1f;
                return a * Mathf.Exp(-curve * t / Mathf.Max(0.01f, len));
            }

            float[] Lowpass(float[] x, float cutoff)
            {
                float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / SR), y = 0;
                for (int i = 0; i < x.Length; i++) { y += a * (x[i] - y); x[i] = y; }
                return x;
            }

            float[] Highpass(float[] x, float cutoff)
            {
                float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / SR), low = 0;
                for (int i = 0; i < x.Length; i++) { low += a * (x[i] - low); x[i] -= low; }
                return x;
            }

            public float[] Mix(params float[][] parts)
            {
                int n = 0;
                foreach (var p in parts) n = Mathf.Max(n, p.Length);
                var y = new float[n];
                foreach (var p in parts) for (int i = 0; i < p.Length; i++) y[i] += p[i];
                return y;
            }

            /// <summary>Parts one after another, each starting <paramref name="step"/> seconds after the previous.</summary>
            public float[] Seq(float step, params float[][] parts)
            {
                int off = (int)(step * SR), n = 0;
                for (int k = 0; k < parts.Length; k++) n = Mathf.Max(n, k * off + parts[k].Length);
                var y = new float[n];
                for (int k = 0; k < parts.Length; k++) for (int i = 0; i < parts[k].Length; i++) y[k * off + i] += parts[k][i];
                return y;
            }

            public float[] Am(float[] x, float rate, float depth)
            {
                for (int i = 0; i < x.Length; i++) x[i] *= 1f - depth * 0.5f * (1f + Mathf.Sin(2f * Mathf.PI * rate * i / SR));
                return x;
            }

            float[] Osc(Func<float, float> wave, float f0, float f1, float sec, float vib = 0f, float vibHz = 6f, float curve = 3f, float attack = 0.01f)
            {
                var y = Buf(sec);
                float phase = 0;
                for (int i = 0; i < y.Length; i++)
                {
                    float k = i / (float)y.Length;
                    float f = f0 * Mathf.Pow(f1 / f0, k) * (1f + vib * Mathf.Sin(2f * Mathf.PI * vibHz * i / SR));
                    phase += f / SR;
                    y[i] = wave(phase - Mathf.Floor(phase)) * Env(i, y.Length, attack, curve);
                }
                return y;
            }

            static float SineW(float p) => Mathf.Sin(2f * Mathf.PI * p);
            static float SawW(float p) => 2f * p - 1f;
            static float SquareW(float p) => p < 0.5f ? 1f : -1f;

            float[] Noise(float sec, float curve = 3f, float attack = 0.005f)
            {
                var y = Buf(sec);
                for (int i = 0; i < y.Length; i++) y[i] = Rand() * Env(i, y.Length, attack, curve);
                return y;
            }

            public float[] Tone(float f0, float f1, float sec) => Osc(SineW, f0, f1, sec, curve: 1.5f);
            public float[] Saw(float f0, float f1, float sec) => Lowpass(Osc(SawW, f0, f1, sec, curve: 1.5f), 2500);
            public float[] Square(float f0, float f1, float sec) => Lowpass(Osc(SquareW, f0, f1, sec, curve: 1.2f), 1800);
            public float[] Whine(float f0, float f1, float sec, float vib = 0.03f, float gain = 1f)
            {
                var y = Osc(SineW, f0, f1, sec, vib, 6f, 1.8f, 0.03f);
                for (int i = 0; i < y.Length; i++) y[i] *= gain;
                return y;
            }
            public float[] Breath(float sec, float cutoff, int puffs)
            {
                var y = Lowpass(Noise(sec, 1f, 0.08f), cutoff);
                return puffs > 1 ? Am(y, puffs / sec, 0.9f) : y;
            }
            public float[] Growl(float f, float sec, float amRate, float f1 = 0f)
            {
                var y = Mix(Lowpass(Osc(SawW, f, f1 > 0 ? f1 : f * 0.9f, sec, 0.02f, 4f, 1.5f, 0.05f), 800), Lowpass(Noise(sec, 1.5f, 0.05f), 600));
                return Am(y, amRate, 0.7f);
            }
            public float[] Thud(float f, float sec, float gain = 1f)
            {
                var y = Mix(Osc(SineW, f, f * 0.5f, sec, curve: 6f, attack: 0.002f), Lowpass(Noise(sec * 0.4f, 8f), 300));
                for (int i = 0; i < y.Length; i++) y[i] *= gain;
                return y;
            }
            public float[] Crack(float sec) => Highpass(Noise(sec, 7f, 0.001f), 2000);
            public float[] NoiseBurst(float sec, float cutoff) => Lowpass(Noise(sec, 4f, 0.003f), cutoff);
            public float[] Crackle(float sec)
            {
                var y = Buf(sec);
                for (int i = 0; i < y.Length; i++) if (rng.NextDouble() < 0.004) for (int j = 0; j < 60 && i + j < y.Length; j++) y[i + j] += Rand() * Mathf.Exp(-j / 12f);
                for (int i = 0; i < y.Length; i++) y[i] *= Env(i, y.Length, 0.005f, 1.5f);
                return Highpass(y, 1200);
            }
            public float[] Whoosh(float sec, float f0, float f1, float gain = 1f)
            {
                var y = Noise(sec, 1.5f, sec * 0.3f);
                float lowA = 0, bandY = 0;
                for (int i = 0; i < y.Length; i++)
                {
                    float f = f0 * Mathf.Pow(f1 / f0, i / (float)y.Length);
                    float a = 1f - Mathf.Exp(-2f * Mathf.PI * f / SR);
                    lowA += a * (y[i] - lowA);
                    bandY += a * 0.5f * (lowA - bandY);
                    y[i] = (lowA - bandY) * gain;
                }
                return y;
            }
            public float[] Ticks(float rate0, float rate1, float sec, float band, float gain = 1f)
            {
                var y = Buf(sec);
                float t = 0;
                while (t < sec)
                {
                    int start = (int)(t * SR);
                    for (int j = 0; j < SR / 200 && start + j < y.Length; j++) y[start + j] += Rand() * Mathf.Exp(-j / 20f) * gain;
                    float rate = Mathf.Lerp(rate0, rate1, t / sec);
                    t += 1f / Mathf.Max(1f, rate) * (0.8f + 0.4f * (float)rng.NextDouble());
                }
                return Highpass(y, band * 0.5f);
            }
            public float[] Beeps(float f, float rate0, float rate1, float sec)
            {
                var y = Buf(sec);
                float t = 0;
                while (t < sec)
                {
                    var b = Osc(SquareW, f, f, 0.05f, curve: 2f, attack: 0.002f);
                    int start = (int)(t * SR);
                    for (int j = 0; j < b.Length && start + j < y.Length; j++) y[start + j] += b[j] * 0.6f;
                    t += 1f / Mathf.Lerp(rate0, rate1, t / sec);
                }
                return y;
            }
            public float[] Ring(float[] partials, float sec, float gain = 1f)
            {
                var y = Buf(sec);
                foreach (float p in partials)
                    for (int i = 0; i < y.Length; i++) y[i] += Mathf.Sin(2f * Mathf.PI * p * i / SR) * Mathf.Exp(-5f * i / y.Length) * gain / partials.Length;
                return y;
            }
            public float[] Chime(float[] notes, float sec)
            {
                float step = sec / notes.Length;
                var parts = new float[notes.Length][];
                for (int k = 0; k < notes.Length; k++) parts[k] = Osc(SineW, notes[k], notes[k], step * 1.6f, curve: 4f, attack: 0.003f);
                return Seq(step, parts);
            }
            public float[] Servo(float f0, float f1, float sec) => Mix(Lowpass(Osc(SawW, f0, f1, sec, 0.03f, 9f, 1.2f, 0.03f), 1500), Highpass(Noise(sec, 2f), 3000));
            public float[] Motor(float f, float sec, float lfo) => Lowpass(Osc(SawW, f, f, sec, 0.06f, lfo, 0.8f, 0.05f), 1200);
            public float[] Rev(float f0, float f1, float sec) => Lowpass(Osc(SawW, f0, f1, sec, 0.01f, 30f, 0.6f, 0.02f), Mathf.Max(f0, f1) * 3f);
            public float[] Zap(float f0, float f1, float sec) => Osc(SquareW, f0, f1, sec, curve: 4f, attack: 0.001f);
            public float[] Creak(float f0, float f1, float sec) => Lowpass(Osc(SawW, f0, f1, sec, 0.08f, 11f, 1.5f, 0.05f), 900);
            public float[] Warble(float f, float depth, float rate, float sec) => Osc(SineW, f, f, sec, depth / f, rate, 1.2f, 0.02f);
            public float[] Siren(float f0, float f1, float sec)
            {
                var a = Square(f0, f0, sec / 2f);
                var b = Square(f1, f1, sec / 2f);
                return Seq(sec / 2f, a, b);
            }
            public float[] Steps(int count, float sec)
            {
                var parts = new float[count][];
                for (int k = 0; k < count; k++) parts[k] = Lowpass(Noise(0.09f, 6f, 0.002f), 500 + 80 * k);
                return Seq(sec / count, parts);
            }
        }

        // ------------------------------------------------------------------ output

        static void Normalize(float[] x, float peak)
        {
            float max = 0.0001f;
            foreach (float v in x) max = Mathf.Max(max, Mathf.Abs(v));
            float g = peak / max;
            int fade = Mathf.Min(x.Length / 4, SR / 200); // no click at the end
            for (int i = 0; i < x.Length; i++) x[i] *= g * (i >= x.Length - fade ? (x.Length - i) / (float)fade : 1f);
        }

        static void WriteWav(string path, float[] x)
        {
            using var w = new BinaryWriter(File.Create(path));
            int bytes = x.Length * 2;
            w.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' }); w.Write(36 + bytes);
            w.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });
            w.Write(new[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' }); w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(SR); w.Write(SR * 2); w.Write((short)2); w.Write((short)16);
            w.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' }); w.Write(bytes);
            foreach (float v in x) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(v * 32767f), -32768, 32767));
        }

        static int Hash(string s)
        {
            unchecked
            {
                int h = 23;
                foreach (char c in s) h = h * 31 + c;
                return h;
            }
        }
    }
}
