using FuelIntake.Core.Hosting;

namespace FuelIntake.Core.Tests;

public class DataFolderTests
{
    private const string Default = @"C:\Users\pexample\Documents\Client Files - Fuel 26-27";
    private const string Synced = @"C:\Users\pexample\Example Agency\Crisis Fuel - Documents\Client Files - Fuel 26-27";

    [Fact]
    public void Resolve_UsesSavedChoice_OrDefault()
    {
        Assert.Equal(Default, DataFolderRules.Resolve(null, Default));
        Assert.Equal(Default, DataFolderRules.Resolve("  ", Default));
        Assert.Equal(Synced, DataFolderRules.Resolve(Synced + @"\", Default));
    }

    [Fact]
    public void ToSaved_StoresNothingForTheDefault()
    {
        Assert.Null(DataFolderRules.ToSaved(null, Default));
        Assert.Null(DataFolderRules.ToSaved(Default.ToUpperInvariant() + @"\", Default));
        Assert.Equal(Synced, DataFolderRules.ToSaved(" " + Synced + " ", Default));
    }

    [Fact]
    public void IsInside_MatchesWholeFolderNames()
    {
        Assert.True(DataFolderRules.IsInside(Synced, @"C:\Users\pexample\Example Agency"));
        Assert.True(DataFolderRules.IsInside(@"c:\users\PEXAMPLE\example agency", @"C:\Users\pexample\Example Agency\"));
        Assert.False(DataFolderRules.IsInside(@"C:\Users\pexample\Example Agency 2\x", @"C:\Users\pexample\Example Agency"));
    }

    [Fact]
    public void Warning_SpotsSettingsAndClientFolders()
    {
        Assert.Null(DataFolderRules.Warning(Synced, hasIntakeFile: false));
        Assert.NotNull(DataFolderRules.Warning(Synced + @"\_Fuel Intake Settings", hasIntakeFile: false));
        Assert.NotNull(DataFolderRules.Warning(Synced + @"\_TEST Sampleton, Testy", hasIntakeFile: true));
    }

    [Fact]
    public void NotShared_WarnsForTheDefaultAndUnsyncedFolders()
    {
        Assert.NotNull(DataFolderRules.NotSharedReason(new(Default, Default, true, true, null)));
        // Documents backed up to the user's own OneDrive is still the default, so still only theirs
        Assert.NotNull(DataFolderRules.NotSharedReason(new(Default, Default, true, true, "OneDrive / SharePoint")));
        Assert.NotNull(DataFolderRules.NotSharedReason(new(@"D:\Clients", Default, false, true, null)));
        Assert.Null(DataFolderRules.NotSharedReason(new(Synced, Default, false, true, "OneDrive / SharePoint")));
        Assert.Null(DataFolderRules.NotSharedReason(new(Synced, Default, false, false, null)));   // missing: its own message
    }
}

public class DeleteClientTests
{
    [Theory]
    [InlineData("Sampleton", "Sampleton", true)]
    [InlineData("  sampleton ", "Sampleton", true)]
    [InlineData("Van  Example", "Van Example", true)]
    [InlineData("_TEST Sampleton", "Sampleton", true)]
    [InlineData("Sample", "Sampleton", false)]
    [InlineData("", "Sampleton", false)]
    [InlineData("", "", false)]
    [InlineData(null, null, false)]
    public void DeleteConfirmed_NeedsTheLastName(string? typed, string? last, bool ok) =>
        Assert.Equal(ok, FuelIntake.Core.Storage.ClientFolders.DeleteConfirmed(typed, last));
}
