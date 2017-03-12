using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Luxand;
using MessageBox = System.Windows.MessageBox;
using GDIScreen = System.Windows.Forms.Screen;

namespace FaceRec
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;

            MouseDown += MainWindow_MouseDown;
            MouseDoubleClick += MainWindow_MouseDoubleClick;
        }

        private void MainWindow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Close();
        }

        private void MainWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Face.Source = null;

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }

            var window = WindowFromPoint(new System.Drawing.Point((int)Left + (int)(Width / 2), (int)Top + (int)(Height / 2)));

            var bit = PrintWindow(window);

            Findface(bit);
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool PrintWindow(IntPtr hwnd, IntPtr hDC, uint nFlags);

        [DllImport("gdi32.dll")]
        static extern IntPtr CreateRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;        // x position of upper-left corner
            public int Top;         // y position of upper-left corner
            public int Right;       // x position of lower-right corner
            public int Bottom;      // y position of lower-right corner
        }

        [DllImport("user32.dll")]
        static extern int GetWindowRgn(IntPtr hWnd, IntPtr hRgn);

        [DllImport("user32.dll")]
        static extern IntPtr WindowFromPoint(System.Drawing.Point p);

        public Bitmap PrintWindow(IntPtr hwnd)
        {
            RECT rc;
            GetWindowRect(hwnd, out rc);

            Bitmap bmp = new Bitmap(rc.Right - rc.Left, rc.Bottom - rc.Top, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            //Bitmap bmp = new Bitmap((int)Width, (int)Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Graphics gfxBmp = Graphics.FromImage(bmp);
            IntPtr hdcBitmap = gfxBmp.GetHdc();
            bool succeeded = PrintWindow(hwnd, hdcBitmap, 0);
            gfxBmp.ReleaseHdc(hdcBitmap);
            if (!succeeded)
            {
                gfxBmp.FillRectangle(new SolidBrush(Color.Gray), new Rectangle(System.Drawing.Point.Empty, bmp.Size));
            }
            IntPtr hRgn = CreateRectRgn(0, 0, 0, 0);
            GetWindowRgn(hwnd, hRgn);
            Region region = Region.FromHrgn(hRgn);
            if (!region.IsEmpty(gfxBmp))
            {
                gfxBmp.ExcludeClip(region);
                gfxBmp.Clear(System.Drawing.Color.Transparent);
            }

            gfxBmp.Dispose();

            //Crop image
            var screen = GDIScreen.FromHandle(new WindowInteropHelper(this).Handle);
            Bitmap bmpFullScreen = new Bitmap(screen.WorkingArea.Width, screen.WorkingArea.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Graphics g = Graphics.FromImage(bmpFullScreen);

            //Crop if image was bigger than screen
            var cropped = bmp.Clone(
                 new System.Drawing.Rectangle(0, 0, Math.Min(screen.WorkingArea.Height, rc.Right - rc.Left),
                     Math.Min(screen.WorkingArea.Width, rc.Bottom - rc.Top)), bmp.PixelFormat);

            g.DrawImage(cropped, new System.Drawing.Point(rc.Left, rc.Top));
            g.Dispose();

            //10 for border
            var result = bmpFullScreen.Clone(new System.Drawing.Rectangle((int)Left + 10, (int)Top + 10, (int)Width - 10, (int)Height - 10), bmpFullScreen.PixelFormat);

            return result;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (FSDK.FSDKE_OK != FSDK.ActivateLibrary(@"Free Activation key"))
            {
                MessageBox.Show("Please run the License Key Wizard (Start - Luxand - FaceSDK - License Key Wizard)", "Error activating FaceSDK", MessageBoxButton.OK);
                Close();
            }

            FSDK.InitializeLibrary();
        }

        private void Findface(Bitmap bitmap)
        {
            var img = 0;

            var image = bitmap;
            image.Save("test.png");
            FSDK.LoadImageFromFile(ref img, "test.png");

            Face.Source = new BitmapImage(new Uri("test.png", UriKind.Relative));

            FSDK.TPoint[] array;
            FSDK.DetectFacialFeatures(img, out array);
            if (array != null)
            {
                Face.Source = DrawPoints(image, array);
            }


            FSDK.TFacePosition[] faces;
            int faceCount = 0;
            FSDK.DetectMultipleFaces(img, ref faceCount, out faces, 10000);


            if (faces != null)
            {
                foreach (var f in faces)
                {

                    Face.Source = DrawCircle(image, f.xc, f.yc, f.w);
                }

            }
        }

        private BitmapImage DrawCircle(Bitmap image, int x, int y, int width)
        {
            using (var graphics = Graphics.FromImage(image))
            {
                System.Drawing.Pen blackPen = new System.Drawing.Pen(System.Drawing.Color.Blue, 3);

                graphics.DrawEllipse(blackPen, x - width, y - width, width * 2, width * 2);
            }
            return BitmapToImageSource(image);
        }

        private BitmapImage DrawPoints(Bitmap image, FSDK.TPoint[] points)
        {
            using (var graphics = Graphics.FromImage(image))
            {
                foreach (var point in points)
                {
                    System.Drawing.Pen blackPen = new System.Drawing.Pen(System.Drawing.Color.Blue, 3);

                    int x1 = point.x;
                    int y1 = point.y;
                    int x2 = point.x + 1;
                    int y2 = point.y + 1;
                    // Draw line to screen.
                    graphics.DrawLine(blackPen, x1, y1, x2, y2);
                }
            }
            return BitmapToImageSource(image);
        }


        BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapimage = new BitmapImage();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();

                return bitmapimage;
            }
        }
    }
}
