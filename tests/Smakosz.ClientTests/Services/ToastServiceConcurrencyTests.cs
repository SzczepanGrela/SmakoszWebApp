using FluentAssertions;
using NSubstitute;
using Smakosz.Client.Services;

namespace Smakosz.ClientTests.Services;

public class ToastServiceConcurrencyTests
{
    [Fact]
    public void ShowError_WhenConcurrencyModalOpen_SkipsToast()
    {
        var concurrency = Substitute.For<IConcurrencyConflictService>();
        concurrency.IsOpen.Returns(true);
        var service = new ToastService(concurrency);
        var raised = 0;
        service.OnShow += _ => raised++;

        service.ShowError("Operacja nieudana");

        raised.Should().Be(0);
    }

    [Fact]
    public void ShowError_WhenConcurrencyModalClosed_ShowsToast()
    {
        var concurrency = Substitute.For<IConcurrencyConflictService>();
        concurrency.IsOpen.Returns(false);
        var service = new ToastService(concurrency);
        var raised = 0;
        service.OnShow += _ => raised++;

        service.ShowError("Operacja nieudana");

        raised.Should().Be(1);
    }

    [Fact]
    public void ShowSuccess_AlwaysFires_RegardlessOfConcurrencyState()
    {
        var concurrency = Substitute.For<IConcurrencyConflictService>();
        concurrency.IsOpen.Returns(true);
        var service = new ToastService(concurrency);
        var raised = 0;
        service.OnShow += _ => raised++;

        service.ShowSuccess("Zapisano");

        raised.Should().Be(1);
    }

    [Fact]
    public void Constructor_WithoutConcurrencyService_StillWorks()
    {
        var service = new ToastService();
        var raised = 0;
        service.OnShow += _ => raised++;

        service.ShowError("Error");

        raised.Should().Be(1);
    }
}
