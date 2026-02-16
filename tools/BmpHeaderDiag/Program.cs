using System;
using System.Collections.Generic;
using System.IO;
using PELoaderLib;

// Analyze BMP headers from EGF resources to diagnose pixel offset issues
// Usage: dotnet run -- <path-to-egf> [resource_id] [resource_id] ...
// If no resource IDs given, scans first 200 resources

var args2 = args;
if (args2.Length < 1)
{
    Console.WriteLine("Usage: BmpHeaderDiag <path-to-egf> [resource_id ...]");
    Console.WriteLine("  e.g.: BmpHeaderDiag ../../ClientAssets/gfx/gfx021.egf 100 101 102");
    return;
}

var egfPath = args2[0];
if (!File.Exists(egfPath))
{
    Console.WriteLine($"File not found: {egfPath}");
    return;
}

using var pe = new PEFile(egfPath);
pe.Initialize();

var resourceIds = new List<int>();
if (args2.Length > 1)
{
    for (int i = 1; i < args2.Length; i++)
        if (int.TryParse(args2[i], out var id))
            resourceIds.Add(id);
}
else
{
    // Scan first 500 resources
    for (int id = 100; id <= 600; id++)
        resourceIds.Add(id);
}

// Compression type stats
var stats = new Dictionary<string, int>
{
    ["BI_RGB (0)"] = 0,
    ["BI_BITFIELDS (3)"] = 0,
    ["Other"] = 0
};
var headerSizeStats = new Dictionary<int, int>();
var offsetStats = new Dictionary<int, int>();
int found = 0;

Console.WriteLine($"Scanning {Path.GetFileName(egfPath)}...");
Console.WriteLine("─────────────────────────────────────────────────────────────────────────");
Console.WriteLine($"{"ID",5} {"W",6} {"H",6} {"BPP",4} {"HdrSz",6} {"Compress",10} {"bfOffBits",10} {"bfSize",10} {"Issue?",8}");
Console.WriteLine("─────────────────────────────────────────────────────────────────────────");

foreach (var resId in resourceIds)
{
    ReadOnlyMemory<byte> data;
    try
    {
        data = pe.GetEmbeddedBitmapResourceByID(resId);
    }
    catch (ArgumentException)
    {
        continue; // resource not found
    }

    if (data.Length < 54)
        continue;

    var span = data.Span;

    // Check BMP magic
    if (span[0] != 0x42 || span[1] != 0x4D)
    {
        Console.WriteLine($"{resId,5} NOT A BMP (magic: 0x{span[0]:X2}{span[1]:X2})");
        continue;
    }

    found++;

    var bfSize = BitConverter.ToInt32(span.Slice(2, 4));
    var bfOffBits = BitConverter.ToInt32(span.Slice(10, 4));
    var infoHeaderSize = BitConverter.ToInt32(span.Slice(14, 4));
    var width = BitConverter.ToInt32(span.Slice(18, 4));
    var height = BitConverter.ToInt32(span.Slice(22, 4));
    var bpp = BitConverter.ToInt16(span.Slice(28, 2));
    var compression = BitConverter.ToInt32(span.Slice(30, 4));

    string compStr = compression switch
    {
        0 => "BI_RGB",
        3 => "BI_BITFLDS",
        _ => $"Other({compression})"
    };

    // Check for the issue: BI_BITFIELDS with 40-byte header but bfOffBits=54
    string issue = "";
    if (compression == 3 && infoHeaderSize == 40 && bfOffBits == 54)
        issue = "BAD_OFF";  // bfOffBits should be 66
    else if (compression == 3 && infoHeaderSize == 40 && bfOffBits == 66)
        issue = "OK_FIXED";
    else if (compression == 3 && infoHeaderSize >= 56)
        issue = "V3_BF";
    else if (compression == 0)
        issue = "OK_RGB";

    Console.WriteLine($"{resId,5} {width,6} {height,6} {bpp,4} {infoHeaderSize,6} {compStr,10} {bfOffBits,10} {bfSize,10} {issue,8}");

    // Stats
    if (compression == 0) stats["BI_RGB (0)"]++;
    else if (compression == 3) stats["BI_BITFIELDS (3)"]++;
    else stats["Other"]++;

    headerSizeStats.TryGetValue(infoHeaderSize, out var hc);
    headerSizeStats[infoHeaderSize] = hc + 1;

    offsetStats.TryGetValue(bfOffBits, out var oc);
    offsetStats[bfOffBits] = oc + 1;
}

