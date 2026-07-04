#nullable enable
namespace MpqInstaller.UI;

partial class MainForm
{
    private System.ComponentModel.IContainer? components = null;

    // 顶部蓝色标题栏
    private System.Windows.Forms.Panel pnlTitle = null!;
    private System.Windows.Forms.Label lblTitle = null!;
    private System.Windows.Forms.Label lblTitleVersion = null!;
    private System.Windows.Forms.Label lblLanguage = null!;
    private System.Windows.Forms.ComboBox cboLanguage = null!;

    // 配置状态小字
    private System.Windows.Forms.Label lblConfigStatus = null!;
    private System.Windows.Forms.Button btnReloadConfig = null!;

    // 操作行
    private System.Windows.Forms.Label lblOperation = null!;
    private System.Windows.Forms.RadioButton rbInstall = null!;
    private System.Windows.Forms.RadioButton rbUninstall = null!;

    // 分隔线
    private System.Windows.Forms.Panel pnlSep1 = null!;

    // Profile 行（两列单选）
    private System.Windows.Forms.Label lblProfile = null!;
    private System.Windows.Forms.RadioButton rbProfileA = null!;
    private System.Windows.Forms.RadioButton rbProfileB = null!;

    // 分隔线
    private System.Windows.Forms.Panel pnlSep2 = null!;

    // Mode 行（复选项列表）
    private System.Windows.Forms.Label lblMode = null!;
    private System.Windows.Forms.CheckedListBox clbModes = null!;

    // 分隔线
    private System.Windows.Forms.Panel pnlSep3 = null!;

    // 底部两个大按钮
    private System.Windows.Forms.Button btnSingleMap = null!;
    private System.Windows.Forms.Button btnFolder = null!;

    // 取消按钮
    private System.Windows.Forms.Button btnCancel = null!;
    private System.Windows.Forms.Button btnShowLog = null!;

    // 底部进度条 + 数量
    private System.Windows.Forms.ProgressBar bottomProgress = null!;
    private System.Windows.Forms.Label lblBottomProgress = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
            components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        pnlTitle = new System.Windows.Forms.Panel();
        lblTitle = new System.Windows.Forms.Label();
        lblTitleVersion = new System.Windows.Forms.Label();
        lblLanguage = new System.Windows.Forms.Label();
        cboLanguage = new System.Windows.Forms.ComboBox();
        lblConfigStatus = new System.Windows.Forms.Label();
        btnReloadConfig = new System.Windows.Forms.Button();
        lblOperation = new System.Windows.Forms.Label();
        rbInstall = new System.Windows.Forms.RadioButton();
        rbUninstall = new System.Windows.Forms.RadioButton();
        pnlSep1 = new System.Windows.Forms.Panel();
        lblProfile = new System.Windows.Forms.Label();
        rbProfileA = new System.Windows.Forms.RadioButton();
        rbProfileB = new System.Windows.Forms.RadioButton();
        pnlSep2 = new System.Windows.Forms.Panel();
        lblMode = new System.Windows.Forms.Label();
        clbModes = new System.Windows.Forms.CheckedListBox();
        pnlSep3 = new System.Windows.Forms.Panel();
        btnSingleMap = new System.Windows.Forms.Button();
        btnFolder = new System.Windows.Forms.Button();
        btnCancel = new System.Windows.Forms.Button();
        btnShowLog = new System.Windows.Forms.Button();
        bottomProgress = new System.Windows.Forms.ProgressBar();
        lblBottomProgress = new System.Windows.Forms.Label();

        SuspendLayout();

        // ==================== 顶部蓝色标题栏 ====================
        pnlTitle.BackColor = System.Drawing.Color.FromArgb(0, 120, 215);
        pnlTitle.Dock = System.Windows.Forms.DockStyle.Top;
        pnlTitle.Size = new System.Drawing.Size(420, 60);
        pnlTitle.Name = "pnlTitle";

        lblTitle.Font = new System.Drawing.Font("微软雅黑", 14F, System.Drawing.FontStyle.Bold);
        lblTitle.ForeColor = System.Drawing.Color.White;
        lblTitle.Location = new System.Drawing.Point(16, 10);
        lblTitle.AutoSize = true;
        lblTitle.Name = "lblTitle";

