using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace ClipJournal;

public sealed class ClipCardItem
{
    public int Index { get; }
    public string Timestamp { get; }
    public string Content { get; }

    public ClipCardItem(int index, string timestamp, string content)
    {
        Index = index;
        Timestamp = timestamp;
        Content = content;
    }
}

public sealed class ItemActionEventArgs : EventArgs
{
    public ClipCardItem Item { get; }

    public ItemActionEventArgs(ClipCardItem item) => Item = item;
}

public sealed class ModernCardList : Control
{
    private const int MaximumItems = 500;

    private readonly List<ClipCardItem> _allItems = new();
    private readonly List<ClipCardItem> _filteredItems = new();
    private readonly VScrollBar _scrollBar = new();
    private readonly System.Windows.Forms.Timer _animationTimer = new();

    private string _filterQuery = string.Empty;
    private int _hoveredIndex = -1;
    private bool _copyHovered;
    private float _hoverProgress;
    private ClipCardItem? _selectedItem;
    private ClipCardItem? _newItem;
    private float _newItemProgress;
    // Fractional scroll accumulator: precision touchpads emit sub-120 wheel deltas
    // and integer division (-Delta / ScrollDelta) silently floors them to zero.
    private float _scrollCarry;

    public event EventHandler<ItemActionEventArgs>? ItemCopyClicked;
    public event EventHandler? SelectionChanged;
    public event EventHandler? FilterResultChanged;

    public IReadOnlyList<ClipCardItem> AllItems => _allItems;
    public int Count => _allItems.Count;
    public int FilteredCount => _filteredItems.Count;
    public ClipCardItem? SelectedItem => _selectedItem;

