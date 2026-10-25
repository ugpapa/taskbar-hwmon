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

    public bool Compact { get; set; }                     // 간편 보기: 온도·팬만
    public bool ClickToggle { get; set; } = true;         // 태스크바 표시 클릭 시 전체↔간편 전환
    public string? Language { get; set; }                  // auto | ko | en | zh-Hans | ja, 없으면 설치 시 고른 언어

    /// <summary>설치 프로그램이 {app}\language.txt에 남긴 언어 → 없으면 Windows 언어.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string EffectiveLanguage => Language ?? InstallerLanguage() ?? "auto";

    static string? InstallerLanguage()
    {
        try
        {
            string f = Path.Combine(AppContext.BaseDirectory, "language.txt");
            return File.Exists(f) ? File.ReadAllText(f).Trim() is { Length: > 0 } s ? s : null : null;
        }
        catch { return null; }
    }

    public bool IsVisible(Metric m) => Compact ? m.InCompact : !Hidden.Contains(m.Id);

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
