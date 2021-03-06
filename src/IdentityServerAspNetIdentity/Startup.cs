﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Startup.cs" company="">
// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.
// </copyright>
// <summary>
//   The startup.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SingleSignOn.IdentityServerAspNetIdentity
{
    #region Usings

    using System.IO;
    using System.Reflection;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc.ApiExplorer;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;
    using SingleSignOn.Data.Context;
    using SingleSignOn.IdentityServerAspNetIdentity.Models;
    using SingleSignOn.IdentityServerAspNetIdentity.Swagger;
    using Swashbuckle.AspNetCore.SwaggerGen;

    #endregion

    /// <summary>
    /// The startup.
    /// </summary>
    public class Startup
    {
        #region Constructors and Destructors

        /// <summary>
        /// Initializes a new instance of the <see cref="Startup"/> class.
        /// </summary>
        /// <param name="configuration">
        /// The configuration.
        /// </param>
        /// <param name="environment">
        /// The environment.
        /// </param>
        public Startup(IConfiguration configuration, IHostingEnvironment environment)
        {
            this.Configuration = configuration;
            this.Environment = environment;

            var builder = new ConfigurationBuilder().SetBasePath(environment.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true).AddJsonFile(
                    $"appsettings.{environment.EnvironmentName}.json",
                    optional: true).AddEnvironmentVariables();

            this.Configuration = builder.Build();
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets the configuration.
        /// </summary>
        public IConfiguration Configuration { get; }

        /// <summary>
        /// Gets the environment.
        /// </summary>
        public IHostingEnvironment Environment { get; }

        #endregion

        #region Other Properties

        /// <summary>
        /// Gets the xml comments file path.
        /// </summary>
        string XmlCommentsFilePath
        {
            get
            {
                var fileName = typeof(Startup).GetTypeInfo().Assembly.GetName().Name + ".xml";
                return Path.Combine(this.Environment.ContentRootPath, fileName);
            }
        }

        #endregion

        #region Public Methods And Operators

        /// <summary>
        /// The configure.
        /// </summary>
        /// <param name="app">
        /// The app.
        /// </param>
        /// <param name="provider">
        /// The provider.
        /// </param>
        public void Configure(IApplicationBuilder app, IApiVersionDescriptionProvider provider)
        {
            // this will do the initial DB population
            // Todo: This is a temp step.
            // SeedData.InitializeDatabase(app);
            // SeedData.EnsureSeedData(this.Configuration.GetConnectionString("DefaultConnection"));

            if (this.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseCors(
                options =>
                    {
                        options.AllowAnyOrigin();
                        options.AllowAnyMethod();
                        options.AllowAnyHeader();
                    });

            app.UseStaticFiles();
            app.UseIdentityServer();
            app.UseMvcWithDefaultRoute();

            app.UseSwagger();
            app.UseSwaggerUI(
                options =>
                    {
                        // build a swagger endpoint for each discovered API version
                        foreach (var description in provider.ApiVersionDescriptions)
                        {
                            options.SwaggerEndpoint(
                                $"/swagger/{description.GroupName}/swagger.json",
                                description.GroupName.ToUpperInvariant());
                        }
                    });
        }

        /// <summary>
        /// The configure services.
        /// </summary>
        /// <param name="services">
        /// The services.
        /// </param>
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<ApplicationDbContext>(
                options =>

                    // options.UseSqlite(Configuration.GetConnectionString("SqlLite")));
                    // options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")));
                    options.UseNpgsql(this.Configuration.GetConnectionString("DefaultConnection")));

            services.AddIdentity<ApplicationUser, IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            // EntityFramework
            var migrationsAssembly = typeof(Startup).GetTypeInfo().Assembly.GetName().Name;

            // services.AddMvc().SetCompatibilityVersion(Microsoft.AspNetCore.Mvc.CompatibilityVersion.Version_2_1);
            services.AddMvc(options => options.EnableEndpointRouting = true)
                .SetCompatibilityVersion(Microsoft.AspNetCore.Mvc.CompatibilityVersion.Version_2_1);
            services.AddApiVersioning(
                options =>
                    {
                        // reporting api versions will return the headers "api-supported-versions" and "api-deprecated-versions"
                        options.ReportApiVersions = true;
                    });
            services.AddVersionedApiExplorer(
                options =>
                    {
                        // add the versioned api explorer, which also adds IApiVersionDescriptionProvider service
                        // note: the specified format code will format the version as "'v'major[.minor][-status]"
                        options.GroupNameFormat = "'v'VVV";

                        // note: this option is only necessary when versioning by url segment. the SubstitutionFormat
                        // can also be used to control the format of the API version in route templates
                        options.SubstituteApiVersionInUrl = true;
                    });
            services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
            services.AddSwaggerGen(
                options =>
                    {
                        // add a custom operation filter which sets default values
                        options.OperationFilter<SwaggerDefaultValues>();

                        // integrate xml comments
                        // options.IncludeXmlComments( XmlCommentsFilePath );
                    });

            services.Configure<IISOptions>(
                iis =>
                    {
                        iis.AuthenticationDisplayName = "Windows";
                        iis.AutomaticAuthentication = false;
                    });

            var builder = services.AddIdentityServer(
                    options =>
                        {
                            options.Events.RaiseErrorEvents = true;
                            options.Events.RaiseInformationEvents = true;
                            options.Events.RaiseFailureEvents = true;
                            options.Events.RaiseSuccessEvents = true;
                        })

                // .AddInMemoryIdentityResources(Config.GetIdentityResources())
                // .AddInMemoryApiResources(Config.GetApis())
                // .AddInMemoryClients(Config.GetClients())
                .AddAspNetIdentity<ApplicationUser>().AddConfigurationStore(
                    options =>
                        {
                            options.ConfigureDbContext = b =>

                                // b.UseSqlServer(Configuration.GetConnectionString("DefaultConnection"),
                                b.UseNpgsql(
                                    this.Configuration.GetConnectionString("DefaultConnection"),
                                    sql => sql.MigrationsAssembly(migrationsAssembly));
                        })

                // this adds the operational data from DB (codes, tokens, consents)
                .AddOperationalStore(
                    options =>
                        {
                            options.ConfigureDbContext = b =>

                                // b.UseSqlServer(Configuration.GetConnectionString("DefaultConnection"),
                                b.UseNpgsql(
                                    this.Configuration.GetConnectionString("DefaultConnection"),
                                    sql => sql.MigrationsAssembly(migrationsAssembly));

                            // this enables automatic token cleanup. this is optional.
                            options.EnableTokenCleanup = true;
                        });

            if (this.Environment.IsDevelopment())
            {
                builder.AddDeveloperSigningCredential();
            }
            else
            {
                builder.AddDeveloperSigningCredential();

                // throw new Exception("need to configure key material");
            }

            services.AddAuthentication().AddGoogle(
                options =>
                    {
                        // register your IdentityServer with Google at https://console.developers.google.com
                        // enable the Google+ API
                        // set the redirect URI to http://localhost:5000/signin-google
                        options.ClientId = "copy client ID from Google here";
                        options.ClientSecret = "copy client secret from Google here";
                    });
        }

        #endregion
    }
}