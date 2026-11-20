using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace NoctyraSetup
{
    /// <summary>Dosya kopyalama, kısayol, kayıt defteri ve kaldırma işleri.</summary>
    public static class Installer
    {
        public const string GameExe = "Noctyra.exe";
        const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Noctyra";
        const string AppKey = @"Software\NoctyraLauncher"; // Unity PlayerPrefs (Software\Noctyra\Noctyra) ile karışmasın

        public static string DefaultRoot
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Noctyra"); }
        }

        public static string LoadRoot()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(AppKey))
                {
                    var v = k == null ? null : k.GetValue("InstallRoot") as string;
                    if (!string.IsNullOrWhiteSpace(v)) return v;
                }
            }
            catch { }
            return DefaultRoot;
        }

        static void SaveRoot(string root)
        {
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(AppKey)) k.SetValue("InstallRoot", root);
            }
            catch { }
        }

        public static string GameDir(string root) { return Path.Combine(root, "Game"); }
        public static string VersionFile(string root) { return Path.Combine(GameDir(root), "version.txt"); }
        public static string LauncherCopy(string root) { return Path.Combine(root, "NoctyraSetup.exe"); }

        public static string InstalledVersion(string root)
        {
            try
            {
                string f = VersionFile(root);
                if (File.Exists(f) && File.Exists(Path.Combine(GameDir(root), GameExe)))
                    return File.ReadAllText(f).Trim();
            }
            catch { }
            return null;
        }

        public static bool IsGameRunning()
        {
            return Process.GetProcessesByName(Path.GetFileNameWithoutExtension(GameExe)).Length > 0;
        }

        public static string Sha256Of(string file)
        {
            using (var sha = SHA256.Create())
            using (var fs = File.OpenRead(file))
            {
                var hash = sha.ComputeHash(fs);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>Zip'i geçici klasöre açar, eski oyunu değiştirir, sürüm bilgisini yazar.</summary>
        public static Task InstallZipAsync(string zipFile, string root, string tag, Action<int> progress)
        {
            return Task.Run(() =>
            {
                Directory.CreateDirectory(root);
                string gameDir = GameDir(root);
                string tmp = Path.Combine(root, "Game.new");
                if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
                Directory.CreateDirectory(tmp);

                string tmpFull = Path.GetFullPath(tmp) + Path.DirectorySeparatorChar;
                using (var zip = ZipFile.OpenRead(zipFile))
                {
                    int i = 0, count = zip.Entries.Count;
                    foreach (var e in zip.Entries)
                    {
                        i++;
                        string dest = Path.GetFullPath(Path.Combine(tmp, e.FullName));
                        if (!dest.StartsWith(tmpFull, StringComparison.OrdinalIgnoreCase)) continue; // zip-slip koruması
                        if (string.IsNullOrEmpty(e.Name))
                        {
                            Directory.CreateDirectory(dest);
                            continue;
                        }
                        Directory.CreateDirectory(Path.GetDirectoryName(dest));
                        e.ExtractToFile(dest, true);
                        if (progress != null && (i % 5 == 0 || i == count)) progress(i * 100 / Math.Max(1, count));
                    }
                }

                // Zip tek bir üst klasörle geldiyse içeri al
                string source = tmp;
                var files = Directory.GetFiles(tmp);
                var dirs = Directory.GetDirectories(tmp);
                if (files.Length == 0 && dirs.Length == 1) source = dirs[0];

                if (!File.Exists(Path.Combine(source, GameExe)))
                    throw new InvalidOperationException("İndirilen pakette " + GameExe + " bulunamadı.");

                if (Directory.Exists(gameDir)) Directory.Delete(gameDir, true);
                Directory.Move(source, gameDir);
                if (Directory.Exists(tmp)) Directory.Delete(tmp, true);

                File.WriteAllText(VersionFile(root), tag);
                SaveRoot(root);
            });
        }

        // ------------------------------------------------------------ Kısayollar & kayıt

        static string DesktopLink { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Noctyra.lnk"); } }

        static string StartMenuDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Noctyra"); }
        }

        public static void CreateShortcuts(string root, bool desktop)
        {
            string exe = Path.Combine(GameDir(root), GameExe);
            Directory.CreateDirectory(StartMenuDir);
            CreateLink(Path.Combine(StartMenuDir, "Noctyra.lnk"), exe, GameDir(root), "Noctyra - Vampir Köylü");

            string launcher = LauncherCopy(root);
            if (File.Exists(launcher))
                CreateLink(Path.Combine(StartMenuDir, "Noctyra Güncelleyici.lnk"), launcher, root, "Noctyra güncelle / kaldır");

            if (desktop) CreateLink(DesktopLink, exe, GameDir(root), "Noctyra - Vampir Köylü");
            else TryDelete(DesktopLink);
        }

        static void CreateLink(string lnkPath, string target, string workDir, string description)
        {
            try
            {
                Type t = Type.GetTypeFromProgID("WScript.Shell");
                if (t == null) return;
                object shell = Activator.CreateInstance(t);
                object sc = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
                Type st = sc.GetType();
                st.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { target });
                st.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { workDir });
                st.InvokeMember("Description", BindingFlags.SetProperty, null, sc, new object[] { description });
                st.InvokeMember("IconLocation", BindingFlags.SetProperty, null, sc, new object[] { target + ",0" });
                st.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
            }
            catch { /* kısayol oluşmasa da kurulum geçerlidir */ }
        }

        /// <summary>Kurulum aracının kendisini kurulum klasörüne kopyalar (ileride güncelleme/kaldırma için).</summary>
        public static void CopyLauncher(string root)
        {
            try
            {
                string self = Assembly.GetExecutingAssembly().Location;
                string dest = LauncherCopy(root);
                if (!string.Equals(Path.GetFullPath(self), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase))
                    File.Copy(self, dest, true);
            }
            catch { }
        }

        public static void RegisterUninstall(string root, string version)
        {
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(UninstallKey))
                {
                    k.SetValue("DisplayName", "Noctyra");
                    k.SetValue("DisplayVersion", (version ?? "").TrimStart('v'));
                    k.SetValue("Publisher", "Noctyra");
                    k.SetValue("InstallLocation", root);
                    k.SetValue("DisplayIcon", Path.Combine(GameDir(root), GameExe));
                    k.SetValue("UninstallString", "\"" + LauncherCopy(root) + "\" --uninstall");
                    k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                }
            }
            catch { }
        }

        static void TryDelete(string file)
        {
            try { if (File.Exists(file)) File.Delete(file); } catch { }
        }

        public static void Uninstall(string root)
        {
            TryDelete(DesktopLink);
            try { if (Directory.Exists(StartMenuDir)) Directory.Delete(StartMenuDir, true); } catch { }
            try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false); } catch { }
            try { Registry.CurrentUser.DeleteSubKeyTree(AppKey, false); } catch { }

            try { if (Directory.Exists(GameDir(root))) Directory.Delete(GameDir(root), true); } catch { }

            // Çalışan kurulum aracı kendini silemez; birkaç saniye sonra cmd ile sildir.
            // Klasörü yalnızca boşsa kaldırır (kullanıcının başka dosyalarına dokunmaz).
            try
            {
                var psi = new ProcessStartInfo("cmd.exe",
                    "/c ping 127.0.0.1 -n 3 > nul & del /f /q \"" + LauncherCopy(root) + "\" & rmdir \"" + root + "\"")
                {
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                };
                Process.Start(psi);
            }
            catch { }
        }
    }
}
