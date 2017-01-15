using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.Drawing;
using System.Drawing.Imaging;

namespace ctpktool
{
    [XmlRoot("Entry")]
    public class CTPKEntry
    {
        [XmlElement("InternalFilePath")]
        public string InternalFilePath;
        [XmlElement("RealFilePath")]
        public string FilePath;
        [XmlElement("TextureSize")]
        [XmlIgnore]
        public uint TextureSize;
        [XmlElement("TextureOffset")]
        [XmlIgnore]
        public uint TextureOffset;
        [XmlElement("Format")]
        public uint Format;
        [XmlIgnore]
        [XmlElement("Width")]
        public ushort Width;
        [XmlIgnore]
        [XmlElement("Height")]
        public ushort Height;
        [XmlElement("MipLevel")]
        public byte MipLevel;
        [XmlElement("Type")]
        public byte Type; // 0 = cube? 1=1D? 2=2D?
        [XmlElement("Unknown")]
        public ushort Unknown;
        [XmlElement("BitmapSizeOffset")]
        public uint BitmapSizeOffset;
        [XmlIgnore]
        public uint FileTime; // Generate this on our own, so don't export or import it

        [XmlElement("Info")]
        public uint Info; // ??

        [XmlElement("Info2")]
        public uint Info2; // ??

        [XmlIgnore]
        public uint FilenameHash;

        [XmlIgnore]
        public byte[] TextureRawData;

        [XmlIgnore]
        private Texture _textureData;

        [XmlElement("HasAlpha")]
        public bool HasAlpha;

        public Bitmap GetBitmap()
        {
            if (TextureRawData.Length == 0)
            {
                return new Bitmap(0, 0);
            }

            _textureData = new Texture(Width, Height, (TextureFormat)Format, TextureRawData);
            return _textureData.GetBitmap();
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(0); // Temporary. Will come back to write correct data later when filenames have been written into the data
            writer.Write(TextureRawData.Length);
            writer.Write(0); // Temporary
            writer.Write(Format);
            writer.Write(Width);
            writer.Write(Height);
            writer.Write(MipLevel);
            writer.Write(Type);
            writer.Write(Unknown);
            writer.Write(BitmapSizeOffset); // What is this exactly?
            writer.Write(FileTime);
        }

        public static CTPKEntry Read(BinaryReader reader)
        {
            CTPKEntry entry = new CTPKEntry();

            var pathOffset = reader.ReadInt32();
            entry.TextureSize = reader.ReadUInt32();
            entry.TextureOffset = reader.ReadUInt32();
            entry.Format = reader.ReadUInt32();
            entry.Width = reader.ReadUInt16();
            entry.Height = reader.ReadUInt16();
            entry.MipLevel = reader.ReadByte();
            entry.Type = reader.ReadByte();
            entry.Unknown = reader.ReadUInt16();
            entry.BitmapSizeOffset = reader.ReadUInt32();
            entry.FileTime = reader.ReadUInt32();
            
            #region Read path string
            var curOffset = reader.BaseStream.Position;
            reader.BaseStream.Seek(pathOffset, SeekOrigin.Begin);

            List<byte> temp = new List<byte>();
            byte c = 0;
            while ((c = reader.ReadByte()) != 0)
            {
                temp.Add(c);
            }

            entry.InternalFilePath = Encoding.GetEncoding(932).GetString(temp.ToArray()); // 932 = Shift-JIS

            reader.BaseStream.Seek(curOffset, SeekOrigin.Begin);
            #endregion

            switch ((TextureFormat)entry.Format)
            {
                case TextureFormat.A4:
                case TextureFormat.A8:
                case TextureFormat.La4:
                case TextureFormat.Rgb5551:
                case TextureFormat.Rgba4:
                case TextureFormat.Rgba8:
                case TextureFormat.Etc1A4:
                    entry.HasAlpha = true;
                    break;

                default:
                    entry.HasAlpha = false;
                    break;
            }

            return entry;
        }

        public void ToFile(string outputFolder)
        {
            string dir = Path.GetDirectoryName(InternalFilePath);
            string filename = Path.GetFileNameWithoutExtension(InternalFilePath);

            if (!String.IsNullOrWhiteSpace(outputFolder) && !Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            if (!String.IsNullOrWhiteSpace(dir))
            {
                if (!String.IsNullOrWhiteSpace(outputFolder))
                    dir = Path.Combine(outputFolder, dir);

                filename = Path.Combine(dir, filename);

                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
            }

            FilePath = filename + ".png";

            var outputPath = filename;
            if (!String.IsNullOrWhiteSpace(outputFolder))
                outputPath = Path.Combine(outputFolder, outputPath);

            using (TextWriter writer = new StreamWriter(outputPath + ".xml"))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(CTPKEntry));
                serializer.Serialize(writer, this);
            }

            // Export image data to file here?
            GetBitmap().Save(outputPath + ".png");
        }

        public static CTPKEntry FromFile(string filename, string foldername)
        {
            if (!File.Exists(filename))
                return new CTPKEntry();

            using (XmlTextReader reader = new XmlTextReader(filename))
            {
                reader.WhitespaceHandling = WhitespaceHandling.All;

                XmlSerializer serializer = new XmlSerializer(typeof(CTPKEntry));

                CTPKEntry entry = (CTPKEntry)serializer.Deserialize(reader);

                Console.WriteLine("Reading {0}...", entry.FilePath);

                // Import image file
                entry._textureData = new Texture();

                var path = entry.FilePath;

                if (!String.IsNullOrWhiteSpace(foldername))
                    path = Path.Combine(foldername, path);

                var origbmp = Bitmap.FromFile(path);

                var pixelSize = 3;
                var bmpPixelFormat = PixelFormat.Format24bppRgb;
                entry.Format = (int)TextureFormat.Rgb8;

                entry.HasAlpha = true;
                if (entry.HasAlpha)
                {
                    bmpPixelFormat = PixelFormat.Format32bppArgb;
                    entry.Format = (int)TextureFormat.Rgba8;
                    pixelSize = 4;
                }

                var bmp = new Bitmap(origbmp.Width, origbmp.Height, bmpPixelFormat);
                using (Graphics gr = Graphics.FromImage(bmp))
                {
                    gr.DrawImage(origbmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
                }

                entry.Width = (ushort)bmp.Width;
                entry.Height = (ushort)bmp.Height;

                var scramble = new Texture().GetScrambledTextureData(bmp);
                var dataSize = bmp.Width * bmp.Height * pixelSize;

                entry.TextureRawData = new byte[dataSize];
                entry.TextureSize = (uint)dataSize;
                Array.Copy(scramble, entry.TextureRawData, dataSize);

                entry.FileTime = (uint)File.GetLastWriteTime(path).Ticks; // This is right exactly? Not sure, don't think it matters either

                var filenameData = Encoding.GetEncoding(932).GetBytes(entry.InternalFilePath);
                entry.FilenameHash = Crc32.Calculate(filenameData, filenameData.Length);

                return entry;
            }
        }
    }
}
