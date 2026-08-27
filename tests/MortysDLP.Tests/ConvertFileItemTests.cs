using MortysDLP.Models;
using System.ComponentModel;

namespace MortysDLP.Tests;

public class ConvertFileItemTests
{
    [Fact]
    public void Status_Aendern_LoestPropertyChangedAus()
    {
        var item = new ConvertFileItem();
        var raised = new List<string?>();
        item.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        item.Status = "Konvertiere...";

        Assert.Contains(nameof(ConvertFileItem.Status), raised);
    }

    [Fact]
    public void Progress_Aendern_LoestPropertyChangedAus()
    {
        var item = new ConvertFileItem();
        var raised = new List<string?>();
        item.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        item.Progress = 42.5;

        Assert.Contains(nameof(ConvertFileItem.Progress), raised);
    }

    [Fact]
    public void SourcePath_Aendern_LoestPropertyChangedFuerSourcePathUndNameAus()
    {
        var item = new ConvertFileItem();
        var raised = new List<string?>();
        item.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        item.SourcePath = @"C:\Videos\urlaub.mp4";

        Assert.Contains(nameof(ConvertFileItem.SourcePath), raised);
        Assert.Contains(nameof(ConvertFileItem.Name), raised);
    }

    [Fact]
    public void Name_LeitetSichAusSourcePathAb()
    {
        var item = new ConvertFileItem { SourcePath = @"C:\Videos\urlaub.mp4" };

        Assert.Equal("urlaub.mp4", item.Name);
    }

    [Fact]
    public void Status_StandardwertIstBereit()
    {
        var item = new ConvertFileItem();

        Assert.Equal("Bereit", item.Status);
    }
}
