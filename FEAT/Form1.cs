using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using DSDecmp.Formats.Nitro;
using ctpktool;
using BCH;

namespace Fire_Emblem_Awakening_Archive_Tool
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            AllowDrop = RTB_Output.AllowDrop = true;
            DragEnter += Form1_DragEnter;
            RTB_Output.DragEnter += Form1_DragEnter;
            DragDrop += Form1_DragDrop;
            RTB_Output.DragDrop += Form1_DragDrop;
        }

        private volatile int threads;
        private string Selected_Path;

        private void Form1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        private void Form1_DragDrop(object sender, DragEventArgs e)
        {
            if (threads > 0)
            {
                MessageBox.Show("Please wait until all operations are finished.");
                return;
            }
            new Thread(() =>
            {
                threads++;
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (string file in files)
                    Open(file);
                threads--;
            }).Start();
        }

        private void B_Open_Click(object sender, EventArgs e)
        {
            if (threads > 0)
            {
                MessageBox.Show("Please wait until all operations are finished.");
                return;
            }
            B_Go.Enabled = false;
            CommonDialog dialog;
            if (ModifierKeys == Keys.Alt)
                dialog = new FolderBrowserDialog();
            else
                dialog = new OpenFileDialog();
            if (dialog.ShowDialog() != DialogResult.OK) return;

            if (dialog is OpenFileDialog)
                TB_FilePath.Text = (dialog as OpenFileDialog).FileName;
            else TB_FilePath.Text = (dialog as FolderBrowserDialog).SelectedPath;
            B_Go.Enabled = true;
        }

        // Unused Method
        private void B_Go_Click(object sender, EventArgs e)
        {
            if (threads > 0)
            {
                MessageBox.Show("Please wait until all operations are finished.");
                return;
            }
            new Thread(() =>
            {
                threads++;
                Open(Selected_Path);
                threads--;
            }).Start();
        }

        public static byte[] Decompress(byte[] Data)
        {
            var leng = (uint)(Data[4] << 24 | Data[5] << 16 | Data[6] << 8 | Data[7]);
            byte[] Result = new byte[leng];
            int Offs = 16;
            int dstoffs = 0;
            while (true)
            {
                byte header = Data[Offs++];
                for (int i = 0; i < 8; i++)
                {
                    if ((header & 0x80) != 0) Result[dstoffs++] = Data[Offs++];
                    else
                    {
                        byte b = Data[Offs++];
                        int offs = ((b & 0xF) << 8 | Data[Offs++]) + 1;
                        int length = (b >> 4) + 2;
                        if (length == 2) length = Data[Offs++] + 0x12;
                        for (int j = 0; j < length; j++)
                        {
                            Result[dstoffs] = Result[dstoffs - offs];
                            dstoffs++;
                        }
                    }
                    if (dstoffs >= leng) return Result;
                    header <<= 1;
                }
            }
        }

        private void Open(string path)
        {
            if (Directory.Exists(path))
            {
                    foreach (string p in (new DirectoryInfo(path)).GetFiles().Select(f => f.FullName))
                        Open(p);
                    foreach (string p in (new DirectoryInfo(path)).GetDirectories().Select(f => f.FullName))
                        Open(p);
            }
            else if (File.Exists(path))
            {
                string ext = Path.GetExtension(path).ToLower();
                var yaz0 = false;
                using (var fs = File.OpenRead(path))
                {
                    if (fs.Length > 4 && fs.ReadByte() == 'Y' && fs.ReadByte() == 'a' && fs.ReadByte() == 'z' && fs.ReadByte() == '0')
                    {
                        yaz0 = true;
                    }
                }
                if ((ModifierKeys == Keys.Control) && (MessageBox.Show("Compress " + path + "?", "Prompt", MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk) == DialogResult.Yes))
                {
                    var cmp = LZ11Compress(File.ReadAllBytes(path));
                    byte[] cmp2 = new byte[cmp.Length + 4];

                    cmp2[0] = 0x13;
                    Array.Copy(cmp, 0, cmp2, 4, cmp.Length);
                    Array.Copy(cmp, 1, cmp2, 1, 3);
                    File.WriteAllBytes(path + ".lz", cmp2);
                    AddLine(RTB_Output, string.Format("LZ13 compressed {0} to {1}", path, path + ".lz"));
                }
                else if (yaz0)
                {
                    var cmp = File.ReadAllBytes(path);
                    File.WriteAllBytes(path + ".dec", Decompress(cmp));
                    AddLine(RTB_Output, string.Format("Yaz0 decompressed {0}.", path));
                } 
                else if (ext == ".lz")
                {
                    byte[] filedata = File.ReadAllBytes(path);
                    string decpath = Path.GetDirectoryName(path) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(path);
                    if (filedata[0] == 0x13 && filedata[4] == 0x11) // "LZ13"
                    {
                        filedata = filedata.Skip(4).ToArray();
                    }
                    else if (filedata[0] == 0x17 && filedata[4] == 0x11) // Fire Emblem Heroes "LZ17"
                    {
                        var xorkey = BitConverter.ToUInt32(filedata, 0) >> 8;
                        xorkey *= 0x8083;
                        for (var i = 8; i < filedata.Length; i += 0x4)
                        {
                            BitConverter.GetBytes(BitConverter.ToUInt32(filedata, i) ^ xorkey).CopyTo(filedata, i);
                            xorkey ^= BitConverter.ToUInt32(filedata, i);
                        }
                        filedata = filedata.Skip(4).ToArray();
                    }
                    else if (filedata[0] == 0x4 && (BitConverter.ToUInt32(filedata, 0) >> 8) == filedata.Length - 4)
                    {
                        var xorkey = BitConverter.ToUInt32(filedata, 0) >> 8;
                        xorkey *= 0x8083;
                        for (var i = 4; i < filedata.Length; i += 0x4)
                        {
                            BitConverter.GetBytes(BitConverter.ToUInt32(filedata, i) ^ xorkey).CopyTo(filedata, i);
                            xorkey ^= BitConverter.ToUInt32(filedata, i);
                        }
                        filedata = filedata.Skip(4).ToArray();
                        if (BitConverter.ToUInt32(filedata, 0) == filedata.Length)
                        {
                            File.WriteAllBytes(decpath, filedata);
                            AddLine(RTB_Output, string.Format("Successfully decompressed {0}.", Path.GetFileName(decpath)));
                            if (File.Exists(decpath))
                                Open(decpath);
                        }
                        else
                        {
                            AddLine(RTB_Output, string.Format("Unable to automatically decompress {0}.", Path.GetFileName(path)));
                        }
                        return;
                    }
                    try
                    {
                        File.WriteAllBytes(decpath, LZ11Decompress(filedata));
                        AddLine(RTB_Output, string.Format("Successfully decompressed {0}.", Path.GetFileName(decpath)));
                    }
                    catch (Exception ex)
                    {
                        AddLine(RTB_Output, string.Format("Unable to automatically decompress {0}.", Path.GetFileName(path)));
                        Console.WriteLine(ex.Message);
                    }
                    if (File.Exists(decpath))
                        Open(decpath);
                }
                else if (ext == ".bin")
                {
                    byte[] filedata = File.ReadAllBytes(path);
                    var outname = Path.GetDirectoryName(path) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(path) + ".txt";
                    if (BitConverter.ToUInt32(filedata, 0) == filedata.Length &&
                        new string(filedata.Skip(0x20).Take(0xC).Select(c => (char)c).ToArray()) == "MESS_ARCHIVE")
                    {
                        string archive_name = ExtractFireEmblemMessageArchive(outname, filedata);
                        AddLine(RTB_Output, string.Format("Successfully Extracted {0} ({1}).", archive_name, Path.GetFileName(path)));
                    }
                    else if (TryExtractFireEmblemHeroesMessageArchive(outname, filedata))
                    {
                        AddLine(RTB_Output, string.Format("Successfully extracted Heroes Message Archive {0}", Path.GetFileName(path)));
                    }
                }
                else if (ext == ".arc")
                {
                    byte[] filedata = File.ReadAllBytes(path);
                    if (BitConverter.ToUInt32(filedata, 0) == filedata.Length || BitConverter.ToUInt32(filedata,filedata.Length-4) == 0x43524654)
                        ExtractFireEmblemArchive(Path.GetDirectoryName(path) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(path) + Path.DirectorySeparatorChar, filedata);
                }
                else if (ext == ".ctpk")
                {
                    AddText(RTB_Output, string.Format("Extracting images from {0}...", Path.GetFileName(path)));
                    Ctpk.Read(path);
                    AddLine(RTB_Output, "Complete!");
                }
                else if (ext == ".bch")
                {
                    AddText(RTB_Output, string.Format("Extracting textures from {0}...", Path.GetFileName(path)));
                    AddLine(RTB_Output, BCHTool.parseBCH(path) ? "Complete!" : "Failure!");
                }
                else if (ext == ".bfnt")
                {
                    AddText(RTB_Output, string.Format("Extracting font textures from {0}...", Path.GetFileName(path)));
                    var bfnt = File.ReadAllBytes(path);
                    if (BitConverter.ToUInt16(bfnt, 0x20) != 0x30)
                    {
                        AddLine(RTB_Output, "Failure!");
                    }
                    else
                    {
                        var w = BitConverter.ToUInt16(bfnt, 0x10);
                        var h = BitConverter.ToUInt16(bfnt, 0x12);
                        var texsize = BitConverter.ToUInt32(bfnt, 0x14);
                        var texofs = BitConverter.ToUInt32(bfnt, 0x24);
                        if (texsize != (w * h) /2)
                        {
                            AddLine(RTB_Output, "Failure!");
                        }
                        else
                        {
                            var num_textures = BitConverter.ToUInt16(bfnt, 0x1A);
                            for (var i = 0; i < num_textures; i++)
                            {
                                var dat = new byte[texsize];
                                Array.Copy(bfnt, texofs + texsize * i, dat, 0, texsize);
                                var outname = Path.GetDirectoryName(path) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(path) + "_" + i + ".png";;
                                using (var bmp = CTR.TextureUtil.DecodeByteArray(dat, w, h, CTR.TextureFormat.L4))
                                    bmp.Save(outname, ImageFormat.Png);
                            }
                            AddLine(RTB_Output, "Complete!");
                        }
                    }
                }
                else if (ext == ".txt")
                {
                    string[] textfile = File.ReadAllLines(path);
                    if (textfile.Length > 6 && textfile[0].StartsWith("MESS_ARCHIVE") && textfile[3] == "Message Name: Message" && textfile.Skip(6).All(s => s.Contains(": ")))
                    {
                            AddText(RTB_Output, string.Format("Rebuilding Message Archive from {0}...", Path.GetFileName(path)));
                            string outname = Path.GetDirectoryName(path) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(path) + ".bin";
                            byte[] arch = MakeFireEmblemMessageArchive(textfile);
                            File.WriteAllBytes(outname, arch);
                            AddLine(RTB_Output, "Complete!");
                            byte[] cmp = LZ11Compress(arch);
                            byte[] cmp2 = new byte[cmp.Length + 4];

                            cmp2[0] = 0x13;
                            Array.Copy(cmp, 0, cmp2, 4, cmp.Length);
                            Array.Copy(cmp, 1, cmp2, 1, 3);
                            File.WriteAllBytes(outname + ".lz", cmp2);
                    }
                }
            }
        }

        private void ExtractFireEmblemArchive(string outdir, byte[] archive)
        {
            if (Directory.Exists(outdir))
                Directory.Delete(outdir, true);
            Directory.CreateDirectory(outdir);

            var ShiftJIS = Encoding.GetEncoding(932);

            uint MetaOffset = BitConverter.ToUInt32(archive, 4) + 0x20;
            uint FileCount = BitConverter.ToUInt32(archive, 0x8);

            bool awakening = (BitConverter.ToUInt32(archive, 0x20) != 0);

            AddText(RTB_Output, string.Format("Extracting {0} files from {1} to {2}...", FileCount, Path.GetFileName(outdir.Substring(0, outdir.Length - 1)) + ".arc", Path.GetFileName(outdir.Substring(0, outdir.Length - 1)) + "/"));

            for (int i = 0; i < FileCount; i++)
            {
                int FileMetaOffset = 0x20 + BitConverter.ToInt32(archive, (int)MetaOffset + 4 * i);
                int FileNameOffset = BitConverter.ToInt32(archive, FileMetaOffset) + 0x20;
                // int FileIndex = BitConverter.ToInt32(archive, FileMetaOffset + 4);
                uint FileDataLength = BitConverter.ToUInt32(archive, FileMetaOffset + 8);
                int FileDataOffset = BitConverter.ToInt32(archive, FileMetaOffset + 0xC) + (awakening ? 0x20 : 0x80);
                byte[] file = new byte[FileDataLength];
                Array.Copy(archive, FileDataOffset, file, 0, FileDataLength);
                string outpath = outdir + ShiftJIS.GetString(archive.Skip(FileNameOffset).TakeWhile(b => b != 0).ToArray());
                if (!Directory.Exists(Path.GetDirectoryName(outpath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(outpath));
                File.WriteAllBytes(outpath, file);
            }

            AddLine(RTB_Output, "Complete!");
        }

        private byte[] MakeFireEmblemMessageArchive(string[] lines)
        {
            var ShiftJIS = Encoding.GetEncoding(932);
            int StringCount = lines.Length - 6;
            string[] Messages = new string[StringCount];
            string[] Names = new string[StringCount];
            uint[] MPos = new uint[StringCount];
            uint[] NPos = new uint[StringCount];
            for (int i = 6; i < lines.Length; i++)
            {
                int ind = lines[i].IndexOf(": ", StringComparison.Ordinal);
                Names[i - 6] = lines[i].Substring(0, ind);
                Messages[i - 6] = lines[i].Substring(ind + 2, lines[i].Length - (ind + 2)).Replace("\\n", "\n").Replace("\\r", "\r");
            }
            byte[] Header = new byte[0x20];
            byte[] StringTable;
            byte[] MetaTable = new byte[StringCount * 8];
            byte[] NamesTable;
            using (MemoryStream st = new MemoryStream())
            {
                using (BinaryWriter bw = new BinaryWriter(st))
                {
                    bw.Write(ShiftJIS.GetBytes(lines[0]));
                    bw.Write((byte)0);
                    while (bw.BaseStream.Position % 4 != 0)
                        bw.Write((byte)0);
                    for (int i = 0; i < StringCount; i++)
                    {
                        MPos[i] = (uint)bw.BaseStream.Position;
                        bw.Write(Encoding.Unicode.GetBytes(Messages[i]));
                        bw.Write((ushort)0);
                        while (bw.BaseStream.Position % 4 != 0)
                            bw.Write((byte)0);
                    }
                }
                StringTable = st.ToArray();
            }
            using (MemoryStream nt = new MemoryStream())
            {
                using (BinaryWriter bw = new BinaryWriter(nt))
                {
                    for (int i = 0; i < StringCount; i++)
                    {
                        NPos[i] = (uint)bw.BaseStream.Position;
                        bw.Write(ShiftJIS.GetBytes(Names[i]));
                        bw.Write((byte)0);
                    }
                }
                NamesTable = nt.ToArray();
            }
            for (int i = 0; i < StringCount; i++)
            {
                Array.Copy(BitConverter.GetBytes(MPos[i]), 0, MetaTable, (i * 8), 4);
                Array.Copy(BitConverter.GetBytes(NPos[i]), 0, MetaTable, (i * 8) + 4, 4);
            }
            byte[] Archive = new byte[Header.Length + StringTable.Length + MetaTable.Length + NamesTable.Length];
            Array.Copy(BitConverter.GetBytes(Archive.Length), Header, 4);
            Array.Copy(BitConverter.GetBytes(StringTable.Length), 0, Header, 4, 4);
            Array.Copy(BitConverter.GetBytes(StringCount), 0, Header, 0xC, 4);
            Array.Copy(Header, Archive, Header.Length);
            Array.Copy(StringTable, 0, Archive, Header.Length, StringTable.Length);
            Array.Copy(MetaTable, 0, Archive, Header.Length + StringTable.Length, MetaTable.Length);
            Array.Copy(NamesTable, 0, Archive, Header.Length + StringTable.Length + MetaTable.Length, NamesTable.Length);
            return Archive;
        }

        private string ExtractFireEmblemMessageArchive(string outname, byte[] archive)
        {
            var ShiftJIS = Encoding.GetEncoding(932);
            string ArchiveName = ShiftJIS.GetString(archive.Skip(0x20).TakeWhile(b => b != 0).ToArray()); // Archive Name.
            uint TextPartitionLen = BitConverter.ToUInt32(archive, 4);
            uint StringCount = BitConverter.ToUInt32(archive, 0xC);
            string[] MessageNames = new string[StringCount];
            string[] Messages = new string[StringCount];

            uint StringMetaOffset = 0x20 + TextPartitionLen;
            uint NamesOffset = StringMetaOffset + 0x8 * StringCount;

            for (int i = 0; i < StringCount; i++)
            {
                int MessageOffset = 0x20+BitConverter.ToInt32(archive, (int)StringMetaOffset + 0x8 * i);
                int MessageLen = 0;
                while (BitConverter.ToUInt16(archive, MessageOffset + MessageLen) != 0)
                    MessageLen += 2;
                Messages[i] = Encoding.Unicode.GetString(archive.Skip(MessageOffset).Take(MessageLen).ToArray()).Replace("\n","\\n").Replace("\r","\\r");
                int NameOffset = (int)NamesOffset + BitConverter.ToInt32(archive, (int)StringMetaOffset + (0x8 * i) + 4);
                MessageNames[i] = ShiftJIS.GetString(archive.Skip(NameOffset).TakeWhile(b => b != 0).ToArray());
            }

            List<string> Lines = new List<string>
            {
                ArchiveName,
                Environment.NewLine,
                "Message Name: Message",
                Environment.NewLine
            };
            for (int i = 0; i < StringCount; i++)
                Lines.Add(string.Format("{0}: {1}", MessageNames[i], Messages[i]));
            File.WriteAllLines(outname, Lines);

            return ArchiveName;
        }

        private bool TryExtractFireEmblemHeroesMessageArchive(string outname, byte[] archive)
        {
            if (archive.Length < 0x28)
                return false;

            var encoding = Encoding.UTF8;

            var len = BitConverter.ToUInt32(archive, 0);
            var string_table_end = BitConverter.ToUInt32(archive, 4);
            if (len != archive.Length)
                return false;
            if (string_table_end > archive.Length)
                return false;

            var num_strings = BitConverter.ToUInt64(archive, 0x20);

            var is_message_archive = (BitConverter.ToUInt64(archive, (int) string_table_end + 0x20) == 8) &&
                         (BitConverter.ToUInt64(archive, archive.Length - 8) == num_strings*0x10);

            is_message_archive |= ((num_strings*0x10 + string_table_end + 0x20) != (ulong) archive.Length);

            is_message_archive &= num_strings < ulong.MaxValue;

            if (!is_message_archive)
                return false;

            // Okay this is probably a message archive.
            var dec_archive = (byte[]) archive.Clone();
            var names = new string[num_strings];
            var messages = new string[num_strings];

            // This isn't how the game internally does it, but the game's cipher reduces to this.
            var xorkey = new byte[] { 0x58, 0xDF, 0x3F, 0x59, 0x39, 0x85, 0x30, 0xB1, 0x2D, 0xB0, 0x80, 0x13, 0xB3, 0xCB, 0x25, 0xB0, 0xE8, 0x5D, 0x2E, 0x29, 0xBF, 0xC9, 0xEA, 0x70, 0x33, 0x7B, 0xE6, 0xD3, 0xD2 };

            is_message_archive = false;

            try
            {
                for (var i = (ulong)0; i < num_strings; i++)
                {
                    var name_ofs = BitConverter.ToUInt64(archive, (int)(0x28 + i * 0x10)) + 0x20;
                    var str_ofs = BitConverter.ToUInt64(archive, (int)(0x30 + i * 0x10)) + 0x20;

                    if ((name_ofs < 0x28 + num_strings * 0x10 || name_ofs > string_table_end + 0x20) && name_ofs != 0x20)
                        return false;

                    if ((str_ofs < 0x28 + num_strings * 0x10 || str_ofs > string_table_end + 0x20) && str_ofs != 0x20)
                        return false;


                    var n_len = 0;
                    var s_len = 0;

                    if (name_ofs != 0x20)
                    {
                        var cur_k = (xorkey[0] + xorkey[1]) & 0xFF;
                        while (dec_archive[n_len + (int)name_ofs] != 0)
                        {
                            cur_k ^= xorkey[n_len % xorkey.Length];
                            if (cur_k != dec_archive[n_len + (int)name_ofs])
                                dec_archive[n_len + (int)name_ofs] ^= (byte)cur_k;
                            n_len++;
                        }
                        names[i] = encoding.GetString(dec_archive, (int)name_ofs, n_len).Replace("\n", "\\n").Replace("\r", "\\r");
                    }
                    else
                    {
                        names[i] = "";
                    }

                    if (str_ofs != 0x20)
                    {
                        var cur_k = (xorkey[0] + xorkey[1]) & 0xFF;
                        while (dec_archive[s_len + (int)str_ofs] != 0)
                        {
                            cur_k ^= xorkey[s_len % xorkey.Length];
                            if (cur_k != dec_archive[s_len + (int)str_ofs])
                                dec_archive[s_len + (int)str_ofs] ^= (byte)cur_k;
                            s_len++;
                        }
                        messages[i] = encoding.GetString(dec_archive, (int)str_ofs, s_len).Replace("\n", "\\n").Replace("\r", "\\r");
                    }
                    else
                    {
                        messages[i] = "";
                    }


                    if (!is_message_archive)
                    {
                        is_message_archive = names[i].StartsWith("M") && names[i].Substring(0, 8).Contains("ID_");
                    }
                }
            }
            catch
            {
                return false;
            }

            if (!is_message_archive)
                return false;

            var Lines = new List<string>
            {
                "[Heroes Archive] (" + Path.GetFileNameWithoutExtension(outname) + ")",
                Environment.NewLine,
                "Heroes Message Name: Message",
                Environment.NewLine
            };

            for (var i = (ulong)0; i < num_strings; i++)
                Lines.Add(string.Format("{0}: {1}", names[i], messages[i]));
            File.WriteAllLines(outname, Lines);

            return true;
        }

        private void TB_FilePath_TextChanged(object sender, EventArgs e)
        {
            Selected_Path = TB_FilePath.Text;
        }

        private byte[] LZ11Decompress(byte[] compressed)
        {
            using (MemoryStream cstream = new MemoryStream(compressed))
            {
                using (MemoryStream dstream = new MemoryStream())
                {
                    (new LZ11()).Decompress(cstream, compressed.Length, dstream);
                    return dstream.ToArray();
                }
            }
        }

        private byte[] LZ11Compress(byte[] decompressed)
        {
            using (MemoryStream dstream = new MemoryStream(decompressed))
            {
                using (MemoryStream cstream = new MemoryStream())
                {
                    (new LZ11()).Compress(dstream, decompressed.Length, cstream);
                    return cstream.ToArray();
                }
            }
        }

        private void AddText(RichTextBox RTB, string msg)
        {
            if (RTB.InvokeRequired)
                RTB.Invoke(new Action(() => RTB.AppendText(msg)));
            else
                RTB.AppendText(msg);
        }

        private void AddLine(RichTextBox RTB, string line)
        {
            if (RTB.InvokeRequired)
                RTB.Invoke(new Action(() => RTB.AppendText(line + Environment.NewLine)));
            else
                RTB.AppendText(line + Environment.NewLine);
        }

        private void RTB_Output_Click(object sender, EventArgs e)
        {
            RTB_Output.Clear();
            RTB_Output.Text = "Open a file, or Drag/Drop several! Click this box to clear its text." + Environment.NewLine;
        }
    }
}
