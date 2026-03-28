using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ChromaLink.Reader;

public enum CaptureBackend
{
    ScreenBitBlt,
    PrintWindow
}

public sealed record CaptureResult(
    Bgr24Frame Image,
    CaptureBackend Backend,
    int ClientX,
    int ClientY,
    int ClientWidth,
    int ClientHeight,
    int CaptureLeft,
    int CaptureTop,
    int CaptureWidth,
    int CaptureHeight,
    string RouteReason);

public static class WindowCaptureService
{
    public static nint FindRiftWindow()
    {
        var process = Process
            .GetProcesses()
            .FirstOrDefault(static candidate =>
                candidate.MainWindowHandle != nint.Zero &&
                (candidate.ProcessName.Contains("rift", StringComparison.OrdinalIgnoreCase)
                 || (candidate.MainWindowTitle?.Contains("RIFT", StringComparison.OrdinalIgnoreCase) ?? false)));

        return process?.MainWindowHandle ?? nint.Zero;
    }

    public static CaptureResult CaptureTopSlice(nint hwnd, StripProfile profile, int heightPadding, CaptureBackend backend)
    {
        if (IsIconic(hwnd))
        {
            throw new InvalidOperationException("The RIFT window is minimized.");
        }

        var clientRect = TryGetClientRectOnScreen(hwnd);
        var windowRect = GetWindowRectOnScreen(hwnd);
        var sourceRect = clientRect is { Width: > 0, Height: > 0 } ? clientRect.Value : windowRect;
        if (sourceRect.Width <= 0 || sourceRect.Height <= 0)
        {
            throw new InvalidOperationException("Could not resolve a valid RIFT capture rectangle.");
        }

        if (sourceRect.Width < profile.BandWidth || sourceRect.Height < profile.BandHeight)
        {
            throw new InvalidOperationException(
                $"The RIFT capture rectangle is too small for {profile.Id}: {sourceRect.Width}x{sourceRect.Height}.");
        }

        var captureHeight = Math.Min(sourceRect.Height, Math.Max(profile.BandHeight, profile.BandHeight + heightPadding));
        return backend switch
        {
            CaptureBackend.ScreenBitBlt => CaptureScreen(sourceRect.X, sourceRect.Y, sourceRect.Width, captureHeight, sourceRect, backend),
            CaptureBackend.PrintWindow => CapturePrintWindow(hwnd, sourceRect.Width, sourceRect.Height, captureHeight, sourceRect, backend),
            _ => throw new ArgumentOutOfRangeException(nameof(backend))
        };
    }

    private static CaptureResult CaptureScreen(int left, int top, int width, int height, NativeRect clientRect, CaptureBackend backend)
    {
        var image = CaptureBitmap(width, height, (hdc) =>
        {
            var screenDc = GetDC(nint.Zero);
            try
            {
                if (!BitBlt(hdc, 0, 0, width, height, screenDc, left, top, 0x40CC0020))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "BitBlt failed.");
                }
            }
            finally
            {
                _ = ReleaseDC(nint.Zero, screenDc);
            }
        });

        return new CaptureResult(
            image with { CaptureRouteReason = "screen" },
            backend,
            clientRect.X,
            clientRect.Y,
            clientRect.Width,
            clientRect.Height,
            left,
            top,
            width,
            height,
            "screen");
    }

    private static CaptureResult CapturePrintWindow(nint hwnd, int clientWidth, int clientHeight, int captureHeight, NativeRect clientRect, CaptureBackend backend)
    {
        var full = CaptureBitmap(clientWidth, clientHeight, (hdc) =>
        {
            if (!PrintWindow(hwnd, hdc, 0x00000003) && !PrintWindow(hwnd, hdc, 0x00000001))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "PrintWindow failed.");
            }
        });

        var cropped = full.Crop(0, 0, clientWidth, captureHeight, "printwindow");
        return new CaptureResult(
            cropped with { CaptureRouteReason = "printwindow" },
            backend,
            clientRect.X,
            clientRect.Y,
            clientRect.Width,
            clientRect.Height,
            clientRect.X,
            clientRect.Y,
            cropped.Width,
            cropped.Height,
            "printwindow");
    }

    private static Bgr24Frame CaptureBitmap(int width, int height, Action<nint> drawAction)
    {
        var screenDc = GetDC(nint.Zero);
        if (screenDc == nint.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "GetDC failed.");
        }

        var memoryDc = CreateCompatibleDC(screenDc);
        if (memoryDc == nint.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateCompatibleDC failed.");
        }

        var bitmap = CreateCompatibleBitmap(screenDc, width, height);
        if (bitmap == nint.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateCompatibleBitmap failed.");
        }

        var oldBitmap = SelectObject(memoryDc, bitmap);
        try
        {
            drawAction(memoryDc);
            var paddedStride = ((width * 3) + 3) & ~3;
            var bytes = new byte[paddedStride * height];
            var info = new BitmapInfo
            {
                bmiHeader = new BitmapInfoHeader
                {
                    biSize = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                    biWidth = width,
                    biHeight = height,
                    biPlanes = 1,
                    biBitCount = 24,
                    biCompression = 0,
                    biSizeImage = (uint)bytes.Length
                }
            };

            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                var result = GetDIBits(memoryDc, bitmap, 0, (uint)height, handle.AddrOfPinnedObject(), ref info, 0);
                if (result != height)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "GetDIBits failed.");
                }
            }
            finally
            {
                handle.Free();
            }

            return Bgr24Frame.FromPaddedBottomUpRows(width, height, bytes, "capture");
        }
        finally
        {
            _ = SelectObject(memoryDc, oldBitmap);
            _ = DeleteObject(bitmap);
            _ = DeleteDC(memoryDc);
            _ = ReleaseDC(nint.Zero, screenDc);
        }
    }

    private static NativeRect? TryGetClientRectOnScreen(nint hwnd)
    {
        if (!GetClientRect(hwnd, out var rect))
        {
            return null;
        }

        var point = new NativePoint();
        if (!ClientToScreen(hwnd, ref point))
        {
            return null;
        }

        return new NativeRect(point.X, point.Y, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    private static NativeRect GetWindowRectOnScreen(nint hwnd)
    {
        if (!GetWindowRect(hwnd, out var rect))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "GetWindowRect failed.");
        }

        return new NativeRect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    private readonly record struct NativeRect(int X, int Y, int Width, int Height);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public BitmapInfoHeader bmiHeader;
        public uint bmiColors;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint GetDC(nint hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int ReleaseDC(nint hwnd, nint hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern nint CreateCompatibleDC(nint hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteDC(nint hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern nint CreateCompatibleBitmap(nint hdc, int width, int height);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern nint SelectObject(nint hdc, nint obj);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteObject(nint obj);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool BitBlt(nint hdcDest, int xDest, int yDest, int width, int height, nint hdcSrc, int xSrc, int ySrc, uint rop);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int GetDIBits(nint hdc, nint hBitmap, uint start, uint lines, nint bits, ref BitmapInfo info, uint usage);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PrintWindow(nint hwnd, nint hdc, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetClientRect(nint hwnd, out Rect rect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ClientToScreen(nint hwnd, ref NativePoint point);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(nint hwnd, out Rect rect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool IsIconic(nint hwnd);
}
