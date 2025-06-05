using System.Linq.Expressions;
using System.Reflection;
using FastExpressionCompiler;
using Htmx.Components.Attributes;
using Htmx.Components.Models;
using Htmx.Components.Services;

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

        foreach (var controllerType in controllerTypes)
        {
            var configMethod = controllerType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(m => m.GetCustomAttribute<ModelConfigAttribute>() != null);

            if (configMethod == null)
                continue;

            var configParam = configMethod.GetParameters().FirstOrDefault();
            if (configParam == null || !configParam.ParameterType.IsGenericType)
                continue;

            var builderType = configParam.ParameterType;
            var modelType = builderType.GetGenericArguments()[0];
            var keyType = builderType.GetGenericArguments()[1];
            var typeId = modelType.Name;

            // Find CRUD methods
            var methods = controllerType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance);
            var createMethod = methods.FirstOrDefault(m => m.GetCustomAttribute<ModelCreateAttribute>() != null);
            var readMethod = methods.FirstOrDefault(m => m.GetCustomAttribute<ModelReadAttribute>() != null);
            var updateMethod = methods.FirstOrDefault(m => m.GetCustomAttribute<ModelUpdateAttribute>() != null);
            var deleteMethod = methods.FirstOrDefault(m => m.GetCustomAttribute<ModelDeleteAttribute>() != null);

            // Compile delegates using FastExpressionCompiler
            var controllerParam = Expression.Parameter(typeof(object), "controller");
            var argParam = Expression.Parameter(typeof(object), "arg");

            Func<object, object, Task<Result>>? createDelegate = null;
            if (createMethod != null)
            {
                var call = Expression.Call(
                    Expression.Convert(controllerParam, controllerType),
                    createMethod,
                    Expression.Convert(argParam, modelType));
                var lambda = Expression.Lambda<Func<object, object, Task<Result>>>(call, controllerParam, argParam);
                createDelegate = lambda.CompileFast();
            }

            Func<object, IQueryable>? readDelegate = null;
            if (readMethod != null)
            {
                var call = Expression.Call(
                    Expression.Convert(controllerParam, controllerType),
                    readMethod);
                var lambda = Expression.Lambda<Func<object, IQueryable>>(call, controllerParam);
                readDelegate = lambda.CompileFast();
            }

            Func<object, object, Task<Result>>? updateDelegate = null;
            if (updateMethod != null)
            {
                var call = Expression.Call(
                    Expression.Convert(controllerParam, controllerType),
                    updateMethod,
                    Expression.Convert(argParam, modelType));
                var lambda = Expression.Lambda<Func<object, object, Task<Result>>>(call, controllerParam, argParam);
                updateDelegate = lambda.CompileFast();
            }

            Func<object, object, Task<Result>>? deleteDelegate = null;
            if (deleteMethod != null)
            {
                var keyParam = Expression.Parameter(typeof(object), "key");
                var call = Expression.Call(
                    Expression.Convert(controllerParam, controllerType),
                    deleteMethod,
                    Expression.Convert(keyParam, keyType));
                var lambda = Expression.Lambda<Func<object, object, Task<Result>>>(call, controllerParam, keyParam);
                deleteDelegate = lambda.CompileFast();
            }

            _registrations.Add(new HandlerRegistration
            {
                TypeId = typeId,
                ModelType = modelType,
                KeyType = keyType,
                ControllerType = controllerType,
                ConfigMethod = configMethod,
                CreateDelegate = createDelegate,
                ReadDelegate = readDelegate,
                UpdateDelegate = updateDelegate,
                DeleteDelegate = deleteDelegate
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
        public Func<object, object, Task<Result>>? CreateDelegate;
        public Func<object, IQueryable>? ReadDelegate;
        public Func<object, object, Task<Result>>? UpdateDelegate;
        public Func<object, object, Task<Result>>? DeleteDelegate;

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

                    // WithCreate
                    if (CreateDelegate != null)
                    {
                        var withCreateMethod = builderType.GetMethod("WithCreate");
                        var param = Expression.Parameter(ModelType, "entity");
                        var body = Expression.Call(
                            Expression.Constant(CreateDelegate),
                            CreateDelegate.GetType().GetMethod("Invoke")!,
                            Expression.Constant(controller),
                            Expression.Convert(param, typeof(object))
                        );
                        var lambda = Expression.Lambda(
                            typeof(Func<,>).MakeGenericType(ModelType, typeof(Task<Result>)),
                            body, param);
                        var typedDelegate = lambda.CompileFast();
                        withCreateMethod!.Invoke(builder, new object[] { typedDelegate });
                    }

                    // WithUpdate
                    if (UpdateDelegate != null)
                    {
                        var withUpdateMethod = builderType.GetMethod("WithUpdate");
                        var param = Expression.Parameter(ModelType, "entity");
                        var body = Expression.Call(
                            Expression.Constant(UpdateDelegate),
                            UpdateDelegate.GetType().GetMethod("Invoke")!,
                            Expression.Constant(controller),
                            Expression.Convert(param, typeof(object))
                        );
                        var lambda = Expression.Lambda(
                            typeof(Func<,>).MakeGenericType(ModelType, typeof(Task<Result>)),
                            body, param);
                        var typedDelegate = lambda.CompileFast();
                        withUpdateMethod!.Invoke(builder, new object[] { typedDelegate });
                    }

                    // WithDelete
                    if (DeleteDelegate != null)
                    {
                        var withDeleteMethod = builderType.GetMethod("WithDelete");
                        var param = Expression.Parameter(KeyType, "key");
                        var body = Expression.Call(
                            Expression.Constant(DeleteDelegate),
                            DeleteDelegate.GetType().GetMethod("Invoke")!,
                            Expression.Constant(controller),
                            Expression.Convert(param, typeof(object))
                        );
                        var lambda = Expression.Lambda(
                            typeof(Func<,>).MakeGenericType(KeyType, typeof(Task<Result>)),
                            body, param);
                        var typedDelegate = lambda.CompileFast();
                        withDeleteMethod!.Invoke(builder, new object[] { typedDelegate });
                    }

                    // WithQueryable
                    if (ReadDelegate != null)
                    {
                        var withQueryableMethod = builderType.GetMethod("WithQueryable");
                        var funcType = typeof(Func<>).MakeGenericType(typeof(IQueryable<>).MakeGenericType(ModelType));
                        var body = Expression.Convert(
                            Expression.Call(
                                Expression.Constant(ReadDelegate),
                                ReadDelegate.GetType().GetMethod("Invoke")!,
                                Expression.Constant(controller)
                            ),
                            typeof(IQueryable<>).MakeGenericType(ModelType)
                        );
                        var lambda = Expression.Lambda(funcType, body);
                        var typedDelegate = lambda.CompileFast();
                        withQueryableMethod!.Invoke(builder, new object[] { typedDelegate });
                    }
                })
            ]);
        }
    }
}