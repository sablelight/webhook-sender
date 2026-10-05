#nullable disable
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WebhookSender;

public partial class MainWindow : Window
{
    internal readonly List<Msg> _msgs = new();

    public MainWindow()
    {
        InitializeComponent();
        AddMessage();
    }

    void Log(string m) => Dispatcher.Invoke(() => StatusBox.Text = m);

    void ClearLog_Click(object s, RoutedEventArgs e) => StatusBox.Clear();

    void AddMsg_Click(object s, RoutedEventArgs e) => AddMessage();

    void AddMessage()
    {
        var m = new Msg(_msgs.Count + 1, this);
        _msgs.Add(m);
        m.BuildUI(MsgPanel);
        m.OnChange += RefreshPreviews;
        RefreshPreviews();
        m.ExpandFirst();
    }

    internal void RefreshPreviews()
    {
        PreviewPanel.Children.Clear();
        foreach (var m in _msgs)
            RenderPreview(m);
    }

    void SendAll_Click(object s, RoutedEventArgs e)
    {
        var url = WebhookBox.Text.Trim();
        if (string.IsNullOrEmpty(url)) { MessageBox.Show("Enter a webhook URL."); return; }
        if (!url.StartsWith("https://discord.com/api/webhooks/")) { MessageBox.Show("Invalid webhook URL."); return; }
        var un = UsernameBox.Text.Trim();
        var av = "";

        foreach (var m in _msgs)
        {
            var j = m.BuildJson(un, av);
            if (string.IsNullOrEmpty(j)) continue;
            int n = m.Number;
            ThreadPool.QueueUserWorkItem(_ => HttpSend(url, j, n));
        }
    }

    void HttpSend(string url, string json, int n)
    {
        using var cl = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var ct = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        for (int att = 0; att < 3; att++)
        {
            try
            {
                var r = cl.PostAsync(url, ct).Result;
                if (r.StatusCode == System.Net.HttpStatusCode.NoContent)
                { Log($"#{n}: sent OK"); return; }
                if (r.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    int retry = 2;
                    try { retry = (int)JsonDocument.Parse(r.Content.ReadAsStringAsync().Result).RootElement.GetProperty("retry_after").GetDouble() + 1; } catch { }
                    Log($"#{n}: rate limit, waiting {retry}s");
                    Thread.Sleep(retry * 1000); continue;
                }
                var err = r.Content.ReadAsStringAsync().Result;
                Log($"#{n}: {r.StatusCode} {err[..Math.Min(err.Length, 200)]}");
                return;
            }
            catch (Exception ex) { Log($"#{n}: {ex.Message}"); return; }
        }
    }

    // ─── Preview Renderer ────────────────────────

