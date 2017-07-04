﻿using EFGetStarted.AspNetCore.ExistingDb.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO;
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using AspNetCore.ExistingDb;
using AspNetCore.ExistingDb.Repositories;
using System.Data.Common;
using Microsoft.AspNetCore.Mvc.Razor.Compilation;
using Microsoft.VisualStudio.Web.CodeGeneration.Templating.Compilation;

//[assembly: UserSecretsId("aspnet-AspNetCore.ExistingDb-20161230022416")]

namespace EFGetStarted.AspNetCore.ExistingDb
{
	public class Startup
	{
		public IConfiguration Configuration { get; }

		public static void Main(string[] args)
		{
			var host = new WebHostBuilder()
				.UseKestrel()
				.UseContentRoot(Directory.GetCurrentDirectory())
				.UseIISIntegration()
				.UseStartup<Startup>()
				//.UseApplicationInsights()
				.Build();

			host.Run();
		}

		public Startup(IHostingEnvironment env)
		{
			var builder = new ConfigurationBuilder()
				.SetBasePath(env.ContentRootPath)
				.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
				.AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
				.AddEnvironmentVariables();
			if (env.IsDevelopment())
				builder.AddUserSecrets<Startup>();

			Configuration = builder.Build();
		}

		/// <summary>
		/// Sets up DB kind and connection
		/// </summary>
		/// <returns>connection string</returns>
		/// <param name="dbContextOpts"></param>
		/// <param name="configuration"></param>
		/// <param name="distributedCacheServices"></param>
		internal static string ConfigureDBKind(DbContextOptionsBuilder dbContextOpts, IConfiguration configuration, IServiceCollection distributedCacheServices = null)
		{
			string conn_str;
			switch (configuration["DBKind"]?.ToLower())
			{
				case "mysql":
				case "mariadb":
				case "maria":
					conn_str = configuration.GetConnectionString("MySQL");
					if (dbContextOpts != null)
						dbContextOpts.UseMySql(conn_str);
					if (distributedCacheServices != null)
					{
						distributedCacheServices.AddDistributedMySqlCache(opts =>
						{
							opts.ConnectionString = conn_str;

							var builder = new DbConnectionStringBuilder();
							builder.ConnectionString = conn_str;
							opts.SchemaName = builder["database"] as string;

							opts.TableName = nameof(SessionCache);
							opts.ExpiredItemsDeletionInterval = TimeSpan.FromMinutes(30);
						});
					}
					break;

				case "sqlserver":
				case "mssql":
					conn_str = configuration.GetConnectionString("SqlServer");
					if (dbContextOpts != null)
						dbContextOpts.UseSqlServer(conn_str);
					if (distributedCacheServices != null)
					{
						distributedCacheServices.AddDistributedSqlServerCache(opts =>
						{
							opts.ConnectionString = conn_str;
							opts.SchemaName = "dbo";
							opts.TableName = nameof(SessionCache);
							opts.ExpiredItemsDeletionInterval = TimeSpan.FromMinutes(30);
						});
					}
					break;

				case "sqlite":
					conn_str = "Filename=./Blogging.db";
					if (dbContextOpts != null)
						dbContextOpts.UseSqlite(conn_str);
					break;

				default:
					throw new NotSupportedException($"Bad DBKind name");
			}
			return conn_str;
		}

		void ConfigureDistributedCache(IConfiguration configuration, IServiceCollection services)
		{
			ConfigureDBKind(null, configuration, services);
		}

		void ConfigureDependencyInjection(IServiceCollection services)
		{
			services.AddSingleton(Configuration);
#if DEBUG
			services.AddSingleton<ICompilationService, RoslynCompilationService>();
#endif
			services.AddScoped<IBloggingRepository, BloggingRepository>();
			services.AddScoped<IHashesRepository, HashesRepository>();
		}

		// This method gets called by the runtime. Use this method to add services to the container.
		public void ConfigureServices(IServiceCollection services)
		{
			ConfigureDependencyInjection(services);

			services.AddDbContext<BloggingContext>(options =>
			{
				ConfigureDBKind(options, Configuration);
			});

			ConfigureDistributedCache(Configuration, services);

			services.AddSession(options =>
			{
				// Set a short timeout for easy testing.
				options.IdleTimeout = TimeSpan.FromMinutes(60);
				options.CookieHttpOnly = true;
			});

			// Add framework services.
			services.AddMvc();

			var keys_directory = Configuration["SharedKeysDirectory"]?.Replace('/', Path.DirectorySeparatorChar)?.Replace('\\', Path.DirectorySeparatorChar);
			if (!string.IsNullOrEmpty(keys_directory) && Directory.Exists(keys_directory))
			{
				services.AddDataProtection()
					.SetDefaultKeyLifetime(TimeSpan.FromDays(14))
					.PersistKeysToFileSystem(new DirectoryInfo(keys_directory));
			}
			else
			{
				services.AddDataProtection()
					.SetDefaultKeyLifetime(TimeSpan.FromDays(14));
			}
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
		{
			loggerFactory.AddConsole(Configuration.GetSection("Logging"));

			//app.UseEnvironmentTitleDisplay();

			if (env.IsDevelopment())
			{
				loggerFactory.AddDebug();
				app.UseDeveloperExceptionPage();
				//app.UseExceptionHandler("/Home/Error");
				app.UseBrowserLink();
			}
			else
			{
				app.UseExceptionHandler("/Home/Error");
			}
			app.UseStatusCodePagesWithReExecute("/Home/Error/{0}");

			app.UseStaticFiles();

			app.UseSession();

			app.UseMvc(routes =>
			{
				routes.MapRoute(
					name: "default",
					template: "{controller=Home}/{action=Index}/{id?}");
			});
		}
	}
}
