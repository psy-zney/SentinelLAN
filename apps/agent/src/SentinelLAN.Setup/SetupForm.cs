using SentinelLAN.Enrollment;

namespace SentinelLAN.Setup;

internal sealed class SetupForm : Form
{
    private readonly TextBox codeInput = new() { Dock = DockStyle.Fill, UseSystemPasswordChar = true, MaxLength = 4096, AccessibleName = "Mã kết nối do IT cấp" };
    private readonly Button connectButton = new() { Text = "Cài đặt và kết nối", AutoSize = true, Padding = new Padding(16, 8, 16, 8) };
    private readonly Button closeButton = new() { Text = "Đóng", AutoSize = true, Padding = new Padding(16, 8, 16, 8) };
    private readonly Label codeLabel = new() { Text = "Mã kết nối", AutoSize = true };
    private readonly Label introduction = new() { AutoSize = true };
    private readonly Label status = new() { AutoSize = true, MaximumSize = new Size(590, 0), AccessibleName = "Tiến độ kết nối" };
    private readonly SetupWorkflow workflow = new(() => SetupInstaller.IsEnrolled);
    private bool running;
    private bool closeRequested;

    public SetupForm()
    {
        Text = "SentinelLAN — Kết nối máy với công ty";
        ClientSize = new Size(650, 400);
        MinimumSize = new Size(650, 400);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 11);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(28), ColumnCount = 1, RowCount = 7 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label { Text = "Kết nối thiết bị với công ty", AutoSize = true, Font = new Font(Font, FontStyle.Bold) });
        layout.Controls.Add(introduction);
        layout.Controls.Add(codeLabel);
        layout.Controls.Add(codeInput);
        layout.Controls.Add(new Label { Text = "Agent gửi thông tin CPU, RAM, ổ đĩa và trạng thái máy.\nẢnh hỗ trợ do bạn chủ động chọn và gửi.", AutoSize = true });
        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
        actions.Controls.Add(connectButton);
        actions.Controls.Add(closeButton);
        layout.Controls.Add(actions);
        layout.Controls.Add(status);
        foreach (Control control in layout.Controls) control.Margin = new Padding(0, 0, 0, 12);
        Controls.Add(layout);
        AcceptButton = connectButton;
        connectButton.Click += async (_, _) =>
        {
            if (workflow.Completed) Close();
            else await ConnectAsync();
        };
        closeButton.Click += (_, _) => Close();
        FormClosing += (_, args) =>
        {
            if (!running) return;
            closeRequested = true;
            args.Cancel = true;
            status.Text = "Đang hoàn tất bước hiện tại để đóng an toàn…";
        };
        Shown += async (_, _) => { if (!workflow.RequiresEnrollment) await ConnectAsync(); };
        UpdateActions();
    }

    private EnrollmentConnectionCode ReadCode()
    {
        var code = string.IsNullOrWhiteSpace(SetupInstaller.CompanyServerUrl)
                ? EnrollmentConnectionCode.Decode(codeInput.Text)
                : CompanyEnrollment.Resolve(codeInput.Text, SetupInstaller.CompanyServerUrl);
        if (code.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new InvalidOperationException("Mã kết nối đã hết hạn. Vui lòng liên hệ IT để cấp mã mới.");
        return code;
    }

    private void UpdateActions()
    {
        var server = string.IsNullOrWhiteSpace(SetupInstaller.CompanyServerUrl)
            ? "" : $"Máy chủ công ty: {SetupInstaller.CompanyServerUrl}\n";
        introduction.Text = server + (workflow.RequiresEnrollment
            ? "Dán mã do IT cấp. Mỗi mã chỉ đăng ký được một máy và có thời hạn."
            : "Máy đã đăng ký. Setup sẽ kiểm tra và khôi phục kết nối, không cần mã mới.");
        codeLabel.Visible = codeInput.Visible = workflow.RequiresEnrollment && !workflow.Completed;
        codeInput.Enabled = connectButton.Enabled = !running;
        connectButton.Text = workflow.Completed ? "Hoàn tất"
            : running ? "Đang kết nối…"
            : workflow.RequiresEnrollment ? "Cài đặt và kết nối" : "Thử lại kết nối";
    }

    private async Task ConnectAsync()
    {
        if (running) return;
        running = true;
        UpdateActions();
        try
        {
            var progress = new Progress<string>(message => { if (running && !workflow.Completed) status.Text = message; });
            await workflow.RunAsync(
                () => SetupInstaller.ConnectAsync(ReadCode(), progress),
                () => SetupInstaller.ResumeAsync(progress));
            codeInput.Clear();
            status.Text = "Kết nối thành công. Agent sẽ tự chạy khi bật máy. IT có thể gán máy này cho tài khoản của bạn.";
        }
        catch (Exception exception) when (exception is FormatException or InvalidOperationException or IOException or
            HttpRequestException or System.ComponentModel.Win32Exception or UnauthorizedAccessException or TaskCanceledException)
        {
            status.Text = exception is FormatException or InvalidOperationException
                ? exception.Message
                : "Chưa xác nhận được kết nối. Kiểm tra mạng và nhờ IT kiểm tra mã, chứng chỉ máy chủ và trạng thái Agent.";
            codeInput.Clear();
        }
        finally
        {
            running = false;
            UpdateActions();
            if (closeRequested) Close();
        }
    }
}
