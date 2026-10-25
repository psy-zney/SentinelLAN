namespace SentinelLAN.Application;

public sealed record PageWindow(int Offset = 0, int Limit = 200)
{
    public void Validate()
    {
        if (Offset is < 0 or > 1_000_000 || Limit is < 1 or > 200)
            throw new ArgumentOutOfRangeException(nameof(PageWindow), "Trang dữ liệu không hợp lệ: offset từ 0 đến 1000000, limit từ 1 đến 200.");
    }
}
