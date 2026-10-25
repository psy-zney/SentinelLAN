"""Print-sized, monochrome thesis figures; geometry is retained for verification."""
from __future__ import annotations

import json
import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

WIDTH = 2400
PRINT_CM = 15.5
DPI = WIDTH / (PRINT_CM / 2.54)
FONT_PX = 58  # 10.62 pt at the actual insertion width
FONT_DIR = Path("C:/Windows/Fonts")


class Figure:
    def __init__(self, height: int):
        self.image = Image.new("RGB", (WIDTH, height), "white")
        self.draw = ImageDraw.Draw(self.image)
        self.texts: list[dict] = []

    def text(self, xy, text, size=FONT_PX, bold=False, anchor="mm"):
        font = ImageFont.truetype(str(FONT_DIR / ("timesbd.ttf" if bold else "times.ttf")), size)
        bounds = self.draw.multiline_textbbox(xy, text, font=font, anchor=anchor, spacing=13, align="center")
        self.draw.rectangle((bounds[0]-12, bounds[1]-7, bounds[2]+12, bounds[3]+7), fill="white")
        self.draw.multiline_text(xy, text, font=font, fill="black", anchor=anchor, spacing=13, align="center")
        self.texts.append({"text": text, "bounds": bounds, "font_pt": size * 72 / DPI})

    def box(self, rect, text):
        self.draw.rounded_rectangle(rect, radius=14, fill="white", outline="black", width=4)
        self.text(((rect[0]+rect[2])/2, (rect[1]+rect[3])/2), text)

    def path(self, points, dashed=False, arrow=True):
        for a, b in zip(points, points[1:]):
            if a[0] != b[0] and a[1] != b[1]:
                raise ValueError("Diagram connectors must be orthogonal")
            if dashed:
                length = math.dist(a, b)
                for offset in range(0, int(length), 30):
                    end = min(offset+17, length)
                    p = tuple(a[i] + (b[i]-a[i])*offset/length for i in (0, 1))
                    q = tuple(a[i] + (b[i]-a[i])*end/length for i in (0, 1))
                    self.draw.line((p, q), fill="#555555", width=3)
            else:
                self.draw.line((a, b), fill="black", width=4)
        if arrow:
            a, b = points[-2:]
            angle = math.atan2(b[1]-a[1], b[0]-a[0])
            corners = [(b[0]-24*math.cos(angle+s*.45), b[1]-24*math.sin(angle+s*.45)) for s in (-1, 1)]
            self.draw.polygon([b, *corners], fill="black")

    def save(self, folder, name):
        self.image.save(folder / f"{name}.png", dpi=(DPI, DPI))
        return {"name": name, "width_px": WIDTH, "print_width_cm": PRINT_CM,
                "dpi": DPI, "text": self.texts}


