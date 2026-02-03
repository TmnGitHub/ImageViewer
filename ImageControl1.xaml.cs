using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ImageViewer
{
    /// <summary>
    /// Interaction logic for ImageControl1.xaml
    /// </summary>
    public partial class ImageControl1 : UserControl
    {
        private VMS.TPS.Common.Model.API.Image _image;
        private int _sliceNum;


        public ImageControl1(VMS.TPS.Common.Model.API.Image image)
        {
            InitializeComponent();

            //set a variable for VMS image
            _image = image;
            _sliceNum = (int)(_image.ZSize / 2);

            //code to show the initial image
            BuildImage();
        }

        private void BuildImage()
        {
            // Get image dimensions
            int xSize = _image.XSize;
            int ySize = _image.YSize;

            // Create array to hold voxel values for the current slice
            int[,] voxels = new int[xSize, ySize];

            // Get voxel data from ESAPI for the current slice
            _image.GetVoxels(_sliceNum, voxels);

            // Create WriteableBitmap (using Bgr32 format for grayscale medical images)
            WriteableBitmap bitmap = new WriteableBitmap(xSize, ySize, 96, 96, PixelFormats.Bgr32, null);

            // Lock the bitmap for writing
            bitmap.Lock();

            try
            {
                unsafe
                {
                    int* pBackBuffer = (int*)bitmap.BackBuffer;

                    // Convert voxel values to pixel values
                    for (int y = 0; y < ySize; y++)
                    {
                        for (int x = 0; x < xSize; x++)
                        {
                            // Normalize the voxel value to 0-255 range
                            // ESAPI voxel values are typically in HU units or raw values
                            int voxelValue = voxels[x, y];
                            byte pixelValue = (byte)Math.Max(0, Math.Min(255, (voxelValue + 1000) / 16));

                            // Create grayscale color (R=G=B)
                            int colorValue = (pixelValue << 16) | (pixelValue << 8) | pixelValue;

                            pBackBuffer[y * xSize + x] = colorValue;
                        }
                    }
                }

                // Mark the entire bitmap as dirty
                bitmap.AddDirtyRect(new Int32Rect(0, 0, xSize, ySize));
            }
            finally
            {
                bitmap.Unlock();
            }

            // Set the bitmap as the source for the Image control
            DisplayImage.Source = bitmap;


        }

        private void Prev_BTN_Click(object sender, RoutedEventArgs e)
        {
            _sliceNum--;
            if( _sliceNum < 0)
            {
                _sliceNum = _image.ZSize - 1; //loop to the top of the image
            }
            BuildImage();

        }

        private void Next_BTN_Click(object sender, RoutedEventArgs e)
        {
            _sliceNum++;
            if (_sliceNum > _image.ZSize)
            {
                _sliceNum = 0; //loop to the top of the image
            }
            BuildImage() ;
        }
    }
}
