using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

// Read the keyboard in the background so the tray stays responsive.
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
            Detail = reading.Message;
            SetIcon(reading.Percent.ToString(), reading.Percent <= 20 ? Color.FromArgb(190,60,35) : color);
            if (reading.Percent > 25) notified = false;
            if (reading.Percent <= 20 && !notified) {
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
        TooltipStatus = Headline;
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
    BatteryDevice[] devices;
    KeyboardSettings settings;
    KeyboardProfile profile;
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    Form popup;
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr window, int command);

    static AulaBatteryProbe.Reading QueryKeyboard(KeyboardProfile selected, string receiverPath) {
        var endpoint = KeyboardProfiles.Select(selected, Receiver.Scan(), receiverPath);
        return endpoint == null ? new AulaBatteryProbe.Reading { Message = "No compatible receiver found. Connect the 2.4 GHz receiver or export diagnostics." } : AulaBatteryProbe.Query(endpoint.Path, selected);
    }

    public TrayApp() : this(KeyboardSettings.Load()) { }

    public TrayApp(KeyboardSettings configuration)
    {
        settings = configuration;
        ConfigureDevice();
        timer.Interval = 60000;
        timer.Tick += delegate { Refresh(); };
        timer.Start();
        Refresh();
    }

    void ConfigureDevice()
    {
        if (popup != null) { popup.Dispose(); popup = null; }
        if (devices != null) foreach (var old in devices) old.Dispose();
        profile = KeyboardProfiles.Get(settings.Model);
        var selected = profile;
        var selectedPath = settings.ReceiverPath;
        devices = new[] {
            new BatteryDevice(selected.Name, Color.FromArgb(25,125,90), delegate { return QueryKeyboard(selected, selectedPath); })
        };
        foreach (var device in devices) {
            var current = device;
            var menu = new ContextMenuStrip();
            menu.Items.Add(current.Name, null, delegate { ShowPopup(); });
            menu.Items.Add(current.Status); menu.Items.Add(current.Updated);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Show battery details", null, delegate { ShowPopup(); });
            menu.Items.Add("Refresh now", null, delegate { Refresh(); });
            var models = new ToolStripMenuItem("Keyboard model");
            foreach (var model in KeyboardProfiles.All) {
                var choice = model;
                var item = new ToolStripMenuItem(choice.ToString()) { Checked = choice.Id == selected.Id, ToolTipText = choice.Support };
                item.Click += delegate { SelectKeyboard(choice.Id, ""); };
                models.DropDownItems.Add(item);
            }
            menu.Items.Add(models);
            var receivers = new ToolStripMenuItem("Receiver");
            receivers.DropDownOpening += delegate { PopulateReceivers(receivers); };
            receivers.DropDownItems.Add("Scanning...");
            menu.Items.Add(receivers);
            menu.Items.Add("Export receiver diagnostics...", null, delegate { ExportDiagnostics(); });
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
    }

    void SelectKeyboard(string model, string receiverPath)
    {
        try {
            var next = new KeyboardSettings { Model = model, ReceiverPath = receiverPath };
            next.Save(); settings = next;
            ConfigureDevice(); Refresh();
        } catch (Exception ex) { MessageBox.Show(ex.Message, "Keyboard selection"); }
    }

    void PopulateReceivers(ToolStripMenuItem menu)
    {
        menu.DropDownItems.Clear();
        var automatic = new ToolStripMenuItem("Automatic (one compatible receiver)") { Checked = String.IsNullOrEmpty(settings.ReceiverPath) };
        automatic.Click += delegate { SelectKeyboard(settings.Model, ""); };
        menu.DropDownItems.Add(automatic);
        try {
            int index = 0;
            foreach (var endpoint in Receiver.Scan()) {
                if (!KeyboardProfiles.Accepts(profile,endpoint)) continue;
                var choice = endpoint;
                var item = new ToolStripMenuItem((++index) + ": " + choice.ToString()) { Checked = choice.Path == settings.ReceiverPath };
                item.Click += delegate { SelectKeyboard(settings.Model,choice.Path); };
                menu.DropDownItems.Add(item);
            }
            if (index == 0) menu.DropDownItems.Add(new ToolStripMenuItem("No compatible receivers found") { Enabled = false });
        } catch (Exception ex) { menu.DropDownItems.Add(new ToolStripMenuItem(ex.Message) { Enabled = false }); }
    }

    static string Diagnostics(KeyboardProfile selected)
    {
        var text = new System.Text.StringBuilder();
        text.AppendLine("AULA Battery Widget receiver diagnostics");
        text.AppendLine("Profile: " + selected.Name + " - " + selected.Support);
        text.AppendLine("Discovery only; no battery commands, serial numbers, or device paths included.");
        foreach (var endpoint in Receiver.Scan()) text.AppendLine(endpoint.ToString() + "  reader=" + (endpoint.Family ?? "unrecognized"));
        return text.ToString();
    }

    void ExportDiagnostics()
    {
        using (var dialog = new SaveFileDialog { Title = "Save receiver diagnostics", Filter = "Text file|*.txt", FileName = "aula-diagnostics.txt" }) {
            if (dialog.ShowDialog() != DialogResult.OK) return;
            try { File.WriteAllText(dialog.FileName,Diagnostics(profile)); }
            catch (Exception ex) { MessageBox.Show(ex.Message,"Diagnostics"); }
        }
    }

    void Refresh() { foreach (var device in devices) device.Refresh(); }

    void UpdateTooltips()
    {
        string tip = profile.Name + ": " + devices[0].TooltipStatus;
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
        if (popup != null && !popup.IsDisposed) { PresentPopup(); Refresh(); return; }
        popup = new Form {
            Text = profile.Name + " Battery", ClientSize = new Size(440,290),
            FormBorderStyle = FormBorderStyle.FixedDialog, ShowInTaskbar = true,
            TopMost = true, StartPosition = FormStartPosition.Manual,
            BackColor = Color.FromArgb(25,29,36), ForeColor = Color.White
        };
        for (int i=0; i<devices.Length; i++) {
            var device = devices[i];
            int top = 18 + i*150;
            var title = new Label { Text = device.Name, Left = 20, Top = top, Width = 400, Height = 23, Font = new Font("Segoe UI",11,FontStyle.Bold) };
            device.LevelLabel = new Label { Left = 18, Top = top+27, Width = 365, Height = 45, Font = new Font("Segoe UI",22,FontStyle.Bold) };
            device.DetailLabel = new Label { Left = 20, Top = top+76, Width = 400, Height = 63, Font = new Font("Segoe UI",9) };
            popup.Controls.AddRange(new Control[] { title,device.LevelLabel,device.DetailLabel });
            device.UpdateLabels();
        }
        var support = new Label { Text = (profile.Experimental ? "Experimental profile\n" : "") + profile.Support + "\nChoose your model from the tray menu.", Left = 20, Top = 164, Width = 400, Height = 72, ForeColor = Color.LightGray, Font = new Font("Segoe UI",9) };
        popup.Controls.Add(support);
        var refresh = new Button { Text = "Refresh", Left = 20, Top = 250, Width = 110, Height = 27, FlatStyle = FlatStyle.Flat };
        refresh.Click += delegate { Refresh(); };
        popup.Controls.Add(refresh);
        PresentPopup();
        Refresh();
    }

    void PresentPopup()
    {
        popup.WindowState = FormWindowState.Normal;
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        popup.Location = new Point(Math.Max(area.Left, area.Right-popup.Width-12), Math.Max(area.Top, area.Bottom-popup.Height-12));
        popup.Show();
        // A hidden process launch can override the first native ShowWindow call
        // inside Form.Show(). A subsequent SW_RESTORE makes it visible reliably.
        ShowWindow(popup.Handle, 9); // SW_RESTORE
        popup.BringToFront();
        popup.Activate();
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
        var selectedSettings = KeyboardSettings.Load();
        int modelIndex = Array.IndexOf(args,"--model");
        if (modelIndex >= 0) {
            if (modelIndex+1 >= args.Length || !Array.Exists(KeyboardProfiles.All,p => p.Id == args[modelIndex+1])) { Environment.ExitCode=2; return; }
            selectedSettings.Model=args[modelIndex+1]; selectedSettings.ReceiverPath="";
        }
        var selectedProfile=KeyboardProfiles.Get(selectedSettings.Model);
        if (Array.IndexOf(args,"--diagnostics") >= 0) {
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"aula-diagnostics.txt"),Diagnostics(selectedProfile));
            return;
        }
        if (Array.IndexOf(args,"--probe") >= 0) {
            string result = "";
            foreach (var query in new Func<AulaBatteryProbe.Reading>[] { delegate { return QueryKeyboard(selectedProfile,selectedSettings.ReceiverPath); } }) {
                try { var r = query(); result += (r.Percent.HasValue ? r.Percent + "%: " : "") + r.Message + Environment.NewLine; }
                catch (Exception ex) { result += ex.Message + Environment.NewLine; Environment.ExitCode=1; }
            }
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"probe-result.txt"),result);
            return;
        }
        bool created;
        using (var mutex = new Mutex(true,"Local\\AulaF75BatteryTray",out created)) {
            if (!created) return;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (var app = new TrayApp(selectedSettings)) {
                if (Array.IndexOf(args,"--show") >= 0) {
                    var showTimer = new System.Windows.Forms.Timer { Interval = 250 };
                    showTimer.Tick += delegate { showTimer.Stop(); showTimer.Dispose(); app.ShowPopup(); };
                    showTimer.Start();
                }
                Application.Run(app);
            }
        }
    }
}
