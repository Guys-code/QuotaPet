using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using QuotaPet.Core;

namespace QuotaPet.App;

internal sealed class PetForm : Form
{
    private readonly PictureBox _pet = new();
    private readonly Label _badge = new();
    private readonly ToolTip _badgeToolTip = new();
    private readonly ToolStripMenuItem _displayAll = new("全部显示");
    private readonly ToolStripMenuItem _displayGptPlus = new("仅 GPT Plus");
    private readonly ToolStripMenuItem _displayDeepSeek = new("仅 DeepSeek");
    private UsagePanelForm? _panel;
    private readonly System.Windows.Forms.Timer _animation = new() { Interval = 250 };
    private readonly NotifyIcon _tray;
    private readonly CodexSessionMonitor _monitor;
    private readonly DeepSeekBalanceMonitor _deepSeekMonitor;
    private readonly ClaudeDeepSeekUsageMonitor _claudeUsageMonitor;
    private readonly string _settingsPath;
    private readonly string _sessionsRoot;
    private readonly PetFrameSet _swimFrames;
    private readonly PetFrameSet _restFrames;
    private AppSettings _settings;
    private DeepSeekViewState _deepSeekState = DeepSeekViewState.NotConfigured;
    private ClaudeDeepSeekUsageSnapshot _claudeDeepSeekUsage = ClaudeDeepSeekUsageSnapshot.Empty;
    private bool _animationEnabled;
    private bool _wasAiActive;
    private int _animationFrame;
    private int _restTick;
    private bool _dragging;
    private bool _dragMoved;
    private Point _dragOrigin;
    private Point _formOrigin;
    private UsageSnapshot _snapshot = UsageSnapshot.Empty;
    private DateTimeOffset? _lastAiActivity;

    public PetForm()
    {
        _sessionsRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "sessions");
        _settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuotaPet", "settings.json");
        _settings = AppSettingsStore.Load(_settingsPath);
        _animationEnabled = _settings.AnimationEnabled;
        _deepSeekMonitor = new DeepSeekBalanceMonitor(_settings.DeepSeekLedger);
        _deepSeekMonitor.Updated += DeepSeekOnUpdated;
        var claudeProjectsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");
        _claudeUsageMonitor = new ClaudeDeepSeekUsageMonitor(claudeProjectsRoot);
        _claudeUsageMonitor.UsageUpdated += ClaudeUsageOnUpdated;

        var assetsPath = Path.Combine(AppContext.BaseDirectory, "Assets");
        _swimFrames = PetVisuals.LoadFrames(assetsPath, "pet-swim", moving: true);
        _restFrames = PetVisuals.LoadFrames(assetsPath, "pet-rest", moving: false);

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        ClientSize = new Size(124, 128);
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        StartPosition = FormStartPosition.Manual;
        DoubleBuffered = true;

        _pet.Image = _restFrames.Images[0];
        _pet.SizeMode = PictureBoxSizeMode.Zoom;
        _pet.BackColor = Color.Transparent;
        _pet.Bounds = new Rectangle(6, 8, 112, 112);
        Controls.Add(_pet);

        _badge.AutoSize = false;
        _badge.Size = new Size(24, 24);
        _badge.Location = new Point(96, 0);
        _badge.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
        _badge.ForeColor = Color.DodgerBlue;
        _badge.BackColor = Color.Transparent;
        _badge.TextAlign = ContentAlignment.MiddleCenter;
        Controls.Add(_badge);
        _badge.BringToFront();
        _badgeToolTip.SetToolTip(_badge, "正在等待用量数据");

        RegisterMouseHandlers(this);
        RegisterMouseHandlers(_pet);
        RegisterMouseHandlers(_badge);

