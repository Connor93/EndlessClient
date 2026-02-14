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
        /// </summary>
        private static byte[] FixBitmapData(ReadOnlyMemory<byte> rawData)
        {
            var span = rawData.Span;
            var result = rawData.ToArray();

            // Need at least BMP file header (14) + minimum info header (40)
            if (span.Length < 54)
                return result;

            // Check BMP magic "BM"
            if (span[0] != 0x42 || span[1] != 0x4D)
                return result;

            // Read info header size (at file offset 14)
            var infoHeaderSize = BitConverter.ToInt32(result, 14);
            if (infoHeaderSize < 40)
                return result;

            // Read biCompression (at file offset 14 + 16 = 30)
            const int BI_BITFIELDS = 3;
            var compression = BitConverter.ToInt32(result, 30);
            if (compression != BI_BITFIELDS)
                return result;

            if (infoHeaderSize == 40)
            {
                // Standard 40-byte BITMAPINFOHEADER with BI_BITFIELDS:
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
