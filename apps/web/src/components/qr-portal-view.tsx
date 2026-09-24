"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import type { Route } from "next";
import { useRouter } from "next/navigation";
import { ApiClient } from "@/lib/api-client";
import { useTranslation } from "@/lib/i18n";
import type { PublicQrResolveResponse } from "@/types/api";

export function QrPortalView({ id }: { id: string }) {
  const { t, lang, setLang } = useTranslation();
  const router = useRouter();

  const [device, setDevice] = useState<PublicQrResolveResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

  useEffect(() => {
    let active = true;
    const client = new ApiClient();

    // 1. Try authenticated resolution first
    client
      .resolveQr(id)
      .then((authRes) => {
        if (!active) return;
        if (authRes?.authorized && authRes.nextRoute) {
          router.replace(authRes.nextRoute as Route);
          return;
        }
        fetchPublicTag();
      })
      .catch(() => {
        if (active) {
          fetchPublicTag();
        }
      });

    function fetchPublicTag() {
      client
        .resolvePublicQr(id)
        .then((res) => {
          if (!active) return;
          if (res?.deviceName) {
            setDevice(res);
            setLoading(false);
          } else {
            setError(true);
            setLoading(false);
          }
        })
        .catch(() => {
          if (active) {
            setError(true);
            setLoading(false);
          }
        });
    }

    return () => {
      active = false;
    };
  }, [id, router]);

  if (loading) {
    return (
      <div style={{ minHeight: "100vh", display: "grid", placeItems: "center", padding: 20 }}>
        <p className="subtitle">{lang === "vi" ? "Đang xác thực tem mã QR hiện trường..." : "Verifying asset QR tag..."}</p>
      </div>
    );
  }

  if (error || !device) {
    return (
      <div style={{ minHeight: "100vh", display: "grid", placeItems: "center", padding: 20 }}>
        <div className="panel" style={{ maxWidth: 420, textAlign: "center", padding: 28 }}>
          <div style={{ fontSize: "2.8rem", marginBottom: 10 }}>❌</div>
          <h2 style={{ fontSize: "1.25rem", margin: "0 0 8px" }}>
            {lang === "vi" ? "Không tìm thấy thiết bị" : "Device Not Found"}
          </h2>
          <p className="subtitle" style={{ fontSize: ".86rem", marginBottom: 20 }}>
            {lang === "vi"
              ? "Mã QR không hợp lệ, đã hết hạn hoặc đã bị thu hồi bởi quản trị viên."
              : "This QR code is invalid, expired, or has been revoked by an administrator."}
          </p>
          <div style={{ display: "grid", gap: 8 }}>
            <Link href="/scan" className="action" style={{ display: "inline-block", padding: "11px", textAlign: "center" }}>
              📷 {lang === "vi" ? "Quét lại mã khác" : "Scan Another Code"}
            </Link>
            <Link href="/login" className="action-outline" style={{ display: "inline-block", padding: "10px", textAlign: "center" }}>
              {lang === "vi" ? "Về trang đăng nhập" : "Go to Sign In"}
            </Link>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div style={{ minHeight: "100vh", background: "radial-gradient(circle at 50% 0%, #d6ece6 0, transparent 24rem), #f4f7f5", padding: "24px 16px" }}>
      <div style={{ maxWidth: 480, margin: "0 auto" }}>
        {/* Header */}
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 20 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
            <div style={{ width: 30, height: 34, border: "2px solid #0b6b5f", borderRadius: "10px 10px 6px 6px", display: "grid", placeItems: "center", color: "#0b6b5f", fontWeight: 800 }}>
              S
            </div>
            <div>
              <strong style={{ fontSize: "1.1rem" }}>SentinelLAN</strong>
              <div style={{ fontSize: ".72rem", color: "var(--muted)" }}>{t("publicAssetTag")}</div>
            </div>
          </div>
          <button
            type="button"
            className="action-outline"
            style={{ padding: "4px 10px", fontSize: ".75rem" }}
            onClick={() => setLang(lang === "vi" ? "en" : "vi")}
          >
            🌐 {lang === "vi" ? "EN" : "VI"}
          </button>
        </div>

        {/* Minimal Public Asset Tag Card */}
        <div className="panel" style={{ padding: 24, boxShadow: "0 15px 35px rgba(20, 55, 50, .08)" }}>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 14 }}>
            <span className="badge badge-success">✓ Verified SentinelLAN Tag</span>
            <span className="badge badge-neutral">{device.assetStatus}</span>
          </div>

          <h1 style={{ fontSize: "1.5rem", margin: "0 0 4px" }}>💻 {device.deviceName}</h1>
          <p style={{ color: "var(--muted)", margin: "0 0 16px", fontSize: ".88rem" }}>
            {device.isOnline ? (lang === "vi" ? "Đang trực tuyến" : "Online") : (lang === "vi" ? "Đang ngoại tuyến" : "Offline")}
          </p>

          <div style={{ background: "#f8faf9", padding: "12px 14px", borderRadius: 10, border: "1px dashed #cad9d4", margin: "14px 0" }}>
            <span style={{ fontSize: ".72rem", color: "var(--muted)", textTransform: "uppercase", letterSpacing: ".08em", fontWeight: 700 }}>
              {lang === "vi" ? "Tiền tố Số Sê-ri Phần Cứng" : "Hardware Serial Prefix"}
            </span>
            <div style={{ fontSize: "1.15rem", fontWeight: 800, letterSpacing: ".05em", color: "var(--ink)", fontFamily: "monospace", marginTop: 4 }}>
              {device.assetTag}
            </div>
          </div>

          {/* Privacy explanation */}
          <div className="privacy" style={{ margin: "16px 0", fontSize: ".82rem" }}>
            <strong>🛡️ {lang === "vi" ? "Bảo vệ thông tin công cộng" : "Public Privacy Protection"}</strong>
            <p style={{ margin: "4px 0 0" }}>
              {lang === "vi"
                ? "Dữ liệu người dùng, cấu hình phần cứng chi tiết và telemetry sức khỏe được bảo vệ nghiêm ngặt và chỉ hiển thị sau khi xác thực danh tính."
                : "Assigned user identity, full hardware specifications, and telemetry are strictly restricted and accessible only after authentication."}
            </p>
          </div>

          {/* Action deep links */}
          <div style={{ display: "grid", gap: 10, marginTop: 20 }}>
            <Link
              href="/login"
              className="action"
              style={{ width: "100%", padding: "12px", textAlign: "center", fontSize: ".92rem", boxSizing: "border-box" }}
            >
              🔐 {lang === "vi" ? "Đăng nhập để xem chi tiết / quản lý" : "Sign In to Access Device"}
            </Link>

            <Link
              href="/scan"
              className="action-outline"
              style={{ width: "100%", padding: "11px", textAlign: "center", fontSize: ".88rem", boxSizing: "border-box" }}
            >
              📷 {lang === "vi" ? "Quét tem QR khác" : "Scan Another QR"}
            </Link>
          </div>
        </div>
      </div>
    </div>
  );
}
