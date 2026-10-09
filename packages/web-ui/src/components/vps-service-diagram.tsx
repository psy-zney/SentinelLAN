"use client";

import { useId, useRef, useState } from "react";
import type { VpsHostSnapshot } from "@/types/host-status";
import { containersForPort, endpoint, getProjectMeta, portsForService, portScope } from "@/lib/vps-host";
import { VpsGatewayDiagram } from "./vps-gateway-diagram";

type Selection = { kind: "service" | "container"; key: string } | null;

export function VpsServiceDiagram({ snapshot, vi }: { snapshot: VpsHostSnapshot; vi: boolean }) {
  const [selection, setSelection] = useState<Selection>(null);
  const detailsId = useId();
  const detailTitleId = useId();
  const details = useRef<HTMLDivElement>(null);
  const tree = useRef<HTMLDivElement>(null);
  const projectNodes = useRef(new Map<string, HTMLDetailsElement>());
  const inventory = useRef<HTMLDetailsElement>(null);
  const selectionTrigger = useRef<HTMLButtonElement>(null);
  const label = (vietnamese: string, english: string) => vi ? vietnamese : english;
  const services = snapshot.runtime.services;
  const containers = snapshot.runtime.containers;
  const selectedService = selection?.kind === "service" ? services.find(service => service.name === selection.key) : undefined;
  const selectedContainer = selection?.kind === "container" ? containers.find(container => container.id === selection.key) : undefined;
  const selectedPorts = selectedService ? portsForService(selectedService.name, snapshot.listeningPorts) : [];
  const selectedName = selectedService?.name ?? selectedContainer?.name;
  const projectKey = (container: typeof containers[number]) => {
    const meta = getProjectMeta(container.project, container.name);
    return meta.id === "other" ? container.project || meta.id : meta.id;
  };
  const groups = Array.from(new Set(containers.map(projectKey))).map(id => {
    const members = containers.filter(container => projectKey(container) === id);
    return { ...getProjectMeta(members[0].project, members[0].name), id, members };
  });
  const select = (kind: "service" | "container", key: string, trigger: HTMLButtonElement | null = null) => {
    const closing = selection?.kind === kind && selection.key === key;
    setSelection(closing ? null : { kind, key });
    selectionTrigger.current = closing ? null : trigger;
    if (closing && trigger && inventory.current) inventory.current.open = false;
    if (!closing && inventory.current) inventory.current.open = true;
    if (!closing && kind === "container") {
      const container = containers.find(item => item.id === key);
      const project = container && projectNodes.current.get(projectKey(container));
      if (project) project.open = true;
    }
    if (!closing && window.matchMedia("(max-width: 1000px)").matches) {
      window.requestAnimationFrame(() => details.current?.scrollIntoView({ block: "nearest" }));
    }
  };

  return <div className="vps-service-diagram" aria-label={label("Sơ đồ dịch vụ VPS", "VPS service diagram")}>
    <VpsGatewayDiagram snapshot={snapshot} vi={vi} selectContainer={(id, trigger) => select("container", id, trigger)}/>
    <p className="vps-section-description">{label("Bấm một dịch vụ hoặc container để xem chi tiết; bấm lại để thu gọn. Bấm tên dự án để mở nhánh container.", "Click a service or container for details; click again to collapse. Click a project to expand its containers.")}</p>
    <details className="vps-diagram-inventory" ref={inventory}>
      <summary>{label("Chi tiết tất cả dịch vụ và container", "All service and container details")}</summary>
      <div className="vps-diagram-layout">
      <div className="vps-diagram-tree" ref={tree}>
        <div className="vps-diagram-root"><span aria-hidden="true">▣</span><strong>{snapshot.name}</strong><code>{snapshot.host}</code><span className="badge badge-info">{snapshot.runtime.systemState}</span></div>
        <div className="vps-diagram-branches">
          <section className="vps-diagram-branch" aria-label={label("Nhánh dịch vụ hệ thống", "System service branch")}>
            <h3>{label("Dịch vụ hệ thống", "System services")} <span className="badge badge-neutral">{services.length}</span></h3>
            <div className="vps-diagram-nodes">
              {services.map(service => {
                const ports = portsForService(service.name, snapshot.listeningPorts);
                const open = selectedService?.name === service.name;
                return <button type="button" className="vps-diagram-node" key={service.name} aria-label={`${label("Dịch vụ", "Service")} ${service.name}`} aria-expanded={open} aria-controls={detailsId} data-state={service.activeState === "active" ? "ok" : "warn"} onClick={() => select("service", service.name)}>
                  <span className="vps-diagram-node-heading"><strong>{service.name}</strong><span className={`badge ${service.activeState === "active" ? "badge-success" : "badge-neutral"}`}>{service.activeState}</span></span>
                  <small>{ports.length} {label("cổng đang nghe", "listening ports")} · {service.startupState}</small>
                  <span className="vps-diagram-node-action">{open ? label("Thu gọn", "Collapse") : label("Xem chi tiết →", "View details →")}</span>
                </button>;
              })}
              {!services.length && <p>{label("Chưa ghi nhận dịch vụ hệ thống.", "No system services recorded.")}</p>}
            </div>
          </section>
          <section className="vps-diagram-branch" aria-label={label("Nhánh dự án Docker", "Docker project branch")}>
            <h3>{label("Dự án Docker", "Docker projects")} <span className="badge badge-neutral">{containers.length}</span></h3>
            <div className="vps-diagram-nodes">
              {groups.map(group => <details className="vps-diagram-project" key={group.id} ref={element => { if (element) projectNodes.current.set(group.id, element); else projectNodes.current.delete(group.id); }}>
                <summary><span aria-hidden="true">{group.icon}</span> <strong>{group.name}</strong> <span className="badge badge-neutral">{group.members.length} container</span></summary>
                <div className="vps-diagram-nodes">
                  {group.members.map(container => {
                    const open = selectedContainer?.id === container.id;
                    const running = container.state === "running" && container.health !== "unhealthy";
                    const published = container.ports?.filter(port => port.hostPort !== null).length ?? 0;
                    return <button type="button" key={container.id} className="vps-diagram-node" data-state={running ? "ok" : "warn"} aria-label={`Container ${container.name}`} aria-expanded={open} aria-controls={detailsId} onClick={() => select("container", container.id)}>
                      <span className="vps-diagram-node-heading"><strong>{container.name}</strong><span className={`badge ${running ? "badge-success" : "badge-warn"}`}>{container.state}{container.health ? ` · ${container.health}` : ""}</span></span>
                      <small>{published} {label("ánh xạ cổng VPS", "host port bindings")}</small>
                      <span className="vps-diagram-node-action">{open ? label("Thu gọn", "Collapse") : label("Xem chi tiết →", "View details →")}</span>
                    </button>;
                  })}
                </div>
              </details>)}
              {!containers.length && <p>{snapshot.runtime.dockerAvailable ? label("Chưa ghi nhận container.", "No containers recorded.") : label("Chưa đọc được Docker.", "Docker is unavailable.")}</p>}
            </div>
          </section>
        </div>
      </div>
      <div className="vps-diagram-detail" ref={details} id={detailsId} role="region" aria-labelledby={detailTitleId}>
        <div className="vps-diagram-detail-heading"><h3 id={detailTitleId}>{selectedName ?? label("Chi tiết từng nút", "Node details")}</h3>{selectedName && <button type="button" className="vps-port-btn-sm" onClick={() => { const trigger = selectionTrigger.current; (trigger ?? tree.current?.querySelector<HTMLButtonElement>('.vps-diagram-node[aria-expanded="true"]'))?.focus(); if (trigger && inventory.current) inventory.current.open = false; selectionTrigger.current = null; setSelection(null); }}>{label("Thu gọn", "Collapse")}</button>}</div>
        {!selectedName && <p className="vps-section-description">{label("Chọn một nút trong sơ đồ để xem trạng thái, cổng và phạm vi localhost của nút đó.", "Select a diagram node for its state, ports and localhost scope.")}</p>}
        {selectedService && <>
          <dl className="vps-diagram-facts"><div><dt>{label("Trạng thái", "State")}</dt><dd>{selectedService.activeState}</dd></div><div><dt>{label("Khởi động cùng VPS", "Startup")}</dt><dd>{selectedService.startupState}</dd></div></dl>
          <h4>{label("Cổng do tiến trình nghe", "Process listening ports")}</h4>
          <div className="vps-diagram-port-list">{selectedPorts.map((port, index) => {
            const owners = containersForPort(port, containers);
            return <div className="vps-diagram-port" key={index}>
              <code>{endpoint(port.address, port.port)}/{port.protocol}</code><span className="badge badge-info">{portScope(port.address, vi)}</span><small>{port.process}</small>
              {owners.map(container => <div className="vps-diagram-flow" key={container.id}><span aria-hidden="true">→</span><button type="button" className="vps-port-detail-toggle" onClick={() => select("container", container.id)}>{container.name}</button></div>)}
            </div>;
          })}</div>
          {!selectedPorts.length && <p className="vps-section-description">{label("Chưa ghi nhận cổng tương ứng với tiến trình của dịch vụ này.", "No matching process ports recorded for this service.")}</p>}
        </>}
        {selectedContainer && <>
          <dl className="vps-diagram-facts">
            <div><dt>{label("Dự án", "Project")}</dt><dd>{getProjectMeta(selectedContainer.project, selectedContainer.name).name}</dd></div>
            <div><dt>Image</dt><dd><code>{selectedContainer.image}</code></dd></div>
            <div><dt>{label("Trạng thái", "State")}</dt><dd>{selectedContainer.status}{selectedContainer.health ? ` · ${selectedContainer.health}` : ""}</dd></div>
            <div><dt>CPU</dt><dd>{selectedContainer.cpuPercent !== null ? `${selectedContainer.cpuPercent.toFixed(2)}%` : label("Chưa có số liệu", "No metrics")}</dd></div>
            <div><dt>RAM</dt><dd>{selectedContainer.memoryUsage ?? label("Chưa có số liệu", "No metrics")}{selectedContainer.memoryPercent !== null ? ` (${selectedContainer.memoryPercent.toFixed(1)}%)` : ""}</dd></div>
          </dl>
          <h4>{label("Ánh xạ cổng VPS → container", "Host → container port bindings")}</h4>
          <div className="vps-diagram-port-list">{selectedContainer.ports?.filter(port => port.hostPort !== null).map((port, index) => <div className="vps-diagram-port" key={index}>
            <span className="vps-diagram-binding"><code>{endpoint(port.hostIp, port.hostPort!)}</code><span aria-hidden="true">→</span><code>{port.containerPort}/{port.protocol}</code></span><span className="badge badge-info">{portScope(port.hostIp, vi)}</span>
          </div>)}</div>
          {!selectedContainer.ports?.some(port => port.hostPort !== null) && <p className="vps-section-description">{label("Không có ánh xạ cổng ra VPS được ghi nhận.", "No host port bindings recorded.")}</p>}
          <h4>{label("Cổng đang nghe trong container", "Container listening ports")}</h4>
          <div className="vps-diagram-port-list">{selectedContainer.listeningPorts?.map((port, index) => <div className="vps-diagram-port" key={index}><code>{endpoint(port.address, port.port)}/{port.protocol}</code><span className="badge badge-neutral">{portScope(port.address, vi)}</span></div>)}</div>
          {!selectedContainer.listeningPorts?.length && <p className="vps-section-description">{label("Chưa ghi nhận socket đang nghe trong container.", "No container listening sockets recorded.")}</p>}
          {selectedContainer.ports?.some(port => port.hostPort === null) && <><h4>{label("Cổng EXPOSE — không có ánh xạ ra VPS", "EXPOSE ports — no host mapping")}</h4><div className="vps-diagram-port-list">{selectedContainer.ports.filter(port => port.hostPort === null).map((port, index) => <code key={index}>{port.containerPort}/{port.protocol}</code>)}</div></>}
        </>}
      </div>
    </div>
    <p className="vps-diagram-legend">{label("Đường nhánh: thành phần trên VPS. Mũi tên cổng: ánh xạ Docker được ghi nhận. Phạm vi nghe trong container thuộc mạng container; quyền truy cập từ Internet còn phụ thuộc firewall và reverse proxy.", "Branches show host components. Port arrows show recorded Docker bindings. Container listening scope belongs to its network; Internet access also depends on the firewall and reverse proxy.")}</p>
    </details>
  </div>;
}
