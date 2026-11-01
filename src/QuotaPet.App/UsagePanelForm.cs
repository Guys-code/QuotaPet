using QuotaPet.Core;

namespace QuotaPet.App;

internal sealed class UsagePanelForm : Form
{
    private readonly TableLayoutPanel _layout;
    private readonly List<int> _rowHeights = [];
    private readonly Label _status = ValueLabel();
    private readonly Label _primary = ValueLabel();
    private readonly ProgressBar _primaryBar = Bar();
    private readonly Label _secondary = ValueLabel();
    private readonly ProgressBar _secondaryBar = Bar();
    private readonly Label _session = ValueLabel();
    private readonly Label _last = ValueLabel();
    private readonly Label _context = ValueLabel();
    private readonly Label _deepSeekStatus = ValueLabel();
    private readonly Label _deepSeekBalance = ValueLabel();
    private readonly Label _deepSeekSpendRecent = ValueLabel();
    private readonly Label _deepSeekSpendPeriods = ValueLabel();
    private readonly Label _deepSeekTokensToday = ValueLabel();
    private readonly Label _deepSeekTokensTotal = ValueLabel();
    private readonly Label _deepSeekTokensLast = ValueLabel();
    private readonly System.Windows.Forms.Timer _clock = new() { Interval = 1000 };
    private UsageSnapshot _snapshot = UsageSnapshot.Empty;
    private DeepSeekViewState _deepSeek = DeepSeekViewState.NotConfigured;
    private ClaudeDeepSeekUsageSnapshot _claudeUsage = ClaudeDeepSeekUsageSnapshot.Empty;
    private UsageDisplayMode _displayMode = UsageDisplayMode.All;

