using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Modular.Abstractions.Modules;

public interface IModule
{
    string Name { get; }
    IEnumerable<string> Policies => Array.Empty<string>();
    void Register(IServiceCollection services);
    void Use(IApplicationBuilder app);
}