        var menu = new ContextMenuStrip();
        var displayMenu = new ToolStripMenuItem("显示内容");
        displayMenu.DropDownItems.AddRange(new ToolStripItem[]
        {
            _displayAll, _displayGptPlus, _displayDeepSeek
        });
        _displayAll.Click += (_, _) => SetDisplayMode(UsageDisplayMode.All);
        _displayGptPlus.Click += (_, _) => SetDisplayMode(UsageDisplayMode.GptPlusOnly);
        _displayDeepSeek.Click += (_, _) => SetDisplayMode(UsageDisplayMode.DeepSeekOnly);
        UpdateDisplayModeMenu();
        var showPet = new ToolStripMenuItem("隐藏宠物");
        showPet.Click += (_, _) =>
        {
            Visible = !Visible;
            showPet.Text = Visible ? "隐藏宠物" : "显示宠物";
            if (!Visible) _panel?.Hide();
        };
        var pause = new ToolStripMenuItem(_animationEnabled ? "暂停动画" : "继续动画");
        pause.Click += (_, _) =>
        {
            _animationEnabled = !_animationEnabled;
            pause.Text = _animationEnabled ? "暂停动画" : "继续动画";
            if (!_animationEnabled) _pet.Top = 8;
        };
        var deepSeekSettings = new ToolStripMenuItem("设置 DeepSeek API");
        deepSeekSettings.Click += async (_, _) => await ConfigureDeepSeekAsync();
        var refreshDeepSeek = new ToolStripMenuItem("刷新 DeepSeek 余额");
        refreshDeepSeek.Click += async (_, _) => await _deepSeekMonitor.RefreshAsync();
        var openDeepSeekUsage = new ToolStripMenuItem("打开 DeepSeek 官方用量页面");
        openDeepSeekUsage.Click += (_, _) => Process.Start(new ProcessStartInfo(
            "https://platform.deepseek.com/usage") { UseShellExecute = true });
        var importDeepSeekHistory = new ToolStripMenuItem("导入 DeepSeek 官方费用 CSV/ZIP");
        importDeepSeekHistory.Click += async (_, _) => await ImportDeepSeekHistoryAsync();
        var openFolder = new ToolStripMenuItem("打开套餐用量数据目录");
        openFolder.Click += (_, _) => OpenDataFolder();
        var autoStart = new ToolStripMenuItem("开机启动") { Checked = AutoStartManager.IsEnabled(), CheckOnClick = true };
        autoStart.CheckedChanged += (_, _) => AutoStartManager.SetEnabled(autoStart.Checked);
        var exit = new ToolStripMenuItem("退出");
        exit.Click += (_, _) => Close();
        menu.Items.AddRange(new ToolStripItem[]
        {
            displayMenu, showPet, pause, autoStart, new ToolStripSeparator(),
            deepSeekSettings, refreshDeepSeek, openDeepSeekUsage, importDeepSeekHistory, openFolder,
            new ToolStripSeparator(), exit
        });
        ContextMenuStrip = menu;
        _pet.ContextMenuStrip = menu;
        _badge.ContextMenuStrip = menu;

        _tray = new NotifyIcon
        {
            Icon = SystemIcons.Information,
            Text = "QuotaPet 用量宠物",
            Visible = true,
            ContextMenuStrip = menu
        };
        _tray.DoubleClick += (_, _) => TogglePanel();

        _animation.Tick += (_, _) => Animate();
        _animation.Start();

