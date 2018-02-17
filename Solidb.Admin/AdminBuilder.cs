using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;
using Wire;

namespace Solidb.Admin
{
    public static class AdminBuilder
    {
        public static void Build(IApplicationBuilder app)
        {
            string adminScript = "var myApp = angular.module('myApp', ['ng-admin']); myApp.config(['NgAdminConfigurationProvider', function(NgAdminConfigurationProvider) { var nga = NgAdminConfigurationProvider; var admin = nga.application('My First Admin'); nga.configure(admin); }]); ";
            app.Use(async (context, next) =>
            {
                if (context.Request.Path.Value.ToLower() == "/admin/admin.js")
                {
                    context.Response.ContentType = "application/javascript";
                    await context.Response.WriteAsync(adminScript);
                }
                else
                {
                    // Do work that doesn't write to the Response.
                    await next.Invoke();
                    // Do logging or other work that doesn't write to the Response.
                }
            });
        }
    }
}
