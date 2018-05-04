namespace CoreExtentions.DI.ServiceConfigurators
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using CoreExtentions.DI.ServiceConfigurators.Exceptions;
    using Microsoft.Extensions.DependencyInjection;

    public static class DIConfigurationLocator
    {
        private const string ConfiguratorClassConventionSuffix = "Configurator";
        private const string ConfigureMethodName = "Configure";

        public static IServiceCollection InstallConfigurators(this IServiceCollection serviceCollection, IEnumerable<Assembly> assemblies, Predicate<Type> typeMatch)
        {
            foreach (var assembly in assemblies)
            {
                var types = assembly.GetTypes().Where(t => t.IsClass && t.IsPublic && typeMatch(t));

                foreach (var installerType in types)
                {
                    LoadConfigurator(installerType, serviceCollection);
                }
            }

            return serviceCollection;
        }

        public static IServiceCollection InstallConfigurators(this IServiceCollection serviceCollection, Predicate<AssemblyName> assemblyMatch, Predicate<Type> typeMatch, Assembly rootAssembly = null)
        {
            if (rootAssembly == null)
            {
                rootAssembly = Assembly.GetCallingAssembly();
            }

            var assemblies = new HashSet<Assembly> { rootAssembly };

            AddReferencedAssemblyTree(rootAssembly, assemblyMatch, assemblies);

            if (assemblies.Count() < 1)
            {
                throw new ConfiguratorException("There were no assemblies found that match the predicate provided.");
            }

            serviceCollection.InstallConfigurators(assemblies, typeMatch);

            return serviceCollection;
        }

        private static void AddReferencedAssemblyTree(Assembly parentAssembly, Predicate<AssemblyName> assemblyMatch, HashSet<Assembly> assemblies)
        {
            var newlyDiscoveredAssemblies = new List<Assembly>();

            var newlyDiscoveredAssemblyNames = parentAssembly.GetReferencedAssemblies().Where(a => assemblyMatch(a));

            foreach (var assemblyName in newlyDiscoveredAssemblyNames)
            {
                var assembly = Assembly.Load(assemblyName);

                newlyDiscoveredAssemblies.Add(assembly);
                assemblies.Add(assembly);
            }

            foreach (var assembly in newlyDiscoveredAssemblies)
            {
                AddReferencedAssemblyTree(assembly, assemblyMatch, assemblies);
            }
        }

        public static IServiceCollection InstallConfigurators(this IServiceCollection serviceCollection, IEnumerable<Assembly> assemblies)
        {
            Predicate<Type> typeMatch = (Type t) => t.Name.EndsWith(ConfiguratorClassConventionSuffix);

            serviceCollection.InstallConfigurators(assemblies, typeMatch);

            return serviceCollection;
        }

        public static IServiceCollection InstallConfigurators(this IServiceCollection serviceCollection, Predicate<AssemblyName> assemblyMatch, Assembly rootAssembly = null)
        {
            if (rootAssembly == null)
            {
                rootAssembly = Assembly.GetCallingAssembly();
            }

            Predicate<Type> typeMatch = (Type t) => t.Name.EndsWith(ConfiguratorClassConventionSuffix);

            serviceCollection.InstallConfigurators(assemblyMatch, typeMatch, rootAssembly);

            return serviceCollection;
        }

        public static IServiceCollection InstallConfiguratorsInApplicationPath(this IServiceCollection serviceCollection, Predicate<AssemblyName> assemblyMatch, Predicate<Type> typeMatch)
        {
             var assemblies = new HashSet<Assembly>();
             
             var allDlls = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.dll", SearchOption.AllDirectories);
             
             foreach (string dll in allDlls)
             {
                 try
                 {
                     var loadedAssembly = Assembly.LoadFile(dll);
                     assemblies.Add(loadedAssembly);
                 }
                 catch (BadImageFormatException) { }
             }

            assemblies.RemoveWhere(a => !assemblyMatch(a.GetName()));

            serviceCollection.InstallConfigurators(assemblies, typeMatch);

            return serviceCollection;
        }

        public static IServiceCollection InstallConfiguratorsInApplicationPath(this IServiceCollection serviceCollection, Predicate<AssemblyName> assemblyMatch)
        {
            Predicate<Type> typeMatch = (Type t) => t.Name.EndsWith(ConfiguratorClassConventionSuffix);
            InstallConfiguratorsInApplicationPath(serviceCollection, assemblyMatch, typeMatch);

            return serviceCollection;
        }

        static IServiceCollection LoadConfigurator(Type type, IServiceCollection serviceCollection)
        {
            var configuratorMethods = type
                            .GetMethods(BindingFlags.Static | BindingFlags.Public)
                            .Where(mi => mi.Name == ConfigureMethodName);

            var numberOfMethods = configuratorMethods.Count();

            if (numberOfMethods > 1)
            {
                throw new ConfiguratorException($"Found more than one \"{ConfigureMethodName}\" method in the configurator \"{type.FullName}\". Ensure that there is exactly one public extentionmethod called \"{ConfigureMethodName}\" in the configurator.");
            }

            if (numberOfMethods == 0)
            {
                throw new ConfiguratorException($"No \"{ConfigureMethodName}\" method was found in the configurator \"{type.FullName}\". Ensure that there is exactly one public extentionmethod called \"{ConfigureMethodName}\" in the configurator.");
            }

            var configuratorMethod = configuratorMethods.First();

            configuratorMethod.Invoke(null, new object[] { serviceCollection });

            return serviceCollection;
        }
    }
}
