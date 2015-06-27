// Code from Normmatt's texturipper (with permission)
using System;
using System.Drawing;

namespace ctpktool
{
  internal class Etc
  {
    private static readonly int[,] CompressParams =
    {
        {
            -8, -2, 2, 8
        },
        {
            -17, -5, 5, 17
        },
        {
            -29, -9, 9, 29
        },
        {
            -42, -13, 13, 42
        },
        {
            -60, -18, 18, 60
        },
        {
            -80, -24, 24, 80
        },
        {
            -106, -33, 33, 106
        },
        {
            -183, -47, 47, 183
        }
    };

    private static readonly uint[] Unscramble =
    {
        2U, 3U, 1U, 0U
    };

    static Etc()
    {
    }

    private static int GetEtc1BlockStart(Size size, int x, int y, bool hasAlpha)
    {
      int num1 = x / 4;
      int num2 = y / 4;
      return ((x / 8 + y / 8 * (size.Width / 8)) * 4 + (num1 & 1) + (num2 & 1) * 2) * (hasAlpha ? 16 : 8);
    }

    public static int GetEtc1Length(Size size, bool hasAlpha, int levels = 1)
    {
      int num1 = 0;
      int num2 = hasAlpha ? 16 : 8;
      int num3;
      if (size.Width % 4 != 0)
        num3 = 0;
      else if (size.Height % 4 != 0)
      {
        num3 = 0;
      }
      else
      {
        for (int index = 0; index < levels; ++index)
        {
          int num4 = size.Height >> index;
          if (size.Width >> index >= 4 && num4 >= 4)
            num1 += num2 * num4 / 4 * (size.Width >> index) / 4;
          else
            break;
        }
        num3 = num1;
      }
      return num3;
    }

