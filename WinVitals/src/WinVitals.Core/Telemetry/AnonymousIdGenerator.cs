using System.Security.Cryptography;

namespace WinVitals.Core.Telemetry;

public static class AnonymousIdGenerator
{
    public static string GetOrCreate(string dataDir)
    {
        var file = Path.Combine(dataDir, "anonymous_id");
        if (File.Exists(file))
        {
            var v = File.ReadAllText(file).Trim();
            if (v.Length == 32) return v;
        }
        var bytes = RandomNumberGenerator.GetBytes(16);
        var id = Convert.ToHexString(bytes);
        Directory.CreateDirectory(dataDir);
        File.WriteAllText(file, id);
        return id;
    }
}
