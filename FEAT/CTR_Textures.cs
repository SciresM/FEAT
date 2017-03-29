using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;


/* This code is really old. Would not recommend. */

namespace CTR
{
    public enum TextureFormat : uint
    {
        Invalid,
        L8 = 0x0,
        A8 = 0x1,
        La4 = 0x2,
        La8 = 0x3,
        Hilo8 = 0x4,
        Rgb565 = 0x5,
        Rgb8 = 0x6,
        Rgba5551 = 0x7,
        Rgba4 = 0x8,
        Rgba8 = 0x9,
        Etc1 = 0xA,
        Etc1A4 = 0xB,
        L4 = 0xC,
        A4 = 0xD
    }

    public enum ChannelOrder
    {
        Rgba,
        Abgr,
        Invalid
    }



    public class TextureUtil
    {
        private static readonly int[,] TileTable = new int[8, 8]
        {
            { 00, 01, 04, 05, 16, 17, 20, 21},
            { 02, 03, 06, 07, 18, 19, 22, 23},
            { 08, 09, 12, 13, 24, 25, 28, 29},
            { 10, 11, 14, 15, 26, 27, 30, 31},
            { 32, 33, 36, 37, 48, 49, 52, 53},
            { 34, 35, 38, 39, 50, 51, 54, 55},
            { 40, 41, 44, 45, 56, 57, 60, 61},
            { 42, 43, 46, 47, 58, 59, 62, 63}
        };

        private static readonly int[,] etcCompressionTable = new int[8, 4]
        {
            { 2, 8, -2, -8 }, 
            { 5, 17, -5, -17 }, 
            { 9, 29, -9, -29 }, 
            { 13, 42, -13, -42 }, 
            { 18, 60, -18, -60 }, 
            { 24, 80, -24, -80 }, 
            { 33, 106, -33, -106 }, 
            { 47, 183, -47, -183 }
        };

        /* private static readonly int[] etcScramble = new int[4] { 2, 3, 1, 0 }; */

        public static Bitmap DecodeBCLIM(string filename)
        {
            CTRTexture bclim = new CTRTexture();

            using (FileStream fs = File.OpenRead(filename))
            {
                using (BinaryReader br = new BinaryReader(fs))
                {
                    br.BaseStream.Seek(fs.Length - 0x28 + 0x1C, SeekOrigin.Begin);
                    bclim.Size = new Size { Width = br.ReadUInt16(), Height = br.ReadUInt16() };
                    bclim.Format = (TextureFormat)br.ReadUInt32();
                    int imageDataLen = br.ReadInt32();
                    br.BaseStream.Seek(0, SeekOrigin.Begin);
                    bclim.ImageData = br.ReadBytes(imageDataLen);
                }
            }

            return DecodeTexture(bclim);
        }

        public static Bitmap DecodeBFLIM(string filename)
        {
            CTRTexture bclim = new CTRTexture();

            using (FileStream fs = File.OpenRead(filename))
            {
                using (BinaryReader br = new BinaryReader(fs))
                {
                    br.BaseStream.Seek(fs.Length - 0x28 + 0x1C, SeekOrigin.Begin);
                    bclim.Size = new Size { Width = br.ReadUInt16(), Height = br.ReadUInt16() };
                    bclim.Format = (TextureFormat)br.ReadUInt32();
                    int imageDataLen = br.ReadInt32();
                    br.BaseStream.Seek(0, SeekOrigin.Begin);
                    bclim.ImageData = br.ReadBytes(imageDataLen);
                }
            }

            return DecodeTexture(bclim);
        }

        public static Bitmap DecodeByteArray(byte[] imagedata, ushort Width, ushort Height, TextureFormat Format)
        {
            CTRTexture tex = new CTRTexture
            {
                Format = Format,
                Size = new Size(Width, Height),
                ImageData = (byte[]) imagedata.Clone()
            };
            return DecodeTexture(tex);
        }

        public static Bitmap DecodeTexture(CTRTexture tex)
        {
            tex.BytesPerPixel = GetBytesPerPixel(tex.Format);

            if (tex.IsEtc1) return GetEtcBitmap(tex);

            tex.RedBitSize = GetRedBitSize(tex.Format);
            tex.GreenBitSize = GetGreenBitSize(tex.Format);
            tex.BlueBitSize = GetBlueBitSize(tex.Format);
            tex.AlphaBitSize = GetAlphaBitSize(tex.Format);
            tex.LuminanceBitSize = GetLuminanceBitSize(tex.Format);
            tex.PostProcess = getPostProcessingDelegate(tex.Format);

            tex.ChannelOrder = GetChannelOrder(tex.Format);

            return GetGenericBitmap(tex);
        }

