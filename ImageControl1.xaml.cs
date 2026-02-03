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
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace ImageViewer
{
    /// <summary>
    /// Interaction logic for ImageControl1.xaml
    /// </summary>
    public partial class ImageControl1 : UserControl
    {
        private VMS.TPS.Common.Model.API.Image _image;
        private StructureSet _structureSet;
        private PlanSetup _plan;
        private int _sliceNum;


        //public ImageControl1(VMS.TPS.Common.Model.API.Image image)
        //public ImageControl1(StructureSet structureSet)
        public ImageControl1(PlanSetup plan)
        {
            InitializeComponent();

            //set a variable for VMS image
            //_image = image;
            //_image = structureSet.Image;
            //_structureSet = structureSet;

            _image = plan.StructureSet.Image;
            _structureSet = plan.StructureSet;
            _plan = plan;

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
                    AddContours(pBackBuffer);
                    AddDose(pBackBuffer);
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

        private unsafe void AddDose(int* pBackBuffer)
        {
            // Check if plan has dose calculated
            if (_plan.Dose == null)
                return;
            _plan.DoseValuePresentation = DoseValuePresentation.Absolute;
            Dose dose = _plan.Dose;

            int xSize = _image.XSize;
            int ySize = _image.YSize;

            // Get dose voxels for current slice
            int[,] doseVoxels = new int[dose.XSize, dose.YSize];
            dose.GetVoxels(_sliceNum, doseVoxels);

            // Define max dose for color scaling (using prescription dose or max dose)
            double maxDose = _plan.TotalDose.Dose; // in Gy
            double maxDoseValue = maxDose; // Convert to cGy for scaling

            // Loop through dose grid
            for (int dy = 0; dy < dose.YSize; dy++)
            {
                for (int dx = 0; dx < dose.XSize; dx++)
                {
                    int doseValue = doseVoxels[dx, dy];

                    // Skip if no dose
                    if (doseValue == 0)
                        continue;

                    // Convert dose voxel to dose in cGy
                    double doseInCGy = dose.VoxelToDoseValue(doseValue).Dose;


                    // Skip doses below 10% of max
                    if (doseInCGy < maxDoseValue * 0.1)
                        continue;

                    // Calculate dose position in image coordinates
                    VVector dosePos = dose.Origin +
                        new VVector(dx * dose.XRes, dy * dose.YRes, 0);

                    // Convert to image pixel coordinates
                    int imgX = (int)((dosePos.x - _image.Origin.x) / _image.XRes);
                    int imgY = (int)((dosePos.y - _image.Origin.y) / _image.YRes);

                    // Check bounds
                    if (imgX < 0 || imgX >= xSize || imgY < 0 || imgY >= ySize)
                        continue;

                    // Normalize dose to 0-1 range
                    double normalizedDose = Math.Min(1.0, doseInCGy / maxDoseValue);

                    // Get color based on dose intensity (heat map)
                    System.Windows.Media.Color doseColor = GetDoseColor(normalizedDose);

                    // Get existing pixel
                    int existingPixel = pBackBuffer[imgY * xSize + imgX];
                    byte existingR = (byte)((existingPixel >> 16) & 0xFF);
                    byte existingG = (byte)((existingPixel >> 8) & 0xFF);
                    byte existingB = (byte)(existingPixel & 0xFF);

                    // Blend dose color with existing pixel (50% alpha)
                    double alpha = 0.5;
                    byte blendedR = (byte)((doseColor.R * alpha) + (existingR * (1 - alpha)));
                    byte blendedG = (byte)((doseColor.G * alpha) + (existingG * (1 - alpha)));
                    byte blendedB = (byte)((doseColor.B * alpha) + (existingB * (1 - alpha)));

                    // Write blended color
                    int blendedColor = (blendedR << 16) | (blendedG << 8) | blendedB;
                    pBackBuffer[imgY * xSize + imgX] = blendedColor;
                }
            }
        }

        private System.Windows.Media.Color GetDoseColor(double normalizedDose)
        {
            // Heat map color scale: Blue -> Cyan -> Green -> Yellow -> Red
            byte r, g, b;

            if (normalizedDose < 0.25)
            {
                // Blue to Cyan
                double t = normalizedDose / 0.25;
                r = 0;
                g = (byte)(t * 255);
                b = 255;
            }
            else if (normalizedDose < 0.5)
            {
                // Cyan to Green
                double t = (normalizedDose - 0.25) / 0.25;
                r = 0;
                g = 255;
                b = (byte)((1 - t) * 255);
            }
            else if (normalizedDose < 0.75)
            {
                // Green to Yellow
                double t = (normalizedDose - 0.5) / 0.25;
                r = (byte)(t * 255);
                g = 255;
                b = 0;
            }
            else
            {
                // Yellow to Red
                double t = (normalizedDose - 0.75) / 0.25;
                r = 255;
                g = (byte)((1 - t) * 255);
                b = 0;
            }

            return System.Windows.Media.Color.FromRgb(r, g, b);
        }



        private unsafe void AddContours(int* pBackBuffer)
        {
            // Define the DICOM types we want to display
            string[] displayTypes = { "PTV", "CTV", "ORGAN", "EXTERNAL" };

            int xSize = _image.XSize;
            int ySize = _image.YSize;

            // Loop through all structures in the structure set
            foreach (Structure structure in _structureSet.Structures)
            {
                // Check if the structure's DICOM type matches our filter
                if (!displayTypes.Contains(structure.DicomType))
                    continue;

                // Get contours for the current slice
                VVector[][] contours = structure.GetContoursOnImagePlane(_sliceNum);

                if (contours == null || contours.Length == 0)
                    continue;

                // Get the structure color
                System.Windows.Media.Color structureColor = structure.Color;
                int colorValue = (structureColor.R << 16) | (structureColor.G << 8) | structureColor.B;

                // Draw each contour segment
                foreach (VVector[] contour in contours)
                {
                    if (contour.Length < 2)
                        continue;

                    // Draw lines between consecutive points
                    for (int i = 0; i < contour.Length; i++)
                    {
                        VVector point1 = contour[i];
                        VVector point2 = contour[(i + 1) % contour.Length]; // Loop back to first point

                        // Convert DICOM coordinates to pixel coordinates
                        int x1 = (int)((point1.x - _image.Origin.x) / _image.XRes);
                        int y1 = (int)((point1.y - _image.Origin.y) / _image.YRes);
                        int x2 = (int)((point2.x - _image.Origin.x) / _image.XRes);
                        int y2 = (int)((point2.y - _image.Origin.y) / _image.YRes);

                        // Draw line using Bresenham's algorithm
                        DrawLine(pBackBuffer, x1, y1, x2, y2, colorValue, xSize, ySize);
                    }
                }
            }
        }

        private unsafe void DrawLine(int* pBackBuffer, int x0, int y0, int x1, int y1, int color, int width, int height)
        {
            // Bresenham's line algorithm
            int dx = Math.Abs(x1 - x0);
            int dy = Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                // Draw pixel if within bounds
                if (x0 >= 0 && x0 < width && y0 >= 0 && y0 < height)
                {
                    pBackBuffer[y0 * width + x0] = color;
                }

                if (x0 == x1 && y0 == y1)
                    break;

                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
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
