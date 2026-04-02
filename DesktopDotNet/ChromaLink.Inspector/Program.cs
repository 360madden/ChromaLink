using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using ChromaLink.Reader;
using Timer = System.Windows.Forms.Timer;

namespace ChromaLink.Inspector;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new InspectorForm(args));
    }
}

internal sealed class InspectorForm : Form
{
    private readonly StripPreviewControl _preview = new();
    private readonly TextBox _details = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Both,
        Font = new Font("Consolas", 9.0f)
    };
    private readonly Label _pathLabel = new() { AutoSize = true, Text = "No capture loaded." };
    private readonly CheckBox _autoRefreshCheckBox = new() { AutoSize = true, Text = "Auto refresh latest capture" };
    private readonly Timer _refreshTimer = new() { Interval = 1000 };

    private string? _currentPath;
    private DateTime _currentWriteTimeUtc;

    public InspectorForm(string[] args)
    {
        Text = "ChromaLink Inspector";
        Width = 1280;
        Height = 800;

        var openButton = new Button { AutoSize = true, Text = "Open BMP..." };
        openButton.Click += (_, _) => OpenBmp();

        var latestButton = new Button { AutoSize = true, Text = "Load Latest" };
        latestButton.Click += (_, _) => LoadLatestCapture(force: true);

        var topBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true
        };
        topBar.Controls.Add(openButton);
        topBar.Controls.Add(latestButton);
        topBar.Controls.Add(_autoRefreshCheckBox);
        topBar.Controls.Add(_pathLabel);

        var previewHost = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true
        };
        previewHost.Controls.Add(_preview);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 820
        };
        split.Panel1.Controls.Add(previewHost);
        split.Panel2.Controls.Add(_details);

        Controls.Add(split);
        Controls.Add(topBar);

        _refreshTimer.Tick += (_, _) =>
        {
            if (_autoRefreshCheckBox.Checked)
            {
                LoadLatestCapture(force: false);
            }
        };
        _refreshTimer.Start();

        if (args.Length > 0 && File.Exists(args[0]))
        {
            LoadFromPath(args[0]);
        }
        else
        {
            LoadLatestCapture(force: true);
        }
    }

    private void OpenBmp()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "BMP files (*.bmp)|*.bmp|All files (*.*)|*.*",
            Title = "Open ChromaLink Capture"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            LoadFromPath(dialog.FileName);
        }
    }

    private void LoadLatestCapture(bool force)
    {
        var latest = PathProvider.GetLatestCapturePath();
        if (string.IsNullOrWhiteSpace(latest) || !File.Exists(latest))
        {
            return;
        }

        var writeTime = File.GetLastWriteTimeUtc(latest);
        if (!force && string.Equals(latest, _currentPath, StringComparison.OrdinalIgnoreCase) && writeTime == _currentWriteTimeUtc)
        {
            return;
        }

        LoadFromPath(latest);
    }

    private void LoadFromPath(string path)
    {
        var frame = BmpIO.Load(path);
        var analysis = ColorStripAnalyzer.Analyze(frame, StripProfiles.Default);
        _preview.SetFrame(frame, analysis);
        _details.Text = BuildSummary(path, frame, analysis);
        _pathLabel.Text = path;
        _currentPath = path;
        _currentWriteTimeUtc = File.GetLastWriteTimeUtc(path);
    }

    private static string BuildSummary(string path, Bgr24Frame frame, FrameValidationResult analysis)
    {
        var builder = new StringBuilder();
        builder.AppendLine("ChromaLink Inspector");
        builder.AppendLine($"Path: {path}");
        builder.AppendLine($"Size: {frame.Width}x{frame.Height}");
        builder.AppendLine($"Accepted: {analysis.IsAccepted}");
        builder.AppendLine($"Reason: {analysis.Reason}");

        var sidecarPath = Path.ChangeExtension(path, ".json");
        if (File.Exists(sidecarPath))
        {
            builder.AppendLine($"Sidecar: {sidecarPath}");
            AppendSidecarSummary(builder, sidecarPath);
        }

        if (analysis.Detection is not null)
        {
            builder.AppendLine($"Origin: {analysis.Detection.OriginX},{analysis.Detection.OriginY}");
            builder.AppendLine($"Pitch: {analysis.Detection.Pitch:F3}");
            builder.AppendLine($"Scale: {analysis.Detection.Scale:F3}");
            builder.AppendLine($"ControlError: {analysis.Detection.ControlError:F4}");
            builder.AppendLine($"LeftControlScore: {analysis.Detection.LeftControlScore:F4}");
            builder.AppendLine($"RightControlScore: {analysis.Detection.RightControlScore:F4}");
            builder.AppendLine($"AnchorLumaDelta: {analysis.Detection.AnchorLumaDelta:F2}");
            builder.AppendLine($"SearchMode: {analysis.Detection.SearchMode}");
            builder.AppendLine($"LeftExpected: {FormatPattern(StripProfiles.Default.LeftControl)}");
            builder.AppendLine($"LeftObserved: {FormatObservedPattern(analysis.Samples, 0, StripProfiles.Default.LeftControl.Length)}");
            builder.AppendLine($"RightExpected: {FormatPattern(StripProfiles.Default.RightControl)}");
            builder.AppendLine($"RightObserved: {FormatObservedPattern(analysis.Samples, StripProfiles.Default.SegmentCount - StripProfiles.Default.RightControl.Length, StripProfiles.Default.RightControl.Length)}");
        }

        if (analysis.ParseResult is not null)
        {
            builder.AppendLine("Transport:");
            builder.AppendLine($"  ParseAccepted: {analysis.ParseResult.IsAccepted}");
            builder.AppendLine($"  ParseReason: {analysis.ParseResult.Reason}");
            builder.AppendLine($"  MagicValid: {analysis.ParseResult.MagicValid}");
            builder.AppendLine($"  ProtocolProfileValid: {analysis.ParseResult.ProtocolProfileValid}");
            builder.AppendLine($"  FrameSchemaValid: {analysis.ParseResult.FrameSchemaValid}");
            builder.AppendLine($"  HeaderCrcValid: {analysis.ParseResult.HeaderCrcValid}");
            builder.AppendLine($"  PayloadCrcValid: {analysis.ParseResult.PayloadCrcValid}");
            builder.AppendLine($"  TransportBytes: {BitConverter.ToString(analysis.ParseResult.TransportBytes)}");
        }

        if (analysis.Frame is not null)
        {
            builder.AppendLine("Header:");
            builder.AppendLine($"  Protocol: {analysis.Frame.Header.ProtocolVersion}");
            builder.AppendLine($"  Profile: {analysis.Frame.Header.ProfileId}");
            builder.AppendLine($"  FrameType: {analysis.Frame.Header.FrameType}");
            builder.AppendLine($"  Schema: {analysis.Frame.Header.SchemaId}");
            builder.AppendLine($"  Sequence: {analysis.Frame.Header.Sequence}");
            builder.AppendLine("Payload:");
            builder.AppendLine($"  PlayerFlags: {analysis.Frame.Payload.PlayerStateFlags}");
            builder.AppendLine($"  PlayerHealthPctQ8: {analysis.Frame.Payload.PlayerHealthPctQ8}");
            builder.AppendLine($"  PlayerResourceKind: {analysis.Frame.Payload.PlayerResourceKind}");
            builder.AppendLine($"  PlayerResourcePctQ8: {analysis.Frame.Payload.PlayerResourcePctQ8}");
            builder.AppendLine($"  TargetFlags: {analysis.Frame.Payload.TargetStateFlags}");
            builder.AppendLine($"  TargetHealthPctQ8: {analysis.Frame.Payload.TargetHealthPctQ8}");
            builder.AppendLine($"  TargetResourceKind: {analysis.Frame.Payload.TargetResourceKind}");
            builder.AppendLine($"  TargetResourcePctQ8: {analysis.Frame.Payload.TargetResourcePctQ8}");
            builder.AppendLine($"  PlayerLevel: {analysis.Frame.Payload.PlayerLevel}");
            builder.AppendLine($"  TargetLevel: {analysis.Frame.Payload.TargetLevel}");
            builder.AppendLine($"  PlayerCalling: {analysis.Frame.Payload.PlayerCallingRolePacked >> 4}");
            builder.AppendLine($"  PlayerRole: {analysis.Frame.Payload.PlayerCallingRolePacked & 0x0F}");
            builder.AppendLine($"  TargetCalling: {analysis.Frame.Payload.TargetCallingRelationPacked >> 4}");
            builder.AppendLine($"  TargetRelation: {analysis.Frame.Payload.TargetCallingRelationPacked & 0x0F}");
        }

        builder.AppendLine("Segments:");
        foreach (var sample in analysis.Samples)
        {
            builder.AppendLine(
                $"  {sample.SegmentIndex:00}: symbol={sample.Symbol} confidence={sample.Confidence:F3} distance={sample.Distance:F3} second={sample.SecondChoiceSymbol}/{sample.SecondChoiceDistance:F3} rgb={sample.SampleColor.R},{sample.SampleColor.G},{sample.SampleColor.B}");
            foreach (var probe in sample.Probes)
            {
                builder.AppendLine(
                    $"      probe=({probe.X},{probe.Y}) rgb={probe.SampleColor.R},{probe.SampleColor.G},{probe.SampleColor.B}");
            }
        }

        return builder.ToString();
    }

    private static void AppendSidecarSummary(StringBuilder builder, string sidecarPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(sidecarPath));
        var root = document.RootElement;

        if (root.TryGetProperty("backend", out var backend))
        {
            builder.AppendLine($"Backend: {backend.GetString()}");
        }

        if (root.TryGetProperty("clientRect", out var clientRect))
        {
            builder.AppendLine(
                $"ClientRect: {clientRect.GetProperty("left").GetInt32()},{clientRect.GetProperty("top").GetInt32()} {clientRect.GetProperty("width").GetInt32()}x{clientRect.GetProperty("height").GetInt32()}");
        }

        if (root.TryGetProperty("captureRect", out var captureRect))
        {
            builder.AppendLine(
                $"CaptureRect: {captureRect.GetProperty("left").GetInt32()},{captureRect.GetProperty("top").GetInt32()} {captureRect.GetProperty("width").GetInt32()}x{captureRect.GetProperty("height").GetInt32()}");
        }

        if (root.TryGetProperty("observerLane", out var observerLane) && observerLane.ValueKind == JsonValueKind.Object)
        {
            builder.AppendLine(
                $"ObserverLane: visible={observerLane.GetProperty("probablyVisible").GetBoolean()} matched={observerLane.GetProperty("matchedMarkers").GetInt32()}/{observerLane.GetProperty("totalMarkers").GetInt32()} conf={observerLane.GetProperty("averageConfidence").GetDouble():F3}");
            builder.AppendLine($"ObserverExpected: {observerLane.GetProperty("expectedPattern").GetString()}");
            builder.AppendLine($"ObserverObserved: {observerLane.GetProperty("observedPattern").GetString()}");

            if (observerLane.TryGetProperty("markers", out var markers) && markers.ValueKind == JsonValueKind.Array)
            {
                foreach (var marker in markers.EnumerateArray())
                {
                    builder.AppendLine(
                        $"  marker[{marker.GetProperty("markerIndex").GetInt32()}]: expected={marker.GetProperty("expectedSymbol").GetInt32()} observed={marker.GetProperty("observedSymbol").GetInt32()} conf={marker.GetProperty("confidence").GetDouble():F3} rect={marker.GetProperty("left").GetInt32()},{marker.GetProperty("top").GetInt32()} {marker.GetProperty("width").GetInt32()}x{marker.GetProperty("height").GetInt32()}");
                }
            }
        }
    }

    private static string FormatPattern(IEnumerable<byte> symbols)
    {
        return string.Join(" ", symbols.Select(static symbol => symbol.ToString()));
    }

    private static string FormatObservedPattern(IReadOnlyList<SegmentSample> samples, int start, int length)
    {
        return string.Join(
            " ",
            samples
                .Where(sample => sample.SegmentIndex >= start && sample.SegmentIndex < start + length)
                .OrderBy(sample => sample.SegmentIndex)
                .Select(sample => sample.Symbol.ToString()));
    }
}

