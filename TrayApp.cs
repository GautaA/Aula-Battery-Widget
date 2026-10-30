using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

// Independent workers keep a silent keyboard from blocking mouse updates.
sealed class BatteryDevice : IDisposable
{
    public readonly string Name;
    public readonly NotifyIcon Tray = new NotifyIcon();
    public readonly ToolStripMenuItem Status = new ToolStripMenuItem("Checking...");
    public readonly ToolStripMenuItem Updated = new ToolStripMenuItem("No reading yet");
    public readonly ToolStripMenuItem Startup = new ToolStripMenuItem("Start with Windows");
    public readonly BackgroundWorker Worker = new BackgroundWorker();
    public string Headline = "Checking...", Detail = "Waiting for a reading";
    public string TooltipStatus = "Checking...";
    public Label LevelLabel, DetailLabel;
    public event Action Changed;
    readonly Color color;
    Icon icon;
    int? lastPercent;
    DateTime? lastSeen;
    bool notified, disposed;
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr handle);

    public BatteryDevice(string name, Color color, Func<AulaBatteryProbe.Reading> query)
    {
        Name = name; this.color = color;
        Status.Enabled = Updated.Enabled = false;
        SetIcon("...", Color.DimGray);
        Tray.Text = name + ": checking";
        Worker.DoWork += delegate(object sender, DoWorkEventArgs e) { e.Result = query(); };
        Worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e) {
            if (disposed) return;
            Apply(e.Error == null ? (AulaBatteryProbe.Reading)e.Result : new AulaBatteryProbe.Reading { Message = e.Error.Message });
        };
    }

    public void Refresh() { if (!disposed && !Worker.IsBusy) Worker.RunWorkerAsync(); }

    void Apply(AulaBatteryProbe.Reading reading)
    {
        if (reading.Percent.HasValue || reading.PluggedIn) lastSeen = DateTime.Now;
        if (reading.Percent.HasValue) {
            lastPercent = reading.Percent;
            Headline = reading.Percent + "%";
            Detail = reading.Charging.HasValue ? (reading.Charging.Value ? "Charging" : "On battery") : reading.Message;
            bool charging = reading.Charging == true;
            SetIcon(reading.Percent.ToString(), charging ? Color.FromArgb(40,110,185) : reading.Percent <= 20 ? Color.FromArgb(190,60,35) : color);
            if (reading.Percent > 25 || charging) notified = false;
            if (reading.Percent <= 20 && !charging && !notified) {
                Tray.ShowBalloonTip(6000, Name + " battery is low", reading.Percent + "% remaining. Connect it to power to charge.", ToolTipIcon.Warning);
                notified = true;
            }
        } else if (reading.PluggedIn) {
            lastPercent = null;
            Headline = "Plugged in";
            Detail = "Battery percentage unavailable while connected to power";
            notified = false;
            SetIcon("+", Color.FromArgb(40,110,185));
        } else {
            Headline = lastPercent.HasValue ? lastPercent + "% (last seen)" : "Unavailable";
            Detail = reading.Message;
            SetIcon("?", Color.DimGray);
        }
        Status.Text = Headline + " - " + Detail;
        Updated.Text = lastSeen.HasValue ? "Last reading: " + lastSeen.Value.ToString("dd MMM HH:mm:ss") : "No battery reading yet";
        TooltipStatus = Headline + (reading.Charging == true ? " (charging)" : "");
        UpdateLabels();
        if (Changed != null) Changed();
    }

    public void UpdateLabels()
    {
        if (LevelLabel == null || LevelLabel.IsDisposed) return;
        LevelLabel.Text = Headline;
        DetailLabel.Text = Detail + Environment.NewLine + Updated.Text;
    }

    void SetIcon(string text, Color background)
    {
        using (var bitmap = new Bitmap(32,32))
        using (var graphics = Graphics.FromImage(bitmap))
        using (var brush = new SolidBrush(background))
        using (var font = new Font("Segoe UI", text.Length >= 3 ? 13 : 17, FontStyle.Bold, GraphicsUnit.Pixel))
        using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);
            graphics.FillEllipse(brush,0,0,31,31);
            graphics.DrawString(text,font,Brushes.White,new RectangleF(0,0,32,31),format);
            IntPtr native = bitmap.GetHicon();
            Icon next;
            using (var borrowed = Icon.FromHandle(native)) next = (Icon)borrowed.Clone();
            DestroyIcon(native);
            Tray.Icon = next;
            if (icon != null) icon.Dispose();
            icon = next;
        }
    }

    public void Dispose()
    {
        disposed = true;
        Tray.Visible = false;
        var menu = Tray.ContextMenuStrip;
        Tray.Dispose();
        if (menu != null) menu.Dispose();
        if (icon != null) icon.Dispose();
    }
}

sealed class TrayApp : ApplicationContext
{
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    readonly BatteryDevice[] devices;
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    Form popup;

    static AulaBatteryProbe.Reading QueryAula() {
        string path = Receiver.Find();
        return path == null ? new AulaBatteryProbe.Reading { Message = "Receiver disconnected" } : AulaBatteryProbe.Query(path);
    }

    static AulaBatteryProbe.Reading QueryRazer() {
        string path = Receiver.FindRazer();
        return path == null ? new AulaBatteryProbe.Reading { Message = "Mouse receiver disconnected" } : RazerBatteryProbe.Query(path);
    }

