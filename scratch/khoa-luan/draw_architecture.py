from PIL import Image, ImageDraw, ImageFont
from pathlib import Path
import math

ROOT = Path(__file__).resolve().parent
W, H = 2600, 1680
im = Image.new('RGB', (W, H), 'white')
d = ImageDraw.Draw(im)
FONT = 'C:/Windows/Fonts/arial.ttf'
BOLD = 'C:/Windows/Fonts/arialbd.ttf'

def font(size, bold=False):
    return ImageFont.truetype(BOLD if bold else FONT, size)

def text_center(lines, cx, cy, size=48, bold=False, fill='#202020', spacing=14):
    if isinstance(lines, str):
        lines = lines.split('\n')
    f = font(size, bold)
    heights = [d.textbbox((0, 0), s, font=f)[3] - d.textbbox((0, 0), s, font=f)[1] for s in lines]
    total = sum(heights) + spacing * (len(lines) - 1)
    y = cy - total / 2
    for s, height in zip(lines, heights):
        box = d.textbbox((0, 0), s, font=f)
        d.text((cx - (box[2] - box[0]) / 2, y - box[1]), s, font=f, fill=fill)
        y += height + spacing

def box(bounds, title, lines=None, fill='#ffffff', title_size=51, body_size=44):
    x0, y0, x1, y1 = bounds
    d.rounded_rectangle(bounds, radius=22, fill=fill, outline='#303030', width=5)
    cx = (x0 + x1) / 2
    if lines:
        text_center(title, cx, y0 + 62, title_size, bold=True)
        text_center(lines, cx, (y0 + y1) / 2 + 45, body_size)
    else:
        text_center(title, cx, (y0 + y1) / 2, title_size, bold=True)

def arrow(points, color='#303030', width=6, dashed=False, start_head=False):
    def head(a, b):
        ax, ay = a; bx, by = b
        angle = math.atan2(by - ay, bx - ax)
        size = 23
        wing = 11
        backx = bx - size * math.cos(angle)
        backy = by - size * math.sin(angle)
        pts = [(bx, by), (backx + wing * math.sin(angle), backy - wing * math.cos(angle)),
               (backx - wing * math.sin(angle), backy + wing * math.cos(angle))]
        d.polygon(pts, fill=color)
    for a, b in zip(points[:-1], points[1:]):
        if dashed:
            dx, dy = b[0]-a[0], b[1]-a[1]
            length = math.hypot(dx, dy)
            for pos in range(0, int(length), 24):
                end = min(pos + 13, length)
                d.line([(a[0]+dx*pos/length, a[1]+dy*pos/length),
                        (a[0]+dx*end/length, a[1]+dy*end/length)], fill=color, width=width)
        else:
            d.line([a,b], fill=color, width=width)
    head(points[-2], points[-1])
    if start_head:
        head(points[1], points[0])

def label(lines, cx, cy, size=40, fill='#333333', background=True):
    if background:
        parts = lines.split('\n') if isinstance(lines, str) else lines
        f = font(size)
        dimensions = [d.textbbox((0,0), s, font=f) for s in parts]
        width = max(b[2]-b[0] for b in dimensions)
        height = sum(b[3]-b[1] for b in dimensions) + 10*(len(parts)-1)
        d.rectangle((cx-width/2-9,cy-height/2-7,cx+width/2+9,cy+height/2+7), fill='white')
    text_center(lines, cx, cy, size=size, fill=fill, spacing=10)

text_center('KIẾN TRÚC TỔNG THỂ SENTINELLAN', W / 2, 70, 66, bold=True)
text_center('Giao tiếp qua API và phân chia trách nhiệm trong Backend', W / 2, 145, 44, fill='#505050')

# The container represents the backend composition; it is not a network boundary.
d.rounded_rectangle((875, 220, 1815, 1340), radius=30, fill='#fafafa', outline='#777777', width=4)
label('BACKEND ASP.NET CORE', 1345, 270, 48, background=False)

box((70, 345, 590, 590), ['Dashboard quản trị', 'và cổng nhân viên'], title_size=48)
box((980, 345, 1690, 590), 'API', ['Xác thực • Kiểm tra quyền', 'Điều phối yêu cầu'], fill='#eeeeee', title_size=56, body_size=43)
box((2110, 345, 2530, 590), 'Agent', ['Trên thiết bị', 'Windows'], title_size=54, body_size=45)

