namespace Klyr.Services
{
    /// <summary>
    /// v2.4.0 — Icône de zone de notification (system tray) basée sur
    /// System.Windows.Forms.NotifyIcon. Types WinForms/Drawing pleinement qualifiés
    /// pour éviter tout conflit avec WPF. À disposer à la fermeture de l'app.
    /// </summary>
    public sealed class TrayIconService : IDisposable
    {
        private readonly System.Windows.Forms.NotifyIcon _icon;

        public event Action? OpenRequested;
        public event Action? QuickCleanRequested;
        public event Action? QuitRequested;

        public TrayIconService(string tooltip, string openText, string cleanText, string quitText)
        {
            _icon = new System.Windows.Forms.NotifyIcon
            {
                Text    = tooltip.Length > 62 ? tooltip.Substring(0, 62) : tooltip,
                Visible = false
            };

            // Icône : on extrait celle de l'exécutable (même que la fenêtre).
            try
            {
                string? exe = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exe))
                    _icon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(exe);
            }
            catch { /* icône par défaut sinon */ }

            var menu = new System.Windows.Forms.ContextMenuStrip();

            var open = new System.Windows.Forms.ToolStripMenuItem(openText);
            open.Click += (_, _) => OpenRequested?.Invoke();

            var clean = new System.Windows.Forms.ToolStripMenuItem(cleanText);
            clean.Click += (_, _) => QuickCleanRequested?.Invoke();

            var quit = new System.Windows.Forms.ToolStripMenuItem(quitText);
            quit.Click += (_, _) => QuitRequested?.Invoke();

            menu.Items.Add(open);
            menu.Items.Add(clean);
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add(quit);

            _icon.ContextMenuStrip = menu;
            _icon.DoubleClick += (_, _) => OpenRequested?.Invoke();
        }

        public void Show() => _icon.Visible = true;
        public void Hide() => _icon.Visible = false;

        public void ShowBalloon(string title, string text)
        {
            try
            {
                _icon.BalloonTipTitle = title;
                _icon.BalloonTipText  = text;
                _icon.ShowBalloonTip(3000);
            }
            catch { /* best-effort */ }
        }

        public void Dispose()
        {
            try
            {
                _icon.Visible = false;
                _icon.Dispose();
            }
            catch { /* déjà disposé */ }
        }
    }
}
