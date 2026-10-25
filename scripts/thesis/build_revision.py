"""Repair the audited report without changing or overwriting its source document.

Run with the Python returned by load_workspace_dependencies. The source must be
the 2026-10-23 edited report, not an unrelated thesis or the older outline.
"""
from __future__ import annotations

import argparse
from collections import Counter
import csv
from copy import deepcopy
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import subprocess
import shutil
import xml.etree.ElementTree as ET

from docx import Document
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Cm, Pt, RGBColor
from docx.text.paragraph import Paragraph

from figures import create_figures
from revision_content import ADDITIONS, PARTIAL_TCS, REFERENCES, TC_PATTERNS, USE_CASES

FONT = "Times New Roman"
NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


def force_font(rpr):
    fonts = rpr.find(qn("w:rFonts"))
    if fonts is None:
        fonts = OxmlElement("w:rFonts")
        rpr.insert(0, fonts)
    for name in list(fonts.attrib):
        if "theme" in name.lower():
            del fonts.attrib[name]
    for name in ("ascii", "hAnsi", "eastAsia", "cs"):
        fonts.set(qn("w:"+name), FONT)


def set_text(paragraph, text):
    formatting = deepcopy(paragraph.runs[0]._r.rPr) if paragraph.runs and paragraph.runs[0]._r.rPr is not None else None
    paragraph.clear()
    run = paragraph.add_run(text)
    if formatting is not None:
        run._r.insert(0, formatting)


def before(anchor, text="", style="Normal"):
    node = OxmlElement("w:p")
    anchor._p.addprevious(node)
    p = Paragraph(node, anchor._parent)
    p.style = style
    p.add_run(text)
    return p


def field(paragraph, instruction, value):
    node = OxmlElement("w:fldSimple")
    node.set(qn("w:instr"), instruction)
    run = OxmlElement("w:r")
    rpr = OxmlElement("w:rPr")
    force_font(rpr)
    run.append(rpr)
    text = OxmlElement("w:t")
    text.text = str(value)
    run.append(text)
    node.append(run)
    paragraph._p.append(node)


def table_style(table, widths, font_size=11):
    table.autofit = False
    for column, width in zip(table.columns, widths):
        column.width = Cm(width)
    pr = table._tbl.tblPr
    tw = pr.find(qn("w:tblW"))
    tw.set(qn("w:type"), "dxa")
    tw.set(qn("w:w"), str(round(sum(widths)*1440/2.54)))
    for old in list(pr.findall(qn("w:tblBorders"))):
        pr.remove(old)
    borders = OxmlElement("w:tblBorders")
    for side in ("top", "left", "bottom", "right", "insideH", "insideV"):
        edge = OxmlElement("w:"+side)
        for k, v in {"val": "single", "sz": "4", "color": "000000"}.items():
            edge.set(qn("w:"+k), v)
        borders.append(edge)
    pr.append(borders)
    for row_index, row in enumerate(table.rows):
        trpr = row._tr.get_or_add_trPr()
        if trpr.find(qn("w:cantSplit")) is None:
            trpr.append(OxmlElement("w:cantSplit"))
        if row_index == 0 and trpr.find(qn("w:tblHeader")) is None:
            trpr.append(OxmlElement("w:tblHeader"))
        for column_index, (cell, width) in enumerate(zip(row.cells, widths)):
            cell.width = Cm(width)
            tcpr = cell._tc.get_or_add_tcPr()
            for shading in list(tcpr.findall(qn("w:shd"))):
                tcpr.remove(shading)
            for paragraph in cell.paragraphs:
                pf = paragraph.paragraph_format
                pf.first_line_indent = Cm(0)
                pf.left_indent = Cm(0)
                pf.line_spacing = 1.05
                pf.space_before = Pt(3)
                pf.space_after = Pt(3)
                pf.keep_with_next = row_index == 0
                paragraph.alignment = WD_ALIGN_PARAGRAPH.LEFT
                for run in paragraph.runs:
                    run.font.size = Pt(font_size)
                    run.font.bold = row_index == 0
                    run.font.color.rgb = RGBColor(0, 0, 0)


def new_table(document, anchor, title, headers, rows, widths):
    cap = before(anchor, "Bảng  "+title, "Caption")
    cap.paragraph_format.keep_with_next = True
    table = document.add_table(rows=1, cols=len(headers))
    for cell, text in zip(table.rows[0].cells, headers):
        cell.text = text
    for values in rows:
        for cell, text in zip(table.add_row().cells, values):
            cell.text = str(text)
    anchor._p.addprevious(table._tbl)
    table_style(table, widths)
    # Keep short use-case/specification tables as a complete readable unit.
    # Long matrices retain repeated headers and can continue on the next page.
    if len(rows) <= 8:
        for index, row in enumerate(table.rows):
            for cell in row.cells:
                for paragraph in cell.paragraphs:
                    paragraph.paragraph_format.keep_with_next = index < len(table.rows)-1
    before(anchor).paragraph_format.space_after = Pt(2)
    return table


def add_figure(anchor, path, title, source):
    p = before(anchor)
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p.paragraph_format.first_line_indent = Cm(0)
    p.paragraph_format.keep_with_next = True
    run = p.add_run()
    run.add_picture(str(path), width=Cm(15.5))
    run._r.xpath(".//wp:docPr")[0].set("descr", title)
    cap = before(anchor, "Hình  "+title, "Caption")
    cap.paragraph_format.keep_with_next = True
    p = before(anchor, source)
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p.paragraph_format.first_line_indent = Cm(0)
    p.paragraph_format.line_spacing = 1.0
    p.paragraph_format.space_after = Pt(6)
    p.runs[0].font.size = Pt(11)
    p.runs[0].italic = True


