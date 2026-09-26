# Installed under protected Program Files. This helper never receives device credentials.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

function Read-Exact([IO.Stream]$Stream, [int]$Length) {
    $buffer = New-Object byte[] $Length
    $offset = 0
    while ($offset -lt $Length) {
        $pending = $Stream.BeginRead($buffer, $offset, $Length - $offset, $null, $null)
        if (-not $pending.AsyncWaitHandle.WaitOne(15000)) { throw 'Response timed out.' }
        $count = $Stream.EndRead($pending)
        if ($count -le 0) { throw 'Agent disconnected.' }
        $offset += $count
    }
    return ,$buffer
}

$form = New-Object Windows.Forms.Form
$form.Text = 'SentinelLAN - Xác nhận của IT'
$form.Size = New-Object Drawing.Size(510, 410)
$form.StartPosition = 'CenterScreen'
$form.FormBorderStyle = 'FixedDialog'
$form.MaximizeBox = $false
$form.MinimizeBox = $false
$intro = New-Object Windows.Forms.Label
$intro.Text = "Để tạm dừng hoặc gỡ SentinelLAN, bạn cần IT phê duyệt.`nLấy mã yêu cầu và mã xác nhận từ IT. Đóng cửa sổ này vẫn giữ Agent hoạt động."
$intro.Location = New-Object Drawing.Point(20, 20)
$intro.Size = New-Object Drawing.Size(455, 65)
$form.Controls.Add($intro)
$requestLabel = New-Object Windows.Forms.Label
$requestLabel.Text = 'Mã yêu cầu (sao chép từ yêu cầu trên điện thoại)'
$requestLabel.Location = New-Object Drawing.Point(20, 95)
$requestLabel.AutoSize = $true
$form.Controls.Add($requestLabel)
$requestBox = New-Object Windows.Forms.TextBox
$requestBox.Location = New-Object Drawing.Point(20, 120)
$requestBox.Size = New-Object Drawing.Size(455, 25)
$requestBox.MaxLength = 36
$form.Controls.Add($requestBox)
$codeLabel = New-Object Windows.Forms.Label
$codeLabel.Text = 'Mã xác nhận do IT cấp (8 chữ số)'
$codeLabel.Location = New-Object Drawing.Point(20, 155)
$codeLabel.AutoSize = $true
$form.Controls.Add($codeLabel)
$codeBox = New-Object Windows.Forms.TextBox
$codeBox.Location = New-Object Drawing.Point(20, 180)
$codeBox.Size = New-Object Drawing.Size(455, 25)
$codeBox.MaxLength = 8
$codeBox.UseSystemPasswordChar = $true
$form.Controls.Add($codeBox)
$confirmed = New-Object Windows.Forms.CheckBox
$confirmed.Text = 'Tôi xác nhận thao tác đã được IT duyệt cho máy này.'
$confirmed.Location = New-Object Drawing.Point(20, 220)
$confirmed.Size = New-Object Drawing.Size(455, 30)
$form.Controls.Add($confirmed)
$status = New-Object Windows.Forms.Label
$status.Location = New-Object Drawing.Point(20, 265)
$status.Size = New-Object Drawing.Size(455, 60)
$form.Controls.Add($status)
$send = New-Object Windows.Forms.Button
$send.Text = 'Xác nhận mã'
$send.Location = New-Object Drawing.Point(220, 330)
$send.Size = New-Object Drawing.Size(120, 30)
$form.Controls.Add($send)
$cancel = New-Object Windows.Forms.Button
$cancel.Text = 'Hủy'
$cancel.Location = New-Object Drawing.Point(355, 330)
$cancel.Size = New-Object Drawing.Size(120, 30)
$cancel.Add_Click({ $form.Close() })
$form.Controls.Add($cancel)
$form.CancelButton = $cancel
$form.AcceptButton = $send
$send.Add_Click({
    $requestId = [Guid]::Empty
    if (-not [Guid]::TryParse($requestBox.Text, [ref]$requestId) -or $requestId -eq [Guid]::Empty -or
        $codeBox.Text -notmatch '^[0-9]{8}$' -or -not $confirmed.Checked) {
        $status.Text = 'Nhập đúng mã yêu cầu, mã IT gồm 8 chữ số và xác nhận thao tác.'
        return
    }
    $send.Enabled = $false
    $status.Text = 'Đang kiểm tra với IT...'
    [Windows.Forms.Application]::DoEvents()
    $pipe = $null
    try {
        $pipe = [IO.Pipes.NamedPipeClientStream]::new('.', 'SentinelLAN.Maintenance.v1',
            [IO.Pipes.PipeDirection]::InOut, [IO.Pipes.PipeOptions]::Asynchronous,
            [Security.Principal.TokenImpersonationLevel]::Identification)
        $pipe.Connect(3000)
        $owner = $pipe.GetAccessControl().GetOwner([Security.Principal.SecurityIdentifier]).Value
        if ($owner -notin @('S-1-5-18', 'S-1-5-19', 'S-1-5-32-544')) { throw 'Untrusted pipe owner.' }
        $request = @{ requestId = $requestId.ToString(); code = $codeBox.Text; confirmed = $true } | ConvertTo-Json -Compress
        $bytes = [Text.Encoding]::UTF8.GetBytes($request)
        $header = [BitConverter]::GetBytes([int]$bytes.Length)
        $pipe.Write($header, 0, $header.Length)
        $pipe.Write($bytes, 0, $bytes.Length)
        $pipe.Flush()
        $responseHeader = Read-Exact $pipe 4
        $length = [BitConverter]::ToInt32($responseHeader, 0)
        if ($length -le 0 -or $length -gt 4096) { throw 'Invalid response.' }
        $response = [Text.Encoding]::UTF8.GetString((Read-Exact $pipe $length)) | ConvertFrom-Json
        $status.Text = [string]$response.message
        if ($response.succeeded) { $codeBox.Clear(); $confirmed.Checked = $false }
    }
    catch {
        $status.Text = 'Không kết nối được với Agent. Chưa có thao tác nào được xác nhận; hãy nhờ IT kiểm tra dịch vụ hoặc sửa bộ cài.'
    }
    finally {
        if ($pipe) { $pipe.Dispose() }
        $codeBox.Clear()
        $send.Enabled = $true
    }
})
[void]$form.ShowDialog()
$codeBox.Clear()
$form.Dispose()
