using System.Drawing;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace ChromaLink.Monitor;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MonitorForm(args));
    }
}

internal sealed class MonitorForm : Form
{
    private static readonly string DefaultSnapshotPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ChromaLink",
        "DesktopDotNet",
        "out",
        "chromalink-live-telemetry.json");

    private readonly Label _title = new()
    {
        AutoSize = true,
        Text = "ChromaLink Live Monitor",
        Font = new Font("Segoe UI", 16.0f, FontStyle.Bold),
        ForeColor = Color.White
    };

    private readonly Label _contract = new() { AutoSize = true, ForeColor = Color.Gainsboro };
    private readonly Label _freshness = new() { AutoSize = true, ForeColor = Color.Gainsboro };
    private readonly Label _readiness = new() { AutoSize = true, ForeColor = Color.Gainsboro };
    private readonly Label _pathLabel = new() { AutoSize = true, ForeColor = Color.Gainsboro };
    private readonly Label _coreLine = new() { AutoSize = true, ForeColor = Color.WhiteSmoke };
    private readonly Label _vitalsLine = new() { AutoSize = true, ForeColor = Color.WhiteSmoke };
    private readonly Label _positionLine = new() { AutoSize = true, ForeColor = Color.WhiteSmoke };
    private readonly Label _metricsLine = new() { AutoSize = true, ForeColor = Color.WhiteSmoke };
    private readonly Label _statusLine = new() { AutoSize = true, ForeColor = Color.Gainsboro };
    private readonly TextBox _details = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Both,
        Font = new Font("Consolas", 9.0f),
        BorderStyle = BorderStyle.FixedSingle
    };
    private readonly Timer _refreshTimer = new() { Interval = 750 };

    private string _snapshotPath = DefaultSnapshotPath;
    private DateTime _snapshotWriteTimeUtc;
    private bool _startMinimized;

    public MonitorForm(string[] args)
    {
        Text = "ChromaLink Monitor";
        Width = 1120;
        Height = 760;
        BackColor = Color.FromArgb(24, 28, 36);
        ForeColor = Color.White;
        StartPosition = FormStartPosition.CenterScreen;
        ParseArgs(args);

        var openButton = new Button
        {
            AutoSize = true,
            Text = "Open Snapshot..."
        };
        openButton.Click += (_, _) => OpenSnapshot();

        var reloadButton = new Button
        {
            AutoSize = true,
            Text = "Reload"
        };
        reloadButton.Click += (_, _) => RefreshSnapshot(force: true);

        var topRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            BackColor = Color.FromArgb(30, 36, 46),
            Padding = new Padding(12)
        };
        topRow.Controls.Add(openButton);
        topRow.Controls.Add(reloadButton);
        topRow.Controls.Add(new Label
        {
            AutoSize = true,
            Text = " ",
            Width = 8
        });
        topRow.Controls.Add(_pathLabel);

        var headline = new Panel
        {
            Dock = DockStyle.Top,
            Height = 160,
            Padding = new Padding(16),
            BackColor = Color.FromArgb(18, 22, 30)
        };
        headline.Controls.Add(_statusLine);
        headline.Controls.Add(_metricsLine);
        headline.Controls.Add(_positionLine);
        headline.Controls.Add(_vitalsLine);
        headline.Controls.Add(_coreLine);
        headline.Controls.Add(_readiness);
        headline.Controls.Add(_freshness);
        headline.Controls.Add(_contract);
        headline.Controls.Add(_title);

        _title.Location = new Point(0, 0);
        _contract.Location = new Point(0, 36);
        _freshness.Location = new Point(0, 58);
        _readiness.Location = new Point(0, 80);
        _coreLine.Location = new Point(0, 108);
        _vitalsLine.Location = new Point(0, 130);
        _positionLine.Location = new Point(0, 152);
        _metricsLine.Location = new Point(0, 174);
        _statusLine.Location = new Point(0, 196);
        headline.Height = 224;

        var detailsHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            BackColor = Color.FromArgb(28, 32, 42)
        };
        detailsHost.Controls.Add(_details);

        Controls.Add(detailsHost);
        Controls.Add(headline);
        Controls.Add(topRow);

        _refreshTimer.Tick += (_, _) => RefreshSnapshot(force: false);
        _refreshTimer.Start();

        RefreshSnapshot(force: true);

        if (_startMinimized)
        {
            Shown += (_, _) =>
            {
                WindowState = FormWindowState.Minimized;
            };
        }
    }

    private void ParseArgs(string[] args)
    {
        foreach (var arg in args)
        {
            if (string.Equals(arg, "--start-minimized", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--minimized", StringComparison.OrdinalIgnoreCase))
            {
                _startMinimized = true;
                continue;
            }

            if (File.Exists(arg))
            {
                _snapshotPath = arg;
            }
        }
    }

    private void OpenSnapshot()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            Title = "Open ChromaLink Live Snapshot",
            FileName = Path.GetFileName(_snapshotPath)
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _snapshotPath = dialog.FileName;
        RefreshSnapshot(force: true);
    }

    private void RefreshSnapshot(bool force)
    {
        if (!File.Exists(_snapshotPath))
        {
            _pathLabel.Text = $"Snapshot: not found at {_snapshotPath}";
            _statusLine.Text = "Waiting for chromalink-live-telemetry.json...";
            _details.Text = "No live snapshot is available yet.";
            return;
        }

        var writeTime = File.GetLastWriteTimeUtc(_snapshotPath);
        if (!force && writeTime == _snapshotWriteTimeUtc)
        {
            return;
        }

        _snapshotWriteTimeUtc = writeTime;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(_snapshotPath));
            var root = document.RootElement;
            _pathLabel.Text = $"Snapshot: {_snapshotPath}";
            _statusLine.Text = BuildStatusLine(root);
            _contract.Text = BuildContractLine(root);
            _freshness.Text = BuildFreshnessLine(root);
            _readiness.Text = BuildReadinessLine(root);
            _coreLine.Text = BuildFrameLine(root, "coreStatus", "CoreStatus");
            _vitalsLine.Text = BuildFrameLine(root, "playerVitals", "PlayerVitals");
            _positionLine.Text = BuildFrameLine(root, "playerPosition", "PlayerPosition");
            _metricsLine.Text = BuildMetricsLine(root);
            _details.Text = BuildDetails(root);
        }
        catch (Exception ex)
        {
            _statusLine.Text = $"Failed to parse snapshot: {ex.Message}";
            _details.Text = ex.ToString();
        }
    }

    private static string BuildStatusLine(JsonElement root)
    {
        var generated = root.TryGetProperty("generatedAtUtc", out var generatedAtUtc)
            ? generatedAtUtc.GetDateTimeOffset()
            : DateTimeOffset.MinValue;
        var age = generated == DateTimeOffset.MinValue ? TimeSpan.Zero : DateTimeOffset.UtcNow - generated;
        if (age < TimeSpan.Zero)
        {
            age = TimeSpan.Zero;
        }

        return $"Updated {Math.Round(age.TotalSeconds, 1):F1}s ago";
    }

    private static string BuildContractLine(JsonElement root)
    {
        if (!root.TryGetProperty("contract", out var contract))
        {
            return "Contract: unavailable";
        }

        var name = contract.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : "unknown";
        var version = contract.TryGetProperty("schemaVersion", out var versionProp) ? versionProp.GetInt32() : 0;
        return $"Contract: {name}/v{version}";
    }

    private static string BuildFreshnessLine(JsonElement root)
    {
        if (!root.TryGetProperty("aggregate", out var aggregate) || !aggregate.TryGetProperty("freshness", out var freshness))
        {
            return "Freshness: unavailable";
        }

        var healthy = aggregate.TryGetProperty("healthy", out var healthyProp) && healthyProp.GetBoolean();
        var stale = aggregate.TryGetProperty("stale", out var staleProp) && staleProp.GetBoolean();
        var ageMs = freshness.TryGetProperty("lastUpdatedAgeMs", out var ageProp) && ageProp.ValueKind != JsonValueKind.Null
            ? ageProp.GetDouble().ToString("F0")
            : "n/a";
        var windowMs = freshness.TryGetProperty("windowMs", out var windowProp) ? windowProp.GetDouble().ToString("F0") : "n/a";

        return $"Freshness: healthy={healthy} stale={stale} age={ageMs}ms window={windowMs}ms";
    }

    private static string BuildReadinessLine(JsonElement root)
    {
        if (!root.TryGetProperty("aggregate", out var aggregate))
        {
            return "Readiness: unavailable";
        }

        var ready = aggregate.TryGetProperty("ready", out var readyProp) && readyProp.GetBoolean();
        var acceptedFrames = aggregate.TryGetProperty("acceptedFrames", out var acceptedProp) ? acceptedProp.GetInt32() : 0;
        return $"Readiness: ready={ready} acceptedFrames={acceptedFrames}";
    }

    private static string BuildFrameLine(JsonElement root, string propertyName, string label)
    {
        if (!root.TryGetProperty("aggregate", out var aggregate) || !aggregate.TryGetProperty(propertyName, out var frame) || frame.ValueKind != JsonValueKind.Object)
        {
            return $"{label}: missing";
        }

        var sequence = frame.TryGetProperty("sequence", out var sequenceProp) ? sequenceProp.GetInt32().ToString() : "n/a";
        var ageMs = frame.TryGetProperty("ageMs", out var ageProp) ? ageProp.GetDouble().ToString("F0") : "n/a";
        var fresh = frame.TryGetProperty("fresh", out var freshProp) && freshProp.GetBoolean();
        var stale = frame.TryGetProperty("stale", out var staleProp) && staleProp.GetBoolean();
        return $"{label}: seq={sequence} age={ageMs}ms fresh={fresh} stale={stale}";
    }

    private static string BuildMetricsLine(JsonElement root)
    {
        if (!root.TryGetProperty("metrics", out var metrics))
        {
            return "Metrics: unavailable";
        }

        var accepted = metrics.TryGetProperty("acceptedSamples", out var acceptedProp) ? acceptedProp.GetInt32() : 0;
        var rejected = metrics.TryGetProperty("rejectedSamples", out var rejectedProp) ? rejectedProp.GetInt32() : 0;
        var backend = root.TryGetProperty("lastBackend", out var backendProp) ? backendProp.GetString() : "n/a";
        return $"Metrics: accepted={accepted} rejected={rejected} backend={backend}";
    }

    private static string BuildDetails(JsonElement root)
    {
        var builder = new StringBuilder();
        builder.AppendLine("ChromaLink Monitor");
        builder.AppendLine(BuildContractLine(root));
        builder.AppendLine(BuildFreshnessLine(root));
        builder.AppendLine(BuildReadinessLine(root));
        builder.AppendLine(BuildFrameLine(root, "coreStatus", "CoreStatus"));
        builder.AppendLine(BuildFrameLine(root, "playerVitals", "PlayerVitals"));
        builder.AppendLine(BuildFrameLine(root, "playerPosition", "PlayerPosition"));
        builder.AppendLine(BuildMetricsLine(root));

        if (root.TryGetProperty("aggregate", out var aggregate) && aggregate.TryGetProperty("freshness", out var freshness))
        {
            builder.AppendLine("Aggregate Freshness:");
            AppendProperty(builder, freshness, "windowMs");
            AppendProperty(builder, freshness, "lastUpdatedAgeMs");
            AppendProperty(builder, freshness, "oldestFrameAgeMs");
            AppendProperty(builder, freshness, "newestFrameAgeMs");
            AppendProperty(builder, freshness, "freshFrameCount");
            AppendProperty(builder, freshness, "staleFrameCount");
        }

        if (root.TryGetProperty("metrics", out var metrics) && metrics.TryGetProperty("frameTypeCounts", out var counts) && counts.ValueKind == JsonValueKind.Object)
        {
            builder.AppendLine("Frame Counts:");
            foreach (var property in counts.EnumerateObject())
            {
                builder.AppendLine($"  {property.Name}: {property.Value}");
            }
        }

        return builder.ToString();
    }

    private static void AppendProperty(StringBuilder builder, JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return;
        }

        builder.AppendLine($"  {propertyName}: {property}");
    }
}
