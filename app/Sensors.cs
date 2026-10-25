using LibreHardwareMonitor.Hardware;

namespace TaskbarHwMon;

record Snapshot(
    float? GpuClock, float? GpuPower, float? GpuTemp, float? GpuVolt, float? VramClock, float? GpuLoad, float? GpuFan,
    float? CpuClock, float? CpuPower, float? CpuTemp, float? CpuLoad, float? CpuVolt, float? DramClock, float? CpuFan);

enum Tone { Normal, Dim, Temp }

/// <summary>표시 항목. Column은 GPU/CPU 두 줄에서 같은 열에 정렬되는 위치.</summary>
record Metric(string Id, string Label, bool Gpu, int Column, Func<Snapshot, float?> Get, string Format, string Unit, Tone Tone = Tone.Normal, string Prefix = "")
{
    public string Text(Snapshot s) => Prefix + (Get(s) is float f ? f.ToString(Format) + Unit : "-");

    /// <summary>간편 보기에 나오는 항목: 온도, 팬.</summary>
    public bool InCompact => Column is 2 or 7;

    // 열: 0 이름, 1 사용률, 2 온도, 3 전력, 4 클럭, 5 전압, 6 VRAM 클럭, 7 팬
    public static readonly string[] ColumnTemplates = ["GPU", "100%", "100°C", "450.0W", "5800MHz", "1.100V", "VRAM 10501MHz", "3000RPM"];

    public static readonly Metric[] All =
    [
        new("GpuLoad",   "GPU 사용률",    true,  1, s => s.GpuLoad,   "0",     "%"),
        new("GpuTemp",   "GPU 온도",      true,  2, s => s.GpuTemp,   "0",     "°C",  Tone.Temp),
        new("GpuPower",  "GPU 전력",      true,  3, s => s.GpuPower,  "0",     "W"),
        new("GpuClock",  "GPU 클럭",      true,  4, s => s.GpuClock,  "0",     "MHz"),
        new("GpuVolt",   "GPU 전압",      true,  5, s => s.GpuVolt,   "0.000", "V",   Tone.Dim),
        new("VramClock", "VRAM 클럭",     true,  6, s => s.VramClock, "0",     "MHz", Tone.Dim, "VRAM "),
        new("GpuFan",    "GPU 팬 속도",   true,  7, s => s.GpuFan,    "0",     "RPM", Tone.Dim),
        new("CpuLoad",   "CPU 사용률",    false, 1, s => s.CpuLoad,   "0",     "%"),
        new("CpuTemp",   "CPU 온도",      false, 2, s => s.CpuTemp,   "0",     "°C",  Tone.Temp),
        new("CpuPower",  "CPU 전력",      false, 3, s => s.CpuPower,  "0.0",   "W"),
        new("CpuClock",  "CPU 클럭",      false, 4, s => s.CpuClock,  "0",     "MHz"),
        new("CpuVolt",   "CPU 전압",      false, 5, s => s.CpuVolt,   "0.000", "V",   Tone.Dim),
        new("DramClock", "DRAM 클럭",     false, 6, s => s.DramClock, "0",     "MHz", Tone.Dim, "DRAM "),
        new("CpuFan",    "CPU 쿨러 팬",   false, 7, s => s.CpuFan,    "0",     "RPM", Tone.Dim),
    ];
}

sealed class Sensors : IDisposable
{
    readonly Computer _pc = new() { IsCpuEnabled = true, IsGpuEnabled = true, IsMotherboardEnabled = true };
    readonly IHardware? _cpu, _gpu, _superIo;
    readonly float? _dramClock;
    readonly object _lock = new(); // LHM은 스레드 안전하지 않음

    /// <summary>CPU 쿨러로 쓸 메인보드 팬 센서 이름 (예: "Fan #1").</summary>
    public string CpuFanName { get; set; } = "Fan #1";

    public Sensors()
    {
        _pc.Open();
        _cpu = _pc.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
        _gpu = _pc.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.GpuNvidia)
            ?? _pc.Hardware.FirstOrDefault(h => h.HardwareType is HardwareType.GpuAmd or HardwareType.GpuIntel);
        _superIo = _pc.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Motherboard)?
            .SubHardware.FirstOrDefault(h => h.HardwareType == HardwareType.SuperIO);
        _dramClock = ReadDramClock();
    }

    /// <summary>DRAM은 실시간 클럭 센서가 없어 설정 속도(MT/s)를 읽어 실제 클럭(÷2)으로 표시.</summary>
    static float? ReadDramClock()
    {
        try
        {
            using var q = new System.Management.ManagementObjectSearcher("SELECT ConfiguredClockSpeed FROM Win32_PhysicalMemory");
            var mts = q.Get().Cast<System.Management.ManagementBaseObject>()
                .Select(o => Convert.ToSingle(o["ConfiguredClockSpeed"] ?? 0)).DefaultIfEmpty(0).Max();
            return mts > 0 ? mts / 2 : null;
        }
        catch { return null; }
    }

    /// <summary>설정 창에서 CPU 쿨러 팬을 고를 수 있도록 현재 팬 이름과 RPM.</summary>
    public IReadOnlyList<(string Name, float? Rpm)> MotherboardFans()
    {
        lock (_lock) _superIo?.Update();
        return _superIo?.Sensors.Where(s => s.SensorType == SensorType.Fan).Select(s => (s.Name, s.Value)).ToList()
            ?? [];
    }

    static float? Find(IHardware? h, SensorType t, string name) =>
        h?.Sensors.FirstOrDefault(s => s.SensorType == t && s.Name == name)?.Value;

    static float? First(IHardware? h, SensorType t) =>
        h?.Sensors.FirstOrDefault(s => s.SensorType == t)?.Value;

    public Snapshot Read()
    {
        lock (_lock) return ReadCore();
    }

    Snapshot ReadCore()
    {
        _cpu?.Update();
        _gpu?.Update();
        _superIo?.Update();

        var coreClocks = _cpu?.Sensors
            .Where(s => s.SensorType == SensorType.Clock && s.Name.Contains("Core") && s.Value is > 0)
            .Select(s => s.Value!.Value).ToList();

        return new Snapshot(
            GpuClock: Find(_gpu, SensorType.Clock, "GPU Core"),
            GpuPower: Find(_gpu, SensorType.Power, "GPU Package") ?? First(_gpu, SensorType.Power),
            GpuTemp: Find(_gpu, SensorType.Temperature, "GPU Core"),
            GpuVolt: First(_gpu, SensorType.Voltage),
            VramClock: Find(_gpu, SensorType.Clock, "GPU Memory"),
            GpuLoad: Find(_gpu, SensorType.Load, "GPU Core"),
            GpuFan: First(_gpu, SensorType.Fan),
            CpuClock: coreClocks is { Count: > 0 } ? coreClocks.Average() : null,
            CpuPower: Find(_cpu, SensorType.Power, "CPU Package"),
            CpuTemp: Find(_cpu, SensorType.Temperature, "CPU Package") ?? Find(_cpu, SensorType.Temperature, "Core Max"),
            CpuLoad: Find(_cpu, SensorType.Load, "CPU Total"),
            CpuVolt: Find(_cpu, SensorType.Voltage, "CPU Core") ?? First(_cpu, SensorType.Voltage),
            DramClock: _dramClock,
            CpuFan: Find(_superIo, SensorType.Fan, CpuFanName));
    }

    public void Dispose() => _pc.Close();
}
