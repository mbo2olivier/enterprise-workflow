using EnterpriseWorkflow.Cli;
using EnterpriseWorkflow.Extensions;
using EnterpriseWorkflow.Security;
using EnterpriseWorkflow.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EnterpriseWorkflow.Cli.Tests;

public sealed class CliBuilderTests
{
    [Fact]
    public async Task CommandReceivesFrozenConfigurationServicesAndArguments()
    {
        var builder = EnterpriseWorkflowCli.CreateBuilder(["policy", "--customer", "123"]);
        builder.Configuration["Instance"] = "example";
        builder.Services.AddSingleton(new Marker("compiled"));
        builder.AddCommand("policy", "Check policy", context =>
        {
            Assert.Equal("example", context.Configuration["Instance"]);
            Assert.Equal("compiled", context.Services.GetRequiredService<Marker>().Value);
            Assert.Equal("--customer", context.Arguments[0]);
            Assert.Equal("123", context.Arguments[1]);
            return Task.FromResult(17);
        }, new WorkflowCliOption("--customer", true));
        var app = builder.Build();
        builder.Configuration["Instance"] = "changed";
        Assert.Throws<InvalidOperationException>(() => builder.UseCommandName("late"));
        Assert.Equal(17, await app.RunAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CollisionsAndReservedCommandsAreRejectedAndReplacementIsExplicit()
    {
        var builder = EnterpriseWorkflowCli.CreateBuilder(["inspect"]);
        Assert.Throws<InvalidOperationException>(() => builder.AddCommand("inspect", "collision", _ => Task.FromResult(0)));
        Assert.Throws<ArgumentException>(() => builder.AddCommand("__host", "reserved", _ => Task.FromResult(0)));
        Assert.Throws<ArgumentException>(() => builder.AddCommand("custom", "duplicate", _ => Task.FromResult(0), new("--x"), new("--x")));
        builder.ReplaceCommand("inspect", "Explicit replacement", _ => Task.FromResult(23));
        Assert.Equal(23, await builder.Build().RunAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("--unknown")]
    [InlineData("--customer")]
    public async Task InvalidOptionsDoNotInvokeTheCommand(string argument)
    {
        var called = false;
        var app = EnterpriseWorkflowCli.CreateBuilder(["check", argument]).AddCommand("check", "Check", _ =>
        { called = true; return Task.FromResult(0); }, new WorkflowCliOption("--customer", true)).Build();
        Assert.Equal(1, await app.RunAsync(TestContext.Current.CancellationToken));
        Assert.False(called);
    }

    [Fact]
    public void DevelopmentCompositionCannotReplaceSecurityButCanAddBusinessServices()
    {
        using var execution = WorkflowCliExecution.Enter([(context, services) =>
        {
            Assert.Equal("test", context.Configuration["Endpoint"]);
            services.AddSingleton(new Marker("business"));
        }], [options => options.RuntimeId = "Example.Workflow"], TestContext.Current.CancellationToken);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Endpoint"] = "test" }).Build();
        var services = new ServiceCollection();
        WorkflowCliExecution.ConfigureDevelopment(configuration, services);
        Assert.Contains(services, item => item.ServiceType == typeof(Marker));
        var options = new WorkflowModuleLoadOptions(); WorkflowCliExecution.ConfigureModules(options);
        Assert.Equal("Example.Workflow", options.RuntimeId);
        using var forbidden = WorkflowCliExecution.Enter([(_, collection) => collection.AddSingleton(new SecondFactorOptions())], [], TestContext.Current.CancellationToken);
        Assert.Throws<InvalidOperationException>(() => WorkflowCliExecution.ConfigureDevelopment(configuration, services));
    }

    [Fact]
    public void TypedHttpClientCanBeAddedToWebDevelopmentServices()
    {
        var services = new ServiceCollection();
        services.AddLogging(); services.AddEnterpriseWorkflowWeb();
        using var execution = WorkflowCliExecution.Enter([(_, collection) => collection.AddHttpClient<ITestApi, TestApi>()], [], TestContext.Current.CancellationToken);
        WorkflowCliExecution.ConfigureDevelopment(new ConfigurationBuilder().Build(), services);
        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<ITestApi>().Client);
    }
    private interface ITestApi { HttpClient Client { get; } }
    private sealed record TestApi(HttpClient Client) : ITestApi;

    [Fact]
    public void AuthenticationOptionsAndExistingBusinessServicesRemainProtected()
    {
        var services = new ServiceCollection(); services.AddEnterpriseWorkflowWeb();
        var configuration = new ConfigurationBuilder().Build();
        using (WorkflowCliExecution.Enter([(_, collection) => collection.Configure<Microsoft.AspNetCore.Authentication.AuthenticationOptions>(options =>
            options.DefaultAuthenticateScheme = "external")], [], TestContext.Current.CancellationToken))
            Assert.Throws<InvalidOperationException>(() => WorkflowCliExecution.ConfigureDevelopment(configuration, services));
        var original = new ServiceCollection(); original.AddSingleton(new Marker("original"));
        using (WorkflowCliExecution.Enter([(_, collection) => collection.AddSingleton(new Marker("override"))], [], TestContext.Current.CancellationToken))
            Assert.Throws<InvalidOperationException>(() => WorkflowCliExecution.ConfigureDevelopment(configuration, original));
    }

    [Fact]
    public async Task SubcommandsApplyDefaultsWithoutOverridingExplicitArguments()
    {
        var app = EnterpriseWorkflowCli.CreateBuilder(["bank", "check", "--customer", "explicit"])
            .AddCommand("bank check", "Check", context =>
            {
                Assert.Equal("explicit", context.Arguments[1]);
                Assert.Contains("--verbose", context.Arguments);
                Assert.DoesNotContain("default", context.Arguments);
                return Task.FromResult(0);
            }, new WorkflowCliOption("--customer", true), new WorkflowCliOption("--verbose"))
            .ConfigureCommandDefaults("bank check", "--customer", "default", "--verbose").Build();
        Assert.Equal(0, await app.RunAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CancellationIsAConventionalExitCode()
    {
        using var source = new CancellationTokenSource(); source.Cancel();
        Assert.Equal(130, await EnterpriseWorkflowCli.CreateBuilder([]).Build().RunAsync(source.Token));
    }
    private sealed record Marker(string Value);
}
