"use client";

import { DeviceTable } from "@/components/device-table";
import { useLiveQuery } from "@/hooks/use-live-query";
import { ApiClient } from "@/lib/api-client";
import { useTranslation } from "@/lib/i18n";

const loadDevices = () => new ApiClient().devices();

export function DevicesView() {
  const { t } = useTranslation();
  const { data: devices, error, refresh } = useLiveQuery(loadDevices);

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

  if (!devices) return <p role="status">{t("loading")}</p>;

  if (!devices.length) {
    return (
      <div className="panel empty-state">
        <p>{t("noTelemetry")}</p>
      </div>
    );
  }

  return <DeviceTable initialDevices={devices} />;
}
