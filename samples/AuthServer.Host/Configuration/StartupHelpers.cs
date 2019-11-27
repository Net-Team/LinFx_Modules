﻿using AuthServer.Host.Configuration.ApplicationParts;
using IdentityServer4.EntityFramework.Interfaces;
using LinFx.Extensions.Identity.IdentityServer.Configuration;
using LinFx.Extensions.Identity.IdentityServer.Configuration.Constants;
using LinFx.Extensions.Identity.IdentityServer.Configuration.Intefaces;
using LinFx.Extensions.Identity.IdentityServer.Extensions;
using LinFx.Extensions.Identity.IdentityServer.Extensions.Localization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.QQ;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Logging;
using Serilog;
using System;
using System.Globalization;
using System.Reflection;

namespace AuthServer.Host.Helpers
{
    public static class StartupHelpers
    {
        /// <summary>
        /// Register services for MVC and localization including available languages
        /// </summary>
        /// <param name="services"></param>
        public static void AddMvcWithLocalization<TUser, TKey>(this IServiceCollection services)
            where TUser : IdentityUser<TKey>
            where TKey : IEquatable<TKey>
        {
            services.TryAddTransient(typeof(IGenericControllerLocalizer<>), typeof(GenericControllerLocalizer<>));

            services.AddLocalization(opts => { opts.ResourcesPath = ConfigurationConsts.ResourcesPath; });

            services.AddControllersWithViews(o =>
            {
                o.Conventions.Add(new GenericControllerRouteConvention());
            })
                .AddDataAnnotationsLocalization()
                .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix, opts => { opts.ResourcesPath = ConfigurationConsts.ResourcesPath; })
                .ConfigureApplicationPartManager(m =>
                {
                    m.FeatureProviders.Add(new GenericTypeControllerFeatureProvider<TUser, TKey>());
                });

            services.AddRazorPages();

            services.Configure<RequestLocalizationOptions>(options =>
            {
                var supportedCultures = new[]
                {
                    new CultureInfo("en"),
                    new CultureInfo("fa"),
                    new CultureInfo("ru"),
                    new CultureInfo("sv"),
                    new CultureInfo("zh")
                };
                options.DefaultRequestCulture = new RequestCulture("en");
                options.SupportedCultures = supportedCultures;
                options.SupportedUICultures = supportedCultures;
            });
        }

