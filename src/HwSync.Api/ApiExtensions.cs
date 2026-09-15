using System.Net;
using System.Text.Json.Serialization;
using HwSync.Api.Controllers;
using HwSync.Api.Handlers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace HwSync.Api
{
    public static class ApiExtensions
    {
        public static IServiceCollection AddHwSyncApi(this IServiceCollection services)
        {
            services.AddTransient<ScanJobHandler>();
            services.AddControllers().AddApplicationPart(typeof(ScanJobsController).Assembly)
                .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
            return services;
        }

        public static void MapHwSyncApi(this WebApplication app)
        {
            // До появления сетевой аутентификации принимаем только локальные подключения.
            app.Use(static async (context, next) =>
            {
                if (context.Connection.RemoteIpAddress is not IPAddress address || !IPAddress.IsLoopback(address))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }
                await next(context);
            });
            app.MapControllers();
        }
    }
}
