using System;
using System.Collections.Generic;

namespace Hollow.Data
{
    /// <summary>Integer splitting that preserves exact totals (largest-remainder method, ties by order).</summary>
    public static class Rounding
    {
        public static int[] SplitByShares(int total, IReadOnlyList<float> shares)
        {
            int n = shares.Count;
            var result = new int[n];
            if (n == 0 || total <= 0) return result;
            float sum = 0;
            for (int i = 0; i < n; i++) sum += Math.Max(0, shares[i]);
            if (sum <= 0) { result[n - 1] = total; return result; }

            var remainders = new double[n];
            int assigned = 0;
            for (int i = 0; i < n; i++)
            {
                double exact = total * Math.Max(0, shares[i]) / (double)sum;
                result[i] = (int)Math.Floor(exact);
                remainders[i] = exact - result[i];
                assigned += result[i];
            }
            DistributeLeftover(result, remainders, total - assigned);
            return result;
        }

        /// <summary>Scale per-type counts by a multiplier; the new total is round(total × multiplier) exactly.</summary>
        public static List<EnemyCount> ScaleCounts(IReadOnlyList<EnemyCount> counts, float multiplier)
        {
            int total = 0;
            foreach (var c in counts) total += c.Count;
            int target = (int)Math.Round(total * (double)multiplier, MidpointRounding.AwayFromZero);

            var weights = new float[counts.Count];
            for (int i = 0; i < counts.Count; i++) weights[i] = counts[i].Count;
            int[] scaled = SplitByShares(target, weights);

            var result = new List<EnemyCount>(counts.Count);
            for (int i = 0; i < counts.Count; i++) result.Add(new EnemyCount(counts[i].Id, scaled[i]));
            return result;
        }

        public static int ScaleCap(int cap, float multiplier) =>
            Math.Max(1, (int)Math.Round(cap * (double)multiplier, MidpointRounding.AwayFromZero));

        static void DistributeLeftover(int[] result, double[] remainders, int leftover)
        {
            while (leftover > 0)
            {
                int best = 0;
                for (int i = 1; i < remainders.Length; i++)
                    if (remainders[i] > remainders[best] + 1e-9) best = i;
                result[best]++;
                remainders[best] = -1;
                leftover--;
            }
        }
    }
}
