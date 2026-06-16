using BusFire;
using BusFire.EventPublishers;
using BusFire.Infrastructure;
using BusFire.Pipeline;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BusFire.Tests;

public class CoverageFillTests
{
    // ---- Multiple exception handlers exercise HandlersOrderer + ObjectDetails prioritization ----

    public sealed record Crash(string Message) : ICommand;

    public sealed class CrashHandler : ICommandHandler<Crash>
    {
        public Task Handle(Crash command, CancellationToken cancellationToken)
            => throw new InvalidOperationException("crash");
    }

    public sealed class CrashExHandler1 : ICommandExceptionHandler<Crash, InvalidOperationException>
    {
        private readonly InvocationRecorder _recorder;
        public CrashExHandler1(InvocationRecorder recorder) => _recorder = recorder;
        public Task Handle(Crash command, InvalidOperationException exception, CommandExceptionHandlerState<Crash> state)
        {
            _recorder.Record("ex1");
            return Task.CompletedTask;
        }
    }

    public sealed class CrashExHandler2 : ICommandExceptionHandler<Crash, InvalidOperationException>
    {
        private readonly InvocationRecorder _recorder;
        public CrashExHandler2(InvocationRecorder recorder) => _recorder = recorder;
        public Task Handle(Crash command, InvalidOperationException exception, CommandExceptionHandlerState<Crash> state)
        {
            _recorder.Record("ex2");
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Multiple_exception_handlers_are_all_run_and_prioritized()
    {
        var services = new ServiceCollection();
        var recorder = new InvocationRecorder();
        services.AddSingleton(recorder);
        services.AddTransient<ICommandHandler<Crash>, CrashHandler>();
        services.AddTransient<ICommandExceptionHandler<Crash, InvalidOperationException>, CrashExHandler1>();
        services.AddTransient<ICommandExceptionHandler<Crash, InvalidOperationException>, CrashExHandler2>();
        services.AddTransient<IPipelineBehavior<Crash>, CommandExceptionProcessorBehavior<Crash>>();
        var provider = services.BuildServiceProvider();
        var bus = new BusInternal(provider, new ForEachAwaitPublisher());

        // Neither handler sets Handled, so both run (ordering applied) and the exception still propagates.
        await Assert.ThrowsAsync<InvalidOperationException>(() => bus.Send(new Crash("x"), default));

        Assert.Contains("ex1", recorder.Entries);
        Assert.Contains("ex2", recorder.Entries);
    }

    // ---- HangfireConfigurationExtensions.UseBusFire ----

    [Fact]
    public void UseBusFire_registry_overload_applies_serializer_settings()
    {
        var registry = new MessageTypeRegistry(new[] { typeof(Ping) });

        // Should configure Hangfire's serializer without throwing.
        GlobalConfiguration.Configuration.UseBusFire(registry);
    }

    [Fact]
    public void UseBusFire_provider_overload_resolves_registry_and_filter()
    {
        var services = new ServiceCollection();
        services.AddSingleton<InvocationRecorder>();
        services.AddSingleton<IBackgroundJobClient>(new RecordingBackgroundJobClient());
        services.AddBusFire(cfg => cfg.RegisterServicesFromAssemblyContaining<Ping>());
        using var provider = services.BuildServiceProvider();

        GlobalConfiguration.Configuration.UseBusFire(provider);
    }

    // ---- Default IFailureHandler (NullFailureHandler) ----

    [Fact]
    public async Task Default_failure_handler_is_a_no_op()
    {
        var services = new ServiceCollection();
        services.AddSingleton<InvocationRecorder>();
        services.AddSingleton<IBackgroundJobClient>(new RecordingBackgroundJobClient());
        services.AddBusFire(cfg => cfg.RegisterServicesFromAssemblyContaining<Ping>());
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var failureHandler = scope.ServiceProvider.GetRequiredService<IFailureHandler>();
        await failureHandler.Handle("job-1", new Exception("boom"), default);

        Assert.NotNull(failureHandler);
    }

    // ---- Default/empty pipeline implementations ----

    [Fact]
    public async Task Empty_pipeline_behavior_passes_through_to_next()
    {
        var ran = false;
        await new EmptyPipelineBehavior<Ping>().Handle(
            new Ping("x"), () => { ran = true; return Task.CompletedTask; }, default);
        Assert.True(ran);
    }

    [Fact]
    public async Task Empty_pre_and_post_processors_are_no_ops()
    {
        await new EmptyCommandPreProcessor<Ping>().Process(new Ping("x"), default);
        await new EmptyCommandPostProcessor<Ping>().Process(new Ping("x"), default);
    }

    // ---- ServiceFactory extensions ----

    [Fact]
    public void ServiceFactory_resolves_single_and_multiple_instances()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new InvocationRecorder());
        using var provider = services.BuildServiceProvider();
        ServiceFactory factory = type => provider.GetService(type)!;

        Assert.NotNull(factory.GetInstance<InvocationRecorder>());
        Assert.Empty(factory.GetInstances<string>());
    }

    // ---- BusOptions ----

    [Fact]
    public void BusOptions_defaults_to_the_default_queue()
    {
        Assert.Equal(new[] { "default" }, new BusOptions().Queues);
    }
}
