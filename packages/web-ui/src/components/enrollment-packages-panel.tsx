"use client";
import { useMemo, useState } from "react";
import { QRCodeWriter, BarcodeFormat } from "@zxing/library";
import { ApiClient } from "@/lib/api-client";

type Package = { connectionCode: string; expiresAt: string; fileName: string; script: string };
function EnrollmentQr({ value }: { value: string }) {
  const qr = useMemo(() => {
    try { const matrix = new QRCodeWriter().encode(value, BarcodeFormat.QR_CODE, 0, 0, new Map()); let path = ""; for (let y = 0; y < matrix.getHeight(); y++) for (let x = 0; x < matrix.getWidth(); x++) if (matrix.get(x, y)) path += `M${x},${y}h1v1h-1z`; return { size: matrix.getWidth(), path }; } catch { return null; }
  }, [value]);
  return qr ? <svg role="img" aria-label="QR đăng ký thiết bị, giữ riêng" width={260} height={260} viewBox={`0 0 ${qr.size} ${qr.size}`}><rect width="100%" height="100%" fill="white" /><path d={qr.path} fill="black" /></svg> : <p>Mã quá dài để tạo QR. Hãy dùng script hoặc sao chép mã.</p>;
}
function download(name: string, content: string) {
  const url = URL.createObjectURL(new Blob([content], { type: "text/plain;charset=utf-8" })); const link = document.createElement("a"); link.href = url; link.download = name; link.click(); setTimeout(() => URL.revokeObjectURL(url), 1000);
}
export function EnrollmentPackagesPanel() {
  const [count, setCount] = useState(1); const [platform, setPlatform] = useState<"Windows" | "Linux">("Windows"); const [reason, setReason] = useState(""); const [confirmed, setConfirmed] = useState(false); const [busy, setBusy] = useState(false); const [packages, setPackages] = useState<Package[]>([]); const [message, setMessage] = useState(""); const [selected, setSelected] = useState(0);
  const current = packages[selected];
  return <details className="panel upgrade-panel"><summary>Cài Agent nhanh / cấp lô tối đa 50 máy</summary><p>Mỗi máy có mã riêng, dùng một lần trong 15 phút. Script chứa mã bí mật; chỉ chuyển cho IT được ủy quyền và xóa sau khi dùng. QR được tạo ngay trong trình duyệt.</p>
    <form className="management-form" onSubmit={e => { e.preventDefault(); if (busy || !confirmed) return; setBusy(true); setMessage(""); void new ApiClient().enrollmentPackages(count, platform, reason).then(result => { setPackages(result); setSelected(0); }).catch(cause => setMessage(cause instanceof Error ? cause.message : "Chưa cấp được gói cài")).finally(() => setBusy(false)); }}>
      <label>Số máy<input required type="number" min={1} max={50} value={count} onChange={e => setCount(Number(e.target.value))} /></label><label>Nền tảng<select value={platform} onChange={e => setPlatform(e.target.value as typeof platform)}><option>Windows</option><option>Linux</option></select></label><label>Lý do<input required minLength={3} maxLength={1000} value={reason} onChange={e => setReason(e.target.value)} /></label><label><input type="checkbox" checked={confirmed} onChange={e => setConfirmed(e.target.checked)} /> Tôi được phép cài Agent cho các máy này</label><button className="action" disabled={busy || !confirmed}>Cấp gói cài 15 phút</button>
    </form>{message && <p role="alert">{message}</p>}
    {current && <><label>Gói dành riêng cho máy<select value={selected} onChange={e => setSelected(Number(e.target.value))}>{packages.map((p, index) => <option key={p.fileName} value={index}>Máy {index + 1}</option>)}</select></label><p>Hết hạn: {new Date(current.expiresAt).toLocaleString("vi-VN")}</p><EnrollmentQr value={current.connectionCode} /><label>Mã kết nối<input readOnly value={current.connectionCode} onFocus={e => e.target.select()} /></label><button className="action" type="button" onClick={() => download(current.fileName, current.script)}>Tải script riêng cho máy {selected + 1}</button><button className="action-outline" type="button" onClick={() => download("sentinellan-enrollment-manifest.json", JSON.stringify(packages, null, 2))}>Tải toàn bộ lô (JSON riêng tư)</button><button className="action-outline" type="button" onClick={() => { setPackages([]); setMessage("Đã ẩn mã khỏi trang; các mã chưa dùng vẫn hết hạn theo thời gian đã cấp."); }}>Ẩn các mã</button><p>IT mở và kiểm tra script đã tải, rồi chạy bằng PowerShell quản trị hoặc sudo bash. Windows kiểm tra SHA-256 và Authenticode; Linux kiểm tra SHA-256 của executable đã được IT duyệt. Mỗi gói chỉ dành cho một máy.</p></>}
  </details>;
}