        lblTitleVersion.Font = new System.Drawing.Font("微软雅黑", 9F);
        lblTitleVersion.ForeColor = System.Drawing.Color.FromArgb(220, 230, 245);
        lblTitleVersion.Location = new System.Drawing.Point(16, 36);
        lblTitleVersion.AutoSize = true;
        lblTitleVersion.Name = "lblTitleVersion";

        cboLanguage.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        cboLanguage.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        cboLanguage.Font = new System.Drawing.Font("微软雅黑", 9F);
        cboLanguage.Location = new System.Drawing.Point(316, 12);
        cboLanguage.Size = new System.Drawing.Size(90, 25);
        cboLanguage.Name = "cboLanguage";

        pnlTitle.Controls.Add(lblTitle);
        pnlTitle.Controls.Add(lblTitleVersion);
        pnlTitle.Controls.Add(cboLanguage);

        // ==================== 配置状态 ====================
        lblConfigStatus.AutoSize = true;
        lblConfigStatus.ForeColor = System.Drawing.Color.Gray;
        lblConfigStatus.Font = new System.Drawing.Font("微软雅黑", 8F);
        lblConfigStatus.Location = new System.Drawing.Point(12, 68);
        lblConfigStatus.Name = "lblConfigStatus";

        btnReloadConfig.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        btnReloadConfig.Font = new System.Drawing.Font("微软雅黑", 8F);
        btnReloadConfig.ForeColor = System.Drawing.Color.FromArgb(0, 120, 215);
        btnReloadConfig.Location = new System.Drawing.Point(316, 64);
        btnReloadConfig.Size = new System.Drawing.Size(92, 22);
        btnReloadConfig.Name = "btnReloadConfig";

        // ==================== 操作 ====================
        lblOperation.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
        lblOperation.ForeColor = System.Drawing.Color.FromArgb(50, 50, 50);
        lblOperation.Location = new System.Drawing.Point(12, 98);
        lblOperation.Size = new System.Drawing.Size(120, 20);
        lblOperation.Name = "lblOperation";

        rbInstall.Font = new System.Drawing.Font("微软雅黑", 9F);
        rbInstall.Location = new System.Drawing.Point(24, 125);
        rbInstall.AutoSize = true;
        rbInstall.Checked = true;
        rbInstall.Name = "rbInstall";

        rbUninstall.Font = new System.Drawing.Font("微软雅黑", 9F);
        rbUninstall.Location = new System.Drawing.Point(230, 125);
        rbUninstall.AutoSize = true;
        rbUninstall.Name = "rbUninstall";

        pnlSep1.BackColor = System.Drawing.Color.FromArgb(225, 225, 225);
        pnlSep1.Location = new System.Drawing.Point(12, 158);
        pnlSep1.Size = new System.Drawing.Size(396, 1);
        pnlSep1.Name = "pnlSep1";

        // ==================== Profile（版本，两列单选）====================
        lblProfile.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
        lblProfile.ForeColor = System.Drawing.Color.FromArgb(50, 50, 50);
        lblProfile.Location = new System.Drawing.Point(12, 168);
        lblProfile.Size = new System.Drawing.Size(120, 20);
        lblProfile.Name = "lblProfile";

        rbProfileA.Font = new System.Drawing.Font("微软雅黑", 9F);
        rbProfileA.Location = new System.Drawing.Point(24, 195);
        rbProfileA.AutoSize = true;
        rbProfileA.Checked = true;
        rbProfileA.Name = "rbProfileA";

        rbProfileB.Font = new System.Drawing.Font("微软雅黑", 9F);
        rbProfileB.Location = new System.Drawing.Point(230, 195);
        rbProfileB.AutoSize = true;
        rbProfileB.Name = "rbProfileB";

        pnlSep2.BackColor = System.Drawing.Color.FromArgb(225, 225, 225);
        pnlSep2.Location = new System.Drawing.Point(12, 228);
        pnlSep2.Size = new System.Drawing.Size(396, 1);
        pnlSep2.Name = "pnlSep2";

        // ==================== Mode（多列复选列表）====================
        lblMode.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
        lblMode.ForeColor = System.Drawing.Color.FromArgb(50, 50, 50);
        lblMode.Location = new System.Drawing.Point(12, 238);
        lblMode.Size = new System.Drawing.Size(120, 20);
        lblMode.Name = "lblMode";

