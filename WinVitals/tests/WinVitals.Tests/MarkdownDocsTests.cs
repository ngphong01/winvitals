using FluentAssertions;
using NSubstitute;
using WinVitals.App.Services;
using WinVitals.App.ViewModels;
using Xunit;

namespace WinVitals.Tests;

public class MarkdownDocsTests
{
    [Fact]
    public void HelpViewModel_Loads_Toc()
    {
        var loc = Substitute.For<ILocalizationService>();
        loc.CurrentCulture.Returns("en");
        var vm = new HelpViewModel(loc);
        vm.TableOfContents.Should().NotBeEmpty();
        vm.TableOfContents.Select(d => d.Key).Should().Contain("getting-started");
    }

    [Fact]
    public void Selecting_Item_Does_Not_Throw()
    {
        var loc = Substitute.For<ILocalizationService>();
        loc.CurrentCulture.Returns("en");
        var vm = new HelpViewModel(loc);
        vm.SelectedItem = vm.TableOfContents[0];
    }
}
