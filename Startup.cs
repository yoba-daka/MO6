using GoogleReCaptcha.V3;
using GoogleReCaptcha.V3.Interface;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MO6.Middleware;
using MyProject12;
using MyProject12.Controllers;
using MyProject12.Services;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.Website.Controllers;
using Umbraco.Extensions;

namespace MosheSharon
{
    public class Startup
    {
        private static readonly HashSet<string> PwaNoCachePaths = new(StringComparer.OrdinalIgnoreCase)
        {
            "/sw.js",
            "/manifest.json",
            "/offline.html",
            "/offline-lesson.html",
            "/js/db-helper.js"
        };

        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;

        /// <summary>
        /// Initializes a new instance of the <see cref="Startup" /> class.
        /// </summary>
        /// <param name="webHostEnvironment">The web hosting environment.</param>
        /// <param name="config">The configuration.</param>
        /// <remarks>
        /// Only a few services are possible to be injected here https://github.com/dotnet/aspnetcore/issues/9337.
        /// </remarks>
        public Startup(IWebHostEnvironment webHostEnvironment, IConfiguration config)
        {
            _env = webHostEnvironment ?? throw new ArgumentNullException(nameof(webHostEnvironment));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Configures the services.
        /// </summary>
        /// <param name="services">The services.</param>
        /// <remarks>
        /// This method gets called by the runtime. Use this method to add services to the container.
        /// For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940.
        /// </remarks>
        public void ConfigureServices(IServiceCollection services)
        {
            var connectionString = _config["Storage:AzureBlob:Media:ConnectionString"];
            var containerName = _config["Storage:AzureBlob:Media:ContainerName"];
            services.AddUmbraco(_env, _config)
                .AddBackOffice()

                .AddWebsite()
                .AddDeliveryApi()
                .AddComposers()
                .AddNotificationHandler<UmbracoApplicationStartingNotification, CreateBundlesNotificationHandler>()
                .AddAzureBlobMediaFileSystem() // This configures the required services for Media
                .AddAzureBlobImageSharpCache() // This configures the required services for the Image Sharp cache
                .Build();

            services.AddDbContext<DB>(options =>
            options.UseSqlServer(_config.GetConnectionString("umbracoDbDSN")));

            services.AddMemoryCache();
            services.AddHttpClient<ICaptchaValidator, GoogleReCaptchaValidator>();
            services.AddTransient<LessonBlobService>();
            services.AddTransient<EmailService>();
            services.AddHostedService<EveryHour>();
            services.AddTransient<MeshulamService>();
            services.AddTransient<MeshulamWebhookPayloadReader>();
            services.AddTransient<TemporaryMemberResolver>();
            services.Configure<PaymentsHarnessOptions>(_config.GetSection("PaymentsHarness"));
            services.AddSingleton<PaymentsHarnessStore>();
            services.AddTransient<PaymentsHarnessSandboxClient>();
            services.AddHttpClient();
            services.Configure<SecurityStampValidatorOptions>(o =>
             {
                 o.ValidationInterval = TimeSpan.Zero;
             });

        }

        /// <summary>
        /// Configures the application.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <param name="env">The web hosting environment.</param>
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.Use(async (context, next) =>
            {
                context.Response.OnStarting(() =>
                {
                    if (PwaNoCachePaths.Contains(context.Request.Path.Value ?? string.Empty))
                    {
                        context.Response.Headers["Cache-Control"] = "no-store, no-cache, max-age=0, must-revalidate";
                        context.Response.Headers["Pragma"] = "no-cache";
                        context.Response.Headers["Expires"] = "0";
                    }

                    return Task.CompletedTask;
                });

                await next();
            });


            app.UseMiddleware<CaptureRedirectMiddleware>();

            app.UseMiddleware<CustomRedirectMiddleware>();

            app.UseMiddleware<LegacyUrlRedirectMiddleware>();



                app.UseUmbraco()
               .WithMiddleware(u =>
               {
                   u.UseBackOffice();
                   u.UseWebsite();
               })
               .WithEndpoints(u =>
               {
                   u.UseInstallerEndpoints();
                   u.UseBackOfficeEndpoints();
                   u.EndpointRouteBuilder.MapControllerRoute(
                       "HlsProxy",
                       "media/hls/{contentName}/{**blobPath}",
                       new { controller = "HlsProxy", action = "Get" });
                   u.EndpointRouteBuilder.MapControllerRoute(
                       "LessonAudio",
                       "media/audio/{contentName}/{**blobPath}",
                       new { controller = "LessonMedia", action = "Audio" });
                   u.UseWebsiteEndpoints();
                   u.EndpointRouteBuilder.MapControllerRoute(
                       "PaymentsHarnessPage",
                       "payments-harness",
                       new { controller = "Meshulam", action = "PaymentsHarness" });
                   u.EndpointRouteBuilder.MapControllerRoute(
                       "PaymentsHarnessStartYearly",
                       "payments-harness/start-yearly",
                       new { controller = "Meshulam", action = "StartYearlyHarness" });
                   u.EndpointRouteBuilder.MapControllerRoute(
                       "PaymentsHarnessStartMonthly",
                       "payments-harness/start-monthly",
                       new { controller = "Meshulam", action = "StartMonthlyHarness" });
                   u.EndpointRouteBuilder.MapControllerRoute(
                       "PaymentsHarnessCancelLatest",
                       "payments-harness/cancel-latest",
                       new { controller = "Meshulam", action = "CancelLatestHarness" });
                   u.EndpointRouteBuilder.MapControllerRoute(
                       "MeshulamResponseWebhook",
                       "meshulam-response",
                       new { controller = "Meshulam", action = "HandleMeshulamResponse9jf83207409f27" });
                   u.EndpointRouteBuilder.MapControllerRoute(
                       "MeshulamRecurringSuccessWebhook",
                       "meshulam-dd-success",
                       new { controller = "Meshulam", action = "HandleMeshulamResponse2847g93j596034" });
                   u.EndpointRouteBuilder.MapControllerRoute(
                       "MeshulamRecurringFailureWebhook",
                       "meshulam-dd-failure",
                       new { controller = "Meshulam", action = "HandleMeshulamResponse2847g93j565745" });
                   u.EndpointRouteBuilder.MapControllerRoute(
                       "BackOfficeExtra",
                       "umbraco/backoffice/plugins/{controller}/{action}",
                       new { controller = "ContactMessages", action = "Index" });
               });



        }
    }
}