def evidence(evidence_dir):
    records, files = [], []
    for path in sorted((evidence_dir / "dotnet-final").glob("*.trx")):
        root = ET.parse(path).getroot()
        for result in root.findall(".//t:UnitTestResult", NS):
            records.append({"name": result.attrib["testName"], "outcome": result.attrib["outcome"], "file": path.name})
        files.append({"name": path.name, "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
    if len(records) != 382 or Counter(r["outcome"] for r in records) != {"Passed": 380, "NotExecuted": 2}:
        raise ValueError("Evidence differs from the reviewed 380 passed / 2 skipped run; review all report claims")
    summary = json.loads((evidence_dir / "dotnet-coverage-summary.json").read_text(encoding="utf-8"))
    counts = summary["lines"]
    if round(counts["covered"]*100/counts["total"], 2) != counts["percent"]:
        raise ValueError("Inconsistent .NET coverage summary")
    logs = {}
    for name, expected in [("web-coverage-combined.log", "127 passed"),
                           ("mobile-coverage.log", "195 passed"), ("browser-e2e-final.log", "13 passed")]:
        path = evidence_dir / name
        log = path.read_text(encoding="utf-8", errors="replace")
        if expected not in log:
            raise ValueError(f"Missing expected result in {name}")
        logs[name] = log
        files.append({"name": name, "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
    return records, summary, files, logs


def revise(source: Path, output: Path, evidence_dir: Path, screenshot_manifest: Path | None, measurements: Path | None = None):
    if source.resolve() == output.resolve():
        raise ValueError("Use a separate output file; preserve the original")
    document = Document(source)
    if len(document.tables) != 17 or not any(p.text.startswith("CHƯƠNG 7") and p.style.name == "Heading 1" for p in document.paragraphs):
        raise ValueError("The source is not the audited 17-table report")
    output.parent.mkdir(parents=True, exist_ok=True)
    figures = output.parent / "hinh-anh"
    records, coverage, evidence_files, logs = evidence(evidence_dir)
    create_figures(figures, coverage["lines"]["percent"])
    original_tables = list(document.tables)
    headings = {p.text.split()[0]: p for p in document.paragraphs if p.style.name == "Heading 2"}

    def next_heading(prefix):
        start = headings[prefix]
        # python-docx wrappers are recreated; identify the XML element itself.
        seen = False
        for p in document.paragraphs:
            if p._p is start._p:
                seen = True
            elif seen and p.style.name.startswith("Heading"):
                return p
        raise ValueError(prefix)

    # Keep the original cover ornament and original colored logo.
    cover_lines = list(document.paragraphs[:12])
    faculty = next(p for p in cover_lines if p.text.startswith("KHOA CÔNG NGHỆ"))
    school = before(faculty, "TRƯỜNG CÔNG NGHỆ VÀ THIẾT KẾ")
    school.paragraph_format.alignment = WD_ALIGN_PARAGRAPH.CENTER
    school.paragraph_format.first_line_indent = Cm(0)
    school.paragraph_format.space_after = Pt(4)
    school.paragraph_format.line_spacing = 1
    school.runs[0].font.bold = True
    school.runs[0].font.size = Pt(13)
    for p in cover_lines:
        if p.text.startswith("GVHD"):
            set_text(p, "Giảng viên hướng dẫn: TS. Nguyễn An Tế\nSinh viên thực hiện: Lê Quang Khánh")
        elif p.text.startswith("MSSV"):
            set_text(p, "MSSV: 31231025401")
        elif p.text.startswith("Ngành:"):
            set_text(p, "Ngành: Công nghệ thông tin")
        else:
            continue
        p.paragraph_format.left_indent = Cm(1.8)
        p.paragraph_format.first_line_indent = Cm(0)
        p.alignment = WD_ALIGN_PARAGRAPH.LEFT
        for r in p.runs:
            r.font.size = Pt(14)
    for section in list(document.sections)[1:]:
        section.top_margin = Cm(2.5)
        section.bottom_margin = Cm(2.5)

    replacements = {
        "(Đại học Kinh tế Thành phố Hồ Chí Minh, n.d.)": "(Đại học Kinh tế Thành phố Hồ Chí Minh, 2025)",
        "(Microsoft, n.d.)": "(Microsoft, 2026)",
        "(PostgreSQL Global Development Group, n.d.)": "(PostgreSQL Global Development Group, 2025)",
        "chống CSRF cho mutation": "chống CSRF cho các yêu cầu thay đổi dữ liệu (POST, PUT, PATCH, DELETE)",
        "denominator": "mẫu số (tổng số dòng mã đưa vào đo kiểm)",
        ".NET hợp instrument": "Khối mã .NET được đo kiểm",
        "các dòng đã được instrument": "các dòng mã được gắn bộ thu thập độ phủ",
        "hợp các dòng đã được instrument": "tập hợp các dòng mã được gắn bộ thu thập độ phủ",
        "Trích outcome từ kết quả TRX": "Kết quả thực thi trích từ báo cáo TRX",
        "có outcome Passed": "đạt kết quả Thành công (Passed)",
        "outcome": "kết quả thực thi",
        "mọi assertion web": "mọi phép kiểm tra điều kiện của giao diện web",
        "endpoint máy của tôi": "endpoint /api/v1/my-device (Thiết bị của tôi)",
        "Endpoint máy của tôi": "Endpoint /api/v1/my-device (Thiết bị của tôi)",
        "chưa nghiệm thu": "cần kiểm chứng trong phạm vi tương ứng",
        "local opt-in": "cờ cho phép thực thi cục bộ",
        "opt in": "cờ cho phép thực thi cục bộ",
        "allow-list": "danh sách cho phép",
        "kiểm tra revision": "kiểm tra mã phiên bản bản ghi (RowVersion / Revision)",
        "cần revision": "cần mã phiên bản bản ghi (RowVersion / Revision)",
        "cấu hình instrument": "cấu hình thu thập độ phủ",
        "phát outbox, retention": "đẩy thông báo từ hàng đợi sự kiện và dọn dữ liệu kỹ thuật quá hạn",
        "Tổng hợp hồ sơ kiểm thử được đọc": "Tổng hợp kết quả thực thi các bộ kiểm thử tự động",
        "Độ phủ dòng trong các hồ sơ đã lưu": "Độ phủ dòng, nhánh và hàm theo từng bộ đo",
        "76,05%": str(coverage["lines"]["percent"]).replace(".", ",")+"%",
        "6314/8302": f"{coverage['lines']['covered']}/{coverage['lines']['total']}",
        "6.1 Phạm vi bằng chứng kiểm thử": "6.1 Phương pháp và phạm vi kiểm thử",
    }
    body_texts = list(document.paragraphs)
    body_texts += [p for t in document.tables for row in t.rows for c in row.cells for p in c.paragraphs]
    for p in body_texts:
        if p._p.xpath(".//w:drawing") or p._p.xpath(".//w:instrText") or p._p.xpath(".//w:fldSimple"):
            continue
        text = p.text
        for old, new in replacements.items():
            text = text.replace(old, new)
        text = re.sub(r"\(tài liệu dự án (A\d+)\)", r"(xem Phụ lục A, mục \1)", text)
        if text != p.text:
            set_text(p, text)
    for p in document.paragraphs:
        if p.text.startswith("Tôi chịu trách nhiệm đối với nội dung"):
            set_text(p, "Tôi xin cam đoan đồ án tốt nghiệp về hệ thống quản lý, giám sát và bảo mật thiết bị đầu cuối trong mạng nội bộ SentinelLAN là công trình do tôi thực hiện dưới sự hướng dẫn của TS. Nguyễn An Tế. Các số liệu kiểm thử và tài liệu tham khảo được trình bày có nguồn gốc rõ ràng; kết quả được diễn giải trong phạm vi bằng chứng tương ứng.")
        elif p.text.startswith("Tôi sử dụng OpenAI Codex"):
            set_text(p, "Tôi sử dụng OpenAI Codex để hỗ trợ rà soát tài liệu và mã kiểm thử, biên tập ngôn ngữ, dựng sơ đồ và định dạng Word. Tôi kiểm chứng các nội dung được hỗ trợ và chịu trách nhiệm về báo cáo theo Quyết định 4002/QĐ-ĐHKT-NCPTGKTC ngày 18/12/2025 về liêm chính học thuật và sử dụng AI (Đại học Kinh tế Thành phố Hồ Chí Minh, 2025).")
        elif p.text.startswith("Tôi trân trọng những góp ý chuyên môn"):
            set_text(p, "Tôi xin trân trọng cảm ơn TS. Nguyễn An Tế đã hướng dẫn quá trình thực hiện đồ án SentinelLAN. Sự định hướng chuyên môn của Thầy là cơ sở để tôi xác định phạm vi bài toán quản lý thiết bị, tổ chức nội dung báo cáo và trình bày các quyết định thiết kế theo cách có thể kiểm chứng.")
            anchor = p._p.getnext()
            anchor = Paragraph(anchor, p._parent)
            before(anchor, "Tôi cảm ơn quý Thầy, Cô Khoa Công nghệ thông tin kinh doanh, Trường Công nghệ và Thiết kế, Đại học Kinh tế Thành phố Hồ Chí Minh đã trang bị nền tảng về phát triển phần mềm, mạng máy tính, hệ thống thông tin và an toàn thông tin. Quá trình xây dựng đồ án giúp tôi vận dụng kiến thức đó vào các luồng nghiệp vụ, mô hình dữ liệu và kiểm thử của một hệ thống cụ thể.")
            before(anchor, "Tôi ghi nhận sự hỗ trợ của OpenAI Codex trong việc rà soát tài liệu, chuẩn hóa sơ đồ và hiệu đính cách trình bày. Các công cụ hỗ trợ không thay thế việc đối chiếu mã nguồn, đánh giá bằng chứng và trách nhiệm học thuật của tôi. Những hạn chế trong phạm vi kiểm chứng được trình bày để người đọc có cơ sở đánh giá kết quả đồ án.")
        elif p.text.startswith("Phương pháp đánh giá kết hợp đối chiếu hồ sơ"):
            set_text(p, "Phương pháp đánh giá kết hợp kiểm thử tự động ở các tầng đơn vị, tích hợp, kiến trúc, giao diện web/mobile và trình duyệt. Đợt kiểm thử ngày 23/10/2026 ghi nhận 380/382 ca .NET thành công, hai ca bỏ qua; 127 ca Web UI, 195 ca Mobile và 13 kịch bản E2E thành công. Cổng độ phủ web và mobile còn tiêu chí dưới ngưỡng 80%. Kết quả này kiểm chứng các luồng và điều kiện được thử; số đo hiệu năng và nghiệm thu hành động OS cần được đánh giá riêng.")
        elif "Các kết quả kiểm thử trong Chương 6 được trích" in p.text:
            set_text(p, "Chương 6 trình bày kết quả của đợt kiểm thử tự động ngày 23/10/2026, gồm TRX, kết quả Web/Mobile/E2E và báo cáo độ phủ. Từng nhận định được gắn với loại bằng chứng tương ứng; phiên bản nguồn và tệp kết quả được theo dõi trong danh mục kiểm chứng đi cùng đồ án.")
        elif "scratch/" in p.text and "Chương này" in p.text:
            set_text(p, "Chương này trình bày kết quả thực thi các bộ kiểm thử tự động của SentinelLAN trong đợt ngày 23/10/2026. Bộ .NET lưu từng kết quả trong TRX; các bộ Web UI, Mobile và Browser E2E cung cấp tổng kết kiểm thử và độ phủ riêng. Phụ lục C dẫn tên ca tiêu biểu, Phụ lục D đối chiếu phạm vi bằng chứng của từng kịch bản.")
        elif "không chạy lại các bộ test" in p.text:
            set_text(p, "Kết quả được xác định từ từng ca và tổng kết thực thi, phân biệt thành công, bỏ qua và lỗi cổng độ phủ. Thời điểm kiểm thử được giữ cùng dữ liệu gốc để đối chiếu khi mã nguồn hoặc cấu hình thay đổi.")
        elif "Hồ sơ dấu vân tay và kết quả thô" in p.text:
            set_text(p, "Các mã A1–A7 chỉ tài liệu kỹ thuật của dự án, không phải ấn phẩm học thuật độc lập. Đường dẫn được tính từ gốc kho mã nguồn. Báo cáo kết quả và danh mục minh chứng được tổ chức trong docs/verification/; dữ liệu truy vết chi tiết được lưu riêng, không đưa vào chính văn.")
        elif "tất cả được ghi Chưa nghiệm thu" in p.text:
            set_text(p, "Ma trận dưới đây liên kết kịch bản với kết quả kiểm thử cụ thể. Trạng thái Đạt phần tự động chỉ xác nhận điều kiện được test; các thao tác OS, thử tải dài hạn và phép đo trên thiết bị lab được ghi phạm vi còn cần kiểm chứng. Không suy ra nghiệm thu thực địa từ số ca thành công của một suite.")
        elif p.text.startswith("Hạn chế của đánh giá gồm"):
            set_text(p, "Hạn chế của đánh giá gồm khác biệt giữa đợt kiểm thử ngày 23/10/2026 và cây mã còn thay đổi, hai ca .NET bị bỏ qua và cổng độ phủ web/mobile còn tiêu chí dưới ngưỡng. Bộ ảnh giao diện ngày 24/10/2026 minh họa luồng chạy thật với dữ liệu demo; ảnh không chứng minh telemetry được thu trên một đội thiết bị thực tế. Nghiệm thu hành động OS, PostgreSQL, TLS và khả năng phục hồi dài hạn cần bằng chứng riêng.")
        elif "Danh sách tài liệu dùng định dạng" in p.text:
            set_text(p, "Tài liệu được dẫn theo tác giả và năm. Các trang sản phẩm không công bố ngày sử dụng n.d.; ngày phát hành phiên bản phần mềm không được dùng thay năm xuất bản của mọi trang tài liệu.")

    # Replace all five diagram media in place, preserving their document anchors.
    names = ["enrollment", "architecture", "erd", "command", "coverage"]
    captions = [p for p in document.paragraphs if p.style.name == "Caption" and p.text.startswith("Hình")]
    for caption, name in zip(captions, names):
        prev = caption._p.getprevious()
        while prev is not None and not prev.xpath(".//a:blip"):
            prev = prev.getprevious()
        if prev is None:
            raise ValueError(f"Missing drawing for {name}")
        # The original edited report contains an SVG alternative under asvg:svgBlip.
        # Replacing only the PNG fallback leaves Word rendering the original SVG.
        # Recreate this drawing as a single image so every viewer sees the revision.
        picture_paragraph = Paragraph(prev, document._body)
        picture_paragraph.clear()
        picture_paragraph.alignment = WD_ALIGN_PARAGRAPH.CENTER
        picture_paragraph.paragraph_format.first_line_indent = Cm(0)
        picture_paragraph.paragraph_format.keep_with_next = True
        picture = picture_paragraph.add_run().add_picture(str(figures / (name+".png")), width=Cm(15.5))
        picture._inline.docPr.set("descr", "Sơ đồ đã sửa ngày 24/10/2026: "+name)

    for prefix, paragraphs in ADDITIONS.items():
        anchor = next_heading(prefix)
        if prefix == "3.9":
            before(anchor, "3.10 Quản lý tài sản, bảo trì và tem QR", "Heading 2")
        for text in paragraphs:
            before(anchor, text)
    new_table(document, next_heading("1.5"), "Phạm vi triển khai và điều kiện đánh giá",
              ["Nhóm chức năng", "Phạm vi và điều kiện kiểm chứng"], [
                  ["Quan sát kỹ thuật", "CPU/RAM/đĩa, inventory và trạng thái bảo vệ; dữ liệu có thời điểm, giữ Chưa xác định khi thiếu quan sát."],
                  ["Hỗ trợ nhân viên", "Máy được giao, phiếu hỗ trợ và ảnh do nhân viên chọn; kiểm tra quyền trên cả yêu cầu, trao đổi và nội dung ảnh."],
                  ["Quản trị và tài sản", "Lệnh theo danh sách cho phép, tem QR, cho mượn và bảo trì; phân biệt bản ghi đã duyệt với tác động OS đã xác nhận."],
                  ["Đánh giá vận hành", "Tách bằng chứng tự động, minh họa giao diện và phép đo cục bộ; các điều kiện PostgreSQL, TLS và OS thật cần được kiểm chứng riêng."],
              ], [4.1, 11.4])
    add_figure(next_heading("3.1"), figures / "use-cases.png", "Tác nhân và các nhóm ca sử dụng của SentinelLAN",
               "Nguồn: tác giả tổng hợp từ hợp đồng Application của SentinelLAN.")
    for prefix, code, name, actor, pre, post, flow, alternative, rules in USE_CASES:
        new_table(document, next_heading(prefix), "Đặc tả "+code+" "+name.lower(),
                  ["Thành phần", "Đặc tả"], [["Mã và tên", code+" – "+name], ["Tác nhân", actor],
                  ["Tiền điều kiện", pre], ["Hậu điều kiện", post], ["Luồng chính", flow],
                  ["Ngoại lệ", alternative], ["Quy tắc nghiệp vụ", rules]], [3.2, 12.3])

    measured = None
    if measurements:
        measured = json.loads(measurements.read_text(encoding="utf-8"))
        sample_file = measurements.parent / "process-samples.csv"
        heartbeat_file = measurements.parent / "heartbeats.json"
        with sample_file.open(encoding="utf-8", newline="") as file:
            process_samples = list(csv.DictReader(file))
        heartbeat_samples = json.loads(heartbeat_file.read_text(encoding="utf-8"))
        if measured.get("status") != "measured" or len(process_samples) != measured["metric_samples"] or not heartbeat_samples:
            raise ValueError("Incomplete raw local measurement evidence")
        if len(heartbeat_samples) != measured["successful_heartbeat_requests"]:
            raise ValueError("Heartbeat summary differs from the raw evidence")
        if measured["api_samples_returned"] != measured["unique_successful_keys"]:
            raise ValueError("API sample count does not match the successful distinct heartbeat keys")
        evidence_output = output.parent / "bang-chung"
        evidence_output.mkdir(exist_ok=True)
        for path in [measurements, sample_file, heartbeat_file]:
            shutil.copy2(path, evidence_output / path.name)
        measured["raw_sha256"] = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in [sample_file, heartbeat_file]}
        def replace_section(prefix, texts):
            anchor = next_heading(prefix)
            node = headings[prefix]._p.getnext()
            while node is not None and node is not anchor._p:
                following = node.getnext()
                node.getparent().remove(node)
                node = following
            for text in texts:
                before(anchor, text)
        def vi(value, digits=2):
            return f"{value:.{digits}f}".replace(".", ",")
        replace_section("6.6", [
            f"Ngày 24/10/2026, thử nghiệm cục bộ chạy API và Agent bằng hai tiến trình .NET riêng, với dữ liệu Development/InMemory và kết nối HTTP loopback. Bộ chuyển tiếp trả lỗi 503 trong {measured['outage_seconds_configured']} giây để mô phỏng đường truyền không khả dụng. Tổng thời lượng đo sau khi đăng ký và nhận heartbeat đầu tiên là {vi(measured['duration_seconds'], 1)} giây. Thử nghiệm không ngắt card mạng hay thay đổi firewall.",
            f"Sau khi kết nối được mở lại, quan sát {measured['backfilled_samples_observed']} mẫu có thời điểm thu thập trước lúc phục hồi được gửi bù. Toàn lần chạy có {measured['successful_heartbeat_requests']} yêu cầu heartbeat thành công, {measured['unique_successful_keys']} khóa idempotency khác nhau và {measured['api_samples_returned']} mẫu được API trả về. Số mẫu lưu khớp số khóa thành công; kết quả giới hạn ở lần chạy cục bộ này.",
            "Agent áp dụng thời gian chờ tăng dần khi mất kết nối, vì vậy chu kỳ lấy mẫu trong giai đoạn gián đoạn không giữ cố định năm giây. Không dùng phép chia thời lượng mất mạng cho năm giây để suy ra số mẫu đã xếp hàng. Kết quả kiểm chứng phục hồi qua HTTP trong bộ nhớ; cần thử tiếp trên PostgreSQL, TLS, gián đoạn mạng vật lý và thời lượng dài hơn trước khi kết luận về vận hành thực tế.",
        ])
        replace_section("6.7", [
            f"Cùng lần chạy, công cụ đọc thời gian CPU và bộ nhớ của đúng tiến trình Agent qua API Windows, theo khoảng hai giây. Bộ dữ liệu gồm {measured['metric_samples']} mẫu; thống kê tài nguyên loại ba mươi giây đầu của giai đoạn đo. CPU được chuẩn hóa theo {measured['logical_processors']} bộ xử lý logic. Working set là bộ nhớ vật lý của tiến trình, được tách khỏi tỷ lệ RAM toàn hệ thống trong telemetry.",
            f"Working set quan sát từ {vi(measured['agent_working_set_mib_min'])} đến {vi(measured['agent_working_set_mib_max'])} MiB; CPU trung bình chuẩn hóa là {vi(measured['agent_cpu_all_processors_percent_mean'], 3)}%. Trung vị thời gian từ bộ chuyển tiếp gửi HTTP đến lúc API trả phản hồi heartbeat là {vi(measured['heartbeat_roundtrip_ms_median'])} ms. Giá trị này chưa bao gồm độ trễ từ Agent tạo mẫu tới Dashboard hiển thị và không được diễn giải thành SLA.",
            "Dữ liệu thô gồm process-samples.csv, heartbeats.json và measurements.json trong hồ sơ bằng chứng kèm bản báo cáo; danh mục kiểm chứng nêu cách tái chạy và dấu vân tay tệp. Đây là một phép thử cục bộ trong môi trường có tải nền, dùng để bổ sung số đo có thể đối chiếu. Chưa có cấu hình đối chứng, nhiều thiết bị hoặc nhiều lần lặp để kết luận về khả năng mở rộng hay cải thiện hiệu năng.",
        ])
        new_table(document, next_heading("6.7"), "Kết quả đo cục bộ tiến trình Agent và HTTP heartbeat",
                  ["Chỉ tiêu", "Kết quả và phạm vi"], [
                      ["Môi trường", "Windows; API và Agent riêng; HTTP loopback; Development/InMemory."],
                      ["Gián đoạn mô phỏng", f"{measured['outage_seconds_configured']} giây; trả 503 tại bộ chuyển tiếp."],
                      ["Mẫu gửi bù quan sát", str(measured['backfilled_samples_observed'])],
                      ["Yêu cầu thành công / mẫu lưu", f"{measured['successful_heartbeat_requests']} / {measured['api_samples_returned']}"],
                      ["Working set Agent", f"{vi(measured['agent_working_set_mib_min'])}–{vi(measured['agent_working_set_mib_max'])} MiB"],
                      ["CPU Agent trung bình", f"{vi(measured['agent_cpu_all_processors_percent_mean'], 3)}% trên toàn bộ bộ xử lý logic"],
                      ["HTTP heartbeat trung vị", vi(measured['heartbeat_roundtrip_ms_median'])+" ms; từ bộ chuyển tiếp đến API."],
                  ], [5.2, 10.3])
        for p in document.paragraphs:
            if p.text.startswith("Phương pháp đánh giá kết hợp kiểm thử tự động"):
                set_text(p, p.text.replace("số đo hiệu năng và nghiệm thu hành động OS cần được đánh giá riêng.",
                                         "thử nghiệm cục bộ 5 phút bổ sung số đo tiến trình và phục hồi HTTP. Nghiệm thu OS, PostgreSQL và TLS cần được đánh giá riêng."))

    glossary = original_tables[0]
    for row in glossary.rows:
        if row.cells[0].text == "RSA-PSS":
            row.cells[1].text = "Lược đồ chữ ký số RSA sử dụng cơ chế đệm xác suất (RSA Probabilistic Signature Scheme)."
        elif row.cells[0].text == "RLS":
            row.cells[1].text = "Cơ chế bảo mật phân quyền theo từng dòng dữ liệu (Row-Level Security)."
    for term, meaning in [("AES-GCM", "Chuẩn mã hóa nâng cao ở chế độ Galois/Counter."),
                          ("PBKDF2", "Hàm dẫn xuất khóa dựa trên mật khẩu phiên bản 2."),
                          ("ITAM", "Quản lý tài sản công nghệ thông tin."),
                          ("CMMS", "Hệ thống quản lý bảo trì bằng máy tính."),
                          ("DPAPI", "Giao diện lập trình bảo vệ dữ liệu của Windows."),
                          ("SDDL", "Ngôn ngữ định nghĩa bộ mô tả bảo mật của Windows.")]:
        row = glossary.add_row()
        row.cells[0].text, row.cells[1].text = term, meaning
    table_style(glossary, [3.2, 12.3])
    for table in original_tables[10:12]:
        table_style(table, [6.3, 9.2])
        for row in table.rows[1:]:
            row.cells[0].text = row.cells[0].text.replace(".", ".\u200b")
            for run in row.cells[0].paragraphs[0].runs:
                run.font.size = Pt(10.5)
    table_style(original_tables[9], [1.2, 4.4, 9.9])
    original_tables[9].rows[-1].cells[1].text = "Kết quả kiểm thử tự động"
    original_tables[9].rows[-1].cells[2].text = "docs/verification/; danh mục kết quả TRX, Web UI, Mobile, Browser E2E và độ phủ."
    for row in original_tables[6].rows[1:]:
        row.cells[3].text = row.cells[3].text.replace("Outcome trong TRX.", "Theo kết quả TRX.").replace("Assertion đạt; lệnh coverage thất bại.", "Các phép kiểm tra đạt; cổng độ phủ chưa đạt.")
        if row.cells[0].text in ("Web UI", "Mobile", "Browser E2E"):
            row.cells[2].text = "0"
        for cell in row.cells[1:3]:
            for p in cell.paragraphs:
                p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    table_style(original_tables[6], [4.3, 1.5, 2.0, 7.7])
    for row in original_tables[6].rows[1:]:
        for cell in row.cells[1:3]:
            cell.paragraphs[0].alignment = WD_ALIGN_PARAGRAPH.CENTER
    original_tables[7].rows[1].cells[1].text = f"{coverage['lines']['percent']:.2f}%".replace(".", ",")
    original_tables[7].rows[1].cells[4].text = f"{coverage['lines']['covered']}/{coverage['lines']['total']} dòng trong tập mã đo kiểm."
    table_style(original_tables[7], [3.6, 2.0, 2.1, 2.0, 5.8])
    for row in original_tables[7].rows[1:]:
        for cell in row.cells[1:4]:
            cell.paragraphs[0].alignment = WD_ALIGN_PARAGRAPH.CENTER
    if measured:
        for row in original_tables[8].rows[1:]:
            if "Hiệu năng" in row.cells[0].text:
                row.cells[1].text = "Đo cục bộ 300 giây; 150 mẫu tài nguyên, 20 heartbeat và 5 mẫu gửi bù."
                row.cells[2].text = "Cần kiểm chứng PostgreSQL/TLS, mạng vật lý, nhiều máy và dài hạn."
            elif "Web và hỗ trợ" in row.cells[0].text:
                row.cells[1].text = "Kiểm thử giao diện, E2E và tám ảnh demo ngày 24/10/2026."
    table_style(original_tables[8], [4.0, 6.0, 5.5])
    original_tables[12].rows[0].cells[1].text = "Kết quả"
    for row in original_tables[12].rows[1:]:
        if row.cells[1].text == "Passed":
            row.cells[1].text = "Thành công (Passed)"
        elif row.cells[1].text == "NotExecuted":
            row.cells[1].text = "Bỏ qua (NotExecuted)"
    table_style(original_tables[12], [12.5, 3.0], 10.5)

    mappings = []
    for row in original_tables[13].rows[1:]:
        code = row.cells[0].text
        pattern = TC_PATTERNS.get(code)
        found = [r for r in records if pattern and pattern in r["name"] and r["outcome"] == "Passed"]
        if pattern and not found:
            raise ValueError(f"No passing test supports {code}: {pattern}")
        state = "Đạt phần tự động"
        note = ""
        if code in PARTIAL_TCS:
            note = "Cần kiểm chứng các điều kiện còn lại của kịch bản."
        if code == "TC19":
            found = [{"name": "office employee confirms panic, previews evidence and sees command outcome separately", "outcome": "Passed", "file": "browser-e2e-final.log"}]
            if found[0]["name"] not in logs["browser-e2e-final.log"]:
                raise ValueError("TC19 browser evidence is missing")
            note = "Bằng chứng giao diện trên dữ liệu fixture; ảnh do người dùng chọn."
        elif code == "TC24":
            state = "Đạt điều kiện adapter"
            note = "Chưa có biên bản tác động và khôi phục mạng trên OS thật."
        elif code == "TC25":
            state = "Đạt cơ chế hàng đợi"
            note = "Kiểm thử tự động; còn thiếu bộ đo mất kết nối thực địa."
        elif code == "TC26":
            state = "Cần đo trên máy lab"
            note = "Chưa có dữ liệu CPU/RAM tiến trình và phân bố độ trễ."
        if measured and code in ("TC25", "TC26"):
            state = "Đã đo cục bộ"
            note = "HTTP loopback và Development/InMemory; chưa phải nghiệm thu PostgreSQL, TLS hoặc mạng vật lý."
            found.append({"name": "Thử nghiệm API và Agent riêng, 5 phút", "outcome": "Measured", "file": "bang-chung/measurements.json", "kind": "measurement"})
        row.cells[2].text = state+". "+note
        if found:
            short = found[0]["name"].split(".")[-1]
            row.cells[1].text += "\nMinh chứng: "+re.sub(r"(?<=[a-z])(?=[A-Z])", " ", short)
        mappings.append({"code": code, "status": state, "scope_note": note, "evidence": found})
    # State the actual enrollment recovery semantics rather than a blanket retry refusal.
    original_tables[13].rows[2].cells[1].text = "Token dùng lại: không tạo máy thứ hai; đúng bằng chứng được phục hồi trong cửa sổ bootstrap.\nMinh chứng: Stale Enrollment Context Cannot Consume Token Again."
    table_style(original_tables[13], [1.3, 9.2, 5.0], font_size=10.5)

    endpoints = original_tables[15]
    endpoint_rows = [
        ("POST /api/v1/auth/login", "Đăng nhập theo tổ chức; cookie phiên và chính sách giới hạn thử đăng nhập."),
        ("POST /api/v1/auth/refresh", "Xoay refresh token; phát lại token cũ thu hồi chuỗi phiên."),
        ("POST /api/v1/auth/activate", "Kích hoạt tài khoản bằng token một lần còn hạn."),
        ("POST /api/v1/agent/enroll", "Tiêu thụ token riêng; phục hồi bằng chứng gốc trong cửa sổ bootstrap."),
        ("POST /api/v1/agent/heartbeat", "HMAC, nonce và khóa idempotency theo thiết bị."),
        ("POST /api/v1/agent/commands/poll", "Máy chỉ nhận lệnh hợp lệ của mình theo lease."),
        ("POST /api/v1/agent/commands/{id}/result", "Biên lai gắn lệnh, kiểm tra quyền máy và không ghi đè khi retry."),
        ("GET /api/v1/qr/{code}/public", "Thẻ tài sản công khai tối giản, không lộ danh tính người dùng."),
        ("GET /api/v1/qr/{code}", "Tra QR có xác thực; kiểm tra tenant, vai trò và phân công."),
        ("GET /api/v1/my-device", "Employee đọc thiết bị được giao."),
        ("POST /api/v1/self-service/requests", "Tạo yêu cầu; máy đích lấy từ phân công của Employee."),
        ("POST /api/v1/self-service/requests/{id}/decision", "Admin duyệt; OTP bảo trì 8 số khi phù hợp."),
        ("POST /api/v1/self-service/requests/{id}/redeem", "Tiêu thụ OTP còn hạn và xếp một lệnh bảo trì đã ký."),
        ("GET /api/v1/host/status", "Snapshot máy chủ chỉ đọc cho vai trò vận hành được cho phép."),
    ]
    for row in list(endpoints.rows)[1:]:
        endpoints._tbl.remove(row._tr)
    for route, meaning in endpoint_rows:
        row = endpoints.add_row()
        row.cells[0].text, row.cells[1].text = route.replace("/", "/\u200b"), meaning
    table_style(endpoints, [7.3, 8.2], 10.5)
    # Keep the operating instructions together and make the verification steps explicit.
    appendix_e1 = next(p for p in document.paragraphs if p.text.startswith("E.1 ") and p.style.name == "Heading 2")
    appendix_e2 = next(p for p in document.paragraphs if p.text.startswith("E.2 ") and p.style.name == "Heading 2")
    appendix_e1.paragraph_format.page_break_before = True
    node = appendix_e1._p.getnext()
    while node is not appendix_e2._p:
        following = node.getnext()
        node.getparent().remove(node)
        node = following
    appendix_f_heading = next(p for p in document.paragraphs if p.style.name == "Heading 1" and p.text.startswith("PHỤ LỤC F"))
    node = appendix_e2._p.getnext()
    while node is not appendix_f_heading._p:
        following = node.getnext()
        node.getparent().remove(node)
        node = following
    before(appendix_e2, "Dựng môi trường thử tách biệt; ghi phiên bản ứng dụng, chu kỳ lấy mẫu và loại cơ sở dữ liệu. Cấu hình secret ngoài mã nguồn, TLS và khóa xác minh lệnh phù hợp. Sau khi API hoạt động, tạo token có hạn riêng cho máy lab; Agent lưu bí mật trước khi đăng ký. Chỉ gán Employee cùng tổ chức. Không đưa token, OTP, cookie hoặc khóa vào ảnh minh chứng.")
    before(appendix_e2, "Kiểm tra luồng đọc trước thao tác thay đổi: đăng nhập đúng vai trò, mở máy được giao và đối chiếu thời điểm quan sát. Với hỗ trợ, dùng dữ liệu demo và ảnh được chọn chủ động; kiểm tra bước xem trước và đồng ý gửi. Phân biệt lệnh đã xếp hàng, đã giao và có biên lai. Không dùng thao tác khóa hoặc cô lập trên máy làm việc để lấy ảnh.")

    # Replace references as a block, preserving the section structure.
    ref_heading = next(p for p in document.paragraphs if p.style.name == "Heading 1" and p.text == "TÀI LIỆU THAM KHẢO")
    next_top = ref_heading._p.getnext()
    while next_top is not None and not (next_top.tag == qn("w:p") and Paragraph(next_top, ref_heading._parent).style.name == "Heading 1"):
        old = next_top
        next_top = next_top.getnext()
        old.getparent().remove(old)
    ref_anchor = Paragraph(next_top, ref_heading._parent)
    for author, year, title, url in REFERENCES:
        p = before(ref_anchor)
        p.paragraph_format.left_indent = Cm(1.27)
        p.paragraph_format.first_line_indent = Cm(-1.27)
        p.paragraph_format.line_spacing = 1.05
        p.paragraph_format.space_after = Pt(5)
        p.paragraph_format.keep_together = True
        p.add_run(f"{author}. ({year}). ")
        p.add_run(title).italic = True
        p.add_run(". "+url)
    # Remove the editorial image checklist, retaining the final section properties.
    appendix_f = next(p for p in document.paragraphs if p.style.name == "Heading 1" and p.text.startswith("PHỤ LỤC F"))
    node = appendix_f._p
    while node is not None and node.tag != qn("w:sectPr"):
        following = node.getnext()
        node.getparent().remove(node)
        node = following
    document.add_paragraph("Thu hồi qua chức năng có quyền, lý do và xác nhận. Kiểm tra credential cũ không còn gửi heartbeat hoặc nhận lệnh; đối chiếu quyền xem máy và yêu cầu hỗ trợ trước đó. Ghi thời điểm, chủ thể và kết quả API cùng audit. Phản hồi từ chối là kết quả mong đợi của ca sai quyền, không phải bằng chứng luồng hợp lệ bị lỗi.")
    document.add_paragraph("Khôi phục cần đúng bản sao lưu, migration và khóa mã hóa. Kiểm tra khả năng đọc dữ liệu cùng quan hệ tenant sau phục hồi. Với cô lập mạng, phải xác nhận Scheduled Task cứu hộ trước khi áp rule. Thử lưu lượng và khôi phục thực tế chỉ trên máy lab được cấp quyền, có người vận hành và đường phục hồi cục bộ.")
    document.add_heading("E.3 Hồ sơ kết quả cần giữ", level=2)
    document.add_paragraph("Mỗi lần thử lưu thời gian UTC, phiên bản nguồn, loại cơ sở dữ liệu và quyền nền tảng. Giữ mẫu đo gốc, phương pháp tính, giới hạn và thông tin nguồn ảnh. Phân biệt kết quả tự động, phép đo HTTP cục bộ và biên bản tác động OS; không suy ra nghiệm thu thực địa chỉ từ ảnh hoặc số ca kiểm thử thành công.")
    screenshots = []
    if screenshot_manifest:
        screenshots = json.loads(screenshot_manifest.read_text(encoding="utf-8"))
        for shot in screenshots:
            path = screenshot_manifest.parent / shot["file"]
            if not path.is_file() or not shot.get("captured_at_utc"):
                raise ValueError("Screenshot provenance is incomplete")
            anchor = next_heading(shot.get("section", "5.3"))
            add_figure(anchor, path, shot["title"], "Nguồn: giao diện SentinelLAN chạy cục bộ ngày 24/10/2026, dữ liệu demo; "+shot["role"]+".")
        before(next_heading("5.3"), "Các hình trong mục này được chụp từ giao diện chạy với API cục bộ và dữ liệu Development/InMemory ngày 24/10/2026. Ảnh chi tiết thiết bị giữ trạng thái Chưa xác định khi Agent chưa cung cấp quan sát; các thông số seed chỉ minh họa cách trình bày. Form lệnh thể hiện yêu cầu lý do, thời hạn và xác nhận trước khi gửi; ảnh nhật ký ghi nhận yêu cầu hỗ trợ được tạo trong phiên demo.")
        before(next_heading("5.4"), "Nhóm hình cổng nhân viên minh họa thiết bị được giao, mô tả mức gián đoạn công việc và bước chọn ảnh. Ở ảnh xem trước, ô đồng ý còn bỏ chọn nên nút Gửi ảnh bị khóa. Hành vi này yêu cầu nhân viên chủ động kiểm tra nội dung trước khi chia sẻ với IT; việc chọn ảnh không đồng nghĩa với đã tải ảnh lên máy chủ.")
    else:
        before(next_heading("5.4"), "Các luồng giao diện được mô tả theo triển khai và kiểm thử tự động. Phạm vi minh chứng hiện tại chưa bao gồm bộ ảnh chụp giao diện đã đối chiếu trên môi trường nghiệm thu.")

    # Chapter captions use the same native SEQ labels as the existing figure lists.
    chapter = None
    counters = Counter()
    caption_map = []
    for p in list(document.paragraphs):
        if p.style.name == "Heading 1":
            match = re.match(r"CHƯƠNG (\d+)|PHỤ LỤC ([A-F])", p.text)
            chapter = next((x for x in match.groups() if x), None) if match else None
        if p.style.name != "Caption":
            continue
        label = "Hình" if p.text.startswith("Hình") else "Bảng"
        title = re.sub(r"^(?:Bảng|Hình)\s*(?:\d+[.:]?\s*)?", "", p.text).strip()
        for old, new in replacements.items():
            title = title.replace(old, new)
        if not chapter:
            # Front-matter glossary does not belong to a numbered chapter.
            p._p.getparent().remove(p._p)
            continue
        if "Kế hoạch TC01" in title:
            title = "Ma trận kịch bản kiểm chứng chức năng và an toàn (TC01–TC26)"
        counters[(chapter, label)] += 1
        number = counters[(chapter, label)]
        p.clear()
        p.add_run(f"{label} {chapter}.")
        reset = " \\r 1" if number == 1 else ""
        field(p, f" SEQ {label}{reset} \\* ARABIC ", number)
        p.add_run(": "+title)
        p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        pf = p.paragraph_format
        pf.first_line_indent = Cm(0)
        pf.left_indent = Cm(0)
        pf.space_before, pf.space_after = Pt(4), Pt(2)
        pf.line_spacing = 1.05
        pf.keep_with_next = True
        caption_map.append({"label": label, "chapter": chapter, "number": number, "title": title})
        if title == "Trường dữ liệu cho phép đo thực địa":
            pf.page_break_before = True
    for p in document.paragraphs:
        if p.text.startswith("Nguồn:"):
            p.alignment = WD_ALIGN_PARAGRAPH.CENTER
            p.paragraph_format.first_line_indent = Cm(0)
            p.paragraph_format.line_spacing = 1.05
            p.paragraph_format.space_before = Pt(0)
            p.paragraph_format.space_after = Pt(5)
        # Fixing field styles must also clear inherited first-line indents.
        name = p.style.name.lower()
        if name in ("toc 1", "toc 2", "toc 3", "table of figures"):
            depth = {"toc 1": 0, "toc 2": .5, "toc 3": 1}.get(name, 0)
            p.paragraph_format.left_indent = Cm(depth)
            p.paragraph_format.first_line_indent = Cm(0)
    for style in document.styles:
        if style.type in (1, 2):
            force_font(style.element.get_or_add_rPr())
        name = style.name.lower()
        if name in ("toc 1", "toc 2", "toc 3", "table of figures"):
            pf = style.paragraph_format
            pf.left_indent = Cm({"toc 2": .5, "toc 3": 1}.get(name, 0))
            pf.first_line_indent = Cm(0)
            pf.line_spacing = 1
            pf.space_before = Pt(4 if name == "toc 1" else 0)
            pf.space_after = Pt(2)
            style.font.size = Pt(13 if name == "toc 1" else 12)
            style.font.bold = name == "toc 1"
    for part in document.part.package.parts:
        element = getattr(part, "element", None)
        if element is None:
            continue
        for run in element.iter(qn("w:r")):
            rpr = run.find(qn("w:rPr"))
            if rpr is None:
                rpr = OxmlElement("w:rPr")
                run.insert(0, rpr)
            force_font(rpr)
    defaults = document.styles.element.find(qn("w:docDefaults"))
    for rpr in defaults.iter(qn("w:rPr")):
        force_font(rpr)
    document.core_properties.comments = "Bản sửa ngày 24/10/2026; bằng chứng và giới hạn được lưu trong hồ sơ riêng."
    document.core_properties.subject = "Đồ án tốt nghiệp SentinelLAN"
    update = document.settings.element.find(qn("w:updateFields"))
    if update is not None:
        update.set(qn("w:val"), "false")  # render_report.ps1 refreshes fields before delivery
    document.save(output)
    report = {"source_sha256": hashlib.sha256(source.read_bytes()).hexdigest(),
              "created_at_utc": datetime.now(timezone.utc).isoformat(),
              "source_revision": subprocess.check_output(["git", "rev-parse", "HEAD"], text=True).strip(),
              "source_worktree": "dirty; evidence is the run of 2026-10-23, not a claim of a new full application run",
              "dotnet": dict(Counter(r["outcome"] for r in records)), "coverage": coverage,
              "evidence_files": evidence_files, "scenarios": mappings, "captions": caption_map,
              "screenshots": screenshots, "local_measurement": measured,
              "publication_blockers": ["TC24: nghiệm thu hành động OS và khôi phục trên máy lab",
                                       "PostgreSQL, TLS, mạng vật lý và vận hành dài hạn cần nghiệm thu riêng"]
                                       if measured else ["TC24: nghiệm thu hành động OS và khôi phục trên máy lab", "TC25: bộ đo mất mạng thực địa", "TC26: CPU/RAM tiến trình và phân bố độ trễ"]}
    (output.parent / "revision-evidence.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"output": str(output), "tables": len(document.tables), "figures": sum(c['label'] == 'Hình' for c in caption_map), "screenshots": len(screenshots)}, ensure_ascii=False))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--evidence-dir", type=Path, required=True)
    parser.add_argument("--screenshots", type=Path)
    parser.add_argument("--measurements", type=Path)
    args = parser.parse_args()
    revise(args.source, args.output, args.evidence_dir, args.screenshots, args.measurements)
