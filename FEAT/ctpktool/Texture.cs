// Code from Normmatt's texturipper (with permission)
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace ctpktool
{
    public class Texture
    {
        public int Width;
        public int Height;
        public bool HasAlpha;
        public byte[] Data;
        public TextureFormat TextureFormat;

        private readonly int[] _tileOrder =
        {
            0, 1, 8, 9, 2, 3, 10, 11, 16, 17, 24, 25, 18, 19, 26, 27, 4, 5, 12, 13, 6, 7, 14, 15,
            20, 21, 28, 29, 22, 23, 30, 31, 32, 33, 40, 41, 34, 35, 42, 43, 48, 49, 56, 57, 50, 51, 58, 59, 36, 37, 44,
            45, 38, 39, 46, 47, 52, 53, 60, 61, 54, 55, 62, 63
        };

        public Texture()
        {
            // Stub
        }

        public Texture(int width, int height, TextureFormat textureFormat, byte[] data)
        {
            Width = IsPowerOfTwo(width) ? width : nlpo2(width);
            Height = IsPowerOfTwo(height) ? height : nlpo2(height);
            Data = data;
            TextureFormat = textureFormat;

            switch (textureFormat)
            {
                case TextureFormat.A4:
                case TextureFormat.A8:
                case TextureFormat.La4:
                case TextureFormat.Rgb5551:
                case TextureFormat.Rgba4:
                case TextureFormat.Rgba8:
                case TextureFormat.Etc1A4:
                    HasAlpha = true;
                    break;

                default:
                    HasAlpha = false;
                    break;
            }
        }

        public static uint GetPixelSize(TextureFormat textureFormat, int width, int height)
        {
            switch (textureFormat)
            {
                case TextureFormat.Rgba8:
                    return (uint)(4 * width * height);
                case TextureFormat.Rgb8:
                    return (uint)(3 * width * height);
                case TextureFormat.Rgb5551:
                case TextureFormat.Rgb565:
                case TextureFormat.Rgba4:
                case TextureFormat.La8:
                case TextureFormat.Hilo8:
                    return (uint)(2 * width * height);
                case TextureFormat.L8:
                case TextureFormat.A8:
                case TextureFormat.La4:
                    return (uint)(1 * width * height);
                case TextureFormat.L4: //TODO: Verify this is correct
                case TextureFormat.A4:
                    return (uint)((1 * width * height) / 2);
                case TextureFormat.Etc1:
                    return (uint)Etc.GetEtc1Length(new Size(width, height), false);
                case TextureFormat.Etc1A4:
                    return (uint)Etc.GetEtc1Length(new Size(width, height), true);
                default:
                    throw new Exception("Unsupported Texture Format " + (int)textureFormat);
            }
        }

        private Color GetPixel(ref int ofs, out decimal adjustment)
        {
            var col = new Color();
            int pixel;

            //Exit early if trying to read out of bounds
            if (ofs >= Data.Length)
            {
                adjustment = 0;
                return col;
            }

            switch (TextureFormat)
            {
                case TextureFormat.Rgba8:
                    col = Color.FromArgb(Data[ofs], Data[ofs + 3], Data[ofs + 2], Data[ofs + 1]);
                    adjustment = 4;
                    break;
                case TextureFormat.Rgb8:
                    col = Color.FromArgb(255, Data[ofs + 2], Data[ofs + 1], Data[ofs]);
                    adjustment = 3;
                    break;
                case TextureFormat.Rgb5551:
                    pixel = BitConverter.ToInt16(Data, ofs);
                    col = Color.FromArgb(((pixel & 1) == 1) ? 255 : 0, ((pixel >> 11) & 0x1F) * 8, ((pixel >> 6) & 0x1F) * 8, ((pixel >> 1) & 0x1F) * 8);
                    adjustment = 2;
                    break;
                case TextureFormat.Rgb565:
                    pixel = BitConverter.ToInt16(Data, ofs);
                    col = Color.FromArgb(255, ((pixel >> 11) & 0x1F) * 8, ((pixel >> 5) & 0x3F) * 4, ((pixel) & 0x1F) * 8);
                    adjustment = 2;
                    break;
                case TextureFormat.Rgba4:
                    pixel = BitConverter.ToInt16(Data, ofs);
                    col = Color.FromArgb((pixel & 0xF) * 16, ((pixel >> 12) & 0xF) * 16, ((pixel >> 8) & 0xF) * 16, ((pixel >> 4) & 0xF) * 16);
                    adjustment = 2;
                    break;
                case TextureFormat.La8:
                    pixel = Data[ofs + 1];
                    col = Color.FromArgb(Data[ofs], pixel, pixel, pixel);
                    adjustment = 2;
                    break;
                case TextureFormat.Hilo8:
                    col = Color.FromArgb(255, Data[ofs], Data[ofs + 1], 0);
                    adjustment = 2;
                    break;
                case TextureFormat.L8:
                    pixel = Data[ofs];
                    col = Color.FromArgb(255, pixel, pixel, pixel);
                    adjustment = 1;
                    break;
                case TextureFormat.A8:
                    col = Color.FromArgb(Data[ofs], 0, 0, 0);
                    adjustment = 1;
                    break;
                case TextureFormat.La4:
                    pixel = Data[ofs];
                    col = Color.FromArgb((pixel & 0xF) * 16, ((pixel >> 4) & 0xF) * 16, ((pixel >> 4) & 0xF) * 16, ((pixel >> 4) & 0xF) * 16);
                    adjustment = 1;
                    break;
                case TextureFormat.L4: //TODO: Verify this is correct
                    col = Color.FromArgb(255, (Data[ofs] & 0xF) * 16, (Data[ofs] & 0xF) * 16, (Data[ofs] & 0xF) * 16);
                    Data[ofs] >>= 4; //Hacky
                    adjustment = 0.5M;
                    break;
                case TextureFormat.A4:
                    col = Color.FromArgb((Data[ofs] & 0xF) * 16, 0, 0, 0);
                    Data[ofs] >>= 4; //Hacky
                    adjustment = 0.5M;
                    break;
                default:
                    throw new Exception("Unsupported Texture Format " + TextureFormat);
            }

            return col;
        }

        private void GetPlainRasterData(Bitmap bmp)
        {
            var ofs = 0;
            decimal adjustment = 0;
            for (int y = 0; y < Height; y += 8)
            {
                for (int x = 0; x < Width; x += 8)
                {
                    for (int k = 0; k < 8 * 8; k++)
                    {
                        var i = _tileOrder[k] % 8;
                        var j = (_tileOrder[k] - i) / 8;
                        var nosub = adjustment == 0.5M;
                        var pix = GetPixel(ref ofs, out adjustment);
                        bmp.SetPixel(Math.Min(x + i, Width), Math.Min(y + j, Height), pix);

                        ofs += Convert.ToInt32(Math.Ceiling(adjustment));

                        if (adjustment == 0.5M && !nosub)
                        {
                            //Hacky I know
                            ofs--;
                        }
                        else
                        {
                            adjustment = 0;
                        }
                    }
                }
            }
        }

        private void GetPlainRasterData(ref byte[] data)
        {
            var ofs = 0;
            decimal adjustment = 0;
            for (int y = 0; y < Height; y += 8)
            {
                for (int x = 0; x < Width; x += 8)
                {
                    for (int k = 0; k < 8 * 8; k++)
                    {
                        var i = _tileOrder[k] % 8;
                        var j = (_tileOrder[k] - i) / 8;
                        //bmp.SetPixel(Math.Min(x + i, Width), Math.Min(y + j, Height), GetPixel(ref ofs));
                        var nosub = adjustment == 0.5M;
                        var pix = GetPixel(ref ofs, out adjustment);
                        var argb = pix.ToArgb();
                        var bytes = BitConverter.GetBytes(argb);
                        Array.Copy(bytes, 0, data, ((x + i) + (y + j) * Width) * 4, bytes.Length);

                        ofs += Convert.ToInt32(Math.Ceiling(adjustment));

                        if (adjustment == 0.5M && !nosub)
                        {
                            //Hacky I know
                            ofs--;
                        }
                        else
                        {
                            adjustment = 0;
                        }
                    }
                }
            }
        }

        public byte[] GetRGBA()
        {
            var data = new byte[Width * Height * 4];

            switch (TextureFormat)
            {
                case TextureFormat.Invalid:
                    break;
                case TextureFormat.Etc1:
                case TextureFormat.Etc1A4:
                    for (int y = 0; y < Height; y++)
                    {
                        Etc.GetEtc1RasterData(Data, new Size(Width, Height), y, HasAlpha, data, 0);
                    }
                    break;
                default:
                    GetPlainRasterData(ref data);
                    break;
            }
            return data;
        }

        public byte[] GetScrambledTextureData(Bitmap bmp)
        {
            Width = IsPowerOfTwo(bmp.Width) ? bmp.Width : nlpo2(bmp.Width);
            Height = IsPowerOfTwo(bmp.Height) ? bmp.Height : nlpo2(bmp.Height);

            MemoryStream stream = new MemoryStream();
            using (BinaryWriter output = new BinaryWriter(stream))
            {
                for (int y = 0; y < Height; y += 8)
                {
                    for (int x = 0; x < Width; x += 8)
                    {
                        for (int k = 0; k < 8 * 8; k++)
                        {
                            var i = _tileOrder[k] % 8;
                            var j = (_tileOrder[k] - i) / 8;
                            var pix = bmp.GetPixel(Math.Min(x + i, Width), Math.Min(y + j, Height));

                            if (bmp.PixelFormat == PixelFormat.Format32bppArgb)
                            {
                                output.Write(pix.A);
                                output.Write(pix.B);
                                output.Write(pix.G);
                                output.Write(pix.R);
                            }
                            else
                            {
                                output.Write(pix.B);
                                output.Write(pix.G);
                                output.Write(pix.R);
                            }
                        }
                    }
                }
            }

            return stream.GetBuffer();
        }

        public Bitmap GetBitmap()
        {
            var bmp = new Bitmap(Width, Height);

            switch (TextureFormat)
            {
                case TextureFormat.Invalid:
                    break;
                case TextureFormat.Etc1:
                case TextureFormat.Etc1A4:
                    for (int y = 0; y < Height; y++)
                    {
                        Etc.GetEtc1RasterData(Data, new Size(Width, Height), y, HasAlpha, bmp, 0);
                    }
                    break;
                default:
                    GetPlainRasterData(bmp);
                    break;
            }
            return bmp;
        }

        static bool IsPowerOfTwo(int x)
        {
            return (x != 0) && ((x & (x - 1)) == 0);
        }

        int nlpo2(int x)
        {
            x--; // comment out to always take the next biggest power of two, even if x is already a power of two
            x |= (x >> 1);
            x |= (x >> 2);
            x |= (x >> 4);
            x |= (x >> 8);
            x |= (x >> 16);
            return (x + 1);
        }
    }
}