    void RenderPreview(Msg m)
    {
        var p = PreviewPanel;

        var msgOuter = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
        var topRow = new StackPanel { Orientation = Orientation.Horizontal };

        // Avatar
        var avatar = new Border
        {
            Width = 40, Height = 40, CornerRadius = new CornerRadius(20),
            Background = new SolidColorBrush(Color.FromRgb(88, 101, 242)),
            Margin = new Thickness(0, 0, 12, 0)
        };
        avatar.Child = new TextBlock
        {
            Text = "W", Foreground = Brushes.White, FontSize = 18,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var bodyCol = new StackPanel { Width = double.NaN };

        // Username row
        var nameRow = new StackPanel { Orientation = Orientation.Horizontal, Height = 20 };
        nameRow.Children.Add(new TextBlock
        {
            Text = string.IsNullOrEmpty(UsernameBox.Text.Trim()) ? "Webhook" : UsernameBox.Text.Trim(),
            Foreground = new SolidColorBrush(Color.FromRgb(242, 243, 245)),
            FontSize = 15, FontWeight = FontWeights.Medium
        });
        var badge = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(88, 101, 242)),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 1, 4, 1),
            Margin = new Thickness(6, 2, 0, 0),
            Height = 15
        };
        badge.Child = new TextBlock { Text = "BOT", Foreground = Brushes.White, FontSize = 9, FontWeight = FontWeights.Bold };
        nameRow.Children.Add(badge);
        nameRow.Children.Add(new TextBlock
        {
            Text = "  Today at " + DateTime.Now.ToString("h:mm tt"),
            Foreground = new SolidColorBrush(Color.FromRgb(148, 155, 164)),
            FontSize = 11, VerticalAlignment = VerticalAlignment.Center
        });
        bodyCol.Children.Add(nameRow);

        // Content
        var ct = m.GetContent();
        if (!string.IsNullOrEmpty(ct))
        {
            bodyCol.Children.Add(new TextBlock
            {
                Text = ct, Foreground = new SolidColorBrush(Color.FromRgb(219, 222, 225)),
                FontSize = 14, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            });
        }

        // Embeds
        foreach (var e in m.GetEmbeds())
            bodyCol.Children.Add(RenderEmbed(e));

        // Action Buttons
        var btns = m.GetButtons();
        if (btns.Count > 0)
        {
            var wrap = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            foreach (var b in btns)
            {
                wrap.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(43, 45, 49)),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(14, 6, 14, 6),
                    Margin = new Thickness(0, 0, 6, 4),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(67, 67, 73)),
                    BorderThickness = new Thickness(1, 1, 1, 1),
                    Child = new TextBlock { Text = b.Label, Foreground = new SolidColorBrush(Color.FromRgb(219, 222, 225)), FontSize = 13 }
                });
            }
            bodyCol.Children.Add(wrap);
        }

        topRow.Children.Add(avatar);
        topRow.Children.Add(bodyCol);
        msgOuter.Children.Add(topRow);
        p.Children.Add(msgOuter);
    }

    Border RenderEmbed(EmbedData e)
    {
        var accent = e.Color.HasValue
            ? new SolidColorBrush(Color.FromRgb((byte)(e.Color.Value >> 16), (byte)(e.Color.Value >> 8), (byte)(e.Color.Value)))
            : new SolidColorBrush(Color.FromRgb(88, 185, 255));

        var inner = new StackPanel { Margin = new Thickness(12, 8, 12, 8) };

        if (!string.IsNullOrEmpty(e.AuthorName))
        {
            var ar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            if (!string.IsNullOrEmpty(e.AuthorIcon))
                ar.Children.Add(new Border { Width = 20, Height = 20, CornerRadius = new CornerRadius(10), Background = new SolidColorBrush(Color.FromRgb(148, 155, 164)), Margin = new Thickness(0, 0, 6, 0) });
            ar.Children.Add(new TextBlock { Text = e.AuthorName, Foreground = new SolidColorBrush(Color.FromRgb(242, 243, 245)), FontSize = 13 });
            inner.Children.Add(ar);
        }

        if (!string.IsNullOrEmpty(e.Title))
            inner.Children.Add(new TextBlock
            {
                Text = e.Title,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 175, 244)),
                FontSize = 15, FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap
            });

        if (!string.IsNullOrEmpty(e.Description))
            inner.Children.Add(new TextBlock
            {
                Text = e.Description, Foreground = new SolidColorBrush(Color.FromRgb(219, 222, 225)),
                FontSize = 13, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            });

        if (e.Fields.Count > 0)
        {
            var wp = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            foreach (var f in e.Fields)
            {
                double w = f.Inline ? 156 : 380;
                var fp = new StackPanel { Width = w, Margin = new Thickness(0, 0, 8, 8) };
                fp.Children.Add(new TextBlock { Text = f.Name, Foreground = new SolidColorBrush(Color.FromRgb(242, 243, 245)), FontSize = 13, FontWeight = FontWeights.SemiBold });
                fp.Children.Add(new TextBlock { Text = f.Value, Foreground = new SolidColorBrush(Color.FromRgb(219, 222, 225)), FontSize = 13, TextWrapping = TextWrapping.Wrap });
                wp.Children.Add(fp);
            }
            inner.Children.Add(wp);
        }

        if (!string.IsNullOrEmpty(e.Image))
            inner.Children.Add(new Border
            {
                Height = 160, CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 8, 0, 0),
                Background = new SolidColorBrush(Color.FromRgb(30, 31, 34)),
                Child = new TextBlock { Text = "[Image]", Foreground = new SolidColorBrush(Color.FromRgb(148, 155, 164)), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            });

        if (!string.IsNullOrEmpty(e.FooterText))
        {
            var fr = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            if (!string.IsNullOrEmpty(e.FooterIcon))
                fr.Children.Add(new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(8), Background = new SolidColorBrush(Color.FromRgb(148, 155, 164)), Margin = new Thickness(0, 0, 6, 0) });
            fr.Children.Add(new TextBlock { Text = e.FooterText, Foreground = new SolidColorBrush(Color.FromRgb(148, 155, 164)), FontSize = 11 });
            if (e.HasTimestamp)
                fr.Children.Add(new TextBlock { Text = " \u2022 " + DateTime.UtcNow.ToString("MMM dd, yyyy h:mm tt"), Foreground = new SolidColorBrush(Color.FromRgb(148, 155, 164)), FontSize = 11 });
            inner.Children.Add(fr);
        }

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(new Border { Background = accent, CornerRadius = new CornerRadius(4, 0, 0, 4), Width = 4, HorizontalAlignment = HorizontalAlignment.Left });
        grid.Children.Add(inner);
        Grid.SetColumn(inner, 1);

        return new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(55, 55, 61)),
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(0, 6, 0, 0),
            BorderBrush = new SolidColorBrush(Color.FromRgb(67, 67, 73)),
            BorderThickness = new Thickness(1, 1, 1, 1),
            MaxWidth = 520,
            Child = grid
        };
    }
}

