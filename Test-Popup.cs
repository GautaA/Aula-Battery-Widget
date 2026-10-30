using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Exercise the actual tray menu handler in a process launched with SW_HIDE.
static class PopupSmokeTest
{
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
    static Form OpenDetails(TrayApp app) {
        var devices = (BatteryDevice[])typeof(TrayApp).GetField("devices", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(app);
        foreach (ToolStripItem item in devices[0].Tray.ContextMenuStrip.Items) {
            if (item.Text == "Show battery details") { item.PerformClick(); break; }
        }
        return (Form)typeof(TrayApp).GetField("popup", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(app);
    }
    static void AssertVisible(Form form, string scenario) {
        if (form == null || form.IsDisposed || !IsWindowVisible(form.Handle) || IsIconic(form.Handle))
            throw new Exception(scenario + ": details window is hidden or minimized");
        if (!Screen.FromControl(form).WorkingArea.IntersectsWith(form.Bounds))
            throw new Exception(scenario + ": details window is off screen");
    }
    [STAThread]
    static void Main() {
        string temporarySettings = Path.Combine(Path.GetTempPath(),"aula-popup-test-" + Guid.NewGuid().ToString("N") + ".txt");
        KeyboardSettings.FileName = temporarySettings;
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using (var app = new TrayApp())
        using (var timer = new Timer { Interval = 200 }) {
            timer.Tick += delegate {
                timer.Stop();
                try {
                    var form = OpenDetails(app);
                    AssertVisible(form, "First menu click after hidden launch");
                    form.WindowState = FormWindowState.Minimized;
                    AssertVisible(OpenDetails(app), "Reopen minimized window");
                    form.Hide();
                    AssertVisible(OpenDetails(app), "Reopen hidden window");
                    form.Location = new System.Drawing.Point(-30000,-30000);
                    AssertVisible(OpenDetails(app), "Reopen off-screen window");
                    form.Close();
                    AssertVisible(OpenDetails(app), "Reopen closed window");
                    foreach (var profile in KeyboardProfiles.All) {
                        var devices = (BatteryDevice[])typeof(TrayApp).GetField("devices",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(app);
                        ToolStripMenuItem models = null;
                        foreach (ToolStripItem item in devices[0].Tray.ContextMenuStrip.Items) if(item.Text=="Keyboard model") models=(ToolStripMenuItem)item;
                        if(models==null || models.DropDownItems.Count!=KeyboardProfiles.All.Length) throw new Exception("Missing model options");
                        foreach (ToolStripItem item in models.DropDownItems) if(item.Text==profile.ToString()) { item.PerformClick(); break; }
                        if(KeyboardSettings.Load().Model!=profile.Id) throw new Exception("Selected profile did not persist");
                        var selectedForm=OpenDetails(app);
                        AssertVisible(selectedForm,"Switch to " + profile.Name);
                        if(selectedForm.Text!=profile.Name+" Battery") throw new Exception("Window label did not follow profile");
                        if(profile.Experimental) {
                            bool marked=false;
                            foreach(Control control in selectedForm.Controls) if(control.Text.Contains("Experimental profile")) marked=true;
                            if(!marked) throw new Exception("Untested profile lacks experimental label");
                        }
                    }
                    File.WriteAllText("popup-test-result.txt", "PASS: hidden launch, window restoration, all 11 model menu selections, persistence and experimental labels");
                } catch (Exception ex) {
                    File.WriteAllText("popup-test-result.txt", "FAIL: " + ex.Message);
                    Environment.ExitCode = 1;
                } finally { app.ExitThread(); if(File.Exists(temporarySettings)) File.Delete(temporarySettings); }
            };
            timer.Start();
            Application.Run(app);
        }
    }
}
