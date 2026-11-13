using System.Diagnostics;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Qlix;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

class MainForm : Form
{
    const string StartUrl = "https://oldsoultech.github.io/Qlix/";
    const string AppName = "Qlix";

    // Hosts that stay inside the app window
    static readonly string[] AppHosts = { "oldsoultech.github.io" };
    // Hosts needed for Firebase / Google sign-in popups
    static readonly string[] AuthSuffixes = { "google.com", "firebaseapp.com", "googleapis.com", "gstatic.com" };

    const string OfflineHtml =
        "<html><head><meta name='viewport' content='width=device-width,initial-scale=1'>" +
        "<style>body{font-family:Segoe UI,sans-serif;display:flex;height:100vh;margin:0;" +
        "align-items:center;justify-content:center;text-align:center;background:#547538;color:#fff}" +
        "a{display:inline-block;margin-top:16px;padding:10px 20px;border-radius:10px;" +
        "background:#fff;color:#547538;text-decoration:none;font-weight:bold}</style></head>" +
        "<body><div><h2>Can't reach Qlix</h2><p>Check your connection.</p>" +
        "<a href='" + StartUrl + "'>Try Again</a></div></body></html>";

    readonly WebView2 web = new() { Dock = DockStyle.Fill };

    public MainForm()
    {
        Text = AppName;
        Width = 1200;
        Height = 800;
        MinimumSize = new Size(480, 360);
        StartPosition = FormStartPosition.CenterScreen;

        try
        {
            if (Environment.ProcessPath is string p)
                Icon = Icon.ExtractAssociatedIcon(p);
        }
        catch { /* no icon, fine */ }

        Controls.Add(web);
        Load += async (_, _) => await InitAsync();
    }

    static bool IsAppHost(Uri u) =>
        AppHosts.Contains(u.Host, StringComparer.OrdinalIgnoreCase);

    static bool IsAuthHost(Uri u) =>
        AuthSuffixes.Any(s => u.Host.Equals(s, StringComparison.OrdinalIgnoreCase) ||
                              u.Host.EndsWith("." + s, StringComparison.OrdinalIgnoreCase));

    static void OpenExternal(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* ignore */ }
    }

    async Task InitAsync()
    {
        try
        {
            // Keeps logins/cookies between launches
            var dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);
            var env = await CoreWebView2Environment.CreateAsync(null, dataDir);
            await web.EnsureCoreWebView2Async(env);
        }
        catch (Exception ex)
        {
            var result = MessageBox.Show(
                "Qlix needs the Microsoft Edge WebView2 Runtime.\n\n" + ex.Message +
                "\n\nOpen the download page?",
                AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result == DialogResult.Yes)
                OpenExternal("https://developer.microsoft.com/microsoft-edge/webview2/");
            Close();
            return;
        }

        var core = web.CoreWebView2;

        core.DocumentTitleChanged += (_, _) =>
            Text = string.IsNullOrWhiteSpace(core.DocumentTitle) ? AppName : core.DocumentTitle;

        // Links that open new windows (target=_blank / window.open)
        core.NewWindowRequested += (_, e) =>
        {
            if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri)) return;
            if (IsAppHost(uri))
            {
                e.Handled = true;
                core.Navigate(e.Uri);
            }
            else if (IsAuthHost(uri) || uri.Scheme == "about")
            {
                // let WebView2 open its own popup (needed for sign-in)
            }
            else
            {
                e.Handled = true;
                OpenExternal(e.Uri);
            }
        };

        // Other sites open in the default browser instead of inside the app
        core.NavigationStarting += (_, e) =>
        {
            if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri)) return;
            if (uri.Scheme != "http" && uri.Scheme != "https") return;
            if (IsAppHost(uri) || IsAuthHost(uri)) return;
            e.Cancel = true;
            OpenExternal(e.Uri);
        };

        core.NavigationCompleted += (_, e) =>
        {
            if (!e.IsSuccess && e.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled)
                core.NavigateToString(OfflineHtml);
        };

        core.Navigate(StartUrl);
    }
}
