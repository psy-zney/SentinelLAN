// scripts/export-codegraph-html.js
// Exports .codegraph/codegraph.db to a standalone interactive HTML visualizer using Vis-Network.

const fs = require('fs');
const path = require('path');
const { DatabaseSync } = require('node:sqlite');

const dbPath = path.resolve(__dirname, '../.codegraph/codegraph.db');
const outHtmlPath = path.resolve(__dirname, '../docs/html/codegraph-visualizer.html');

if (!fs.existsSync(dbPath)) {
  console.error('Error: .codegraph/codegraph.db not found. Run `codegraph init` first.');
  process.exit(1);
}

const db = new DatabaseSync(dbPath);

// 1. Fetch high-level architecture nodes (Classes, Interfaces, Routes, Functions, Structs)
const rawNodes = db.prepare(`
  SELECT id, kind, name, qualified_name, file_path, language, start_line, end_line
  FROM nodes
  WHERE kind IN ('class', 'interface', 'route', 'struct', 'enum', 'function')
`).all();

const nodeMap = new Map();
rawNodes.forEach(n => {
  nodeMap.set(n.id, n);
});

// 2. Fetch architectural edges (implements, extends, instantiates, calls, references)
const rawEdges = db.prepare(`
  SELECT id, source, target, kind
  FROM edges
  WHERE kind IN ('implements', 'extends', 'instantiates', 'calls')
`).all();

// Filter edges that connect known nodes
const validEdges = [];
const degreeMap = new Map();

rawEdges.forEach(e => {
  if (nodeMap.has(e.source) && nodeMap.has(e.target)) {
    validEdges.push(e);
    degreeMap.set(e.source, (degreeMap.get(e.source) || 0) + 1);
    degreeMap.set(e.target, (degreeMap.get(e.target) || 0) + 1);
  }
});

// Format nodes for vis-network
const visNodes = rawNodes.map(n => {
  const deg = degreeMap.get(n.id) || 0;
  let color = '#3b82f6'; // default blue (class)
  let shape = 'dot';
  
  if (n.kind === 'interface') {
    color = '#a855f7'; // purple
    shape = 'diamond';
  } else if (n.kind === 'route') {
    color = '#f97316'; // orange
    shape = 'triangle';
  } else if (n.kind === 'function') {
    color = '#10b981'; // emerald
    shape = 'dot';
  } else if (n.kind === 'struct' || n.kind === 'enum') {
    color = '#06b6d4'; // cyan
    shape = 'box';
  }

  // Group layer by file_path
  let layer = 'Other';
  if (n.file_path.includes('SentinelLAN.Domain')) layer = 'Domain';
  else if (n.file_path.includes('SentinelLAN.Application')) layer = 'Application';
  else if (n.file_path.includes('SentinelLAN.Infrastructure')) layer = 'Infrastructure';
  else if (n.file_path.includes('SentinelLAN.Api')) layer = 'API';
  else if (n.file_path.includes('apps/web')) layer = 'Frontend (Web)';

  return {
    id: n.id,
    label: n.name,
    title: `${n.kind.toUpperCase()}: ${n.qualified_name || n.name}\nFile: ${n.file_path}:${n.start_line}`,
    kind: n.kind,
    layer,
    filePath: n.file_path,
    startLine: n.start_line,
    endLine: n.end_line,
    color: {
      background: color,
      border: '#ffffff',
      highlight: { background: '#f59e0b', border: '#ffffff' }
    },
    shape,
    size: Math.min(32, Math.max(12, 10 + deg * 2)),
    value: deg
  };
});

// Format edges for vis-network
const visEdges = validEdges.map(e => {
  let color = '#64748b';
  let dashes = false;
  if (e.kind === 'implements') {
    color = '#c084fc';
    dashes = true;
  } else if (e.kind === 'extends') {
    color = '#60a5fa';
  } else if (e.kind === 'instantiates') {
    color = '#34d399';
    dashes = [4, 4];
  } else if (e.kind === 'calls') {
    color = '#94a3b8';
  }

  return {
    id: e.id,
    from: e.source,
    to: e.target,
    title: `${e.kind}: ${e.source} -> ${e.target}`,
    kind: e.kind,
    arrows: 'to',
    color: { color, highlight: '#f59e0b', opacity: 0.7 },
    dashes
  };
});

