using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using CouchPad.Core.Performance;

namespace CouchPad.Platform.Windows;

/// <summary>Read-only Windows performance sampler for the local HUD.</summary>
public sealed class WindowsPerformanceSampler : IPerformanceSampler, IDisposable
{
    private const uint PdhFmtDouble = 0x00000200;
    private const uint PdhMoreData = 0x800007D2;
    private const int SmCxScreen = 0;
    private const int SmCyScreen = 1;
    private const int VRefresh = 116;

    private readonly object _sync = new();
    private ulong _lastIdle;
    private ulong _lastKernel;
    private ulong _lastUser;
    private DateTimeOffset _lastCpuAt;
    private int _lastProcessId = -1;
    private TimeSpan _lastProcessCpu;
    private DateTimeOffset _lastProcessAt;
    private IntPtr _gpuQuery;
    private IntPtr _gpuCounter;
    private IntPtr _vramQuery;
    private IntPtr _vramCounter;
    private bool _disposed;

    public WindowsPerformanceSampler()
    {
        TryInitializePdh();
    }

    public Task<PerformanceSnapshot> SampleAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Sample());
    }

    private PerformanceSnapshot Sample()
    {
        lock (_sync)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(WindowsPerformanceSampler));
            var now = DateTimeOffset.UtcNow;
            var cpu = ReadCpu(now);
            var process = ReadProcess(now);
            var memory = ReadMemory();
            var gpu = ReadGpu();
            var vram = ReadVram();
            var display = ReadDisplay();
            return new PerformanceSnapshot(
                cpu.Total,
                process.Cpu,
                gpu,
                memory.Used,
                memory.Total,
                vram,
                null,
                process.Name,
                display.Width,
                display.Height,
                display.Refresh,
                now);
        }
    }

    private (double? Total, double? Process) ReadCpu(DateTimeOffset now)
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user)) return (null, null);
        var idleValue = ToUInt64(idle);
        var kernelValue = ToUInt64(kernel);
        var userValue = ToUInt64(user);
        if (_lastCpuAt == default)
        {
            _lastIdle = idleValue;
            _lastKernel = kernelValue;
            _lastUser = userValue;
            _lastCpuAt = now;
            return (null, null);
        }

        var totalDelta = (kernelValue - _lastKernel) + (userValue - _lastUser);
        var idleDelta = idleValue - _lastIdle;
        _lastIdle = idleValue;
        _lastKernel = kernelValue;
        _lastUser = userValue;
        _lastCpuAt = now;
        var percent = totalDelta == 0
            ? (double?)null
            : Math.Clamp((double)(totalDelta - Math.Min(idleDelta, totalDelta)) / totalDelta * 100, 0, 100);
        return (percent, null);
    }

    private (double? Cpu, string? Name) ReadProcess(DateTimeOffset now)
    {
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero)
        {
            _lastProcessId = -1;
            return (null, null);
        }

        _ = GetWindowThreadProcessId(window, out var processId);
        if (processId == 0) return (null, null);
        try
        {
            var processId32 = (int)processId;
            using var process = Process.GetProcessById(processId32);
            var name = process.ProcessName;
            var cpuTime = process.TotalProcessorTime;
            double? percent = null;
            if (_lastProcessId == processId32 && _lastProcessAt != default)
            {
                var elapsed = now - _lastProcessAt;
                if (elapsed > TimeSpan.Zero)
                    percent = Math.Clamp(cpuTime.Subtract(_lastProcessCpu).TotalMilliseconds /
                        elapsed.TotalMilliseconds / Environment.ProcessorCount * 100, 0, 100);
            }
            _lastProcessId = processId32;
            _lastProcessCpu = cpuTime;
            _lastProcessAt = now;
            return (percent, name);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception or NotSupportedException)
        {
            return (null, null);
        }
    }

    private static (ulong? Used, ulong? Total) ReadMemory()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status)) return (null, null);
        return (status.TotalPhys - status.AvailPhys, status.TotalPhys);
    }

    private double? ReadGpu()
    {
        var values = ReadCounter(_gpuQuery, _gpuCounter)
            .Where(item => item.Name.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Value)
            .Where(value => double.IsFinite(value))
            .ToArray();
        if (values.Length == 0)
        {
            values = ReadCounter(_gpuQuery, _gpuCounter)
                .Select(item => item.Value)
                .Where(value => double.IsFinite(value))
                .ToArray();
        }
        return values.Length == 0 ? null : Math.Clamp(values.Max(), 0, 100);
    }

    private ulong? ReadVram()
    {
        var values = ReadCounter(_vramQuery, _vramCounter)
            .Select(item => item.Value)
            .Where(value => double.IsFinite(value) && value >= 0)
            .ToArray();
        return values.Length == 0 ? null : (ulong)Math.Min(values.Max(), (double)ulong.MaxValue);
    }

    private static (int? Width, int? Height, int? Refresh) ReadDisplay()
    {
        var width = GetSystemMetrics(SmCxScreen);
        var height = GetSystemMetrics(SmCyScreen);
        var dc = GetDC(IntPtr.Zero);
        int? refresh = null;
        if (dc != IntPtr.Zero)
        {
            try { refresh = GetDeviceCaps(dc, VRefresh); }
            finally { _ = ReleaseDC(IntPtr.Zero, dc); }
        }
        return (width > 0 ? width : null, height > 0 ? height : null, refresh is > 0 ? refresh : null);
    }

    private void TryInitializePdh()
    {
        try
        {
            if (PdhOpenQuery(null, 0, out _gpuQuery) == 0)
                if (PdhAddEnglishCounter(_gpuQuery, @"\GPU Engine(*)\Utilization Percentage", 0, out _gpuCounter) != 0)
                    _gpuCounter = IntPtr.Zero;
            if (PdhOpenQuery(null, 0, out _vramQuery) == 0)
                if (PdhAddEnglishCounter(_vramQuery, @"\GPU Adapter Memory(*)\Dedicated Usage", 0, out _vramCounter) != 0)
                    _vramCounter = IntPtr.Zero;
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }

    private static IReadOnlyList<(string Name, double Value)> ReadCounter(IntPtr query, IntPtr counter)
    {
        if (query == IntPtr.Zero || counter == IntPtr.Zero) return [];
        if (PdhCollectQueryData(query) != 0) return [];
        uint size = 0;
        uint count = 0;
        var status = PdhGetFormattedCounterArray(counter, PdhFmtDouble, ref size, ref count, IntPtr.Zero);
        if (status != PdhMoreData || size == 0 || count == 0) return [];
        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            if (PdhGetFormattedCounterArray(counter, PdhFmtDouble, ref size, ref count, buffer) != 0)
                return [];
            var itemSize = Marshal.SizeOf<PdhFmtCounterValueItem>();
            var result = new List<(string Name, double Value)>((int)count);
            for (var index = 0; index < count; index++)
            {
                var item = Marshal.PtrToStructure<PdhFmtCounterValueItem>(buffer + index * itemSize);
                var name = Marshal.PtrToStringUni(item.Name) ?? "";
                result.Add((name, item.Value.DoubleValue));
            }
            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static ulong ToUInt64(FileTime time) => ((ulong)time.High << 32) | time.Low;

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            if (_gpuQuery != IntPtr.Zero) _ = PdhCloseQuery(_gpuQuery);
            if (_vramQuery != IntPtr.Zero) _ = PdhCloseQuery(_vramQuery);
            _gpuQuery = _vramQuery = IntPtr.Zero;
            _gpuCounter = _vramCounter = IntPtr.Zero;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint Low;
        public uint High;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PdhFmtCounterValueItem
    {
        public IntPtr Name;
        public PdhFmtCounterValue Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PdhFmtCounterValue
    {
        public uint CStatus;
        public double DoubleValue;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr window);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr window, IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern int GetDeviceCaps(IntPtr dc, int index);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhOpenQuery(string? dataSource, nuint userData, out IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhAddEnglishCounter(IntPtr query, string path, nuint userData, out IntPtr counter);

    [DllImport("pdh.dll")]
    private static extern uint PdhCollectQueryData(IntPtr query);

    [DllImport("pdh.dll")]
    private static extern uint PdhGetFormattedCounterArray(
        IntPtr counter, uint format, ref uint bufferSize, ref uint itemCount, IntPtr buffer);

    [DllImport("pdh.dll")]
    private static extern uint PdhCloseQuery(IntPtr query);
}
