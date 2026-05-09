using FluentAssertions;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using Smakosz.Client.Services;

namespace Smakosz.ClientTests.Services;

public class ConcurrencyConflictServiceTests
{
    private static ConcurrencyConflictService CreateService(out NavigationManager nav)
    {
        nav = Substitute.For<NavigationManager>();
        return new ConcurrencyConflictService(nav);
    }

    [Fact]
    public void Show_SetsIsOpenAndRaisesStateChanged()
    {
        var service = CreateService(out _);
        var raised = 0;
        service.StateChanged += () => raised++;

        service.Show();

        service.IsOpen.Should().BeTrue();
        raised.Should().Be(1);
    }

    [Fact]
    public void Show_WhenAlreadyOpen_DoesNotRaiseAgain()
    {
        var service = CreateService(out _);
        service.Show();
        var raised = 0;
        service.StateChanged += () => raised++;

        service.Show();

        raised.Should().Be(0);
    }

    [Fact]
    public void Dismiss_ClosesAndRaisesStateChanged()
    {
        var service = CreateService(out _);
        service.Show();
        var raised = 0;
        service.StateChanged += () => raised++;

        service.Dismiss();

        service.IsOpen.Should().BeFalse();
        raised.Should().Be(1);
    }
}