    public UsagePanelForm()
    {
        Text = "GPT Plus 套餐与 API 用量";
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        ControlBox = false;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        ClientSize = new Size(410, 680);
        BackColor = Color.FromArgb(247, 251, 255);
        Font = new Font("Microsoft YaHei UI", 9F);

        _layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16, 12, 16, 12),
            ColumnCount = 1,
            RowCount = 23,
            AutoSize = false
        };
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(Heading("GPT Plus 套餐用量"), 38);
        AddRow(_status, 30);
        AddRow(Section("5 小时窗口"), 26);
        AddRow(_primary, 28);
        AddRow(_primaryBar, 18);
        AddRow(Section("每周窗口"), 26);
        AddRow(_secondary, 28);
        AddRow(_secondaryBar, 18);
        AddRow(Section("Token"), 26);
        AddRow(_last, 28);
        AddRow(_session, 28);
        AddRow(_context, 28);
        AddRow(Section("DeepSeek API 费用"), 30);
        AddRow(_deepSeekStatus, 28);
        AddRow(_deepSeekBalance, 28);
        AddRow(_deepSeekSpendRecent, 28);
        AddRow(_deepSeekSpendPeriods, 28);
        AddRow(Section("DeepSeek Token（Claude Code）"), 30);
        AddRow(_deepSeekTokensToday, 28);
        AddRow(_deepSeekTokensTotal, 28);
        AddRow(_deepSeekTokensLast, 28);
        AddRow(new Label
        {
            Text = "套餐与 Token 来自本机日志；DeepSeek 金额来自官方余额接口",
            ForeColor = Color.SlateGray,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            AutoEllipsis = true
        }, 44);
        Controls.Add(_layout);
        _clock.Tick += (_, _) => Render();
        _clock.Start();
        Render();
    }

    public void SetDisplayMode(UsageDisplayMode mode)
    {
        _displayMode = mode;
        for (var row = 0; row < _rowHeights.Count; row++)
        {
            var show = row == 21 || mode switch
            {
                UsageDisplayMode.GptPlusOnly => row <= 11,
                UsageDisplayMode.DeepSeekOnly => row is >= 12 and <= 20,
                _ => true
            };
            _layout.RowStyles[row].Height = show ? _rowHeights[row] : 0;
            foreach (Control control in _layout.Controls)
                if (_layout.GetRow(control) == row) control.Visible = show;
        }
        ClientSize = new Size(410, mode switch
        {
            UsageDisplayMode.GptPlusOnly => 400,
            UsageDisplayMode.DeepSeekOnly => 334,
            _ => 680
        });
        Text = mode switch
        {
            UsageDisplayMode.GptPlusOnly => "GPT Plus 套餐用量",
            UsageDisplayMode.DeepSeekOnly => "DeepSeek API 用量",
            _ => "GPT Plus 套餐与 DeepSeek API 用量"
        };
    }

    public void SetSnapshot(
        UsageSnapshot snapshot,
        DeepSeekViewState deepSeek,
        ClaudeDeepSeekUsageSnapshot claudeUsage)
    {
        _snapshot = snapshot;
        _deepSeek = deepSeek;
        _claudeUsage = claudeUsage;
        Render();
    }

    public void ShowNear(Rectangle petBounds)
    {
        var area = Screen.FromRectangle(petBounds).WorkingArea;
        var x = petBounds.Right + 8;
        if (x + Width > area.Right) x = petBounds.Left - Width - 8;
        var y = Math.Clamp(petBounds.Top - 80, area.Top, Math.Max(area.Top, area.Bottom - Height));
        Location = new Point(Math.Max(area.Left, x), y);
        Show();
        BringToFront();
    }

    private void Render()
    {
        if (!_snapshot.HasData)
        {
            _status.Text = "正在等待 GPT Plus 套餐用量数据…";
            _primary.Text = _secondary.Text = "暂无数据";
            _session.Text = "当前会话：—";
            _last.Text = "最近请求：—";
            _context.Text = "上下文窗口：—";
            _primaryBar.Value = _secondaryBar.Value = 0;
        }
        else
        {
            _status.Text = $"GPT PLUS · 更新于 {_snapshot.ObservedAt.ToLocalTime():HH:mm:ss}";
            RenderWindow(_snapshot.Primary, _primary, _primaryBar);
            RenderWindow(_snapshot.Secondary, _secondary, _secondaryBar);
            _last.Text = $"最近请求：{UsageDisplayFormatter.Tokens(_snapshot.LastTokens.TotalTokens)} tokens";
            _session.Text = $"当前会话：{UsageDisplayFormatter.Tokens(_snapshot.SessionTokens.TotalTokens)} tokens";
            _context.Text = _snapshot.ModelContextWindow is { } context
                ? $"上下文窗口：{UsageDisplayFormatter.Tokens(context)} tokens"
                : "上下文窗口：未知";
        }

        RenderDeepSeek();
        RenderClaudeDeepSeekUsage();
    }

    private void RenderClaudeDeepSeekUsage()
    {
        if (_claudeUsage.RequestCount == 0)
        {
            _deepSeekTokensToday.Text = "今日：暂无 DeepSeek 调用记录";
            _deepSeekTokensTotal.Text = "本机累计：—";
            _deepSeekTokensLast.Text = "最近请求：—";
            return;
        }
        _deepSeekTokensToday.Text = $"今日：{UsageDisplayFormatter.Tokens(_claudeUsage.Today.TotalTokens)} tokens";
        _deepSeekTokensTotal.Text =
            $"本机累计：{UsageDisplayFormatter.Tokens(_claudeUsage.Total.TotalTokens)} · {_claudeUsage.RequestCount:N0} 次";
        _deepSeekTokensLast.Text =
            $"最近：{UsageDisplayFormatter.Tokens(_claudeUsage.LastRequest.TotalTokens)} " +
            $"(入 {_claudeUsage.LastRequest.InputTokens:N0} / 出 {_claudeUsage.LastRequest.OutputTokens:N0} / 缓存 {_claudeUsage.LastRequest.CacheReadInputTokens + _claudeUsage.LastRequest.CacheCreationInputTokens:N0})";
    }

    private void RenderDeepSeek()
    {
        if (!_deepSeek.IsConfigured)
        {
            _deepSeekStatus.Text = "未配置 · 右键宠物选择“设置 DeepSeek API”";
            _deepSeekBalance.Text = "当前余额：—";
            _deepSeekSpendRecent.Text = "近 3 小时 / 今日消费：—";
            _deepSeekSpendPeriods.Text = "本周 / 本月消费：—";
            return;
        }
        RenderSpendPeriods();
        if (_deepSeek.IsLoading)
        {
            _deepSeekStatus.Text = "正在查询官方余额…";
            return;
        }
        if (!string.IsNullOrEmpty(_deepSeek.Error))
        {
            _deepSeekStatus.Text = _deepSeek.Error;
            _deepSeekBalance.Text = "当前余额：—";
            return;
        }

        var balances = _deepSeek.Snapshot?.Balances ?? [];
        _deepSeekStatus.Text = _deepSeek.Snapshot is { } snapshot
            ? $"{(snapshot.IsAvailable ? "可用" : "余额不足")} · 更新于 {snapshot.ObservedAt:HH:mm:ss}"
            : "等待查询…";
        _deepSeekBalance.Text = "当前余额：" + FormatAmounts(balances.Select(x => (x.Currency, x.TotalBalance)));
    }

    private void RenderSpendPeriods()
    {
        var currencies = _deepSeek.Ledger?.Currencies.Keys.ToArray() ?? [];
        if (currencies.Length == 0)
        {
            _deepSeekSpendRecent.Text = "近 3 小时：— · 今日：—";
            _deepSeekSpendPeriods.Text = "本周：— · 本月：—";
            return;
        }
        var summaries = currencies.Select(currency =>
            (Currency: currency, Summary: DeepSeekSpendTracker.Summarize(_deepSeek.Ledger, currency))).ToArray();
        _deepSeekSpendRecent.Text =
            "近 3 小时：" + FormatAmounts(summaries.Select(x => (x.Currency, x.Summary.LastThreeHours))) +
            " · 今日：" + FormatAmounts(summaries.Select(x => (x.Currency, x.Summary.Today)));
        _deepSeekSpendPeriods.Text =
            "本周：" + FormatAmounts(summaries.Select(x => (x.Currency, x.Summary.ThisWeek))) +
            " · 本月：" + FormatAmounts(summaries.Select(x => (x.Currency, x.Summary.ThisMonth)));
    }

    private static string FormatAmounts(IEnumerable<(string Currency, decimal Amount)> amounts)
    {
        var values = amounts.Select(x => x.Currency.Equals("CNY", StringComparison.OrdinalIgnoreCase)
            ? $"¥{x.Amount:N2}"
            : x.Currency.Equals("USD", StringComparison.OrdinalIgnoreCase)
                ? $"${x.Amount:N2}"
                : $"{x.Amount:N2} {x.Currency}").ToArray();
        return values.Length == 0 ? "—" : string.Join(" / ", values);
    }

    private static void RenderWindow(RateLimitWindow? window, Label label, ProgressBar bar)
    {
        if (window is null)
        {
            label.Text = "暂无数据";
            bar.Value = 0;
            return;
        }
        label.Text = $"已用 {UsageDisplayFormatter.Percent(window.UsedPercent)} · 剩余 {UsageDisplayFormatter.Percent(window.RemainingPercent)} · {UsageDisplayFormatter.Countdown(window.ResetsAt)}后重置";
        bar.Value = Math.Clamp((int)Math.Round(window.RemainingPercent), 0, 100);
    }

    private static Label Heading(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold),
        ForeColor = Color.FromArgb(24, 73, 117),
        TextAlign = ContentAlignment.MiddleLeft
    };

    private static Label Section(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
        ForeColor = Color.FromArgb(55, 96, 135),
        TextAlign = ContentAlignment.BottomLeft
    };

    private static Label ValueLabel() => new()
    {
        Dock = DockStyle.Fill,
        ForeColor = Color.FromArgb(42, 50, 58),
        TextAlign = ContentAlignment.MiddleLeft,
        AutoEllipsis = true
    };

    private static ProgressBar Bar() => new() { Dock = DockStyle.Fill, Minimum = 0, Maximum = 100 };

    private void AddRow(Control control, int height)
    {
        _rowHeights.Add(height);
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        _layout.Controls.Add(control, 0, _layout.RowStyles.Count - 1);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _clock.Dispose();
        base.Dispose(disposing);
    }
}
