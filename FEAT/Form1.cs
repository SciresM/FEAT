using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
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
            this.AllowDrop = this.RTB_Output.AllowDrop = true;
            this.DragEnter += new DragEventHandler(Form1_DragEnter);
            this.RTB_Output.DragEnter += new DragEventHandler(Form1_DragEnter);
            this.DragDrop += new DragEventHandler(Form1_DragDrop);
            this.RTB_Output.DragDrop += new DragEventHandler(Form1_DragDrop);
        }

        private volatile int threads = 0;
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
            if (Control.ModifierKeys == Keys.Alt)
                dialog = new FolderBrowserDialog();
            else
                dialog = new OpenFileDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                if (dialog is OpenFileDialog)
                    TB_FilePath.Text = (dialog as OpenFileDialog).FileName;
                else if (dialog is FolderBrowserDialog)
                    TB_FilePath.Text = (dialog as FolderBrowserDialog).SelectedPath;
                else
                    TB_FilePath.Text = string.Empty;
                B_Go.Enabled = true;
            }
        }

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
                if (ext == ".lz")
                {
                    byte[] filedata = File.ReadAllBytes(path);
                    string decpath = Path.GetDirectoryName(path) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(path);
                    if ((BitConverter.ToUInt32(filedata, 0) & 0xFFFF0000) == (BitConverter.ToUInt32(filedata, 4) & 0xFFFF0000)
                        && filedata[0] == 0x13
                        && filedata[4] == 0x11) // "LZ13"
                    {
                        filedata = filedata.Skip(4).ToArray();
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
                    if (BitConverter.ToUInt32(filedata, 0) == filedata.Length &&
                        new string(filedata.Skip(0x20).Take(0xC).Select(c => (char)c).ToArray()) == "MESS_ARCHIVE")
                    {
                        string archive_name = ExtractFireEmblemMessageArchive(Path.GetDirectoryName(path) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(path) + ".txt", filedata);
                        AddLine(RTB_Output, string.Format("Successfully Extracted {0} ({1}).", archive_name, Path.GetFileName(path)));
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
                else if (ext == ".txt")
                {
                    string[] textfile = File.ReadAllLines(path);
                    if (textfile.Length > 6 && textfile[0].StartsWith("MESS_ARCHIVE") && textfile[3] == "Message Name: Message" && !textfile.Skip(6).Any(s => !s.Contains(": ")))
                    {
                        DialogResult dr = DialogResult.Cancel;
                        if (this.InvokeRequired)
                            this.Invoke(new Action(() => dr = MessageBox.Show(string.Format("Found Message Archive .txt file ({0}). Rebuild archive?", Path.GetFileName(path)), "Prompt", MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk)));
                        else
                            dr = MessageBox.Show(string.Format("Found Message Archive .txt file ({0}). Rebuild archive?", Path.GetFileName(path)), "Prompt", MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk);
                        if (dr == DialogResult.Yes)
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
                int FileIndex = BitConverter.ToInt32(archive, FileMetaOffset + 4);
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
                int ind = lines[i].IndexOf(": ");
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

            List<string> Lines = new List<string>();
            Lines.Add(ArchiveName);
            Lines.Add(Environment.NewLine);
            Lines.Add("Message Name: Message");
            Lines.Add(Environment.NewLine);
            for (int i = 0; i < StringCount; i++)
                Lines.Add(string.Format("{0}: {1}", MessageNames[i], Messages[i]));
            File.WriteAllLines(outname, Lines);

            return ArchiveName;
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
