using FluentAssertions;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using NSubstitute;
using Smakosz.Client.Services;

namespace Smakosz.ClientTests.Services;

public class ScrollPositionServiceTests
{
    private static ScrollPositionService Create(IJSRuntime js) => new(js);

    [Fact]
    public async Task SaveAndRestore_ReturnsTrueAndCallsJsWithSavedY()
    {
        var js = Substitute.For<IJSRuntime>();
        js.InvokeAsync<double>("smakoszScroll.getY", Arg.Any<object?[]?>())
            .Returns(new ValueTask<double>(1234.5));
        var service = Create(js);

        await service.SaveCurrentPositionAsync("/dishes");
        var restored = await service.TryRestorePositionAsync("/dishes");

        restored.Should().BeTrue();
        await js.Received(1).InvokeAsync<IJSVoidResult>(
            "smakoszScroll.setY",
            Arg.Is<object?[]?>(args => args != null && args.Length == 1 && (double)args[0]! == 1234.5));
    }

    [Fact]
    public async Task TryRestore_UnknownUri_ReturnsFalseWithoutJsCall()
    {
        var js = Substitute.For<IJSRuntime>();
        var service = Create(js);

        var restored = await service.TryRestorePositionAsync("/never-visited");

        restored.Should().BeFalse();
        await js.DidNotReceive().InvokeAsync<IJSVoidResult>(
            "smakoszScroll.setY",
            Arg.Any<object?[]?>());
    }

    [Fact]
    public async Task Save_SecondCallSameUri_OverwritesValue()
    {
        var js = Substitute.For<IJSRuntime>();
        var sequence = new Queue<double>(new[] { 100.0, 500.0 });
        js.InvokeAsync<double>("smakoszScroll.getY", Arg.Any<object?[]?>())
            .Returns(_ => new ValueTask<double>(sequence.Dequeue()));
        var service = Create(js);

        await service.SaveCurrentPositionAsync("/restaurants");
        await service.SaveCurrentPositionAsync("/restaurants");
        await service.TryRestorePositionAsync("/restaurants");

        await js.Received(1).InvokeAsync<IJSVoidResult>(
            "smakoszScroll.setY",
            Arg.Is<object?[]?>(args => args != null && (double)args[0]! == 500.0));
    }

    [Fact]
    public async Task ScrollToElement_CallsJsWithElementId()
    {
        var js = Substitute.For<IJSRuntime>();
        var service = Create(js);

        await service.ScrollToElementAsync("search-results-anchor");

        await js.Received(1).InvokeAsync<IJSVoidResult>(
            "smakoszScroll.scrollToElement",
            Arg.Is<object?[]?>(args => args != null && (string)args[0]! == "search-results-anchor"));
    }
}
