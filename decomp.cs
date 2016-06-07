using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace _4932a2
{
    //the decompression class has all the same class members as the compression for my own mental simplicity
    class decomp
    {
        public double[,] Y, Cb, Cr;
        public Bitmap bmp { get; set; }
        public Bitmap bmp2 { get; set; }
        public int width, height;
        private byte[] byteArray;
        List<MVPair> mvList;
        int index = 0;

        public struct MVPair
        {
            public int x, y;

            public MVPair(int p1, int p2)
            {
                x = p1;
                y = p2;
            }
        }

        readonly double[,] luminance = {
            { 16, 11, 10, 16, 24, 40, 51, 61 },
            { 12, 12, 14, 19, 26, 58, 60, 55 },
            { 14, 13, 16, 24, 40, 57, 69, 56 },
            { 14, 17, 22, 29, 51, 87, 80, 62 },
            { 18, 22, 37, 56, 68, 109, 103, 77 },
            { 24, 35, 55, 64, 81, 104, 113, 92 },
            { 49, 64, 78, 87, 103, 121, 120, 101 },
            { 72, 92, 95, 98, 112, 100, 103, 99 }};

        readonly double[,] chrominance = {
            { 17, 18, 24, 27, 47, 99, 99, 99 },
            { 18, 21, 26, 66, 99, 99, 99, 99 },
            { 24, 26, 56, 99, 99, 99, 99, 99 },
            { 47, 66, 99, 99, 99, 99, 99, 99 },
            { 99, 99, 99, 99, 99, 99, 99, 99 },
            { 99, 99, 99, 99, 99, 99, 99, 99 },
            { 99, 99, 99, 99, 99, 99, 99, 99 },
            { 99, 99, 99, 99, 99, 99, 99, 99 }};

        public decomp()
        {
            mvList = new List<MVPair>();
        }

        //this is the entire decompression routine function.
        public void daDecomp()
        {
            //open the file!
            byteArray = File.ReadAllBytes("thefile.cam");
            //read width and height
            width = BitConverter.ToInt32(byteArray, 0);
            height = BitConverter.ToInt32(byteArray, 4);
            int width2 = (int)Math.Ceiling(width / 2.0);
            int height2 = (int)Math.Ceiling(height / 2.0);
            //read the first frame channels
            int yCount = BitConverter.ToInt32(byteArray, 8);
            int CbCount = BitConverter.ToInt32(byteArray, 12);
            int CrCount = BitConverter.ToInt32(byteArray, 16);

            //read the 2nd frame channels
            int diffYCount = BitConverter.ToInt32(byteArray, 20);
            int diffCbCount = BitConverter.ToInt32(byteArray, 24);
            int diffCrCount = BitConverter.ToInt32(byteArray, 28);
            //intialize the Y Cb Cr for class!
            Y = new double[width, height];
            Cb = new double[width, height];
            Cr = new double[width, height];

            //rewrite array w/o counts, makes indexing SIMPLE!
            Array.Copy(byteArray, 32, byteArray, 0, byteArray.Length - 32);
            //decode the Y Cb Cr lists into their channel arrays
            DecodeChannel(Y, 0, yCount, width, height, luminance);
            DecodeChannel(Cb, yCount, yCount + CbCount, width2, height2, chrominance);
            DecodeChannel(Cr, yCount + CbCount, yCount + CbCount + CrCount, width2, height2, chrominance);

            //initialize the difference Y Cb Cr!
            double[,] diffY = new double[width, height];
            double[,] diffCb = new double[width, height];
            double[,] diffCr = new double[width, height];
            //decode the Y Cb Cr difference lists into their channel arrays
            int iFrameCount = yCount + CbCount + CrCount;
            DecodeChannel(diffY, iFrameCount, iFrameCount + diffYCount, width, height, luminance);
            DecodeChannel(diffCb, iFrameCount + diffYCount, iFrameCount + diffYCount + diffCbCount, width2, height2, chrominance);
            DecodeChannel(diffCr, iFrameCount + diffYCount + diffCbCount, iFrameCount + diffYCount + diffCbCount + diffCrCount, width2, height2, chrominance);
            //INITIALIZE the motion vector structured arrays!
            MVPair[,] YMV = new MVPair[(int)Math.Ceiling(width / 8.0), (int)Math.Ceiling(height / 8.0)];
            MVPair[,] CbMV = new MVPair[(int)Math.Ceiling(width2 / 8.0), (int)Math.Ceiling(height2 / 8.0)];
            MVPair[,] CrMV = new MVPair[(int)Math.Ceiling(width2 / 8.0), (int)Math.Ceiling(height2 / 8.0)];

            //rewrite the MVs at the start of the array
            int countsAndChannels = iFrameCount + diffYCount + diffCbCount + diffCrCount;
            Array.Copy(byteArray, countsAndChannels, byteArray, 0, byteArray.Length - countsAndChannels);

            //read the bytes as ints and get the 2's complements back as initial values
            copyMVs(YMV);
            copyMVs(CbMV);
            copyMVs(CrMV);

            //build p frame y cb cr channels from differences and motion vectors
            implementMotionVectors(YMV, diffY, diffY.GetLength(0), diffY.GetLength(1), Y);
            implementMotionVectors(CbMV, diffCb, diffCb.GetLength(0) / 2, diffCb.GetLength(1) / 2, Cb);
            implementMotionVectors(CrMV, diffCr, diffCr.GetLength(0) / 2, diffCr.GetLength(1) / 2, Cr);

            //supersample iframe cb cr and switch back to rgb and write to the iframe bmp
            bmp = new Bitmap(width, height);
            supersample();
            convertYCbCr2RGB();

            //supersample pframe cb cr and switch back to rgb and write to the pframe bmp
            bmp2 = new Bitmap(width, height);
            double[,] superCb = new double[width, height];
            double[,] superCr = new double[width, height];
            int sx = 0, sy = 0;
            for (int x = 0; x < width; x = x + 2)
            {
                for (int y = 0; y < height; y = y + 2)
                {
                    superCb[x, y] = diffCb[sx, sy];
                    if (x < width - 1) superCb[x + 1, y] = diffCb[sx, sy];
                    if (y < height - 1) superCb[x, y + 1] = diffCb[sx, sy];
                    if (x < width - 1 && y < height - 1) superCb[x + 1, y + 1] = diffCb[sx, sy];

                    superCr[x, y] = diffCr[sx, sy];
                    if (x < width - 1) superCr[x + 1, y] = diffCr[sx, sy];
                    if (y < height - 1) superCr[x, y + 1] = diffCr[sx, sy];
                    if (x < width - 1 && y < height - 1) superCr[x + 1, y + 1] = diffCr[sx, sy];
                    sy++;
                }
                sx++;
                sy = 0;
            }
            diffCb = superCb;
            diffCr = superCr;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    double thisY = diffY[x, y];
                    double ThisCb = diffCb[x, y];
                    double thisCr = diffCr[x, y];

                    int r = (int)(1.164 * (thisY - 16) + 1.596 * (thisCr - 128));
                    int g = (int)(1.164 * (thisY - 16) - 0.813 * (thisCr - 128) - 0.392 * (ThisCb - 128));
                    int b = (int)(1.164 * (thisY - 16) + 2.017 * (ThisCb - 128));

                    r = Math.Max(0, Math.Min(255, r));
                    g = Math.Max(0, Math.Min(255, g));
                    b = Math.Max(0, Math.Min(255, b));

                    bmp2.SetPixel(x, y, Color.FromArgb(r, g, b));
                }
            }
        }
        //this is a simple function to copy each motion vector into a structured array
        //simplifies indexing when reconstructing the pixels
        private void copyMVs(MVPair[,] MVArray)
        {
            for (int i = 0; i < MVArray.GetLength(0); i++)
            {
                for (int j = 0; j < MVArray.GetLength(1); j++)
                {
                    int mvX = byteArray[index++];
                    int mvY = byteArray[index++];
                    if (mvX > 127) mvX -= 256;
                    if (mvY > 127) mvY -= 256;
                    //Debug.Write("(" + mvX + ", " + mvY + ") " + Environment.NewLine);

                    MVArray[i, j] = new MVPair(mvX, mvY);
                }
            }
        }
        //this is the function that implements the motion vectors and differences
        //creates the 2nd frame!
        private void implementMotionVectors(MVPair[,] MVArray, double[,] diffChannel, int chanWidth, int chanHeight, double[,] iFrameChannel)
        {
            for (int i = 0; i < chanWidth; i++)
            {
                for (int j = 0; j < chanHeight; j++)
                {
                    if (diffChannel[i, j] > 127) diffChannel[i, j] -= 256;
                    diffChannel[i, j] = (
                                          iFrameChannel[i + MVArray[i / 8, j / 8].x, j + MVArray[i / 8, j / 8].y]
                                          -
                                          diffChannel[i, j] / 3.5
                                         );
                }
            }
        }
        //this function takes a section of the file that is known to be a channel,
        //and performs the iDCT, iQuantize and decode to give back the channel
        private void DecodeChannel(double[,] channel, int begin, int end, int widf, int hye, double[,] table)
        {
            List<double> decodeList = new List<double>();
            double[] temp = new double[64];
            int startx = 0;
            int starty = 0;
            int m = 0, n = 0;
            for (int i = begin; i < end; i++)
            {
                decodeList.Add(byteArray[i] > 127 ? byteArray[i] - 256 : byteArray[i]);
            }

            decodeList = modifiedRunLengthDecode(decodeList);
            
            double[] listArray = decodeList.ToArray();
            

            for (int i = 0; i < decodeList.Count; i += 64)
            {
                Array.Copy(listArray, i, temp, 0, 64);

                double[,] quanBlock = IQuantize(temp, table);
                quanBlock = inverseDCT(quanBlock);
                for (int x = 0; x < 8; x++)
                {
                    for (int y = 0; y < 8; y++)
                    {
                        quanBlock[x, y] += 128;
                    }
                }

                for (int x = startx; x < startx + 8; x++)
                {
                    for (int y = starty; y < starty + 8; y++)
                        if (x < widf && y < hye)
                            channel[x, y] = quanBlock[m, n++];

                    n = 0;
                    m++;
                }

                m = 0;
                n = 0;

                starty += 8;
                if (starty >= hye)
                {
                    startx += 8;
                    starty = 0;
                }
            }

            decodeList.Clear();
        }
        //this is the modified run length dencode function
        private static List<double> modifiedRunLengthDecode(List<double> deList)
        {
            List<double> newList = new List<double>();

            for (int x = 0; x < deList.Count; x++)
            {
                if (deList[x] == 127)
                {
                    for (int u = 0; u < deList[x + 1]; u++)
                    {
                        newList.Add(deList[x + 2]);
                    }
                    x = x + 2;
                }
                else
                {
                    newList.Add(deList[x]);
                }
            }
            return newList;
        }

        //inverse DCT function
        public double[,] inverseDCT(double[,] F)
        {
            double[,] f = new double[8, 8];
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    double sumtotal = 0;
                    for (int u = 0; u < 8; u++)
                    {
                        for (int v = 0; v < 8; v++)
                        {
                            double Cu = u == 0 ? Math.Sqrt(2) / 2 : 1;
                            double Cv = v == 0 ? Math.Sqrt(2) / 2 : 1;
                            sumtotal += (Cu * Cv / 4
                                * Math.Cos(((2 * i + 1) * u * Math.PI) / 16)
                                * Math.Cos(((2 * j + 1) * v * Math.PI) / 16)
                                * F[u, v]);
                        }
                    }
                    if (sumtotal > 0)
                    {
                        f[i, j] = (int)Math.Round(sumtotal);
                    }
                    else if (sumtotal < 0)
                    {
                        f[i, j] = (int)Math.Floor(sumtotal);
                    }

                }
            }
            return f;
        }

        //this is the function that puts back into the first frame the RGB values
        //from the Y Cb Cr
        public void convertYCbCr2RGB()
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    double thisY = Y[x, y];
                    double ThisCb = Cb[x, y];
                    double thisCr = Cr[x, y];

                    int r = (int)(1.164 * (thisY - 16) + 1.596 * (thisCr - 128));
                    int g = (int)(1.164 * (thisY - 16) - 0.813 * (thisCr - 128) - 0.392 * (ThisCb - 128));
                    int b = (int)(1.164 * (thisY - 16) + 2.017 * (ThisCb - 128));

                    r = Math.Max(0, Math.Min(255, r));
                    g = Math.Max(0, Math.Min(255, g));
                    b = Math.Max(0, Math.Min(255, b));

                    bmp.SetPixel(x, y, Color.FromArgb(r, g, b));
                }
            }
        }

        //this function supersamples the first frame's Cb and Cr
        public void supersample()
        {
            double[,] superCb = new double[width, height];
            double[,] superCr = new double[width, height];
            int sx = 0, sy = 0;
            for (int x = 0; x < width; x = x + 2)
            {
                for (int y = 0; y < height; y = y + 2)
                {
                    superCb[x, y] = Cb[sx, sy];
                    if (x < width - 1) superCb[x + 1, y] = Cb[sx, sy];
                    if (y < height - 1) superCb[x, y + 1] = Cb[sx, sy];
                    if (x < width - 1 && y < height - 1) superCb[x + 1, y + 1] = Cb[sx, sy];

                    superCr[x, y] = Cr[sx, sy];
                    if (x < width - 1) superCr[x + 1, y] = Cr[sx, sy];
                    if (y < height - 1) superCr[x, y + 1] = Cr[sx, sy];
                    if (x < width - 1 && y < height - 1) superCr[x + 1, y + 1] = Cr[sx, sy];
                    sy++;
                }
                sx++;
                sy = 0;
            }
            Cb = superCb;
            Cr = superCr;
        }


        //The inverse quantization function
        private double[,] IQuantize(double[] matrix, double[,] table)
        {
            int i = 0, j = 0, n = 8, index = 0;
            int d = -1; // -1 for top-right move, +1 for bottom-left move
            int start = 0, end = n * n - 1;
            double[,] quantizedBlock = new double[8, 8];

            do
            {
                quantizedBlock[i, j] = Math.Round((matrix[index] * table[i, j]));
                quantizedBlock[n - i - 1, n - j - 1] = Math.Round((matrix[64 - index - 1] * table[n - i - 1, n - j - 1]));
                index++;

                start++;
                end--;

                i += d;
                j -= d;

                if (i < 0)
                {
                    i++;
                    d = -d; // top reached, reverse
                }
                else if (j < 0)
                {
                    j++;
                    d = -d; // left reached, reverse
                }
            } while (start < end);

            return quantizedBlock;
        }

    }
}
