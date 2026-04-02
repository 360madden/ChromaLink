using Microsoft.AspNetCore.TestHost;

public static class HttpBridgeApp
{
    public static WebApplication CreateApp(string[] args, bool useTestServer = false, string? snapshotPathOverride = null)
    {
        var builder = WebApplication.CreateSlimBuilder(args);
        if (useTestServer)
        {
            builder.WebHost.UseTestServer();
        }

        builder.WebHost.UseUrls(GetDefaultUrl());

        var app = builder.Build();
        var snapshotPath = ResolveSnapshotPath(snapshotPathOverride);

        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.MapGet("/dashboard", () => Results.Redirect("/"));
        app.MapGet("/latest-snapshot", () => HttpBridgeSnapshotService.TryReadRawSnapshot(snapshotPath).ToResult());
        app.MapGet("/snapshot", () => HttpBridgeSnapshotService.TryReadRawSnapshot(snapshotPath).ToResult());
        app.MapGet("/health", () =>
        {
            var document = HttpBridgeSnapshotService.BuildHealthDocument(snapshotPath);
            return Results.Json(HttpBridgeSnapshotService.BuildHealthPayload(document), HttpBridgeSnapshotService.JsonOptions, statusCode: document.Ok ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
        });
        app.MapGet("/ready", () =>
        {
            var document = HttpBridgeSnapshotService.BuildHealthDocument(snapshotPath);
            return Results.Json(HttpBridgeSnapshotService.BuildHealthPayload(document), HttpBridgeSnapshotService.JsonOptions, statusCode: document.Ok ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
        });

        return app;
    }

    private static string GetDefaultUrl()
    {
        var port = 7337;
        if (int.TryParse(Environment.GetEnvironmentVariable("CHROMALINK_HTTP_BRIDGE_PORT"), out var parsedPort) && parsedPort > 0)
        {
            port = parsedPort;
        }

        return $"http://127.0.0.1:{port}";
    }

    private static string ResolveSnapshotPath(string? snapshotPathOverride)
    {
        if (!string.IsNullOrWhiteSpace(snapshotPathOverride))
        {
            return snapshotPathOverride;
        }

        var overridePath = Environment.GetEnvironmentVariable("CHROMALINK_HTTP_BRIDGE_SNAPSHOT");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return overridePath;
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ChromaLink",
            "DesktopDotNet",
            "out",
            "chromalink-live-telemetry.json");
    }
}
