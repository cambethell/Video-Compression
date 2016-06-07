using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace _4932a2
{
    //the compression class. it defines a struct for a "motion vector pair" and has a few class members.
    class compression
    {
        public double[,] Y, Cb, Cr;
        public Bitmap bmp { get; set; }
        public Bitmap bmp2 { get; set; }
        public int width, height, heightInPixels;
        List<MVPair> mvList;

        public struct MVPair
        {
            public int x, y;

            public MVPair(int p1, int p2)
            {
                x = p1;
                y = p2;
            }
        }
        //the tables for quantization
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
        //this is how a compression object is initialized 
        public compression(Bitmap bmp, Bitmap bmp2)
        {
            width = bmp.Width;
            height = bmp.Height;
            Y = new double[bmp.Width, bmp.Height];
            Cb = new double[bmp.Width, bmp.Height];
            Cr = new double[bmp.Width, bmp.Height];
            this.bmp = bmp;
            this.bmp2 = bmp2;
            mvList = new List<MVPair>();
        }

        //this is the function that orchestrates the compression.
        public void fullCompress(int pRange)
        {
            //convert the rgb to ycbcr
            //subsample the cb and cr channels
            convertRGB2YCbCr();
            subsample();
            
            //variables that hold lists of the YCbCr channels after DCT, quantization, zigzag, and mRLE
            var yBlocks = separateThemBlocks(Y, width, height, luminance);
            var cbBlocks = separateThemBlocks(Cb, (int)Math.Ceiling(width / 2.0), (int)Math.Ceiling(height / 2.0), chrominance);
            var crBlocks = separateThemBlocks(Cr, (int)Math.Ceiling(width / 2.0), (int)Math.Ceiling(height / 2.0), chrominance);

            //get y cr cb of next frame
            var diffY = convertRGB2YCbCr(bmp2, 0);
            var diffCb = convertRGB2YCbCr(bmp2, 1);
            var diffCr = convertRGB2YCbCr(bmp2, 2);


            //SUBSAMPLE THE DIFF CB CR
            double[,] subCb = new double[(int)Math.Ceiling(width / 2.0), (int)Math.Ceiling((height / 2.0))];
            double[,] subCr = new double[(int)Math.Ceiling(width / 2.0), (int)Math.Ceiling((height / 2.0))];
            for (int u2 = 0, u = 0; u2 < width; u2 += 2, u++)
            {
                for (int v2 = 0, v = 0; v2 < height; v2 += 2, v++)
                {
                    subCb[u, v] = diffCb[u2, v2];
                    subCr[u, v] = diffCr[u2, v2];
                }
            }
            diffCb = subCb;
            diffCr = subCr;
            //get the difference channels and put their motion vectors in the class motion vector list.
            var diffYMotion = motionVectors(Y, diffY, pRange);
            var diffCbMotion = motionVectors(Cb, diffCb, pRange);
            var diffCrMotion = motionVectors(Cr, diffCr, pRange);
            //variables that hold lists of the YCbCr difference channels after DCT, quantization, zigzag, and mRLE
            var diffYBlocks = separateThemBlocks(diffYMotion, width, height, luminance);
            var diffCbBlocks = separateThemBlocks(diffCbMotion, (int)Math.Ceiling(width / 2.0), (int)Math.Ceiling(height / 2.0), chrominance);
            var diffCrBlocks = separateThemBlocks(diffCrMotion, (int)Math.Ceiling(width / 2.0), (int)Math.Ceiling(height / 2.0), chrominance);
            //start writing to the file
            FileStream stream = new FileStream("thefile.cam", FileMode.Create);
            //write the bitmap width and height
            stream.Write(BitConverter.GetBytes(width), 0, 4);
            stream.Write(BitConverter.GetBytes(height), 0, 4);
            //write each channel count
            stream.Write(BitConverter.GetBytes(yBlocks.Count), 0, 4);
            stream.Write(BitConverter.GetBytes(cbBlocks.Count), 0, 4);
            stream.Write(BitConverter.GetBytes(crBlocks.Count), 0, 4);
            //write each difference channel count
            stream.Write(BitConverter.GetBytes(diffYBlocks.Count), 0, 4);
            stream.Write(BitConverter.GetBytes(diffCbBlocks.Count), 0, 4);
            stream.Write(BitConverter.GetBytes(diffCrBlocks.Count), 0, 4);
            //for each list we wrote, write them to the filestream
            //y cb cr here
            foreach (double val in yBlocks)
            {
                stream.WriteByte((byte)val);
            }

            foreach (double val in cbBlocks)
            {
                stream.WriteByte((byte)val);
            }

            foreach (double val in crBlocks)
            {
                stream.WriteByte((byte)val);
            }
            //write the difference lists here for y cb cr
            foreach (double val in diffYBlocks)
            {
                stream.WriteByte((byte)val);
            }

            foreach (double val in diffCbBlocks)
            {
                stream.WriteByte((byte)val);
            }

            foreach (double val in diffCrBlocks)
            {
                stream.WriteByte((byte)val);
            }
            //write motion vector list last
            foreach (MVPair val in mvList)
            {
                stream.WriteByte((byte)val.x);
                stream.WriteByte((byte)val.y);
            }
            mvList.Clear();
            stream.Flush();
            stream.Close();
        }


        //this is the motion vector function. 
        //the best match for MAD is calculated here.
        //their motion vector is also added here.
        public double[,] motionVectors(double[,] refChannel, double[,] tarChannel, int pRange)
        {
            double[,] diffChannel = new double[refChannel.GetLength(0), refChannel.GetLength(1)];
            int width1 = (int)Math.Ceiling(diffChannel.GetLength(0) / 8.0);
            int height1 = (int)Math.Ceiling(diffChannel.GetLength(1) / 8.0);
            double[,] tarBlock;
            int refX = 0, refY = 0;

            for (int i = 0; i < width1; i++)
            {
                for (int j = 0; j < height1; j++)
                {
                    tarBlock = new double[8, 8];
                    //each search origin x, y
                    refX = (i * 8) + 4;
                    refY = (j * 8) + 4;

                    //loop 8left and 8 down from current position on target frame
                    //store all those values to do MAD later
                    for (int x = -4; x < 4; x++)
                    {
                        for (int y = -4; y < 4; y++)
                        {
                            int reachX = 0;
                            int reachY = 0;
                            if (refX + x < refChannel.GetLength(0) && refX + x > 0) reachX = refX + x;
                            if (refY + y < refChannel.GetLength(1) && refY + y > 0) reachY = refY + y;
                            tarBlock[x + 4, y + 4] = tarChannel[reachX, reachY];
                        }
                    }

                    //and now do the search about the same position on ref frame
                    //maybe try p = 12
                    //full search algo for 2p+1 by 2p+1
                    //save that x, y difference
                    double[,] refBlock;
                    double[,] diffBlock;
                    int MAD = 1000000000;
                    int temp = 0;
                    int mvX = 0, mvY = 0; ;
                    int p = pRange;
                    for (int x = -p; x < p + 1 && refX + x < width; x++)
                    {
                        if (refX + x < 0) x += p - refX;
                        for (int y = -p; y < p + 1 && refY + y < height; y++)
                        {
                            if (refY + y < 0) y += p - refY;

                            refBlock = new double[8, 8];
                            diffBlock = new double[8, 8];

                            for (int u = -4; u < 4; u++)
                            {
                                for (int v = -4; v < 4; v++)
                                {
                                    int reachX = 0;
                                    int reachY = 0;
                                    if (refX + x + u < refChannel.GetLength(0) && refX + x + u > 0) reachX = refX + x + u;
                                    if (refY + y + v < refChannel.GetLength(1) && refY + y + v > 0) reachY = refY + y + v;
                                    refBlock[u + 4, v + 4] = refChannel[reachX, reachY];
                                }
                            }

                            //Mean Average Difference
                            for (int avgX = 0; avgX < 8; avgX++)
                            {
                                for (int avgY = 0; avgY < 8; avgY++)
                                {
                                    temp += (int)Math.Abs(tarBlock[avgX, avgY] - refBlock[avgX, avgY]);
                                    diffBlock[avgX, avgY] = (refBlock[avgX, avgY] - tarBlock[avgX, avgY]);
                                }
                            }
                            temp = temp / 64;
                            //if its the new lowest MAD, save it and the MV
                            if (temp < MAD)
                            {
                                MAD = temp;
                                mvX = x;
                                mvY = y;
                            }
                        }
                    }

                    refBlock = new double[8, 8];
                    diffBlock = new double[8, 8];

                    //check center LAST
                    for (int u = -4; u < 4; u++)
                    {
                        for (int v = -4; v < 4; v++)
                        {
                            int reachX = 0;
                            int reachY = 0;
                            if (refX + u < refChannel.GetLength(0) && refX + u > 0) reachX = refX + u;
                            if (refY + v < refChannel.GetLength(1) && refY + v > 0) reachY = refY + v;
                            refBlock[u + 4, v + 4] = refChannel[reachX, reachY];
                        }
                    }
                    temp = 0;
                    for (int avgX = 0; avgX < 8; avgX++)
                    {
                        for (int avgY = 0; avgY < 8; avgY++)
                        {
                            temp += (int)Math.Abs(tarBlock[avgX, avgY] - refBlock[avgX, avgY]);
                            diffBlock[avgX, avgY] = (refBlock[avgX, avgY] - tarBlock[avgX, avgY]);
                        }
                    }
                    temp = temp / 64;
                    //if its the new lowest MAD, save it and the MV
                    if (temp <= MAD)
                    {
                        MAD = temp;
                        mvX = 0;
                        mvY = 0;
                    }

                    mvList.Add(new MVPair(mvX, mvY));
                    //add the blocks to the channel
                    for (int x = -4; x < 4; x++)
                    {
                        for (int y = -4; y < 4; y++)
                        {
                            int reachX = 0;
                            int reachY = 0;
                            if (refX + x < refChannel.GetLength(0) && refX + x > 0) reachX = refX + x;
                            if (refY + y < refChannel.GetLength(1) && refY + y > 0) reachY = refY + y;
                            diffChannel[reachX, reachY] = diffBlock[x + 4, y + 4];
                        }
                    }
                }
            }
            return diffChannel;
        }

        //OVERLOAD: for finding the y cb cr channels of the 2nd frame
        public double[,] convertRGB2YCbCr(Bitmap aBMP, int TYPE)
        {
            width = aBMP.Width;
            height = aBMP.Height;
            double[,] aChannel = new double[width, height];
            unsafe
            {
                BitmapData bitmapData = aBMP.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, aBMP.PixelFormat);
                height = bitmapData.Height;
                byte* ptrFirstPixel = (byte*)bitmapData.Scan0;

                //Convert to YCbCr
                for (int y = 0; y < height; y++)
                {
                    byte* currentLine = ptrFirstPixel + (y * bitmapData.Stride);
                    for (int x = 0; x < width; x++)
                    {
                        int xPor3 = x * 3;
                        float blue = currentLine[xPor3++];
                        float green = currentLine[xPor3++];
                        float red = currentLine[xPor3];

                        double yTemp = ((0.257 * red) + (0.504 * green) + (0.098 * blue) + 16);
                        double cbTemp = (128 + (-0.148 * red) + (-0.291 * green) + (0.439 * blue));
                        double crTemp = (128 + (0.439 * red) + (-0.368 * green) + (-0.071 * blue));

                        if (TYPE == 0) aChannel[x, y] = Math.Max(0, Math.Min(255, yTemp));
                        if (TYPE == 1) aChannel[x, y] = Math.Max(0, Math.Min(255, cbTemp));
                        if (TYPE == 2) aChannel[x, y] = Math.Max(0, Math.Min(255, crTemp));

                    }
                }
                aBMP.UnlockBits(bitmapData);
            }
            return aChannel;
        }

        //function for finding the y cb cr of the first frame
        public void convertRGB2YCbCr()
        {
            width = bmp.Width;
            height = bmp.Height;
            unsafe
            {
                BitmapData bitmapData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, bmp.PixelFormat);
                heightInPixels = bitmapData.Height;
                byte* ptrFirstPixel = (byte*)bitmapData.Scan0;

                //Convert to YCbCr
                for (int y = 0; y < heightInPixels; y++)
                {
                    byte* currentLine = ptrFirstPixel + (y * bitmapData.Stride);
                    for (int x = 0; x < width; x++)
                    {
                        int xPor3 = x * 3;
                        float blue = currentLine[xPor3++];
                        float green = currentLine[xPor3++];
                        float red = currentLine[xPor3];

                        double yTemp = (int)((0.257 * red) + (0.504 * green) + (0.098 * blue) + 16);
                        double cbTemp = (int)(128 + (-0.148 * red) + (-0.291 * green) + (0.439 * blue));
                        double crTemp = (int)(128 + (0.439 * red) + (-0.368 * green) + (-0.071 * blue));

                        Y[x, y] = Math.Max(0, Math.Min(255, yTemp));
                        Cb[x, y] = Math.Max(0, Math.Min(255, cbTemp));
                        Cr[x, y] = Math.Max(0, Math.Min(255, crTemp));

                    }
                }
                bmp.UnlockBits(bitmapData);
            }
        }
        
        //subsample the y cb cr of the first frame
        public void subsample()
        {
            double[,] subCb = new double[(int)Math.Ceiling(width / 2.0), (int)Math.Ceiling((height / 2.0))];
            double[,] subCr = new double[(int)Math.Ceiling(width / 2.0), (int)Math.Ceiling((height / 2.0))];
            for (int u2 = 0, u = 0; u2 < width; u2 += 2, u++)
            {
                for (int v2 = 0, v = 0; v2 < height; v2 += 2, v++)
                {
                    subCb[u, v] = Cb[u2, v2];
                    subCr[u, v] = Cr[u2, v2];
                }
            }
            Cb = subCb;
            Cr = subCr;
        }

        //this function takes a channel, its dimensions, and the relevant quantization table
        //and performs DCT, quantization, and mRLE on each block.
        //each block is put into the list for that channel, and the entire channel is returned
        private List<double> separateThemBlocks(double[,] channel, int width1, int height1, double[,] quanTable)
        {
            List<double> listToBeEncoded = new List<double>();
            double[,] someBlock = new double[8, 8];
            double temp = 0;
            for(int x = 0; x < width1; x += 8)
            {
                for(int y = 0; y < height1; y += 8)
                {
                    for(int blkX = x; blkX < x + 8; blkX++)
                    {
                        for(int blkY = y; blkY < y + 8; blkY++)
                        {
                            if (blkX < width1 && blkY < height1) temp = channel[blkX, blkY];
                            someBlock[blkX - x, blkY - y] = temp;
                        }
                    }
                    //center around 0
                    for(int i = 0; i < 8; i++)
                    {
                        for(int j = 0; j < 8; j++)
                        {
                            someBlock[i, j] -= 128;
                        }
                    }
                    //DCT, QUANTIZE, MRL ENCODE!
                    someBlock = DCT(someBlock);
                    var theQuantization = Quantization(someBlock, quanTable);
                    var encoArray = modifiedRunLengthEncode(theQuantization);
                    for(int i = 0; i < encoArray.Length; i++)
                    {
                        listToBeEncoded.Add(encoArray[i]);
                    }
                }
            }
            return listToBeEncoded;
        }

        //the DCT function
        public double[,] DCT(double[,] f)
        {
            double[,] F = new double[8, 8];
            double Cu, Cv;
            for (int u = 0; u < 8; u++)
            {
                if (u == 0) Cu = Math.Sqrt(2) / 2;
                else Cu = 1;

                for (int v = 0; v < 8; v++)
                {
                    if (v == 0) Cv = Math.Sqrt(2) / 2;
                    else Cv = 1;

                    double sumtotal = 0;

                    for (int i = 0; i < 8; i++)
                    {
                        for (int j = 0; j < 8; j++)
                        {
                            var v1 = Math.Cos(((2 * i) + 1) * u * Math.PI / 16);
                            var v2 = Math.Cos(((2 * j) + 1) * v * Math.PI / 16);
                            sumtotal += v1 * v2 * f[i, j];
                        }
                    }
                    F[u, v] = (int)(sumtotal * Cu * Cv / 4);
                }
            }
            return F;
        }

        //the quantization function with zig zag
        public double[] Quantization(double[,] matrix, double[,] table)
        {

            int i = 0, j = 0, n = matrix.GetLength(0), d = -1, start = 0, end = n * n - 1, ind = 0;
            double[] result = new double[matrix.Length];
            do
            {
                result[ind] = Math.Round((matrix[i, j] / table[i, j]));
                result[matrix.Length - ind - 1] = Math.Round((matrix[n - i - 1, n - j - 1] / table[n - i - 1, n - j - 1]));

                start++;
                end--;
                ind++;

                i += d;
                j -= d;

                if (i < 0)
                {
                    i++;
                    d = -d;
                }
                else if (j < 0)
                {
                    j++;
                    d = -d;
                }
            } while (start < end);

            return result;
        }
        
        //the MRLE function! 127 is the symbol for runs.
        private double[] modifiedRunLengthEncode(double[] matrice)
        {
            int n = matrice.Length, end = n * n - 1, sym = 127;
            double[] result;
            List<double> list = new List<double>();

            //encoding starts meow
            int c = 1;
            double current = matrice[0];
            for (int k = 1; k < matrice.Length; k++)
            {
                if (current == matrice[k])
                {
                    c++;
                }
                else
                {
                    if (c > 1 || current == sym)
                    {
                        list.Add(sym);
                        list.Add(c);
                        list.Add(current);
                    }
                    else
                    {
                        list.Add(current);
                    }
                    c = 1;
                    current = matrice[k];
                }
            }

            //repeating logic again
            if (c > 1 || current == sym)
            {
                list.Add(sym);
                list.Add(c);
                list.Add(current);
            }
            else
            {
                list.Add(current);
            }

            result = new double[list.Count];
            for (int z = 0; z < list.Count; z++)
            {
                result[z] = list[z];
            }
            return result;
        }
    }
}
