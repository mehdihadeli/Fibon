﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Fibon.Messages;
using Fibon.Messages.Commands;
using Fibon.Service.Framework;
using Fibon.Service.Handlers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RawRabbit;
using RawRabbit.Instantiation;
using RawRabbit.Pipe;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Elasticsearch;

namespace Fibon.Service
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add framework services.
            var serilogOptions = new SerilogOptions();
            Configuration.GetSection("serilog").Bind(serilogOptions);
            services.AddSingleton<SerilogOptions>(serilogOptions);
            services.AddLogging();
            services.AddControllers();
            ConfigureRabbitMq(services);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment  env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddSerilog();
            var serilogOptions = app.ApplicationServices.GetService<SerilogOptions>();
            var level = (LogEventLevel)Enum.Parse(typeof(LogEventLevel), serilogOptions.Level, true);
            Log.Logger = new LoggerConfiguration()
               .Enrich.FromLogContext()
               .MinimumLevel.Is(level)
               .WriteTo.Elasticsearch(
                    new ElasticsearchSinkOptions(new Uri(serilogOptions.ApiUrl))
                    {
                        MinimumLogEventLevel = level,
                        AutoRegisterTemplate = true,
                        IndexFormat = string.IsNullOrWhiteSpace(serilogOptions.IndexFormat) ? 
                            "logstash-{0:yyyy.MM.dd}" : 
                            serilogOptions.IndexFormat,
                        ModifyConnectionSettings = x => 
                            serilogOptions.UseBasicAuth ? 
                            x.BasicAuthentication(serilogOptions.Username, serilogOptions.Password) : 
                            x
                    }) 
               .CreateLogger();
            app.UseRouting();
            
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
            ConfigureRabbitMqSubscriptions(app);
        }

        private void ConfigureRabbitMqSubscriptions(IApplicationBuilder app)
        {
            IBusClient client = app.ApplicationServices.GetService<IBusClient>();
            var handler = app.ApplicationServices.GetService<ICommandHandler<CalculateValue>>();
            client.SubscribeAsync<CalculateValue>(msg => handler.HandleAsync(msg),
                ctx => ctx.UseConsumerConfiguration(cfg => 
                    cfg.FromDeclaredQueue(q => q.WithName(GetExchangeName<CalculateValue>()))));
        }

        private void ConfigureRabbitMq(IServiceCollection services)
        {
            var options = new RabbitMqOptions();
            var section = Configuration.GetSection("rabbitmq");
            section.Bind(options);

            var client = RawRabbitFactory.CreateSingleton(new RawRabbitOptions
            {
                ClientConfiguration  = options
            });
            services.AddSingleton<IBusClient>(_ => client);
            services.AddSingleton<ICalculator>(_ => new Fast());
            services.AddTransient<ICommandHandler<CalculateValue>, CalculateValueHandler>();
        }

        private static string GetExchangeName<T>(string name = null)
            => string.IsNullOrWhiteSpace(name)
                ? $"{Assembly.GetEntryAssembly().GetName()}/{typeof(T).Name}"
                : $"{name}/{typeof(T).Name}";
    }
}
