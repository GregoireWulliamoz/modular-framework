using System.Reflection;
using Microsoft.Extensions.Configuration;
using Modular.Abstractions.Modules;

namespace Modular.Infrastructure.Modules;

public static class ModuleLoader
{
    public static IList<Assembly> LoadAssemblies(IConfiguration configuration, string modulePart)
    {
        List<Assembly> assemblies = AppDomain.CurrentDomain.GetAssemblies().ToList();
        string[] locations = assemblies.Where(x => !x.IsDynamic).Select(x => x.Location).ToArray();
        List<string> files = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.dll")
            .Where(x => !locations.Contains(x, StringComparer.InvariantCultureIgnoreCase))
            .ToList();

        var disabledModules = new List<string>();
        foreach (string file in files)
        {
            if (!file.Contains(modulePart))
            {
                continue;
            }

            string moduleName = file.Split(modulePart)[1].Split(".")[0].ToLowerInvariant();
            var enabled = configuration.GetValue<bool>($"{moduleName}:module:enabled");
            if (!enabled)
            {
                disabledModules.Add(file);
            }
        }

        foreach (string disabledModule in disabledModules)
        {
            files.Remove(disabledModule);
        }

        files.ForEach(x => assemblies.Add(AppDomain.CurrentDomain.Load(AssemblyName.GetAssemblyName(x))));

        return assemblies;
    }

    public static IList<IModule> LoadModules(IEnumerable<Assembly> assemblies)
        => assemblies
            .SelectMany(x => x.GetTypes())
            .Where(x => typeof(IModule).IsAssignableFrom(x) && !x.IsInterface)
            .OrderBy(x => x.Name)
            .Select(Activator.CreateInstance)
            .Cast<IModule>()
            .ToList();
}