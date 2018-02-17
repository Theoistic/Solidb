using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using Wire;

namespace Solidb.Admin
{
    public static class SolidbWireExtension
    {
        public static void UseSolidbAdmin(this IApplicationBuilder app, string SQLConnectionString)
        {
            app.UseWire();
            Solidbase.Strategy = () => new SqlConnection(SQLConnectionString); // "Server=.\\SQLEXPRESS;Database=NewSolidb;Trusted_Connection=True;"
            API.GET("/admin/api/{Type}", x =>
            {
                Solidbase list = new Solidbase(x.Parameters.Type);
                return list.ToList();
            });
            API.GET("/admin/api/{Type}/{Id}", x =>
            {
                Solidbase list = new Solidbase(x.Parameters.Type);
                return list.FirstOrDefault(z => z.Id == x.Parameters.Id);
            });
            API.POST("/admin/api/{Type}", x =>
            {
                Solidbase list = new Solidbase(x.Parameters.Type);
                list.Add(x.Body.As<dynamic>());
                return list.ToList();
            });
            API.DELETE("/admin/api/{Type}/{Id}", x =>
            {
                Solidbase list = new Solidbase(x.Parameters.Type);
                var itm = list.FirstOrDefault(z => z.Id == x.Parameters.Id);
                list.Remove(itm);
                return true;
            });

            Assembly SolidbAdminAssembly = typeof(SolidbWireExtension).GetTypeInfo().Assembly;
            EmbeddedFileProvider SolidbAdminEmbeddedFileProvider = new EmbeddedFileProvider(
                SolidbAdminAssembly,
                "Solidb.Admin.wwwroot"
            );
            app.UseDefaultFiles(new DefaultFilesOptions
            {
                FileProvider = SolidbAdminEmbeddedFileProvider,
                RequestPath = new PathString("/Admin")
            });
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = SolidbAdminEmbeddedFileProvider,
                RequestPath = new PathString("/Admin")
            });

            AdminBuilder.Build(app);
        }
    }
}
