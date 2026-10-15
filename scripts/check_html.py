#!/usr/bin/env python3
"""Validate the standalone tutorial and demo, including their embedded JavaScript."""
from html.parser import HTMLParser
from pathlib import Path
import subprocess
import tempfile

class Scripts(HTMLParser):
    def __init__(self):
        super().__init__()
        self.in_script = False
        self.scripts = []
    def handle_starttag(self, tag, attrs):
        if tag == "script":
            self.in_script = True
            self.scripts.append("")
    def handle_endtag(self, tag):
        if tag == "script":
            self.in_script = False
    def handle_data(self, data):
        if self.in_script:
            self.scripts[-1] += data

for name in ("index.html", "tutorial/index.html"):
    content = Path(name).read_text(encoding="utf-8")
    parser = Scripts()
    parser.feed(content)
    if not parser.scripts:
        raise SystemExit(f"{name}: no inline scripts found")
    for i, code in enumerate(parser.scripts):
        with tempfile.NamedTemporaryFile(suffix=".js", mode="w", encoding="utf-8") as f:
            f.write(code)
            f.flush()
            subprocess.run(["node", "--check", f.name], check=True)
    print(f"{name}: {len(parser.scripts)} inline JS block(s) syntax-checked")