const htmlContent = `<!DOCTYPE html>
<html lang="vi">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>SentinelLAN — CodeGraph Visualizer</title>
  <script src="https://unpkg.com/vis-network/standalone/umd/vis-network.min.js"></script>
  <style>
    :root {
      --bg: #0f172a;
      --card: #1e293b;
      --border: #334155;
      --text: #f8fafc;
      --muted: #94a3b8;
      --primary: #10b981;
      --accent: #f59e0b;
    }
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body {
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
      background: var(--bg);
      color: var(--text);
      display: flex;
      flex-direction: column;
      height: 100vh;
      overflow: hidden;
    }
    header {
      background: var(--card);
      border-bottom: 1px solid var(--border);
      padding: 12px 20px;
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 16px;
      z-index: 10;
    }
    .logo {
      display: flex;
      align-items: center;
      gap: 10px;
      font-size: 16px;
      font-weight: 700;
      color: var(--text);
    }
    .badge {
      background: rgba(16, 185, 129, 0.15);
      color: var(--primary);
      border: 1px solid var(--primary);
      padding: 2px 8px;
      border-radius: 999px;
      font-size: 12px;
      font-weight: 600;
    }
    .controls {
      display: flex;
      align-items: center;
      gap: 12px;
      flex-wrap: wrap;
    }
    input, select, button {
      background: #0f172a;
      border: 1px solid var(--border);
      color: var(--text);
      padding: 6px 12px;
      border-radius: 6px;
      font-size: 13px;
      outline: none;
    }
    input:focus, select:focus {
      border-color: var(--primary);
    }
    button {
      background: var(--card);
      cursor: pointer;
      font-weight: 600;
      transition: all 0.15s ease;
    }
    button:hover {
      background: var(--border);
      color: #fff;
    }
    button.btn-primary {
      background: var(--primary);
      color: #0f172a;
      border: none;
    }
    button.btn-primary:hover {
      background: #059669;
    }
    .main-container {
      display: flex;
      flex: 1;
      position: relative;
      overflow: hidden;
    }
    #network {
      flex: 1;
      height: 100%;
      background: radial-gradient(circle at center, #1e293b 0%, #0f172a 100%);
    }
    .sidebar {
      width: 340px;
      background: var(--card);
      border-left: 1px solid var(--border);
      padding: 18px;
      display: flex;
      flex-direction: column;
      gap: 16px;
      overflow-y: auto;
      z-index: 5;
    }
    .panel-title {
      font-size: 14px;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      color: var(--muted);
      border-bottom: 1px solid var(--border);
      padding-bottom: 8px;
    }
    .legend-item {
      display: flex;
      align-items: center;
      gap: 8px;
      font-size: 13px;
      margin-bottom: 6px;
    }
    .color-dot {
      width: 12px;
      height: 12px;
      border-radius: 50%;
    }
    .stats-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 8px;
    }
    .stat-box {
      background: #0f172a;
      border: 1px solid var(--border);
      border-radius: 6px;
      padding: 10px;
      text-align: center;
    }
    .stat-num {
      font-size: 20px;
      font-weight: 800;
      color: var(--primary);
    }
    .stat-lbl {
      font-size: 11px;
      color: var(--muted);
      margin-top: 2px;
    }
    .info-card {
      background: #0f172a;
      border: 1px solid var(--border);
      border-radius: 8px;
      padding: 14px;
      font-size: 13px;
      line-height: 1.5;
    }
    .info-card strong {
      color: var(--accent);
      display: block;
      margin-bottom: 4px;
    }
    .info-card code {
      background: #1e293b;
      padding: 2px 6px;
      border-radius: 4px;
      font-size: 12px;
      display: block;
      margin-top: 4px;
      overflow-x: auto;
      word-break: break-all;
    }
  </style>
</head>
<body>
  <header>
    <div class="logo">
      <span>🛡️ SentinelLAN CodeGraph</span>
      <span class="badge">Architecture Explorer</span>
    </div>
    <div class="controls">
      <input type="text" id="searchInput" placeholder="Tìm symbol (vd: VpsNodeStore)..." />
      <button onclick="searchAndFocus()">🔍 Tìm & Focus</button>
      <select id="layerFilter" onchange="applyFilters()">
        <option value="all">Tất cả tầng (All Layers)</option>
        <option value="Domain">Domain</option>
        <option value="Application">Application</option>
        <option value="Infrastructure">Infrastructure</option>
        <option value="API">API</option>
        <option value="Frontend (Web)">Frontend (Web)</option>
      </select>
      <select id="kindFilter" onchange="applyFilters()">
        <option value="all">Tất cả loại (All Kinds)</option>
        <option value="class">Class</option>
        <option value="interface">Interface</option>
        <option value="route">Route (API/Web)</option>
        <option value="function">Function</option>
      </select>
      <button onclick="resetView()" class="btn-primary">Đặt lại góc nhìn</button>
    </div>
  </header>

  <div class="main-container">
    <div id="network"></div>
    <div class="sidebar">
      <div class="panel-title">Thống kê Kiến trúc</div>
      <div class="stats-grid">
        <div class="stat-box">
          <div class="stat-num">${visNodes.length}</div>
          <div class="stat-lbl">Symbols (Nodes)</div>
        </div>
        <div class="stat-box">
          <div class="stat-num">${visEdges.length}</div>
          <div class="stat-lbl">Mối quan hệ (Edges)</div>
        </div>
      </div>

      <div class="panel-title">Chú thích (Legend)</div>
      <div>
        <div class="legend-item"><div class="color-dot" style="background: #3b82f6;"></div><span>Class / DTO</span></div>
        <div class="legend-item"><div class="color-dot" style="background: #a855f7;"></div><span>Interface (Hợp đồng)</span></div>
        <div class="legend-item"><div class="color-dot" style="background: #f97316;"></div><span>Route Endpoint</span></div>
        <div class="legend-item"><div class="color-dot" style="background: #10b981;"></div><span>Function / Handler</span></div>
        <div class="legend-item"><div class="color-dot" style="background: #06b6d4;"></div><span>Struct / Enum</span></div>
      </div>
      <div>
        <div class="legend-item"><span style="color:#c084fc; font-weight:bold;">-- - -></span><span>Implements (Hiện thực)</span></div>
        <div class="legend-item"><span style="color:#60a5fa; font-weight:bold;">-----></span><span>Extends (Kế thừa)</span></div>
        <div class="legend-item"><span style="color:#94a3b8; font-weight:bold;">-----></span><span>Calls (Gọi hàm)</span></div>
        <div class="legend-item"><span style="color:#34d399; font-weight:bold;">· · · ></span><span>Instantiates (Khởi tạo)</span></div>
      </div>

      <div class="panel-title">Chi tiết Node được chọn</div>
      <div id="nodeDetail" class="info-card">
        <em>Nhấp vào bất kỳ node nào trên đồ thị để xem chi tiết vị trí file, liên kết và tầng kiến trúc.</em>
      </div>
    </div>
  </div>

  <script>
    const allNodes = ${JSON.stringify(visNodes)};
    const allEdges = ${JSON.stringify(visEdges)};

    const nodesDataSet = new vis.DataSet(allNodes);
    const edgesDataSet = new vis.DataSet(allEdges);

    const container = document.getElementById('network');
    const data = { nodes: nodesDataSet, edges: edgesDataSet };

    const options = {
      physics: {
        solver: 'forceAtlas2Based',
        forceAtlas2Based: {
          gravitationalConstant: -35,
          centralGravity: 0.005,
          springLength: 100,
          springConstant: 0.18,
          damping: 0.95
        },
        stabilization: { iterations: 120 }
      },
      interaction: {
        hover: true,
        tooltipDelay: 100,
        navigationButtons: true,
        keyboard: true
      }
    };

    const network = new vis.Network(container, data, options);

    // Node click handler
    network.on('click', function(params) {
      if (params.nodes.length > 0) {
        const nodeId = params.nodes[0];
        const node = allNodes.find(n => n.id === nodeId);
        if (node) {
          showNodeDetail(node);
        }
      }
    });

    function showNodeDetail(node) {
      const detailDiv = document.getElementById('nodeDetail');
      detailDiv.innerHTML = \`
        <strong>\${node.label}</strong>
        <p><strong>Loại:</strong> \${node.kind} (\${node.layer})</p>
        <p><strong>Kết nối:</strong> \${node.value} liên kết</p>
        <p><strong>File:</strong></p>
        <code>\${node.filePath} : L\${node.startLine}-L\${node.endLine}</code>
      \`;
    }

    function searchAndFocus() {
      const q = document.getElementById('searchInput').value.trim().toLowerCase();
      if (!q) return;
      const found = allNodes.find(n => n.label.toLowerCase().includes(q) || n.id.toLowerCase().includes(q));
      if (found) {
        network.focus(found.id, {
          scale: 1.4,
          animation: { duration: 800, easingFunction: 'easeInOutQuad' }
        });
        network.selectNodes([found.id]);
        showNodeDetail(found);
      } else {
        alert('Không tìm thấy symbol nào khớp với: ' + q);
      }
    }

    document.getElementById('searchInput').addEventListener('keydown', function(e) {
      if (e.key === 'Enter') searchAndFocus();
    });

    function applyFilters() {
      const layer = document.getElementById('layerFilter').value;
      const kind = document.getElementById('kindFilter').value;

      const filtered = allNodes.filter(n => {
        const matchLayer = layer === 'all' || n.layer === layer;
        const matchKind = kind === 'all' || n.kind === kind;
        return matchLayer && matchKind;
      });

      const allowedIds = new Set(filtered.map(n => n.id));
      const filteredEdges = allEdges.filter(e => allowedIds.has(e.from) && allowedIds.has(e.to));

      nodesDataSet.clear();
      edgesDataSet.clear();
      nodesDataSet.add(filtered);
      edgesDataSet.add(filteredEdges);
    }

    function resetView() {
      document.getElementById('layerFilter').value = 'all';
      document.getElementById('kindFilter').value = 'all';
      document.getElementById('searchInput').value = '';
      applyFilters();
      network.fit({ animation: { duration: 600 } });
    }
  </script>
</body>
</html>
`;

fs.writeFileSync(outHtmlPath, htmlContent, 'utf-8');
console.log(`[Success] Exported interactive visualizer to: ${outHtmlPath}`);
console.log(`Nodes: ${visNodes.length}, Edges: ${visEdges.length}`);