// ═══════════════════════════════════════════════════════════
//  UTILITY
// ═══════════════════════════════════════════════════════════

static class UI
{
    internal static readonly SolidColorBrush IB = new(Color.FromRgb(51, 51, 56));
    internal static readonly SolidColorBrush FG = new(Color.FromRgb(219, 222, 225));
    internal static readonly SolidColorBrush FG2 = new(Color.FromRgb(148, 155, 164));
    internal static readonly SolidColorBrush Blurple = new(Color.FromRgb(88, 101, 242));
    internal static readonly SolidColorBrush Red = new(Color.FromRgb(218, 55, 60));
    internal static readonly SolidColorBrush BG4 = new(Color.FromRgb(43, 45, 49));
    internal static readonly FontFamily Font = new("Whitney, Helvetica, Segoe UI, sans-serif");

    internal static Border InputBox()
    {
        return new Border { Background = IB, CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 6, 8, 6), Margin = new Thickness(0, 2, 0, 2) };
    }

    internal static TextBox MakeTb(string hint = "")
    {
        return new TextBox { Background = IB, Foreground = FG, BorderThickness = new Thickness(0), FontSize = 13, CaretBrush = FG, MinHeight = 20 };
    }

    internal static Button MakeBtn(string text, SolidColorBrush bg, RoutedEventHandler click)
    {
        var b = new Button
        {
            Content = text, Background = bg, Foreground = Brushes.White,
            BorderThickness = new Thickness(0), FontSize = 12,
            Cursor = System.Windows.Input.Cursors.Hand,
            Padding = new Thickness(10, 3, 10, 3)
        };
        b.Click += click;
        return b;
    }

    internal static Border MakeSep()
    {
        return new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(63, 65, 71)), Margin = new Thickness(0, 4, 0, 4) };
    }
}

// ═══════════════════════════════════════════════════════════
//  MESSAGE
// ═══════════════════════════════════════════════════════════

public class Msg
{
    public int Number { get; set; }
    readonly MainWindow _w;
    TextBox _content;
    TextBox _title, _desc, _url, _color;
    TextBox _authorN, _authorI, _authorU;
    TextBox _thumb, _img, _footerT, _footerI;
    CheckBox _ts;
    StackPanel _fPanel, _bPanel;
    readonly List<Fld> _fields = new();
    readonly List<Btn> _buttons = new();
    readonly List<Expander> _expanders = new();
    Border _card;
    public event Action OnChange;

    public Msg(int n, MainWindow w) { Number = n; _w = w; }

