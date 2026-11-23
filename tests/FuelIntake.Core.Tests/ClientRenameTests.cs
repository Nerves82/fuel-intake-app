using FuelIntake.Core.Storage;

namespace FuelIntake.Core.Tests;

public class ClientRenameTests
{
    [Fact]
    public void ParseFolderName_ReadsLastFirst_AndTestPrefix()
    {
        Assert.Equal(("Jane", "Smyth", false), ClientRename.ParseFolderName("Smyth, Jane")!.Value);
        Assert.Equal(("Testy", "Sampleton", true), ClientRename.ParseFolderName("_TEST Sampleton, Testy")!.Value);
        Assert.Equal(("Mary Ann", "Van Example", false), ClientRename.ParseFolderName("Van Example, Mary Ann")!.Value);
        Assert.Null(ClientRename.ParseFolderName("Just a folder"));
        Assert.Null(ClientRename.ParseFolderName("Smyth, "));
    }

    [Fact]
    public void Plan_RenamesFolderAndAppFiles()
    {
        string[] files =
        [
            "intake.json",
            "J_Smyth_FA (pg. 2.3-2.6) - Consent Forms_11_20_26.pdf",
            "J_Smyth_signed_intake_11_20_26.pdf",
            "Smyth Jane - letter from landlord.pdf",
            "electric bill.pdf",
        ];
        var plan = ClientRename.Plan("Smyth, Jane", "Jane", "Smith", files)!;
        Assert.Equal("Smith, Jane", plan.FolderTo);
        Assert.Equal(
        [
            new FileRename("J_Smyth_FA (pg. 2.3-2.6) - Consent Forms_11_20_26.pdf", "J_Smith_FA (pg. 2.3-2.6) - Consent Forms_11_20_26.pdf"),
            new FileRename("J_Smyth_signed_intake_11_20_26.pdf", "J_Smith_signed_intake_11_20_26.pdf"),
            new FileRename("Smyth Jane - letter from landlord.pdf", "Smith Jane - letter from landlord.pdf"),
        ], plan.Files);
    }

    [Fact]
    public void Plan_FirstNameChange_UpdatesInitial_AndKeepsTestPrefix()
    {
        var plan = ClientRename.Plan("_TEST Sampleton, Testi", "Testy", "Sampleton",
            ["T_Sampleton_all_forms_11_20_26.pdf", "Testi notes.txt"])!;
        Assert.Equal("_TEST Sampleton, Testy", plan.FolderTo);
        Assert.Equal([new FileRename("Testi notes.txt", "Testy notes.txt")], plan.Files); // initial T stays T

        var plan2 = ClientRename.Plan("Smith, Jon", "Ron", "Smith", ["J_Smith_cover_sheet_11_20_26.pdf"])!;
        Assert.Equal([new FileRename("J_Smith_cover_sheet_11_20_26.pdf", "R_Smith_cover_sheet_11_20_26.pdf")], plan2.Files);
    }

    [Fact]
    public void RenameFile_OnlyWholeNames()
    {
        // "Al" must not change "all_forms"; "Lee" must not change "Leeway"
        Assert.Equal("B_Lee_all_forms.pdf", ClientRename.RenameFile("A_Lee_all_forms.pdf", "Al", "Lee", "Bo", "Lee"));
        Assert.Null(ClientRename.RenameFile("Leeway notes.pdf", "Al", "Lee", "Al", "Li"));
        Assert.Equal("A_Li_notes.pdf", ClientRename.RenameFile("A_Lee_notes.pdf", "Al", "Lee", "Al", "Li"));
    }

    [Fact]
    public void Plan_NeverOverwritesAnotherFile()
    {
        var plan = ClientRename.Plan("Smyth, Jane", "Jane", "Smith", ["J_Smyth_notes.pdf", "J_Smith_notes.pdf"])!;
        Assert.Equal([new FileRename("J_Smyth_notes.pdf", "J_Smith_notes (2).pdf")], plan.Files);
    }

    [Fact]
    public void Plan_SwapNames_DoesNotReplaceTwice()
    {
        // first and last were entered the wrong way round
        var plan = ClientRename.Plan("Jane, Smith", "Jane", "Smith", ["S_Jane_intake.pdf"])!;
        Assert.Equal("Smith, Jane", plan.FolderTo);
        Assert.Equal([new FileRename("S_Jane_intake.pdf", "J_Smith_intake.pdf")], plan.Files);
    }
}
