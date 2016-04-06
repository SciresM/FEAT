using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ctpktool
{
    class Ctpk
    {
        private const int Magic = 0x4B505443; // 'CTPK'

        public ushort Version; // ?
        public ushort NumberOfTextures;
        public uint TextureSectionOffset;
        public uint TextureSectionSize;
        public uint HashSectionOffset;
        public uint TextureInfoSection; // ??

        private readonly List<CTPKEntry> _entries;

        public Ctpk()
        {
            _entries = new List<CTPKEntry>();
            Version = 1;
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(new byte[0x20]); // Write 0x20 bytes of blank data so we can come back to it later

            // Section 1
            foreach (var entry in _entries)
            {
                entry.Write(writer);
            }

            // Section 2
            foreach (var entry in _entries)
            {
                writer.Write(entry.Info);
            }

            // Section 3
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                var curOffset = writer.BaseStream.Position;

                // Fix filename offset in section 1
                writer.BaseStream.Seek(0x20*(i+1), SeekOrigin.Begin);
                writer.Write((uint) curOffset);
                writer.BaseStream.Seek(curOffset, SeekOrigin.Begin);

                writer.Write(Encoding.GetEncoding(932).GetBytes(entry.InternalFilePath));
                writer.Write((byte)0); // Null terminated
            }

            writer.Write(new byte[4 - writer.BaseStream.Length%4]); // Pad the filename section to the nearest 4th byte

            // Section 4
            HashSectionOffset = (uint)writer.BaseStream.Length;
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                writer.Write(entry.FilenameHash);
                writer.Write(i);
            }

            // Section 5
            TextureInfoSection = (uint)writer.BaseStream.Length;
            foreach (var entry in _entries)
            {
                writer.Write(entry.Info2);
            }

            writer.Write(new byte[0x80 - writer.BaseStream.Length % 0x80]); // Pad the filename section to the nearest 0x80th byte
            
            // Section 6
            TextureSectionOffset = (uint)writer.BaseStream.Length;
            TextureSectionSize = 0;
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                var curOffset = writer.BaseStream.Position;

                // Fix texture data offset in section 1
                writer.BaseStream.Seek(0x20 * (i+1) + 0x08, SeekOrigin.Begin);
                writer.Write(TextureSectionSize);
                writer.BaseStream.Seek(curOffset, SeekOrigin.Begin);

                writer.Write(entry.TextureRawData);

                TextureSectionSize += (uint)entry.TextureRawData.Length;
            }

            NumberOfTextures = (ushort) _entries.Count;

            writer.BaseStream.Seek(0, SeekOrigin.Begin);
            writer.Write(Magic);
            writer.Write(Version);
            writer.Write(NumberOfTextures);
            writer.Write(TextureSectionOffset);
            writer.Write(TextureSectionSize);
            writer.Write(HashSectionOffset);
            writer.Write(TextureInfoSection);
        }

        public static Ctpk Create(string folder)
        {
            Ctpk file = new Ctpk();

            // Look for all xml definition files in the folder
            var files = Directory.GetFiles(folder, "*.xml", SearchOption.AllDirectories);
            foreach (var xmlFilename in files)
            {
                CTPKEntry entry = CTPKEntry.FromFile(xmlFilename, folder);
                file._entries.Add(entry);
            }

            for (int i = 0; i < file._entries.Count; i++)
            {
                file._entries[i].BitmapSizeOffset = (uint)((file._entries.Count + 1)*8 + (i*8));
            }

            var outputFilename = folder + ".ctpk";
            using (BinaryWriter writer = new BinaryWriter(File.Open(outputFilename, FileMode.Create)))
            {
                file.Write(writer);
            }

            Console.WriteLine("Finished! Saved to {0}", outputFilename);

            return file;
        }

        public static Ctpk Read(string filename)
        {
            using (BinaryReader reader = new BinaryReader(File.Open(filename, FileMode.Open)))
            {
                var data = new byte[reader.BaseStream.Length];
                reader.Read(data, 0, data.Length);
                return Read(data, filename);
            }
        }

        public static Ctpk Read(byte[] data, string filename)
        {
            Ctpk file = new Ctpk();

            using(MemoryStream dataStream = new MemoryStream(data))
            using (BinaryReader reader = new BinaryReader(dataStream))
            {
                if (reader.ReadUInt32() != Magic)
                {
                    Console.WriteLine("ERROR: Not a valid CTPK file.");
                }

                file.Version = reader.ReadUInt16();
                file.NumberOfTextures = reader.ReadUInt16();
                file.TextureSectionOffset = reader.ReadUInt32();
                file.TextureSectionSize = reader.ReadUInt32();
                file.HashSectionOffset = reader.ReadUInt32();
                file.TextureInfoSection = reader.ReadUInt32();

                // Section 1 + 3
                for (int i = 0; i < file.NumberOfTextures; i++)
                {
                    reader.BaseStream.Seek(0x20 * (i + 1), SeekOrigin.Begin);

                    CTPKEntry entry = CTPKEntry.Read(reader);
                    file._entries.Add(entry);
                }

                // Section 2
                for (int i = 0; i < file.NumberOfTextures; i++)
                {
                    file._entries[i].Info = reader.ReadUInt32();
                }

                // Section 4
                for (int i = 0; i < file.NumberOfTextures; i++)
                {
                    file._entries[i].FilenameHash = reader.ReadUInt32();
                }

                // Section 5
                reader.BaseStream.Seek(file.TextureInfoSection, SeekOrigin.Begin);
                for (int i = 0; i < file.NumberOfTextures; i++)
                {
                    file._entries[i].Info2 = reader.ReadUInt32();
                }

                // Section 6
                for (int i = 0; i < file.NumberOfTextures; i++)
                {
                    reader.BaseStream.Seek(file.TextureSectionOffset + file._entries[i].TextureOffset, SeekOrigin.Begin);
                    file._entries[i].TextureRawData = new byte[file._entries[i].TextureSize];
                    reader.Read(file._entries[i].TextureRawData, 0, (int)file._entries[i].TextureSize);
                }

                string basePath = Path.GetDirectoryName(filename);
                string baseFilename = Path.GetFileNameWithoutExtension(filename);

                if (!String.IsNullOrWhiteSpace(basePath))
                {
                    baseFilename = Path.Combine(basePath, baseFilename);
                }

                for (int i = 0; i < file.NumberOfTextures; i++)
                {
                    Console.WriteLine("Converting {0}...", file._entries[i].InternalFilePath);
                    file._entries[i].ToFile(baseFilename);
                }
            }

            return file;
        }
    }
}