    public void BuildUI(StackPanel parent)
    {
        _card = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(43, 45, 49)),
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 0, 0, 8),
            Padding = new Thickness(12, 10, 12, 10)
        };

        var root = new StackPanel();

        // ─── Message Header ───
        var hdr = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        hdr.Children.Add(new TextBlock { Text = $"Message {Number}", Foreground = new SolidColorBrush(Color.FromRgb(219, 222, 225)), FontSize = 14, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        hdr.Children.Add(new TextBlock { Text = "   ", FontSize = 8 });
        var upBtn = UI.MakeBtn("\u25B2", UI.BG4, (_, _) => MoveMsg(-1));
        var dnBtn = UI.MakeBtn("\u25BC", UI.BG4, (_, _) => MoveMsg(1));
        upBtn.FontSize = 10; upBtn.Padding = new Thickness(6, 1, 6, 1);
        dnBtn.FontSize = 10; dnBtn.Padding = new Thickness(6, 1, 6, 1);
        hdr.Children.Add(upBtn);
        hdr.Children.Add(dnBtn);
        hdr.Children.Add(new TextBlock { Text = "  ", FontSize = 8 });
        var copyBtn = UI.MakeBtn("\u2398", UI.BG4, (_, _) => CopyMsg());
        copyBtn.FontSize = 12; copyBtn.Padding = new Thickness(6, 1, 6, 1);
        hdr.Children.Add(copyBtn);
        var delBtn = UI.MakeBtn("\u2715", UI.Red, (_, _) => DeleteMsg(parent));
        delBtn.FontSize = 11; delBtn.Padding = new Thickness(6, 1, 6, 1);
        hdr.Children.Add(delBtn);
        root.Children.Add(hdr);

        // ─── Content ───
        _content = UI.MakeTb();
        _content.AcceptsReturn = true;
        _content.TextWrapping = TextWrapping.Wrap;
        _content.MinHeight = 120;
        _content.MaxHeight = 200;
        _content.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _content.TextChanged += (_, _) => OnChange?.Invoke();
        var cb = UI.InputBox();
        cb.Child = _content;
        root.Children.Add(cb);
        root.Children.Add(UI.MakeSep());

        // ─── Embeds ───
        root.Children.Add(MakeExpander("Embeds", BuildEmbedsUI()));
        root.Children.Add(UI.MakeSep());

        // ─── Fields ───
        root.Children.Add(MakeExpander("Fields", BuildFieldsUI()));
        root.Children.Add(UI.MakeSep());

        // ─── Action Buttons ───
        root.Children.Add(MakeExpander("Action Buttons", BuildButtonsUI()));

        _card.Child = root;
        parent.Children.Add(_card);
    }

    public void ExpandFirst()
    {
        if (_expanders.Count > 0)
            _expanders[0].IsExpanded = true;
    }

    void MoveMsg(int dir)
    {
        var parent = _card.Parent as StackPanel;
        if (parent == null) return;
        int i = parent.Children.IndexOf(_card);
        int t = i + dir;
        if (t < 0 || t >= parent.Children.Count) return;
        parent.Children.RemoveAt(i);
        parent.Children.Insert(t, _card);
        _w.RefreshPreviews();
    }

    void CopyMsg()
    {
        var m = new Msg(_w._msgs.Count + 1, _w);
        m._content.Text = _content.Text;
        m.OnChange += _w.RefreshPreviews;
        _w._msgs.Add(m);
        m.BuildUI((StackPanel)_card.Parent);
        _w.RefreshPreviews();
    }

    void DeleteMsg(StackPanel parent)
    {
        parent.Children.Remove(_card);
        _w._msgs.Remove(this);
        _w.RefreshPreviews();
        for (int i = 0; i < _w._msgs.Count; i++)
            _w._msgs[i].Renumber(i + 1);
    }

    void Renumber(int n)
    {
        Number = n;
        // Find the header text and update it
        if (_card?.Child is StackPanel root && root.Children[0] is StackPanel hdr && hdr.Children[0] is TextBlock tb)
            tb.Text = $"Message {n}";
    }

    // ─── Helpers ─────────────────────────────────

    Expander MakeExpander(string title, UIElement content)
    {
        var exp = new Expander
        {
            Header = title,
            Background = Brushes.Transparent,
            Foreground = UI.FG,
            BorderThickness = new Thickness(0),
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0),
            Padding = new Thickness(0),
            Content = content
        };
        _expanders.Add(exp);
        return exp;
    }

    UIElement BuildEmbedsUI()
    {
        var sp = new StackPanel();
        _title = UI.MakeTb(); _desc = UI.MakeTb(); _url = UI.MakeTb(); _color = UI.MakeTb();
        _authorN = UI.MakeTb(); _authorI = UI.MakeTb(); _authorU = UI.MakeTb();
        _thumb = UI.MakeTb(); _img = UI.MakeTb();
        _footerT = UI.MakeTb(); _footerI = UI.MakeTb();
        foreach (var tb in new[] { _title, _desc, _url, _color, _authorN, _authorI, _authorU, _thumb, _img, _footerT, _footerI })
            tb.TextChanged += (_, _) => OnChange?.Invoke();

        AddRow(sp, "Title", _title, 300, "URL", _url, 300, "Color", _color, 100);
        AddRow(sp, "Author", _authorN, 200, "Icon URL", _authorI, 250, "Author URL", _authorU, 250);
        AddRow(sp, "Description", _desc, 600);
        sp.Children.Add(UI.MakeSep());
        AddRow(sp, "Thumbnail", _thumb, 350, "Image", _img, 350);
        sp.Children.Add(UI.MakeSep());
        AddRow(sp, "Footer", _footerT, 200, "Icon URL", _footerI, 300);

        _ts = new CheckBox { Content = "Add timestamp", Foreground = UI.FG, FontSize = 12, Margin = new Thickness(0, 4, 0, 0) };
        _ts.Checked += (_, _) => OnChange?.Invoke();
        _ts.Unchecked += (_, _) => OnChange?.Invoke();
        sp.Children.Add(_ts);
        return sp;
    }

    UIElement BuildFieldsUI()
    {
        var sp = new StackPanel();
        _fPanel = new StackPanel();
        sp.Children.Add(_fPanel);
        sp.Children.Add(UI.MakeBtn("+ Add Field", UI.Blurple, (_, _) => AddField()));
        return sp;
    }

    UIElement BuildButtonsUI()
    {
        var sp = new StackPanel();
        _bPanel = new StackPanel();
        sp.Children.Add(_bPanel);
        sp.Children.Add(UI.MakeBtn("+ Add Button", UI.Blurple, (_, _) => AddButton()));
        return sp;
    }

    void AddRow(StackPanel sp, string label1, TextBox tb1, int w1,
                string label2 = null, TextBox tb2 = null, int w2 = 0,
                string label3 = null, TextBox tb3 = null, int w3 = 0)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };

        void AddField(string label, TextBox tb, int width)
        {
            if (!string.IsNullOrEmpty(label))
                row.Children.Add(new TextBlock { Text = label, Foreground = UI.FG2, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
            var b = UI.InputBox();
            tb.Width = width;
            tb.VerticalAlignment = VerticalAlignment.Center;
            b.Child = tb;
            row.Children.Add(b);
        }

        AddField(label1, tb1, w1);
        if (label2 != null) AddField(label2, tb2, w2);
        if (label3 != null) AddField(label3, tb3, w3);

        sp.Children.Add(row);
    }

    // ═══ FIELDS ══════════════════════════════════════

    void AddField(string n = "", string v = "", bool il = false)
    {
        var f = new Fld(n, v, il);
        f.OnChange += () => OnChange?.Invoke();
        var rmv = UI.MakeBtn("\u2715", UI.Red, (_, _) => { _fields.Remove(f); _fPanel.Children.Remove(f.Cont); OnChange?.Invoke(); });
        f.Build(UI.InputBox, rmv);
        _fields.Add(f);
        _fPanel.Children.Add(f.Cont);
        OnChange?.Invoke();
    }

    // ═══ BUTTONS ═════════════════════════════════════

    void AddButton(string l = "", string u = "")
    {
        var b = new Btn(l, u);
        b.OnChange += () => OnChange?.Invoke();
        var rmv = UI.MakeBtn("\u2715", UI.Red, (_, _) => { _buttons.Remove(b); _bPanel.Children.Remove(b.Cont); OnChange?.Invoke(); });
        b.Build(UI.InputBox, rmv);
        _buttons.Add(b);
        _bPanel.Children.Add(b.Cont);
        OnChange?.Invoke();
    }

    // ═══ DATA ════════════════════════════════════════

    public string GetContent() => _content?.Text.Trim() ?? "";

    public List<EmbedData> GetEmbeds()
    {
        var e = new EmbedData
        {
            Title = _title?.Text.Trim() ?? "",
            Description = _desc?.Text.Trim() ?? "",
            Url = _url?.Text.Trim() ?? "",
            AuthorName = _authorN?.Text.Trim() ?? "",
            AuthorIcon = _authorI?.Text.Trim() ?? "",
            AuthorUrl = _authorU?.Text.Trim() ?? "",
            Thumbnail = _thumb?.Text.Trim() ?? "",
            Image = _img?.Text.Trim() ?? "",
            FooterText = _footerT?.Text.Trim() ?? "",
            FooterIcon = _footerI?.Text.Trim() ?? "",
            HasTimestamp = _ts?.IsChecked == true,
            Fields = _fields.Select(f => new EmbedField { Name = f.Name, Value = f.Value, Inline = f.Inline }).ToList()
        };
        var cs = _color?.Text.Trim().TrimStart('#');
        if (!string.IsNullOrEmpty(cs) && int.TryParse(cs, System.Globalization.NumberStyles.HexNumber, null, out var cv))
            e.Color = cv;
        if (string.IsNullOrEmpty(e.Title) && string.IsNullOrEmpty(e.Description) && string.IsNullOrEmpty(e.FooterText) && e.Fields.Count == 0)
            return new();
        return new() { e };
    }

    public List<EmbedButton> GetButtons()
        => _buttons.Where(b => !string.IsNullOrEmpty(b.Label) && !string.IsNullOrEmpty(b.Url))
            .Select(b => new EmbedButton { Label = b.Label, Url = b.Url }).ToList();

    public string BuildJson(string username, string avatar)
    {
        var p = new Dictionary<string, object>();
        var c = _content?.Text.Trim();
        if (!string.IsNullOrEmpty(c)) p["content"] = c;
        if (!string.IsNullOrEmpty(username)) p["username"] = username;
        if (!string.IsNullOrEmpty(avatar)) p["avatar_url"] = avatar;

        var ed = new Dictionary<string, object>();
        if (H(_title)) ed["title"] = _title.Text.Trim();
        if (H(_desc)) ed["description"] = _desc.Text.Trim();
        if (H(_url)) ed["url"] = _url.Text.Trim();
        var cs = _color?.Text.Trim().TrimStart('#');
        if (!string.IsNullOrEmpty(cs) && int.TryParse(cs, System.Globalization.NumberStyles.HexNumber, null, out var cv)) ed["color"] = cv;
        if (H(_authorN))
        {
            var a = new Dictionary<string, object> { ["name"] = _authorN.Text.Trim() };
            if (H(_authorI)) a["icon_url"] = _authorI.Text.Trim();
            if (H(_authorU)) a["url"] = _authorU.Text.Trim();
            ed["author"] = a;
        }
        if (H(_thumb)) ed["thumbnail"] = new Dictionary<string, object> { ["url"] = _thumb.Text.Trim() };
        if (H(_img)) ed["image"] = new Dictionary<string, object> { ["url"] = _img.Text.Trim() };
        if (H(_footerT))
        {
            var f = new Dictionary<string, object> { ["text"] = _footerT.Text.Trim() };
            if (H(_footerI)) f["icon_url"] = _footerI.Text.Trim();
            ed["footer"] = f;
        }
        if (_ts?.IsChecked == true) ed["timestamp"] = DateTime.UtcNow.ToString("o");

        var fl = _fields.Where(f => !string.IsNullOrEmpty(f.Name) && !string.IsNullOrEmpty(f.Value))
            .Select(f => { var d = new Dictionary<string, object> { ["name"] = f.Name, ["value"] = f.Value }; if (f.Inline) d["inline"] = true; return d; }).ToList();
        if (fl.Count > 0) ed["fields"] = fl;
        if (ed.Count > 0) p["embeds"] = new[] { ed };

        var bt = _buttons.Where(b => !string.IsNullOrEmpty(b.Label) && !string.IsNullOrEmpty(b.Url))
            .Select(b => (object)new Dictionary<string, object> { ["type"] = 2, ["style"] = 5, ["label"] = b.Label, ["url"] = b.Url }).ToList();
        if (bt.Count > 0) p["components"] = new[] { new Dictionary<string, object> { ["type"] = 1, ["components"] = bt } };

        return p.Count == 0 ? null : JsonSerializer.Serialize(p);
    }

    static bool H(TextBox t) => t != null && !string.IsNullOrEmpty(t.Text.Trim());
}