internal sealed class StripPreviewControl : Control
{
    private const int Zoom = 2;
    private Bitmap? _bitmap;
    private FrameValidationResult? _analysis;
    private ObserverLaneReport? _observerReport;

    public StripPreviewControl()
    {
        DoubleBuffered = true;
    }

    public void SetFrame(Bgr24Frame frame, FrameValidationResult analysis)
    {
        _bitmap?.Dispose();
        _bitmap = ToBitmap(frame);
        _analysis = analysis;
        _observerReport = ObserverLaneAnalyzer.Analyze(frame, StripProfiles.Default, analysis.Detection);
        Width = frame.Width * Zoom;
        Height = frame.Height * Zoom;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_bitmap is null)
        {
            return;
        }

        e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        e.Graphics.DrawImage(_bitmap, new Rectangle(0, 0, _bitmap.Width * Zoom, _bitmap.Height * Zoom));

        if (_analysis?.Detection is null)
        {
            return;
        }

        using var pen = new Pen(Color.LimeGreen, 1);
        using var roiPen = new Pen(Color.Gold, 1);
        using var probeBrush = new SolidBrush(Color.DeepPink);
        using var observerMatchPen = new Pen(Color.DeepSkyBlue, 1);
        using var observerMismatchPen = new Pen(Color.OrangeRed, 1);
        using var observerCenterBrush = new SolidBrush(Color.Cyan);
        var detection = _analysis.Detection;
        var bandHeight = StripProfiles.Default.SegmentHeight * detection.Scale * Zoom;
        var bandWidth = StripProfiles.Default.SegmentCount * detection.Pitch * Zoom;
        e.Graphics.DrawRectangle(
            roiPen,
            (float)(detection.OriginX * Zoom),
            (float)(detection.OriginY * Zoom),
            (float)bandWidth,
            (float)bandHeight);
        for (var segmentIndex = 0; segmentIndex <= StripProfiles.Default.SegmentCount; segmentIndex++)
        {
            var x = (float)((detection.OriginX + (segmentIndex * detection.Pitch)) * Zoom);
            e.Graphics.DrawLine(pen, x, detection.OriginY * Zoom, x, (float)(detection.OriginY * Zoom + bandHeight));
        }

