using FluentAssertions;
using WinVitals.App.ViewModels;
using WinVitals.Core.Entities;
using WinVitals.Core.Storage;
using Xunit;

namespace WinVitals.Tests;

public class OnboardingFlowTests
{
    private static (SettingsStore store, string dataDir) CreateStore()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wv-onb-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var db = new LiteDbStore(Path.Combine(dir, "test.db"));
        var store = new SettingsStore(db);
        return (store, dir);
    }

    [Fact]
    public void Step_Navigation_Works()
    {
        var (store, dir) = CreateStore();
        try
        {
            var vm = new OnboardingViewModel(store, null!, null!, () => { });
            vm.CurrentStep.Should().Be(0);
            vm.NextCommand.Execute(null);
            vm.CurrentStep.Should().Be(1);
            vm.BackCommand.Execute(null);
            vm.CurrentStep.Should().Be(0);
            vm.FinishCommand.Execute(null);
            store.Get().OnboardingCompleted.Should().BeTrue();
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Skip_Scan_Skips_Step()
    {
        var (store, dir) = CreateStore();
        try
        {
            var vm = new OnboardingViewModel(store, null!, null!, () => { });
            vm.CurrentStep = 3;
            vm.DoSkipScanCommand.Execute(null);
            vm.CurrentStep.Should().Be(4);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
