import type { VpsContainer } from "@/types/api";
import { listeningPorts, publishedPorts } from "../lib/vps-ports";

export function ContainerPorts({ container }: { container: VpsContainer }) {
  const published = publishedPorts(container.ports ?? []);
  const listening = container.listeningPorts == null ? null : listeningPorts(container.listeningPorts.filter(port => port.address !== "127.0.0.11"));
  const dockerDns = container.listeningPorts?.filter(port => port.address === "127.0.0.11") ?? [];
  const declared = container.ports?.filter(port => port.hostPort == null) ?? [];
  return <div className="container-ports">
    {container.state !== "running" ? <span>Container đã dừng</span> : listening === null ? <span className="muted">Chưa đo cổng đang nghe</span> :
      listening.length === 0 ? <span>Không có cổng đang nghe</span> : <div><strong>Đang nghe:</strong> {listening.map(port =>
        <code key={`${port.address}:${port.port}/${port.protocol}`} title={`Địa chỉ trong mạng container: ${port.address}`}>
          {port.port}{port.endPort !== port.port ? `–${port.endPort}` : ""}/{port.protocol.toUpperCase()}
        </code>)}{container.networkMode?.startsWith("container:") && <small>Dùng chung mạng với container khác</small>}
        {container.networkMode === "host" && <small>Dùng chung mạng của VPS</small>}</div>}
    <div><strong>Công bố trên VPS:</strong>{published.length ? published.map(port => <div key={port.label}>
      <code>{port.label}</code><small>{port.local ? "Chỉ trên VPS" : "Lắng nghe trên địa chỉ VPS"}</small>
    </div>) : <span> Không có cổng công bố riêng</span>}</div>
    {declared.length > 0 && <details><summary>Cổng khai báo trong Docker ({declared.length})</summary>
      <small>Cổng khai báo chưa chứng minh ứng dụng đang nghe tại đó.</small>
      <div>{declared.map(port => <code key={`${port.containerPort}/${port.protocol}`}>{port.containerPort}/{port.protocol.toUpperCase()}</code>)}</div>
    </details>}
    {dockerDns.length > 0 && <details><summary>Cổng DNS nội bộ của Docker ({dockerDns.length})</summary>
      <div>{dockerDns.map(port => <code key={`${port.port}/${port.protocol}`}>127.0.0.11:{port.port}/{port.protocol.toUpperCase()}</code>)}</div>
    </details>}
  </div>;
}
