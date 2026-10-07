"""Compatibility entry point for exporting only the blue P1 rider."""
from pathlib import Path
p = Path(__file__).with_name('export_riders.py')
exec(compile(p.read_text(), str(p), 'exec'), {'__file__': str(p), 'RIDERS': [(0, 'BlueRider')]})
