# Quy trình phát triển SentinelLAN

Mỗi nhánh chỉ xử lý một mục tiêu có thể review độc lập. Không phát triển trực tiếp trên `main`; mọi thay đổi vào `main` đi qua pull request.

## Tên nhánh

Tên nhánh dùng chữ thường, kebab-case và một trong các tiền tố sau:

- `feature/<issue>-<mo-ta>`: tính năng mới, ví dụ `feature/42-employee-device-view`
- `fix/<issue>-<mo-ta>`: sửa lỗi thông thường
- `hotfix/<issue>-<mo-ta>`: sửa lỗi sản xuất khẩn cấp
- `refactor/<issue>-<mo-ta>`: đổi cấu trúc nhưng không đổi hành vi
- `docs/<issue>-<mo-ta>`: tài liệu
- `test/<issue>-<mo-ta>`: kiểm thử
- `chore/<issue>-<mo-ta>`: bảo trì repository hoặc dependency
- `build/`, `ci/`, `perf/`: build, CI và hiệu năng
- `release/v<major>.<minor>.<patch>`: chuẩn bị phiên bản

`dependabot/**` là namespace dành riêng cho bot và không dùng cho nhánh do người tạo.

## Pull request

Tiêu đề dùng Conventional Commit, ví dụ `feat(auth): complete employee login flow`. PR phải target `main`, có phạm vi nhỏ, điền template và giải quyết hết review thread.

Các gate bắt buộc trước khi merge:

1. `policy` kiểm tra tên nhánh và tiêu đề PR.
2. `verify` chạy format, build, unit/integration, web và E2E.
3. `dependencies` quét lỗ hổng NuGet và npm.

Chỉ dùng squash hoặc rebase merge để giữ lịch sử tuyến tính. Cấm force-push và xóa `main`. Với repository chỉ có một maintainer, không bắt buộc tự phê duyệt PR của chính mình; khi có thêm reviewer độc lập, tăng ruleset lên tối thiểu một approval và bật code-owner review.

## Dependency

Dependabot gom bản cập nhật minor/patch theo từng ecosystem và gom security update riêng. Major upgrade phải được mở chủ động bằng nhánh `chore/<issue>-upgrade-<dependency>` để có kế hoạch tương thích và rollback rõ ràng.