        _monitor = new CodexSessionMonitor(_sessionsRoot);
        _monitor.UsageUpdated += MonitorOnUsageUpdated;
        Load += (_, _) => StartMonitoring();
        FormClosing += (_, _) => SaveSettings();
        FormClosed += (_, _) => DisposeFrames();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        if (_settings.X >= area.Left && _settings.X < area.Right - Width &&
            _settings.Y >= area.Top && _settings.Y < area.Bottom - Height)
            Location = new Point(_settings.X, _settings.Y);
        else
            Location = new Point(area.Right - Width - 28, area.Bottom - Height - 28);
    }

    private void StartMonitoring()
    {
        _monitor.Start();
        _deepSeekMonitor.Start();
        _ = Task.Run(_claudeUsageMonitor.Start);
    }

    private void MonitorOnUsageUpdated(object? sender, UsageSnapshot snapshot)
    {
        if (IsDisposed) return;
        if (InvokeRequired) BeginInvoke(() => ApplySnapshot(snapshot));
        else ApplySnapshot(snapshot);
    }

    private void ApplySnapshot(UsageSnapshot snapshot)
    {
        _snapshot = snapshot;
        RecordAiActivity(snapshot.ObservedAt);
        _panel?.SetSnapshot(snapshot, _deepSeekState, _claudeDeepSeekUsage);
        UpdateBadge();
        _tray.Text = snapshot.Primary is { } primary
            ? $"GPT Plus 五小时额度剩余 {UsageDisplayFormatter.Percent(primary.RemainingPercent)}"
            : "QuotaPet 用量宠物";
    }

    private void UpdateBadge()
    {
        var mood = PetMoodPolicy.GetMood(_settings.DisplayMode, _snapshot, _deepSeekState.Snapshot);
        (_badge.Text, _badge.ForeColor) = mood switch
        {
            PetMood.Normal => ("●", Color.MediumSeaGreen),
            PetMood.Tired => ("!", Color.DarkOrange),
            PetMood.Sleeping => ("Z", Color.MediumPurple),
            _ => ("…", Color.SteelBlue)
        };
        _badgeToolTip.SetToolTip(_badge, _settings.DisplayMode switch
        {
            UsageDisplayMode.GptPlusOnly => "GPT Plus 套餐状态",
            UsageDisplayMode.DeepSeekOnly => "DeepSeek 人民币余额状态",
            _ => "GPT Plus 与 DeepSeek 中较充足的一项"
        });
    }

    private void Animate()
    {
        var active = AiActivityPolicy.IsActive(_lastAiActivity, DateTimeOffset.Now);
        if (!_animationEnabled || FullscreenDetector.IsForegroundFullscreen(Handle))
        {
            _pet.Top = 8;
            _pet.Image = active ? _swimFrames.Images[0] : _restFrames.Images[0];
            return;
        }

        if (active != _wasAiActive)
        {
            _wasAiActive = active;
            _animationFrame = 0;
            _restTick = 0;
        }

        if (active)
        {
            _pet.Image = _swimFrames.Images[_animationFrame++ % _swimFrames.Images.Length];
        }
        else if (_restTick++ % 4 == 0)
        {
            _pet.Image = _restFrames.Images[_animationFrame++ % _restFrames.Images.Length];
        }
        _pet.Top = 8;
    }

    private void RegisterMouseHandlers(Control control)
    {
        control.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            _dragging = true;
            _dragMoved = false;
            _dragOrigin = Cursor.Position;
            _formOrigin = Location;
        };
        control.MouseMove += (_, e) =>
        {
            if (!_dragging || e.Button != MouseButtons.Left) return;
            var delta = new Size(Cursor.Position.X - _dragOrigin.X, Cursor.Position.Y - _dragOrigin.Y);
            if (Math.Abs(delta.Width) + Math.Abs(delta.Height) > 4) _dragMoved = true;
            Location = _formOrigin + delta;
            if (_panel?.Visible == true) _panel.ShowNear(Bounds);
        };
        control.MouseUp += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            _dragging = false;
            if (!_dragMoved) TogglePanel();
        };
    }

    private void TogglePanel()
    {
        _panel ??= new UsagePanelForm();
        if (_panel.Visible) _panel.Hide();
        else
        {
            _panel.SetDisplayMode(_settings.DisplayMode);
            _panel.SetSnapshot(_snapshot, _deepSeekState, _claudeDeepSeekUsage);
            _panel.ShowNear(Bounds);
        }
    }

    private void SetDisplayMode(UsageDisplayMode mode)
    {
        _settings = _settings with { DisplayMode = mode };
        UpdateDisplayModeMenu();
        UpdateBadge();
        SaveSettings();
        if (_panel is null) return;
        _panel.SetDisplayMode(mode);
        if (_panel.Visible) _panel.ShowNear(Bounds);
    }

    private void UpdateDisplayModeMenu()
    {
        _displayAll.Checked = _settings.DisplayMode == UsageDisplayMode.All;
        _displayGptPlus.Checked = _settings.DisplayMode == UsageDisplayMode.GptPlusOnly;
        _displayDeepSeek.Checked = _settings.DisplayMode == UsageDisplayMode.DeepSeekOnly;
    }

    private async Task ImportDeepSeekHistoryAsync()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "选择 DeepSeek 官方费用 CSV 或 ZIP",
            Filter = "DeepSeek 用量文件 (*.csv;*.zip)|*.csv;*.zip|CSV 文件 (*.csv)|*.csv|ZIP 文件 (*.zip)|*.zip",
            Multiselect = true,
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var entries = new List<DeepSeekOfficialCostEntry>();
            foreach (var path in dialog.FileNames) entries.AddRange(ReadOfficialCosts(path));
            if (entries.Count == 0) throw new FormatException("文件中没有可导入的 DeepSeek 费用记录。");
            var ledger = await _deepSeekMonitor.ImportOfficialCostsAsync(entries);
            _settings = _settings with { DeepSeekLedger = ledger };
            _deepSeekState = _deepSeekState with
            {
                IsConfigured = DeepSeekCredentialStore.IsConfigured,
                Ledger = ledger
            };
            SaveSettings();
            _panel?.SetSnapshot(_snapshot, _deepSeekState, _claudeDeepSeekUsage);
            MessageBox.Show(
                $"已导入 {entries.Count:N0} 条官方费用记录。重复导入同一文件不会重复计费。",
                "DeepSeek 历史费用", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or FormatException)
        {
            MessageBox.Show(ex.Message, "DeepSeek 历史费用导入失败",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static IReadOnlyList<DeepSeekOfficialCostEntry> ReadOfficialCosts(string path)
    {
        if (!Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            return DeepSeekOfficialCostImporter.Parse(File.ReadAllText(path));

        var result = new List<DeepSeekOfficialCostEntry>();
        using var archive = ZipFile.OpenRead(path);
        foreach (var item in archive.Entries.Where(x =>
                     x.Name.StartsWith("cost-", StringComparison.OrdinalIgnoreCase) &&
                     x.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)))
        {
            using var stream = item.Open();
            using var reader = new StreamReader(stream, Encoding.UTF8, true);
            result.AddRange(DeepSeekOfficialCostImporter.Parse(reader.ReadToEnd()));
        }
        return result;
    }

    private void DeepSeekOnUpdated(object? sender, DeepSeekViewState state)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            BeginInvoke(() => DeepSeekOnUpdated(sender, state));
            return;
        }
        _deepSeekState = state;
        if (state.Ledger is not null && state.Ledger != _settings.DeepSeekLedger)
        {
            _settings = _settings with { DeepSeekLedger = state.Ledger };
            SaveSettings();
        }
        _panel?.SetSnapshot(_snapshot, state, _claudeDeepSeekUsage);
        UpdateBadge();
    }

    private void ClaudeUsageOnUpdated(object? sender, ClaudeDeepSeekUsageSnapshot snapshot)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            BeginInvoke(() => ClaudeUsageOnUpdated(sender, snapshot));
            return;
        }
        _claudeDeepSeekUsage = snapshot;
        RecordAiActivity(snapshot.ObservedAt);
        _panel?.SetSnapshot(_snapshot, _deepSeekState, snapshot);
    }

    private void RecordAiActivity(DateTimeOffset observedAt)
    {
        if (observedAt == DateTimeOffset.MinValue) return;
        if (_lastAiActivity is null || observedAt > _lastAiActivity) _lastAiActivity = observedAt;
    }

    private void DisposeFrames()
    {
        _swimFrames.Dispose();
        _restFrames.Dispose();
        _badgeToolTip.Dispose();
    }

    private async Task ConfigureDeepSeekAsync()
    {
        using var dialog = new DeepSeekSettingsForm(DeepSeekCredentialStore.IsConfigured);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            if (dialog.RemoveRequested)
            {
                DeepSeekCredentialStore.Delete();
                _settings = _settings with { DeepSeekLedger = null };
                SaveSettings();
            }
            else if (!string.IsNullOrWhiteSpace(dialog.ApiKey))
            {
                DeepSeekCredentialStore.Save(dialog.ApiKey);
            }
            await _deepSeekMonitor.RefreshAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存 DeepSeek 设置失败：{ex.Message}", "QuotaPet",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenDataFolder()
    {
        if (!Directory.Exists(_sessionsRoot))
        {
            MessageBox.Show("尚未找到 Codex 会话目录。", "QuotaPet",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        Process.Start(new ProcessStartInfo("explorer.exe", _sessionsRoot) { UseShellExecute = true });
    }

    private void SaveSettings()
    {
        try
        {
            _settings = _settings with { X = Left, Y = Top, AnimationEnabled = _animationEnabled };
            AppSettingsStore.Save(_settingsPath, _settings);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animation.Dispose();
            _monitor.Dispose();
            _deepSeekMonitor.Dispose();
            _claudeUsageMonitor.Dispose();
            _panel?.Dispose();
            _tray.Visible = false;
            _tray.Dispose();
            _pet.Image = null;
        }
        base.Dispose(disposing);
    }
}
