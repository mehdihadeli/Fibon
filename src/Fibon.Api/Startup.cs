using System;
using System.Reflection;
using Fibon.Api.Framework;
using Fibon.Api.Handlers;
using Fibon.Messages.Events;
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

namespace Fibon.Api
{
    public class Startup
    {
        public Startup(IWebHostEnvironment env)
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
            services.AddSingleton<IRepository>(_ => new InMemoryRepository());
            ConfigureRabbitMq(services);
            //services.Configure<RabbitMqOptions>(Configuration.GetSection("rabbitmq"));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddSerilog();
            var serilogOptions = app.ApplicationServices.GetService<SerilogOptions>();
            var level = (LogEventLevel) Enum.Parse(typeof(LogEventLevel), serilogOptions.Level, true);
            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Is(level)
                .WriteTo.Elasticsearch(
                    new ElasticsearchSinkOptions(new Uri(serilogOptions.ApiUrl))
                    {
                        MinimumLogEventLevel = level,
                        AutoRegisterTemplate = true,
                        IndexFormat = string.IsNullOrWhiteSpace(serilogOptions.IndexFormat)
                            ? "logstash-{0:yyyy.MM.dd}"
                            : serilogOptions.IndexFormat,
                        ModifyConnectionSettings = x =>
                            serilogOptions.UseBasicAuth
                                ? x.BasicAuthentication(serilogOptions.Username, serilogOptions.Password)
                                : x
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
            var handler = app.ApplicationServices.GetService<IEventHandler<ValueCalculated>>();
            client.SubscribeAsync<ValueCalculated>(msg => handler.HandleAsync(msg),
                ctx => ctx.UseConsumerConfiguration(cfg =>
                    cfg.FromDeclaredQueue(q => q.WithName(GetExchangeName<ValueCalculated>()))));
        }

        private void ConfigureRabbitMq(IServiceCollection services)
        {
            var options = new RabbitMqOptions();
            var section = Configuration.GetSection("rabbitmq");
            section.Bind(options);

            var client = RawRabbitFactory.CreateSingleton(new RawRabbitOptions
            {
                ClientConfiguration = options
            });
            services.AddSingleton<IBusClient>(_ => client);
            services.AddScoped<IEventHandler<ValueCalculated>, ValueCalculatedHandler>();
        }

        private static string GetExchangeName<T>(string name = null)
            => string.IsNullOrWhiteSpace(name)
                ? $"{Assembly.GetEntryAssembly().GetName()}/{typeof(T).Name}"
                : $"{name}/{typeof(T).Name}";
    }
}