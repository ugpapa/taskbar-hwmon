using System.Globalization;

namespace TaskbarHwMon;

/// <summary>UI 문자열 (한국어 / English / 简体中文 / 日本語). Config.Language가 "auto"면 Windows 표시 언어를 따른다.</summary>
static class L
{
    public static readonly (string Code, string Name)[] Languages =
        [("ko", "한국어"), ("en", "English"), ("zh-Hans", "简体中文"), ("ja", "日本語")];

    // 정적 필드는 선언 순서대로 초기화되므로 여기서 En을 참조하면 null — T()에서 대체한다
    static Dictionary<string, string>? _cur;

    public static void Use(string? language)
    {
        string code = language is null or "auto" ? Detect() : language;
        _cur = code switch { "ko" => Ko, "zh-Hans" => ZhHans, "ja" => Ja, _ => En };
    }

    static string Detect()
    {
        var c = CultureInfo.CurrentUICulture;
        return c.TwoLetterISOLanguageName switch
        {
            "ko" => "ko",
            "ja" => "ja",
            // 번체(zh-Hant: 대만·홍콩)는 간체 대신 영어
            "zh" when !c.Name.Contains("Hant") && c.Name is not ("zh-TW" or "zh-HK" or "zh-MO") => "zh-Hans",
            _ => "en",
        };
    }

    public static string T(string key) =>
        (_cur ?? En).TryGetValue(key, out var s) ? s : En.TryGetValue(key, out var e) ? e : key;
    public static string T(string key, params object[] args) => string.Format(T(key), args);

    static readonly Dictionary<string, string> Ko = new()
    {
        ["SettingsTitle"] = "{0} 설정",
        ["Items"] = "표시 항목",
        ["Usage"] = "사용률",
        ["Temp"] = "온도",
        ["Power"] = "전력",
        ["Clock"] = "클럭",
        ["Voltage"] = "전압",
        ["MemClock"] = "메모리 클럭",
        ["Fan"] = "팬 속도",
        ["CpuCoolerFan"] = "CPU 쿨러 팬",
        ["FanCurrent"] = "{0}  (현재 {1} RPM)",
        ["Look"] = "모양",
        ["DisplayMode"] = "표시 모드",
        ["ModeFull"] = "전체",
        ["ModeCompact"] = "간편 (온도·팬)",
        ["ClickToggle"] = "태스크바 표시를 클릭해 전체 ↔ 간편 전환",
        ["AllMonitors"] = "보조 모니터 태스크바에도 표시",
        ["LeftMargin"] = "왼쪽 여백",
        ["FontSize"] = "글자 크기",
        ["Bold"] = "굵게",
        ["Interval"] = "갱신 간격",
        ["Seconds"] = "{0}초",
        ["Language"] = "언어",
        ["LangAuto"] = "자동 (Windows 언어)",
        ["Start"] = "시작",
        ["AutoStart"] = "Windows 시작 시 자동 실행",
        ["OK"] = "확인",
        ["Cancel"] = "취소",
        ["MenuSettings"] = "설정(&S)...",
        ["MenuCompact"] = "간편 보기(&C)",
        ["MenuExit"] = "종료(&X)",
    };

    static readonly Dictionary<string, string> En = new()
    {
        ["SettingsTitle"] = "{0} Settings",
        ["Items"] = "Items",
        ["Usage"] = "Usage",
        ["Temp"] = "Temperature",
        ["Power"] = "Power",
        ["Clock"] = "Clock",
        ["Voltage"] = "Voltage",
        ["MemClock"] = "Memory clock",
        ["Fan"] = "Fan speed",
        ["CpuCoolerFan"] = "CPU cooler fan",
        ["FanCurrent"] = "{0}  (now {1} RPM)",
        ["Look"] = "Appearance",
        ["DisplayMode"] = "Display mode",
        ["ModeFull"] = "Full",
        ["ModeCompact"] = "Compact (temp · fan)",
        ["ClickToggle"] = "Click the taskbar display to switch Full ↔ Compact",
        ["AllMonitors"] = "Also show on secondary monitor taskbars",
        ["LeftMargin"] = "Left margin",
        ["FontSize"] = "Font size",
        ["Bold"] = "Bold",
        ["Interval"] = "Update interval",
        ["Seconds"] = "{0} s",
        ["Language"] = "Language",
        ["LangAuto"] = "Auto (Windows language)",
        ["Start"] = "Startup",
        ["AutoStart"] = "Start with Windows",
        ["OK"] = "OK",
        ["Cancel"] = "Cancel",
        ["MenuSettings"] = "&Settings...",
        ["MenuCompact"] = "&Compact view",
        ["MenuExit"] = "E&xit",
    };

