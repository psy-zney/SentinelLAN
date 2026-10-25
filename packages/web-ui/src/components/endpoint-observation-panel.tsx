"use client";

import { useCallback } from "react";
import { ApiClient } from "@/lib/api-client";
import { useLiveQuery } from "@/hooks/use-live-query";
import { useTranslation } from "@/lib/i18n";

export function EndpointObservationPanel({ deviceId, technical = false }: { deviceId?: string; technical?: boolean }) {
  const { lang } = useTranslation();
  const vi = lang === "vi";
  const load = useCallback(() => new ApiClient().endpointObservations(deviceId), [deviceId]);
  // The updates hub is administrator-only; employee views refresh their own API.
  const { data, error, refresh } = useLiveQuery(load, Boolean(deviceId));
  const inventory = data?.inventory;
  const security = data?.security;
  const rows = [
    [vi ? "Bảo vệ chống mã độc" : "Antivirus protection", security?.antivirusStatus],
    [vi ? "Tường lửa mạng cơ quan" : "Domain firewall", security?.firewallDomainStatus],
    [vi ? "Tường lửa mạng riêng" : "Private firewall", security?.firewallPrivateStatus],
    [vi ? "Tường lửa mạng công cộng" : "Public firewall", security?.firewallPublicStatus],
    [vi ? "Bảo vệ mã hóa ổ đĩa" : "Drive encryption protection", security?.diskEncryptionStatus]
  ];
  const size = (bytes: number | null | undefined) => bytes == null ? "—" : `${new Intl.NumberFormat(vi ? "vi-VN" : "en", { maximumFractionDigits: 1 }).format(bytes / 1024 ** 3)} GiB`;
  return <section className="panel" aria-label={vi ? "Cấu hình và bảo vệ máy" : "Hardware and endpoint protection"}>
    <div className="panel-head"><div><h2>{vi ? "Cấu hình và bảo vệ máy" : "Hardware and endpoint protection"}</h2>
      <p className="subtitle">{vi ? "Thông tin kỹ thuật do Agent báo cáo; thiếu quyền đọc có thể khiến trạng thái chưa xác định." : "Technical observations reported by the Agent; restricted access may leave a status unknown."}</p></div>
      <button className="action-sm" type="button" onClick={refresh}>{vi ? "Cập nhật" : "Refresh"}</button></div>
    {error ? <p role="alert">{vi ? "Chưa đọc được thông tin máy. Kiểm tra kết nối hoặc nhờ IT cập nhật Agent." : "Could not load observations. Check connectivity or ask IT to update the Agent."}</p> : !data ? <p role="status">{vi ? "Đang tải…" : "Loading…"}</p> : <>
      {inventory ? <><dl className="device-facts">
        <div><dt>{vi ? "Bộ xử lý" : "Processor"}</dt><dd>{inventory.cpuName ?? "—"}</dd></div>
        <div><dt>RAM</dt><dd>{size(inventory.ramBytes)}</dd></div>
        <div><dt>{vi ? "Ổ đĩa hệ thống" : "System drive"}</dt><dd>{size(inventory.systemDiskBytes)}</dd></div>
        <div><dt>{vi ? "Số sê-ri" : "Serial number"}</dt><dd>{inventory.serialNumber ?? "—"}</dd></div>
      </dl><p className="subtitle">{vi ? "Lần đọc cấu hình:" : "Inventory captured:"} {new Date(inventory.capturedAt).toLocaleString(vi ? "vi-VN" : "en")}</p>
        {technical && inventory.networkAdapters.length > 0 && <details><summary>{vi ? "Địa chỉ mạng LAN" : "LAN addresses"}</summary>
          <ul>{inventory.networkAdapters.map(adapter => <li key={adapter.macAddress}><code>{adapter.macAddress}</code> · {adapter.addresses.join(", ")}</li>)}</ul></details>}</> : <p className="subtitle">{vi ? "Agent chưa báo cáo cấu hình phần cứng." : "No hardware inventory has been reported."}</p>}
      <dl className="device-facts">{rows.map(([name, status]) => {
        const current = !data.securityStale;
        const healthy = current && status === "Healthy";
        const unhealthy = current && status === "Unhealthy";
        const unsupported = status === "NotSupported";
        const label = healthy ? (vi ? "Đang bảo vệ" : "Protected") : unhealthy ? (vi ? "Cần IT kiểm tra" : "Needs IT attention") : unsupported ? (vi ? "Không hỗ trợ trên máy này" : "Not supported") : (vi ? "Chưa xác định" : "Unknown");
        return <div key={name}><dt>{name}</dt><dd><span className={`badge ${healthy ? "badge-success" : unhealthy ? "badge-warn" : "badge-info"}`}>{label}</span></dd></div>;
      })}</dl>
      {data.securityStale && <p className="subtitle">{vi ? "Chưa có mẫu bảo mật mới trong 2 phút. Trạng thái cũ không được dùng để kết luận máy đang an toàn." : "No fresh security sample within two minutes. Older observations do not establish current protection."}</p>}
      {security && <p className="subtitle">{vi ? "Lần đọc bảo mật:" : "Protection captured:"} {new Date(security.capturedAt).toLocaleString(vi ? "vi-VN" : "en")}</p>}
    </>}
  </section>;
}
