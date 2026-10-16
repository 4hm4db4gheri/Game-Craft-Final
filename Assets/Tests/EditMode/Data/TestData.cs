using System.IO;
using Hollow.Data;
using UnityEngine;

namespace Hollow.Tests.Data
{
    public static class TestData
    {
        public static string BaselinePath => Path.Combine(Application.dataPath, "Resources/Balance/Baseline.json");

        /// <summary>A fresh copy of the committed baseline profile.</summary>
        public static BalanceProfile Baseline() => BalanceJson.FromJson(File.ReadAllText(BaselinePath));
    }
}
