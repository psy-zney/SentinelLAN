using SentinelLAN.Enrollment;

namespace SentinelLAN.Setup;

internal sealed class SetupForm : Form
{
    private readonly TextBox codeInput = new() { Dock = DockStyle.Fill, UseSystemPasswordChar = true, MaxLength = 4096, AccessibleName = "Mã kết nối do IT cấp" };
    private readonly Button connectButton = new() { Text = "Cài đặt và kết nối", AutoSize = true, Padding = new Padding(16, 8, 16, 8) };
    private readonly Label status = new() { AutoSize = true, MaximumSize = new Size(590, 0), AccessibleName = "Tiến độ kết nối" };
    private bool running;

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
        layout.Controls.Add(new Label { Text = string.IsNullOrWhiteSpace(SetupInstaller.CompanyServerUrl)
            ? "Dán mã do IT cấp. Địa chỉ máy chủ đã có trong mã.\nMỗi mã chỉ đăng ký được một máy và có thời hạn."
            : "Dán mã do IT cấp. Bộ cài đã cấu hình hệ thống công ty.\nMỗi mã chỉ đăng ký được một máy và có thời hạn.", AutoSize = true });
        layout.Controls.Add(new Label { Text = "Mã kết nối", AutoSize = true });
        layout.Controls.Add(codeInput);
        layout.Controls.Add(new Label { Text = "Agent gửi thông tin CPU, RAM, ổ đĩa và trạng thái máy.\nẢnh hỗ trợ do bạn chủ động chọn và gửi.", AutoSize = true });
        layout.Controls.Add(connectButton);
        layout.Controls.Add(status);
        foreach (Control control in layout.Controls) control.Margin = new Padding(0, 0, 0, 12);
        Controls.Add(layout);
        AcceptButton = connectButton;
        connectButton.Click += async (_, _) => await ConnectAsync();
        FormClosing += (_, args) => { if (running) args.Cancel = true; };
        if (SetupInstaller.IsEnrolled)
        {
            codeInput.Enabled = connectButton.Enabled = false;
            status.Text = "Máy đã có định danh Agent. Không cần dùng mã mới. Liên hệ IT nếu cần đổi công ty hoặc đăng ký lại.";
        }
    }

    private async Task ConnectAsync()
    {
        if (running) return;
        try
        {
            var code = string.IsNullOrWhiteSpace(SetupInstaller.CompanyServerUrl)
                ? EnrollmentConnectionCode.Decode(codeInput.Text)
                : CompanyEnrollment.Resolve(codeInput.Text, SetupInstaller.CompanyServerUrl);
            if (code.ExpiresAt <= DateTimeOffset.UtcNow)
                throw new InvalidOperationException("Mã kết nối đã hết hạn. Vui lòng liên hệ IT để cấp mã mới.");
            running = true;
            codeInput.Enabled = connectButton.Enabled = false;
            var progress = new Progress<string>(message => status.Text = message);
            await SetupInstaller.ConnectAsync(code, progress);
            codeInput.Clear();
            status.Text = "Kết nối thành công. Agent sẽ tự chạy khi bật máy. IT có thể gán máy này cho tài khoản của bạn.";
            connectButton.Text = "Đã kết nối";
        }
        catch (Exception exception) when (exception is FormatException or InvalidOperationException or IOException or
            HttpRequestException or System.ComponentModel.Win32Exception or UnauthorizedAccessException or TaskCanceledException)
        {
            status.Text = exception is FormatException or InvalidOperationException
                ? exception.Message
                : "Chưa xác nhận được kết nối. Kiểm tra mạng và nhờ IT kiểm tra mã, chứng chỉ máy chủ và trạng thái Agent.";
            codeInput.Clear();
            codeInput.Enabled = connectButton.Enabled = !SetupInstaller.IsEnrolled;
        }
        finally { running = false; }
    }
}
