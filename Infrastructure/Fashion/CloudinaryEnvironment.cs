namespace Infrastructure.Fashion;

internal static class CloudinaryEnvironment
{
    public static void AddLocalFile(WebApplicationBuilder builder)
    {
        var root = builder.Environment.ContentRootPath;
        var candidates = new[] { Path.Combine(root, ".env"), Path.Combine(Directory.GetParent(root)?.FullName ?? root, ".env") };
        var path = candidates.FirstOrDefault(File.Exists);
        if (path is null) return;
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "CloudinarySettings__CloudName", "CloudinarySettings__ApiKey", "CloudinarySettings__ApiSecret", "Groq__ApiKey", "Groq__Model" };
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim().TrimStart('\uFEFF');
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var split = line.IndexOf('=');
            if (split <= 0) continue;
            var key = line[..split].Trim();
            if (!keys.Contains(key) || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key))) continue;
            var value = line[(split + 1)..].Trim();
            if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\''))) value = value[1..^1];
            var configKey = key.Replace("__", ":", StringComparison.Ordinal);
            if (!values.TryAdd(configKey, value)) throw new InvalidOperationException($"Biến {key} được khai báo nhiều lần trong .env.");
        }
        builder.Configuration.AddInMemoryCollection(values);
    }
}
