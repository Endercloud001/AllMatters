"""Compatibility entry point: rebuild current-rule input pools and witnesses."""
from pathlib import Path
import subprocess,sys
subprocess.run([sys.executable,'-X','utf8',str(Path(__file__).with_name('refresh-rooms.py'))],check=True)
