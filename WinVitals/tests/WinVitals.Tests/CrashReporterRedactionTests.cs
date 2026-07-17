using FluentAssertions;
using WinVitals.App.Services;
using WinVitals.Core.Telemetry;
using Xunit;

namespace WinVitals.Tests;

public class CrashReporterRedactionTests
{
    [Fact]
    public void Redacts_User_Home_Path()
    {
        var input = @"failed at C:\Users\Anh\Documents\secret.docx";
        var output = CrashReporter.Redact(input);
        output.Should().Contain("<user>");
        output.Should().NotContain("Anh");
    }

    [Fact]
    public void Redacts_Absolute_Path()
    {
        var input = @"could not read D:\projects\hush\config.json";
        var output = CrashReporter.Redact(input);
        output.Should().Contain("<path>");
        output.Should().NotContain("hush");
    }

    [Fact]
    public void Redacts_Email()
    {
        var input = "sent by user anh@example.com in message";
        var output = CrashReporter.Redact(input);
        output.Should().Contain("<email>");
        output.Should().NotContain("anh@example.com");
    }

    [Fact]
    public void Empty_String_Passes_Through()
    {
        CrashReporter.Redact("").Should().Be("");
    }

    [Fact]
    public async Task SaveCrashDump_Writes_Redacted_Json()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wv-crash-" + Guid.NewGuid().ToString("N"));
        try
        {
            var reporter = new CrashReporter(new NullTelemetry(), null!, dir);
            var ex = new InvalidOperationException(@"boom in C:\Users\Anh\file.txt from anh@x.com");
            var path = await reporter.SaveCrashDumpAsync(ex);

            File.Exists(path).Should().BeTrue();
            var json = await File.ReadAllTextAsync(path);
            json.Should().NotContain("Anh");
            // Email might be redacted or JSON-escaped
            json.ToLowerInvariant().Should().NotContain("anh@x.com");
            // The redacted message should contain the user placeholder
            json.Should().Contain("user");
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
