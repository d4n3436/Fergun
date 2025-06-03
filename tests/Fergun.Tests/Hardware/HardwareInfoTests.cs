using System;
using System.Threading.Tasks;
using Fergun.Hardware;
using Xunit;

namespace Fergun.Tests.Hardware;

public class HardwareInfoTests
{
    [Fact]
    public void HardwareInfo_Instance_Is_Of_Expected_Type()
    {
        if (OperatingSystem.IsWindows())
            Assert.IsType<WindowsHardwareInfo>(HardwareInfo.Instance);
        else if (OperatingSystem.IsLinux())
            Assert.IsType<LinuxHardwareInfo>(HardwareInfo.Instance);
        else if (OperatingSystem.IsMacOS())
            Assert.IsType<MacOsHardwareInfo>(HardwareInfo.Instance);
        else
            Assert.IsType<UnknownHardwareInfo>(HardwareInfo.Instance);
    }

    [Fact]
    public void HardwareInfo_GetCpuName_Returns_Expected_Value()
    {
        string? cpuName = HardwareInfo.GetCpuName();

        if (HardwareInfo.Instance is UnknownHardwareInfo)
        {
            Assert.Null(cpuName);
        }
        else
        {
            Assert.NotNull(cpuName);
            Assert.NotEmpty(cpuName);
        }
    }

    [Fact]
    public void HardwareInfo_GetOperatingSystemName_Is_Not_Null()
    {
        string osName = HardwareInfo.GetOperatingSystemName();

        Assert.NotNull(osName);
        Assert.NotEmpty(osName);
    }

    [Fact]
    public void HardwareInfo_GetMemoryStatus_Has_Valid_Values()
    {
        var memoryStatus = HardwareInfo.GetMemoryStatus();

        if (HardwareInfo.Instance is not UnknownHardwareInfo)
        {
            Assert.True(memoryStatus.TotalPhysicalMemory > 0);
            if (HardwareInfo.Instance is not MacOsHardwareInfo)
            {
                Assert.True(memoryStatus.AvailablePhysicalMemory > 0);
                Assert.Equal(memoryStatus.TotalPhysicalMemory - memoryStatus.AvailablePhysicalMemory, memoryStatus.UsedPhysicalMemory);
            }
        }

        Assert.True(memoryStatus.ProcessUsedMemory > 0);
    }

    [Fact]
    public async Task HardwareInfo_GetCpuUsageAsync_Value_Is_In_Range()
    {
        double value = await HardwareInfo.GetCpuUsageAsync();

        Assert.InRange(value, 0, 1);
    }
}