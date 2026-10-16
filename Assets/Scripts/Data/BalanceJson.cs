using System;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Hollow.Data
{
    /// <summary>JSON round-trip for balance profiles (also used for save snapshots and undo).</summary>
    public static class BalanceJson
    {
        public static readonly JsonSerializerSettings Settings = new()
        {
            Formatting = Formatting.Indented,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            Converters = { new StringEnumConverter() },
            FloatFormatHandling = FloatFormatHandling.String,
        };

        public static string ToJson(BalanceProfile profile) => JsonConvert.SerializeObject(profile, Settings);

        public static BalanceProfile FromJson(string json)
        {
            var p = JsonConvert.DeserializeObject<BalanceProfile>(json, Settings)
                    ?? throw new FormatException("Balance profile JSON is empty.");
            if (p.Schema > BalanceProfile.CurrentSchema)
                throw new FormatException($"Profile schema {p.Schema} is newer than this build ({BalanceProfile.CurrentSchema}).");
            p.BuildIndex();
            return p;
        }

        public static BalanceProfile Clone(BalanceProfile profile) => FromJson(ToJson(profile));

        /// <summary>Short content hash, stored with saves to tell profiles apart.</summary>
        public static string Hash(BalanceProfile profile)
        {
            using var sha = SHA1.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(ToJson(profile)));
            var sb = new StringBuilder(16);
            for (int i = 0; i < 8; i++) sb.Append(bytes[i].ToString("x2"));
            return sb.ToString();
        }
    }
}