    static readonly Dictionary<string, string> ZhHans = new()
    {
        ["SettingsTitle"] = "{0} 设置",
        ["Items"] = "显示项目",
        ["Usage"] = "使用率",
        ["Temp"] = "温度",
        ["Power"] = "功耗",
        ["Clock"] = "频率",
        ["Voltage"] = "电压",
        ["MemClock"] = "内存频率",
        ["Fan"] = "风扇转速",
        ["CpuCoolerFan"] = "CPU 散热风扇",
        ["FanCurrent"] = "{0}  (当前 {1} RPM)",
        ["Look"] = "外观",
        ["DisplayMode"] = "显示模式",
        ["ModeFull"] = "完整",
        ["ModeCompact"] = "简洁 (温度·风扇)",
        ["ClickToggle"] = "点击任务栏上的显示内容可切换 完整 ↔ 简洁",
        ["AllMonitors"] = "在副显示器的任务栏上也显示",
        ["LeftMargin"] = "左边距",
        ["FontSize"] = "字体大小",
        ["Bold"] = "粗体",
        ["Interval"] = "刷新间隔",
        ["Seconds"] = "{0} 秒",
        ["Language"] = "语言",
        ["LangAuto"] = "自动 (Windows 语言)",
        ["Start"] = "启动",
        ["AutoStart"] = "开机时自动运行",
        ["OK"] = "确定",
        ["Cancel"] = "取消",
        ["MenuSettings"] = "设置(&S)...",
        ["MenuCompact"] = "简洁视图(&C)",
        ["MenuExit"] = "退出(&X)",
    };

    static readonly Dictionary<string, string> Ja = new()
    {
        ["SettingsTitle"] = "{0} の設定",
        ["Items"] = "表示項目",
        ["Usage"] = "使用率",
        ["Temp"] = "温度",
        ["Power"] = "消費電力",
        ["Clock"] = "クロック",
        ["Voltage"] = "電圧",
        ["MemClock"] = "メモリクロック",
        ["Fan"] = "ファン速度",
        ["CpuCoolerFan"] = "CPU クーラーファン",
        ["FanCurrent"] = "{0}  (現在 {1} RPM)",
        ["Look"] = "外観",
        ["DisplayMode"] = "表示モード",
        ["ModeFull"] = "フル",
        ["ModeCompact"] = "シンプル (温度・ファン)",
        ["ClickToggle"] = "タスクバーの表示をクリックしてフル ↔ シンプルを切り替え",
        ["AllMonitors"] = "サブモニターのタスクバーにも表示",
        ["LeftMargin"] = "左余白",
        ["FontSize"] = "文字サイズ",
        ["Bold"] = "太字",
        ["Interval"] = "更新間隔",
        ["Seconds"] = "{0} 秒",
        ["Language"] = "言語",
        ["LangAuto"] = "自動 (Windows の言語)",
        ["Start"] = "スタートアップ",
        ["AutoStart"] = "Windows 起動時に自動実行",
        ["OK"] = "OK",
        ["Cancel"] = "キャンセル",
        ["MenuSettings"] = "設定(&S)...",
        ["MenuCompact"] = "シンプル表示(&C)",
        ["MenuExit"] = "終了(&X)",
    };
}