        private static Bitmap GetGenericBitmap(CTRTexture tex)
        {
            uint redBitMask = (uint)((1 << tex.RedBitSize) - 1);
            uint blueBitMask = (uint)((1 << tex.BlueBitSize) - 1);
            uint greenBitMask = (uint)((1 << tex.GreenBitSize) - 1);
            uint alphaBitMask = (uint)((1 << tex.AlphaBitSize) - 1);
            uint luminanceBitMask = (uint)((1 << tex.LuminanceBitSize) - 1);

            bool crop = false;

            Size size = new Size { Width = tex.Size.Width, Height = tex.Size.Height };

            if ((int)(tex.ImageData.Length / tex.BytesPerPixel) > (tex.Size.Width * tex.Size.Height))
            {
                size = new Size { Width = Nlpo2(tex.Size.Width), Height = Nlpo2(tex.Size.Height) };
                crop = true;
            }

            Bitmap bmp = new Bitmap(size.Width, size.Height);
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite,
                bmp.PixelFormat);

            IntPtr ptr = bmpData.Scan0;
            int bytes = Math.Abs(bmpData.Stride) * bmp.Height;
            byte[] rgbaValues = new byte[bytes];

            int bitsPerPixel = (int)(tex.BytesPerPixel * 8);


            int Offset = 0;

            for (int y = 0; y < size.Height; y++)
            {
                for (int x = 0; x < size.Width; x++)
                {
                    int xInd = x & 7, yInd = y & 7;
                    int pixelOffset = GetPixelDataOffset(size, x, y, tex.BytesPerPixel) + (int)(tex.BytesPerPixel * (double)TileTable[yInd, xInd]);
                    uint Value;
                    switch (bitsPerPixel)
                    {
                        case 4:
                            Value = (uint)(tex.ImageData[pixelOffset] >> (4 * (x & 1))) & 0xF;
                            break;
                        case 8:
                            Value = (uint)(tex.ImageData[pixelOffset]);
                            break;
                        case 16:
                            Value = BitConverter.ToUInt16(tex.ImageData, pixelOffset);
                            break;
                        case 24:
                            Value = (uint)(BitConverter.ToUInt32(tex.ImageData, pixelOffset));
                            break;
                        case 32:
                            Value = BitConverter.ToUInt32(tex.ImageData, pixelOffset);
                            break;
                        default:
                            Value = 0;
                            break;
                    }

                    uint Red, Green, Blue, Alpha, Luminance;
                    Red = Green = Blue = Alpha = Luminance = 0xFF;

                    if (tex.LuminanceBitSize > 0)
                    {
                        Luminance = (Value & luminanceBitMask) * (0xFF / luminanceBitMask);
                        Value >>= tex.LuminanceBitSize;
                    }



                    switch (tex.ChannelOrder)
                    {
                        case ChannelOrder.Rgba:
                            if (tex.RedBitSize > 0)
                            {
                                Red = (Value & redBitMask) * (0xFF / redBitMask);
                                Value >>= tex.RedBitSize;
                            }
                            if (tex.GreenBitSize > 0)
                            {
                                Green = (Value & greenBitMask) * (0xFF / greenBitMask);
                                Value >>= tex.GreenBitSize;
                            }
                            if (tex.BlueBitSize > 0)
                            {
                                Blue = (Value & blueBitMask) * (0xFF / blueBitMask);
                                Value >>= tex.BlueBitSize;

                            }
                            if (tex.AlphaBitSize > 0)
                            {
                                Alpha = (Value & alphaBitMask) * (0xFF / alphaBitMask);
                                Value >>= tex.AlphaBitSize;
                            }
                            break;
                        case ChannelOrder.Abgr:
                            if (tex.AlphaBitSize > 0)
                            {
                                Alpha = (Value & alphaBitMask) * (0xFF / alphaBitMask);
                                Value >>= tex.AlphaBitSize;
                            }
                            if (tex.BlueBitSize > 0)
                            {
                                Blue = (Value & blueBitMask) * (0xFF / blueBitMask);
                                Value >>= tex.BlueBitSize;
                            }
                            if (tex.GreenBitSize > 0)
                            {
                                Green = (Value & greenBitMask) * (0xFF / greenBitMask);
                                Value >>= tex.GreenBitSize;
                            }
                            if (tex.RedBitSize > 0)
                            {
                                Red = (Value & redBitMask) * (0xFF / redBitMask);
                                Value >>= tex.RedBitSize;
                            }
                            break;
                    }

                    if (tex.LuminanceBitSize > 0)
                    {
                        rgbaValues[Offset + 0] = (byte)Luminance;
                        rgbaValues[Offset + 1] = (byte)Luminance;
                        rgbaValues[Offset + 2] = (byte)Luminance;
                        rgbaValues[Offset + 3] = (byte)Alpha;
                    }
                    else
                    {
                        rgbaValues[Offset + 0] = (byte)Blue;
                        rgbaValues[Offset + 1] = (byte)Green;
                        rgbaValues[Offset + 2] = (byte)Red;
                        rgbaValues[Offset + 3] = (byte)Alpha;

                    }
                    Offset += 4;
                }
            }

