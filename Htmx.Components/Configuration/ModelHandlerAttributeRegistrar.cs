using System.Linq.Expressions;
using System.Reflection;
using FastExpressionCompiler;
using Htmx.Components.Attributes;
using Htmx.Components.Models;
using Htmx.Components.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Htmx.Components.Configuration;

public static class ModelHandlerAttributeRegistrar
{
    private static readonly List<HandlerRegistration> _registrations = new();

    public static void RegisterAll(IModelRegistry registry)
    {
        // Ensure we only scan controllers once to avoid performance issues
        if (_registrations.Count == 0)
            ScanControllers();

        foreach (var reg in _registrations)
            reg.RegisterWithRegistry(registry);
    }

    private static void ScanControllers()
    {
        var controllerTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(Microsoft.AspNetCore.Mvc.Controller).IsAssignableFrom(t) && !t.IsAbstract);

        foreach (var (controllerType, configMethod, modelConfigAttribute) in controllerTypes
            .SelectMany(t => t.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                .Select(m => (ControllerType: t, ConfigMethod: m, ModelConfigAttribute: m.GetCustomAttribute<ModelConfigAttribute>()))
                .Where(x => x.ConfigMethod != null && x.ModelConfigAttribute != null)))
        {
            var configParam = configMethod.GetParameters().FirstOrDefault();
            if (configParam == null || !configParam.ParameterType.IsGenericType)
                continue;

            var builderType = configParam.ParameterType;
            var modelType = builderType.GetGenericArguments()[0];
            var keyType = builderType.GetGenericArguments()[1];

            // Compile delegates using FastExpressionCompiler
            var controllerParam = Expression.Parameter(typeof(object), "controller");
            var argParam = Expression.Parameter(typeof(object), "arg");

            _registrations.Add(new HandlerRegistration
            {
                TypeId = modelConfigAttribute!.ModelTypeId,
                ModelType = modelType,
                KeyType = keyType,
                ControllerType = controllerType,
                ConfigMethod = configMethod!,
            });
        }
    }

    private class HandlerRegistration
    {
        public string TypeId = null!;
        public Type ModelType = null!;
        public Type KeyType = null!;
        public Type ControllerType = null!;
        public MethodInfo ConfigMethod = null!;

        public void RegisterWithRegistry(IModelRegistry registry)
        {
            var registerMethod = typeof(IModelRegistry).GetMethod("Register")
                ?.MakeGenericMethod(ModelType, KeyType);

            registerMethod?.Invoke(registry,
            [
                TypeId,
                (Action<IServiceProvider, object>)((sp, builder) =>
                {
                    var controller = ActivatorUtilities.CreateInstance(sp, ControllerType);

                    // Call config method
                    ConfigMethod.Invoke(controller, new object[] { builder });

                    // Use reflection to get strongly-typed builder methods
                    var builderType = builder.GetType();

                })
            ]);
        }
    }
}