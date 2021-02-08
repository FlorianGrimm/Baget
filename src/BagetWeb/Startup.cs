using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BaGet.Core;
using BaGet;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.SpaServices.StaticFiles;
using Microsoft.AspNetCore.HttpOverrides;
using NuGet.Packaging;
using BaGet.Azure;

namespace BagetWeb {
    public class Startup {
        public Startup(IConfiguration configuration) {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services) {
            services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApp(Configuration.GetSection("AzureAd"));

            services.AddAuthorization(options => {
                // By default, all incoming requests will be authorized according to the default policy
                options.FallbackPolicy = options.DefaultPolicy;
            });
            services.AddRazorPages()
                .AddMvcOptions(options => { })
                .AddMicrosoftIdentityUI();

            //
            services.AddBaGetOptions<IISServerOptions>(nameof(IISServerOptions));

            // TODO: Ideally we'd use:
            //
            //       services.ConfigureOptions<ConfigureBaGetOptions>();
            //
            //       However, "ConfigureOptions" doesn't register validations as expected.
            //       We'll instead register all these configurations manually.
            // See: https://github.com/dotnet/runtime/issues/38491
            //services.AddTransient<IConfigureOptions<CorsOptions>, ConfigureBaGetOptions>();
            //services.AddTransient<IConfigureOptions<FormOptions>, ConfigureBaGetOptions>();
            //services.AddTransient<IConfigureOptions<ForwardedHeadersOptions>, ConfigureBaGetOptions>();
            //services.AddTransient<IConfigureOptions<IISServerOptions>, ConfigureBaGetOptions>();
            //services.AddTransient<IValidateOptions<BaGetOptions>, ConfigureBaGetOptions>();
            services.Configure<FormOptions>(options => {
                options.MultipartBodyLengthLimit = int.MaxValue;
            });
            services.Configure<ForwardedHeadersOptions>(options => {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

                // Do not restrict to local network/proxy
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            });
            services.Configure<IISServerOptions>(options => { options.MaxRequestBodySize = 262144000; });
            services.Configure<BaGetOptions>(options => {
                options.Database = new DatabaseOptions() { 
                    Type = "AzureTable",
                    ConnectionString = ""
                };
                options.Storage = new StorageOptions() { 
                    Type = "AzureBlobStorage",
                    
                };
                options.PackageDeletionBehavior = PackageDeletionBehavior.HardDelete;
                options.Mirror = new MirrorOptions() { Enabled = false };
            });

            services.AddSpaStaticFiles(ConfigureSpaStaticFiles);
            services.AddBaGetWebApplication(options=> {
                options.AddAzureBlobStorage(storage => {
                    storage.Container = "";
                    storage.ConnectionString = "";
                });
                //options.AddAzureSearch(search => {
                //    search.ApiKey = "";
                //    search.AccountName=""
                //});

            });

            // You can swap between implementations of subsystems like storage and search using BaGet's configuration.
            // Each subsystem's implementation has a provider that reads the configuration to determine if it should be
            // activated. BaGet will run through all its providers until it finds one that is active.
            services.AddScoped(DependencyInjectionExtensions.GetServiceFromProviders<IContext>);
            services.AddTransient(DependencyInjectionExtensions.GetServiceFromProviders<IStorageService>);
            services.AddTransient(DependencyInjectionExtensions.GetServiceFromProviders<IPackageService>);
            services.AddTransient(DependencyInjectionExtensions.GetServiceFromProviders<ISearchService>);
            services.AddTransient(DependencyInjectionExtensions.GetServiceFromProviders<ISearchIndexer>);

            services.AddCors();
        }


        private void ConfigureSpaStaticFiles(SpaStaticFilesOptions options) {
            // In production, the UI files will be served from this directory
            options.RootPath = "BaGet.UI/build";
        }

        private void ConfigureBaGetApplication(BaGetApplication app) {
            // Add database providers.
            app.AddAzureTableDatabase();
            //app.AddMySqlDatabase();
            //app.AddPostgreSqlDatabase();
            //app.AddSqliteDatabase();
            //app.AddSqlServerDatabase();

            // Add storage providers.
            app.AddFileStorage();
            //app.AddAliyunOssStorage();
            //app.AddAwsS3Storage();
            app.AddAzureBlobStorage();
            //app.AddGoogleCloudStorage();

            // Add search providers.
            app.AddAzureSearch();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
            if (env.IsDevelopment()) {
                app.UseDeveloperExceptionPage();
            } else {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints => {
                endpoints.MapRazorPages();
                endpoints.MapControllers();
            });
        }
    }
}
