using fhello1.App;
using HelloMe.Interface;
using HelloMe.Service;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(Startup))]
namespace fhello1.App
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddSingleton<IHelloCustomSingleton, HelloCustomSingleton>();
            builder.Services.AddScoped<IHelloCustomScoped, HelloCustomScoped>();
            builder.Services.AddTransient<IHelloCustomTransient, HelloCustomTransient>();

            builder.Services
                .AddOptions<IConfig>()
                .Configure<IConfiguration>((settings, configuration) => {
                    configuration
                    .GetSection("Config")
                    .Bind(settings);
    });
        }
    }
}
