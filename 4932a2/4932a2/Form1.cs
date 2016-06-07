using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace _4932a2
{
    public partial class Form1 : Form
    {
        compression JPEG;
        decomp decompImages;

        public Form1()
        {
            InitializeComponent();
            JPEG = new compression((Bitmap)Image.FromFile(@"nomad1.jpg"), (Bitmap)Image.FromFile(@"nomad2.jpg"));
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            numericUpDown1.Value = 10;
            pictureBox1.Image = JPEG.bmp;
            pictureBox2.Image = JPEG.bmp2;
            Resize(JPEG.width, JPEG.height);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                pictureBox1.Image = new Bitmap(ofd.FileName);
            }
            JPEG = new compression((Bitmap)pictureBox1.Image, JPEG.bmp2);
            Resize(JPEG.width, JPEG.height);
        }

        new private void Resize(int width, int height)
        {
            table.RowStyles.Clear();
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            Width = 2 * width;
            Height = height + 130;
            Update();
        }

        public void test_button_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                pictureBox2.Image = new Bitmap(ofd.FileName);
            }
            JPEG = new compression(JPEG.bmp, (Bitmap)pictureBox2.Image);
            Resize(JPEG.width, JPEG.height);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            decompImages = new decomp();
            decompImages.daDecomp();
            pictureBox1.Image = decompImages.bmp;
            pictureBox2.Image = decompImages.bmp2;
        }

        private void compress_button_Click(object sender, EventArgs e)
        {
            JPEG.fullCompress((int)numericUpDown1.Value);
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
        }
    }
}
