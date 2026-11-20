using FuelIntake.Core.Model;
using FuelIntake.Core.Packets;
using FuelIntake.Core.Storage;

namespace FuelIntake.Core.Tests;

public class PacketAndStorageTests
{
    [Fact]
    public void ApplicationPack_AddsFuelOrderOrPledge()
    {
        Assert.Equal([FormId.Application, FormId.Access, FormId.IncomeExpense, FormId.FuelOrder],
            PacketCatalog.FormsFor(PacketCatalog.ApplicationPack, "Low on Fuel"));
        Assert.Equal([FormId.Application, FormId.Access, FormId.IncomeExpense, FormId.ElectricPledge],
            PacketCatalog.FormsFor(PacketCatalog.ApplicationPack, "Disconnect Notice"));
    }

    [Fact]
    public void StaffOnlyPacks_AreMarked()
    {
        Assert.True(PacketCatalog.CoverAndGoEngage.StaffOnly);
        Assert.False(PacketCatalog.IntakePackage.StaffOnly);
    }

    [Fact]
    public void FileNames()
    {
        Assert.Equal("T_Sampleton_fuel_assistance_11_20_26",
            PacketCatalog.ExpandFileName(PacketCatalog.ApplicationPack.FileNamePattern, "Testy", "Sampleton", new DateOnly(2026, 11, 20)));
        Assert.Equal("Sampleton, Testy", ClientFolders.FolderName("Testy", "Sampleton"));
        Assert.Equal("OBrien, Al", ClientFolders.FolderName("Al", "O\"Brien"));
    }

    [Fact]
    public void Json_RoundTrips_AndKeepsLeadingZeros()
    {
        var r = FakeIntake.Family();
        var json = IntakeJson.Serialize(r);
        var back = IntakeJson.Deserialize(json);

        Assert.Equal("000123456", back.Household[0].Ssn);
        Assert.Equal("05819", back.Client.Zip);
        Assert.Equal(new DateOnly(2026, 11, 16), back.Staff.AppDate);
        Assert.Equal(900m, back.IncomeExpenses.Expenses["Rent"].PaidLast30Days);
        Assert.Equal(IntakeJson.Serialize(back), json);
    }

    [Fact]
    public void Json_RejectsNewerFormat()
    {
        var json = IntakeJson.Serialize(new IntakeRecord()).Replace("\"formatVersion\": 1", "\"formatVersion\": 99");
        Assert.Throws<IntakeFormatException>(() => IntakeJson.Deserialize(json));
    }

    [Fact]
    public void Json_RejectsGarbage() => Assert.Throws<IntakeFormatException>(() => IntakeJson.Deserialize("not json"));
}

public class TemplateNameTests
{
    [Fact]
    public void TemplateCopy_MatchesOldScript() =>
        Assert.Equal("T_Sampleton_FA (pg. 2.3-2.6) - Consent Forms_11_20_26.pdf",
            ClientFolders.TemplateCopyName("FA (pg. 2.3-2.6) - Consent Forms.pdf", "Testy", "Sampleton", new DateOnly(2026, 11, 20)));
}