            tex.PostProcess(size, rgbaValues);

            Marshal.Copy(rgbaValues, 0, ptr, bytes);
            bmp.UnlockBits(bmpData);

            if (crop)
            {
                Bitmap bmp2 = new Bitmap(tex.Size.Width, tex.Size.Height);
                using (Graphics g = Graphics.FromImage(bmp2))
                    g.DrawImage(bmp, new Point(0, 0));
                return bmp2;
            }

            return bmp;
        }

        private static Bitmap GetEtcBitmap(CTRTexture tex)
        {
            Bitmap bmp = new Bitmap(tex.Size.Width, tex.Size.Height);
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite,
                bmp.PixelFormat);

            IntPtr ptr = bmpData.Scan0;
            int bytes = Math.Abs(bmpData.Stride) * bmp.Height;
            byte[] rgbaValues = new byte[bytes];


            bool hasAlpha = tex.HasAlpha;

            int offset = 0;

            for (int y = 0; y < tex.Size.Height; y += 4)
            {
                for (int x = 0; x < tex.Size.Width; x += 4)
                {
                    offset = GetEtc1BlockOffset(tex.Size, x, y, hasAlpha);

                    ulong alphaValue = hasAlpha ? BitConverter.ToUInt64(tex.ImageData, offset) : ulong.MaxValue;
                    ulong rgbValue = BitConverter.ToUInt64(tex.ImageData, hasAlpha ? offset + 8 : offset);

                    bool flip = (rgbValue & 0x100000000L) != 0;
                    bool dif = (rgbValue & 0x200000000L) != 0;

                    uint r1, g1, b1;
                    uint r2, g2, b2;

                    if (dif)
                    {
                        uint mult = 0xFF / 0x1F;

                        sbyte rt, gt, bt;

                        rt = (sbyte)((long)(rgbValue >> 56) & 7);
                        gt = (sbyte)((long)(rgbValue >> 48) & 7);
                        bt = (sbyte)((long)(rgbValue >> 40) & 7);

                        rt = (sbyte)((int)rt << 5);
                        rt = (sbyte)((int)rt >> 5);
                        gt = (sbyte)((int)gt << 5);
                        gt = (sbyte)((int)gt >> 5);
                        bt = (sbyte)((int)bt << 5);
                        bt = (sbyte)((int)bt >> 5);

                        r1 = (uint)(rgbValue >> 59) & 31;
                        g1 = (uint)(rgbValue >> 51) & 31;
                        b1 = (uint)(rgbValue >> 43) & 31;

                        r2 = (uint)(r1 + (int)rt) * mult;
                        g2 = (uint)(g1 + (int)gt) * mult;
                        b2 = (uint)(b1 + (int)bt) * mult;

                        r1 *= mult;
                        g1 *= mult;
                        b1 *= mult;
                    }
                    else
                    {
                        uint mult = 0xFF / 0xF;

                        r1 = (uint)((rgbValue >> 60) & 0xF) * mult;
                        g1 = (uint)((rgbValue >> 52) & 0xF) * mult;
                        b1 = (uint)((rgbValue >> 44) & 0xF) * mult;

                        r2 = (uint)((rgbValue >> 56) & 0xF) * mult;
                        g2 = (uint)((rgbValue >> 48) & 0xF) * mult;
                        b2 = (uint)((rgbValue >> 40) & 0xF) * mult;
                    }

                    uint cmpOne = (uint)((rgbValue >> 37) & 7);
                    uint cmpTwo = (uint)((rgbValue >> 34) & 7);

                    byte[] block = new byte[0x40];
                    if (flip)
                    {
                        for (int bY = 0; bY < 2; bY++)
                        {
                            for (int bX = 0; bX < 4; bX++)
                            {
                                GetEtc1Pixel(r1, g1, b1, bX, bY, rgbValue, alphaValue, cmpOne).CopyTo(rgbaValues, ((x + bX) * 4 + ((y + bY) * 4 * tex.Size.Width)));
                                GetEtc1Pixel(r2, g2, b2, bX, bY + 2, rgbValue, alphaValue, cmpTwo).CopyTo(rgbaValues, ((x + bX) * 4 + (((y + 2) + bY) * 4 * tex.Size.Width)));
                            }
                        }

                    }
                    else
                    {
                        for (int bY = 0; bY < 4; bY++)
                        {
                            for (int bX = 0; bX < 2; bX++)
                            {
                                GetEtc1Pixel(r1, g1, b1, bX, bY, rgbValue, alphaValue, cmpOne).CopyTo(rgbaValues, ((x + bX) * 4 + ((y + bY) * 4 * tex.Size.Width)));
                                GetEtc1Pixel(r2, g2, b2, bX + 2, bY, rgbValue, alphaValue, cmpTwo).CopyTo(rgbaValues, ((x + bX + 2) * 4 + ((y + bY) * 4 * tex.Size.Width)));
                            }
                        }
                    }
                }
            }

