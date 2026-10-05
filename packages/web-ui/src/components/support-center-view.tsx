"use client";

import Image from "next/image";
import { useCallback, useEffect, useRef, useState } from "react";
import { useCurrentSession } from "@/components/app-shell";
import { ApiClient, ApiError } from "@/lib/api-client";
import { categoryNames, executionLabel, itStatusOptions, kindNames, statusNames, validateSupportImage } from "@/lib/self-service";
import { useSelfServiceQuery } from "@/hooks/use-self-service-query";
import type { CatalogApp, CreateSupportRequest, RequestKind, SaveAnnouncement, SaveCatalogApp, SupportCategory, SupportDecision, SupportRequest, SupportStatus } from "@/types/self-service";

const api = new ApiClient();
function errorMessage(cause: unknown) {
  if (cause instanceof ApiError && cause.status === 404) return "Chưa có máy tính được giao cho bạn, hoặc yêu cầu không còn khả dụng. Hãy liên hệ IT.";
  if (cause instanceof ApiError && cause.status === 403) return "Bạn chưa được phép thực hiện thao tác này.";
  if (cause instanceof ApiError && cause.status === 409) return "Yêu cầu đã thay đổi hoặc mã đã dùng/hết hạn. Hãy tải lại và liên hệ IT.";
  return cause instanceof Error ? cause.message : "Chưa thực hiện được. Kiểm tra mạng rồi thử lại.";
}
function when(value: string) { return new Date(value).toLocaleString("vi-VN"); }

