using SolidWorksMcpApp.Ipc;
using SolidWorksMcpApp.Logging;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace SolidWorksMcpApp;

internal sealed class MonitorForm : Form
{
    private const int DesiredPanel1MinSize = 160;
    private const int DesiredPanel2MinSize = 180;

    private readonly SplitContainer _splitContainer;
    private readonly ListView _clientsView;
    private readonly TextBox _logTextBox;
    private bool _splitInitialized;

    public MonitorForm()
    {
        Text = Strings.MonitorTitle;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(720, 480);
        Size = new Size(920, 640);

        _splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
        };

        _clientsView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            MultiSelect = false,
            HideSelection = false,
        };
        _clientsView.Columns.Add(Strings.MonitorColClient, 220);
        _clientsView.Columns.Add(Strings.MonitorColPid, 90);
        _clientsView.Columns.Add(Strings.MonitorColConnected, 180);
        _clientsView.Columns.Add(Strings.MonitorColSession, 120);

        _logTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point),
        };

        _splitContainer.Panel1.Controls.Add(WrapSection(Strings.MonitorClientsHeading, _clientsView));
        _splitContainer.Panel2.Controls.Add(WrapSection(Strings.MonitorLogsHeading, _logTextBox));
        Controls.Add(_splitContainer);

        ClientRegistry.Changed += OnClientsChanged;
        ServerLogBuffer.Changed += OnLogsChanged;
        Shown += (_, _) => BeginInvoke((Action)EnsureSplitInitialized);
        Resize += (_, _) =>
        {
            if (_splitInitialized)
                ClampSplitterDistance();
        };

        RefreshClients();
        RefreshLogs();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ClientRegistry.Changed -= OnClientsChanged;
            ServerLogBuffer.Changed -= OnLogsChanged;
        }

        base.Dispose(disposing);
    }

    private void OnClientsChanged()
    {
        if (IsDisposed) return;
        if (InvokeRequired)
            BeginInvoke(RefreshClients);
        else
            RefreshClients();
    }

    private void OnLogsChanged()
    {
        if (IsDisposed) return;
        if (InvokeRequired)
            BeginInvoke(RefreshLogs);
        else
            RefreshLogs();
    }

    private void RefreshClients()
    {
        var clients = ClientRegistry.GetSnapshot()
            .OrderBy(c => c.ConnectedAt)
            .ToArray();

        _clientsView.BeginUpdate();
        try
        {
            _clientsView.Items.Clear();
            foreach (var client in clients)
            {
                var item = new ListViewItem(client.Name);
                item.SubItems.Add(client.Pid.ToString());
                item.SubItems.Add(client.ConnectedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                item.SubItems.Add(client.SessionId);
                _clientsView.Items.Add(item);
            }
        }
        finally
        {
            _clientsView.EndUpdate();
        }
    }

    private void RefreshLogs()
    {
        var entries = ServerLogBuffer.GetSnapshot();
        var sb = new StringBuilder();
        foreach (var entry in entries)
            sb.AppendLine(entry.ToString());

        var wasAtBottom = _logTextBox.SelectionStart >= Math.Max(0, _logTextBox.TextLength - 2);
        _logTextBox.Text = sb.ToString();
        if (wasAtBottom || _logTextBox.TextLength == 0)
        {
            _logTextBox.SelectionStart = _logTextBox.TextLength;
            _logTextBox.ScrollToCaret();
        }
    }

    private void EnsureSplitInitialized()
    {
        if (_splitInitialized) return;
        if (_splitContainer.IsDisposed) return;

        var available = _splitContainer.ClientSize.Height - _splitContainer.SplitterWidth;
        if (available <= 0) return;

        var panel1Min = Math.Min(DesiredPanel1MinSize, Math.Max(0, available));
        var panel2Min = Math.Min(DesiredPanel2MinSize, Math.Max(0, available - panel1Min));
        if (panel1Min + panel2Min > available)
            panel2Min = Math.Max(0, available - panel1Min);

        _splitContainer.Panel1MinSize = panel1Min;
        _splitContainer.Panel2MinSize = panel2Min;

        _splitInitialized = true;
        ClampSplitterDistance(preferredDistance: 220);
    }

    private void ClampSplitterDistance(int? preferredDistance = null)
    {
        if (_splitContainer.IsDisposed) return;

        var available = _splitContainer.ClientSize.Height;
        if (available <= 0) return;

        var min = _splitContainer.Panel1MinSize;
        var max = available - _splitContainer.Panel2MinSize - _splitContainer.SplitterWidth;
        if (max < min) return;

        var target = preferredDistance ?? _splitContainer.SplitterDistance;
        target = Math.Max(min, Math.Min(max, target));

        if (_splitContainer.SplitterDistance != target)
            _splitContainer.SplitterDistance = target;
    }

    private static Control WrapSection(string title, Control content)
    {
        var sectionFont = SystemFonts.MessageBoxFont ?? Control.DefaultFont;

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(10),
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        panel.Controls.Add(new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 6),
            Font = new Font(sectionFont, FontStyle.Bold),
        }, 0, 0);
        panel.Controls.Add(content, 0, 1);
        return panel;
    }
}