    public static void GetEtc1RasterData(byte[] imageData, Size size, int y, bool hasAlpha, byte[] output, int outputOffset)
    {
      int width = size.Width;
      int num1 = hasAlpha ? 8 : 0;
      Color[,] colorArray = new Color[4, 4];
      int x = 0;
      int num2 = 0;
      while (x != width)
      {
        int index1 = y & 3;
        int etc1BlockStart = GetEtc1BlockStart(size, x, y, hasAlpha);
        const ulong num3 = 0UL;
        ulong num4 = !hasAlpha ? ulong.MaxValue : num3 | imageData[etc1BlockStart] | (ulong) imageData[etc1BlockStart + 1] << 8 | (ulong) imageData[etc1BlockStart + 2] << 16 | (ulong) imageData[etc1BlockStart + 3] << 24 | (ulong) imageData[etc1BlockStart + 4] << 32 | (ulong) imageData[etc1BlockStart + 5] << 40 | (ulong) imageData[etc1BlockStart + 6] << 48 | (ulong) imageData[etc1BlockStart + 7] << 56;
        ulong num5 = 0UL | imageData[etc1BlockStart + num1] | (ulong) imageData[etc1BlockStart + num1 + 1] << 8 | (ulong) imageData[etc1BlockStart + num1 + 2] << 16 | (ulong) imageData[etc1BlockStart + num1 + 3] << 24 | (ulong) imageData[etc1BlockStart + num1 + 4] << 32 | (ulong) imageData[etc1BlockStart + num1 + 5] << 40 | (ulong) imageData[etc1BlockStart + num1 + 6] << 48 | (ulong) imageData[etc1BlockStart + num1 + 7] << 56;
        bool flag1 = ((long) num5 & 4294967296L) != 0L;
        bool flag2 = ((long) num5 & 8589934592L) != 0L;
        uint num6 = (uint) (num5 >> 37 & 7UL);
        uint num7 = (uint) (num5 >> 34 & 7UL);
        int num8;
        int num9;
        int num10;
        int num11;
        int num12;
        int num13;
        if (flag2)
        {
          sbyte num14 = (sbyte) ((long) (num5 >> 56) & 7L);
          sbyte num15 = (sbyte) ((long) (num5 >> 48) & 7L);
          sbyte num16 = (sbyte) ((long) (num5 >> 40) & 7L);
          sbyte num17 = (sbyte) (num14 << 5);
          sbyte num18 = (sbyte) (num15 << 5);
          sbyte num19 = (sbyte) (num16 << 5);
          sbyte num20 = (sbyte) (num17 >> 5);
          sbyte num21 = (sbyte) (num18 >> 5);
          sbyte num22 = (sbyte) (num19 >> 5);
          int num23 = (int) (num5 >> 59) & 31;
          int num24 = (int) (num5 >> 51) & 31;
          int num25 = (int) (num5 >> 43) & 31;
          int num26 = num23 + num20;
          int num27 = num24 + num21;
          int num28 = num25 + num22;
          num8 = num23 * byte.MaxValue / 31;
          num9 = num24 * byte.MaxValue / 31;
          num10 = num25 * byte.MaxValue / 31;
          num11 = num26 * byte.MaxValue / 31;
          num12 = num27 * byte.MaxValue / 31;
          num13 = num28 * byte.MaxValue / 31;
        }
        else
        {
          num8 = (int) ((num5 >> 60 & 15UL) * byte.MaxValue / 15UL);
          num11 = (int) ((num5 >> 56 & 15UL) * byte.MaxValue / 15UL);
          num9 = (int) ((num5 >> 52 & 15UL) * byte.MaxValue / 15UL);
          num12 = (int) ((num5 >> 48 & 15UL) * byte.MaxValue / 15UL);
          num10 = (int) ((num5 >> 44 & 15UL) * byte.MaxValue / 15UL);
          num13 = (int) ((num5 >> 40 & 15UL) * byte.MaxValue / 15UL);
        }
        uint num29 = (uint) (num5 >> 16 & ushort.MaxValue);
        uint num30 = (uint) (num5 & ushort.MaxValue);
        int num31 = flag1 ? 4 : 2;
        int num32 = flag1 ? 2 : 4;
        int num33 = 0;
        for (int index2 = 0; index2 != num31; ++index2)
        {
          int index3 = 0;
          while (index3 != num32)
          {
            if (index3 == index1)
            {
              uint num14 = (uint) (((int) (num29 >> num33) & 1) << 1 | (int) (num30 >> num33) & 1);
              uint num15 = Unscramble[num14];
              int num16 = num8 + CompressParams[(int) (IntPtr) num6, (int) (IntPtr) num15];
              int num17 = num9 + CompressParams[(int) (IntPtr) num6, (int) (IntPtr) num15];
              int num18 = num10 + CompressParams[(int) (IntPtr) num6, (int) (IntPtr) num15];
              int red = Clamp(num16, 0, byte.MaxValue);
              int green = Clamp(num17, 0, byte.MaxValue);
              int blue = Clamp(num18, 0, byte.MaxValue);
              int num19 = index2 * 4 + index3;
              int alpha = (int) ((long) (num4 >> num19 * 4) & 15L) * byte.MaxValue / 15;
              colorArray[index3, index2] = Color.FromArgb(alpha, red, green, blue);
            }
            ++index3;
            ++num33;
          }
          if (flag1)
            num33 += 2;
        }
        int num34 = flag1 ? 0 : 2;
        int num35 = flag1 ? 2 : 0;
        int num36 = flag1 ? 2 : 8;
        for (int index2 = num34; index2 != 4; ++index2)
        {
          int index3 = num35;
          while (index3 != 4)
          {
            if (index3 == index1)
            {
              uint num14 = (uint) (((int) (num29 >> num36) & 1) << 1 | (int) (num30 >> num36) & 1);
              uint num15 = Unscramble[num14];
              int num16 = num11 + CompressParams[(int) (IntPtr) num7, (int) (IntPtr) num15];
              int num17 = num12 + CompressParams[(int) (IntPtr) num7, (int) (IntPtr) num15];
              int num18 = num13 + CompressParams[(int) (IntPtr) num7, (int) (IntPtr) num15];
              int red = Clamp(num16, 0, byte.MaxValue);
              int green = Clamp(num17, 0, byte.MaxValue);
              int blue = Clamp(num18, 0, byte.MaxValue);
              int num19 = index2 * 4 + index3;
              int alpha = (int) ((long) (num4 >> num19 * 4) & 15L) * byte.MaxValue / 15;
              colorArray[index3, index2] = Color.FromArgb(alpha, red, green, blue);
            }
            ++index3;
            ++num36;
          }
          if (flag1)
            num36 += 2;
        }
        int index4 = num2;
        int index5 = num2 + 4;
        int index6 = num2 + 8;
        int index7 = num2 + 12;
        output[index4] = colorArray[index1, 0].A;
        output[index4 + 1] = colorArray[index1, 0].R;
        output[index4 + 2] = colorArray[index1, 0].G;
        output[index4 + 3] = colorArray[index1, 0].B;
        output[index5] = colorArray[index1, 1].A;
        output[index5 + 1] = colorArray[index1, 1].R;
        output[index5 + 2] = colorArray[index1, 1].G;
        output[index5 + 3] = colorArray[index1, 1].B;
        output[index6] = colorArray[index1, 2].A;
        output[index6 + 1] = colorArray[index1, 2].R;
        output[index6 + 2] = colorArray[index1, 2].G;
        output[index6 + 3] = colorArray[index1, 2].B;
        output[index7] = colorArray[index1, 3].A;
        output[index7 + 1] = colorArray[index1, 3].R;
        output[index7 + 2] = colorArray[index1, 3].G;
        output[index7 + 3] = colorArray[index1, 3].B;
        x += 4;
        num2 += 16;
      }
    }