    public TrayApp()
    {
        devices = new[] {
            new BatteryDevice("AULA F75", Color.FromArgb(25,125,90), QueryAula),
            new BatteryDevice("Razer DeathAdder V4 Pro", Color.FromArgb(125,75,185), QueryRazer)
        };
        foreach (var device in devices) {
            var current = device;
            var menu = new ContextMenuStrip();
            menu.Items.Add(current.Name, null, delegate { ShowPopup(); });
            menu.Items.Add(current.Status); menu.Items.Add(current.Updated);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Show both batteries", null, delegate { ShowPopup(); });
            menu.Items.Add("Refresh now", null, delegate { Refresh(); });
            current.Startup.Click += delegate { ToggleStartup(); };
            menu.Items.Add(current.Startup);
            menu.Items.Add("Exit battery tracker", null, delegate { ExitThread(); });
            current.Tray.ContextMenuStrip = menu;
            current.Tray.MouseClick += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) ShowPopup(); };
            current.Changed += delegate { UpdateTooltips(); SaveStatus(); };
            current.Tray.Visible = true;
        }
        UpdateTooltips();
        SyncStartup();
        timer.Interval = 60000;
        timer.Tick += delegate { Refresh(); };
        timer.Start();
        Refresh();
    }

    void Refresh() { foreach (var device in devices) device.Refresh(); }

    void UpdateTooltips()
    {
        // Compact names keep both complete lines within NotifyIcon's 63-character limit,
        // including the longest states: "100% (last seen)" and "100% (charging)".
        string tip = "AULA F75: " + devices[0].TooltipStatus + "\nDeathAdder V4 Pro: " + devices[1].TooltipStatus;
        foreach (var device in devices) device.Tray.Text = tip;
    }

    void SyncStartup() {
        bool enabled;
        using (var key = Registry.CurrentUser.OpenSubKey(RunKey)) enabled = key != null && key.GetValue("AulaF75Battery") != null;
        foreach (var device in devices) device.Startup.Checked = enabled;
    }

    void ToggleStartup() {
        try {
            using (var key = Registry.CurrentUser.CreateSubKey(RunKey)) {
                if (key.GetValue("AulaF75Battery") != null) key.DeleteValue("AulaF75Battery", false);
                else key.SetValue("AulaF75Battery", "\"" + Application.ExecutablePath + "\"");
            }
            SyncStartup();
        } catch (Exception ex) { MessageBox.Show(ex.Message, "Startup setting"); }
    }

    void SaveStatus() {
        try {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AulaF75Battery");
            Directory.CreateDirectory(dir);
            string text = DateTime.Now.ToString("O") + Environment.NewLine;
            foreach (var d in devices) text += d.Name + ": " + d.Status.Text + Environment.NewLine + d.Updated.Text + Environment.NewLine;
            text += "Hover tooltip:" + Environment.NewLine + devices[0].Tray.Text + Environment.NewLine;
            File.WriteAllText(Path.Combine(dir,"status.txt"),text);
        } catch { /* Optional local diagnostic snapshot. */ }
    }

    void ShowPopup()
    {
        if (popup != null && !popup.IsDisposed) { popup.Activate(); return; }
        popup = new Form {
            Text = "Keyboard & Mouse Batteries", ClientSize = new Size(400,360),
            FormBorderStyle = FormBorderStyle.FixedDialog, ShowInTaskbar = true,
            TopMost = true, StartPosition = FormStartPosition.Manual,
            BackColor = Color.FromArgb(25,29,36), ForeColor = Color.White
        };
        for (int i=0; i<devices.Length; i++) {
            var device = devices[i];
            int top = 18 + i*150;
            var title = new Label { Text = device.Name, Left = 20, Top = top, Width = 360, Height = 23, Font = new Font("Segoe UI",11,FontStyle.Bold) };
            device.LevelLabel = new Label { Left = 18, Top = top+27, Width = 365, Height = 45, Font = new Font("Segoe UI",22,FontStyle.Bold) };
            device.DetailLabel = new Label { Left = 20, Top = top+76, Width = 360, Height = 63, Font = new Font("Segoe UI",9) };
            popup.Controls.AddRange(new Control[] { title,device.LevelLabel,device.DetailLabel });
            device.UpdateLabels();
        }
        var refresh = new Button { Text = "Refresh both", Left = 20, Top = 322, Width = 110, Height = 27, FlatStyle = FlatStyle.Flat };
        refresh.Click += delegate { Refresh(); };
        popup.Controls.Add(refresh);
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        popup.Location = new Point(area.Right-popup.Width-12,area.Bottom-popup.Height-12);
        popup.Show();
        Refresh();
    }

    protected override void ExitThreadCore() {
        timer.Stop(); timer.Dispose();
        if (popup != null) popup.Dispose();
        foreach (var device in devices) device.Dispose();
        base.ExitThreadCore();
    }

    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--probe") {
            string result = "";
            foreach (var query in new Func<AulaBatteryProbe.Reading>[] { QueryAula,QueryRazer }) {
                try { var r = query(); result += (r.Percent.HasValue ? r.Percent + "%: " : "") + r.Message + Environment.NewLine; }
                catch (Exception ex) { result += ex.Message + Environment.NewLine; }
            }
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"probe-result.txt"),result);
            return;
        }
        bool created;
        using (var mutex = new Mutex(true,"Local\\AulaF75BatteryTray",out created)) {
            if (!created) return;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (var app = new TrayApp()) {
                if (args.Length > 0 && args[0] == "--show") {
                    var showTimer = new System.Windows.Forms.Timer { Interval = 250 };
                    showTimer.Tick += delegate { showTimer.Stop(); showTimer.Dispose(); app.ShowPopup(); };
                    showTimer.Start();
                }
                Application.Run(app);
            }
        }
    }
}
