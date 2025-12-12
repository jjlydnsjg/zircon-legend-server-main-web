using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Server.Envir;
using Server.Web.Services;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Server.Web
{
    /// <summary>
    /// Admin Web Host - 管理后台 Web 主机
    /// 嵌入式 ASP.NET Core 服务器，与游戏服务器共进程运行
    /// </summary>
    public static class AdminWebHost
    {
        private static WebApplication? _app;

        /// <summary>
        /// 启动管理后台 Web 服务
        /// </summary>
        public static void Start()
        {
            // 直接输出到控制台，用于调试
            Console.WriteLine($"[Admin] ========== AdminWebHost.Start() 被调用 ==========");
            Console.WriteLine($"[Admin] AdminEnabled={Config.AdminEnabled}, AdminPort={Config.AdminPort}");

            SEnvir.Log($"[Admin] 正在启动管理后台... AdminEnabled={Config.AdminEnabled}, Port={Config.AdminPort}");

            if (!Config.AdminEnabled)
            {
                SEnvir.Log("[Admin] 管理后台已禁用 (AdminEnabled=false)");
                return;
            }

            try
            {
                // 确定源代码根目录（包含 Web/Pages 的目录）
                // 在开发环境中使用源码目录，在发布环境中使用 AppContext.BaseDirectory
                var contentRoot = AppContext.BaseDirectory;

                // 检查是否在开发环境（Web/Pages 目录在源码中）
                var sourceRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
                var sourcePagesPath = Path.Combine(sourceRoot, "Web", "Pages");
                if (Directory.Exists(sourcePagesPath))
                {
                    contentRoot = sourceRoot;
                    Console.WriteLine($"[Admin] 检测到开发环境，使用源码目录: {contentRoot}");
                }
                else
                {
                    Console.WriteLine($"[Admin] 检测到发布环境，使用运行目录: {contentRoot}");
                }

                var builder = WebApplication.CreateBuilder(new WebApplicationOptions
                {
                    ContentRootPath = contentRoot
                });

                // 禁用 ASP.NET Core 默认的控制台日志
                builder.Logging.ClearProviders();

                // 配置 Kestrel 监听端口
                builder.WebHost.UseUrls($"http://0.0.0.0:{Config.AdminPort}");

                // 配置 Razor Pages - 指定页面根目录
                var pagesPath = Path.Combine(AppContext.BaseDirectory, "Web", "Pages");
                Console.WriteLine($"[Admin] Razor Pages 路径: {pagesPath}, 存在: {Directory.Exists(pagesPath)}");

                builder.Services.AddRazorPages(options =>
                {
                    options.RootDirectory = "/Web/Pages";
                });

                // 配置响应压缩
                builder.Services.AddResponseCompression(options =>
                {
                    options.EnableForHttps = true;
                    options.Providers.Add<BrotliCompressionProvider>();
                    options.Providers.Add<GzipCompressionProvider>();
                    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
                    {
                        "text/css",
                        "application/javascript",
                        "text/javascript",
                        "application/json",
                        "image/svg+xml",
                        "font/woff2",
                        "font/woff"
                    });
                });

                builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
                {
                    options.Level = CompressionLevel.Fastest;
                });

                builder.Services.Configure<GzipCompressionProviderOptions>(options =>
                {
                    options.Level = CompressionLevel.Optimal;
                });

                // 配置 Cookie 认证
                builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                    .AddCookie(options =>
                    {
                        options.LoginPath = "/Login";
                        options.LogoutPath = "/Logout";
                        options.AccessDeniedPath = "/AccessDenied";
                        options.ExpireTimeSpan = TimeSpan.FromHours(24);
                        options.SlidingExpiration = true;
                        options.Cookie.Name = "ZirconAdmin";
                        options.Cookie.HttpOnly = true;
                    });

                builder.Services.AddAuthorization();

                // 注册业务服务
                builder.Services.AddSingleton<AdminAuthService>();
                builder.Services.AddSingleton<PlayerService>();
                builder.Services.AddSingleton<AccountService>();
                builder.Services.AddSingleton<ConfigService>();

                var app = builder.Build();

                // 启用响应压缩（必须在静态文件之前）
                app.UseResponseCompression();

                // 配置静态文件（带缓存头）
                var webRootPath = Path.Combine(contentRoot, "Web", "wwwroot");
                SEnvir.Log($"[Admin] 静态文件路径: {webRootPath}, 存在: {Directory.Exists(webRootPath)}");
                if (Directory.Exists(webRootPath))
                {
                    app.UseStaticFiles(new StaticFileOptions
                    {
                        FileProvider = new PhysicalFileProvider(webRootPath),
                        RequestPath = "",
                        OnPrepareResponse = ctx =>
                        {
                            var path = ctx.File.Name.ToLowerInvariant();

                            // CSS/JS/字体缓存 7 天
                            if (path.EndsWith(".css") || path.EndsWith(".js") ||
                                path.EndsWith(".woff2") || path.EndsWith(".woff") ||
                                path.EndsWith(".ttf") || path.EndsWith(".eot"))
                            {
                                ctx.Context.Response.Headers[HeaderNames.CacheControl] =
                                    "public, max-age=604800, immutable";
                            }
                            // 图片缓存 30 天
                            else if (path.EndsWith(".png") || path.EndsWith(".jpg") ||
                                     path.EndsWith(".gif") || path.EndsWith(".ico") ||
                                     path.EndsWith(".svg"))
                            {
                                ctx.Context.Response.Headers[HeaderNames.CacheControl] =
                                    "public, max-age=2592000";
                            }
                            // 其他资源缓存 1 小时
                            else
                            {
                                ctx.Context.Response.Headers[HeaderNames.CacheControl] =
                                    "public, max-age=3600";
                            }
                        }
                    });
                }

                // IP 白名单中间件
                if (!string.IsNullOrEmpty(Config.AdminAllowedIPs))
                {
                    app.Use(async (context, next) =>
                    {
                        var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "";
                        var allowedIps = Config.AdminAllowedIPs.Split(',', StringSplitOptions.RemoveEmptyEntries);

                        bool allowed = false;
                        foreach (var ip in allowedIps)
                        {
                            if (remoteIp.Contains(ip.Trim()) || ip.Trim() == "*")
                            {
                                allowed = true;
                                break;
                            }
                        }

                        if (!allowed && allowedIps.Length > 0)
                        {
                            context.Response.StatusCode = 403;
                            await context.Response.WriteAsync("Forbidden: IP not allowed");
                            return;
                        }

                        await next();
                    });
                }

                app.UseAuthentication();
                app.UseAuthorization();
                app.MapRazorPages();

                _app = app;

                // 非阻塞启动
                _ = app.RunAsync();

                Console.WriteLine($"[Admin] ========== 管理后台已启动 ==========");
                Console.WriteLine($"[Admin] 访问地址: http://localhost:{Config.AdminPort}");
                SEnvir.Log($"[Admin] 管理后台已启动: http://localhost:{Config.AdminPort}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Admin] ========== 管理后台启动失败 ==========");
                Console.WriteLine($"[Admin] 错误信息: {ex.Message}");
                Console.WriteLine($"[Admin] 详细堆栈: {ex}");
                SEnvir.Log($"[Admin] 管理后台启动失败: {ex.Message}");
                SEnvir.Log($"[Admin] 详细错误: {ex}");
            }
        }

        /// <summary>
        /// 停止管理后台 Web 服务
        /// </summary>
        public static void Stop()
        {
            try
            {
                _app?.StopAsync().Wait(TimeSpan.FromSeconds(5));
                SEnvir.Log("[Admin] 管理后台已停止");
            }
            catch (Exception ex)
            {
                SEnvir.Log($"[Admin] 管理后台停止失败: {ex.Message}");
            }
        }
    }
}