    public static void GetEtc1RasterData(byte[] imageData, Size size, int y, bool hasAlpha, Bitmap bmp, int outputOffset)
    {
      int width = size.Width;
      int num1 = hasAlpha ? 8 : 0;
      Color[,] colorArray = new Color[4, 4];
      int x = 0;
      while (x != width)
      {
        int index1 = y & 3;
        int etc1BlockStart = GetEtc1BlockStart(size, x, y, hasAlpha);
        const ulong num2 = 0UL;
        ulong num3 = !hasAlpha ? ulong.MaxValue : num2 | imageData[etc1BlockStart] | (ulong) imageData[etc1BlockStart + 1] << 8 | (ulong) imageData[etc1BlockStart + 2] << 16 | (ulong) imageData[etc1BlockStart + 3] << 24 | (ulong) imageData[etc1BlockStart + 4] << 32 | (ulong) imageData[etc1BlockStart + 5] << 40 | (ulong) imageData[etc1BlockStart + 6] << 48 | (ulong) imageData[etc1BlockStart + 7] << 56;
        ulong num4 = 0UL | imageData[etc1BlockStart + num1] | (ulong) imageData[etc1BlockStart + num1 + 1] << 8 | (ulong) imageData[etc1BlockStart + num1 + 2] << 16 | (ulong) imageData[etc1BlockStart + num1 + 3] << 24 | (ulong) imageData[etc1BlockStart + num1 + 4] << 32 | (ulong) imageData[etc1BlockStart + num1 + 5] << 40 | (ulong) imageData[etc1BlockStart + num1 + 6] << 48 | (ulong) imageData[etc1BlockStart + num1 + 7] << 56;
        bool flag1 = ((long) num4 & 4294967296L) != 0L;
        bool flag2 = ((long) num4 & 8589934592L) != 0L;
        uint num5 = (uint) (num4 >> 37 & 7UL);
        uint num6 = (uint) (num4 >> 34 & 7UL);
        int num7;
        int num8;
        int num9;
        int num10;
        int num11;
        int num12;
        if (flag2)
        {
          sbyte num13 = (sbyte) ((long) (num4 >> 56) & 7L);
          sbyte num14 = (sbyte) ((long) (num4 >> 48) & 7L);
          sbyte num15 = (sbyte) ((long) (num4 >> 40) & 7L);
          sbyte num16 = (sbyte) (num13 << 5);
          sbyte num17 = (sbyte) (num14 << 5);
          sbyte num18 = (sbyte) (num15 << 5);
          sbyte num19 = (sbyte) (num16 >> 5);
          sbyte num20 = (sbyte) (num17 >> 5);
          sbyte num21 = (sbyte) (num18 >> 5);
          int num22 = (int) (num4 >> 59) & 31;
          int num23 = (int) (num4 >> 51) & 31;
          int num24 = (int) (num4 >> 43) & 31;
          int num25 = num22 + num19;
          int num26 = num23 + num20;
          int num27 = num24 + num21;
          num7 = num22 * byte.MaxValue / 31;
          num8 = num23 * byte.MaxValue / 31;
          num9 = num24 * byte.MaxValue / 31;
          num10 = num25 * byte.MaxValue / 31;
          num11 = num26 * byte.MaxValue / 31;
          num12 = num27 * byte.MaxValue / 31;
        }
        else
        {
          num7 = (int) ((num4 >> 60 & 15UL) * byte.MaxValue / 15UL);
          num10 = (int) ((num4 >> 56 & 15UL) * byte.MaxValue / 15UL);
          num8 = (int) ((num4 >> 52 & 15UL) * byte.MaxValue / 15UL);
          num11 = (int) ((num4 >> 48 & 15UL) * byte.MaxValue / 15UL);
          num9 = (int) ((num4 >> 44 & 15UL) * byte.MaxValue / 15UL);
          num12 = (int) ((num4 >> 40 & 15UL) * byte.MaxValue / 15UL);
        }
        uint num28 = (uint) (num4 >> 16 & ushort.MaxValue);
        uint num29 = (uint) (num4 & ushort.MaxValue);
        int num30 = flag1 ? 4 : 2;
        int num31 = flag1 ? 2 : 4;
        int num32 = 0;
        for (int index2 = 0; index2 != num30; ++index2)
        {
          int index3 = 0;
          while (index3 != num31)
          {
            if (index3 == index1)
            {
              uint num13 = (uint) (((int) (num28 >> num32) & 1) << 1 | (int) (num29 >> num32) & 1);
              uint num14 = Unscramble[num13];
              int num15 = num7 + CompressParams[(int) (IntPtr) num5, (int) (IntPtr) num14];
              int num16 = num8 + CompressParams[(int) (IntPtr) num5, (int) (IntPtr) num14];
              int num17 = num9 + CompressParams[(int) (IntPtr) num5, (int) (IntPtr) num14];
              int red = Clamp(num15, 0, byte.MaxValue);
              int green = Clamp(num16, 0, byte.MaxValue);
              int blue = Clamp(num17, 0, byte.MaxValue);
              int num18 = index2 * 4 + index3;
              int alpha = (int) ((long) (num3 >> num18 * 4) & 15L) * byte.MaxValue / 15;
              colorArray[index3, index2] = Color.FromArgb(alpha, red, green, blue);
            }
            ++index3;
            ++num32;
          }
          if (flag1)
            num32 += 2;
        }
        int num33 = flag1 ? 0 : 2;
        int num34 = flag1 ? 2 : 0;
        int num35 = flag1 ? 2 : 8;
        for (int index2 = num33; index2 != 4; ++index2)
        {
          int index3 = num34;
          while (index3 != 4)
          {
            if (index3 == index1)
            {
              uint num13 = (uint) (((int) (num28 >> num35) & 1) << 1 | (int) (num29 >> num35) & 1);
              uint num14 = Unscramble[num13];
              int num15 = num10 + CompressParams[(int) (IntPtr) num6, (int) (IntPtr) num14];
              int num16 = num11 + CompressParams[(int) (IntPtr) num6, (int) (IntPtr) num14];
              int num17 = num12 + CompressParams[(int) (IntPtr) num6, (int) (IntPtr) num14];
              int red = Clamp(num15, 0, byte.MaxValue);
              int green = Clamp(num16, 0, byte.MaxValue);
              int blue = Clamp(num17, 0, byte.MaxValue);
              int num18 = index2 * 4 + index3;
              int alpha = (int) ((long) (num3 >> num18 * 4) & 15L) * byte.MaxValue / 15;
              colorArray[index3, index2] = Color.FromArgb(alpha, red, green, blue);
            }
            ++index3;
            ++num35;
          }
          if (flag1)
            num35 += 2;
        }
        bmp.SetPixel(x, y, colorArray[index1, 0]);
        bmp.SetPixel(x + 1, y, colorArray[index1, 1]);
        bmp.SetPixel(x + 2, y, colorArray[index1, 2]);
        bmp.SetPixel(x + 3, y, colorArray[index1, 3]);
        x += 4;
      }
    }

    public static int Clamp(int value, int min, int max)
    {
        if (value < min)
            return min;
        if (max < value)
            return max;
        return value;
    }
  }
}
