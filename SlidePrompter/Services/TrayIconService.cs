using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SlidePrompter.Infrastructure.Interop;
using SlidePrompter.Services.Interfaces;

namespace SlidePrompter.Services;

/// <summary>
/// Provides a persistent Windows system tray icon with quick actions to reopen controls, toggle overlay, or stop presenting.
/// </summary>
public sealed class TrayIconService : ITrayIconService
{
    private NotifyIcon? _notifyIcon;
    private Icon? _generatedIcon;
    private ToolStripMenuItem? _togglePresentationItem;
    private ToolStripMenuItem? _toggleOverlayItem;
    private bool _disposed;

    public event EventHandler? ShowControlWindowRequested;
    public event EventHandler? ToggleOverlayVisibilityRequested;
    public event EventHandler? TogglePresentationRequested;
    public event EventHandler? ExitRequested;

    public void Initialize()
    {
        if (_notifyIcon is not null || _disposed)
        {
            return;
        }

        _generatedIcon = CreateTrayIcon();

        var menu = new ContextMenuStrip();

        var openControlsItem = new ToolStripMenuItem("Abrir Controles")
        {
            Font = new Font(menu.Font, FontStyle.Bold)
        };
        openControlsItem.Click += (_, _) => ShowControlWindowRequested?.Invoke(this, EventArgs.Empty);

        _togglePresentationItem = new ToolStripMenuItem("Iniciar Apresentação");
        _togglePresentationItem.Click += (_, _) => TogglePresentationRequested?.Invoke(this, EventArgs.Empty);

        _toggleOverlayItem = new ToolStripMenuItem("Mostrar / Ocultar Barra (Ctrl+Alt+H)");
        _toggleOverlayItem.Click += (_, _) => ToggleOverlayVisibilityRequested?.Invoke(this, EventArgs.Empty);

        var exitItem = new ToolStripMenuItem("Sair do SlidePrompter");
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        menu.Items.Add(openControlsItem);
        menu.Items.Add(_togglePresentationItem);
        menu.Items.Add(_toggleOverlayItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = _generatedIcon,
            Text = "SlidePrompter — Teleprompter Discreto",
            ContextMenuStrip = menu,
            Visible = true
        };

        _notifyIcon.DoubleClick += (_, _) => ShowControlWindowRequested?.Invoke(this, EventArgs.Empty);
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                ShowControlWindowRequested?.Invoke(this, EventArgs.Empty);
            }
        };
    }

    public void UpdateState(bool isPresenting, bool isOverlayVisible)
    {
        if (_notifyIcon is null || _disposed)
        {
            return;
        }

        if (_togglePresentationItem is not null)
        {
            _togglePresentationItem.Text = isPresenting
                ? "Parar Apresentação"
                : "Iniciar Apresentação";
        }

        if (_toggleOverlayItem is not null)
        {
            _toggleOverlayItem.Text = isOverlayVisible
                ? "Ocultar Barra (Ctrl+Alt+H)"
                : "Mostrar Barra (Ctrl+Alt+H)";
        }

        _notifyIcon.Text = isPresenting
            ? "SlidePrompter — Apresentação Ativa"
            : "SlidePrompter — Modo Edição";
    }

    private static Icon CreateTrayIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var bgBrush = new SolidBrush(Color.FromArgb(245, 17, 17, 24));
            using var borderPen = new Pen(Color.FromArgb(255, 99, 102, 241), 2.2f);
            using var path = CreateRoundedRect(new Rectangle(2, 5, 28, 22), 7);
            g.FillPath(bgBrush, path);
            g.DrawPath(borderPen, path);

            // Minimalist teleprompter line + accent indicator dot
            using var lineBrush = new SolidBrush(Color.FromArgb(245, 244, 244, 248));
            g.FillRectangle(lineBrush, 7, 14, 13, 3.5f);

            using var dotBrush = new SolidBrush(Color.FromArgb(255, 34, 197, 94));
            g.FillEllipse(dotBrush, 22, 13, 5, 5);
        }

        IntPtr hIcon = bitmap.GetHicon();
        try
        {
            using var tempIcon = Icon.FromHandle(hIcon);
            return (Icon)tempIcon.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(hIcon);
        }
    }

    private static GraphicsPath CreateRoundedRect(Rectangle bounds, int radius)
    {
        int diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.ContextMenuStrip?.Dispose();
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        _generatedIcon?.Dispose();
        _generatedIcon = null;
    }
}