            Marshal.Copy(rgbaValues, 0, ptr, bytes);
            bmp.UnlockBits(bmpData);

            return bmp;
        }

        private static uint Clamp(int value, uint min = 0, uint max = 0xFF)
        {
            if (value < min)
                return min;
            else if (value > max)
                return max;
            else
                return (uint)value;
        }

        private static byte[] GetEtc1Pixel(uint r, uint g, uint b, int x, int y, ulong rgbVal, ulong aVal, uint cmp)
        {
            uint block = (uint)(rgbVal & 0xFFFFFFFF);
            block = ((block & 0xFF000000) >> 24) |
                    ((block & 0x00FF0000) >> 8) |
                    ((block & 0x0000FF00) << 8) |
                    ((block & 0x000000FF) << 24);
            int shift = x * 4 + y;

            int tableMod = shift < 8
                ? etcCompressionTable[cmp, ((block >> (shift + 24)) & 1) + (((block << 1) >> (shift + 8)) & 2)]
                : etcCompressionTable[cmp, ((block >> (shift + 8)) & 1) + (((block << 1) >> (shift - 8)) & 2)];

            r = Clamp((int)(r + tableMod));
            g = Clamp((int)(g + tableMod));
            b = Clamp((int)(b + tableMod));

            int a = (int)(aVal >> (shift << 2)) & 0xF;
            a *= 0x10;

            return new byte[] { (byte)b, (byte)g, (byte)r, (byte)a };
        }

        internal static int Nlpo2(int x)
        {
            x--;
            x |= (x >> 1);
            x |= (x >> 2);
            x |= (x >> 4);
            x |= (x >> 8);
            x |= (x >> 16);
            return (x + 1);
        }

        private static int GetEtc1BlockOffset(Size size, int x, int y, bool hasAlpha)
        {
            return (((x >> 3) + (y >> 3) * (size.Width >> 3)) * 4 + ((x >> 2) & 1) + ((y >> 2) & 1) * 2) * (hasAlpha ? 16 : 8);
        }

        private static int GetPixelDataOffset(Size size, int x, int y, float bytesPerPixel)
        {
            return (int)((((x / 8) + (y / 8) * ((size.Width % 8 == 0 ? size.Width : size.Width + 8 - (size.Width % 8)) / 8))) * 64.0 * bytesPerPixel);
        }

        private static float GetBytesPerPixel(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.A4:
                case TextureFormat.L4:
                case TextureFormat.Etc1:
                    return 0.5f;
                case TextureFormat.La4:
                case TextureFormat.L8:
                case TextureFormat.A8:
                case TextureFormat.Etc1A4:
                    return 1.0f;
                case TextureFormat.La8:
                case TextureFormat.Hilo8:
                case TextureFormat.Rgba4:
                case TextureFormat.Rgb565:
                case TextureFormat.Rgba5551:
                    return 2.0f;
                case TextureFormat.Rgb8:
                    return 3.0f;
                case TextureFormat.Rgba8:
                    return 4.0f;
                default:
                    return 0.0f;
            }
        }

        private static int GetRedBitSize(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.Rgba8:
                case TextureFormat.Rgb8:
                case TextureFormat.Hilo8:
                    return 8;
                case TextureFormat.Rgba5551:
                case TextureFormat.Rgb565:
                    return 5;
                case TextureFormat.Rgba4:
                    return 4;
                default:
                    return 0;
            }
        }

        private static int GetGreenBitSize(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.Rgba8:
                case TextureFormat.Rgb8:
                case TextureFormat.Hilo8:
                    return 8;
                case TextureFormat.Rgb565:
                    return 6;
                case TextureFormat.Rgba5551:
                    return 5;
                case TextureFormat.Rgba4:
                    return 4;
                default:
                    return 0;
            }
        }

        private static int GetBlueBitSize(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.Rgba8:
                case TextureFormat.Rgb8:
                    return 8;
                case TextureFormat.Rgba5551:
                case TextureFormat.Rgb565:
                    return 5;
                case TextureFormat.Rgba4:
                    return 4;
                default:
                    return 0;
            }
        }

        private static int GetAlphaBitSize(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.Rgba8:
                case TextureFormat.La8:
                case TextureFormat.A8:
                    return 8;
                case TextureFormat.Rgba4:
                case TextureFormat.La4:
                case TextureFormat.A4:
                    return 4;
                case TextureFormat.Rgba5551:
                    return 1;
                default:
                    return 0;
            }
        }

        private static int GetLuminanceBitSize(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.La8:
                case TextureFormat.L8:
                    return 8;
                case TextureFormat.La4:
                case TextureFormat.L4:
                    return 4;
                default:
                    return 0;
            }
        }

        private static CTRTexture.PostProcessingDelegate getPostProcessingDelegate(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.Hilo8:
                    return (CTRTexture.PostProcessingDelegate)((size, imageData) =>
                    {
                        for (int y = 0; y < size.Height; y++)
                        {
                            for (int x = 0; x < size.Width; x++)
                            {
                                imageData[((y * size.Width) + x) * 4] = 0;
                            }
                        }
                    });
                default:
                    return (CTRTexture.PostProcessingDelegate)((size, imageData) => { });
            }
        }

        private static ChannelOrder GetChannelOrder(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.Rgba5551:
                case TextureFormat.Rgb565:
                case TextureFormat.Rgba4:
                case TextureFormat.Hilo8:
                case TextureFormat.Rgba8:
                case TextureFormat.Rgb8:
                    return ChannelOrder.Abgr;
                case TextureFormat.La8:
                case TextureFormat.L8:
                case TextureFormat.A8:
                case TextureFormat.La4:
                case TextureFormat.L4:
                case TextureFormat.A4:
                    return ChannelOrder.Rgba;
                default:
                    return ChannelOrder.Invalid;
            }
        }
    }


    public class CTRTexture
    {
        public TextureFormat Format { get; set; }

        public bool IsEtc1 { get { return Format == TextureFormat.Etc1 || Format == TextureFormat.Etc1A4; } }

        public bool HasAlpha { get { return AlphaBitSize > 0 || Format == TextureFormat.Etc1A4; } }

        public float BytesPerPixel { get; set; }

        public ChannelOrder ChannelOrder { get; set; }

        public int RedBitSize { get; set; }
        public int GreenBitSize { get; set; }
        public int BlueBitSize { get; set; }
        public int AlphaBitSize { get; set; }

        public int LuminanceBitSize { get; set; }

        public Size Size { get; set; }

        public CTRTexture.PostProcessingDelegate PostProcess { get; set; }

        public delegate void PostProcessingDelegate(Size size, byte[] imageData);

        public byte[] ImageData { get; set; }
    }
}
