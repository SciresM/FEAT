using System;
using System.IO;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Collections.Generic;

namespace BCH
{
    /*
     * A warning: this is old-ass code I wrote in mid-2014...I would not recommend using it. It's really horrible. 
     */

    public class BCHTool
    {
        [DllImport("ETC1.dll", EntryPoint = "ConvertETC1", CallingConvention = CallingConvention.Cdecl)]
        private static extern void ConvertETC1(IntPtr dataout, ref UInt32 dataoutsize, IntPtr datain, ref UInt32 datainsize, UInt16 wd, UInt16 ht, bool alpha);

        public static bool parseBCH(string path)
        {
            // goto 0x80 (skip the PT container header and go straight to the BCH data
            // srcdata[:]=srcdata[0x80:len(srcdata)]
            byte[] input = File.ReadAllBytes(path);
            if (input.Length == 0)
                return false;
            if (BitConverter.ToUInt32(input, 0) != 0x484342) // BCH
                return false;

            // BCH should now be in our data array.
            BCH bch = BCHTool.analyze(path, input);
            if (bch.TextureCount > 0)
            {
                Directory.CreateDirectory(bch.FilePath + "\\" + bch.FileName + "_\\");
            }
            for (int i = 0; i < bch.TextureCount; i++)
            {
                string texname = bch.TexNames[i];
                Bitmap img = parseEntry(bch.Textures[i], bch.data); //pass in texture desc, data

                using (MemoryStream ms = new MemoryStream())
                {
                    //error will throw from here
                    img.Save(ms, ImageFormat.Png);
                    byte[] data = ms.ToArray();
                    File.WriteAllBytes(bch.FilePath + "\\" + bch.FileName + "_\\" + texname + ".png", data);
                }
            }
            return true;
        }

        public static double FormatToBPP(int format)
        {
            switch (format)
            {
                case 0: //rgba8
                    return 4;
                case 1: //rgb8
                    return 3;
                case 2: //rgba5551
                case 3: //rgb565
                case 4: //rgba4
                case 5: //la8
                    return 2;
                case 6: //hilo8
                case 7: //l8
                case 8: //a8
                case 9: //la4
                    return 1;
                case 0xa: //l4
                    return 0.5;
                case 0xb: //ec1a4 (again?)
                    return 1;
                case 0xc: //etc1
                    return 8.0 / 16.0;
                case 0xd: //etc1a4
                    return 1;
                default:
                    return 0;
            }
        }

