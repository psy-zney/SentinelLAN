"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { ApiClient, ApiError } from "@/lib/api-client";
import { useTranslation } from "@/lib/i18n";

export function ActivateView() {
  const { t, lang, setLang } = useTranslation();
  const searchParams = useSearchParams();
  const queryToken = searchParams.get("token") ?? "";

  const [token, setToken] = useState(queryToken);
  const [validating, setValidating] = useState(true);
  const [tokenInfo, setTokenInfo] = useState<{
    valid: boolean;
    email?: string;
    displayName?: string;
    organizationName?: string;
    message?: string;
  } | null>(() => (!token ? { valid: false } : null));

  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  useEffect(() => {
    let active = true;
    const fragmentToken = new URLSearchParams(window.location.hash.slice(1)).get("token") ?? "";
    const activationToken = queryToken || fragmentToken;
    if (window.location.search || window.location.hash) {
      window.history.replaceState(null, "", window.location.pathname);
    }
    // Read the browser-only fragment after hydration, then update UI state asynchronously.
    queueMicrotask(() => {
      if (!active) return;
      setToken(activationToken);
      if (!activationToken) {
        setTokenInfo({ valid: false });
        setValidating(false);
      }
    });
    return () => { active = false; };
  }, [queryToken]);

  useEffect(() => {
    if (!token) return;

    let active = true;
    new ApiClient()
      .validateActivationToken(token)
      .then((res) => {
        if (active) {
          setTokenInfo(res);
          setValidating(false);
        }
      })
      .catch(() => {
        if (active) {
          setTokenInfo({ valid: false });
          setValidating(false);
        }
      });

    return () => {
      active = false;
    };
  }, [token, lang]);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setErrorMessage(null);

    if (password.length < 12) {
      setErrorMessage(t("passwordMinLength"));
      return;
    }

    if (password !== confirmPassword) {
      setErrorMessage(t("passwordMismatch"));
      return;
    }

    setSubmitting(true);
    try {
      await new ApiClient().activateAccount({
        token,
        password,
        confirmPassword
      });
      setSuccess(true);
    } catch (cause) {
      setErrorMessage(
        cause instanceof ApiError && cause.status === 400
          ? lang === "vi" ? "Token không hợp lệ, đã được dùng hoặc đã hết hạn." : "Token invalid, already used, or expired."
          : t("activationFailed")
      );
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div style={{ minHeight: "100vh", display: "grid", placeItems: "center", background: "radial-gradient(circle at 50% 0%, #d6ece6 0, transparent 24rem), #f4f7f5", padding: "20px 16px" }}>
      <div style={{ width: "100%", maxWidth: 440 }}>
        {/* Brand header */}
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 20 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
            <div style={{ width: 32, height: 36, border: "2px solid #0b6b5f", borderRadius: "12px 12px 8px 8px", display: "grid", placeItems: "center", color: "#0b6b5f", fontWeight: 800 }}>
              S
            </div>
            <div>
              <strong style={{ fontSize: "1.1rem" }}>SentinelLAN</strong>
              <div style={{ fontSize: ".72rem", color: "var(--muted)" }}>Secure Endpoint Governance</div>
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

        <div className="panel" style={{ padding: 28, boxShadow: "0 20px 45px rgba(20, 55, 50, .08)" }}>
          {validating ? (
            <div style={{ textAlign: "center", padding: "24px 0" }}>
              <p className="subtitle">{lang === "vi" ? "Đang xác thực liên kết kích hoạt..." : "Validating activation link..."}</p>
            </div>
          ) : success ? (
            <div style={{ textAlign: "center" }}>
              <div style={{ fontSize: "2.8rem", marginBottom: 12 }}>🎉</div>
              <h2 style={{ fontSize: "1.35rem", margin: "0 0 8px" }}>{t("activationSuccess")}</h2>
              <p className="subtitle" style={{ fontSize: ".88rem", marginBottom: 20 }}>
                {lang === "vi"
                  ? "Mật khẩu của bạn đã được thiết lập thành công. Giờ đây bạn có thể đăng nhập vào hệ thống."
                  : "Your password has been successfully established. You may now sign in to SentinelLAN."}
              </p>
              <Link href="/login" className="action" style={{ display: "inline-block", width: "100%", padding: "12px", textAlign: "center", boxSizing: "border-box" }}>
                {t("goToLogin")}
              </Link>
            </div>
          ) : !tokenInfo?.valid ? (
            <div style={{ textAlign: "center" }}>
              <div style={{ fontSize: "2.8rem", marginBottom: 12 }}>⚠️</div>
              <h2 style={{ fontSize: "1.35rem", margin: "0 0 8px" }}>{t("tokenInvalidOrExpired")}</h2>
              <p className="subtitle" style={{ fontSize: ".88rem", marginBottom: 20 }}>
                {lang === "vi"
                  ? "Liên kết kích hoạt này có thể đã được sử dụng trước đó, đã bị thu hồi hoặc đã quá thời hạn 24 giờ. Vui lòng liên hệ Quản trị viên IT để được cấp lại liên kết mới."
                  : "This activation link may have already been used, was revoked, or has expired. Please contact your IT Administrator to reissue an activation link."}
              </p>
              <Link href="/login" className="action-outline" style={{ display: "inline-block", width: "100%", padding: "12px", textAlign: "center", boxSizing: "border-box" }}>
                {t("goToLogin")}
              </Link>
            </div>
          ) : (
            <div>
              <h1 style={{ fontSize: "1.35rem", margin: "0 0 6px" }}>{t("activationTitle")}</h1>
              <p className="subtitle" style={{ fontSize: ".84rem", margin: "0 0 18px" }}>
                {t("activationSubtitle")}
              </p>

              {tokenInfo.organizationName && (
                <div style={{ background: "#f6faf8", padding: "10px 14px", borderRadius: 8, border: "1px solid #dbe6e2", marginBottom: 16 }}>
                  <div style={{ fontSize: ".74rem", color: "var(--muted)", textTransform: "uppercase", fontWeight: 700 }}>
                    {lang === "vi" ? "Tổ chức & Tài khoản" : "Organization & Account"}
                  </div>
                  <strong style={{ fontSize: ".92rem", color: "var(--ink)" }}>{tokenInfo.displayName ?? tokenInfo.email}</strong>
                  <div style={{ fontSize: ".78rem", color: "var(--muted)" }}>{tokenInfo.email} · {tokenInfo.organizationName}</div>
                </div>
              )}

              {errorMessage && (
                <div className="panel" role="alert" style={{ background: "#fff2f0", borderColor: "#ffccc7", padding: 12, marginBottom: 16 }}>
                  <span style={{ color: "var(--danger)", fontSize: ".84rem" }}>{errorMessage}</span>
                </div>
              )}

              <form onSubmit={handleSubmit}>
                <div className="form-group">
                  <label htmlFor="new-password">{t("newPassword")}</label>
                  <input
                    id="new-password"
                    type="password"
                    className="form-input"
                    required
                    minLength={12}
                    maxLength={128}
                    autoComplete="new-password"
                    placeholder="Tối thiểu 12 ký tự..."
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                  />
                  <small style={{ color: "var(--muted)", fontSize: ".74rem" }}>
                    {t("passwordMinLength")}
                  </small>
                </div>

                <div className="form-group">
                  <label htmlFor="confirm-password">{t("confirmNewPassword")}</label>
                  <input
                    id="confirm-password"
                    type="password"
                    className="form-input"
                    required
                    minLength={12}
                    maxLength={128}
                    autoComplete="new-password"
                    value={confirmPassword}
                    onChange={(e) => setConfirmPassword(e.target.value)}
                  />
                </div>

                <div style={{ marginTop: 22 }}>
                  <button
                    type="submit"
                    className="action"
                    disabled={submitting || !password || !confirmPassword}
                    style={{ width: "100%", padding: "12px", fontSize: ".92rem" }}
                  >
                    {submitting ? t("activatingAccount") : t("activateAccountBtn")}
                  </button>
                </div>
              </form>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
