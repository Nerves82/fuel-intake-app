using FuelIntake.Core.Hosting;

namespace FuelIntake.Core.Tests;

public class HostCheckTests
{
    [Fact]
    public void WritableFolder_Passes_AndLeavesNoFile()
    {
        var dir = Directory.CreateTempSubdirectory("fuelintake-test").FullName;
        try
        {
            var result = HostCheck.CanWriteTo("temp", dir);

            Assert.True(result.Passed, result.Detail);
            Assert.Empty(Directory.GetFiles(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void MissingFolder_Fails_WithMessage()
    {
        var missing = Path.Combine(Path.GetTempPath(), "fuelintake-does-not-exist-" + Guid.NewGuid());

        var result = HostCheck.CanWriteTo("missing", missing);

        Assert.False(result.Passed);
        Assert.Contains("not found", result.Detail);
    }
}