        public static BCH analyze(string path, byte[] input)
        {
            BCH bch = new BCH();
            bch.FileName = Path.GetFileNameWithoutExtension(path);
            bch.FilePath = Path.GetDirectoryName(path);
            bch.Extension = Path.GetExtension(path);
            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter bw = new BinaryWriter(ms))
                {
                    bw.Write(input);
                    using (BinaryReader br = new BinaryReader(ms))
                    {
                        br.BaseStream.Seek(0, SeekOrigin.Begin);
                        long bchlength = br.BaseStream.Length;
                        bch.Magic = br.ReadUInt32();          //0x00 [4]byte        "BCH\x00"
                        br.ReadUInt32();                      //0x04 [4]byte        07 07 0D 97
                        bch.InfoOffset = br.ReadUInt32();     //0x08 InfoTable Offset
                        bch.SymbolOffset = br.ReadUInt32();   //0x0C SymbTable Offset
                        bch.DescOffset = br.ReadUInt32();     //0x10 DescTable Offset
                        bch.DataOffset = br.ReadUInt32();     //0x14 DataTable Offset
                        uint AfterDataOffset = br.ReadUInt32();                      //0x18 UnknTable offset
                        uint AADP = br.ReadUInt32();                      //0x1C InfoTable size
                        uint SymbSize = br.ReadUInt32();                      //0x20 SymbTable Size
                        uint DescSize = br.ReadUInt32();                      //0x24 DescTable Size
                        bch.DataLength = br.ReadUInt32();                      //0x28 DataTable Size
                        uint dumb = br.ReadUInt32();                      //0x2C UnknTable Size
                        if (bch.InfoOffset == 0x44)
                        {
                            SymbSize = DescSize;
                            bch.DataLength = Math.Max(dumb, Math.Max(AADP, AfterDataOffset) - bch.DataOffset);
                        }
                        br.BaseStream.Seek(bch.InfoOffset + 0x24, SeekOrigin.Begin);
                        bch.TableOffset = br.ReadUInt32() + bch.InfoOffset; //info+0x24
                        bch.TextureCount = br.ReadUInt32(); //info+0x28
                        bch.TexNames = new List<String>();
                        bch.RawNames = new Dictionary<int, string>();
                        br.BaseStream.Seek((bch.SymbolOffset), SeekOrigin.Begin);
                        int ofs = 0;
                        uint ind = 0;
                        while (ind < SymbSize)
                        {
                            string curname = "";
                            byte b = br.ReadByte();
                            ind++;
                            while (b != 0)
                            {
                                curname = curname + (char)b;
                                b = br.ReadByte();
                                ind++;
                            }
                            bch.RawNames.Add(ofs, curname);
                            ofs += curname.Length + 1;
                        }
                        bch.Textures = new List<BCHTexture>();

                        for (int i = 0; i < bch.TextureCount; i++) 
                        {
                            BCHTexture CurTexture = new BCHTexture();
                            br.BaseStream.Seek(bch.TableOffset + i * 0x4, SeekOrigin.Begin);
                            uint CurTexInfoOffset = br.ReadUInt32();
                            br.BaseStream.Seek(bch.InfoOffset + CurTexInfoOffset, SeekOrigin.Begin);
                            CurTexture.DescOffset = br.ReadUInt32() + bch.DescOffset; //0x0  Location within Desc
                            br.ReadUInt32();                   //0x4  unk
                            br.ReadUInt32();                   //0x8  unk
                            br.ReadUInt32();                   //0xC  unk
                            br.ReadUInt32();                   //0x10 unk
                            br.ReadUInt32();                   //0x14 unk
                            br.ReadUInt32(); //0x18 unk;
                            int key = (int)br.ReadUInt32(); //0x1C Name offset; not useful because we already parsed
                            if (bch.InfoOffset == 0x44)
                            {
                                // key--;
                            }
                            string name = "";
                            bch.RawNames.TryGetValue(key, out name);
                            bch.TexNames.Add(name);
                            br.BaseStream.Seek(CurTexture.DescOffset, SeekOrigin.Begin);
                            CurTexture.Height = br.ReadUInt16(); //0x0 height
                            CurTexture.Width = br.ReadUInt16(); //0x2 width
                            br.ReadUInt32(); //0x4, unk
                            CurTexture.DataOffset = br.ReadUInt32(); //0x8 DataOffset
                            br.ReadUInt32(); //0xC unk
                            CurTexture.Format = br.ReadUInt32(); //0x10 Format
                            if (bch.InfoOffset == 0x44 || bch.InfoOffset == 0x3C)
                            {
                                CurTexture.DataOffset = CurTexture.Format;
                                br.ReadUInt32();
                                CurTexture.Format = br.ReadUInt32();
                            }
                            bch.Textures.Add(CurTexture); //OKAY DONE

                        }
                        for (int i = 0; i < bch.Textures.Count - 1; i++)
                        {
                            BCHTexture bchtex = bch.Textures[i];
                            bchtex.Length = bch.Textures[i + 1].DataOffset - bch.Textures[i].DataOffset;
                            if (bch.InfoOffset == 0x44)
                            {
                                bchtex.Length = (uint)(FormatToBPP((int)bchtex.Format) * (double)bchtex.Width * (double)bchtex.Height);
                            }
                            bch.Textures[i] = bchtex;
                        }
                        if (bch.TextureCount > 0)
                        {
                            BCHTexture bchtex1 = bch.Textures[bch.Textures.Count - 1];
                            bchtex1.Length = bch.DataLength - bchtex1.DataOffset;
                            if (bch.InfoOffset == 0x44)
                            {
                                bchtex1.Length = (uint)(FormatToBPP((int)bchtex1.Format) * (double)bchtex1.Width * (double)bchtex1.Height);
                            }
                            bch.Textures[bch.Textures.Count - 1] = bchtex1;
                        }
                        br.BaseStream.Seek(bch.DataOffset, SeekOrigin.Begin);
                        byte[] data = new byte[bch.DataLength];
                        br.Read(data, 0, (int)bch.DataLength);
                        bch.data = data;
                    }
                }
            }
            return bch;
        }

        private static Bitmap parseEntry(BCHTexture bchtex, byte[] data)
        {
            Bitmap img = new Bitmap(1, 1);
            if (bchtex.Format >= 0 && bchtex.Format < 0xB)
            {
                img = getIMG(bchtex, data);
            }
            else if (bchtex.Format == 0xB || bchtex.Format == 0xC || bchtex.Format == 0xD)
            {
                img = getIMG_ETC1(bchtex, data);
            }
            /*if (CHK_FLIPVERT.Checked)
            {
                img.RotateFlip(RotateFlipType.RotateNoneFlipY);
            }*/
            return img;
        }

        // Bitmap Data Writing
        private static Bitmap getIMG(BCHTexture bchtex, byte[] data)
        {
            // New Image
            Bitmap img = new Bitmap(nlpo2(gcm(bchtex.Width, 8)), nlpo2(gcm(bchtex.Height, 8)));
            int f = (int)bchtex.Format;
            int area = img.Width * img.Height;
            if (f == 0 && area > bchtex.Length / 4 || (f == 3 && area > bchtex.Length / 4))
            {
                img = new Bitmap(gcm(bchtex.Width, 8), gcm(bchtex.Height, 8));
                area = img.Width * img.Height;
            }
            byte[] temp = new byte[(int)bchtex.Length];
            Array.Copy(data, bchtex.DataOffset, temp, 0, temp.Length);
            data = temp;
            // Coordinates
            uint x, y = 0;
            // Colors
            Color c = new Color();
            uint val = 0;
            // Tiles Per Width
            int p = gcm(img.Width, 8) / 8;
            if (p == 0) p = 1;
            // Build Image
            using (Stream BitmapStream = new MemoryStream(data))
            using (BinaryReader br = new BinaryReader(BitmapStream))
                for (uint i = 0; i < area; i++) // for every pixel
                {
                    d2xy(i % 64, out x, out y);
                    uint tile = i / 64;

                    // Shift Tile Coordinate into Tilemap
                    x += (uint)(tile % p) * 8;
                    y += (uint)(tile / p) * 8;

                    // Get Color
                    switch (f)
                    {
                        case 0: //RGBA8 - 4 bytes
                            c = DecodeColor(br.ReadUInt32(), f);
                            break;
                        case 1: //RGB8
                            byte[] data1 = br.ReadBytes(3); Array.Resize(ref data1, 4);
                            c = DecodeColor(BitConverter.ToUInt32(data1, 0), f);
                            break;
                        case 2: //RGBA5551
                        case 3: //RGB565
                        case 4: //RGBA4
                        case 5: //LA8
                            c = DecodeColor(br.ReadUInt16(), f);
                            break;
                        case 6: //HILO8
                        case 7: //L8
                        case 8: //A8
                        case 9: //LA4
                            c = DecodeColor(br.ReadByte(), f);
                            break;
                        case 0xA: //L4
                        case 0xB: //A4
                            val = br.ReadByte();
                            img.SetPixel((int)x, (int)y, DecodeColor(val & 0xF, f));
                            i++; x++;
                            c = DecodeColor(val >> 4, f);
                            break;
                        case 0xC:  // ETC1
                        case 0xD:  // ETC1A4
                        default:
                            throw new Exception("Invalid FileFormat.");
                    }
                    img.SetPixel((int)x, (int)y, c);
                }
            return img;
        }


        private static Bitmap getIMG_ETC1(BCHTexture bchtex, byte[] data)
        {
            Bitmap img = new Bitmap(Math.Max(nlpo2(bchtex.Width), 16), Math.Max(nlpo2(bchtex.Height), 16));
            string dllpath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location).Replace('\\', '/') + "/ETC1.dll";
            if (!File.Exists(dllpath)) File.WriteAllBytes(dllpath, Fire_Emblem_Awakening_Archive_Tool.Properties.Resources.ETC1);
            try
            {
                /*
                 * Much of this code is taken/modified from Tharsis: http://jul.rustedlogic.net/thread.php?pid=436556#436556 Thank you to Tharsis's creator, xdaniel. 
                 */
                byte[] temp = new byte[bchtex.Length];
                Array.Copy(data, bchtex.DataOffset, temp, 0, bchtex.Length);
                /* Get compressed data & handle to it */
                byte[] textureData = temp;
                //textureData = switchEndianness(textureData, 0x10);
                ushort[] input = new ushort[textureData.Length / sizeof(ushort)];
                Buffer.BlockCopy(textureData, 0, input, 0, textureData.Length);
                GCHandle pInput = GCHandle.Alloc(input, GCHandleType.Pinned);

                /* Marshal data around, invoke ETC1.dll for conversion, etc */
                UInt32 size1 = 0, size2 = 0;
                UInt16 wd = (ushort)img.Width, ht = (ushort)img.Height;
                ConvertETC1(IntPtr.Zero, ref size1, IntPtr.Zero, ref size2, wd, ht, bchtex.Format == 0xD || bchtex.Format == 0xB); //true = etc1a4, false = etc1
                uint[] output = new uint[size1];
                GCHandle pOutput = GCHandle.Alloc(output, GCHandleType.Pinned);
                ConvertETC1(pOutput.AddrOfPinnedObject(), ref size1, pInput.AddrOfPinnedObject(), ref size2, wd, ht, bchtex.Format == 0xD || bchtex.Format == 0xB);
                pOutput.Free();
                pInput.Free();

                /* Unscramble if needed // could probably be done in ETC1.dll, it's probably pretty damn ugly, but whatever... */
                /* Non-square code blocks could need some cleanup, verification, etc. as well... */
                uint[] finalized = new uint[output.Length];

                //Act if it's square because BCLIM swizzling is stupid
                Buffer.BlockCopy(output, 0, finalized, 0, finalized.Length);

                byte[] tmp = new byte[finalized.Length];
                Buffer.BlockCopy(finalized, 0, tmp, 0, tmp.Length);
                int h = img.Height;
                int w = img.Width;
                byte[] imgData = tmp;
                for (int i = 0; i < img.Width; i++)
                {
                    for (int j = 0; j < img.Height; j++)
                    {
                        int k = (j + i * img.Height) * 4;
                        Color c = Color.FromArgb(imgData[k + 3], imgData[k], imgData[k + 1], imgData[k + 2]);
                        if (imgData[k] == imgData[k + 1] && imgData[k + 1] == imgData[k + 2] && imgData[k + 1] == 0)
                        {
                            if (imgData[k + 3] == 0)
                            {
                                c = Color.Transparent;
                            }
                        }
                        img.SetPixel(i, j, c);
                        /*if (bchtex.Format == 0xD)
                        {
                            img.SetPixel(i, j, Color.FromArgb(0xFF, imgData[k], imgData[k + 1], imgData[k + 2]));
                        }*/
                    }
                }
                //image is 13  instead of 12
                //         24             34
                img.RotateFlip(RotateFlipType.Rotate90FlipX);
                if (wd > ht)
                {
                    //image is now in appropriate order, but the shifting done been fucked up. Let's fix that.
                    Bitmap img2 = new Bitmap(Math.Max(nlpo2(bchtex.Width), 16), Math.Max(nlpo2(bchtex.Height), 16));
                    for (int y = 0; y < Math.Max(nlpo2(bchtex.Width), 16); y += 8)
                    {
                        for (int x = 0; x < Math.Max(nlpo2(bchtex.Height), 16); x++)
                        {
                            for (int j = 0; j < 8; j++) //treat every 8 vertical pixels as 1 pixel for purposes of calculation, add to offset later.
                            {
                                int x1 = (x + ((y / 8) * h)) % img2.Width; //reshift x
                                int y1 = ((x + ((y / 8) * h)) / img2.Width) * 8; //reshift y
                                img2.SetPixel(x1, y1 + j, img.GetPixel(x, y + j)); //reswizzle
                            }
                        }
                    }
                    return img2;
                }
                else if (ht > wd)
                {
                    //image is now in appropriate order, but the shifting done been fucked up. Let's fix that.
                    Bitmap img2 = new Bitmap(Math.Max(nlpo2(bchtex.Width), 16), Math.Max(nlpo2(bchtex.Height), 16));
                    for (int y = 0; y < Math.Max(nlpo2(bchtex.Width), 16); y += 8)
                    {
                        for (int x = 0; x < Math.Max(nlpo2(bchtex.Height), 16); x++)
                        {
                            for (int j = 0; j < 8; j++) //treat every 8 vertical pixels as 1 pixel for purposes of calculation, add to offset later.
                            {
                                int x1 = x % img2.Width; //reshift x
                                int y1 = ((x + ((y / 8) * h)) / img2.Width) * 8; //reshift y
                                img2.SetPixel(x1, y1 + j, img.GetPixel(x, y + j)); //reswizzle
                            }
                        }
                    }
                    return img2;
                }
            }
            catch (System.IndexOutOfRangeException)
            {
                //
            }
            catch (System.AccessViolationException)
            {
                //
            }
            return img;
        }

        private int getColorCount(Bitmap img)
        {
            Color[] colors = new Color[img.Width * img.Height];
            int colorct = 1;

            for (int i = 0; i < colors.Length; i++)
            {
                Color c = img.GetPixel(i % img.Width, i / img.Width);
                int index = Array.IndexOf(colors, c);
                if (c.A == 0) index = 0;
                if (index < 0)
                {
                    colors[colorct] = c;
                    colorct++;
                }
            }
            return colorct;
        }


        private static int[] Convert5To8 = { 0x00,0x08,0x10,0x18,0x20,0x29,0x31,0x39,
                                      0x41,0x4A,0x52,0x5A,0x62,0x6A,0x73,0x7B,
                                      0x83,0x8B,0x94,0x9C,0xA4,0xAC,0xB4,0xBD,
                                      0xC5,0xCD,0xD5,0xDE,0xE6,0xEE,0xF6,0xFF };


        private static Color DecodeColor(uint val, int format)
        {
            int alpha = 0xFF, red, green, blue;
            switch (format)
            {
                case 0: //RGBA8
                    red = (byte)((val >> 24) & 0xFF);
                    green = (byte)((val >> 16) & 0xFF);
                    blue = (byte)((val >> 8) & 0xFF);
                    alpha = (byte)(val & 0xFF);
                    return Color.FromArgb(alpha, red, green, blue);
                case 1: //RGB8
                    red = (byte)((val >> 16) & 0xFF);
                    green = (byte)((val >> 8) & 0xFF);
                    blue = (byte)(val & 0xFF);
                    return Color.FromArgb(alpha, red, green, blue);
                case 2: //RGBA5551
                    red = Convert5To8[(val >> 11) & 0x1F];
                    green = Convert5To8[(val >> 6) & 0x1F];
                    blue = Convert5To8[(val >> 1) & 0x1F];
                    alpha = (val & 0x0001) == 1 ? 0xFF : 0x00;
                    return Color.FromArgb(alpha, red, green, blue);
                case 3: //RGB565
                    red = Convert5To8[(val >> 11) & 0x1F];
                    green = (byte)(((val >> 5) & 0x3F) * 4);
                    blue = Convert5To8[val & 0x1F];
                    return Color.FromArgb(alpha, red, green, blue);
                case 4: //RGBA4
                    alpha = (byte)(0x11 * (val & 0xf));
                    red = (byte)(0x11 * ((val >> 12) & 0xf));
                    green = (byte)(0x11 * ((val >> 8) & 0xf));
                    blue = (byte)(0x11 * ((val >> 4) & 0xf));
                    return Color.FromArgb(alpha, red, green, blue);
                case 5: //LA8
                    red = (byte)((val >> 8 & 0xFF));
                    alpha = (byte)(val & 0xFF);
                    return Color.FromArgb(alpha, red, red, red);
                case 6: //HILO8
                    red = (byte)(val >> 8);
                    return Color.FromArgb(alpha, red, red, red);
                case 7: //L8
                    return Color.FromArgb(alpha, (byte)val, (byte)val, (byte)val);
                case 8: //A8
                    return Color.FromArgb((byte)val, alpha, alpha, alpha);
                case 9: //LA4
                    red = (byte)(val >> 4);
                    alpha = (byte)(val & 0x0F);
                    return Color.FromArgb(alpha, red, red, red);
                case 0xA: //L4
                    return Color.FromArgb(alpha, (byte)(val * 0x11), (byte)(val * 0x11), (byte)(val * 0x11));
                case 0xB: //A4
                    return Color.FromArgb((byte)(val * 0x11), alpha, alpha, alpha);
                case 0xC: //ETC1
                case 0xD: //ETC1A4
                default:
                    return Color.White;
            }
        }

        // Color Conversion
        private byte GetL8(Color c)
        {
            byte red = c.R;
            byte green = c.G;
            byte blue = c.B;
            // Luma (Y’) = 0.299 R’ + 0.587 G’ + 0.114 B’ from wikipedia
            return (byte)(((0x4CB2 * red + 0x9691 * green + 0x1D3E * blue) >> 16) & 0xFF);
        }        // L8
        private byte GetA8(Color c)
        {
            return c.A;
        }        // A8
        private byte GetLA4(Color c)
        {
            return (byte)((c.A / 0x11) + (c.R / 0x11) << 4);
        }       // LA4
        private ushort GetLA8(Color c)
        {
            return (byte)((c.A) + (c.R) << 8);
        }     // LA8
        // HILO
        private ushort GetRGB565(Color c)
        {
            int val = 0;
            // val += c.A >> 8; // unused
            val += convert8to5(c.B) >> 3;
            val += (c.G >> 2) << 5;
            val += convert8to5(c.R) << 10;
            return (ushort)val;
        }  // RGB565
        // RGB8
        private ushort GetRGBA5551(Color c)
        {
            int val = 0;
            val += (byte)(c.A > 0x80 ? 1 : 0);
            val += convert8to5(c.R) << 11;
            val += convert8to5(c.G) << 6;
            val += convert8to5(c.B) << 1;
            ushort v = (ushort)val;

            return v;
        }// RGBA5551
        private ushort GetRGBA4444(Color c)
        {
            int val = 0;
            val += (c.A / 0x11);
            val += ((c.B / 0x11) << 4);
            val += ((c.G / 0x11) << 8);
            val += ((c.R / 0x11) << 12);
            return (ushort)val;
        }// RGBA4444
        private uint GetRGBA8888(Color c)     // RGBA8888
        {
            uint val = 0;
            val += c.A;
            val += (uint)(c.B << 8);
            val += (uint)(c.G << 16);
            val += (uint)(c.R << 24);
            return val;
        }

        // Unit Conversion
        private byte convert8to5(int colorval)
        {

            byte[] Convert8to5 = { 0x00,0x08,0x10,0x18,0x20,0x29,0x31,0x39,
                                   0x41,0x4A,0x52,0x5A,0x62,0x6A,0x73,0x7B,
                                   0x83,0x8B,0x94,0x9C,0xA4,0xAC,0xB4,0xBD,
                                   0xC5,0xCD,0xD5,0xDE,0xE6,0xEE,0xF6,0xFF };
            byte i = 0;
            while (colorval > Convert8to5[i]) i++;
            return (byte)i;
        }
        UInt32 DM2X(UInt32 code)
        {
            return C11(code >> 0);
        }
        UInt32 DM2Y(UInt32 code)
        {
            return C11(code >> 1);
        }
        UInt32 C11(UInt32 x)
        {
            x &= 0x55555555;                  // x = -f-e -d-c -b-a -9-8 -7-6 -5-4 -3-2 -1-0
            x = (x ^ (x >> 1)) & 0x33333333; // x = --fe --dc --ba --98 --76 --54 --32 --10
            x = (x ^ (x >> 2)) & 0x0f0f0f0f; // x = ---- fedc ---- ba98 ---- 7654 ---- 3210
            x = (x ^ (x >> 4)) & 0x00ff00ff; // x = ---- ---- fedc ba98 ---- ---- 7654 3210
            x = (x ^ (x >> 8)) & 0x0000ffff; // x = ---- ---- ---- ---- fedc ba98 7654 3210
            return x;
        }

        /// <summary>
        /// Greatest common multiple (to round up)
        /// </summary>
        /// <param name="n">Number to round-up.</param>
        /// <param name="m">Multiple to round-up to.</param>
        /// <returns>Rounded up number.</returns>
        private static int gcm(int n, int m)
        {
            return ((n + m - 1) / m) * m;
        }
        /// <summary>
        /// Next Largest Power of 2
        /// </summary>
        /// <param name="x">Input to round up to next 2^n</param>
        /// <returns>2^n > x && x > 2^(n-1) </returns>
        private static int nlpo2(int x)
        {
            x--; // comment out to always take the next biggest power of two, even if x is already a power of two
            x |= (x >> 1);
            x |= (x >> 2);
            x |= (x >> 4);
            x |= (x >> 8);
            x |= (x >> 16);
            return (x + 1);
        }

        // Morton Translation
        /// <summary>
        /// Combines X/Y Coordinates to a decimal ordinate.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        private uint xy2d(uint x, uint y)
        {
            x &= 0x0000ffff;
            y &= 0x0000ffff;
            x |= (x << 8);
            y |= (y << 8);
            x &= 0x00ff00ff;
            y &= 0x00ff00ff;
            x |= (x << 4);
            y |= (y << 4);
            x &= 0x0f0f0f0f;
            y &= 0x0f0f0f0f;
            x |= (x << 2);
            y |= (y << 2);
            x &= 0x33333333;
            y &= 0x33333333;
            x |= (x << 1);
            y |= (y << 1);
            x &= 0x55555555;
            y &= 0x55555555;
            return x | (y << 1);
        }
        /// <summary>
        /// Decimal Ordinate In to X / Y Coordinate Out
        /// </summary>
        /// <param name="d">Loop integer which will be decoded to X/Y</param>
        /// <param name="x">Output X coordinate</param>
        /// <param name="y">Output Y coordinate</param>
        private static void d2xy(uint d, out uint x, out uint y)
        {
            x = d;
            y = (x >> 1);
            x &= 0x55555555;
            y &= 0x55555555;
            x |= (x >> 1);
            y |= (y >> 1);
            x &= 0x33333333;
            y &= 0x33333333;
            x |= (x >> 2);
            y |= (y >> 2);
            x &= 0x0f0f0f0f;
            y &= 0x0f0f0f0f;
            x |= (x >> 4);
            y |= (y >> 4);
            x &= 0x00ff00ff;
            y &= 0x00ff00ff;
            x |= (x >> 8);
            y |= (y >> 8);
            x &= 0x0000ffff;
            y &= 0x0000ffff;
        }
    }

    public struct BCH
    {
        public UInt32 Magic;
        public UInt32 InfoOffset;
        public UInt32 SymbolOffset;
        public UInt32 DescOffset;
        public UInt32 DataOffset;
        public UInt32 DataLength;
        public UInt32 TableOffset;
        public UInt32 TextureCount;
        public List<String> TexNames;
        public Dictionary<int, String> RawNames;
        public List<BCHTexture> Textures;
        public string FileName;
        public string FilePath;
        public string Extension;
        public byte[] data;
    }

    public struct BCHTexture
    {
        public UInt16 Width;
        public UInt16 Height;
        public UInt32 Format;
        public UInt32 DescOffset;
        public UInt32 DataOffset;
        public UInt32 Length;

    }
}