// ═══════════════════════════════════════════════════════════
//  DATA TYPES
// ═══════════════════════════════════════════════════════════

public class EmbedField { public string Name { get; set; } public string Value { get; set; } public bool Inline { get; set; } }

public class EmbedData
{
    public string Title { get; set; }
    public string Description { get; set; }
    public string Url { get; set; }
    public int? Color { get; set; }
    public string AuthorName { get; set; }
    public string AuthorIcon { get; set; }
    public string AuthorUrl { get; set; }
    public string Thumbnail { get; set; }
    public string Image { get; set; }
    public string FooterText { get; set; }
    public string FooterIcon { get; set; }
    public bool HasTimestamp { get; set; }
    public List<EmbedField> Fields { get; set; } = new();
}

public class EmbedButton { public string Label { get; set; } public string Url { get; set; } }

// ═══════════════════════════════════════════════════════════
//  FIELD ROW
// ═══════════════════════════════════════════════════════════

public class Fld
{
    public Border Cont { get; private set; }
    readonly TextBox _n, _v;
    readonly CheckBox _c;
    public string Name => _n.Text.Trim();
    public string Value => _v.Text.Trim();
    public bool Inline => _c.IsChecked == true;
    public event Action OnChange;

    static readonly SolidColorBrush IB = new(Color.FromRgb(51, 51, 56));
    static readonly SolidColorBrush FG = new(Color.FromRgb(219, 222, 225));

