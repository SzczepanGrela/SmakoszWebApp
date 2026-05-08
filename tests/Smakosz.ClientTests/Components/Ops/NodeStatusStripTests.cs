using Smakosz.ClientTests.Common;
using Smakosz.Client.Ops.Components;
using Smakosz.Client.Models;
using Smakosz.Client.Services;

namespace Smakosz.ClientTests.Components.Ops;

public class NodeStatusStripTests : BunitTestBase
{
    private static AdminSystemNodesResponseDto BuildResponse(bool gpuOnline = false) =>
        new()
        {
            Nodes = new List<AdminSystemNodeDto>
            {
                new() { NodeId = "vps-hetzner-prod", NodeType = "Api", Status = "online", Hostname = "hetznerVPS", IpAddress = "100.64.0.5", LastHeartbeat = DateTime.UtcNow.AddSeconds(-30) },
                new() { NodeId = "rbpi-gateway", NodeType = "RbpiGateway", Status = "online", Hostname = "raspberry-pi", IpAddress = "100.64.0.10", LastHeartbeat = DateTime.UtcNow.AddSeconds(-30) },
                new() { NodeId = "gpu-homelab", NodeType = "Gpu", Status = gpuOnline ? "online" : "offline", Hostname = "homelab", IpAddress = "100.64.0.20", GpuName = "GTX 1060", GpuMemoryTotal = 6144, GpuMemoryUsed = 1024, LastHeartbeat = DateTime.UtcNow.AddMinutes(-5) }
            },
            StaleThresholdDays = 7
        };

    private void StubAdminService(AdminSystemNodesResponseDto response)
    {
        var admin = Services.GetRequiredService<IAdminService>();
        admin.GetSystemNodesAsync().Returns(response);
    }

    [Fact]
    public void Renders_ThreePills_WithNodeIds()
    {
        StubAdminService(BuildResponse());

        var cut = RenderComponent<NodeStatusStrip>();
        cut.WaitForState(() => cut.FindAll(".node-pill-wrap").Count == 3);

        var pills = cut.FindAll(".node-pill-wrap");
        pills.Count.Should().Be(3);
        cut.Markup.Should().Contain("vps-hetzner-prod");
        cut.Markup.Should().Contain("rbpi-gateway");
        cut.Markup.Should().Contain("gpu-homelab");
    }

    [Fact]
    public void GpuOffline_WakeButton_Enabled()
    {
        StubAdminService(BuildResponse(gpuOnline: false));

        var cut = RenderComponent<NodeStatusStrip>();
        cut.WaitForState(() => cut.FindAll("button.btn-warning").Count == 1);

        var wakeBtn = cut.Find("button.btn-warning");
        wakeBtn.HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public void GpuOnline_WakeButton_Disabled()
    {
        StubAdminService(BuildResponse(gpuOnline: true));

        var cut = RenderComponent<NodeStatusStrip>();
        cut.WaitForState(() => cut.FindAll("button.btn-warning").Count == 1);

        var wakeBtn = cut.Find("button.btn-warning");
        wakeBtn.HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void ClickPill_TogglesPopover()
    {
        StubAdminService(BuildResponse());

        var cut = RenderComponent<NodeStatusStrip>();
        cut.WaitForState(() => cut.FindAll(".node-pill-wrap").Count == 3);

        cut.FindAll(".node-pill-popover").Should().BeEmpty();
        var gpuPillBtn = cut.FindAll(".node-pill-wrap")[2].QuerySelector("button.btn-outline-secondary")!;
        gpuPillBtn.Click();

        var popover = cut.Find(".node-pill-popover");
        popover.TextContent.Should().Contain("GTX 1060");
        popover.TextContent.Should().Contain("homelab");
    }
}