        foreach (var sample in _analysis.Samples)
        {
            foreach (var probe in sample.Probes)
            {
                e.Graphics.FillEllipse(probeBrush, (probe.X * Zoom) - 2, (probe.Y * Zoom) - 2, 4, 4);
            }
        }

        if (_observerReport is null)
        {
            return;
        }

        foreach (var marker in _observerReport.Markers)
        {
            var penToUse = marker.ExpectedSymbol == marker.ObservedSymbol ? observerMatchPen : observerMismatchPen;
            e.Graphics.DrawRectangle(
                penToUse,
                marker.Left * Zoom,
                marker.Top * Zoom,
                Math.Max(1, (marker.Width * Zoom) - 1),
                Math.Max(1, (marker.Height * Zoom) - 1));
            e.Graphics.FillEllipse(observerCenterBrush, (marker.CenterX * Zoom) - 2, (marker.CenterY * Zoom) - 2, 4, 4);
        }
    }

    private static Bitmap ToBitmap(Bgr24Frame frame)
    {
        var bitmap = new Bitmap(frame.Width, frame.Height, PixelFormat.Format24bppRgb);
        for (var y = 0; y < frame.Height; y++)
        {
            for (var x = 0; x < frame.Width; x++)
            {
                var color = frame.GetColor(x, y);
                bitmap.SetPixel(x, y, Color.FromArgb(color.R, color.G, color.B));
            }
        }

        return bitmap;
    }
}