# User clients use HTTP for operations, SignalR only for UI notifications.
arrow([(590, 410), (980, 410)])
label(['HTTPS', 'Yêu cầu nghiệp vụ'], 772, 337, 39)
arrow([(980, 530), (590, 530)])
label(['SignalR', 'Cập nhật giao diện'], 772, 630, 39)

# Agent communication is a separate HTTP channel. Command delivery is polling.
arrow([(1690, 440), (2110, 440)], start_head=True)
label(['HTTPS qua API', 'Đăng ký • Heartbeat', 'Telemetry'], 1900, 335, 39)
label(['Polling lệnh', 'Gửi biên lai', 'Phản hồi HTTP'], 1900, 590, 39)

box((980, 760, 1690, 965), 'Application', ['Ca sử dụng • Phân quyền', 'Hợp đồng hạ tầng'], fill='#eeeeee', title_size=52, body_size=43)
arrow([(1340, 590), (1340, 760)])
label('Gọi ca sử dụng', 1535, 683, 39, background=False)

box((980, 1110, 1280, 1300), 'Domain', ['Quy tắc', 'nghiệp vụ'], title_size=50, body_size=44)
box((1420, 1110, 1770, 1300), 'Infrastructure', ['Triển khai', 'các hợp đồng'], title_size=42, body_size=43)
arrow([(1130, 965), (1130, 1110)])
label(['Áp dụng', 'quy tắc'], 1000, 1040, 37, background=False)
arrow([(1500, 965), (1500, 1110)])
label(['Gọi qua', 'hợp đồng'], 1390, 1040, 37, background=False)
# Implementation dependency points back to Application-owned contracts.
arrow([(1730, 1110), (1730, 860), (1690, 860)], dashed=True, color='#777777', width=4)
label(['Triển khai', 'hợp đồng'], 1910, 1002, 39, fill='#555555')

# PostgreSQL cylinder, only accessed through the infrastructure adapter.
db = (1420, 1380, 1770, 1510)
x0,y0,x1,y1 = db
d.rectangle((x0, y0+20, x1, y1-20), fill='white', outline='#303030', width=5)
d.ellipse((x0, y1-40, x1, y1), fill='white', outline='#303030', width=5)
d.rectangle((x0+5, y0+24, x1-5, y1-22), fill='white')
d.line((x0,y0+20,x0,y1-20), fill='#303030', width=5)
d.line((x1,y0+20,x1,y1-20), fill='#303030', width=5)
d.ellipse((x0, y0, x1, y0+40), fill='#f4f4f4', outline='#303030', width=5)
text_center('PostgreSQL', (x0+x1)/2, y0+78, 45, bold=True)
arrow([(1595,1300),(1595,1380)])

# Short notes preserve legibility without adding another unverified platform.
d.rounded_rectangle((70, 875, 750, 1215), radius=20, fill='#f8f8f8', outline='#999999', width=3)
text_center('RANH GIỚI TRUY CẬP', 410, 930, 45, bold=True)
text_center(['Dashboard và Agent trao đổi', 'dữ liệu thông qua API.', 'PostgreSQL chỉ được truy cập', 'qua Infrastructure.'], 410, 1080, 42)

d.rounded_rectangle((2060, 875, 2530, 1250), radius=20, fill='#f8f8f8', outline='#999999', width=3)
text_center('AGENT', 2295, 930, 46, bold=True)
text_center(['Core độc lập nền tảng.', 'Adapter hệ điều hành', 'nằm trong Agent', 'Infrastructure.'], 2295, 1090, 42)

d.line((70, 1570, 2530, 1570), fill='#c0c0c0', width=3)
arrow([(110, 1622), (260, 1622)], width=5)
label('Trao đổi hoặc gọi xử lý', 572, 1622, 40)
arrow([(1040, 1622), (1190, 1622)], dashed=True, color='#777777', width=4)
label('Quan hệ triển khai hợp đồng', 1534, 1622, 40)

path = ROOT / 'architecture.png'
im.save(path, dpi=(300,300))
print(path)
