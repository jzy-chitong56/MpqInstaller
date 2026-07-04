using System;
using System.Windows.Forms;
using MpqInstaller.i18n;

namespace MpqInstaller.UI;

public sealed partial class LogForm : Form
{
    private readonly TextBox _txtLog;
    private readonly Button _btnClose;

    public LogForm()
    {
        Text = LanguageManager.Get("log_window_title");
        Width = 640;
        Height = 480;
        StartPosition = FormStartPosition.CenterParent;
        Font = new System.Drawing.Font("微软雅黑", 9F);
        MinimumSize = new System.Drawing.Size(480, 360);

        _txtLog = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new System.Drawing.Font("Consolas", 9F),
            Dock = DockStyle.Fill,
            BackColor = System.Drawing.SystemColors.Window,
        };

        _btnClose = new Button
        {
            Text = LanguageManager.Get("log_close"),
            Size = new System.Drawing.Size(100, 30),
            Dock = DockStyle.Bottom,
        };
        _btnClose.Click += (_, _) => Close();

        Controls.Add(_txtLog);
        Controls.Add(_btnClose);
    }

    public void AppendLog(string line)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            Invoke(new Action<string>(AppendLog), line);
            return;
        }
        if (string.IsNullOrEmpty(line)) return;
        _txtLog.AppendText(line + "\r\n");
        // 限制最大行数，避免内存膨胀
        if (_txtLog.Lines.Length > 2000)
        {
            var start = _txtLog.GetFirstCharIndexFromLine(500);
            _txtLog.Select(0, start);
            _txtLog.SelectedText = string.Empty;
        }
    }
}
