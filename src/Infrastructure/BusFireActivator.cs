using Hangfire.Annotations;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace BusFire.Infrastructure
{
    // Hangfire JobActivator integration — only meaningful inside a running Hangfire server, so excluded
    // from unit-test coverage (integration territory). Fully-qualified attribute to avoid colliding with
    // Hangfire.Annotations.NotNull imported above.
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal class AspNetCoreJobActivatorScope : JobActivatorScope
    {
        private readonly IServiceScope _serviceScope;
        private readonly string jobId;

        public AspNetCoreJobActivatorScope([NotNull] IServiceScope serviceScope, string jobId)
        {
            this.jobId = jobId;
            _serviceScope = serviceScope ?? throw new ArgumentNullException(nameof(serviceScope));
        }  

        public override object Resolve(Type type)
        {
            var res = ActivatorUtilities.GetServiceOrCreateInstance(_serviceScope.ServiceProvider, type);
            //if (res is IBackgroundJob backgroundJob)
            //    backgroundJob.JobID = jobId;
            return res;
        }

        public override void DisposeScope()
        {
            _serviceScope.Dispose();
        }
    }
    // This class injects the default DI container into hangfire jobs
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class BusFireActivator : JobActivator
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public BusFireActivator([NotNull] IServiceScopeFactory serviceScopeFactory)
        {
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        }

        public override JobActivatorScope BeginScope(JobActivatorContext context)
        {
            return new AspNetCoreJobActivatorScope(_serviceScopeFactory.CreateScope(), context.BackgroundJob.Id);
        }
    }
}