    public Fld(string n, string v, bool il)
    {
        _n = new TextBox { Text = n, Background = IB, Foreground = FG, BorderThickness = new Thickness(0), FontSize = 13, CaretBrush = FG };
        _v = new TextBox { Text = v, Background = IB, Foreground = FG, BorderThickness = new Thickness(0), FontSize = 13, CaretBrush = FG };
        _c = new CheckBox { Content = "Inline", IsChecked = il, Foreground = FG, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        _n.TextChanged += (_, _) => OnChange?.Invoke();
        _v.TextChanged += (_, _) => OnChange?.Invoke();
        _c.Checked += (_, _) => OnChange?.Invoke();
        _c.Unchecked += (_, _) => OnChange?.Invoke();
    }

    public void Build(Func<Border> inputBox, Button rmv)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
        var nb = inputBox(); nb.Child = _n; nb.Width = 180;
        var vb = inputBox(); vb.Child = _v; vb.Width = 340;
        sp.Children.Add(nb);
        sp.Children.Add(new TextBlock { Text = "  ", FontSize = 8 });
        sp.Children.Add(vb);
        sp.Children.Add(new TextBlock { Text = "  ", FontSize = 8 });
        sp.Children.Add(_c);
        sp.Children.Add(rmv);
        Cont = new Border { Background = new SolidColorBrush(Color.FromRgb(43, 45, 49)), CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 4, 6, 4), Margin = new Thickness(0, 2, 0, 2), Child = sp };
    }
}

