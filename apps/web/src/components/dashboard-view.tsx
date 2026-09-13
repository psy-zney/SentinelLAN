"use client";

import { DeviceTable } from "@/components/device-table";
import { useLiveQuery } from "@/hooks/use-live-query";
import { ApiClient } from "@/lib/api-client";
import { useTranslation } from "@/lib/i18n";

const loadDashboard = () => new ApiClient().dashboard();

export function DashboardView() {
  const { t } = useTranslation();
  const { data, error, refresh } = useLiveQuery(loadDashboard);

  if (error) {
    return (
      <div role="alert" className="panel">
        <p>{t("error")}</p>
        <button className="action" onClick={refresh}>
          {t("retry")}
        </button>
      </div>
    );
  }

  if (!data) return <p role="status">{t("loading")}</p>;

  return (
    <>
      <section className="metrics" aria-label="Device summary">
        <div className="metric">
          <span>{t("totalDevices")}</span>
          <strong>{data.totalDevices}</strong>
        </div>
        <div className="metric">
          <span>{t("onlineDevices")}</span>
          <strong style={{ color: "var(--accent)" }}>{data.onlineDevices}</strong>
        </div>
        <div className="metric">
          <span>{t("offlineDevices")}</span>
          <strong style={{ color: "var(--danger)" }}>{data.offlineDevices}</strong>
        </div>
        <div className="metric">
          <span>{t("openAlerts")}</span>
          <strong style={{ color: "var(--warn)" }}>{data.openAlerts}</strong>
        </div>
      </section>
      <DeviceTable initialDevices={data.devices} />
    </>
  );
}