        clbModes.BorderStyle = System.Windows.Forms.BorderStyle.None;
        clbModes.CheckOnClick = true;
        clbModes.ColumnWidth = 190;
        clbModes.Font = new System.Drawing.Font("微软雅黑", 9F);
        clbModes.ItemHeight = 20;
        clbModes.Location = new System.Drawing.Point(24, 265);
        clbModes.MultiColumn = true;
        clbModes.Size = new System.Drawing.Size(380, 70);
        clbModes.BackColor = System.Drawing.SystemColors.Window;
        clbModes.Name = "clbModes";

        pnlSep3.BackColor = System.Drawing.Color.FromArgb(225, 225, 225);
        pnlSep3.Location = new System.Drawing.Point(12, 340);
        pnlSep3.Size = new System.Drawing.Size(396, 1);
        pnlSep3.Name = "pnlSep3";

        // ==================== 底部两个大按钮 ====================
        btnSingleMap.Font = new System.Drawing.Font("微软雅黑", 11F, System.Drawing.FontStyle.Regular);
        btnSingleMap.Location = new System.Drawing.Point(12, 358);
        btnSingleMap.Size = new System.Drawing.Size(195, 48);
        btnSingleMap.FlatStyle = System.Windows.Forms.FlatStyle.System;
        btnSingleMap.Name = "btnSingleMap";

        btnFolder.Font = new System.Drawing.Font("微软雅黑", 11F, System.Drawing.FontStyle.Regular);
        btnFolder.Location = new System.Drawing.Point(213, 358);
        btnFolder.Size = new System.Drawing.Size(195, 48);
        btnFolder.FlatStyle = System.Windows.Forms.FlatStyle.System;
        btnFolder.Name = "btnFolder";

        // ==================== 辅助按钮 ====================
        btnCancel.Font = new System.Drawing.Font("微软雅黑", 9F);
        btnCancel.Location = new System.Drawing.Point(12, 420);
        btnCancel.Size = new System.Drawing.Size(80, 26);
        btnCancel.Name = "btnCancel";
        btnCancel.Visible = false;

        btnShowLog.Font = new System.Drawing.Font("微软雅黑", 9F);
        btnShowLog.ForeColor = System.Drawing.Color.FromArgb(0, 120, 215);
        btnShowLog.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        btnShowLog.Location = new System.Drawing.Point(320, 420);
        btnShowLog.Size = new System.Drawing.Size(88, 26);
        btnShowLog.Name = "btnShowLog";

        // ==================== 底部进度条 + 文件数 ====================
        bottomProgress.Dock = System.Windows.Forms.DockStyle.Bottom;
        bottomProgress.Size = new System.Drawing.Size(420, 22);
        bottomProgress.Maximum = 100;
        bottomProgress.Value = 0;
        bottomProgress.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
        bottomProgress.Name = "bottomProgress";

        lblBottomProgress.Dock = System.Windows.Forms.DockStyle.Bottom;
        lblBottomProgress.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
        lblBottomProgress.Font = new System.Drawing.Font("微软雅黑", 9F);
        lblBottomProgress.ForeColor = System.Drawing.Color.DimGray;
        lblBottomProgress.Size = new System.Drawing.Size(420, 22);
        lblBottomProgress.Text = "0/0";
        lblBottomProgress.Padding = new System.Windows.Forms.Padding(0, 0, 14, 0);
        lblBottomProgress.BackColor = System.Drawing.Color.FromArgb(245, 245, 245);
        lblBottomProgress.Name = "lblBottomProgress";

        // ==================== Form ====================
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
        ClientSize = new System.Drawing.Size(420, 510);
        Controls.Add(pnlTitle);
        Controls.Add(lblConfigStatus);
        Controls.Add(btnReloadConfig);
        Controls.Add(lblOperation);
        Controls.Add(rbInstall);
        Controls.Add(rbUninstall);
        Controls.Add(pnlSep1);
        Controls.Add(lblProfile);
        Controls.Add(rbProfileA);
        Controls.Add(rbProfileB);
        Controls.Add(pnlSep2);
        Controls.Add(lblMode);
        Controls.Add(clbModes);
        Controls.Add(pnlSep3);
        Controls.Add(btnSingleMap);
        Controls.Add(btnFolder);
        Controls.Add(btnCancel);
        Controls.Add(btnShowLog);
        Controls.Add(bottomProgress);
        Controls.Add(lblBottomProgress);

        FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = true;
        StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        MinimumSize = new System.Drawing.Size(420, 510);
        MaximumSize = new System.Drawing.Size(420, 510);
        Name = "MainForm";

        ResumeLayout(false);
        PerformLayout();
    }
}
