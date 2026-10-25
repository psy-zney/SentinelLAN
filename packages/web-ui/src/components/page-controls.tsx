import { useTranslation } from "@/lib/i18n";

export function PageControls({ offset, count, pageSize = 100, onChange }: {
  offset: number; count: number; pageSize?: number; onChange: (offset: number) => void;
}) {
  const { lang } = useTranslation();
  return <nav className="support-inline-actions" aria-label={lang === "vi" ? "Chuyển trang danh sách" : "List pages"}>
    <button type="button" className="action-outline" disabled={offset === 0} onClick={() => onChange(Math.max(0, offset - pageSize))}>
      {lang === "vi" ? "Trang trước" : "Previous"}
    </button>
    <span aria-live="polite">{lang === "vi" ? "Trang" : "Page"} {Math.floor(offset / pageSize) + 1}</span>
    <button type="button" className="action-outline" disabled={count < pageSize} onClick={() => onChange(offset + pageSize)}>
      {lang === "vi" ? "Trang sau" : "Next"}
    </button>
  </nav>;
}
