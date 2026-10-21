# Chữ ký lệnh phiên bản 2

Ngày: 05/10/2026. Phạm vi: G3/F12.

SelfHost bắt buộc khóa riêng RSA tối thiểu 3072 bit ở server (`SENTINELLAN_COMMAND_PRIVATE_KEY_PEM`, `SENTINELLAN_COMMAND_KEY_ID`). Agent chỉ nhận SPKI công khai base64 và key ID từ bộ cài công ty đã xác thực, lưu qua cấu hình Windows có ACL. Khóa không được tải qua response đăng ký hoặc nguồn từ xa chưa xác thực.

Signature có định dạng `R2.<key-id>.<base64-signature>`; dùng SHA256 và RSA-PSS. Payload UTF8 là JSON array theo thứ tự: version=2, key ID, command ID, organization ID, device ID, actor ID, type, reason, issuedAt/expiry Unix milliseconds, nonce, parameter. Null parameter khác chuỗi rỗng; serializer .NET chuẩn của cả hai adapter được kiểm tra bằng test liên thông. Mọi trường đều được ký; status/delivery lease không thuộc envelope.

Verifier RSA chỉ nhận R2 với đúng key ID, không fallback HMAC. Thiết bị sai, thời hạn và nonce vẫn do CommandVerifier kiểm tra. Platform hiện hữu còn HMAC khi chưa cấu hình khóa RSA để tránh chuyển đổi âm thầm; không dùng profile đó như bằng chứng nghiệm thu SelfHost.

Luân chuyển khóa hiện dùng quy trình có IT: dừng tạo lệnh, hoàn tất/hết hạn lệnh cũ, tạo bản bộ cài/cấu hình được xác thực chứa public key/key ID mới, cập nhật Agent bằng IT, sau đó đổi khóa server và kiểm tra lệnh thử. Agent chưa đổi khóa từ chối lệnh mới; không tự bỏ xác minh. Khóa cũ bị lộ: ngừng gửi thao tác, kiểm tra audit và đổi trust bằng quy trình IT; chưa có cập nhật keyring tự động. Đây là giới hạn G3 còn phải nghiệm thu.

RsaCommandSecurityTests kiểm tra liên thông server/Agent, sửa từng trường, sai khóa, sai ID/version, HMAC bị từ chối, replay, sai máy, hết hạn, khóa yếu và khóa riêng bị thiếu. Chữ ký bảo vệ nguồn và toàn vẹn, không mã hóa nội dung.
