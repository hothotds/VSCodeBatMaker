using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace VSCodeBatMaker
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }

    public class MainForm : Form
    {
        private readonly TextBox txtCode = new TextBox();
        private readonly TextBox txtFolder = new TextBox();
        private readonly TextBox txtBat = new TextBox();
        private readonly CheckBox chkNewWindow = new CheckBox();
        private readonly TextBox txtPreview = new TextBox();
        private readonly Label lblStatus = new Label();
        private bool batPathEditedByUser = false;

        public MainForm()
        {
            Text = "VS Code 启动 BAT 生成器";
            StartPosition = FormStartPosition.CenterScreen;
            Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);

            // 所有尺寸都以“字体行高(em)”为单位，布局交给表格自动排版，
            // 这样无论系统显示缩放是 100% 还是 200%，也无论从哪里启动，界面比例都一致。
            int em = Font.Height;
            int pad = em / 2;
            ClientSize = new System.Drawing.Size(em * 36, em * 34);
            MinimumSize = new System.Drawing.Size((int)(Width * 0.8), (int)(Height * 0.88));

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                Padding = new Padding(pad + pad / 2)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            void AddRow(Control c, bool fill = false)
            {
                root.RowStyles.Add(fill ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.AutoSize));
                c.Dock = DockStyle.Fill;
                root.Controls.Add(c, 0, root.RowStyles.Count - 1);
            }

            Label MakeLabel(string text) => new Label
            {
                Text = text,
                AutoSize = true,
                Margin = new Padding(0, pad, 0, 2)
            };

            Button MakeButton(string text) => new Button
            {
                Text = text,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(pad, 2, pad, 2),
                Margin = new Padding(0)
            };

            TableLayoutPanel MakePathRow(TextBox tb, Button btn)
            {
                var row = new TableLayoutPanel
                {
                    ColumnCount = 2,
                    RowCount = 1,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    Margin = new Padding(0)
                };
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                tb.Dock = DockStyle.Fill;
                tb.Margin = new Padding(0, 2, pad, 2);
                row.Controls.Add(tb, 0, 0);
                row.Controls.Add(btn, 1, 0);
                return row;
            }

            // --- 使用说明 ---
            var lblIntro = new Label
            {
                AutoSize = true,
                BackColor = System.Drawing.Color.FromArgb(240, 246, 255),
                ForeColor = System.Drawing.Color.FromArgb(40, 50, 70),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(pad),
                Margin = new Padding(0),
                Text =
                    "【使用说明】\n" +
                    "①  确认 VS Code 路径（Code.exe）—— 程序已自动检测，一般无需修改。\n" +
                    "②  选择你想用 VS Code 打开的文件夹。\n" +
                    "③  选择 BAT 的保存位置（默认放在桌面）。\n" +
                    "④  点击「生成 BAT 文件」，以后双击这个 BAT，就会直接用 VS Code 打开该文件夹。\n" +
                    "小提示：BAT 里写的是完整路径，可以随意复制、移动到桌面或任何位置使用；" +
                    "但如果之后移动了 VS Code 或该文件夹，需要重新生成一次。"
            };
            // 说明文字宽度跟随窗口，自动换行
            void FitIntro() => lblIntro.MaximumSize = new System.Drawing.Size(
                Math.Max(em * 10, ClientSize.Width - root.Padding.Horizontal), 0);
            SizeChanged += (s, e) => FitIntro();
            FitIntro();
            AddRow(lblIntro);

            // --- VS Code 路径 ---
            txtCode.Text = DetectCodeExe();
            txtCode.TextChanged += (s, e) => UpdatePreview();
            var btnCode = MakeButton("浏览...");
            btnCode.Click += BrowseCode;
            AddRow(MakeLabel("VS Code (Code.exe) 路径："));
            AddRow(MakePathRow(txtCode, btnCode));

            // --- 文件夹 ---
            txtFolder.TextChanged += (s, e) => { AutoFillBatPath(); UpdatePreview(); };
            var btnFolder = MakeButton("浏览...");
            btnFolder.Click += BrowseFolder;
            AddRow(MakeLabel("要打开的文件夹："));
            AddRow(MakePathRow(txtFolder, btnFolder));

            // --- BAT 位置 ---
            txtBat.TextChanged += (s, e) => UpdatePreview();
            txtBat.KeyPress += (s, e) => batPathEditedByUser = true;
            var btnBat = MakeButton("浏览...");
            btnBat.Click += BrowseBat;
            AddRow(MakeLabel("BAT 文件保存位置："));
            AddRow(MakePathRow(txtBat, btnBat));

            chkNewWindow.Text = "在新窗口中打开（-n）";
            chkNewWindow.AutoSize = true;
            chkNewWindow.Margin = new Padding(0, pad, 0, 0);
            chkNewWindow.CheckedChanged += (s, e) => UpdatePreview();
            AddRow(chkNewWindow);

            // --- 预览 ---
            AddRow(MakeLabel("BAT 内容预览："));
            txtPreview.Multiline = true;
            txtPreview.ReadOnly = true;
            txtPreview.ScrollBars = ScrollBars.Both;
            txtPreview.WordWrap = false;
            txtPreview.Font = new System.Drawing.Font("Consolas", 10F);
            txtPreview.MinimumSize = new System.Drawing.Size(0, em * 5);
            txtPreview.Margin = new Padding(0);
            AddRow(txtPreview, fill: true);

            // --- 生成按钮 + 状态 ---
            var btnGen = MakeButton("生成 BAT 文件");
            btnGen.Padding = new Padding(pad * 2, pad / 2, pad * 2, pad / 2);
            btnGen.Click += Generate;
            lblStatus.AutoSize = false;
            lblStatus.Dock = DockStyle.Fill;
            lblStatus.AutoEllipsis = true;
            lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lblStatus.Margin = new Padding(pad, 0, 0, 0);
            lblStatus.Text = "";

            var bottom = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, pad, 0, 0)
            };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottom.Controls.Add(btnGen, 0, 0);
            bottom.Controls.Add(lblStatus, 1, 0);
            AddRow(bottom);

            Controls.Add(root);

            UpdatePreview();
            ApplyAppIcon();
        }

        // 窗口 / 任务栏图标：优先使用本机 VS Code 的图标；找不到时用本程序 exe 自带的图标
        private void ApplyAppIcon()
        {
            try
            {
                var code = txtCode.Text.Trim();
                if (File.Exists(code))
                {
                    Icon = System.Drawing.Icon.ExtractAssociatedIcon(code);
                    return;
                }
                var self = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(self))
                    Icon = System.Drawing.Icon.ExtractAssociatedIcon(self);
            }
            catch { }
        }

        // 自动检测 Code.exe（用户安装 / 系统安装）
        private static string DetectCodeExe()
        {
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs", "Microsoft VS Code", "Code.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "Microsoft VS Code", "Code.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "Microsoft VS Code", "Code.exe"),
            };
            foreach (var c in candidates)
                if (File.Exists(c)) return c;
            return candidates[0]; // 没找到就填默认位置，用户可手动修改
        }

        private void BrowseCode(object? sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Title = "选择 Code.exe",
                Filter = "Code.exe|Code.exe|可执行文件 (*.exe)|*.exe",
                CheckFileExists = true
            };
            try
            {
                var dir = Path.GetDirectoryName(txtCode.Text);
                if (Directory.Exists(dir)) dlg.InitialDirectory = dir;
            }
            catch { }
            if (dlg.ShowDialog(this) == DialogResult.OK) txtCode.Text = dlg.FileName;
        }

        private void BrowseFolder(object? sender, EventArgs e)
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "选择要用 VS Code 打开的文件夹",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = false
            };
            if (Directory.Exists(txtFolder.Text)) dlg.SelectedPath = txtFolder.Text;
            if (dlg.ShowDialog(this) == DialogResult.OK) txtFolder.Text = dlg.SelectedPath;
        }

        private void BrowseBat(object? sender, EventArgs e)
        {
            using var dlg = new SaveFileDialog
            {
                Title = "选择 BAT 文件保存位置",
                Filter = "批处理文件 (*.bat)|*.bat",
                DefaultExt = "bat",
                AddExtension = true,
                OverwritePrompt = true
            };
            if (!string.IsNullOrWhiteSpace(txtBat.Text))
            {
                try
                {
                    dlg.InitialDirectory = Path.GetDirectoryName(txtBat.Text);
                    dlg.FileName = Path.GetFileName(txtBat.Text);
                }
                catch { }
            }
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                batPathEditedByUser = true;
                txtBat.Text = dlg.FileName;
            }
        }

        // 选择文件夹后，如果用户还没手动设置 bat 路径，则默认放到桌面，文件名为 "打开_文件夹名.bat"
        private void AutoFillBatPath()
        {
            if (batPathEditedByUser) return;
            var folder = txtFolder.Text.Trim().TrimEnd('\\', '/');
            if (string.IsNullOrEmpty(folder)) return;
            var name = Path.GetFileName(folder);
            if (string.IsNullOrEmpty(name)) name = "workspace";
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            txtBat.Text = Path.Combine(desktop, "打开_" + name + ".bat");
        }

        private static string Esc(string s) => s.Replace("%", "%%");

        private string BuildBatContent()
        {
            var code = Esc(txtCode.Text.Trim());
            var folder = Esc(txtFolder.Text.Trim().TrimEnd('\\', '/'));
            var newWin = chkNewWindow.Checked ? " -n" : "";
            var sb = new StringBuilder();
            sb.AppendLine("@echo off");
            sb.AppendLine("chcp 65001 >nul");
            sb.AppendLine($"start \"\" \"{code}\"{newWin} \"{folder}\"");
            return sb.ToString();
        }

        private void UpdatePreview()
        {
            txtPreview.Text = BuildBatContent();
        }

        private void Generate(object? sender, EventArgs e)
        {
            var code = txtCode.Text.Trim();
            var folder = txtFolder.Text.Trim();
            var bat = txtBat.Text.Trim();

            if (string.IsNullOrEmpty(code) || !File.Exists(code))
            {
                if (MessageBox.Show(this, "找不到 Code.exe：\n" + code + "\n\n仍然要生成吗？",
                        "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
            }
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                MessageBox.Show(this, "请选择一个存在的文件夹。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrEmpty(bat))
            {
                MessageBox.Show(this, "请设置 BAT 文件的保存位置。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!bat.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)) bat += ".bat";

            try
            {
                var dir = Path.GetDirectoryName(bat);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                if (File.Exists(bat) &&
                    MessageBox.Show(this, "文件已存在，是否覆盖？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;

                // UTF-8 无 BOM（配合 chcp 65001，支持中文路径）
                File.WriteAllText(bat, BuildBatContent(), new UTF8Encoding(false));
                txtBat.Text = bat;

                lblStatus.ForeColor = System.Drawing.Color.Green;
                lblStatus.Text = "✔ 生成成功：" + bat + "（" + DateTime.Now.ToString("HH:mm:ss") + "）";
            }
            catch (Exception ex)
            {
                lblStatus.Text = "";
                MessageBox.Show(this, "生成失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}