        /// <summary>
        /// Using of Forwarded Headers and Referrer Policy
        /// </summary>
        /// <param name="app"></param>
        public static void UseSecurityHeaders(this IApplicationBuilder app)
        {
            app.UseForwardedHeaders(new ForwardedHeadersOptions()
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            app.UseHsts(options => options.MaxAge(days: 365));
            app.UseReferrerPolicy(options => options.NoReferrer());
        }

        ///// <summary>
        ///// Add email senders - configuration of sendgrid, smtp senders
        ///// </summary>
        ///// <param name="services"></param>
        ///// <param name="configuration"></param>
        //public static void AddEmailSenders(this IServiceCollection services, IConfiguration configuration)
        //{
        //    var sendgridConnectionString = configuration.GetConnectionString(ConfigurationConsts.SendgridConnectionStringKey);
        //    var smtpConfiguration = configuration.GetSection(nameof(SmtpConfiguration)).Get<SmtpConfiguration>();
        //    var sendgridConfiguration = configuration.GetSection(nameof(SendgridConfiguration)).Get<SendgridConfiguration>();

        //    if (!string.IsNullOrWhiteSpace(sendgridConnectionString))
        //    {
        //        services.AddSingleton<ISendGridClient>(_ => new SendGridClient(sendgridConnectionString));
        //        services.AddSingleton(sendgridConfiguration);
        //        services.AddTransient<IEmailSender, SendgridEmailSender>();
        //    }
        //    else if (smtpConfiguration != null && !string.IsNullOrWhiteSpace(smtpConfiguration.Host))
        //    {
        //        services.AddSingleton(smtpConfiguration);
        //        services.AddTransient<IEmailSender, SmtpEmailSender>();
        //    }
        //    else
        //    {
        //        services.AddSingleton<IEmailSender, EmailSender>();
        //    }
        //}

        /// <summary>
        /// Add services for authentication, including Identity model, IdentityServer4 and external providers
        /// </summary>
        /// <typeparam name="TIdentityDbContext">DbContext for Identity</typeparam>
        /// <typeparam name="TIdentityUser">User Identity class</typeparam>
        /// <typeparam name="TIdentityRole">User Identity Role class</typeparam>
        /// <typeparam name="TConfigurationDbContext"></typeparam>
        /// <typeparam name="TPersistedGrantDbContext"></typeparam>
        /// <param name="services"></param>
        /// <param name="hostingEnvironment"></param>
        /// <param name="configuration"></param>
        /// <param name="logger"></param>
        public static void AddAuthenticationServices<TConfigurationDbContext, TPersistedGrantDbContext, TIdentityDbContext, TIdentityUser, TIdentityRole>(this IServiceCollection services, IConfiguration configuration)
            where TPersistedGrantDbContext : DbContext, IPersistedGrantDbContext
            where TConfigurationDbContext : DbContext, IConfigurationDbContext
            where TIdentityDbContext : DbContext
            where TIdentityUser : class
            where TIdentityRole : class
        {
            var loginConfiguration = GetLoginConfiguration(configuration);
            var registrationConfiguration = GetRegistrationConfiguration(configuration);

            services
                .AddSingleton(registrationConfiguration)
                .AddSingleton(loginConfiguration)
                .AddScoped<UserResolver<TIdentityUser>>()
                .AddIdentity<TIdentityUser, TIdentityRole>(options =>
                {
                    options.User.RequireUniqueEmail = false;
                    options.Password.RequireDigit = true;            //是否需要数字(0-9)
                    options.Password.RequireLowercase = true;        //是否需要包含小写字母(a-z)
                    options.Password.RequireUppercase = true;        //是否需要包含大写字母(A-Z)
                    options.Password.RequireNonAlphanumeric = true;  //是否包含非字母或数字字符
                    options.Password.RequiredLength = 6;             //设置密码长度最小为6
                })
                .AddEntityFrameworkStores<TIdentityDbContext>()
                .AddDefaultTokenProviders();

            services.Configure<IISOptions>(iis =>
            {
                iis.AuthenticationDisplayName = "Windows";
                iis.AutomaticAuthentication = false;
            });

            var authenticationBuilder = services.AddAuthentication();
            AddExternalProviders(authenticationBuilder, configuration);
            AddIdentityServer<TConfigurationDbContext, TPersistedGrantDbContext, TIdentityUser>(services, configuration);
        }

        /// <summary>
        /// Get configuration for login
        /// </summary>
        /// <param name="configuration"></param>
        /// <returns></returns>
        private static LoginConfiguration GetLoginConfiguration(IConfiguration configuration)
        {
            var loginConfiguration = configuration.GetSection(nameof(LoginConfiguration)).Get<LoginConfiguration>();

            // Cannot load configuration - use default configuration values
            if (loginConfiguration == null)
                return new LoginConfiguration();

            return loginConfiguration;
        }

        /// <summary>
        /// Get configuration for registration
        /// </summary>
        /// <param name="configuration"></param>
        /// <returns></returns>
        private static RegisterConfiguration GetRegistrationConfiguration(IConfiguration configuration)
        {
            var registerConfiguration = configuration.GetSection(nameof(RegisterConfiguration)).Get<RegisterConfiguration>();

            // Cannot load configuration - use default configuration values
            if (registerConfiguration == null)
                return new RegisterConfiguration();

            return registerConfiguration;
        }

        /// <summary>
        /// Configuration root configuration
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public static IServiceCollection ConfigureRootConfiguration(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddOptions();

            services.Configure<AdminConfiguration>(configuration.GetSection(ConfigurationConsts.AdminConfigurationKey));
            services.Configure<RegisterConfiguration>(configuration.GetSection(ConfigurationConsts.RegisterConfiguration));

            services.TryAddSingleton<IRootConfiguration, RootConfiguration>();

            return services;
        }

        /// <summary>
        /// Add configuration for IdentityServer4
        /// </summary>
        /// <typeparam name="TIdentityUser"></typeparam>
        /// <typeparam name="TConfigurationDbContext"></typeparam>
        /// <typeparam name="TPersistedGrantDbContext"></typeparam>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        /// <param name="logger"></param>
        /// <param name="hostingEnvironment"></param>
        private static void AddIdentityServer<TConfigurationDbContext, TPersistedGrantDbContext, TIdentityUser>(IServiceCollection services, IConfiguration configuration)
            where TIdentityUser : class
            where TPersistedGrantDbContext : DbContext, IPersistedGrantDbContext
            where TConfigurationDbContext : DbContext, IConfigurationDbContext
        {
            var builder = services.AddIdentityServer(options =>
            {
                options.Events.RaiseErrorEvents = true;
                options.Events.RaiseInformationEvents = true;
                options.Events.RaiseFailureEvents = true;
                options.Events.RaiseSuccessEvents = true;
            })
                .AddInMemoryIdentityResources(Config.GetIdentityResources())
                .AddInMemoryApiResources(Config.GetApis())
                .AddInMemoryClients(Config.GetClients())
                .AddAspNetIdentity<TIdentityUser>()
                .AddUserClaimsPrincipalFactory<TIdentityUser>();
            //.AddIdentityServerStoresWithDbContexts<TConfigurationDbContext, TPersistedGrantDbContext>(configuration, hostingEnvironment);

            builder.AddCustomSigningCredential(configuration);
            builder.AddCustomValidationKey(configuration);
        }

        /// <summary>
        /// Add external providers
        /// </summary>
        /// <param name="authenticationBuilder"></param>
        /// <param name="configuration"></param>
        private static void AddExternalProviders(AuthenticationBuilder authenticationBuilder, IConfiguration configuration)
        {
            var externalProviderConfiguration = configuration.GetSection(nameof(ExternalProvidersConfiguration)).Get<ExternalProvidersConfiguration>();

            if (externalProviderConfiguration.UseGitHubProvider)
            {
                authenticationBuilder.AddGitHub(options =>
                {
                    options.ClientId = externalProviderConfiguration.GitHubClientId;
                    options.ClientSecret = externalProviderConfiguration.GitHubClientSecret;
                    options.Scope.Add("user:email");
                });
            }

            //if (externalProviderConfiguration.UseMicrosoftAccountProvider)
            //{
            //    authenticationBuilder.AddMicrosoftAccount(options =>
            //    {
            //        options.RemoteAuthenticationTimeout = TimeSpan.FromMinutes(30);
            //        options.ClientId = externalProviderConfiguration.MicrosoftAccountClientId;
            //        options.ClientSecret = externalProviderConfiguration.MicrosoftAccountClientSecret;
            //    });
            //}

            if (externalProviderConfiguration.UseQQProvider)
            {
                authenticationBuilder.AddQQ("QQ", options =>
                {
                    options.AppId = externalProviderConfiguration.QQAppId;
                    options.AppKey = externalProviderConfiguration.QQAppSecret;
                    options.RemoteAuthenticationTimeout = TimeSpan.FromMinutes(30);
                });
            }

            //if (externalProviderConfiguration.UseWeChatProvider)
            //{
            //    authenticationBuilder.AddWeChat(woptions =>
            //    {
            //        woptions.RemoteAuthenticationTimeout = TimeSpan.FromMinutes(30);
            //        woptions.AppId = externalProviderConfiguration.WeChatAppId;
            //        woptions.AppSecret = externalProviderConfiguration.WeChatSecret;
            //        woptions.UseCachedStateDataFormat = true;
            //    });
            //}

            if (externalProviderConfiguration.UseWeiboProvider)
            {
                authenticationBuilder.AddWeibo(options =>
                {
                    options.ClientId = externalProviderConfiguration.WeiboAppId;
                    options.ClientSecret = externalProviderConfiguration.WeiboSecret;
                });
            }
        }

        /// <summary>
        /// Add DbContext for Identity
        /// </summary>
        /// <typeparam name="TContext"></typeparam>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        /// <param name="hostingEnvironment"></param>
        public static void AddIdentityDbContext<TContext>(this IServiceCollection services,
            IConfiguration configuration, IWebHostEnvironment hostingEnvironment)
            where TContext : DbContext
        {
            if (hostingEnvironment.IsStaging())
            {
                RegisterIdentityDbContextStaging<TContext>(services);
            }
            else
            {
                RegisterIdentityDbContext<TContext>(services, configuration);
            }
        }

        private static void RegisterIdentityDbContextStaging<TContext>(IServiceCollection services) where TContext : DbContext
        {
            var identityDatabaseName = Guid.NewGuid().ToString();

            //services.AddDbContext<TContext>(optionsBuilder => optionsBuilder.UseInMemoryDatabase(identityDatabaseName));
        }

        private static void RegisterIdentityDbContext<TContext>(IServiceCollection services, IConfiguration configuration)
            where TContext : DbContext
        {
            IdentityModelEventSource.ShowPII = true;
            var sqltype = Convert.ToInt32(configuration["SqlDbType:Type"]);
            var connectionString = configuration.GetConnectionString(ConfigurationConsts.IdentityDbConnectionStringKey);

            switch (sqltype)
            {
                #region Mysql
                case 2:
                    services.AddDbContext<TContext>(options => options.UseMySql(connectionString));
                    break;
                #endregion

                #region NgSql
                case 3:
                    services.AddDbContext<TContext>(options => options.UseNpgsql(connectionString));
                    break;
                #endregion

                #region Sqlite
                case 4:
                    services.AddDbContext<TContext>(options => options.UseSqlite(connectionString));
                    break;
                    #endregion
            }

        }

        /// <summary>
        /// Add shared DbContext for Identity and IdentityServer4 stores
        /// </summary>
        /// <typeparam name="TContext"></typeparam>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        public static void AddDbContexts<TContext>(this IServiceCollection services, IConfiguration configuration)
            where TContext : DbContext
        {
            //var connectionString = configuration.GetConnectionString(ConfigurationConsts.AdminConnectionStringKey);
            //services.AddDbContext<TContext>(options => options.UseSqlServer(connectionString));
        }

        /// <summary>
        /// Register DbContexts and configure stores for IdentityServer4
        /// </summary>
        /// <typeparam name="TConfigurationDbContext"></typeparam>
        /// <typeparam name="TPersistedGrantDbContext"></typeparam>
        /// <param name="builder"></param>
        /// <param name="configuration"></param>
        /// <param name="hostingEnvironment"></param>
        public static IIdentityServerBuilder AddIdentityServerStoresWithDbContexts<TConfigurationDbContext,
            TPersistedGrantDbContext>(this IIdentityServerBuilder builder, IConfiguration configuration,
            IWebHostEnvironment hostingEnvironment)
            where TPersistedGrantDbContext : DbContext, IPersistedGrantDbContext
            where TConfigurationDbContext : DbContext, IConfigurationDbContext
        {
            if (hostingEnvironment.IsStaging())
            {
                return RegisterIdentityServerStoresWithDbContextsStaging<TConfigurationDbContext, TPersistedGrantDbContext>(builder, configuration);
            }
            else
            {
                return RegisterIdentityServerStoresWithDbContexts<TConfigurationDbContext, TPersistedGrantDbContext>(builder, configuration);
            }
        }

        private static IIdentityServerBuilder
            RegisterIdentityServerStoresWithDbContextsStaging<TConfigurationDbContext, TPersistedGrantDbContext>(
                IIdentityServerBuilder builder, IConfiguration configuration)
            where TPersistedGrantDbContext : DbContext, IPersistedGrantDbContext
            where TConfigurationDbContext : DbContext, IConfigurationDbContext
        {
            var configurationDatabaseName = Guid.NewGuid().ToString();
            var operationalDatabaseName = Guid.NewGuid().ToString();

            //builder.AddConfigurationStore<TConfigurationDbContext>(options =>
            //{
            //    options.ConfigureDbContext = b => b.UseInMemoryDatabase(configurationDatabaseName);
            //});

            //builder.AddOperationalStore<TPersistedGrantDbContext>(options =>
            //{
            //    options.ConfigureDbContext = b => b.UseInMemoryDatabase(operationalDatabaseName);
            //});

            return builder;
        }

        private static IIdentityServerBuilder
            RegisterIdentityServerStoresWithDbContexts<TConfigurationDbContext, TPersistedGrantDbContext>(
                IIdentityServerBuilder builder, IConfiguration configuration)
            where TPersistedGrantDbContext : DbContext, IPersistedGrantDbContext
            where TConfigurationDbContext : DbContext, IConfigurationDbContext
        {
            //get sqltype
            var sqltype = Convert.ToInt32(configuration["SqlDbType:Type"]);

            var migrationsAssembly = typeof(Startup).GetTypeInfo().Assembly.GetName().Name;

            // Config DB from existing connection
            builder.AddConfigurationStore<TConfigurationDbContext>(options =>
            {
                switch (sqltype)
                {
                    //case 1:
                    //    options.ConfigureDbContext = b =>
                    //b.UseSqlServer(
                    //    configuration.GetConnectionString(ConfigurationConsts.ConfigurationDbConnectionStringKey),
                    //    sql => sql.MigrationsAssembly(migrationsAssembly));
                    //    break;
                    case 2:
                        options.ConfigureDbContext = b =>
                    b.UseMySql(
                        configuration.GetConnectionString(ConfigurationConsts.ConfigurationDbConnectionStringKey),
                        sql => sql.MigrationsAssembly(migrationsAssembly));
                        break;
                    case 3:
                        options.ConfigureDbContext = b =>
                    b.UseNpgsql(
                        configuration.GetConnectionString(ConfigurationConsts.ConfigurationDbConnectionStringKey),
                        sql => sql.MigrationsAssembly(migrationsAssembly));
                        break;
                    case 4:
                        options.ConfigureDbContext = b =>
                    b.UseSqlite(
                        configuration.GetConnectionString(ConfigurationConsts.ConfigurationDbConnectionStringKey),
                        sql => sql.MigrationsAssembly(migrationsAssembly));
                        break;
                }
            });

            // Operational DB from existing connection
            builder.AddOperationalStore<TPersistedGrantDbContext>(options =>
            {
                options.EnableTokenCleanup = true;
#if DEBUG
                options.TokenCleanupInterval = 15;
#endif
                switch (sqltype)
                {
                    //case 1:
                    //    options.ConfigureDbContext = b =>
                    //b.UseSqlServer(
                    //    configuration.GetConnectionString(ConfigurationConsts.PersistedGrantDbConnectionStringKey),
                    //    sql => sql.MigrationsAssembly(migrationsAssembly));
                    //    break;
                    case 2:
                        options.ConfigureDbContext = b =>
                    b.UseMySql(
                        configuration.GetConnectionString(ConfigurationConsts.PersistedGrantDbConnectionStringKey),
                        sql => sql.MigrationsAssembly(migrationsAssembly));
                        break;
                    case 3:
                        options.ConfigureDbContext = b =>
                    b.UseNpgsql(
                        configuration.GetConnectionString(ConfigurationConsts.PersistedGrantDbConnectionStringKey),
                        sql => sql.MigrationsAssembly(migrationsAssembly));
                        break;
                    case 4:
                        options.ConfigureDbContext = b =>
                                            b.UseSqlite(
                                                configuration.GetConnectionString(ConfigurationConsts.PersistedGrantDbConnectionStringKey),
                                                sql => sql.MigrationsAssembly(migrationsAssembly));
                        break;
                }

            });

            return builder;
        }

        /// <summary>
        /// Register middleware for localization
        /// </summary>
        /// <param name="app"></param>
        public static void UseMvcLocalizationServices(this IApplicationBuilder app)
        {
            var options = app.ApplicationServices.GetService<IOptions<RequestLocalizationOptions>>();
            app.UseRequestLocalization(options.Value);
        }

        /// <summary>
        /// Add configuration for logging
        /// </summary>
        /// <param name="app"></param>
        /// <param name="loggerFactory"></param>
        /// <param name="configuration"></param>
        public static void AddLogging(this IApplicationBuilder app, ILoggerFactory loggerFactory, IConfiguration configuration)
        {
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();
        }

        /// <summary>
        /// Add authorization policies
        /// </summary>
        /// <param name="services"></param>
        public static void AddAuthorizationPolicies(this IServiceCollection services)
        {
            services.AddAuthorization(options =>
            {
                options.AddPolicy(AuthorizationConsts.AdministrationPolicy, policy => policy.RequireRole(AuthorizationConsts.AdministrationRole));
            });
        }
    }
}