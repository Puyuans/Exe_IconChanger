/*
Exe Icon Changer - 美化版 (.NET 8 WinForms)
功能：
 - AE 风格主题（暗灰紫）+ 浅色 + 跟随系统
 - 拖拽导入 EXE/ICO 文件
 - 双图标预览（原图标/新图标）
 - 导出 ICO 图标按钮
 - 菜单栏：文件 / 编辑 / 主题 / 赞助作者
 - 自动备份、替换、还原、日志
 - 内置二维码（占位图，可替换 donate.png）
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ExeIconChanger
{
    public partial class MainForm : Form
    {
        private TextBox txtExePath = null!;
        private TextBox txtIcoPath = null!;
        private PictureBox picOriginal = null!;
        private PictureBox picNew = null!;
        private Button btnReplace = null!, btnRestore = null!, btnExportOld = null!;
        private ProgressBar progressBar = null!;
        private CheckBox chkBackup = null!;
        private ListBox lstLog = null!;
        private string currentExe = string.Empty;
        private string currentIco = string.Empty;

        private enum ThemeMode { Light, AE, System }
        private ThemeMode currentTheme = ThemeMode.AE;

        public MainForm()
        {
            InitializeComponent();
            ApplyTheme(currentTheme);
        }

        private void InitializeComponent()
        {
            this.Text = "图标替换工具 v1.0 璞源制作";
            this.ClientSize = new Size(1280, 720);
            this.AllowDrop = true;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            
            // 设置窗体图标
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icos", "IconChanger.ico");
                if (File.Exists(iconPath))
                {
                    this.Icon = new Icon(iconPath);
                }
            }
            catch (Exception ex)
            {
                // 图标加载失败不影响程序运行
                System.Diagnostics.Debug.WriteLine($"图标加载失败: {ex.Message}");
            }

            // ───────── 菜单栏 ─────────
            MenuStrip menu = new MenuStrip();
            ToolStripMenuItem mFile = new ToolStripMenuItem("文件");
            ToolStripMenuItem mEdit = new ToolStripMenuItem("编辑");
            ToolStripMenuItem mTheme = new ToolStripMenuItem("主题");
            ToolStripMenuItem mDonate = new ToolStripMenuItem("赞助作者");

            var openExe = new ToolStripMenuItem("打开 EXE 文件", null, (s, e) => BrowseExe());
            var openFolder = new ToolStripMenuItem("打开文件夹", null, (s, e) => BrowseFolder());
            mFile.DropDownItems.AddRange(new[] { openExe, openFolder });

            var convertToIco = new ToolStripMenuItem("图片转 ICO（256x256 高质量）", null, (s, e) => ConvertImageToIco());
            mEdit.DropDownItems.AddRange(new ToolStripItem[] { convertToIco });

            var themeLight = new ToolStripMenuItem("浅色主题", null, (s, e) => ApplyTheme(ThemeMode.Light));
            var themeSystem = new ToolStripMenuItem("跟随系统", null, (s, e) => ApplyTheme(ThemeMode.System));
            var themeAE = new ToolStripMenuItem("AE 风格主题（暗灰紫）", null, (s, e) => ApplyTheme(ThemeMode.AE));
            mTheme.DropDownItems.AddRange(new[] { themeLight, themeSystem, themeAE });

            mDonate.Click += (s, e) => new DonateForm().ShowDialog();

            menu.Items.AddRange(new[] { mFile, mEdit, mTheme, mDonate });
            this.MainMenuStrip = menu;
            menu.Dock = DockStyle.Top;
            this.Controls.Add(menu);

            // ───────── 固定布局：左/右面板 ─────────
            var leftPanel = new Panel { Location = new Point(10, 30), Size = new Size(360, 670) };
            var rightPanel = new Panel { Location = new Point(380, 30), Size = new Size(890, 670) };

            Label lblExe = new Label() { Text = "目标 EXE：", Location = new Point(10, 10), AutoSize = true };
            txtExePath = new TextBox() { Location = new Point(10, 35), Width = 260, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            Button btnBrowseExe = new Button() { Text = "浏览", Width = 80, Location = new Point(280, 33), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            btnBrowseExe.Click += (s, e) => BrowseExe();

            Label lblIco = new Label() { Text = "ICO 文件：", Location = new Point(10, 75), AutoSize = true };
            txtIcoPath = new TextBox() { Location = new Point(10, 100), Width = 260, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            Button btnBrowseIco = new Button() { Text = "浏览", Width = 80, Location = new Point(280, 98), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            btnBrowseIco.Click += (s, e) => BrowseIco();

            chkBackup = new CheckBox() { Text = "自动备份 (.bak)", Location = new Point(10, 140), Checked = true };
            btnReplace = new Button() { Text = "替换图标", Location = new Point(10, 170), Width = 160, Height = 36 };
            btnRestore = new Button() { Text = "还原备份", Location = new Point(180, 170), Width = 160, Height = 36 };
            btnReplace.Click += (s, e) => ReplaceIconAction();
            btnRestore.Click += (s, e) => RestoreBackup();

            progressBar = new ProgressBar() { Location = new Point(10, 215), Height = 16, Width = 350 };

            // 添加到左侧固定面板
            leftPanel.Controls.AddRange(new Control[] { lblExe, txtExePath, btnBrowseExe, lblIco, txtIcoPath, btnBrowseIco, chkBackup, btnReplace, btnRestore, progressBar });

            // 左侧：原图标预览组
            var grpOriginal = new GroupBox { Text = "原图标预览", Location = new Point(10, 250), Size = new Size(340, 400) };
            picOriginal = new PictureBox() { Location = new Point(10, 50), Size = new Size(320, 320), BorderStyle = BorderStyle.FixedSingle, SizeMode = PictureBoxSizeMode.Zoom };
            btnExportOld = new Button() { Text = "导出原图标", Width = 110, Location = new Point(220, 20) };
            btnExportOld.Click += (s, e) => ExportOriginalIco();
            grpOriginal.Controls.AddRange(new Control[] { picOriginal, btnExportOld });
            leftPanel.Controls.Add(grpOriginal);

            // 右侧：新图标预览与详情
            var rightPanelTop = new GroupBox { Text = "新图标预览", Location = new Point(0, 0), Size = new Size(890, 520) };
            picNew = new PictureBox() { Location = new Point(10, 50), Size = new Size(870, 450), BorderStyle = BorderStyle.FixedSingle, SizeMode = PictureBoxSizeMode.Zoom };
            rightPanelTop.Controls.Add(picNew);

            var rightPanelBottom = new GroupBox { Text = "详情信息", Location = new Point(0, 520), Size = new Size(890, 150) };
            lstLog = new ListBox() { Location = new Point(10, 22), Size = new Size(870, 120) };
            rightPanelBottom.Controls.Add(lstLog);

            rightPanel.Controls.Add(rightPanelTop);
            rightPanel.Controls.Add(rightPanelBottom);

            this.Controls.Add(leftPanel);
            this.Controls.Add(rightPanel);

            // 拖拽事件
            this.DragEnter += (s, e) => { if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy; };
            this.DragDrop += MainForm_DragDrop;
        }

        private void MainForm_DragDrop(object? sender, DragEventArgs e)
        {
            var data = e.Data;
            if (data == null) return;
            if (!data.GetDataPresent(DataFormats.FileDrop)) return;
            var files = data.GetData(DataFormats.FileDrop) as string[];
            if (files == null) return;
            foreach (var file in files)
            {
                if (file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) { txtExePath.Text = file; LoadExePreview(file); }
                else if (file.EndsWith(".ico", StringComparison.OrdinalIgnoreCase)) { txtIcoPath.Text = file; LoadIcoPreview(file); }
            }
        }

        private void ApplyTheme(ThemeMode mode)
        {
            currentTheme = mode;
            Color back, fore, accent, panel;

            if (mode == ThemeMode.AE)
            {
                // AE 暗灰主题 + 紫色点缀
                back = Color.FromArgb(40, 40, 40);        // 深灰背景
                panel = Color.FromArgb(55, 57, 62);       // 面板灰
                fore = Color.FromArgb(220, 220, 220);     // 浅灰文字
                accent = Color.FromArgb(174, 140, 255);   // AE 风格紫色点缀
            }
            else if (mode == ThemeMode.Light)
            {
                back = Color.WhiteSmoke; panel = Color.Gainsboro; fore = Color.Black; accent = Color.SteelBlue;
            }
            else // 跟随系统 (简单模拟)
            {
                back = SystemColors.Control; panel = SystemColors.ControlLight; fore = SystemColors.ControlText; accent = SystemColors.Highlight;
            }

            this.BackColor = back;
            foreach (Control c in this.Controls)
                ApplyThemeToControl(c, back, fore, accent, panel);
        }

        private void ApplyThemeToControl(Control c, Color back, Color fore, Color accent, Color panel)
        {
            if (c is Button b)
            {
                b.BackColor = panel; b.ForeColor = fore;
                b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderColor = accent;
            }
            else if (c is TextBox t)
            {
                t.BackColor = Color.FromArgb((back.R + panel.R) / 2, (back.G + panel.G) / 2, (back.B + panel.B) / 2);
                t.ForeColor = fore;
                t.BorderStyle = BorderStyle.FixedSingle;
            }
            else if (c is ListBox lb)
            {
                lb.BackColor = panel; lb.ForeColor = fore;
            }
            else if (c is Label l)
            {
                l.ForeColor = fore;
            }
            else if (c is CheckBox cb)
            {
                cb.ForeColor = fore;
            }
            else if (c is PictureBox pb)
            {
                pb.BackColor = panel;
            }
            else if (c is GroupBox gb)
            {
                gb.ForeColor = fore; gb.BackColor = back;
            }
            else if (c is Panel p)
            {
                p.BackColor = back;
            }
            else if (c is MenuStrip ms)
            {
                ms.BackColor = back; ms.ForeColor = fore;
                foreach (ToolStripItem item in ms.Items)
                {
                    item.ForeColor = fore;
                }
            }

            // 递归应用主题到子控件
            foreach (Control child in c.Controls)
            {
                ApplyThemeToControl(child, back, fore, accent, panel);
            }
        }

        private void BrowseExe()
        {
            using (var dlg = new OpenFileDialog() { Filter = "EXE 文件|*.exe" })
                if (dlg.ShowDialog() == DialogResult.OK) { txtExePath.Text = dlg.FileName; LoadExePreview(dlg.FileName); }
        }

        private void BrowseFolder()
        {
            using (var dlg = new FolderBrowserDialog())
                if (dlg.ShowDialog() == DialogResult.OK) { txtExePath.Text = dlg.SelectedPath; }
        }

        private void BrowseIco()
        {
            using (var dlg = new OpenFileDialog() { Filter = "ICO 文件|*.ico" })
                if (dlg.ShowDialog() == DialogResult.OK) { txtIcoPath.Text = dlg.FileName; LoadIcoPreview(dlg.FileName); }
        }

        private void LoadExePreview(string path)
        {
            try 
            { 
                var icon = ExtractLargestIcon(path);
                picOriginal.Image = icon?.ToBitmap(); 
                currentExe = path; 
            }
            catch (Exception ex) { Log($"EXE 图标预览失败: {ex.Message}"); }
        }

        private void LoadIcoPreview(string path)
        {
            try 
            { 
                var icon = ExtractLargestIconFromIco(path);
                picNew.Image = icon?.ToBitmap(); 
                currentIco = path; 
            }
            catch (Exception ex) { Log($"ICO 预览失败: {ex.Message}"); }
        }

        // 提取 EXE 最大尺寸图标（优先 256x256）
        private Icon? ExtractLargestIcon(string exePath)
        {
            try
            {
                var icon = Icon.ExtractAssociatedIcon(exePath);
                if (icon == null) return null;
                
                // 尝试提取 256x256 尺寸
                try
                {
                    using var ms = new MemoryStream();
                    icon.Save(ms);
                    ms.Position = 0;
                    return new Icon(ms, 256, 256);
                }
                catch
                {
                    return icon;
                }
            }
            catch
            {
                return Icon.ExtractAssociatedIcon(exePath);
            }
        }

        // 从 ICO 文件提取最大尺寸图标
        private Icon? ExtractLargestIconFromIco(string icoPath)
        {
            try
            {
                var icoBytes = File.ReadAllBytes(icoPath);
                var images = IconResourceUpdater.ParseIco(icoBytes);
                
                // 找到最大尺寸的图标
                var largest = images.OrderByDescending(i => i.Width * i.Height).FirstOrDefault();
                if (largest == null) return new Icon(icoPath);

                // 返回指定尺寸的图标
                return new Icon(icoPath, largest.Width, largest.Height);
            }
            catch
            {
                return new Icon(icoPath);
            }
        }

        private void ReplaceIconAction()
        {
            if (File.Exists(currentExe) && File.Exists(currentIco))
            {
                try
                {
                    if (chkBackup.Checked) BackupFile(currentExe);
                    IconResourceUpdater.ReplaceIcon(currentExe, currentIco);
                    Log("替换成功。"); LoadExePreview(currentExe);
                }
                catch (Exception ex) { Log($"替换失败: {ex.Message}"); }
            }
        }

        private void BackupFile(string path)
        {
            string bak = path + ".bak";
            if (!File.Exists(bak)) { File.Copy(path, bak); Log($"已备份 {Path.GetFileName(bak)}"); }
        }

        private void RestoreBackup()
        {
            if (File.Exists(currentExe + ".bak"))
            {
                File.Copy(currentExe + ".bak", currentExe, true);
                Log("已还原备份。"); LoadExePreview(currentExe);
            }
            else MessageBox.Show("未找到备份文件。", "提示");
        }

        // 导出原图标 - 使用高质量方法从 EXE 资源中提取完整 ICO 数据
        private void ExportOriginalIco()
        {
            if (string.IsNullOrEmpty(currentExe) || !File.Exists(currentExe))
            {
                MessageBox.Show("请先选择一个 EXE 文件。", "提示");
                return;
            }

            using (SaveFileDialog sfd = new SaveFileDialog() 
            { 
                Filter = "ICO 文件|*.ico",
                FileName = Path.GetFileNameWithoutExtension(currentExe) + "_original.ico"
            })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        // 使用高质量方法提取完整的 ICO 资源
                        byte[] icoData = AdvancedIconExtractor.ExtractIconResource(currentExe);
                        
                        if (icoData == null || icoData.Length == 0)
                        {
                            MessageBox.Show("无法从 EXE 中提取图标资源。", "提示");
                            return;
                        }
                        
                        // 保存完整的 ICO 文件
                        File.WriteAllBytes(sfd.FileName, icoData);
                        Log($"已导出原图标 ICO（保留所有尺寸和颜色）：{sfd.FileName}");
                        MessageBox.Show($"导出成功！\n已保存到：{sfd.FileName}", "成功");
                    }
                    catch (Exception ex)
                    {
                        Log($"导出失败: {ex.Message}");
                        MessageBox.Show($"导出失败：{ex.Message}", "错误");
                    }
                }
            }
        }

        // 智能图片转 ICO 功能 - 支持多尺寸，高质量算法
        private void ConvertImageToIco()
        {
            using (OpenFileDialog ofd = new OpenFileDialog() 
            { 
                Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tiff;*.webp|所有文件|*.*",
                Title = "选择要转换的图片（推荐使用 PNG 格式以保留透明度）"
            })
            {
                if (ofd.ShowDialog() != DialogResult.OK) return;

                using (SaveFileDialog sfd = new SaveFileDialog() 
                { 
                    Filter = "ICO 文件|*.ico",
                    FileName = Path.GetFileNameWithoutExtension(ofd.FileName) + ".ico"
                })
                {
                    if (sfd.ShowDialog() != DialogResult.OK) return;

                    try
                    {
                        Log("开始转换图片为 256x256 ICO...");
                        
                        // 使用高质量智能转换器
                        byte[] icoData = AdvancedIconConverter.ConvertImageToMultiSizeIcon(ofd.FileName);
                        
                        if (icoData == null || icoData.Length == 0)
                        {
                            MessageBox.Show("图片转换失败，请检查图片格式是否正确。", "错误");
                            return;
                        }
                        
                        // 保存 ICO 文件
                        File.WriteAllBytes(sfd.FileName, icoData);
                        
                        Log($"已将图片转换为 256x256 高质量 ICO：{sfd.FileName}");
                        MessageBox.Show($"转换成功！\n已生成 256x256 高质量 ICO 文件（PNG 格式）\n保存到：{sfd.FileName}", "成功");
                    }
                    catch (Exception ex)
                    {
                        Log($"图片转 ICO 失败: {ex.Message}");
                        MessageBox.Show($"转换失败：{ex.Message}", "错误");
                    }
                }
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        private void Log(string msg)
        {
            lstLog.Items.Add($"[{DateTime.Now:HH:mm:ss}] {msg}");
            lstLog.TopIndex = lstLog.Items.Count - 1;
        }
    }

    public class DonateForm : Form
    {
        private readonly PictureBox _pb;

        public DonateForm()
        {
            this.Text = "支持作者";
            this.Size = new Size(360, 480);

            _pb = new PictureBox() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.White };
            this.Controls.Add(_pb);

            // 优先加载用户提供的本地路径；否则加载内嵌资源；再否则占位图
            SetDonateImage();
        }

        private void SetDonateImage()
        {
            // 用户指定图片路径
            const string userPath = @"H:\文档资源\财务\收款码\支付宝W1.png";
            if (File.Exists(userPath))
            {
                _pb.Image = LoadImageClone(userPath);
                return;
            }

            // 内嵌资源
            var embedded = LoadEmbeddedDonateImage();
            if (embedded != null)
            {
                _pb.Image = embedded;
                return;
            }

            // 占位
            _pb.Image = GeneratePlaceholder();
        }

        // 以克隆方式加载，避免文件被锁定
        private static Image? LoadImageClone(string path)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var img = Image.FromStream(fs);
                return new Bitmap(img);
            }
            catch { return null; }
        }

        // 从内嵌资源加载图片：将图片添加到项目并设置“生成操作=Embedded resource”
        private Image? LoadEmbeddedDonateImage()
        {
            try
            {
                var asm = typeof(DonateForm).Assembly;
                // 尝试匹配包含 "donate" 关键字的资源名
                var name = Array.Find(asm.GetManifestResourceNames(), n => n.EndsWith("donate.png", StringComparison.OrdinalIgnoreCase) || n.Contains("donate", StringComparison.OrdinalIgnoreCase));
                if (name == null) return null;
                using var s = asm.GetManifestResourceStream(name);
                if (s == null) return null;
                return Image.FromStream(s);
            }
            catch { return null; }
        }

        private Bitmap GeneratePlaceholder()
        {
            Bitmap bmp = new Bitmap(350, 470);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.WhiteSmoke);
                var rect = new Rectangle(20, 20, bmp.Width - 40, bmp.Height - 80);
                using var pen = new Pen(Color.Gray, 2);
                g.DrawRectangle(pen, rect);
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("空白", new Font("微软雅黑", 11, FontStyle.Bold), Brushes.Gray, rect, sf);
            }
            return bmp;
        }
    }

    // 引用原版 IconResourceUpdater （简化版）
    public static class IconResourceUpdater
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern IntPtr BeginUpdateResource(string pFileName, [MarshalAs(UnmanagedType.Bool)] bool bDeleteExistingResources);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern bool UpdateResource(IntPtr hUpdate, IntPtr lpType, IntPtr lpName, short wLanguage, byte[] lpData, uint cbData);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern bool EndUpdateResource(IntPtr hUpdate, bool fDiscard);

        private static readonly IntPtr RT_ICON = (IntPtr)3;
        private static readonly IntPtr RT_GROUP_ICON = (IntPtr)14;

        public static void ReplaceIcon(string exePath, string icoPath)
        {
            byte[] icoBytes = File.ReadAllBytes(icoPath);
            var images = ParseIco(icoBytes);
            IntPtr hUpdate = BeginUpdateResource(exePath, false);
            int id = 1;
            foreach (var img in images)
                UpdateResource(hUpdate, RT_ICON, (IntPtr)id++, 0, img.ImageBytes, (uint)img.ImageBytes.Length);
            byte[] grp = BuildGroupIcon(images);
            UpdateResource(hUpdate, RT_GROUP_ICON, (IntPtr)1, 0, grp, (uint)grp.Length);
            EndUpdateResource(hUpdate, false);
        }

        public static List<IcoImage> ParseIco(byte[] ico)
        {
            List<IcoImage> imgs = new();
            using (BinaryReader br = new(new MemoryStream(ico)))
            {
                br.ReadUInt16(); br.ReadUInt16(); ushort count = br.ReadUInt16();
                for (int i = 0; i < count; i++)
                {
                    byte w = br.ReadByte(), h = br.ReadByte(), cc = br.ReadByte(); br.ReadByte();
                    ushort pl = br.ReadUInt16(), bc = br.ReadUInt16(); uint size = br.ReadUInt32(), offset = br.ReadUInt32();
                    long pos = br.BaseStream.Position; br.BaseStream.Seek(offset, SeekOrigin.Begin); byte[] img = br.ReadBytes((int)size); br.BaseStream.Seek(pos, SeekOrigin.Begin);
                    imgs.Add(new IcoImage { Width = w == 0 ? 256 : w, Height = h == 0 ? 256 : h, BitCount = bc, ImageBytes = img });
                }
            }
            return imgs;
        }

        private static byte[] BuildGroupIcon(List<IcoImage> imgs)
        {
            using (MemoryStream ms = new())
            using (BinaryWriter bw = new(ms))
            {
                bw.Write((ushort)0); bw.Write((ushort)1); bw.Write((ushort)imgs.Count);
                ushort id = 1;
                foreach (var img in imgs)
                {
                    bw.Write((byte)(img.Width == 256 ? 0 : img.Width));
                    bw.Write((byte)(img.Height == 256 ? 0 : img.Height));
                    bw.Write((byte)0); bw.Write((byte)0);
                    bw.Write((ushort)1); bw.Write((ushort)img.BitCount);
                    bw.Write((uint)img.ImageBytes.Length);
                    bw.Write(id++);
                }
                return ms.ToArray();
            }
        }

        public class IcoImage { public int Width, Height, BitCount; public byte[] ImageBytes = Array.Empty<byte>(); }
    }

    // ═══════════════════════════════════════════════════════════════
    // 高级图标提取器 - 从 EXE 中提取完整的 ICO 资源（保留所有颜色和透明度）
    // ═══════════════════════════════════════════════════════════════
    public static class AdvancedIconExtractor
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);
        
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr FindResource(IntPtr hModule, IntPtr lpName, IntPtr lpType);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadResource(IntPtr hModule, IntPtr hResInfo);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LockResource(IntPtr hResData);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint SizeofResource(IntPtr hModule, IntPtr hResInfo);

        private const uint LOAD_LIBRARY_AS_DATAFILE = 0x00000002;
        private static readonly IntPtr RT_GROUP_ICON = (IntPtr)14;
        private static readonly IntPtr RT_ICON = (IntPtr)3;

        public static byte[] ExtractIconResource(string exePath)
        {
            IntPtr hModule = LoadLibraryEx(exePath, IntPtr.Zero, LOAD_LIBRARY_AS_DATAFILE);
            if (hModule == IntPtr.Zero) return Array.Empty<byte>();

            try
            {
                // 查找第一个图标组
                IntPtr hResInfo = FindResource(hModule, (IntPtr)1, RT_GROUP_ICON);
                if (hResInfo == IntPtr.Zero) return Array.Empty<byte>();

                IntPtr hResData = LoadResource(hModule, hResInfo);
                IntPtr pResource = LockResource(hResData);
                uint size = SizeofResource(hModule, hResInfo);

                byte[] groupData = new byte[size];
                Marshal.Copy(pResource, groupData, 0, (int)size);

                // 解析图标组数据
                return BuildCompleteIconFile(hModule, groupData);
            }
            finally
            {
                FreeLibrary(hModule);
            }
        }

        private static byte[] BuildCompleteIconFile(IntPtr hModule, byte[] groupData)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                // ICO 文件头
                bw.Write((ushort)0); // Reserved
                bw.Write((ushort)1); // Type (1 = ICO)
                
                ushort iconCount = BitConverter.ToUInt16(groupData, 4);
                bw.Write(iconCount);

                var iconEntries = new List<(byte width, byte height, byte colors, byte reserved, ushort planes, ushort bpp, uint size, ushort id)>();
                
                // 读取图标目录条目
                int offset = 6;
                for (int i = 0; i < iconCount; i++)
                {
                    byte width = groupData[offset];
                    byte height = groupData[offset + 1];
                    byte colors = groupData[offset + 2];
                    byte reserved = groupData[offset + 3];
                    ushort planes = BitConverter.ToUInt16(groupData, offset + 4);
                    ushort bpp = BitConverter.ToUInt16(groupData, offset + 6);
                    uint size = BitConverter.ToUInt32(groupData, offset + 8);
                    ushort id = BitConverter.ToUInt16(groupData, offset + 12);
                    
                    iconEntries.Add((width, height, colors, reserved, planes, bpp, size, id));
                    offset += 14;
                }

                // 计算图标数据偏移量
                uint dataOffset = (uint)(6 + iconCount * 16);
                
                // 写入图标目录
                foreach (var entry in iconEntries)
                {
                    bw.Write(entry.width);
                    bw.Write(entry.height);
                    bw.Write(entry.colors);
                    bw.Write(entry.reserved);
                    bw.Write(entry.planes);
                    bw.Write(entry.bpp);
                    bw.Write(entry.size);
                    bw.Write(dataOffset);
                    dataOffset += entry.size;
                }

                // 写入图标数据
                foreach (var entry in iconEntries)
                {
                    IntPtr hResInfo = FindResource(hModule, (IntPtr)entry.id, RT_ICON);
                    if (hResInfo == IntPtr.Zero) continue;

                    IntPtr hResData = LoadResource(hModule, hResInfo);
                    IntPtr pResource = LockResource(hResData);
                    uint size = SizeofResource(hModule, hResInfo);

                    byte[] iconData = new byte[size];
                    Marshal.Copy(pResource, iconData, 0, (int)size);
                    bw.Write(iconData);
                }

                return ms.ToArray();
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 智能图标转换器 - 高质量 256x256 ICO 生成（使用 Lanczos 插值算法）
    // ═══════════════════════════════════════════════════════════════
    public static class AdvancedIconConverter
    {
        private const int IconSize = 256; // 固定使用 256x256 尺寸

        public static byte[] ConvertImageToMultiSizeIcon(string imagePath)
        {
            using var originalImage = Image.FromFile(imagePath);
            using var originalBitmap = new Bitmap(originalImage);

            // 检测是否有透明通道
            bool hasAlpha = HasAlphaChannel(originalBitmap);

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            // ICO 文件头
            bw.Write((ushort)0); // Reserved
            bw.Write((ushort)1); // Type (1 = ICO)
            bw.Write((ushort)1); // 图标数量：1 个

            // 生成 256x256 高质量图标数据
            byte[] iconData = GenerateIconData(originalBitmap, IconSize, hasAlpha);

            // 写入图标目录
            uint dataOffset = (uint)(6 + 1 * 16); // 6 字节头 + 1 个图标目录项（16 字节）

            bw.Write((byte)0); // Width (0 表示 256)
            bw.Write((byte)0); // Height (0 表示 256)
            bw.Write((byte)0); // Color palette
            bw.Write((byte)0); // Reserved
            bw.Write((ushort)1); // Color planes
            bw.Write((ushort)32); // Bits per pixel
            bw.Write((uint)iconData.Length); // Image data size
            bw.Write(dataOffset); // Image data offset

            // 写入图标数据
            bw.Write(iconData);

            return ms.ToArray();
        }

        private static bool HasAlphaChannel(Bitmap bitmap)
        {
            return bitmap.PixelFormat == System.Drawing.Imaging.PixelFormat.Format32bppArgb ||
                   bitmap.PixelFormat == System.Drawing.Imaging.PixelFormat.Format32bppPArgb;
        }

        private static byte[] GenerateIconData(Bitmap originalBitmap, int size, bool hasAlpha)
        {
            using var resizedBitmap = ResizeImageHighQuality(originalBitmap, size, size);
            using var ms = new MemoryStream();

            // 256x256 使用 PNG 格式保存（支持透明度，质量最佳，Windows Vista+ 支持）
            resizedBitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);

            return ms.ToArray();
        }

        private static Bitmap ResizeImageHighQuality(Bitmap source, int width, int height)
        {
            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            destImage.SetResolution(source.HorizontalResolution, source.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                // 使用最高质量的插值算法
                graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                using (var wrapMode = new System.Drawing.Imaging.ImageAttributes())
                {
                    wrapMode.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);
                    graphics.DrawImage(source, destRect, 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destImage;
        }
    }
}
