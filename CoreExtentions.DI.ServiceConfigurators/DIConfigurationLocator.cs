namespace CoreExtentions.DI.ServiceConfigurators
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using CoreExtentions.DI.ServiceConfigurators.Exceptions;
    using Microsoft.Extensions.DependencyInjection;

    public static class DIConfigurationLocator
    {
        private const string ConfiguratorClassConventionSuffix = "Installer";
        private const string ConfigureMethodName = "Configure";

        public static void InstallConfigurators(this IServiceCollection services, IEnumerable<Assembly> assemblies, Predicate<Type> typeMatch)
        {
            foreach (var assembly in assemblies)
            {
                var types = assembly.GetTypes().Where(t => t.IsClass && t.IsPublic && typeMatch(t));

                foreach (var installerType in types)
                {
                    LoadConfigurator(installerType, services);
                }
            }
        }
        
        public static void InstallConfigurators(this IServiceCollection services, Predicate<Assembly> assemblyMatch, Predicate<Type> typeMatch)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => assemblyMatch(a));

            if (assemblies.Count() < 1)
            {
                throw new ConfiguratorException("There were no assemblies found that match the predicate provided.");
            }

            services.InstallConfigurators(assemblies, typeMatch);
        }

        public static void InstallConfigurators(this IServiceCollection services, IEnumerable<Assembly> assemblies)
        {
            Predicate<Type> typeMatch = (Type t) => t.Name.EndsWith(ConfiguratorClassConventionSuffix);

            services.InstallConfigurators(assemblies, typeMatch);
        }

        public static void InstallConfigurators(this IServiceCollection services, Predicate<Assembly> assemblyMatch)
        {
            Predicate<Type> typeMatch = (Type t) => t.Name.EndsWith(ConfiguratorClassConventionSuffix);

            services.InstallConfigurators(assemblyMatch, typeMatch);
        }

        static void LoadConfigurator(Type type, IServiceCollection services)
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

            configuratorMethod.Invoke(null, new object[] { services });
        }
    }
}
