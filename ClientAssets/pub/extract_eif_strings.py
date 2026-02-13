# save as extract_eif_strings.py and run where the file is accessible
import os
path = "dat001.eif"
outpath = "dat001_text_segments.txt"

def decode_chunk(b):
    try:
        return b.decode("windows-1254", errors="replace")
    except:
        return b.decode("latin1", errors="replace")

with open(path, "rb") as f:
    data = f.read()

segments = data.split(b'\xfe')
with open(outpath, "w", encoding="utf-8") as out:
    for i, seg in enumerate(segments):
        s = decode_chunk(seg)
        # keep only segments that have a decent amount of printable characters
        printable = sum(1 for ch in s if ch.isprintable() and ch != '\x00')
        if printable >= 4:
            out.write(f"--- SEGMENT {i} (len {len(seg)}) ---\n")
            out.write(s + "\n\n")

print("Wrote candidate segments to:", outpath)