using System.Text.Json;

namespace AlienCyborgModernTempleOS
{
    public static class JobFiles
    {
        public static async Task WriteJsonAsync<T>(string path, T data, CancellationToken ct)
        {
            var dir = Path.GetDirectoryName(path);
            if(!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            await using var stream = File.Create(path);
            await JsonSerializer.SerializeAsync(stream, data, new JsonSerializerOptions
            {
                WriteIndented = true
            }, ct);
        }
    }
}
