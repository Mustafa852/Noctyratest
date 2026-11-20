using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NoctyraSetup
{
    public class MainForm : Form
    {
        static readonly Color Bg = Color.FromArgb(18, 16, 31);
        static readonly Color PanelBg = Color.FromArgb(30, 26, 52);
        static readonly Color Ink = Color.FromArgb(232, 227, 240);
        static readonly Color Muted = Color.FromArgb(168, 161, 192);
        static readonly Color Crimson = Color.FromArgb(194, 26, 58);

        readonly bool uninstallMode;
        string slug;
        ReleaseInfo latest;
        string installed;
        bool busy;
        int lastPct = -1;

        Label status, repoLabel;
        ProgressBar bar;
        TextBox notes, pathBox;
        Button browseBtn, installBtn, playBtn, removeBtn;
        CheckBox desktopCheck;

        public MainForm(bool uninstallMode)
        {
            this.uninstallMode = uninstallMode;
            BuildUi();
            Shown += OnShown;
        }

        // ------------------------------------------------------------------ Arayüz

        void BuildUi()
        {
            Text = "Noctyra Kurulum";
            ClientSize = new Size(760, 610);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Bg;
            ForeColor = Ink;
            Font = new Font("Segoe UI", 10f);

            var title = new Label
            {
                Text = "NOCTYRA",
                Font = new Font("Segoe UI", 34f, FontStyle.Bold),
                ForeColor = Crimson,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Bounds = new Rectangle(0, 14, 760, 64)
            };
            var sub = new Label
            {
                Text = "Vampir • Köylü  —  Kurulum ve Güncelleme",
                ForeColor = Muted,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Bounds = new Rectangle(0, 78, 760, 26)
            };

            status = new Label
            {
                Text = "Hazırlanıyor...",
                ForeColor = Ink,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                AutoSize = false,
                Bounds = new Rectangle(30, 116, 700, 28)
            };

            bar = new ProgressBar { Bounds = new Rectangle(30, 148, 700, 18), Minimum = 0, Maximum = 100 };

            var notesTitle = new Label
            {
                Text = "Sürüm notları",
                ForeColor = Muted,
                AutoSize = false,
                Bounds = new Rectangle(30, 178, 300, 22)
            };
            notes = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = PanelBg,
                ForeColor = Ink,
                BorderStyle = BorderStyle.FixedSingle,
                Bounds = new Rectangle(30, 202, 700, 200)
            };

            var pathLabel = new Label
            {
                Text = "Kurulum klasörü",
                ForeColor = Muted,
                AutoSize = false,
                Bounds = new Rectangle(30, 414, 300, 22)
            };
            pathBox = new TextBox
            {
                Text = Installer.LoadRoot(),
                BackColor = PanelBg,
                ForeColor = Ink,
                BorderStyle = BorderStyle.FixedSingle,
                Bounds = new Rectangle(30, 438, 610, 26)
            };
            browseBtn = MakeButton("Gözat", new Rectangle(650, 436, 80, 30), PanelBg);
            browseBtn.Click += OnBrowse;

            desktopCheck = new CheckBox
            {
                Text = "Masaüstüne kısayol oluştur",
                Checked = true,
                ForeColor = Ink,
                AutoSize = true,
                Location = new Point(30, 476)
            };

            installBtn = MakeButton("Kur", new Rectangle(30, 520, 270, 52), Crimson);
            installBtn.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            installBtn.Click += OnInstall;
            playBtn = MakeButton("Oyna", new Rectangle(312, 520, 270, 52), Color.FromArgb(60, 140, 100));
            playBtn.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            playBtn.Click += OnPlay;
            removeBtn = MakeButton("Kaldır", new Rectangle(594, 520, 136, 52), PanelBg);
            removeBtn.Click += OnRemove;

            repoLabel = new Label
            {
                ForeColor = Muted,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI", 8.5f),
                Bounds = new Rectangle(30, 582, 700, 20)
            };

            Controls.AddRange(new Control[]
            {
                title, sub, status, bar, notesTitle, notes, pathLabel, pathBox, browseBtn,
                desktopCheck, installBtn, playBtn, removeBtn, repoLabel
            });
        }

        static Button MakeButton(string text, Rectangle bounds, Color back)
        {
            var b = new Button
            {
                Text = text,
                Bounds = bounds,
                FlatStyle = FlatStyle.Flat,
                BackColor = back,
                ForeColor = Ink,
                UseVisualStyleBackColor = false,
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        // ------------------------------------------------------------------ Akış

        async void OnShown(object sender, EventArgs e)
        {
            if (uninstallMode)
            {
                DoUninstall();
                return;
            }
            await RefreshAsync();
        }

        string CurrentRoot()
        {
            string p = (pathBox.Text ?? "").Trim();
            if (p.Length == 0) p = Installer.DefaultRoot;
            // Kaldırırken başka dosyalara dokunmamak için her zaman "Noctyra" alt klasörü kullanılır
            if (!string.Equals(Path.GetFileName(p.TrimEnd('\\', '/')), "Noctyra", StringComparison.OrdinalIgnoreCase))
                p = Path.Combine(p, "Noctyra");
            return p;
        }

        async Task RefreshAsync()
        {
            SetBusy(true, "GitHub'dan sürüm bilgisi alınıyor...", true);
            slug = GitHubApi.RepoSlug();
            repoLabel.Text = "Depo: github.com/" + slug + "   •   Kurulum aracı v" + Application.ProductVersion;

            string error = null;
            latest = null;
            if (slug.StartsWith("OWNER/", StringComparison.OrdinalIgnoreCase))
            {
                error = "Depo adı ayarlanmamış. Bu exe'yi GitHub Actions ile derle ya da yanına 'repo.txt' koy (içinde kullanici/Noctyra).";
            }
            else
            {
                try { latest = await GitHubApi.GetLatestAsync(slug); }
                catch (Exception ex) { error = ex.Message; }
            }

            installed = Installer.InstalledVersion(CurrentRoot());
            SetBusy(false, null, false);
            UpdateUi(error);
        }

        void UpdateUi(string error)
        {
            bool isInstalled = installed != null;
            playBtn.Enabled = isInstalled;
            removeBtn.Enabled = isInstalled;
            pathBox.Enabled = !isInstalled;
            browseBtn.Enabled = !isInstalled;
            installBtn.Enabled = false;
            bar.Value = 0;

            if (error != null)
            {
                status.Text = "Sürüm bilgisi alınamadı.";
                notes.Text = error;
                installBtn.Text = "Kur";
                return;
            }
            if (latest == null)
            {
                status.Text = isInstalled ? "Kurulu sürüm: " + installed : "Henüz yayınlanmış bir sürüm yok.";
                notes.Text = "Bu depoda henüz bir GitHub Release yayınlanmamış.\r\n\r\n" +
                             "Bir sürüm yayınlayınca (ör. v0.2.0 etiketi) bu pencere onu otomatik bulur.";
                installBtn.Text = "Kur";
                return;
            }

            notes.Text = (latest.Name ?? latest.Tag) + "\r\n\r\n" +
                         ((latest.Body ?? "").Replace("\r\n", "\n").Replace("\n", "\r\n"));

            if (latest.AssetUrl == null)
            {
                status.Text = "Bu sürümde indirilecek .zip dosyası yok.";
                installBtn.Text = "Kur";
                return;
            }

            installBtn.Enabled = true;
            if (!isInstalled)
            {
                status.Text = "Son sürüm: " + latest.Tag + "  —  kuruluma hazır.";
                installBtn.Text = "Kur (" + latest.Tag + ")";
            }
            else if (!string.Equals(installed, latest.Tag, StringComparison.OrdinalIgnoreCase))
            {
                status.Text = "Yeni sürüm var: " + installed + "  →  " + latest.Tag;
                installBtn.Text = "Güncelle (" + latest.Tag + ")";
            }
            else
            {
                status.Text = "Güncelsin (" + installed + ").";
                installBtn.Text = "Yeniden kur";
            }
        }

        void SetBusy(bool isBusy, string message, bool indeterminate)
        {
            busy = isBusy;
            if (message != null) status.Text = message;
            bar.Style = isBusy && indeterminate ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
            if (isBusy)
            {
                installBtn.Enabled = false;
                playBtn.Enabled = false;
                removeBtn.Enabled = false;
                browseBtn.Enabled = false;
                pathBox.Enabled = false;
            }
        }

        static string Mb(long bytes) { return (bytes / 1048576.0).ToString("0.0") + " MB"; }

        void SetProgress(int pct)
        {
            if (pct == lastPct) return;
            lastPct = pct;
            BeginInvoke(new Action(() => { bar.Value = Math.Max(0, Math.Min(100, pct)); }));
        }

        async void OnInstall(object sender, EventArgs e)
        {
            if (busy || latest == null || latest.AssetUrl == null) return;
            if (Installer.IsGameRunning())
            {
                MessageBox.Show(this, "Noctyra şu an açık. Güncellemeden önce oyunu kapat.", "Noctyra",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string root = CurrentRoot();
            pathBox.Text = root;
            string tmpZip = Path.Combine(Path.GetTempPath(), "Noctyra-" + Guid.NewGuid().ToString("N") + ".zip");
            bool ok = false;
            lastPct = -1;

            try
            {
                SetBusy(true, "İndiriliyor...", false);
                bar.Value = 0;
                await GitHubApi.DownloadAsync(latest.AssetUrl, tmpZip, (done, total) =>
                {
                    if (total > 0) SetProgress((int)(done * 100 / total));
                    BeginInvoke(new Action(() =>
                    {
                        status.Text = "İndiriliyor...  " + Mb(done) + (total > 0 ? " / " + Mb(total) : "");
                    }));
                });

                if (!string.IsNullOrEmpty(latest.AssetSha256))
                {
                    SetBusy(true, "Dosya doğrulanıyor...", true);
                    string hash = await Task.Run(() => Installer.Sha256Of(tmpZip));
                    if (!string.Equals(hash, latest.AssetSha256, StringComparison.OrdinalIgnoreCase))
                        throw new Exception("İndirilen dosya doğrulanamadı (SHA-256 uyuşmuyor). Tekrar dene.");
                }

                SetBusy(true, "Kuruluyor...", false);
                lastPct = -1;
                await Installer.InstallZipAsync(tmpZip, root, latest.Tag, SetProgress);

                Installer.CopyLauncher(root);
                Installer.CreateShortcuts(root, desktopCheck.Checked);
                Installer.RegisterUninstall(root, latest.Tag);
                ok = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Kurulum başarısız:\n\n" + ex.Message, "Noctyra",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                try { if (File.Exists(tmpZip)) File.Delete(tmpZip); } catch { }
            }

            SetBusy(false, null, false);
            installed = Installer.InstalledVersion(root);
            UpdateUi(null);
            if (ok)
            {
                status.Text = "Kurulum tamamlandı: " + installed;
                bar.Value = 100;
            }
        }

        void OnPlay(object sender, EventArgs e)
        {
            try
            {
                string dir = Installer.GameDir(CurrentRoot());
                Process.Start(new ProcessStartInfo(Path.Combine(dir, Installer.GameExe))
                {
                    WorkingDirectory = dir,
                    UseShellExecute = true
                });
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Oyun başlatılamadı:\n" + ex.Message, "Noctyra", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void OnBrowse(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Noctyra'nın kurulacağı klasörü seç (içinde 'Noctyra' alt klasörü oluşturulur).";
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    pathBox.Text = dlg.SelectedPath;
                    pathBox.Text = CurrentRoot();
                }
            }
        }

        void OnRemove(object sender, EventArgs e)
        {
            DoUninstall();
        }

        void DoUninstall()
        {
            if (Installer.IsGameRunning())
            {
                MessageBox.Show(this, "Noctyra açık. Önce oyunu kapat.", "Noctyra", MessageBoxButtons.OK, MessageBoxIcon.Information);
                if (uninstallMode) Close();
                return;
            }
            var r = MessageBox.Show(this, "Noctyra bilgisayarından kaldırılsın mı?", "Noctyra",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.Yes)
            {
                if (uninstallMode) Close();
                return;
            }

            Installer.Uninstall(CurrentRoot());
            MessageBox.Show(this, "Noctyra kaldırıldı. Oyun kayıtların (skorların) silinmedi.", "Noctyra",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }
    }
}