    public ModernCardList()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);

        DoubleBuffered = true;
        BackColor = Theme.Background;
        TabStop = true;
        AccessibleName = Localization.RecordsTitle;

        _scrollBar.Dock = DockStyle.Right;
        _scrollBar.Width = Theme.Scale(this, 11);
        _scrollBar.Scroll += (_, _) =>
        {
            ResetHover();
            Invalidate();
        };
        Controls.Add(_scrollBar);

        _animationTimer.Interval = 15;
        _animationTimer.Tick += Animate;

        MouseWheel += OnMouseWheelScroll;
        Resize += (_, _) => UpdateScrollBounds();
    }

    public void AddItem(int index, string timestamp, string content)
    {
        var shouldFollow = IsNearBottom();
        var item = new ClipCardItem(index, timestamp, content);
        _allItems.Add(item);
        while (_allItems.Count > MaximumItems)
        {
            var removed = _allItems[0];
            _allItems.RemoveAt(0);
            if (ReferenceEquals(_selectedItem, removed))
            {
                _selectedItem = null;
            }
        }

        _newItem = item;
        _newItemProgress = 1f;
        ApplyFilter(preserveSelection: true);
        if (shouldFollow)
        {
            ScrollToBottom();
        }

        _animationTimer.Start();
    }

    public void SetItems(IEnumerable<ClipCardItem> items)
    {
        _allItems.Clear();
        _allItems.AddRange(items.TakeLast(MaximumItems));
        _selectedItem = null;
        _newItem = null;
        _newItemProgress = 0f;
        ApplyFilter(preserveSelection: false);
        ScrollToBottom();
    }

    public void Clear()
    {
        _allItems.Clear();
        _filteredItems.Clear();
        _selectedItem = null;
        _newItem = null;
        _newItemProgress = 0f;
        ResetHover();
        UpdateScrollBounds();
        FilterResultChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    public void SetFilter(string query)
    {
        _filterQuery = query?.Trim() ?? string.Empty;
        ApplyFilter(preserveSelection: true);
        _scrollBar.Value = 0;
        Invalidate();
    }

    private void ApplyFilter(bool preserveSelection)
    {
        _filteredItems.Clear();
        if (string.IsNullOrEmpty(_filterQuery))
        {
            _filteredItems.AddRange(_allItems);
        }
        else
        {
            foreach (var item in _allItems)
            {
                if (item.Content.Contains(_filterQuery, StringComparison.OrdinalIgnoreCase) ||
                    item.Index.ToString().Contains(_filterQuery, StringComparison.Ordinal) ||
                    item.Timestamp.Contains(_filterQuery, StringComparison.OrdinalIgnoreCase))
                {
                    _filteredItems.Add(item);
                }
            }
        }

        if (!preserveSelection || (_selectedItem is not null && !_filteredItems.Contains(_selectedItem)))
        {
            _selectedItem = null;
        }

        ResetHover();
        UpdateScrollBounds();
        FilterResultChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    private int ItemHeight => Theme.Scale(this, 68);
    private int ItemGap => Theme.Scale(this, 8);
    private int OuterPadding => Theme.Scale(this, 18);

    private int ContentHeight =>
        _filteredItems.Count == 0
            ? 0
            : OuterPadding * 2 + _filteredItems.Count * ItemHeight + (_filteredItems.Count - 1) * ItemGap;

    private bool IsNearBottom()
    {
        if (!_scrollBar.Visible)
        {
            return true;
        }

        return _scrollBar.Value >= MaximumScrollValue - ItemHeight;
    }

    private int MaximumScrollValue
        => Math.Max(0, _scrollBar.Maximum - _scrollBar.LargeChange + 1);

    private void ScrollToBottom()
    {
        if (_scrollBar.Visible)
        {
            _scrollBar.Value = MaximumScrollValue;
        }

        Invalidate();
    }

    private void UpdateScrollBounds()
    {
        var visibleHeight = Math.Max(1, ClientSize.Height);
        var totalHeight = ContentHeight;
        if (totalHeight > visibleHeight)
        {
            _scrollBar.Maximum = Math.Max(0, totalHeight - 1);
            _scrollBar.LargeChange = visibleHeight;
            _scrollBar.SmallChange = Math.Max(1, ItemHeight / 2);
            _scrollBar.Visible = true;
            _scrollBar.Value = Math.Min(_scrollBar.Value, MaximumScrollValue);
        }
        else
        {
            _scrollBar.Value = 0;
            _scrollBar.Visible = false;
        }
    }

    private void OnMouseWheelScroll(object? sender, MouseEventArgs e)
    {
        if (!_scrollBar.Visible)
        {
            return;
        }

        var steps = (int)(_scrollCarry += (float)-e.Delta / SystemInformation.MouseWheelScrollDelta);
        if (steps == 0)
        {
            return;
        }

        _scrollCarry -= steps;
        var delta = steps * Theme.Scale(this, 48);
        _scrollBar.Value = Math.Clamp(_scrollBar.Value + delta, 0, MaximumScrollValue);
        ResetHover();
        Invalidate();
    }

    private Rectangle GetItemRectangle(int index)
    {
        var scrollOffset = _scrollBar.Visible ? _scrollBar.Value : 0;
        var availableWidth = ClientSize.Width - (_scrollBar.Visible ? _scrollBar.Width : 0) - OuterPadding * 2;
        var y = OuterPadding + index * (ItemHeight + ItemGap) - scrollOffset;
        return new Rectangle(OuterPadding, y, Math.Max(1, availableWidth), ItemHeight);
    }

    private Rectangle GetCopyRectangle(Rectangle itemRect)
    {
        var size = Theme.Scale(this, 34);
        return new Rectangle(
            itemRect.Right - size - Theme.Scale(this, 14),
            itemRect.Top + (itemRect.Height - size) / 2,
            size,
            size);
    }

    private int HitTestItem(Point location)
    {
        for (var index = 0; index < _filteredItems.Count; index++)
        {
            var rect = GetItemRectangle(index);
            if (rect.Bottom < 0)
            {
                continue;
            }

            if (rect.Top > Height)
            {
                break;
            }

            if (rect.Contains(location))
            {
                return index;
            }
        }

        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var nextIndex = HitTestItem(e.Location);
        var nextCopyHovered = nextIndex >= 0 && GetCopyRectangle(GetItemRectangle(nextIndex)).Contains(e.Location);
        if (_hoveredIndex != nextIndex || _copyHovered != nextCopyHovered)
        {
            _hoveredIndex = nextIndex;
            _copyHovered = nextCopyHovered;
            _hoverProgress = nextIndex >= 0 ? Math.Min(_hoverProgress, .18f) : _hoverProgress;
            Cursor = nextCopyHovered ? Cursors.Hand : Cursors.Default;
            _animationTimer.Start();
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hoveredIndex = -1;
        _copyHovered = false;
        Cursor = Cursors.Default;
        _animationTimer.Start();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (e.Button != MouseButtons.Left || _hoveredIndex < 0 || _hoveredIndex >= _filteredItems.Count)
        {
            return;
        }

        var item = _filteredItems[_hoveredIndex];
        if (_copyHovered)
        {
            ItemCopyClicked?.Invoke(this, new ItemActionEventArgs(item));
            return;
        }

        _selectedItem = item;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        var index = HitTestItem(e.Location);
        if (e.Button == MouseButtons.Left && index >= 0 && index < _filteredItems.Count)
        {
            ItemCopyClicked?.Invoke(this, new ItemActionEventArgs(_filteredItems[index]));
        }
    }

    protected override bool IsInputKey(Keys keyData)
        => (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.Enter ||
           base.IsInputKey(keyData);

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.C))
        {
            if (_selectedItem is not null)
            {
                ItemCopyClicked?.Invoke(this, new ItemActionEventArgs(_selectedItem));
            }

            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_filteredItems.Count == 0)
        {
            return;
        }

        var selectedIndex = _selectedItem is null ? -1 : _filteredItems.IndexOf(_selectedItem);
        if (e.KeyCode == Keys.Down)
        {
            SelectIndex(Math.Min(_filteredItems.Count - 1, selectedIndex + 1));
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Up)
        {
            SelectIndex(selectedIndex <= 0 ? 0 : selectedIndex - 1);
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Home)
        {
            SelectIndex(0);
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.End)
        {
            SelectIndex(_filteredItems.Count - 1);
            e.Handled = true;
        }
        else if ((e.Control && e.KeyCode == Keys.C) || e.KeyCode == Keys.Enter)
        {
            if (_selectedItem is not null)
            {
                ItemCopyClicked?.Invoke(this, new ItemActionEventArgs(_selectedItem));
            }

            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            _selectedItem = null;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
            e.Handled = true;
        }
    }

    private void SelectIndex(int index)
    {
        if (index < 0 || index >= _filteredItems.Count)
        {
            return;
        }

        _selectedItem = _filteredItems[index];
        EnsureVisible(index);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    private void EnsureVisible(int index)
    {
        if (!_scrollBar.Visible)
        {
            return;
        }

        var rect = GetItemRectangle(index);
        if (rect.Top < OuterPadding)
        {
            _scrollBar.Value = Math.Clamp(_scrollBar.Value + rect.Top - OuterPadding, 0, MaximumScrollValue);
        }
        else if (rect.Bottom > Height - OuterPadding)
        {
            _scrollBar.Value = Math.Clamp(_scrollBar.Value + rect.Bottom - Height + OuterPadding, 0, MaximumScrollValue);
        }
    }

    private void ResetHover()
    {
        _hoveredIndex = -1;
        _copyHovered = false;
        _hoverProgress = 0f;
        Cursor = Cursors.Default;
    }

    private void Animate(object? sender, EventArgs e)
    {
        var hoverTarget = _hoveredIndex >= 0 ? 1f : 0f;
        _hoverProgress += (hoverTarget - _hoverProgress) * .22f;
        _newItemProgress = Math.Max(0f, _newItemProgress - .018f);

        if (Math.Abs(hoverTarget - _hoverProgress) < .02f)
        {
            _hoverProgress = hoverTarget;
        }

        if (_newItemProgress <= 0f)
        {
            _newItem = null;
        }

        if (Math.Abs(hoverTarget - _hoverProgress) < .02f && _newItem is null)
        {
            _animationTimer.Stop();
        }

        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        try
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            using (var background = new SolidBrush(Theme.Background))
            {
                graphics.FillRectangle(background, ClientRectangle);
            }

            if (_filteredItems.Count == 0)
            {
                RenderEmptyState(graphics);
                return;
            }

            for (var index = 0; index < _filteredItems.Count; index++)
            {
                var itemRect = GetItemRectangle(index);
                if (itemRect.Bottom < 0)
                {
                    continue;
                }

                if (itemRect.Top > Height)
                {
                    break;
                }

                RenderItem(graphics, itemRect, _filteredItems[index], index);
            }
        }
        catch
        {
            // A transient GDI+ failure (corrupt font cache, OOM mid-paint) could leave
            // the card area half-drawn. Fall back to a solid background so the control
            // stays legible until the next invalidation rather than showing stale pixels.
            graphics.Clear(Theme.Background);
        }
    }

    private void RenderItem(Graphics graphics, Rectangle rect, ClipCardItem item, int index)
    {
        var hovered = index == _hoveredIndex;
        var selected = ReferenceEquals(item, _selectedItem);
        var background = selected
            ? Theme.SurfaceSelected
            : hovered
                ? Theme.Interpolate(Theme.Surface, Theme.SurfaceHover, _hoverProgress)
                : Theme.Surface;
        if (ReferenceEquals(item, _newItem))
        {
            background = Theme.Interpolate(background, Theme.PrimarySoft, _newItemProgress * .7f);
        }

        var radius = Theme.Scale(this, Theme.CornerRadius + 2);
        using (var fill = new SolidBrush(background))
        {
            graphics.FillRoundedRectangle(fill, rect, radius);
        }

        var borderColor = selected
            ? Color.FromArgb(118, Theme.Primary)
            : hovered
                ? Theme.Interpolate(Theme.Border, Theme.BorderStrong, _hoverProgress)
                : Theme.Border;
        using (var border = new Pen(borderColor, Math.Max(1f, (selected ? 1.25f : 1f) * DeviceDpi / 96f)))
        {
            graphics.DrawRoundedRectangle(border, rect, radius);
        }

        var numberWidth = Theme.Scale(this, 46);
        var numberRect = new Rectangle(
            rect.Left + Theme.Scale(this, 14),
            rect.Top + (rect.Height - Theme.Scale(this, 30)) / 2,
            numberWidth,
            Theme.Scale(this, 30));
        using (var numberBackground = new SolidBrush(Theme.PrimarySoft))
        {
            graphics.FillRoundedRectangle(numberBackground, numberRect, Theme.Scale(this, 8));
        }

        TextRenderer.DrawText(
            graphics,
            $"#{item.Index}",
            Theme.FontCount,
            numberRect,
            Theme.PrimaryText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

        var copyRect = GetCopyRectangle(rect);
        var metaText = string.IsNullOrWhiteSpace(item.Timestamp) ? Localization.HistoryItem : item.Timestamp;
        var metaSize = TextRenderer.MeasureText(metaText, Theme.FontSmall, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        var metaRight = hovered ? copyRect.Left - Theme.Scale(this, 10) : rect.Right - Theme.Scale(this, 16);
        var metaRect = new Rectangle(
            Math.Max(numberRect.Right + Theme.Scale(this, 16), metaRight - metaSize.Width),
            rect.Top,
            Math.Min(metaSize.Width, Math.Max(1, metaRight - numberRect.Right)),
            rect.Height);

        TextRenderer.DrawText(
            graphics,
            metaText,
            Theme.FontSmall,
            metaRect,
            Theme.TextMuted,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

        var contentLeft = numberRect.Right + Theme.Scale(this, 16);
        var contentRight = metaRect.Left - Theme.Scale(this, 14);
        var contentRect = new Rectangle(contentLeft, rect.Top, Math.Max(1, contentRight - contentLeft), rect.Height);
        var preview = item.Content.Length <= 220 ? item.Content : item.Content[..220] + "…";
        TextRenderer.DrawText(
            graphics,
            preview,
            Theme.FontCode,
            contentRect,
            Theme.TextPrimary,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

        if (hovered)
        {
            var copyBackground = _copyHovered ? Theme.PrimarySoft : Theme.SurfaceSoft;
            var copyForeground = _copyHovered ? Theme.PrimaryText : Theme.TextSecondary;
            using (var fill = new SolidBrush(copyBackground))
            {
                graphics.FillRoundedRectangle(fill, copyRect, Theme.Scale(this, 8));
            }

            var iconSize = Theme.Scale(this, 16);
            var iconRect = new Rectangle(copyRect.Left + (copyRect.Width - iconSize) / 2, copyRect.Top + (copyRect.Height - iconSize) / 2, iconSize, iconSize);
            UiIconPainter.Draw(graphics, UiIcon.Copy, iconRect, copyForeground, Math.Max(1.35f, 1.5f * DeviceDpi / 96f));
        }

        if (Focused && selected && ShowFocusCues)
        {
            var focusRect = Rectangle.Inflate(rect, -Theme.Scale(this, 4), -Theme.Scale(this, 4));
            using var focusPen = new Pen(Color.FromArgb(160, Theme.Primary)) { DashStyle = DashStyle.Dot };
            graphics.DrawRoundedRectangle(focusPen, focusRect, Math.Max(2, radius - Theme.Scale(this, 4)));
        }
    }

    private void RenderEmptyState(Graphics graphics)
    {
        var centerX = Math.Max(0, (Width - (_scrollBar.Visible ? _scrollBar.Width : 0)) / 2);
        var visualSize = Theme.Scale(this, 64);
        var visualRect = new Rectangle(centerX - visualSize / 2, Height / 2 - Theme.Scale(this, 78), visualSize, visualSize);
        using (var visualBackground = new SolidBrush(Theme.PrimarySoft))
        {
            graphics.FillEllipse(visualBackground, visualRect);
        }

        var iconRect = Rectangle.Inflate(visualRect, -Theme.Scale(this, 18), -Theme.Scale(this, 18));
        UiIconPainter.Draw(graphics, _filterQuery.Length == 0 ? UiIcon.Clipboard : UiIcon.Search, iconRect, Theme.Primary, Math.Max(1.6f, 1.8f * DeviceDpi / 96f));

        var title = _filterQuery.Length == 0 ? Localization.EmptyTitle : Localization.NoResultsTitle;
        var hint = _filterQuery.Length == 0 ? Localization.EmptyHint : Localization.NoResultsHint;
        var titleRect = new Rectangle(OuterPadding, visualRect.Bottom + Theme.Scale(this, 15), Width - OuterPadding * 2, Theme.Scale(this, 26));
        TextRenderer.DrawText(
            graphics,
            title,
            Theme.FontTitle,
            titleRect,
            Theme.TextPrimary,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

        var hintRect = new Rectangle(OuterPadding, titleRect.Bottom + Theme.Scale(this, 4), Width - OuterPadding * 2, Theme.Scale(this, 40));
        TextRenderer.DrawText(
            graphics,
            hint,
            Theme.FontMain,
            hintRect,
            Theme.TextSecondary,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animationTimer.Dispose();
        }

        base.Dispose(disposing);
    }
}