Console.WriteLine("─────────────────────────────────────────────────────────────────────────");
Console.WriteLine($"\nTotal resources found: {found}");
Console.WriteLine("\nCompression type distribution:");
foreach (var (k, v) in stats)
    if (v > 0) Console.WriteLine($"  {k}: {v}");

Console.WriteLine("\nInfo header size distribution:");
foreach (var (k, v) in headerSizeStats)
    Console.WriteLine($"  {k} bytes: {v}");

Console.WriteLine("\nbfOffBits distribution:");
foreach (var (k, v) in offsetStats)
    Console.WriteLine($"  {k}: {v}");

// Also dump the color masks for first BI_BITFIELDS resource found
foreach (var resId in resourceIds)
{
    ReadOnlyMemory<byte> data;
    try { data = pe.GetEmbeddedBitmapResourceByID(resId); }
    catch { continue; }

    if (data.Length < 66) continue;
    var span = data.Span;
    if (span[0] != 0x42 || span[1] != 0x4D) continue;

    var compression = BitConverter.ToInt32(span.Slice(30, 4));
    if (compression != 3) continue;

    var infoHeaderSize = BitConverter.ToInt32(span.Slice(14, 4));
    int maskOffset = 14 + infoHeaderSize;

    if (infoHeaderSize == 40 && data.Length >= maskOffset + 12)
    {
        var rMask = BitConverter.ToUInt32(span.Slice(maskOffset, 4));
        var gMask = BitConverter.ToUInt32(span.Slice(maskOffset + 4, 4));
        var bMask = BitConverter.ToUInt32(span.Slice(maskOffset + 8, 4));
        Console.WriteLine($"\nColor masks for resource {resId} (40-byte header, masks at offset {maskOffset}):");
        Console.WriteLine($"  R: 0x{rMask:X8}  G: 0x{gMask:X8}  B: 0x{bMask:X8}");

        // Check what's at offset 54 (PELoaderLib's bfOffBits) - should be start of masks
        Console.WriteLine($"\nBytes at offset 54 (PELoaderLib's bfOffBits): " +
            $"{span[54]:X2} {span[55]:X2} {span[56]:X2} {span[57]:X2} " +
            $"{span[58]:X2} {span[59]:X2} {span[60]:X2} {span[61]:X2} " +
            $"{span[62]:X2} {span[63]:X2} {span[64]:X2} {span[65]:X2}");
        Console.WriteLine($"Bytes at offset 66 (corrected bfOffBits):    " +
            $"{span[66]:X2} {span[67]:X2} {span[68]:X2} {span[69]:X2} " +
            $"{span[70]:X2} {span[71]:X2} {span[72]:X2} {span[73]:X2}");
    }
    else if (infoHeaderSize >= 56)
    {
        // V3+ header has masks embedded at offsets 40-51 within the info header
        var rMask = BitConverter.ToUInt32(span.Slice(14 + 40, 4));
        var gMask = BitConverter.ToUInt32(span.Slice(14 + 44, 4));
        var bMask = BitConverter.ToUInt32(span.Slice(14 + 48, 4));
        Console.WriteLine($"\nColor masks for resource {resId} (V3+ header, embedded in header):");
        Console.WriteLine($"  R: 0x{rMask:X8}  G: 0x{gMask:X8}  B: 0x{bMask:X8}");
    }
    break;
}