// ═══════════════════════════════════════════════════════════
//  BUTTON ROW
// ═══════════════════════════════════════════════════════════

public class Btn
{
    public Border Cont { get; private set; }
    readonly TextBox _l, _u;
    public string Label => _l.Text.Trim();
    public string Url => _u.Text.Trim();
    public event Action OnChange;

    static readonly SolidColorBrush IB = new(Color.FromRgb(51, 51, 56));
    static readonly SolidColorBrush FG = new(Color.FromRgb(219, 222, 225));
    static readonly SolidColorBrush FM = new(Color.FromRgb(148, 155, 164));

    public Btn(string l, string u)
    {
        _l = new TextBox { Text = l, Background = IB, Foreground = FG, BorderThickness = new Thickness(0), FontSize = 13, CaretBrush = FG };
        _u = new TextBox { Text = u, Background = IB, Foreground = FG, BorderThickness = new Thickness(0), FontSize = 13, CaretBrush = FG };
        _l.TextChanged += (_, _) => OnChange?.Invoke();
        _u.TextChanged += (_, _) => OnChange?.Invoke();
    }

    public void Build(Func<Border> inputBox, Button rmv)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
        sp.Children.Add(new TextBlock { Text = "Label:", Foreground = FM, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        sp.Children.Add(new TextBlock { Text = " ", FontSize = 8 });
        var lb = inputBox(); lb.Child = _l; lb.Width = 140;
        sp.Children.Add(lb);
        sp.Children.Add(new TextBlock { Text = "   URL:", Foreground = FM, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        sp.Children.Add(new TextBlock { Text = " ", FontSize = 8 });
        var ub = inputBox(); ub.Child = _u; ub.Width = 340;
        sp.Children.Add(ub);
        sp.Children.Add(rmv);
        Cont = new Border { Background = new SolidColorBrush(Color.FromRgb(43, 45, 49)), CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 4, 6, 4), Margin = new Thickness(0, 2, 0, 2), Child = sp };
    }
}
