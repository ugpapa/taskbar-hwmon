using System.Text.Json;

namespace TaskbarHwMon;

sealed class Config
{
    public int OffsetX { get; set; } = 12;      // 태스크바 왼쪽 끝에서 띄울 거리(96dpi 기준 px)
    public float FontPx { get; set; } = 13f;    // 글자 크기(96dpi 기준 px)
    public bool Bold { get; set; }
    public int IntervalMs { get; set; } = 1000;
    public HashSet<string> Hidden { get; set; } = [];   // 숨긴 Metric.Id
    public string CpuFanSensor { get; set; } = "Fan #1"; // 메인보드 팬 중 CPU 쿨러

    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TaskbarHwMon");
    public static string FilePath => Path.Combine(Dir, "config.json");

    public bool IsVisible(Metric m) => !Hidden.Contains(m.Id);

    public static Config Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Config>(File.ReadAllText(FilePath)) ?? new Config();
        }
        catch { }
        return new Config();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
        }
        catch { }
    }

    public Config Clone() => JsonSerializer.Deserialize<Config>(JsonSerializer.Serialize(this, Json))!;
}
