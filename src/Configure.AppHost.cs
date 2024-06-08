[assembly: HostingStartup(typeof(docs.unilake.AppHost))]

namespace docs.unilake;

public class AppHost() : AppHostBase("docs.unilake"), IHostingStartup
{
    public void Configure(IWebHostBuilder builder) => builder
        .ConfigureServices(services => {
            // Configure ASP.NET Core IOC Dependencies
        });

    public override void Configure()
    {
    }
}

public class Hello : IGet, IReturn<StringResponse> {}
public class MyServices : Service
{
    public object Any(Hello request) => new StringResponse { Result = $"Hello, World!" };
}