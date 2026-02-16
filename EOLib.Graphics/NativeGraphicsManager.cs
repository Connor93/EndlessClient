using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using AutomaticTypeMapper;
using CommunityToolkit.HighPerformance;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace EOLib.Graphics
{
    [MappedType(BaseType = typeof(INativeGraphicsManager), IsSingleton = true)]
    public sealed class NativeGraphicsManager : INativeGraphicsManager
    {
        private readonly ConcurrentDictionary<GFXTypes, ConcurrentDictionary<int, Texture2D>> _cache;

        private readonly INativeGraphicsLoader _gfxLoader;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;

        public NativeGraphicsManager(INativeGraphicsLoader gfxLoader, IGraphicsDeviceProvider graphicsDeviceProvider)
        {
            _cache = new ConcurrentDictionary<GFXTypes, ConcurrentDictionary<int, Texture2D>>();
            _gfxLoader = gfxLoader;
            _graphicsDeviceProvider = graphicsDeviceProvider;
        }

        // todo: instead of having a bunch of bool params, maybe an enum param with [Flags] for the different options would be better
        public Texture2D TextureFromResource(GFXTypes file, int resourceVal, bool transparent = false, bool reloadFromFile = false, bool fullTransparent = false)
        {
            if (_cache.ContainsKey(file) && _cache[file].ContainsKey(resourceVal))
            {
                if (reloadFromFile)
                {
                    _cache[file][resourceVal]?.Dispose();
                    _cache[file].Remove(resourceVal, out _);
                }
                else
                {
                    return _cache[file][resourceVal];
                }
            }

            var ret = LoadTexture(file, resourceVal, transparent, fullTransparent);
            if (_cache.ContainsKey(file) ||
                _cache.TryAdd(file, new ConcurrentDictionary<int, Texture2D>()))
            {
                _cache[file].TryAdd(resourceVal, ret);
            }

            return ret;
        }

        private Texture2D LoadTexture(GFXTypes file, int resourceVal, bool transparent, bool fullTransparent)
        {
            var rawData = _gfxLoader.LoadGFX(file, resourceVal);

            if (rawData.IsEmpty)
                return new Texture2D(_graphicsDeviceProvider.GraphicsDevice, 1, 1);

            Action<byte[]> processAction = null;

            if (transparent)
            {
                processAction = data => CrossPlatformMakeTransparent(data);

                if (fullTransparent)
                {
                    processAction = data => CrossPlatformMakeTransparent(data, isHat: true);
                }
                else if (file == GFXTypes.FemaleHat || file == GFXTypes.MaleHat)
                {
                    processAction = data => CrossPlatformMakeTransparent(data, checkClip: true, isHat: true);
                }
            }

            var fixedData = FixBitmapData(rawData);
            using var ms = new System.IO.MemoryStream(fixedData);
            var ret = Texture2D.FromStream(_graphicsDeviceProvider.GraphicsDevice, ms, processAction);

            return ret;
        }

        /// <summary>
        /// Fixes BMP data produced by PELoaderLib for BI_BITFIELDS compression.
        /// PELoaderLib's BitmapFileHeader does not account for color mask bytes
        /// when calculating bfOffBits, causing StbImageSharp to fail with "bad BMP".
        /// Additionally, StbImageSharp does not support 16bpp BI_BITFIELDS, so
        /// those are fully converted to 32bpp BI_RGB.
        /// </summary>
        private static byte[] FixBitmapData(ReadOnlyMemory<byte> rawData)
        {
            var span = rawData.Span;

            // Need at least BMP file header (14) + minimum info header (40)
            if (span.Length < 54)
                return rawData.ToArray();

            // Check BMP magic "BM"
            if (span[0] != 0x42 || span[1] != 0x4D)
                return rawData.ToArray();

            // Read info header size (at file offset 14)
            var infoHeaderSize = BitConverter.ToInt32(span.Slice(14, 4));
            if (infoHeaderSize < 40)
                return rawData.ToArray();

            // Read biCompression (at file offset 14 + 16 = 30)
            const int BI_BITFIELDS = 3;
            var compression = BitConverter.ToInt32(span.Slice(30, 4));
            if (compression != BI_BITFIELDS)
                return rawData.ToArray();

            var bpp = BitConverter.ToInt16(span.Slice(28, 2));

            if (bpp == 16 && infoHeaderSize == 40)
            {
                // 16bpp BI_BITFIELDS: StbImageSharp does not support this format.
                // Convert to 32bpp BI_RGB by expanding each 16-bit pixel using the color masks.
                return Convert16bppBitfieldsTo32bppRgb(span);
            }

            var result = rawData.ToArray();

            if (infoHeaderSize == 40)
            {
                // 32bpp BI_BITFIELDS with standard 40-byte BITMAPINFOHEADER:
                // Color masks (3 DWORDs = 12 bytes) sit between the header and pixel data,
                // but PELoaderLib sets bfOffBits = 14 + 40 = 54, missing the 12 mask bytes.
                // Fix: set bfOffBits = 14 + 40 + 12 = 66
                const int COLOR_MASK_BYTES = 12;
                var correctOffset = 14 + 40 + COLOR_MASK_BYTES;
                BitConverter.GetBytes(correctOffset).CopyTo(result, 10);
            }
            else if (infoHeaderSize >= 56)
            {
                // BITMAPV3INFOHEADER (56+ bytes): color masks are part of the header,
                // so bfOffBits is correct. However, StbImageSharp may not support
                // BI_BITFIELDS with V3+ headers. Change to BI_RGB since the masks
                // are standard BGRA byte order.
                BitConverter.GetBytes(0).CopyTo(result, 30); // BI_RGB = 0
            }

            return result;
        }

        /// <summary>
        /// Converts a 16bpp BI_BITFIELDS BMP to 32bpp BI_RGB for StbImageSharp compatibility.
        /// Reads the 3 DWORD color masks after the 40-byte info header, expands each 16-bit
        /// pixel to 32-bit BGRA, and rewrites the BMP/info headers accordingly.
        /// </summary>
        private static byte[] Convert16bppBitfieldsTo32bppRgb(ReadOnlySpan<byte> src)
        {
            const int FILE_HEADER_SIZE = 14;
            const int INFO_HEADER_SIZE = 40;
            const int MASK_SIZE = 12; // 3 DWORDs

            var width = BitConverter.ToInt32(src.Slice(18, 4));
            var height = BitConverter.ToInt32(src.Slice(22, 4));
            var absHeight = Math.Abs(height);

            // Read color masks (at offset 54, right after the 40-byte info header)
            var rMask = BitConverter.ToUInt32(src.Slice(FILE_HEADER_SIZE + INFO_HEADER_SIZE, 4));
            var gMask = BitConverter.ToUInt32(src.Slice(FILE_HEADER_SIZE + INFO_HEADER_SIZE + 4, 4));
            var bMask = BitConverter.ToUInt32(src.Slice(FILE_HEADER_SIZE + INFO_HEADER_SIZE + 8, 4));

            // Calculate shift and scale for each channel
            var rShift = CountTrailingZeros(rMask);
            var gShift = CountTrailingZeros(gMask);
            var bShift = CountTrailingZeros(bMask);
            var rBits = CountBits(rMask);
            var gBits = CountBits(gMask);
            var bBits = CountBits(bMask);

            // Source pixel data starts after file header + info header + masks
            var srcPixelOffset = FILE_HEADER_SIZE + INFO_HEADER_SIZE + MASK_SIZE;
            // Source row stride: width * 2 bytes, padded to 4-byte boundary
            var srcStride = ((width * 2) + 3) & ~3;

            // Destination: 32bpp BI_RGB, no masks needed
            var dstStride = width * 4; // 32bpp rows are always 4-byte aligned (width * 4)
            var dstPixelDataSize = dstStride * absHeight;
            var dstOffset = FILE_HEADER_SIZE + INFO_HEADER_SIZE;
            var dstFileSize = dstOffset + dstPixelDataSize;

            var dst = new byte[dstFileSize];

            // Write BMP file header
            dst[0] = 0x42; dst[1] = 0x4D; // "BM"
            BitConverter.GetBytes(dstFileSize).CopyTo(dst, 2);           // bfSize
            BitConverter.GetBytes(dstOffset).CopyTo(dst, 10);            // bfOffBits

            // Write info header (copy and modify)
            src.Slice(FILE_HEADER_SIZE, INFO_HEADER_SIZE).CopyTo(dst.AsSpan(FILE_HEADER_SIZE));
            BitConverter.GetBytes((short)32).CopyTo(dst, 28);            // biBitCount = 32
            BitConverter.GetBytes(0).CopyTo(dst, 30);                    // biCompression = BI_RGB
            BitConverter.GetBytes(dstPixelDataSize).CopyTo(dst, 34);     // biSizeImage

            // Convert pixels row by row
            for (int y = 0; y < absHeight; y++)
            {
                var srcRowStart = srcPixelOffset + y * srcStride;
                var dstRowStart = dstOffset + y * dstStride;

                for (int x = 0; x < width; x++)
                {
                    if (srcRowStart + x * 2 + 1 >= src.Length)
                        break;

                    var pixel16 = (uint)BitConverter.ToUInt16(src.Slice(srcRowStart + x * 2, 2));

                    // Extract channels and scale to 8-bit
                    var r = (byte)(((pixel16 & rMask) >> rShift) * 255 / ((1 << rBits) - 1));
                    var g = (byte)(((pixel16 & gMask) >> gShift) * 255 / ((1 << gBits) - 1));
                    var b = (byte)(((pixel16 & bMask) >> bShift) * 255 / ((1 << bBits) - 1));

                    var dstIdx = dstRowStart + x * 4;
                    dst[dstIdx] = b;       // Blue
                    dst[dstIdx + 1] = g;   // Green
                    dst[dstIdx + 2] = r;   // Red
                    dst[dstIdx + 3] = 255; // Alpha (fully opaque)
                }
            }

            return dst;
        }

        private static int CountTrailingZeros(uint value)
        {
            if (value == 0) return 32;
            int count = 0;
            while ((value & 1) == 0) { value >>= 1; count++; }
            return count;
        }

        private static int CountBits(uint value)
        {
            int count = 0;
            while (value != 0) { count += (int)(value & 1); value >>= 1; }
            return count;
        }

        private static unsafe void CrossPlatformMakeTransparent(byte[] data, bool isHat = false, bool checkClip = false)
        {
            var shouldClip = false;
            if (checkClip)
            {
                fixed (byte* ptr = data)
                {
                    for (int i = 0; i < data.Length; i += 4)
                    {
                        uint* addr = (uint*)(ptr + i);
                        if (*addr == 0xff000008)
                        {
                            shouldClip = true;
                            break;
                        }
                    }
                }
            }

            // for all gfx: 0,0,0 is transparent

            // for some hats: 8,0,0 and 0,0,0 are both transparent

            // for hats: R=8 G=0 B=0 is transparent
            // some default gfx use R=0 G=8 B=0 as black
            // 0,0,0 clips pixels below it if 8,0,0 is present on the frame

            var transparentColors = isHat
                ? shouldClip
                    ? new Color[] { new Color(0xff000008) } // check clip: make ff000008 transparent only, use black for clipping if present
                    : new Color[] { Color.Black, new Color(0xff000008) } // isHat: make both colors transparent
                : new Color[] { Color.Black }; // default: make only black transparent

            fixed (byte* ptr = data)
            {
                for (int i = 0; i < data.Length; i += 4)
                {
                    uint* addr = (uint*)(ptr + i);
                    if (transparentColors.Contains(new Color(*addr)))
                        *addr = 0;
                }
            }
        }

        public void Dispose()
        {
            foreach (var text in _cache.SelectMany(x => x.Value.Values))
                text.Dispose();

            _cache.Clear();
        }
    }

    [Serializable]
    public class GFXLoadException : Exception
    {
        public GFXLoadException(int resource, GFXTypes gfx)
            : base($"Unable to load graphic {resource + 100} from file gfx{(int)gfx:000}.egf") { }
    }
}
