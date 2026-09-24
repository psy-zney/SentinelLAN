"use client";

import { useEffect, useRef, useState } from "react";
import type { Route } from "next";
import { useRouter } from "next/navigation";
import { ApiClient } from "@/lib/api-client";
import { useTranslation } from "@/lib/i18n";
import { extractSentinelLanQrCode } from "@/lib/qr-code-parser";

type ScannerState = "ready" | "requesting" | "scanning" | "result" | "error";

type BarcodeDetectorInstance = {
  detect: (source: ImageBitmapSource) => Promise<Array<{ rawValue: string }>>;
};
type BarcodeDetectorCtor = new (options?: { formats: string[] }) => BarcodeDetectorInstance;

function getBarcodeDetector(): BarcodeDetectorCtor | null {
  if (typeof window !== "undefined" && "BarcodeDetector" in window) {
    return (window as unknown as { BarcodeDetector: BarcodeDetectorCtor }).BarcodeDetector;
  }
  return null;
}

export function QrScannerView() {
  const { t, lang } = useTranslation();
  const router = useRouter();

  const [scannerState, setScannerState] = useState<ScannerState>("ready");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [manualCode, setManualCode] = useState("");
  const [facingMode, setFacingMode] = useState<"environment" | "user">("environment");
  const [hasBarcodeDetector] = useState<boolean>(() => typeof window !== "undefined" && "BarcodeDetector" in window);
  const [detectedCode, setDetectedCode] = useState<string | null>(null);
  const [resolving, setResolving] = useState(false);

  const videoRef = useRef<HTMLVideoElement | null>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const fallbackControlsRef = useRef<{ stop: () => void } | null>(null);
  const animationFrameRef = useRef<number | null>(null);

  // Clean up camera stream on unmount
  useEffect(() => {
    return () => {
      stopCamera();
    };
  }, []);

  function stopCamera() {
    fallbackControlsRef.current?.stop();
    fallbackControlsRef.current = null;
    if (animationFrameRef.current) {
      cancelAnimationFrame(animationFrameRef.current);
      animationFrameRef.current = null;
    }
    if (streamRef.current) {
      streamRef.current.getTracks().forEach((track) => track.stop());
      streamRef.current = null;
    }
    if (videoRef.current) {
      videoRef.current.srcObject = null;
    }
  }

  async function startCamera(mode = facingMode) {
    setErrorMessage(null);
    setScannerState("requesting");
    stopCamera();

    try {
      if (!navigator?.mediaDevices?.getUserMedia) {
        throw new Error("getUserMedia is not supported on this device/context");
      }

      if (!getBarcodeDetector()) {
        if (!videoRef.current) throw new Error("Camera preview is not ready");
        const { BrowserQRCodeReader } = await import("@zxing/browser");
        const reader = new BrowserQRCodeReader(undefined, { delayBetweenScanAttempts: 180 });
        const controls = await reader.decodeFromConstraints(
          { video: { facingMode: mode, width: { ideal: 1280 }, height: { ideal: 720 } } },
          videoRef.current,
          (result) => {
            if (result) void handleCodeScanned(result.getText());
          }
        );
        fallbackControlsRef.current = controls;
        setScannerState("scanning");
        return;
      }

      const stream = await navigator.mediaDevices.getUserMedia({
        video: {
          facingMode: mode,
          width: { ideal: 1280 },
          height: { ideal: 720 }
        }
      });

      streamRef.current = stream;
      if (videoRef.current) {
        videoRef.current.srcObject = stream;
        await videoRef.current.play();
        setScannerState("scanning");
        startScanLoop();
      }
    } catch (err: unknown) {
      stopCamera();
      setScannerState("error");
      const isDenied = err instanceof Error && (err.name === "NotAllowedError" || err.name === "PermissionDeniedError");
      const errDetail = err instanceof Error ? err.message : "unknown";
      setErrorMessage(
        isDenied
          ? t("cameraDeniedHelp")
          : `${lang === "vi" ? "Không thể truy cập camera: " : "Camera error: "}${errDetail}`
      );
    }
  }

  function startScanLoop() {
    const DetectorClass = getBarcodeDetector();
    if (!DetectorClass) {
      return;
    }

    const detector = new DetectorClass({ formats: ["qr_code"] });

    async function tick() {
      if (videoRef.current && videoRef.current.readyState >= 2) {
        try {
          const barcodes = await detector.detect(videoRef.current);
          if (barcodes.length > 0) {
            const rawValue = barcodes[0].rawValue;
            if (rawValue) {
              handleCodeScanned(rawValue);
              return; // Exit loop on scan
            }
          }
        } catch {
          // Ignore single frame detect errors
        }
      }
      animationFrameRef.current = requestAnimationFrame(tick);
    }

    animationFrameRef.current = requestAnimationFrame(tick);
  }

  async function handleCodeScanned(text: string) {
    stopCamera();
    const code = extractSentinelLanQrCode(text, window.location.origin);
    if (!code) {
      setDetectedCode(null);
      setScannerState("error");
      setErrorMessage(lang === "vi" ? "Mã QR không thuộc SentinelLAN hoặc có định dạng không hợp lệ." : "This QR code is not a valid SentinelLAN code.");
      return;
    }
    setDetectedCode(code);
    setScannerState("result");
    await handleResolve(code);
  }

  async function handleResolve(code: string) {
    if (!code) return;
    setResolving(true);
    setErrorMessage(null);

    try {
      // Try authenticated resolve first
      const authResolve = await new ApiClient().resolveQr(code);
      if (authResolve?.authorized && authResolve.nextRoute) {
        router.push(authResolve.nextRoute as Route);
        return;
      }
    } catch {
      // Unauthenticated or not found -> redirect to public QR portal
    }

    // Default route: public QR view
    router.push(`/qr/${encodeURIComponent(code)}` as Route);
  }

  async function handleImageUpload(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;

    setErrorMessage(null);

    try {
      const DetectorClass = getBarcodeDetector();
      if (DetectorClass) {
        const detector = new DetectorClass({ formats: ["qr_code"] });
        const imageBitmap = await createImageBitmap(file);
        try {
          const barcodes = await detector.detect(imageBitmap);
          if (barcodes.length > 0 && barcodes[0].rawValue) {
            await handleCodeScanned(barcodes[0].rawValue);
            return;
          }
        } finally {
          imageBitmap.close();
        }
      }

      const { BrowserQRCodeReader } = await import("@zxing/browser");
      const objectUrl = URL.createObjectURL(file);
      try {
        const result = await new BrowserQRCodeReader().decodeFromImageUrl(objectUrl);
        await handleCodeScanned(result.getText());
        return;
      } finally {
        URL.revokeObjectURL(objectUrl);
      }
    } catch {
      setErrorMessage(lang === "vi" ? "Không tìm thấy mã QR hợp lệ trong ảnh đã chọn." : "No valid QR code was found in the selected image.");
    }
  }

  function handleManualSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!manualCode.trim()) return;
    const code = extractSentinelLanQrCode(manualCode, window.location.origin);
    if (!code) {
      setScannerState("error");
      setErrorMessage(lang === "vi" ? "Mã nhập không hợp lệ hoặc URL không thuộc hệ thống này." : "The code is invalid or the URL belongs to another origin.");
      return;
    }
    void handleCodeScanned(code);
  }

  function toggleFacingMode() {
    const nextMode = facingMode === "environment" ? "user" : "environment";
    setFacingMode(nextMode);
    if (scannerState === "scanning") {
      startCamera(nextMode);
    }
  }

  return (
    <div style={{ maxWidth: 540, margin: "0 auto", padding: "16px 8px" }}>
      <div className="panel" style={{ padding: 24, boxShadow: "0 15px 35px rgba(20, 55, 50, .08)" }}>
        <div className="panel-head" style={{ marginBottom: 12 }}>
          <div>
            <h1 style={{ fontSize: "1.35rem", margin: 0 }}>📷 {t("scanTitle")}</h1>
            <p className="subtitle" style={{ fontSize: ".82rem", margin: "4px 0 0" }}>{t("scanSubtitle")}</p>
          </div>
        </div>

        {/* Camera Viewport / Stage */}
        <div
          style={{
            position: "relative",
            width: "100%",
            aspectRatio: "4/3",
            background: "#102a2a",
            borderRadius: 14,
            overflow: "hidden",
            display: "grid",
            placeItems: "center",
            margin: "16px 0"
          }}
        >
          <video
            ref={videoRef}
            playsInline
            muted
            style={{
              width: "100%",
              height: "100%",
              objectFit: "cover",
              display: scannerState === "scanning" ? "block" : "none"
            }}
          />

          {/* Aiming Reticle Overlay when scanning */}
          {scannerState === "scanning" && (
            <div
              style={{
                position: "absolute",
                width: 220,
                height: 220,
                border: "3px solid var(--accent)",
                borderRadius: 18,
                boxShadow: "0 0 0 4000px rgba(0, 0, 0, 0.45)",
                pointerEvents: "none",
                display: "grid",
                placeItems: "center"
              }}
            >
              <div
                style={{
                  width: "100%",
                  height: 2,
                  background: "var(--accent)",
                  boxShadow: "0 0 10px var(--accent)",
                  animation: "scanLine 2s infinite ease-in-out"
                }}
              />
            </div>
          )}

          {/* Idle / Permission States */}
          {scannerState === "ready" && (
            <div style={{ textAlign: "center", color: "#e0edea", padding: 20 }}>
              <div style={{ fontSize: "3rem", marginBottom: 10 }}>📷</div>
              <strong style={{ fontSize: "1.05rem" }}>{t("cameraReady")}</strong>
              <p style={{ fontSize: ".82rem", color: "#a0b8b2", maxWidth: 280, margin: "6px auto 16px" }}>
                {lang === "vi" ? "Nhấn nút dưới để bật camera quét tem mã QR dán trên thân máy." : "Click below to start scanning computer asset QR tags."}
              </p>
              <button
                type="button"
                className="action"
                onClick={() => startCamera(facingMode)}
              >
                ▶ {t("startCamera")}
              </button>
            </div>
          )}

          {scannerState === "requesting" && (
            <div style={{ textAlign: "center", color: "#e0edea", padding: 20 }}>
              <div style={{ fontSize: "2.4rem", marginBottom: 10 }}>⏳</div>
              <p style={{ fontSize: ".9rem" }}>{t("requestingPermission")}</p>
            </div>
          )}

          {scannerState === "result" && (
            <div style={{ textAlign: "center", color: "#ffffff", padding: 20 }}>
              <div style={{ fontSize: "2.5rem", marginBottom: 8 }}>✓</div>
              <strong>{t("scanResult")}</strong>
              <p style={{ fontFamily: "monospace", fontSize: "1rem", color: "var(--accent)", marginTop: 6 }}>
                {detectedCode}
              </p>
              {resolving && (
                <p style={{ fontSize: ".82rem", color: "#a0b8b2" }}>{t("verifyingQr")}</p>
              )}
            </div>
          )}

          {scannerState === "error" && (
            <div style={{ textAlign: "center", color: "#ffffff", padding: 20, maxWidth: 360 }}>
              <div style={{ fontSize: "2.5rem", marginBottom: 8 }}>⚠️</div>
              <strong style={{ fontSize: ".95rem", color: "#ff8c82" }}>{lang === "vi" ? "Camera chưa khả dụng" : "Camera unavailable"}</strong>
              <p style={{ fontSize: ".78rem", color: "#cad9d4", marginTop: 8 }}>{errorMessage}</p>
              <button
                type="button"
                className="action-outline"
                style={{ color: "#ffffff", borderColor: "#cad9d4", marginTop: 12 }}
                onClick={() => startCamera(facingMode)}
              >
                🔄 {t("retry")}
              </button>
            </div>
          )}
        </div>

        {/* Camera Control Bar */}
        {scannerState === "scanning" && (
          <div style={{ display: "flex", gap: 10, justifyContent: "center", marginBottom: 16 }}>
            <button
              type="button"
              className="action-outline"
              onClick={toggleFacingMode}
              style={{ fontSize: ".82rem" }}
            >
              🔄 {t("switchCamera")}
            </button>
            <button
              type="button"
              className="action-outline"
              onClick={stopCamera}
              style={{ fontSize: ".82rem", color: "var(--danger)", borderColor: "var(--danger)" }}
            >
              ⏹ {t("stopCamera")}
            </button>
          </div>
        )}

        {/* Fallback 1: File Upload */}
        <div style={{ borderTop: "1px solid #e7eeeb", paddingTop: 16, marginTop: 12 }}>
          <label style={{ fontSize: ".82rem", fontWeight: 700, color: "var(--ink)", display: "block", marginBottom: 6 }}>
            📁 {t("uploadImage")}
          </label>
          <input
            type="file"
            accept="image/*"
            className="form-input"
            style={{ fontSize: ".82rem" }}
            onChange={handleImageUpload}
          />
        </div>

        {/* Fallback 2: Manual Code Input */}
        <form onSubmit={handleManualSubmit} style={{ borderTop: "1px solid #e7eeeb", paddingTop: 16, marginTop: 16 }}>
          <label htmlFor="manual-code" style={{ fontSize: ".82rem", fontWeight: 700, color: "var(--ink)", display: "block", marginBottom: 6 }}>
            ⌨️ {t("manualCodeInput")}
          </label>
          <div style={{ display: "flex", gap: 8 }}>
            <input
              id="manual-code"
              className="form-input"
              placeholder="VD: demo-qr-asset-employee-pc-2026 hoặc mã QR..."
              value={manualCode}
              onChange={(e) => setManualCode(e.target.value)}
              style={{ fontSize: ".85rem" }}
            />
            <button
              type="submit"
              className="action"
              disabled={!manualCode.trim() || resolving}
              style={{ whiteSpace: "nowrap", fontSize: ".82rem" }}
            >
              {resolving ? "..." : t("submitCode")}
            </button>
          </div>
        </form>

        {!hasBarcodeDetector && (
          <p className="subtitle" style={{ fontSize: ".76rem", margin: "14px 0 0", color: "var(--muted)" }}>
            ℹ️ {t("unsupportedBarcodeDetector")}
          </p>
        )}
      </div>
    </div>
  );
}
