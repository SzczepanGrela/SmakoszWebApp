using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Components;

public class StepWizardTests : BunitTestBase
{
    private static List<string> TestSteps => ["Krok 1", "Krok 2", "Krok 3"];

    [Fact]
    public void RendersAllSteps()
    {
        var cut = RenderComponent<StepWizard>(p => p
            .Add(c => c.Steps, TestSteps)
            .Add(c => c.CurrentStep, 0));

        cut.Markup.Should().Contain("Krok 1");
        cut.Markup.Should().Contain("Krok 2");
        cut.Markup.Should().Contain("Krok 3");
    }

    [Fact]
    public void FirstStep_PrevDisabled()
    {
        var cut = RenderComponent<StepWizard>(p => p
            .Add(c => c.Steps, TestSteps)
            .Add(c => c.CurrentStep, 0));

        var prevBtn = cut.FindAll("button").First(b => b.TextContent.Contains("Wstecz"));
        prevBtn.HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void MiddleStep_ShowsDalej()
    {
        var cut = RenderComponent<StepWizard>(p => p
            .Add(c => c.Steps, TestSteps)
            .Add(c => c.CurrentStep, 1));

        cut.FindAll("button").Should().Contain(b => b.TextContent.Contains("Dalej"));
    }

    [Fact]
    public void LastStep_ShowsZakoncz()
    {
        var cut = RenderComponent<StepWizard>(p => p
            .Add(c => c.Steps, TestSteps)
            .Add(c => c.CurrentStep, 2));

        cut.FindAll("button").Should().Contain(b => b.TextContent.Contains("Zakoncz"));
    }

    [Fact]
    public void ClickNext_InvokesCurrentStepChanged()
    {
        int? newStep = null;
        var cut = RenderComponent<StepWizard>(p => p
            .Add(c => c.Steps, TestSteps)
            .Add(c => c.CurrentStep, 0)
            .Add(c => c.CurrentStepChanged, (int s) => newStep = s));

        var nextBtn = cut.FindAll("button").First(b => b.TextContent.Contains("Dalej"));
        nextBtn.Click();

        newStep.Should().Be(1);
    }

    [Fact]
    public void ClickPrev_InvokesCurrentStepChanged()
    {
        int? newStep = null;
        var cut = RenderComponent<StepWizard>(p => p
            .Add(c => c.Steps, TestSteps)
            .Add(c => c.CurrentStep, 2)
            .Add(c => c.CurrentStepChanged, (int s) => newStep = s));

        var prevBtn = cut.FindAll("button").First(b => b.TextContent.Contains("Wstecz"));
        prevBtn.Click();

        newStep.Should().Be(1);
    }

    [Fact]
    public void ClickFinish_InvokesOnFinish()
    {
        var finished = false;
        var cut = RenderComponent<StepWizard>(p => p
            .Add(c => c.Steps, TestSteps)
            .Add(c => c.CurrentStep, 2)
            .Add(c => c.OnFinish, () => finished = true));

        var finishBtn = cut.FindAll("button").First(b => b.TextContent.Contains("Zakoncz"));
        finishBtn.Click();

        finished.Should().BeTrue();
    }

    [Fact]
    public void RendersChildContent()
    {
        var cut = RenderComponent<StepWizard>(p => p
            .Add(c => c.Steps, TestSteps)
            .Add(c => c.CurrentStep, 0)
            .AddChildContent("<div class='step-content'>Step 1 content</div>"));

        cut.Markup.Should().Contain("Step 1 content");
    }

    [Fact]
    public void CompletedStep_ShowsCheckmark()
    {
        var cut = RenderComponent<StepWizard>(p => p
            .Add(c => c.Steps, TestSteps)
            .Add(c => c.CurrentStep, 2));

        cut.FindAll("i.fa-solid.fa-check").Should().HaveCount(3);
    }
}