export function SupportCenterView({ initialRequestId = "" }: { initialRequestId?: string }) {
  const session = useCurrentSession();
  const employee = session?.role === "Employee";
  const admin = session?.role === "Admin";
  const load = useCallback(async () => {
    const [requests, catalog, announcements, notifications, help] = await Promise.all([
      api.supportRequests(), api.catalogApps(), api.announcements(), api.employeeNotifications(), api.helpArticles()
    ]);
    return { requests, catalog, announcements, notifications, help };
  }, []);
  const { data, error, refresh } = useSelfServiceQuery(load);
  const [selected, setSelected] = useState(initialRequestId);
  const [createKind, setCreateKind] = useState<RequestKind | null>(null);
  const [requestedAppId, setRequestedAppId] = useState("");
  const [category, setCategory] = useState<SupportCategory>("Network");
  const [tab, setTab] = useState<"requests" | "catalog" | "help" | "notifications" | "announcements">("requests");
  const [filter, setFilter] = useState("active");
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState("");
  async function mutate(operation: () => Promise<unknown>) {
    if (busy) return;
    setBusy(true); setNotice("");
    try { await operation(); await refresh(); } catch (cause) { setNotice(errorMessage(cause)); }
    finally { setBusy(false); }
  }
  const visibleRequests = data?.requests.filter(request => filter !== "active" || !["Closed", "Rejected"].includes(request.status));
  return <div className="support-center">
    {employee && <section className="support-actions" aria-label="Bạn cần IT giúp gì?">
      <button type="button" className="panel support-shortcut" onClick={() => { setCreateKind("Incident"); setTab("requests"); }}><strong>Tôi gặp sự cố</strong><span>Báo lỗi hoặc hẹn IT hỗ trợ</span></button>
      <button type="button" className="panel support-shortcut" onClick={() => setTab("catalog")}><strong>Tôi cần phần mềm</strong><span>Chọn ứng dụng được công ty cho phép</span></button>
      <button type="button" className="panel support-shortcut" onClick={() => { setTab("requests"); setCreateKind(null); }}><strong>Yêu cầu của tôi</strong><span>Xem phản hồi và trao đổi với IT</span></button>
    </section>}
    {error && <div role="alert" className="privacy"><p>{error}</p><button type="button" className="action" onClick={() => void refresh()}>Thử kết nối lại</button></div>}
    {notice && <p role="alert" className="privacy">{notice}</p>}
    {!data && !error && <p role="status">Đang tải yêu cầu…</p>}
    {data?.announcements.filter(item => item.isOutage).map(item => <section key={item.id} className="panel support-outage" aria-label="Sự cố chung">
      <h2>{item.title}</h2><p>{item.body}</p><p className="subtitle">{when(item.startsAt)} — {when(item.endsAt)}</p>
      {employee && <button type="button" className="action-outline" disabled={busy || item.affected} onClick={() => void mutate(() => api.markAnnouncementAffected(item.id))}>{item.affected ? "IT đã ghi nhận bạn bị ảnh hưởng" : "Tôi cũng bị ảnh hưởng"}</button>}
    </section>)}
    <nav className="support-tabs" aria-label="Nội dung hỗ trợ">
      {([ ["requests", employee ? "Yêu cầu của tôi" : "Yêu cầu hỗ trợ"], ["catalog", "Phần mềm"], ["help", "Hướng dẫn nhanh"], ["notifications", `Thông báo (${data?.notifications.filter(item => !item.readAt).length ?? 0})`], ["announcements", "Thông báo công ty"] ] as const).map(([value, label]) => <button key={value} type="button" className={tab === value ? "action" : "action-outline"} aria-pressed={tab === value} onClick={() => setTab(value)}>{label}</button>)}
    </nav>
    {employee && <div className="support-inline-actions">
      <button type="button" className="action-danger" onClick={() => { setCategory("Suspicious"); setCreateKind("Panic"); setTab("requests"); }}>Tôi nghi máy bị nhiễm virus</button>
      <button type="button" className="action-outline" onClick={() => { setCreateKind("Appointment"); setTab("requests"); }}>Chọn giờ IT hỗ trợ</button>
      <details><summary>Tạm dừng hoặc gỡ SentinelLAN</summary><p>Cần IT duyệt và cấp mã xác nhận riêng cho máy của bạn.</p><div className="support-inline-actions"><button type="button" className="action-outline" onClick={() => { setCreateKind("PauseAgent"); setTab("requests"); }}>Xin tạm dừng 15 phút</button><button type="button" className="action-outline" onClick={() => { setCreateKind("UninstallAgent"); setTab("requests"); }}>Xin gỡ SentinelLAN</button></div></details>
    </div>}
    {tab === "requests" && <>
      {employee && createKind && <CreateRequestForm key={`${createKind}-${category}-${requestedAppId}`} kind={createKind} initialCategory={category} catalog={data?.catalog ?? []} initialCatalogId={requestedAppId} onCancel={() => setCreateKind(null)} onCreated={value => { setSelected(value.id); setCreateKind(null); void refresh(); }} />}
      <div className="support-columns">
        <section className="panel" aria-label="Danh sách yêu cầu">
          <div className="panel-head"><h2>{employee ? "Yêu cầu của tôi" : "Yêu cầu từ nhân viên"}</h2><label>Hiển thị <select value={filter} onChange={event => setFilter(event.target.value)}><option value="active">Đang xử lý</option><option value="all">Tất cả</option></select></label></div>
          {visibleRequests?.length === 0 && <p>Chưa có yêu cầu trong danh sách này.</p>}
          <ul className="support-request-list">{visibleRequests?.map(request => <li key={request.id}><button type="button" className="support-request" aria-pressed={selected === request.id} onClick={() => setSelected(request.id)}>
            <strong>{request.title}</strong><span>{statusNames[request.status]} · {kindNames[request.kind]}</span><small>{employee ? request.deviceName : `${request.userName} · ${request.deviceName}`} · {when(request.createdAt)}</small>
            {!request.canWork && <span className="badge badge-danger">Chưa thể tiếp tục làm việc</span>}
          </button></li>)}</ul>
        </section>
        {selected ? <RequestDetail key={selected} id={selected} employee={employee} admin={admin} onChanged={() => void refresh()} /> : <section className="panel"><h2>{employee ? "Trao đổi với IT" : "Chi tiết yêu cầu"}</h2><p>Chọn một yêu cầu để xem người phụ trách, gửi ảnh và nhận phản hồi.</p></section>}
      </div>
    </>}
    {tab === "catalog" && <section className="panel"><h2>Phần mềm công ty cho phép</h2><div className="support-catalog">{data?.catalog.map(app => <article key={app.id} className="support-card"><h3>{app.name}</h3><p>{app.description}</p><p className="subtitle">Phiên bản {app.version} · {app.isActive ? "Có thể yêu cầu" : "Đang tạm ngưng"}</p>{employee ? <div className="support-inline-actions"><button type="button" className="action" disabled={!app.isActive} onClick={() => { setRequestedAppId(app.id); setCreateKind("InstallApp"); setTab("requests"); }}>Yêu cầu cài đặt</button><button type="button" className="action-outline" disabled={!app.isActive} onClick={() => { setRequestedAppId(app.id); setCreateKind("Privilege"); setTab("requests"); }}>Cài đặt cần quyền IT</button></div> : admin && <CatalogEditor app={app} onSaved={() => void refresh()} />}</article>)}</div>{data?.catalog.length === 0 && <p>IT chưa công bố phần mềm. Bạn có thể báo nhu cầu qua yêu cầu hỗ trợ.</p>}{admin && <CatalogEditor onSaved={() => void refresh()} />}</section>}
    {tab === "help" && <section className="panel"><h2>Hướng dẫn nhanh</h2>{data?.help.map(article => <details key={article.id} className="support-card"><summary>{article.title}</summary><ol>{article.steps.map((step, index) => <li key={index}>{step}</li>)}</ol>{employee && <button type="button" className="action" onClick={() => { setCategory(article.category); setCreateKind("Incident"); setTab("requests"); }}>Vẫn chưa được — nhờ IT</button>}</details>)}</section>}
    {tab === "notifications" && <section className="panel"><h2>Thông báo của bạn</h2>{data?.notifications.length === 0 && <p>Chưa có thông báo mới.</p>}{data?.notifications.map(item => <article key={item.id} className="support-card"><h3>{item.title}</h3><p>{item.body}</p><small>{when(item.createdAt)}</small><div className="support-inline-actions">{item.requestId && <button type="button" className="action-outline" onClick={() => { setSelected(item.requestId!); setTab("requests"); }}>Xem yêu cầu</button>}<button type="button" className="action-outline" disabled={busy || !!item.readAt} onClick={() => void mutate(() => api.readEmployeeNotification(item.id))}>{item.readAt ? "Đã đọc" : "Đánh dấu đã đọc"}</button></div></article>)}</section>}
    {tab === "announcements" && <section className="panel"><h2>Thông báo công ty</h2>{!employee && <AnnouncementEditor onSaved={() => void refresh()} />}{data?.announcements.map(item => <article key={item.id} className="support-card"><h3>{item.title}</h3><p>{item.body}</p><p className="subtitle">{when(item.startsAt)} — {when(item.endsAt)}</p>{item.requiresAcknowledgement && <button type="button" className="action-outline" disabled={busy || item.acknowledged} onClick={() => void mutate(() => api.acknowledgeAnnouncement(item.id))}>{item.acknowledged ? "Đã xác nhận đọc" : "Tôi đã đọc"}</button>}</article>)}</section>}
  </div>;
}

