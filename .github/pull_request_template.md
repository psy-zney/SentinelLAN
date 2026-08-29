## Mục tiêu

Mô tả ngắn gọn vấn đề và kết quả mong muốn.

## Phạm vi thay đổi

- [ ] Một mục tiêu chính, không trộn thay đổi không liên quan
- [ ] Không thêm secret, dữ liệu nhạy cảm hoặc build artifact
- [ ] Đã cập nhật OpenAPI, migration, ADR hoặc tài liệu nếu contract thay đổi

## Kiểm chứng

- [ ] `dotnet format SentinelLAN.slnx --verify-no-changes`
- [ ] `dotnet build SentinelLAN.slnx`
- [ ] `dotnet test SentinelLAN.slnx`
- [ ] `npm.cmd run lint`
- [ ] `npm.cmd run typecheck`
- [ ] `npm.cmd test`
- [ ] `npm.cmd run build`
- [ ] E2E hoặc smoke test liên quan đã đạt

## An toàn

- [ ] Authorization policy và tenant isolation đã được kiểm tra
- [ ] Không có hành vi giám sát bí mật, lấy credential hoặc thực thi tùy ý
- [ ] Thao tác nhạy cảm có permission, reason, expiry, nonce, confirmation và audit phù hợp

## Bằng chứng

Ghi lệnh đã chạy, kết quả, ảnh hoặc log ngắn cần thiết.
