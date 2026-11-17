using CouchPad.Platform.Windows;

namespace CouchPad.Core.Tests;

public sealed class GpuCounterFilterTests
{
    private const string ForegroundInstance = "pid_5360_luid_0x0000e695_phys_0_eng_0_engtype_3D";

    [Theory]
    [InlineData("pid_5360_luid_0x0000e695_phys_0_eng_0_engtype_3D")]
    [InlineData("pid_5360_luid_0x0000d1c4_phys_0_eng_1_engtype_3d")]
    public void Matches_foreground_process_3d_engine(string instanceName)
    {
        Assert.True(GpuCounterFilter.IsProcess3D(instanceName, 5360));
    }

    [Fact]
    public void Ignores_other_process()
    {
        Assert.False(GpuCounterFilter.IsProcess3D(
            "pid_5160_luid_0x0000e695_phys_0_eng_0_engtype_3D", 5360));
    }

    [Fact]
    public void Ignores_non_3d_engine_of_the_same_process()
    {
        Assert.False(GpuCounterFilter.IsProcess3D(
            "pid_5360_luid_0x0000e695_phys_0_eng_0_engtype_Copy", 5360));
    }

    [Fact]
    public void Ignores_process_id_sharing_the_prefix()
    {
        Assert.False(GpuCounterFilter.IsProcess3D(
            "pid_53600_luid_0x0000e695_phys_0_eng_0_engtype_3D", 5360));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\\GPU Engine(*)\\Utilization Percentage")]
    public void Ignores_missing_or_unrelated_instance_names(string? instanceName)
    {
        Assert.False(GpuCounterFilter.IsProcess3D(instanceName, 5360));
    }

    [Fact]
    public void Ignores_invalid_process_ids()
    {
        Assert.False(GpuCounterFilter.IsProcess3D(ForegroundInstance, 0));
        Assert.False(GpuCounterFilter.IsProcess3D(ForegroundInstance, -1));
    }
}
