using System;
using System.Linq;
using System.Reflection;
using AutomaticTypeMapper;

namespace EndlessClient
{
    public static class ManualRegistrar
    {
        public static void Register(object containerObj)
        {
            try
            {
                System.IO.File.AppendAllText("debug_log.txt", "Starting Manual Registration...\n");

                var containerType = containerObj.GetType();
                // Look for RegisterType(Type from, Type to, ITypeLifetimeManager lifetimeManager, params InjectionMember[] injectionMembers)
                // Note: Unity has many overloads. We want one that takes (Type, Type, ITypeLifetimeManager)
                // In Unity 5+, it's ITypeLifetimeManager. In older Unity, LifetimeManager.

                var containerRuntimeType = containerObj.GetType();
                System.IO.File.AppendAllText("debug_log.txt", $"Container Runtime Type: {containerRuntimeType.FullName}\n");

                // Inspect existing IGameInitializer registrations
                try
                {
                    var regsProp = containerRuntimeType.GetProperty("Registrations");
                    if (regsProp != null)
                    {
                        var regs = (System.Collections.Generic.IEnumerable<object>)regsProp.GetValue(containerObj);
                        System.IO.File.AppendAllText("debug_log.txt", "Existing IGameInitializer registrations:\n");
                        foreach (var r in regs)
                        {
                            var regTypeProp = r.GetType().GetProperty("RegisteredType");
                            var nameProp = r.GetType().GetProperty("Name");
                            var mapToProp = r.GetType().GetProperty("MappedToType");
                            var lifeProp = r.GetType().GetProperty("LifetimeManager");

                            var registeredType = (Type)regTypeProp.GetValue(r);
                            if (registeredType != null && registeredType.Name.Contains("IGameInitializer"))
                            {
                                var name = (string)nameProp.GetValue(r);
                                var mappedTo = (Type)mapToProp.GetValue(r);
                                var life = lifeProp.GetValue(r);
                                System.IO.File.AppendAllText("debug_log.txt", $" - Name: '{name}', MapTo: {mappedTo?.Name}, Life: {life?.GetType().Name}\n");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.IO.File.AppendAllText("debug_log.txt", $"Failed to inspect registrations: {ex}\n");
                }

                var methods = containerRuntimeType.GetMethods().Where(m => m.Name == "RegisterType").ToList();

                // Also check interfaces (explicit implementation or interface definition)
                foreach (var iface in containerRuntimeType.GetInterfaces())
                {
                    var ifaceMethods = iface.GetMethods().Where(m => m.Name == "RegisterType");
                    methods.AddRange(ifaceMethods);
                }

                MethodInfo registerTypeMethod = null;

                System.IO.File.AppendAllText("debug_log.txt", $"Found {methods.Count} RegisterType methods:\n");
                foreach (var m in methods)
                {
                    var p = m.GetParameters();
                    System.IO.File.AppendAllText("debug_log.txt", $" - {m.ToString()}\n");
                    System.IO.File.AppendAllText("debug_log.txt", $"   Params: {string.Join(", ", p.Select(pi => pi.ParameterType.Name))}\n");

                    // Try to match: Type from, Type to, LifetimeManager
                    // Or Type from, Type to, string name, LifetimeManager, InjectionMember[]

                    // We want to call it with (from, to, lifetime). 
                    // If we find (from, to, name, lifetime, members), we can pass null for name and members?
                }

                // Let's refine logical search after seeing output. For now, try to find ANY that fits.
                foreach (var m in methods)
                {
                    var p = m.GetParameters();
                    // Match: Type, Type, String, LifetimeManager, InjectionMember[]
                    if (p.Length == 5 && p[0].ParameterType == typeof(Type) && p[1].ParameterType == typeof(Type) && p[2].ParameterType == typeof(string) && p[3].ParameterType.Name.Contains("LifetimeManager"))
                    {
                        registerTypeMethod = m;
                        break;
                    }

                    // Match: Type, Type, LifetimeManager, InjectionMember[] (if name is missing?)
                    if (p.Length == 4 && p[0].ParameterType == typeof(Type) && p[1].ParameterType == typeof(Type) && p[2].ParameterType.Name.Contains("LifetimeManager"))
                    {
                        registerTypeMethod = m;
                        break;
                    }
                }

                if (registerTypeMethod == null)
                {
                    System.IO.File.AppendAllText("debug_log.txt", "Could not find compatible RegisterType method on container.\n");
                    return;
                }

                int paramCount = registerTypeMethod.GetParameters().Length;
                System.IO.File.AppendAllText("debug_log.txt", $"Selected RegisterType with {paramCount} params.\n");

                // Find LifetimeManagers
                Type singletonManagerType = null;
                Type transientManagerType = null;

                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    singletonManagerType = asm.GetType("Unity.Lifetime.ContainerControlledLifetimeManager");
                    transientManagerType = asm.GetType("Unity.Lifetime.TransientLifetimeManager");
                    if (singletonManagerType != null) break;
                }

                if (singletonManagerType == null)
                {
                    System.IO.File.AppendAllText("debug_log.txt", "Could not find Unity LifetimeManagers.\n");
                    // Try searching by name in all types
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try
                        {
                            foreach (var t in asm.GetTypes())
                            {
                                if (t.Name == "ContainerControlledLifetimeManager") singletonManagerType = t;
                                if (t.Name == "TransientLifetimeManager") transientManagerType = t;
                            }
                        }
                        catch { }
                        if (singletonManagerType != null && transientManagerType != null) break;
                    }
                }

                if (singletonManagerType == null)
                {
                    System.IO.File.AppendAllText("debug_log.txt", "FAILED to find LifetimeManager types. Aborting manual registration.\n");
                    return;
                }

                // Register List<IGameComponent> as an empty list instance to prevent auto-resolution failures
                try
                {
                    var registerInstanceMethod = containerRuntimeType.GetInterfaces()
                        .SelectMany(i => i.GetMethods())
                        .FirstOrDefault(m => m.Name == "RegisterInstance" && m.GetParameters().Length >= 2);

                    if (registerInstanceMethod != null)
                    {
                        var emptyList = new System.Collections.Generic.List<Microsoft.Xna.Framework.IGameComponent>();
                        var listType = typeof(System.Collections.Generic.List<Microsoft.Xna.Framework.IGameComponent>);
                        var singletonLife = Activator.CreateInstance(singletonManagerType);

                        // Try to call RegisterInstance(Type, name, object, LifetimeManager)
                        var instParams = registerInstanceMethod.GetParameters();
                        object[] instArgs;
                        if (instParams.Length == 4)
                            instArgs = new object[] { listType, null, emptyList, singletonLife };
                        else if (instParams.Length == 3)
                            instArgs = new object[] { listType, emptyList, singletonLife };
                        else
                            instArgs = new object[] { listType, emptyList };

                        registerInstanceMethod.Invoke(containerObj, instArgs);
                        System.IO.File.AppendAllText("debug_log.txt", "Registered List<IGameComponent> instance.\n");
                    }
                    else
                    {
                        System.IO.File.AppendAllText("debug_log.txt", "Could not find RegisterInstance method.\n");
                    }
                }
                catch (Exception ex)
                {
                    System.IO.File.AppendAllText("debug_log.txt", $"Failed to register List<IGameComponent>: {(ex.InnerException ?? ex).Message}\n");
                }

                var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => a.FullName.StartsWith("EndlessClient") || a.FullName.StartsWith("EOLib"));

                int regCount = 0;
                foreach (var assembly in assemblies)
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        var autoMaps = type.GetCustomAttributes<AutoMappedTypeAttribute>();
                        var maps = type.GetCustomAttributes<MappedTypeAttribute>();

                        foreach (var autoMap in autoMaps)
                        {
                            // Only map to interfaces for AutoMappedType
                            foreach (var i in type.GetInterfaces())
                            {
                                // Avoid IDisposable and System interfaces
                                if (i.Namespace != null && i.Namespace.StartsWith("System")) continue;

                                try
                                {
                                    var lifetimeManager = autoMap.IsSingleton ? Activator.CreateInstance(singletonManagerType) : Activator.CreateInstance(transientManagerType);
                                    object[] args;
                                    if (paramCount == 5) // (Type, Type, string, LifetimeManager, InjectionMember[])
                                        args = new object[] { i, type, null, lifetimeManager, null };
                                    else // (Type, Type, LifetimeManager, InjectionMember[])
                                        args = new object[] { i, type, lifetimeManager, null };

                                    registerTypeMethod.Invoke(containerObj, args);
                                    regCount++;
                                    if (i.Name == "IGameInitializer")
                                        System.IO.File.AppendAllText("debug_log.txt", $"REGISTERED: {type.Name} as IGameInitializer\n");
                                }
                                catch (Exception ex)
                                {
                                    System.IO.File.AppendAllText("debug_log.txt", $"Failed to register {type.Name} as {i.Name}: {(ex.InnerException ?? ex).Message}\n");
                                }
                            }

                            // Register self
                            try
                            {
                                var selfLifetime = autoMap.IsSingleton ? Activator.CreateInstance(singletonManagerType) : Activator.CreateInstance(transientManagerType);
                                object[] args;
                                if (paramCount == 5)
                                    args = new object[] { type, type, null, selfLifetime, null };
                                else
                                    args = new object[] { type, type, selfLifetime, null };

                                registerTypeMethod.Invoke(containerObj, args);
                            }
                            catch { }
                        }

                        foreach (var map in maps)
                        {
                            try
                            {
                                var lifetimeManager = map.IsSingleton ? Activator.CreateInstance(singletonManagerType) : Activator.CreateInstance(transientManagerType);
                                object[] args;
                                if (paramCount == 5)
                                    args = new object[] { map.BaseType, type, null, lifetimeManager, null };
                                else
                                    args = new object[] { map.BaseType, type, lifetimeManager, null };

                                registerTypeMethod.Invoke(containerObj, args);
                            }
                            catch (Exception ex)
                            {
                                System.IO.File.AppendAllText("debug_log.txt", $"Failed to register {type.Name} as {map.BaseType.Name}: {(ex.InnerException ?? ex).Message}\n");
                            }
                        }
                    }
                }
                System.IO.File.AppendAllText("debug_log.txt", $"Manual Registration Completed. Total: {regCount} registrations.\n");
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText("debug_log.txt", $"ManualRegistrar Critical Error: {ex}\n");
            }
        }
    }
}
