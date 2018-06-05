namespace Sitecore.Support.Owin.Authentication.Services
{
  using Microsoft.Extensions.DependencyInjection;
  using Sitecore.DependencyInjection;
  using System.Diagnostics.CodeAnalysis;

  public class ServicesConfigurator : IServicesConfigurator
  {
    [SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling")]
    public void Configure(IServiceCollection serviceCollection)
    {
      serviceCollection.AddSingleton<Sitecore.Owin.Authentication.Services.ApplicationUserResolver, Sitecore.Support.Owin.Authentication.Services.DefaultApplicationUserResolver>();
    }
  }
}
