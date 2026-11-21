using System.Text.Json.Nodes;

using FlickerVoid.Editor.Assets.Data;
using FlickerVoid.Editor.Core.Inspector;

namespace FlickerVoid.Editor.Assets.Tests.Data;

public sealed class TableViewModelTests : IDisposable
{
    private readonly TableViewModelTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void ShowsARowPerEntryWithItsFields()
    {
        TableViewModel table = _builder.Build();

        table.Rows.Select(static row => row.Name).Should().Equal("Red", "White");
        NumberFieldViewModel year = (NumberFieldViewModel)table.Rows[1].Form.Fields.Single();
        year.Value.Should().Be(2004);
    }

    [Fact]
    public void HoldsEditedRowsUntilSaved()
    {
        TableViewModel table = _builder.Build();
        NumberFieldViewModel year = (NumberFieldViewModel)table.Rows[0].Form.Fields.Single();

        year.Value = 2001;

        _builder.Content.IsUnsaved(TableViewModelTestsBuilder.Wines).Should().BeTrue();
        JsonNode saved = JsonNode.Parse(_builder.Save())!;
        saved["rows"]!["Red"]!["year"]!.GetValue<int>().Should().Be(2001);
        saved["$schema"]!.GetValue<string>().Should().Be(".schemas/table.schema.json");
    }

    [Fact]
    public void AddsRenamesAndRemovesRows()
    {
        TableViewModel table = _builder.Build();

        table.AddRow();
        table.AddRow();
        table.Rows[2].Name = "Rose";
        table.Rows[3].Name = "Red";
        table.RemoveRow(table.Rows[0]);

        table.Rows.Select(static row => row.Name).Should().Equal("White", "Rose", "Row 2");
        JsonObject rows = JsonNode.Parse(_builder.Save())!["rows"]!.AsObject();
        rows.Select(static row => row.Key).Should().Equal("White", "Rose", "Row 2");
    }

    [Fact]
    public void ExportsAndImportsCsv()
    {
        TableViewModel table = _builder.Build();

        string csv = table.ExportCsv();
        table.ImportCsv("name,year\nTawny,\"1987\"\n\"Port, old\",1960\n");

        csv.Should().Be("name,year\nRed,1998\nWhite,2004\n");
        table.Rows.Select(static row => row.Name).Should().Equal("Tawny", "Port, old");
        JsonNode saved = JsonNode.Parse(_builder.Save())!;
        saved["rows"]!["Port, old"]!["year"]!.GetValue<int>().Should().Be(1960);
    }

    [Fact]
    public void RefusesCsvWithAnUnknownColumnAndKeepsTheRows()
    {
        TableViewModel table = _builder.Build();

        Action import = () => table.ImportCsv("name,vintage\nRed,1\n");

        import.Should().Throw<InvalidDataException>().WithMessage("*vintage*");
        table.Rows.Should().HaveCount(2);
    }
}
