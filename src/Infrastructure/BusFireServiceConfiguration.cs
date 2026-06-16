//using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Reflection;
using BusFire.EventPublishers;
using Hangfire;
using System.ComponentModel;

namespace BusFire.Infrastructure
{
    public class BusFireGlobalConfiguration
    {
        public static BusFireServiceConfiguration Configuration { get; } = new BusFireServiceConfiguration();
    }

    public class BusFireServiceConfiguration
	{
		public Func<Type, bool> TypeEvaluator { get; set; } = t => true;
		public Type BusImplementationType { get; set; } = typeof(Bus);
        /// <summary>
        /// Strategy for publishing notifications. Defaults to <see cref="ForeachAwaitPublisher"/>
        /// </summary>
        public IEventPublisher EventPublisher { get; set; } = new ForEachAwaitPublisher();

        public IFailureHandler FailureHandler { get; set; } = new NullFailureHandler();
        /// <summary>
        /// Type of notification publisher strategy to register. If set, overrides <see cref="EventPublisher"/>
        /// </summary>
        public Type? EventPublisherType { get; set; }
        public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Transient;
        public CommandExceptionActionProcessorStrategy CommandExceptionActionProcessorStrategy { get; set; } = CommandExceptionActionProcessorStrategy.ApplyForUnhandledExceptions;

        internal List<Assembly> AssembliesToRegister { get; } = new();

        public List<ServiceDescriptor> BehaviorsToRegister { get; } = new();

        public BusFireServiceConfiguration RegisterServicesFromAssemblyContaining<T>()
            => RegisterServicesFromAssemblyContaining(typeof(T));

        public BusFireServiceConfiguration RegisterServicesFromAssemblyContaining(Type type)
            => RegisterServicesFromAssembly(type.Assembly);

        public BusFireServiceConfiguration RegisterServicesFromAssembly(Assembly assembly)
        {
            AssembliesToRegister.Add(assembly);

            return this;
        }

        public BusFireServiceConfiguration RegisterServicesFromAssemblies(
            params Assembly[] assemblies)
        {
            AssembliesToRegister.AddRange(assemblies);

            return this;
        }

        public BusFireServiceConfiguration AddBehavior<TServiceType, TImplementationType>(
            ServiceLifetime serviceLifetime = ServiceLifetime.Transient) =>
            AddBehavior(typeof(TServiceType), typeof(TImplementationType), serviceLifetime);

        public BusFireServiceConfiguration AddBehavior(
            Type serviceType,
            Type implementationType,
            ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        {
            BehaviorsToRegister.Add(new ServiceDescriptor(serviceType, implementationType, serviceLifetime));

            return this;
        }

        public BusFireServiceConfiguration AddOpenBehavior(Type openBehaviorType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        {
            var serviceType = typeof(IPipelineBehavior<>);

            BehaviorsToRegister.Add(new ServiceDescriptor(serviceType, openBehaviorType, serviceLifetime));

            return this;
        }
    }
}
