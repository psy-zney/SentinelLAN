const releasePage = "https://github.com/psy-zney/SentinelLAN/releases";
const status = document.getElementById("release-status");
const links = [...document.querySelectorAll("[data-asset]")];

async function updateDownloads() {
  try {
    const response = await fetch("https://api.github.com/repos/psy-zney/SentinelLAN/releases/latest", {
      headers: { Accept: "application/vnd.github+json" }
    });
    if (!response.ok) throw new Error("No public release");
    const release = await response.json();
    const assets = new Map((release.assets || []).map(asset => [asset.name, asset.browser_download_url]));
    for (const link of links) {
      const url = assets.get(link.dataset.asset);
      if (url) {
        link.href = url;
        if (link.classList.contains("button")) link.textContent = "Tải " + link.dataset.asset;
      } else if (link.classList.contains("button")) {
        link.textContent = "Chưa có " + link.dataset.asset + " · Xem Releases";
      }
    }
    status.textContent = "Bản phát hành hiện có: " + release.tag_name + ". Chỉ các gói đã có asset mới dẫn trực tiếp tới file.";
  } catch {
    status.textContent = "Chưa tìm thấy Release công khai. Các nút sẽ mở danh sách bản phát hành.";
    for (const link of links) link.href = releasePage;
  }
}

updateDownloads();
