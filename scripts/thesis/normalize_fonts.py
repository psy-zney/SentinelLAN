"""Normalize all four font slots after Word has generated TOC/TOF results."""
from pathlib import Path
import sys

from docx import Document
from docx.oxml import OxmlElement
from docx.oxml.ns import qn

from build_revision import force_font


def normalize(path: Path):
    document = Document(path)
    for part in document.part.package.parts:
        root = getattr(part, "element", None)
        if root is None:
            continue
        for rpr in root.iter(qn("w:rPr")):
            force_font(rpr)
        for run in root.iter(qn("w:r")):
            rpr = run.find(qn("w:rPr"))
            if rpr is None:
                rpr = OxmlElement("w:rPr")
                run.insert(0, rpr)
            force_font(rpr)
    for rpr in document.styles.element.iter(qn("w:rPr")):
        force_font(rpr)
    document.save(path)


if __name__ == "__main__":
    normalize(Path(sys.argv[1]))