function CreateRequestForm({ kind, initialCategory, catalog, initialCatalogId = "", onCancel, onCreated }: { kind: RequestKind; initialCategory: SupportCategory; catalog: CatalogApp[]; initialCatalogId?: string; onCancel: () => void; onCreated: (request: SupportRequest) => void }) {
  const [category, setCategory] = useState(initialCategory);
  const [title, setTitle] = useState(""); const [description, setDescription] = useState("");
  const [canWork, setCanWork] = useState(true); const [catalogId, setCatalogId] = useState(initialCatalogId);
  const [appointment, setAppointment] = useState(""); const [confirmed, setConfirmed] = useState(false);
  const [busy, setBusy] = useState(false); const [error, setError] = useState("");
  const nonce = useRef<string | null>(null); const payload = useRef("");
  const sensitive = ["Panic", "PauseAgent", "UninstallAgent", "Privilege", "InstallApp"].includes(kind);
  async function submit(event: React.FormEvent) {
    event.preventDefault(); if (busy || (sensitive && !confirmed)) return;
    setBusy(true); setError("");
    try {
      const data: Omit<CreateSupportRequest, "idempotencyKey"> = { kind, category: kind === "Panic" ? "Suspicious" : category, title: title.trim() || `${kindNames[kind]}: ${categoryNames[category]}`, description: description.trim() || undefined, canWork: kind === "Panic" ? false : canWork, catalogAppId: catalogId || undefined, appointmentAt: appointment ? new Date(appointment).toISOString() : undefined, confirmed: sensitive ? confirmed : true };
      const fingerprint = JSON.stringify(data);
      if (payload.current !== fingerprint) { nonce.current = crypto.randomUUID(); payload.current = fingerprint; }
      onCreated(await api.createSupportRequest({ ...data, idempotencyKey: nonce.current! }));
    } catch (cause) { setError(errorMessage(cause)); } finally { setBusy(false); }
  }
  return <form className="panel support-form" onSubmit={event => void submit(event)} aria-label={kindNames[kind]}>
    <h2>{kindNames[kind]}</h2>
    {kind === "Panic" && <p className="privacy">Yêu cầu sẽ báo khẩn cấp cho IT. Nếu máy đủ điều kiện lab, hệ thống gửi lệnh tạm ngắt mạng. Hãy đợi xác nhận từ máy; gửi yêu cầu chưa có nghĩa là đã cô lập.</p>}
    {kind === "PauseAgent" && <p>Tạm dừng các tác vụ trong 15 phút, sau đó tự hoạt động lại. Kết nối trạng thái vẫn được duy trì để IT hỗ trợ.</p>}
    {kind === "UninstallAgent" && <p>Gỡ SentinelLAN cần IT duyệt và mã xác nhận một lần. Sau khi gỡ, máy không còn nhận hỗ trợ qua Agent.</p>}
    {kind === "Privilege" && <p>IT duyệt quyền cài đặt cho ứng dụng đã chọn, với thời hạn tối đa 30 phút.</p>}
    <label className="field">Bạn gặp vấn đề gì?<select className="form-select" value={category} onChange={event => setCategory(event.target.value as SupportCategory)}>{Object.entries(categoryNames).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
    <label className="field">Bạn có tiếp tục làm việc được không?<select className="form-select" value={String(canWork)} onChange={event => setCanWork(event.target.value === "true")}><option value="true">Có, nhưng cần hỗ trợ</option><option value="false">Không, công việc đang bị gián đoạn</option></select></label>
    {["InstallApp", "Privilege"].includes(kind) && <label className="field">Phần mềm cần cài<select required className="form-select" value={catalogId} onChange={event => setCatalogId(event.target.value)}><option value="">Chọn phần mềm</option>{catalog.filter(app => app.isActive).map(app => <option key={app.id} value={app.id}>{app.name} ({app.version})</option>)}</select></label>}
    {kind === "Appointment" && <label className="field">Giờ bạn muốn IT liên hệ<input required type="datetime-local" value={appointment} onChange={event => setAppointment(event.target.value)} /></label>}
    <label className="field">Mô tả ngắn (không bắt buộc)<input maxLength={200} value={title} onChange={event => setTitle(event.target.value)} placeholder="Ví dụ: Máy in phòng kế toán không in" /></label>
    <label className="field">Nội dung thêm / lý do yêu cầu<textarea className="form-input" maxLength={4000} rows={3} value={description} onChange={event => setDescription(event.target.value)} /></label>
    {sensitive && <label><input type="checkbox" checked={confirmed} onChange={event => setConfirmed(event.target.checked)} /> Tôi hiểu thao tác và xác nhận gửi yêu cầu cho máy được giao cho tôi.</label>}
    {error && <p role="alert">{error}</p>}
    <div className="support-inline-actions"><button type="submit" className="action" disabled={busy || (sensitive && !confirmed)}>{busy ? "Đang gửi…" : "Gửi yêu cầu"}</button><button type="button" className="action-outline" disabled={busy} onClick={onCancel}>Hủy</button></div>
  </form>;
}

function RequestDetail({ id, employee, admin, onChanged }: { id: string; employee: boolean; admin: boolean; onChanged: () => void }) {
  const load = useCallback(async () => {
    const [request, messages, attachments, team] = await Promise.all([api.supportRequest(id), api.supportMessages(id), api.supportAttachments(id), employee ? Promise.resolve([]) : api.supportTeam()]);
    return { request, messages, attachments, team };
  }, [id, employee]);
  const { data, error, refresh } = useSelfServiceQuery(load, 5000);
  const [body, setBody] = useState(""); const [reason, setReason] = useState(""); const [status, setStatus] = useState<SupportStatus>("InProgress");
  const [assignee, setAssignee] = useState(""); const [confirmed, setConfirmed] = useState(false);
  const [code, setCode] = useState(""); const [decision, setDecision] = useState<SupportDecision | null>(null);
  const [busy, setBusy] = useState(false); const [notice, setNotice] = useState("");
  const messageKey = useRef<{ body: string; key: string } | null>(null);
  async function mutate(operation: () => Promise<unknown>) {
    if (busy) return; setBusy(true); setNotice("");
    try { await operation(); await refresh(); onChanged(); } catch (cause) { setNotice(errorMessage(cause)); }
    finally { setBusy(false); }
  }
  const request = data?.request;
  const execution = request ? executionLabel(request) : null;
  const availableStatuses = request ? itStatusOptions(request) : [];
  const selectedStatus = availableStatuses.includes(status) ? status : availableStatuses[0] ?? "Open";
  return <section className="panel" aria-label="Chi tiết yêu cầu">
    {error && <p role="alert">{error}</p>}{notice && <p role="alert">{notice}</p>}
    {!request ? <p role="status">Đang tải yêu cầu…</p> : <>
      <h2>{request.title}</h2><p><span className="badge badge-info">{statusNames[request.status]}</span> · {request.deviceName}</p>
      <p>{request.description}</p><p>Người phụ trách: <strong>{request.assignedTechnicianName ?? "IT đang tiếp nhận"}</strong></p>
      {request.appointmentAt && <p>Lịch hỗ trợ đề nghị: {when(request.appointmentAt)}</p>}
      {execution && <p role="status" className="privacy">{execution}</p>}
      {request.commandMessage && <details><summary>Chi tiết kết quả IT cần kiểm tra</summary><p>{request.commandMessage}</p></details>}
      {employee && ["AwaitingEmployee", "Resolved", "Closed"].includes(request.status) && <div className="support-inline-actions">
        <button type="button" className="action" disabled={busy || request.status === "Closed"} onClick={() => void mutate(() => api.updateSupportRequest(id, { status: "Closed", reason: "Nhân viên xác nhận đã dùng được", confirmed: true }))}>Đã dùng được</button>
        {["Incident", "Appointment", "Panic"].includes(request.kind) && <button type="button" className="action-outline" disabled={busy} onClick={() => void mutate(() => api.updateSupportRequest(id, { status: "Open", reason: "Nhân viên báo vẫn còn lỗi", confirmed: true }))}>Vẫn còn lỗi — nhờ IT kiểm tra lại</button>}
      </div>}
      {employee && request.status === "Approved" && !request.commandId && ["PauseAgent", "UninstallAgent"].includes(request.kind) && <form onSubmit={event => { event.preventDefault(); void mutate(async () => { await api.redeemMaintenanceCode(id, code, confirmed); setCode(""); setConfirmed(false); }); }} className="support-card">
        <label className="field">Mã xác nhận IT cấp<input inputMode="numeric" autoComplete="off" type="password" pattern="[0-9]{8}" maxLength={8} required value={code} onChange={event => setCode(event.target.value)} /></label>
        <label><input type="checkbox" checked={confirmed} onChange={event => setConfirmed(event.target.checked)} /> Tôi xác nhận {kindNames[request.kind].toLocaleLowerCase()} trên máy {request.deviceName}.</label>
        <div className="support-inline-actions"><button type="submit" className="action" disabled={busy || !confirmed}>Xác nhận bằng mã</button></div>
      </form>}
      {!employee && <div className="support-card"><h3>Xử lý yêu cầu</h3>
        <label className="field">Người phụ trách<select className="form-select" value={assignee} onChange={event => setAssignee(event.target.value)}><option value="">Giữ người phụ trách hiện tại</option>{data?.team.map(person => <option key={person.id} value={person.id}>{person.displayName}</option>)}</select></label>
        <label className="field">Trạng thái<select className="form-select" value={selectedStatus} onChange={event => setStatus(event.target.value as SupportStatus)}>{availableStatuses.map(value => <option key={value} value={value}>{statusNames[value]}</option>)}</select></label>
        <label className="field">Lý do / nội dung xử lý<textarea required className="form-input" rows={2} maxLength={1000} value={reason} onChange={event => setReason(event.target.value)} /></label>
        <label><input type="checkbox" checked={confirmed} onChange={event => setConfirmed(event.target.checked)} /> Tôi xác nhận thao tác cho yêu cầu và máy này.</label>
        <div className="support-inline-actions"><button type="button" className="action" disabled={busy || !confirmed || reason.trim().length < 3} onClick={() => void mutate(() => api.updateSupportRequest(id, { status: selectedStatus, assignedTechnicianId: assignee || undefined, reason, confirmed }))}>Cập nhật xử lý</button>
          {admin && request.status === "Open" && ["InstallApp", "Privilege", "PauseAgent", "UninstallAgent"].includes(request.kind) && <>
            <button type="button" className="action" disabled={busy || !confirmed || reason.trim().length < 3} onClick={() => void mutate(async () => { setDecision(await api.decideSupportRequest(id, { approved: true, reason, confirmed })); })}>Duyệt yêu cầu</button>
            <button type="button" className="action-danger" disabled={busy || !confirmed || reason.trim().length < 3} onClick={() => void mutate(() => api.decideSupportRequest(id, { approved: false, reason, confirmed }))}>Từ chối</button>
          </>}
        </div>
        {decision?.otp && <div className="privacy" role="status"><strong>Mã một lần: {decision.otp}</strong><p>Yêu cầu: {id}</p><p>Hết hạn: {decision.expiresAt ? when(decision.expiresAt) : "sau 5 phút"}. Chuyển riêng cho nhân viên sau khi xác minh; mã chỉ hiện tại đây lần này.</p><button type="button" className="action-outline" onClick={() => setDecision(null)}>Ẩn mã</button></div>}
      </div>}
      <h3>Trao đổi với IT</h3><p className="subtitle">Tin nhắn được lưu trong yêu cầu; IT có thể trả lời sau nếu đang bận.</p>
      <ol className="support-messages" aria-label="Cuộc trao đổi">{data?.messages.map(message => <li key={message.id}><strong>{message.authorName}</strong><small>{when(message.createdAt)}</small><p>{message.body}</p></li>)}</ol>
      <form onSubmit={event => { event.preventDefault(); void mutate(async () => {
        const text = body.trim(); if (!text) return;
        if (messageKey.current?.body !== text) messageKey.current = { body: text, key: crypto.randomUUID() };
        await api.sendSupportMessage(id, text, messageKey.current.key); setBody(""); messageKey.current = null;
      }); }}><label className="field">Tin nhắn<textarea className="form-input" required maxLength={4000} rows={3} value={body} onChange={event => setBody(event.target.value)} /></label><button type="submit" className="action" disabled={busy || !body.trim()}>Gửi tin nhắn</button></form>
      <Evidence id={id} onUploaded={() => { void refresh(); onChanged(); }} attachments={data?.attachments ?? []} />
    </>}
  </section>;
}

function Evidence({ id, attachments, onUploaded }: { id: string; attachments: { id: string; fileName: string }[]; onUploaded: () => void }) {
  const [file, setFile] = useState<File | null>(null); const [preview, setPreview] = useState("");
  const [consent, setConsent] = useState(false); const [error, setError] = useState(""); const [busy, setBusy] = useState(false);
  const [download, setDownload] = useState<{ url: string; name: string } | null>(null);
  useEffect(() => { if (preview) return () => URL.revokeObjectURL(preview); }, [preview]);
  useEffect(() => { if (download) return () => URL.revokeObjectURL(download.url); }, [download]);
  async function upload() {
    if (!file || !consent || busy) return; setBusy(true); setError("");
    try {
      const base64 = await new Promise<string>((resolve, reject) => { const reader = new FileReader(); reader.onload = () => resolve(String(reader.result).split(",")[1]); reader.onerror = () => reject(new Error("Chưa đọc được ảnh.")); reader.readAsDataURL(file); });
      await api.uploadSupportAttachment(id, { fileName: file.name, contentType: file.type, base64 });
      setFile(null); setPreview(""); setConsent(false); onUploaded();
    } catch (cause) { setError(errorMessage(cause)); } finally { setBusy(false); }
  }
  return <div className="support-card"><h3>Ảnh lỗi</h3><p>Chỉ gửi ảnh bạn chủ động chọn. Che thông tin cá nhân hoặc tài liệu trước khi gửi. PNG/JPEG, tối đa 2 MB.</p>
    <label className="field">Chọn ảnh<input type="file" accept="image/png,image/jpeg" disabled={busy} onChange={event => {
      const candidate = event.target.files?.[0]; setConsent(false); setFile(null); setPreview("");
      if (!candidate) return; const invalid = validateSupportImage(candidate); setError(invalid ?? ""); if (!invalid) { setFile(candidate); setPreview(URL.createObjectURL(candidate)); } event.target.value = "";
    }} /></label>
    {file && preview && <><Image src={preview} alt={`Ảnh xem trước: ${file.name}`} width={360} height={220} unoptimized className="support-evidence" /><label><input type="checkbox" checked={consent} onChange={event => setConsent(event.target.checked)} /> Tôi đã xem lại ảnh và đồng ý gửi cho IT.</label><div className="support-inline-actions"><button type="button" className="action" disabled={busy || !consent} onClick={() => void upload()}>Gửi ảnh</button><button type="button" className="action-outline" disabled={busy} onClick={() => { setFile(null); setPreview(""); setConsent(false); }}>Bỏ ảnh</button></div></>}
    {error && <p role="alert">{error}</p>}
    <ul>{attachments.map(item => <li key={item.id}><button type="button" className="action-outline" disabled={busy} onClick={async () => { setBusy(true); setError(""); try { const blob = await api.downloadSupportAttachment(id, item.id); setDownload({ url: URL.createObjectURL(blob), name: item.fileName }); } catch (cause) { setError(errorMessage(cause)); } finally { setBusy(false); } }}>{item.fileName}</button></li>)}</ul>
    {download && <Image src={download.url} alt={download.name} width={360} height={220} unoptimized className="support-evidence" />}
  </div>;
}

function CatalogEditor({ app, onSaved }: { app?: CatalogApp; onSaved: () => void }) {
  const [open, setOpen] = useState(false); const [error, setError] = useState(""); const [busy, setBusy] = useState(false);
  const [form, setForm] = useState<SaveCatalogApp>({ name: app?.name ?? "", description: app?.description ?? "", version: app?.version ?? "", packageUrl: app?.packageUrl ?? "", sha256: app?.sha256 ?? "", publisherThumbprint: app?.publisherThumbprint ?? "", isActive: app?.isActive ?? true, requiresApproval: app?.requiresApproval ?? true, reason: "", confirmed: false });
  return <div className="support-card"><button type="button" className="action-outline" onClick={() => setOpen(!open)}>{app ? "Sửa phần mềm / ngưng cung cấp" : "Thêm phần mềm được phép"}</button>{open && <form onSubmit={event => { event.preventDefault(); if (busy || !form.confirmed) return; setBusy(true); setError(""); void api.saveCatalogApp(form, app?.id).then(() => { setOpen(false); onSaved(); }).catch(cause => setError(errorMessage(cause))).finally(() => setBusy(false)); }}>
    {([ ["name", "Tên phần mềm"], ["description", "Ứng dụng giúp nhân viên làm gì?"], ["version", "Phiên bản"], ["packageUrl", "URL HTTPS của gói MSI"], ["sha256", "SHA-256 của gói cài"], ["publisherThumbprint", "Thumbprint chứng thư nhà phát hành"], ["reason", "Lý do công bố / sửa đổi"] ] as const).map(([key, label]) => <label key={key} className="field">{label}<input required maxLength={key === "description" ? 2000 : 2048} value={form[key]} onChange={event => setForm({ ...form, [key]: event.target.value })} /></label>)}
    <label><input type="checkbox" checked={form.isActive} onChange={event => setForm({ ...form, isActive: event.target.checked })} /> Đang cung cấp</label><label><input type="checkbox" checked={form.requiresApproval} onChange={event => setForm({ ...form, requiresApproval: event.target.checked })} /> Yêu cầu Admin duyệt trước khi cài</label><label><input type="checkbox" checked={form.confirmed} onChange={event => setForm({ ...form, confirmed: event.target.checked })} /> Tôi xác minh gói cài, nhà phát hành và xác nhận công bố.</label>
    {error && <p role="alert">{error}</p>}<div className="support-inline-actions"><button type="submit" className="action" disabled={busy || !form.confirmed}>Lưu phần mềm</button></div>
  </form>}</div>;
}

function AnnouncementEditor({ onSaved }: { onSaved: () => void }) {
  const [open, setOpen] = useState(false); const [title, setTitle] = useState(""); const [body, setBody] = useState("");
  const [start, setStart] = useState(""); const [end, setEnd] = useState(""); const [outage, setOutage] = useState(false);
  const [ack, setAck] = useState(false); const [reason, setReason] = useState(""); const [confirmed, setConfirmed] = useState(false);
  const [busy, setBusy] = useState(false); const [error, setError] = useState("");
  return <div className="support-card"><button type="button" className="action-outline" onClick={() => setOpen(!open)}>Gửi thông báo / lịch bảo trì</button>{open && <form onSubmit={event => { event.preventDefault(); if (busy || !confirmed) return; setBusy(true); setError("");
    const data: SaveAnnouncement = { title, body, isOutage: outage, requiresAcknowledgement: ack, startsAt: new Date(start).toISOString(), endsAt: new Date(end).toISOString(), reason, confirmed };
    void api.createAnnouncement(data).then(() => { setOpen(false); onSaved(); }).catch(cause => setError(errorMessage(cause))).finally(() => setBusy(false));
  }}><label className="field">Tiêu đề<input required maxLength={200} value={title} onChange={event => setTitle(event.target.value)} /></label><label className="field">Nội dung<textarea required className="form-input" rows={3} maxLength={4000} value={body} onChange={event => setBody(event.target.value)} /></label><label className="field">Bắt đầu<input required type="datetime-local" value={start} onChange={event => setStart(event.target.value)} /></label><label className="field">Kết thúc<input required type="datetime-local" value={end} onChange={event => setEnd(event.target.value)} /></label><label><input type="checkbox" checked={outage} onChange={event => setOutage(event.target.checked)} /> Sự cố chung — cho phép nhân viên báo bị ảnh hưởng</label><label><input type="checkbox" checked={ack} onChange={event => setAck(event.target.checked)} /> Yêu cầu xác nhận đã đọc</label><label className="field">Lý do gửi<input required minLength={3} maxLength={1000} value={reason} onChange={event => setReason(event.target.value)} /></label><label><input type="checkbox" checked={confirmed} onChange={event => setConfirmed(event.target.checked)} /> Tôi xác nhận gửi thông báo trong tổ chức.</label>{error && <p role="alert">{error}</p>}<div className="support-inline-actions"><button type="submit" className="action" disabled={busy || !confirmed}>Gửi thông báo</button></div></form>}</div>;
}