def create_figures(folder: Path, dotnet_line: float) -> list[dict]:
    folder.mkdir(parents=True, exist_ok=True)
    metadata = []
    fig = Figure(1650)
    xs = [240, 865, 1510, 2160]
    for x in xs:
        fig.path([(x, 190), (x, 1590)], dashed=True, arrow=False)
    events = [(0, 2, "1. Cấp token riêng cho thiết bị"),
              (2, 3, "2. Lưu hash token và hạn"),
              (1, 2, "3. Đăng ký bằng token và DeviceSecret"),
              (2, 3, "4. Tiêu thụ token, lưu danh tính và audit"),
              (2, 1, "5. Trả danh tính thiết bị"),
              (1, 2, "6. Gửi heartbeat ký HMAC"),
              (2, 3, "7. Lưu mẫu theo IdempotencyKey"),
              (0, 2, "8. Gán máy cho Employee")]
    for i, (a, b, _) in enumerate(events):
        y = 350+i*170
        fig.path([(xs[a], y), (xs[b], y)])
    for x, text in zip(xs, ["Admin", "Agent", "Backend", "PostgreSQL"]):
        fig.box((x-210, 40, x+210, 185), text)
    for i, (a, b, text) in enumerate(events):
        # Labels are drawn last over opaque white padding, including lifelines.
        center = (xs[a]+xs[b])/2
        center = min(max(center, 690), 1660)
        fig.text((center, 295+i*170), text)
    metadata.append(fig.save(folder, "enrollment"))

    fig = Figure(1840)
    for points in [[(600, 350), (600, 850)], [(1800, 350), (1800, 850)],
                   [(600, 1130), (600, 1500)], [(1800, 1130), (1800, 1500)]]:
        fig.path(points)
    fig.box((60, 60, 1130, 350), "Web Admin / Employee\nỨng dụng mobile")
    fig.box((1270, 60, 2340, 350), "Agent trên thiết bị\nCore và adapter hệ điều hành")
    fig.box((400, 850, 2000, 1130), "ASP.NET Core API\nApplication và Domain\nInfrastructure")
    fig.box((60, 1500, 1130, 1790), "PostgreSQL\nDữ liệu nghiệp vụ, audit và outbox")
    fig.box((1270, 1500, 2340, 1790), "Worker: cảnh báo, outbox, retention\nTùy chọn: Redis và S3 / MinIO")
    fig.text((600, 580), "HTTPS\nCookie / Bearer; CSRF cho cookie")
    fig.text((1800, 580), "HTTPS\nRequest HMAC; lệnh ký RSA-PSS")
    fig.text((600, 1300), "EF Core")
    fig.text((1800, 1300), "Tác vụ nền và tích hợp")
    metadata.append(fig.save(folder, "architecture"))

    fig = Figure(1830)
    columns = [(40, 740), (850, 1550), (1660, 2360)]
    rows = [(50, 285), (510, 745), (970, 1205), (1430, 1665)]
    texts = [["Organization", "User", "Department"],
             ["DeviceQrLabel", "Device", "Policy /\nPolicyAssignment"],
             ["TelemetrySnapshot", "SelfServiceRequest", "WorkOrder / AssetLoan"],
             ["Command", "CommandResult", "Attachment / Message"]]
    # Each connection stays in a corridor or connects adjacent box edges.
    for a, b in [((740, 168), (850, 168)), ((1550, 168), (1660, 168)),
                 ((1200, 285), (1200, 510)), ((740, 628), (850, 628)),
                 ((1660, 628), (1550, 628)), ((1200, 745), (1200, 970)),
                 ((740, 1548), (850, 1548))]:
        fig.path([a, b])
    fig.path([(1350, 1205), (1350, 1320), (2010, 1320), (2010, 1430)])
    fig.path([(850, 695), (795, 695), (795, 850), (390, 850), (390, 970)])
    fig.path([(1550, 695), (1605, 695), (1605, 850), (2010, 850), (2010, 970)])
    fig.path([(1000, 745), (1000, 805), (10, 805), (10, 1548), (40, 1548)])
    for j, row in enumerate(texts):
        for i, text in enumerate(row):
            fig.box((*columns[i][:1], rows[j][0], columns[i][1], rows[j][1]), text)
    fig.text((1200, 1760), "Quan hệ nghiệp vụ chọn lọc; không thay thế lược đồ khóa ngoại.")
    metadata.append(fig.save(folder, "erd"))

    fig = Figure(1570)
    fig.path([(670, 210), (900, 210)])
    fig.path([(1510, 210), (1740, 210)])
    fig.path([(365, 340), (365, 1010)])
    fig.path([(1205, 340), (1205, 1010)])
    fig.path([(1510, 1140), (2150, 1140), (2150, 340)])
    for rect, text in [((60, 80, 670, 340), "Pending"),
                       ((900, 80, 1510, 340), "Delivered\nLease 30 giây"),
                       ((1740, 80, 2350, 340), "Có biên lai\nThành công / lỗi"),
                       ((60, 1010, 670, 1270), "Expired\nChưa từng giao"),
                       ((900, 1010, 1510, 1270), "ExecutionUnconfirmed\nĐã giao, thiếu kết quả")]:
        fig.box(rect, text)
    fig.text((780, 430), "Poll hợp lệ")
    fig.text((1810, 430), "Biên lai hợp lệ")
    fig.text((365, 700), "Hết hạn")
    fig.text((1205, 700), "Hết hạn thực thi")
    fig.text((2100, 780), "Biên lai\ntrong 7 ngày")
    fig.text((1200, 1440), "Giao lại giữ ID, nonce và chữ ký; chưa bảo đảm exactly-once.")
    metadata.append(fig.save(folder, "command"))

    fig = Figure(1800)
    left, right = 600, 2190
    fig.draw.line([(left, 150), (left, 1500)], fill="black", width=3)
    for tick in range(0, 101, 20):
        x = left+(right-left)*tick/100
        fig.path([(x, 140), (x, 1500)], dashed=tick == 80, arrow=False)
        fig.text((x, 90), f"{tick}%")
    styles = ["black", "#888888", "white"]
    for group, (name, values) in enumerate([(".NET", [dotnet_line, None, None]),
                                          ("Web", [78.09, 60.89, 66.17]),
                                          ("Mobile", [87.96, 75.75, 75.00])]):
        for metric, value in enumerate(values):
            y = 220+group*430+metric*112
            if value is not None:
                x = left+(right-left)*value/100
                fig.draw.rectangle((left, y, x, y+72), fill=styles[metric], outline="black", width=3)
                fig.text((min(x+140, 2280), y+36), f"{value:.2f}%".replace(".", ","))
            else:
                fig.text((1100, y+36), "Không tổng hợp")
        fig.text((270, 340+group*430), name, bold=True)
    for i, (label, fill) in enumerate(zip(["Dòng", "Nhánh", "Hàm"], styles)):
        x = 270+i*710
        fig.draw.rectangle((x, 1550, x+100, 1620), fill=fill, outline="black", width=3)
        fig.text((x+260, 1585), label)
    fig.text((1200, 1720), "Mốc 80%; mỗi bộ đo có tập mã và mẫu số riêng.")
    metadata.append(fig.save(folder, "coverage"))

    fig = Figure(2160)
    fig.draw.rectangle((790, 35, 2370, 2100), outline="black", width=4)
    fig.text((1580, 100), "Hệ thống SentinelLAN", bold=True)
    groups = [
        ("Admin", ["Quản lý thiết bị, tài sản và tem QR", "Phân công và quản lý chính sách", "Phê duyệt, gửi lệnh và xem audit"]),
        ("Employee", ["Xem máy được giao và quét QR", "Báo sự cố và gửi ảnh có đồng ý", "Yêu cầu phần mềm, bảo trì và hẹn IT"]),
        ("Endpoint Agent", ["Đăng ký và gửi telemetry", "Nhận và xác minh lệnh", "Thực thi có giới hạn và gửi biên lai"]),
    ]
    for group, (actor, actions) in enumerate(groups):
        center = 470+group*640
        # UML actors outside the system boundary; associations have no arrowhead.
        fig.draw.ellipse((255, center-135, 345, center-45), outline="black", width=4)
        fig.path([(300, center-45), (300, center+90)], arrow=False)
        fig.path([(190, center+5), (410, center+5)], arrow=False)
        fig.draw.line([(300, center+90), (220, center+190)], fill="black", width=4)
        fig.draw.line([(300, center+90), (380, center+190)], fill="black", width=4)
        fig.text((300, center+245), actor)
        fig.path([(415, center+5), (660, center+5)], arrow=False)
        fig.path([(660, center-175), (660, center+175)], arrow=False)
        for i, action in enumerate(actions):
            y = center-175+i*175
            fig.path([(660, y), (920, y)], arrow=False)
            fig.draw.ellipse((920, y-72, 2300, y+72), outline="black", fill="white", width=4)
            fig.text((1610, y), action)
    metadata.append(fig.save(folder, "use-cases"))
    (folder / "figures.json").write_text(json.dumps(metadata, ensure_ascii=False, indent=2), encoding="utf-8")
    return metadata
