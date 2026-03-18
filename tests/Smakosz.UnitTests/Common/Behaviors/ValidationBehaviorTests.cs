using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using NSubstitute;
using Smakosz.Application.Common.Behaviors;

namespace Smakosz.UnitTests.Common.Behaviors;

public record ValidationTestRequest(string Name) : IRequest<string>;

[Trait("Category", "Behaviors")]
public class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_NoValidators_CallsNext()
    {
        var validators = Enumerable.Empty<IValidator<ValidationTestRequest>>();
        var behavior = new ValidationBehavior<ValidationTestRequest, string>(validators);
        var request = new ValidationTestRequest("test");
        var next = Substitute.For<RequestHandlerDelegate<string>>();
        next().Returns("result");

        var result = await behavior.Handle(request, next, CancellationToken.None);

        result.Should().Be("result");
        await next.Received(1)();
    }

    [Fact]
    public async Task Handle_ValidationPasses_CallsNext()
    {
        var validator = Substitute.For<IValidator<ValidationTestRequest>>();
        validator.ValidateAsync(Arg.Any<ValidationContext<ValidationTestRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
        var behavior = new ValidationBehavior<ValidationTestRequest, string>(new[] { validator });
        var next = Substitute.For<RequestHandlerDelegate<string>>();
        next().Returns("result");

        var result = await behavior.Handle(new ValidationTestRequest("test"), next, CancellationToken.None);

        result.Should().Be("result");
        await next.Received(1)();
    }

    [Fact]
    public async Task Handle_ValidationFails_ThrowsValidationException()
    {
        var validator = Substitute.For<IValidator<ValidationTestRequest>>();
        validator.ValidateAsync(Arg.Any<ValidationContext<ValidationTestRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(new[] { new ValidationFailure("Name", "Name is required") }));
        var behavior = new ValidationBehavior<ValidationTestRequest, string>(new[] { validator });
        var next = Substitute.For<RequestHandlerDelegate<string>>();

        var act = () => behavior.Handle(new ValidationTestRequest(""), next, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .Where(e => e.Errors.Any(f => f.PropertyName == "Name"));
        await next.DidNotReceive()();
    }

    [Fact]
    public async Task Handle_MultipleValidatorsWithFailures_AggregatesErrors()
    {
        var validator1 = Substitute.For<IValidator<ValidationTestRequest>>();
        validator1.ValidateAsync(Arg.Any<ValidationContext<ValidationTestRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(new[] { new ValidationFailure("Name", "Error 1") }));

        var validator2 = Substitute.For<IValidator<ValidationTestRequest>>();
        validator2.ValidateAsync(Arg.Any<ValidationContext<ValidationTestRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(new[] { new ValidationFailure("Name", "Error 2") }));

        var behavior = new ValidationBehavior<ValidationTestRequest, string>(new[] { validator1, validator2 });
        var next = Substitute.For<RequestHandlerDelegate<string>>();

        var act = () => behavior.Handle(new ValidationTestRequest(""), next, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().HaveCount(2);
    }
}
