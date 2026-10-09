import type { RequestKind, SupportCategory, SupportRequest, SupportStatus } from "@/types/self-service";

export const categoryNames: Record<SupportCategory, string> = { Network: "Không vào được mạng", Printer: "Không in được", Slow: "Máy chạy chậm", Application: "Phần mềm không mở", Suspicious: "Có thông báo đáng ngờ", Other: "Vấn đề khác" };
export const kindNames: Record<RequestKind, string> = { Incident: "Báo sự cố", InstallApp: "Cài phần mềm", Privilege: "Cài đặt cần IT duyệt", Panic: "Báo khẩn cấp", PauseAgent: "Tạm dừng SentinelLAN", UninstallAgent: "Gỡ SentinelLAN", Appointment: "Hẹn IT hỗ trợ" };
export const statusNames: Record<SupportStatus, string> = { Open: "Đã tiếp nhận", InProgress: "IT đang xử lý", AwaitingEmployee: "Chờ bạn kiểm tra", Approved: "Đã được duyệt", Rejected: "Chưa được duyệt", Resolved: "Đã xử lý", Closed: "Đã dùng được" };

export function itStatusOptions(request: Pick<SupportRequest, "kind" | "status" | "commandId" | "commandStatus">): SupportStatus[] {
  const reopen = ["Incident", "Appointment", "Panic"].includes(request.kind);
  const options: SupportStatus[] = [request.status];
  if (request.status === "Approved" && !request.commandId) return options;
  switch (request.status) {
    case "Open": if (reopen) options.push("InProgress"); break;
    case "InProgress": options.push("AwaitingEmployee", "Resolved"); break;
    case "Approved": options.push("InProgress", "AwaitingEmployee", "Resolved"); break;
    case "AwaitingEmployee": options.push("InProgress", "Resolved"); if (reopen) options.push("Open"); break;
    case "Resolved": case "Closed": if (reopen) options.push("Open"); break;
  }
  return options.filter(value => !request.commandId || request.commandStatus === "Succeeded" || !["AwaitingEmployee", "Resolved"].includes(value));
}

export function executionLabel(request: Pick<SupportRequest, "commandId" | "commandStatus" | "kind" | "status">): string | null {
  if (!request.commandId) {
    if (request.kind === "Panic") return "Đã báo IT. Chưa có xác nhận máy được cô lập.";
    if (request.status === "Approved" && (request.kind === "PauseAgent" || request.kind === "UninstallAgent")) return "Chờ bạn nhập mã xác nhận do IT cấp.";
    return null;
  }
  switch (request.commandStatus) {
    case "Succeeded": return request.kind === "Panic" ? "Agent đã xác nhận lệnh cô lập trong môi trường lab." : "Agent đã xác nhận hoàn tất.";
    case "Failed": return "Thao tác trên máy chưa thực hiện được. IT cần kiểm tra.";
    case "Expired": return "Lệnh đã hết hạn trước khi được giao cho máy.";
    case "ExecutionUnconfirmed": return "Lệnh đã được giao nhưng chưa có xác nhận. IT cần kiểm tra trước khi thực hiện lại.";
    default: return "Đang chờ máy tính xác nhận. Bạn chưa cần gửi lại yêu cầu.";
  }
}

export function validateSupportImage(file: Pick<File, "size" | "type">): string | null {
  if (file.type !== "image/jpeg" && file.type !== "image/png") return "Chọn ảnh PNG hoặc JPEG.";
  if (file.size <= 0 || file.size > 2 * 1024 * 1024) return "Chọn ảnh có dung lượng tối đa 2 MB.";
  return null;
}
