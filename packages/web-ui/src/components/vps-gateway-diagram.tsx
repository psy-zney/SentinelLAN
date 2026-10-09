"use client";

import type { VpsContainer, VpsHostSnapshot } from "@/types/host-status";
import { gatewayEvidence } from "@/lib/vps-gateway";
import { endpoint, getProjectMeta } from "@/lib/vps-host";

export function VpsGatewayDiagram({ snapshot, vi, selectContainer }: { snapshot: VpsHostSnapshot; vi: boolean; selectContainer: (id: string, trigger: HTMLButtonElement) => void }) {
  const label = (vietnamese: string, english: string) => vi ? vietnamese : english;
  const evidence = gatewayEvidence(snapshot);
  const all = snapshot.runtime.containers;
  const role = (members: VpsContainer[], name: string) => members.find(container => new RegExp("-" + name + "-\\d+$").test(container.name));
  const arrow = <span className="vps-route-arrow" aria-hidden="true">→</span>;
  const node = (container: VpsContainer | undefined, title: string, port: string, kind = "container") => {
    const contents = <><strong>{title}</strong><code>{port}</code><small>{container?.name ?? label("Chưa ghi nhận container", "Container not recorded")}</small>{container && <span className={"badge " + (container.state === "running" && container.health !== "unhealthy" ? "badge-success" : "badge-neutral")}>{container.state}</span>}</>;
    return container ? <button type="button" className={"vps-route-node vps-route-" + kind} aria-label={label("Xem container", "View container") + " " + container.name} onClick={event => selectContainer(container.id, event.currentTarget)}>{contents}</button> : <div className={"vps-route-node vps-route-" + kind}>{contents}</div>;
  };
  const ownerContainerIds = new Set(evidence.projects.flatMap(project => project.containers.map(container => container.id)));
  const other = all.filter(container => !ownerContainerIds.has(container.id));
  const bakery = other.filter(container => getProjectMeta(container.project, container.name).id === "mot-me-banh");
  const unclassified = other.filter(container => !bakery.includes(container));
  const bakeryApi = role(bakery, "api");
  const bakeryBindings = bakeryApi?.ports?.filter(port => port.hostPort !== null && port.protocol === "tcp") ?? [];

  return <section className="vps-gateway-diagram" aria-label={label("Định tuyến dự án qua Nginx 9000", "Project routing through Nginx 9000")}>
    <h3>{label("Các dự án của tôi → Nginx 9000 → localhost > 9000", "My projects → Nginx 9000 → localhost > 9000")}</h3>
    <p className="vps-section-description">{label("Đọc theo mũi tên để biết Nginx nào, localhost nào và container nào nhận kết nối. Bấm một container để xem chi tiết. Trạng thái cổng lấy từ lần cập nhật gần nhất; đường đi theo cấu hình triển khai.", "Follow the arrows to see which Nginx, loopback endpoint and container receives the connection. Click a container for details. Port status comes from the latest snapshot; routes follow the deployment configuration.")}</p>
    <div className="vps-route-legend"><span className="vps-route-key" data-kind="nginx">Nginx</span><span className="vps-route-key" data-kind="localhost">{label("Cổng localhost trên VPS", "Host loopback port")}</span><span className="vps-route-key" data-kind="container">Container</span><span className="vps-route-key" data-kind="direct">{label("Kết nối không qua Nginx", "Connection without Nginx")}</span></div>
    <div className="vps-gateway-entry"><strong>{label("Nginx trên VPS :9000", "Host Nginx :9000")}</strong><span><code>HTTP 127.0.0.1:9000</code> <span className={"badge " + (evidence.http ? "badge-success" : "badge-warn")}>{evidence.http ? label("Đang nghe", "Listening") : label("Chưa ghi nhận", "Not recorded")}</span></span><span><code>TLS 127.0.0.2:9000</code> <span className={"badge " + (evidence.tls ? "badge-success" : "badge-warn")}>{evidence.tls ? label("Đang nghe", "Listening") : label("Chưa ghi nhận", "Not recorded")}</span></span><small>{label("HTTPS manager/voice :443 vẫn đi qua gateway :9000 trên VPS.", "Manager/voice HTTPS :443 still passes through host gateway :9000.")}</small></div>
    <div className="vps-gateway-projects">
      {evidence.projects.map(project => {
        const sentinel = project.id === "sentinellan";
        const nginx = role(project.containers, "nginx");
        return <article className={"vps-gateway-project " + (sentinel ? "vps-gateway-sentinel" : "")} key={project.id}>
          <div className="vps-gateway-project-title"><h4>{project.name}</h4><span className={"badge " + (project.matches ? "badge-success" : "badge-warn")}>{project.matches ? label("Cổng khớp", "Ports match") : label("Cần kiểm tra cổng", "Check ports")}</span></div>
          <div className="vps-route-flow">
            <div className="vps-route-node vps-route-localhost"><strong>{sentinel ? "TLS / localhost VPS" : "HTTP / localhost VPS"}</strong><code>127.0.0.1:{project.port}</code><small>{sentinel ? "127.0.0.2:9000 → :9003" : "Nginx :9000 → localhost"}</small></div>{arrow}
            {project.id === "beatsync" ? <div className="vps-route-node"><strong>beatsync.service</strong><code>127.0.0.1:9001</code><small>{label("Go · dịch vụ hệ thống, không dùng Docker", "Go · system service, without Docker")}</small></div>
              : sentinel ? node(nginx, "Nginx Docker · gateway", ":8443 / TLS", "nginx")
                : node(project.bindings[0]?.container ?? project.containers[0], project.id === "livekit" ? "LiveKit" : "Game server", project.id === "livekit" ? ":7880 / HTTP + WebSocket" : ":3001 / HTTP + Socket.IO")}
          </div>
          {sentinel && <div className="vps-route-sentinel-branches">
            <div className="vps-route-lane"><p><strong>/api · /health · /hubs</strong> <span className="badge badge-info">Nginx Docker → API</span></p>{node(role(project.containers, "api"), "ASP.NET Core API", ":8443 / TLS")}
              <div className="vps-route-direct"><p><strong>API → PostgreSQL</strong> <span className="badge badge-neutral">{label("SQL không qua Nginx", "SQL bypasses Nginx")}</span></p>{node(role(project.containers, "postgres"), "PostgreSQL", ":5432 / TLS · Docker", "database")}<small>{label("Không mở cổng database trên VPS.", "No database host port published.")}</small></div>
            </div>
            {(["company", "employee"] as const).map(web => <div className="vps-route-lane" key={web}><p><strong>/{web}</strong> <span className="badge badge-info">Nginx Docker → Nginx TLS → web</span></p>{node(role(project.containers, web + "-tls"), "Nginx TLS · " + web, ":3443 / TLS", "nginx")}<div className="vps-route-vertical-arrow" aria-hidden="true">↓</div>{node(role(project.containers, web), web === "company" ? label("Web Admin", "Admin web") : label("Web nhân viên", "Employee web"), "127.0.0.1:3000 / HTTP")}<small>{label("Localhost trong mạng dùng chung của web và proxy TLS; hai web có hai mạng riêng.", "Loopback in the network shared by the web and its TLS proxy; the two webs have separate networks.")}</small></div>)}
          </div>}
          {project.id === "livekit" && <div className="vps-route-direct"><strong>{label("Media trực tiếp → LiveKit", "Direct media → LiveKit")}</strong><code>UDP 50000–50100</code><span className="badge badge-neutral">{label("Không qua Nginx", "Bypasses Nginx")}</span></div>}
          <details className="vps-gateway-route-details"><summary>{label("Chi tiết", "Details")} {project.name}</summary><div className="vps-gateway-project-detail">
            <p><strong>{label("Tên miền / đường dẫn", "Domain / path")}</strong><br/><code>{project.route}</code></p>
            <p><strong>{label("Luồng qua gateway", "Gateway flow")}</strong><br/><code>{project.tls ? "HTTPS :443 → TLS 127.0.0.2:9000" : "Nginx HTTP :9000"} → {project.tls ? "https" : "http"}://127.0.0.1:{project.port}</code></p>
            <p><strong>{label("Cổng thực tế trên VPS", "Actual host endpoints")}</strong><br/>{project.endpoints.length ? project.endpoints.map(value => <code key={value}>{value} </code>) : label("Chưa ghi nhận cổng của dự án.", "No project endpoints recorded.")}</p>
            {project.bindings.map(({ container, binding }, index) => <div className="vps-diagram-flow" key={index}><code>{endpoint(binding.hostIp, binding.hostPort!)} → {binding.containerPort}/tcp</code><button type="button" className="vps-port-detail-toggle" onClick={event => selectContainer(container.id, event.currentTarget)}>{container.name}</button></div>)}
            {sentinel && <p>{label("127.0.0.2 cũng là localhost. Relay TCP giữ nguyên TLS tới gateway Docker; Nginx HTTPS :443 tiếp tục xác minh chứng chỉ.", "127.0.0.2 is also loopback. The TCP relay preserves TLS to the Docker gateway; HTTPS Nginx :443 continues to verify its certificate.")}</p>}
          </div></details>
        </article>;
      })}
    </div>
    {!!other.length && <section className="vps-route-other" aria-label={label("Container dự án khác trên VPS", "Other projects' containers")}><h4>{label("Dự án khác trên VPS — ngoài phạm vi chuyển cổng", "Other projects — outside the port migration")}</h4>
      {!!bakery.length && <><p><strong>Mọt Mẻ Bánh</strong> · {label("Dự án của người khác, cấu hình được giữ nguyên.", "Another user's project; configuration is unchanged.")}</p><div className="vps-route-other-flow"><div className="vps-route-node vps-route-nginx"><strong>Nginx VPS :80</strong><small>apimotmebanh.congtc145.id.vn</small></div>{arrow}<div className="vps-route-node vps-route-localhost"><strong>Localhost VPS</strong><code>{bakeryBindings.map(binding => endpoint(binding.hostIp, binding.hostPort!)).join(", ") || label("Chưa ghi nhận", "Not recorded")}</code></div>{arrow}{node(bakeryApi, "API", ":3000 / HTTP")}</div><div className="vps-route-direct"><p><strong>API → PostgreSQL</strong> <span className="badge badge-neutral">{label("SQL không qua Nginx", "SQL bypasses Nginx")}</span></p>{node(role(bakery, "postgres"), "PostgreSQL", ":5432 / Docker", "database")}</div>{bakery.filter(container => container !== bakeryApi && container !== role(bakery, "postgres")).map(container => <div className="vps-route-direct" key={container.id}>{node(container, label("Tác vụ migration", "Migration task"), label("Không nhận lưu lượng web", "No inbound web traffic"), "database")}</div>)}</>}
      {unclassified.map(container => <div className="vps-route-direct" key={container.id}>{node(container, getProjectMeta(container.project, container.name).name, label("Chưa xác định tuyến Nginx", "Nginx route not classified"))}</div>)}
    </section>}
    <p className="vps-diagram-legend">{label("Cổng localhost trên VPS được tô vàng; cổng trong container nằm trong nút container. Nhánh SQL/media được đánh dấu không qua Nginx. Cổng container có thể lặp vì nằm trong mạng riêng. Mọt Mẻ Bánh là dự án khác trên VPS, ngoài phạm vi quy ước này.", "Host loopback ports are amber; container ports appear inside container nodes. SQL/media branches are marked as bypassing Nginx. Container ports may repeat in isolated networks. Mọt Mẻ Bánh is another project outside this policy.")}</p>
  </section>;
}
