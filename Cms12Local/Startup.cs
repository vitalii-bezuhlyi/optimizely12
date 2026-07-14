using Cms12Local.Extensions;
using Cms12Local.Security;
using EPiServer.ContentApi.Cms;
using EPiServer.ContentManagementApi;
using EPiServer.Cms.Shell;
using EPiServer.Cms.UI.AspNetIdentity;
using EPiServer.OpenIDConnect;
using EPiServer.Scheduler;
using EPiServer.ServiceLocation;
using EPiServer.Web.Routing;
using Microsoft.AspNetCore.Authentication;

namespace Cms12Local;

public class Startup
{
    private readonly IWebHostEnvironment _webHostingEnvironment;

    public Startup(IWebHostEnvironment webHostingEnvironment)
    {
        _webHostingEnvironment = webHostingEnvironment;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        if (_webHostingEnvironment.IsDevelopment())
        {
            AppDomain.CurrentDomain.SetData("DataDirectory", Path.Combine(_webHostingEnvironment.ContentRootPath, "App_Data"));

            services.Configure<SchedulerOptions>(options => options.Enabled = false);
        }

        services
            .AddCmsAspNetIdentity<ApplicationUser>()
            .AddCms()
            .AddAlloy()
            .AddAdminUserRegistration()
            .AddEmbeddedLocalization<Startup>();

        services.AddOpenIDConnect<ApplicationUser>(
            useDevelopmentCertificate: true,
            createSchema: true,
            configureOptions: options =>
            {
                options.RequireHttps = false;
                options.AllowResourceOwnerPasswordFlow = true;
                options.Applications.Add(new OpenIDConnectApplication
                {
                    ClientId = "blackbird-local",
                    ClientSecret = "blackbird-local-secret",
                    Scopes =
                    {
                        "openid",
                        "offline_access",
                        "roles",
                        ContentManagementApiOptionsDefaults.Scope
                    }
                });
                options.Applications.Add(new OpenIDConnectApplication
                {
                    ClientId = "blackbird-cc",
                    ClientSecret = "blackbird-cc-secret",
                    Scopes =
                    {
                        ContentManagementApiOptionsDefaults.Scope
                    }
                });
            },
            configureSqlServerOptions: _ => { });

        services.AddOpenIDConnectUI();

        services.AddOpenIddict()
            .AddServer(builder => builder.AddEventHandler(ClientCredentialsRolesEnricher.Descriptor));

        services.AddContentManagementApi(OpenIDConnectOptionsDefaults.AuthenticationScheme, options =>
        {
            options.RequiredRole = string.Empty;
        });

        services.AddContentDeliveryApi(OpenIDConnectOptionsDefaults.AuthenticationScheme, options =>
        {
            options.SiteDefinitionApiEnabled = true;
        });

        services.AddTransient<IClaimsTransformation, BlackbirdLocalClientClaimsTransformation>();

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy
                    .AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        // Required by Wangkanai.Detection
        services.AddDetection();

        services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromSeconds(10);
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
        });
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        // Required by Wangkanai.Detection
        app.UseDetection();
        app.UseSession();

        app.UseStaticFiles();
        app.UseRouting();
        app.UseCors();
        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapContent();
        });
    }
}
