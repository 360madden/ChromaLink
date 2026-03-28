using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
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

        if (analysis.Detection is not null)
        {
            builder.AppendLine($"Origin: {analysis.Detection.OriginX},{analysis.Detection.OriginY}");
            builder.AppendLine($"Pitch: {analysis.Detection.Pitch:F3}");
            builder.AppendLine($"Scale: {analysis.Detection.Scale:F3}");
            builder.AppendLine($"ControlError: {analysis.Detection.ControlError:F4}");
            builder.AppendLine($"SearchMode: {analysis.Detection.SearchMode}");
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
                $"  {sample.SegmentIndex:00}: symbol={sample.Symbol} confidence={sample.Confidence:F3} distance={sample.Distance:F3} rgb={sample.SampleColor.R},{sample.SampleColor.G},{sample.SampleColor.B}");
        }

        return builder.ToString();
    }
}

internal sealed class StripPreviewControl : Control
{
    private const int Zoom = 2;
    private Bitmap? _bitmap;
    private FrameValidationResult? _analysis;

    public StripPreviewControl()
    {
        DoubleBuffered = true;
    }

    public void SetFrame(Bgr24Frame frame, FrameValidationResult analysis)
    {
        _bitmap?.Dispose();
        _bitmap = ToBitmap(frame);
        _analysis = analysis;
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
        var detection = _analysis.Detection;
        var bandHeight = StripProfiles.Default.SegmentHeight * detection.Scale * Zoom;
        for (var segmentIndex = 0; segmentIndex <= StripProfiles.Default.SegmentCount; segmentIndex++)
        {
            var x = (float)((detection.OriginX + (segmentIndex * detection.Pitch)) * Zoom);
            e.Graphics.DrawLine(pen, x, detection.OriginY * Zoom, x, (float)(detection.OriginY * Zoom + bandHeight));
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
