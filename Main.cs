using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Linq;
using System.Drawing;
using Emgu.CV.Structure;
using Emgu.CV;
using Emgu.CV.Util;
using Emgu.CV.CvEnum;
using System.Data;
using STI_CoolingConveyorUpdated;
using Emgu.CV.Cuda;

namespace STI_Tool
{
    public partial class STI : Form
    {
        int blobCounter = 1;

        bool diameter90Deg = true;

        bool linesFilter = false;

        string configPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\STI-Tool\\";
        string csvFilePath = "data.csv";

        int tortillaPolarity = 0;

        double initMaxDiameter;
        double initMinDiameter;

        int nUnitsMm = 1;
        int nUnitsInch = 3;

        double euFactor = 1;

        int validFramesLimit = 7;

        float alpha = 0.8f;
        double controlDiameter = 0;
        double controlDiameterOld = 0;

        int minBlobObjects = 6;
        string units = "inch";

        Queue<int> validFrames = new Queue<int>();
        Queue<int> holesQueue = new Queue<int>();
        Queue<double> cvQueue = new Queue<double>();

        int FH = 0;
        int FFH = 0;
        float align = 0;

        int imageWidth = 640;
        int imageHeight = 480;

        int deltaRoi = 6;
        string cameraConfig = "Thermo";

        bool drawRoi = true;

        // Lista para los strings de los tamaños de la tortilla
        List<string> sizes = new List<string>();

        DataTable dataTable = new DataTable();

        double maxCompactnessHole = 18;
        double maxCompactness = 16;

        int maxIteration = 10000;

        int minArea;
        int maxArea;

        double minDiameter = 100;
        double maxDiameter = 200;

        List<Blob> Blobs = new List<Blob>();

        int threshold = 127;

        string imagesPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\STI-Tool-Images\\";

        public RECT UserROI = new RECT();

        long[] Histogram = new long[256];

        Mat originalImageCV = new Mat();

        bool autoThreshold = true;
        bool imageCorrection = true;

        string settingsPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\STI-Tool";
        Settings settings = Settings.Load();

        string folderPath = "";
        List<string> imagesPaths = new List<string>();
        string actualImagePath = "";

        public STI()
        {
            InitializeComponent();

            LoadSettings();

            InitDataTable();

            InitCSV();

            sizes.Add("Size");
            sizes.Add("OK");
            sizes.Add("BIG");
            sizes.Add("SMALL");
            sizes.Add("OVAL");
            sizes.Add("Oversize");
            sizes.Add("SHAPE");

            if (units == "mm")
            {
                initMaxDiameter = 165.1 / euFactor; //130mm
                initMinDiameter = 139.7 / euFactor; //110mm
            }
            else
            {
                initMaxDiameter = 5 / euFactor; //5inch
                initMinDiameter = 3 / euFactor; //3inch
            }

            maxDiameter = initMaxDiameter;
            minDiameter = initMinDiameter;


            controlDiameter = ((maxDiameter + minDiameter) / 2);
            controlDiameterOld = controlDiameter;

            pbROI.Visible = false;
            pbROI.SendToBack();

            InitControls();
        }

        private void InitCSV()
        {
            // Verifica si el archivo ya existe
            if (!File.Exists(configPath + csvFilePath))
            {
                // Crea un nuevo archivo y escribe los encabezados
                using (StreamWriter sw = new StreamWriter(configPath + csvFilePath, false))
                {
                    sw.WriteLine("ID,Number,Class,Compacity,Ovality,Max Diameter,Min Diameter 90°,Real Min Diameter,Avg Diameter"); // Escribe los encabezados
                }
            }
        }

        private void AddLineToCsv(string ID, string Class,string compactness,string maxDiameter, string minDiameter, string avgDiameter, string ovality)
        {
            // Agrega una nueva línea al archivo CSV
            using (StreamWriter sw = new StreamWriter(configPath + csvFilePath, true))
            {
                sw.WriteLine($"{blobCounter},{ID},,{compactness},{ovality},{maxDiameter},{minDiameter},{avgDiameter}");
            }

            blobCounter++;
        }

        private void InitDataTable()
        {
            dataTable = new DataTable();
            dataTable.Columns.Add("Quad");
            dataTable.Columns.Add("Class");
            dataTable.Columns.Add("SEQ Diameter");
            dataTable.Columns.Add("Average Diameter");
            dataTable.Columns.Add("Maximum Diameter");
            dataTable.Columns.Add("Minimum Diameter");
            dataTable.Columns.Add("Shape Indicator");
            dataTable.Columns.Add("Ovality");
            dataTable.Columns.Add("Area (px)");
        }

        private void LoadSettings()
        {
            folderPath = settings.FolderPath;
            imagesPaths = settings.ImagesPaths;
            actualImagePath = settings.ActualImagePath;

            UserROI = settings.ROI;

            // Units
            units = settings.Units;

            // Factor
            euFactor = settings.EUFactor;

            // Flags parameters
            FH = settings.FH;
            FFH = settings.FFH;
            align = settings.align;

            // Advanced parameters
            minBlobObjects = settings.minBlobObjects;
            alpha = settings.alpha;

            // Compacity
            maxCompactness = settings.maxCompacity;
            maxCompactnessHole = settings.maxCompacityHole;

            //Valid Frames Limit
            validFramesLimit = settings.validFramesLimit;
        }

        private void InitControls()
        {
            // Texts
            txtFolderPath.Text = folderPath;
            
            txtThreshold.Text = threshold.ToString();

            int roiWidth = UserROI.Right - UserROI.Left;
            int roiHeight = UserROI.Bottom - UserROI.Top;

            txtRoiWidth.Text = roiWidth.ToString();
            txtRoiHeight.Text = roiHeight.ToString();

            txtMinDiameter.Text = (minDiameter*euFactor).ToString();
            txtMaxDiameter.Text = (maxDiameter*euFactor).ToString();

            txtAlpha.Text = alpha.ToString();
            txtMinBlobObjects.Text = minBlobObjects.ToString();
            txtFH.Text = FH.ToString();
            txtFFH.Text = FFH.ToString();
            txtDiameterVariation.Text = align.ToString();
            txtEuFactor.Text = euFactor.ToString();
            txtMaxCompacity.Text = maxCompactness.ToString();
            txtMaxCompacityHole.Text = maxCompactnessHole.ToString();

            // ComboBox
            foreach (string imagePath in imagesPaths)
            {
                string imageName = imagePath.Split('\\')[imagePath.Split('\\').Length - 1];
                cmbActualImage.Items.Add(imageName);
            }
            cmbActualImage.SelectedItem = actualImagePath.Split('\\')[actualImagePath.Split('\\').Length - 1];

            // PictureBox
            if (actualImagePath != "")
            {
                pbMain.ImageLocation = actualImagePath;
                pbMain.LoadAsync();
            }

            // Buttons
            if (imageCorrection)
            {
                btnImageCorrection.BackColor = Color.LightGreen;
            }

            if (drawRoi)
            {
                btnDrawRoi.BackColor = Color.LightGreen;
            }

            if (autoThreshold)
            {
                btnAutoThreshold.BackColor = Color.LightGreen;
                btnManualThreshold.BackColor = Color.Silver;
            }
            else
            {
                btnAutoThreshold.BackColor = Color.Silver;
                btnManualThreshold.BackColor = Color.LightGreen;
            }

            if (linesFilter)
            {
                btnLinesFilter.BackColor = Color.LightGreen;
            }
            else
            {
                btnLinesFilter.BackColor = Color.Silver;
            }

            if (diameter90Deg)
            {
                btnDiameters90Deg.BackColor = Color.LightGreen;
            }
            else
            {
                btnDiameters90Deg.BackColor = Color.Silver;
            }

            btnProcess.Enabled = false;
            btnProcess.BackColor = Color.DarkGray;

            // Radio Buttons
            if (cameraConfig == "Thermo")
            {
                rbtThermoCamera.Checked = true;
            }

            if (cameraConfig == "Mono")
            {
                rbtMonoCamera.Checked = true;
            }

            // POlarity
            if (tortillaPolarity == 0)
            {
                cmbPolarity.SelectedItem = "White";
            }
            else
            {
                cmbPolarity.SelectedItem = "Black";

            }
        }

        private void STI_Load(object sender, EventArgs e)
        {
            if (!Directory.Exists(settingsPath))
            {
                Directory.CreateDirectory(settingsPath);
            }
            
            if (!Directory.Exists(imagesPath))
            {
                Directory.CreateDirectory(imagesPath);
            }
        }

        private void btnOpenFolder_Click(object sender, EventArgs e)
        {
            var result = folderBrowserDialog1.ShowDialog();
            if (result == DialogResult.OK)
            {
                folderPath = folderBrowserDialog1.SelectedPath;
                var a = folderPath.Split('\\');
                string path = "";
                for (int i = 0; i < folderPath.Split('\\').Length; i++)
                {
                    path.Concat(folderPath.Split('\\')[i] + "\\n\\t");
                }
                txtFolderPath.Text = folderPath;

                settings.FolderPath = folderPath;

                ReadImages();

            }
        }

        private void ReadImages()
        {
            imagesPaths.Clear();

            string[] imageFiles = Directory.GetFiles(folderPath, "*.jpg")
                                .Concat(Directory.GetFiles(folderPath, "*.bmp"))
                                .ToArray();

            if (imageFiles.Length > 0)
            {
                cmbActualImage.Items.Clear();

                foreach (string imageFile in imageFiles.OrderBy(file => file))
                {
                    imagesPaths.Add(imageFile);
                    cmbActualImage.Items.Add(imageFile.Split('\\')[imageFile.Split('\\').Length - 1]);
                }

                settings.ImagesPaths = imagesPaths;

                actualImagePath = imagesPaths[0];
                cmbActualImage.SelectedItem = actualImagePath.Split('\\')[actualImagePath.Split('\\').Length - 1];

                settings.ActualImagePath = actualImagePath;

                pbMain.ImageLocation = actualImagePath;
                pbMain.LoadAsync();

            }
        }

        private void btnNextImage_Click(object sender, EventArgs e)
        {
            int index = imagesPaths.IndexOf(actualImagePath);

            if (index != imagesPaths.Count-1)
            {
                actualImagePath = imagesPaths[index + 1];
                cmbActualImage.SelectedItem = actualImagePath.Split('\\')[actualImagePath.Split('\\').Length - 1];

                settings.ActualImagePath= actualImagePath;
                settings.Save();

                pbROI.Visible = false;

                pbMain.ImageLocation = actualImagePath;
                pbMain.LoadAsync();
            }
        }

        private void btnBackImage_Click(object sender, EventArgs e)
        {
            int index = imagesPaths.IndexOf(actualImagePath);

            if (index != 0)
            {
                actualImagePath = imagesPaths[index - 1];
                cmbActualImage.SelectedItem = actualImagePath.Split('\\')[actualImagePath.Split('\\').Length - 1];

                settings.ActualImagePath = actualImagePath;
                settings.Save();

                pbROI.Visible = false;

                pbMain.ImageLocation = actualImagePath;
                pbMain.LoadAsync();
            }
        }

        private void cmbActualImage_SelectedIndexChanged(object sender, EventArgs e)
        {
            string selection = cmbActualImage.SelectedItem.ToString();

            actualImagePath = actualImagePath.Replace(actualImagePath.Split('\\')[actualImagePath.Split('\\').Length - 1], selection);

            settings.ActualImagePath = actualImagePath;
            settings.Save();

            pbROI.Visible = false;

            pbMain.ImageLocation = actualImagePath;
            pbMain.Load();

            txtImageSize.Text = pbMain.Image.Size.ToString();
            if (pbMain.Image.Width == 1280)
            {
                ChangeROI(1280);
                rbtMonoCamera.Checked = true;
            }
            else if (pbMain.Image.Width == 640)
            {
                ChangeROI(640);
                rbtThermoCamera.Checked = true;
            }

            imageWidth = pbMain.Image.Width;
            imageHeight = pbMain.Image.Height;
        }

        private void ResizeTextBoxToFitText(System.Windows.Forms.TextBox textBox)
        {
            using (Graphics g = textBox.CreateGraphics())
            {
                SizeF size = g.MeasureString(textBox.Text, textBox.Font);
                textBox.Width = (int)size.Width + 10;  // Añadir un pequeño margen
                textBox.Height = (int)size.Height + 10; // Añadir un pequeño margen
            }

            //tabControl2.Width = textBox.Width + 100;
            //tabPage1.Width = textBox.Width + 100;
        }

        private void txtFolderPath_TextChanged(object sender, EventArgs e)
        {
            ResizeTextBoxToFitText(txtFolderPath);
        }

        private void btnPreProcess_Click(object sender, EventArgs e)
        {
            pbROI.Visible = false;


            using (Bitmap originalImage = new Bitmap(actualImagePath))
            {
                if (!(originalImage.Size == new Size(640, 480)) && !(originalImage.Size == new Size(1280, 960)))
                {
                    MessageBox.Show("Use a image with the size (640,480) or (1280,960)");
                    return;
                }

                if (originalImage.PixelFormat != System.Drawing.Imaging.PixelFormat.Format8bppIndexed)
                {
                    MessageBox.Show("Use a image with the correct color format");
                    return;
                    //Cargar la imagen original
                    //Mat originalImage2 = CvInvoke.Imread(actualImagePath, ImreadModes.Color);

                    //// Convertir la imagen a escala de grises
                    //Mat grayImage = new Mat();
                    //CvInvoke.CvtColor(originalImage2, grayImage, ColorConversion.Bgr2Gray);

                    //// Guardar la imagen en 8 bpp
                    //CvInvoke.Imwrite("0_1.jpg", grayImage);
                    //return;
                }


                // Convertir el objeto Bitmap a una matriz de Emgu CV (Image<Bgr, byte>)
                Image<Bgr, byte> tempImage = originalImage.ToImage<Bgr, byte>();
                ImageHistogram(originalImage);

                if (imageCorrection) 
                {
                    originalImageCV = ImageCorrection(tempImage);
                }
                else
                {
                    originalImageCV = tempImage.Mat;
                }

                //CvInvoke.MedianBlur(originalImageCV, originalImageCV, 5);

            }

            originalImageCV.Save(imagesPath + "updatedROI.bmp");

            if (drawRoi)
            {
                Mat temp = DrawROI(originalImageCV.Clone());

                temp.Save(imagesPath + "roiDraw.bmp");

                temp.Dispose();
            }
            else{
                originalImageCV.Save(imagesPath + "roiDraw.bmp");
            }

            pbMain.ImageLocation = imagesPath + "roiDraw.bmp";
            pbMain.LoadAsync();

            btnProcess.Enabled = true;
            btnProcess.BackColor = Color.Silver;
        }

        private Mat DrawROI(Mat image)
        {
            //Rectangle rect = new Rectangle(UserROI.Left, UserROI.Top, UserROI.Right - UserROI.Left, UserROI.Bottom - UserROI.Top);
            // Coordenadas y tamaño del rectángulo
            int x = UserROI.Left;
            int y = UserROI.Top;
            int ancho = UserROI.Right - UserROI.Left;
            int alto = UserROI.Bottom - UserROI.Top;

            // Color del rectángulo (en formato BGR)
            MCvScalar color = new MCvScalar(0, 255, 0);

            // Grosor del borde del rectángulo
            int grosor = 2;

            DrawHelpLines(image, color, grosor, ancho, alto);

            // Dibujar el rectángulo en la imagen
            CvInvoke.Rectangle(image, new Rectangle(x, y, ancho, alto), color, grosor);

            return image;
        }

        private void DrawHelpLines(Mat image, MCvScalar color, int grosor, int ancho, int alto)
        {
            CvInvoke.Line(image, new Point(UserROI.Left, UserROI.Top + ((int)(alto / 2))), new Point(UserROI.Right, UserROI.Top + ((int)(alto / 2))), color, grosor);
            CvInvoke.Line(image, new Point(UserROI.Left + ((int)(ancho / 2)), UserROI.Top), new Point(UserROI.Left + ((int)(ancho / 2)), UserROI.Bottom), color, grosor);
        }

        void ImageHistogram(Bitmap originalImage)
        {
            int x, y;
            int BytesPerLine;
            int PixelValue;

            // Obtener BitsPerPixel y PixelPerLine
            int bitsPerPixel = System.Drawing.Image.GetPixelFormatSize(originalImage.PixelFormat);
            int pixelPerLine = originalImage.Width;

            // Initialize Histogram array
            for (int i = 0; i < 256; i++)
            {
                Histogram[i] = 0;
            }

            // Calculate the count of bytes per line using the color format and the
            // pixels per line of the image buffer.
            BytesPerLine = bitsPerPixel / 8 * pixelPerLine - 1;

            // For y = 0 To ImgBuffer.Lines - 1
            // For x = 0 To BytesPerLine
            for (y = UserROI.Top; y <= UserROI.Bottom; y++)
            {
                for (x = UserROI.Left; x <= UserROI.Right; x++)
                {
                    // Assuming 8 bits per pixel (grayscale)
                    Color pixelColor = originalImage.GetPixel(x, y);

                    // Get the grayscale value directly
                    PixelValue = pixelColor.R;

                    Histogram[PixelValue] = Histogram[PixelValue] + 1;
                }
            }
            originalImage.Dispose();
        }

        public Mat ImageCorrection(Image<Bgr, byte> image)
        {
            // Declarar el vector de coeficientes de distorsión manualmente
            Matrix<double> distCoeffs = new Matrix<double>(1, 5); // 5 coeficientes de distorsión

            double k1 = 0, k2 = 0, p1 = 0, p2 = 0, k3 = 0, fx = 0, fy = 0, cx = 0, cy = 0;

            switch (cameraConfig)
            {
                case "Thermo":
                    k1 = -1.158e-6;//-21.4641724 - 6;
                    k2 = 1.56e-12;//1391.66319 - 700;
                    p1 = 0;
                    p2 = 0;
                    k3 = 0;

                    fx = 1;//4728.60;
                    fy = 1;// 4623.52;
                    cx = 320;
                    cy = 240;
                    break;
                case "Mono":
                    k1 = -1.8568e-7;//-21.4641724 - 6;
                    k2 = -3.4286e-13 + 4.5e-13;//1391.66319 - 700;
                    p1 = 0;
                    p2 = 0;
                    k3 = 0;

                    fx = 1;//4728.60;
                    fy = 1;// 4623.52;
                    cx = 640;
                    cy = 480;
                    break;
            }
            

            // Asignar los valores de los coeficientes de distorsión
            distCoeffs[0, 0] = k1; // k1
            distCoeffs[0, 1] = k2; // k2
            distCoeffs[0, 2] = p1; // p1
            distCoeffs[0, 3] = p2; // p2
            distCoeffs[0, 4] = k3; // k3

            Matrix<double> cameraMatrix = new Matrix<double>(3, 3);

            cameraMatrix[0, 0] = fx;
            cameraMatrix[0, 2] = cx;
            cameraMatrix[1, 1] = fy;
            cameraMatrix[1, 2] = cy;
            cameraMatrix[2, 2] = 1;

            // Corregir la distorsión en la imagen
            Mat undistortedImage = new Mat();
            CvInvoke.Undistort(image, undistortedImage, cameraMatrix, distCoeffs);
            image.Dispose();

            return undistortedImage;
        }

        public class Blob
        {
            // Propiedades de la estructura Blob
            public double Area { get; set; }
            //public List<Point> AreaPoints { get; set; }
            public double Perimetro { get; set; }
            public VectorOfPoint PerimetroPoints { get; set; }
            public double DiametroIA { get; set; }
            public double Diametro { get; set; }
            public Point Centro { get; set; }
            public double DMayor { get; set; }
            public double DMenor { get; set; }
            public double Sector { get; set; }
            public double Compacidad { get; set; }
            public double Ovalidad { get; set; }
            public ushort Size { get; set; }
            public bool Hole { get; set; }

            // Constructor de la clase Blob
            public Blob(double area, double perimetro, VectorOfPoint perimetroPoints, double diametro, double diametroIA, Point centro, double dMayor, double dMenor, double sector, double compacidad, ushort size, double ovalidad, bool hole)
            {
                Area = area;
                Perimetro = perimetro;
                PerimetroPoints = perimetroPoints;
                Diametro = diametro;
                DiametroIA = diametroIA;
                Centro = centro;
                DMayor = dMayor;
                DMenor = dMenor;
                Sector = sector;
                Compacidad = compacidad;
                Size = size;
                Ovalidad = ovalidad;
                Hole = hole;
            }
        }

        public class RECT
        {
            public int Left { get; set; }
            public int Top { get; set; }
            public int Right { get; set; }
            public int Bottom { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }

            public RECT()
            {
                this.Left = 140;
                this.Right = 500;
                this.Top = 80;
                this.Bottom = 400;
                this.Width = this.Right - this.Left;
                this.Height = this.Bottom - this.Top;
            }
            public int GetWidth()
            {
                this.Width = this.Right - this.Left;
                return this.Width;
            }

            public int GetHeight()
            {
                this.Height = this.Bottom - this.Top;
                return this.Height;
            }
        }

        private void btnImageCorrection_Click(object sender, EventArgs e)
        {
            imageCorrection = !imageCorrection;

            if (imageCorrection)
            {
                btnImageCorrection.BackColor = Color.LightGreen;
            }
            else
            {
                btnImageCorrection.BackColor= Color.Silver;
            }
        }

        private void btnProcess_Click(object sender, EventArgs e)
        {
            Mat binarizedImage = new Mat();

            // Se binariza la imagen
            try
            {
                //binarizedImage = binarizeImage(originalImage, 0);
                binarizedImage = BinarizeImage(originalImageCV);
                originalImageCV.Dispose();
            }
            catch
            {
                Console.WriteLine("Binarization problem");
                return;
            }

            Mat roiImage = new Mat();

            if (linesFilter)
            {
                int div = 120;

                Mat hori = binarizedImage.Clone();

                

                // Especificar tamaño en el eje horizontal
                int rows = hori.Rows;
                int verticalSize = rows / div;

                // Crear elemento estructural para extraer líneas horizontales a través de operaciones morfológicas
                Mat verticalStructure = CvInvoke.GetStructuringElement(ElementShape.Rectangle, new Size(1, verticalSize), new Point(-1, -1));

                // Aplicar operaciones morfológicas
                CvInvoke.Erode(hori, hori, verticalStructure, new Point(-1, -1), 1, BorderType.Default, new MCvScalar());
                CvInvoke.Dilate(hori, hori, verticalStructure, new Point(-1, -1), 1, BorderType.Default, new MCvScalar());

                // Especificar tamaño en el eje horizontal
                int cols = hori.Cols;
                int horizontalSize = cols / div;

                // Crear elemento estructural para extraer líneas horizontales a través de operaciones morfológicas
                Mat horizontalStructure = CvInvoke.GetStructuringElement(ElementShape.Rectangle, new Size(horizontalSize, 1), new Point(-1, -1));

                // Aplicar operaciones morfológicas
                CvInvoke.Erode(hori, hori, horizontalStructure, new Point(-1, -1), 1, BorderType.Default, new MCvScalar());
                CvInvoke.Dilate(hori, hori, horizontalStructure, new Point(-1, -1), 1, BorderType.Default, new MCvScalar());

                // Se extrae el ROI de la imagen binarizada
                roiImage = ExtractROI(hori);
            }
            else
            {
                // Se extrae el ROI de la imagen binarizada
                roiImage = ExtractROI(binarizedImage);
            }

            // Colocamos el picturebox del ROI
            SetPictureBoxPositionAndSize();

            try
            {
                // Procesamos el ROI
                BlobProcess(roiImage);
                //updateView(processBuffer, processView, "final.bmp");

            }
            catch (Exception ex)
            {
                MessageBox.Show("Blob Error " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            btnProcess.Enabled = false;
            btnProcess.BackColor = Color.DarkGray;
        }

        private void BlobProcess(Mat image)
        {
            Blobs.Clear();

            minArea = (int)(((Math.Pow(minDiameter, 2) / 4) * Math.PI) * 0.5);
            maxArea = (int)(((Math.Pow(maxDiameter, 2) / 4) * Math.PI) * 2.5);

            var (contours, centers, areas, perimeters, holePresent) = FindContoursWithEdgesAndCenters(image);

            // Inicializamos variables
            double MaxD = 0;
            double MinD = 99999;
            double avgD = 0;
            double avgDIA = 0;
            int n = 0;
            int sec = 1;
            int nHoles = 0;

            List<double> diametersCV = new List<double>();

            for (int i = 0; i < areas.Count; i++)
            {
                if (!IsTouchingEdges(contours[i]))
                //if (true)
                {
                    int sector = sec;
                    

                    Point centro = centers[i];

                    int area = (int)areas[i];
                    double perimeter = perimeters[i];

                    // Calcular la compacidad
                    double compactness = CalculateCompactness((int)area, perimeter);

                    bool drawFlag = true;

                    bool hole = false;

                    double tempFactor = euFactor;

                    // Este diametro lo vamos a dejar para despues
                    double diametroIA = CalculateDiameterFromArea((int)area);

                    // Calculamos el diametro
                    (double diameterTriangles, double maxDiameter, double minDiameter) = CalculateAndDrawDiameterTrianglesAlghoritm(centro, image.ToBitmap(), drawFlag);

                    double ovalidad = CalculateOvality(maxDiameter, minDiameter);

                    ushort size = CalculateSize(maxDiameter, minDiameter, compactness, ovalidad, hole);

                    // Agregamos los datos a la tabla
                    // dataTable.Rows.Add(sector, area, Math.Round(diametroIA * tempFactor, 3), Math.Round(diameterTriangles * tempFactor, 3), Math.Round(maxDiameter * tempFactor, 3), Math.Round(minDiameter * tempFactor, 3), Math.Round(compactness, 3), Math.Round(ovalidad, 3));

                    Blob blob = new Blob((double)area, perimeter, contours[i], diameterTriangles, diametroIA, centro, maxDiameter, minDiameter, sector, compactness, size, ovalidad, hole);

                    AddLineToCsv((blob.Sector).ToString(), sizes[size], compactness.ToString(), ((blob.DMayor * euFactor)/25.4).ToString(), ((blob.DMenor * euFactor)/25.4).ToString(), ((blob.Diametro * euFactor)/25.4).ToString(),blob.Ovalidad.ToString());

                    if (size != 6) // Shape
                    {
                        if (maxDiameter > MaxD)
                        {
                            MaxD = maxDiameter;
                        }
                        if (minDiameter < MinD)
                        {
                            MinD = minDiameter;
                        }
                        diametersCV.Add(diametroIA);
                        // Sumamos para promediar
                        avgDIA += (diametroIA);
                        avgD += (diameterTriangles * tempFactor);
                        // Aumentamos el numero de elementos para promediar
                        n++;
                    }

                    // Agregamos el elemento a la lista
                    Blobs.Add(blob);

                    if (drawFlag)
                    {
                        try
                        {
                            // Dibujamos el centro
                            DrawCenter(centro, 2, image);

                            // Dibujamos el sector
                            DrawNumber(image, centro, sec);

                            // Dibujamos el numero del sector
                            //drawSectorNumber(image, centro, sector - 1);

                            //drawSize(image, sector, size);

                            //if (hole && size != 6)
                            //{
                            //    nHoles++;
                            //    drawHole(image, sector);
                            //}
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message);
                        }

                        sec++;
                    }
                }
            }

            // Limpiamos la tabla
            dataTable.Clear();
            // Agregamos los datos a la tabla
            SetDataTable();

            CheckHoles(nHoles);

            try
            {
                DrawPerimeters(image, contours, 1);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

            try
            {
                if (imageWidth == 1280)
                {
                    image.Save(imagesPath + "finalOriginal.bmp");
                    CvInvoke.Resize(image, image, new Size(UserROI.GetWidth()/2,UserROI.GetHeight()/2));
                    image.Save(imagesPath + "final.bmp");
                }
                else
                {
                    image.Save(imagesPath + "final.bmp");
                }
                
                pbROI.ImageLocation = imagesPath + "final.bmp";
                pbROI.LoadAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            // Calculamos el promedio de los diametros
            if (MinD == 99999)
            {
                MinD = 0;
            }
            avgDIA /= n;
            avgD /= n;

            double cv = CalculateCV(diametersCV);

            int validObjects = Blobs.Count(Blob => Blob.Size != 6);
            lblValidObjects.Text = validObjects.ToString();

            if (validObjects >= minBlobObjects)
            {
                QueueFrame(1);
                CheckCV(cv);
                ProcessControlDiameter(avgDIA);

                lblValidObjects.ForeColor = Color.Green;
                lblCV.Text = Math.Round(cv, 3).ToString();
            }
            else
            {
                lblValidObjects.ForeColor = Color.Red;
                QueueFrame(0);
            }

            CheckLastFrames();

            if (units == "mm")
            {
                // Asignamos el texto del promedio de los diametros
                lblAvgDiameter.Text = Math.Round(avgD, nUnitsMm).ToString();
                lblMaxDiameter.Text = Math.Round(MaxD * euFactor, nUnitsMm).ToString();
                lblMinDiameter.Text = Math.Round(MinD * euFactor, nUnitsMm).ToString();
                lblSEQDiameter.Text = Math.Round(avgDIA * euFactor, nUnitsMm).ToString();
                dplControlDiameter.Text = Math.Round(controlDiameter * euFactor, nUnitsMm).ToString();
                dplControlDiameter.Text = Math.Round(controlDiameter * euFactor, nUnitsMm).ToString();

                if (controlDiameter > maxDiameter || controlDiameter < minDiameter)
                {
                    dplControlDiameter.BackColor = Color.IndianRed;
                }
                else
                {
                    dplControlDiameter.BackColor = Color.LightGreen;
                }
            }
            else
            {
                // Asignamos el texto del promedio de los diametros
                lblAvgDiameter.Text = Math.Round(avgD, nUnitsInch).ToString();
                lblMaxDiameter.Text = Math.Round(MaxD, nUnitsInch).ToString();
                lblMinDiameter.Text = Math.Round(MinD, nUnitsInch).ToString();
                lblSEQDiameter.Text = Math.Round(avgDIA * euFactor, nUnitsInch).ToString();
                dplControlDiameter.Text = Math.Round(controlDiameter * euFactor, nUnitsInch).ToString();
                dplControlDiameter.Text = Math.Round(controlDiameter * euFactor, nUnitsInch).ToString();
                if (controlDiameter > maxDiameter || controlDiameter < minDiameter)
                {
                    dplControlDiameter.BackColor = Color.IndianRed;
                }
                else
                {
                    dplControlDiameter.BackColor = Color.LightGreen;
                }
            }

            // Asignar la DataTable al DataGridView
            dataGridView1.DataSource = dataTable;
        }

        private void CheckLastFrames()
        {
            int a = validFrames.Count(e => e == 1);
            flagValidFrames.Text = a.ToString();

            if (a < validFramesLimit)
            {
                flagValidFrames.BackColor = Color.IndianRed;
            }
            else
            {
                flagValidFrames.BackColor = Color.LightGreen;
            }
        }

        private void ProcessControlDiameter(double diam)
        {
            if (!double.IsNaN(diam))
            {
                double validateControl = Filtro(diam);
                if (validateControl > maxDiameter * 3)
                {
                    validateControl = maxDiameter * 3;
                }
                else if (validateControl < 0)
                {
                    validateControl = 0;
                }
                controlDiameter = validateControl;
            }
            else
            {
                controlDiameter = Filtro(0);
            }

        }

        public double Filtro(double k)
        {
            double newK = k * (1 - alpha) + controlDiameterOld * alpha;
            controlDiameterOld = newK;
            return newK;
        }

        private void CheckCV(double cv)
        {
            QueueCV(cv);
            flagAlign.Text = Math.Round(cvQueue.Sum(), 2).ToString();
            if (cvQueue.Sum() >= align)
            {
                flagAlign.BackColor = Color.IndianRed;
            }
            else
            {
                flagAlign.BackColor = Color.Silver;
            }
        }

        private void QueueCV(double std)
        {
            if (cvQueue.Count >= 10)
            {
                cvQueue.Dequeue();
            }
            cvQueue.Enqueue(std);
        }

        private void QueueFrame(int v)
        {
            if (validFrames.Count >= 10)
            {
                validFrames.Dequeue();
            }

            validFrames.Enqueue(v);
        }

        static double CalculateCV(IEnumerable<double> values)
        {
            if (values == null || !values.Any())
            {
                return 0;
            }

            // Calcular la media
            double mean = values.Average();

            // Calcular la suma de los cuadrados de las diferencias respecto a la media
            double sumOfSquares = values.Sum(value => Math.Pow(value - mean, 2));

            // Calcular la varianza (usamos Count - 1 para la muestra, Count para la población)
            double variance = sumOfSquares / values.Count();

            double std = Math.Sqrt(variance);

            // Calcular la desviación estándar
            return (std / mean) * 100;
        }

        private void CheckHoles(int holes)
        {
            QueueHoles(holes);
            //Console.WriteLine(holesQueue.Sum());
            flagFH.Text = holesQueue.Sum().ToString();
            //flagFFH.Text = holesQueue.Sum().ToString();
            if (holesQueue.Sum() >= FFH)
            {
                //flagFFH.BackColor = Color.Red;
                flagFH.BackColor = Color.IndianRed;
            }
            else if (holesQueue.Sum() >= FH)
            {
                //flagFFH.BackColor = Color.Silver;
                flagFH.BackColor = Color.Orange;
            }
            else
            {
                //flagFFH.BackColor = Color.Silver;
                flagFH.BackColor = Color.Silver;
            }
        }

        private void QueueHoles(int holes)
        {
            if (holesQueue.Count >= 10)
            {
                holesQueue.Dequeue();
            }
            holesQueue.Enqueue(holes);
        }

        private void DrawNumber(Mat image, Point centro, int n)
        {
            if (imageWidth == 1280)
            {
                MCvScalar brush = new MCvScalar(0, 0, 255);
                Point punto = new Point(centro.X - 10, centro.Y - 10);
                CvInvoke.PutText(image, n.ToString(), punto, FontFace.HersheySimplex, 1, brush, 2);
            }
            else
            {
                MCvScalar brush = new MCvScalar(0, 0, 255);
                Point punto = new Point(centro.X - 10, centro.Y - 10);
                CvInvoke.PutText(image, n.ToString(), punto, FontFace.HersheySimplex, 0.5, brush, 1);
            }
            
        }

        private void SetDataTable()
        {
            IEnumerable<Blob> blobs = Blobs.OrderBy(Blob => Blob.Sector);

            foreach (Blob blob in blobs)
            {
                string size = "";
                if (blob.Hole && blob.Size != 6)
                {
                    size = sizes[blob.Size] + "/Hole";
                }
                else
                {
                    size = sizes[blob.Size];
                }

                dataTable.Rows.Add(blob.Sector, size, Math.Round(blob.DiametroIA * euFactor, 3), Math.Round(blob.Diametro * euFactor, 3), Math.Round(blob.DMayor * euFactor, 3), Math.Round(blob.DMenor * euFactor, 3), Math.Round(blob.Compacidad, 3), Math.Round(blob.Ovalidad, 3), blob.Area);
            }
        }

        private void DrawCenter(Point centro, int thickness, Mat image)
        {
            CvInvoke.Circle(image, centro, thickness, new MCvScalar(255, 255, 0));
        }

        // Función para dibujar un punto con un grosor dado
        void DrawPerimeters(Mat image, VectorOfVectorOfPoint perimeter, int thickness)
        {
            CvInvoke.DrawContours(image, perimeter, -1, new MCvScalar(255, 255, 0), thickness);
        }

        private double CalculateOvality(double maxDiameter, double minDiameter)
        {
            //double ovality = Math.Sqrt((1 - (Math.Pow(minDiameter, 2) / Math.Pow(maxDiameter, 2))));
            double ovality = maxDiameter / minDiameter;
            return ovality;
        }

        private ushort CalculateSize(double dMayor, double dMenor, double compacidad, double ovalidad, bool hole)
        {
            ushort size = 1; // Normal
            double maxOvality = maxDiameter / minDiameter;

            if (hole && compacidad > maxCompactnessHole || (!hole && compacidad > maxCompactness))
            {
                size = 6; // Shape
            }
            else if (ovalidad > maxOvality)
            {
                size = 4; // Oval
            }
            else if (dMayor > maxDiameter)
            {
                size = 2; // Big
            }
            else if (dMenor < minDiameter)
            {
                size = 3; // Small
            }

            return size;
        }

        private (double, double, double) CalculateAndDrawDiameterTrianglesAlghoritm(Point center, Bitmap image, bool draw = true)
        {

            double diameter, maxDiameter, minDiameter;
            List<Point> listXY = new List<Point>();

            maxDiameter = 0; minDiameter = 0;

            int[] deltaX = { 1, 4, 2, 1, 1, 1, 0, -1, -1, -1, -2, -4, -1, -4, -2, -1, -1, -1,  0,  1,  1,  1,  2,  4 };
            int[] deltaY = { 0, 1, 1, 1, 2, 4, 1,  4,  2,  1,  1,  1,  0, -1, -1, -1, -2, -4, -1, -4, -2, -1, -1, -1 };

            int[] correction = { 0, -2, -1, 0, -1, -2, 0, -2, -1, 0, -1, -2, 0, -2, -1, 0, -1, -2, 0, -2, -1, 0, -1, -2 };

            double avg_diameter = 0;

            int x = center.X;
            int y = center.Y;

            int newX = x;
            int newY = y;

            double[] radialLenght = new double[24];

            for (int i = 0; i < 24; i++)
            {
                int iteration = 0;
                Color pixelColor = image.GetPixel(newX, newY);

                while (pixelColor.GetBrightness() != tortillaPolarity)
                {
                    iteration++;

                    newX += deltaX[i];
                    newY += deltaY[i];

                    if (newX >= image.Width || newX < 0)
                    {
                        newX -= deltaX[i];
                    }

                    if (newY >= image.Height || newY < 0)
                    {
                        newY -= deltaY[i];
                    }

                    pixelColor = image.GetPixel(newX, newY);

                    if (iteration >= maxIteration)
                    {
                        iteration = 0;
                        break;
                    }

                }

                double hipotenusa = Math.Sqrt(Math.Pow(deltaX[i], 2) + Math.Pow(deltaY[i], 2));

                listXY.Add(new Point(newX, newY));

                radialLenght[i] = Math.Sqrt(Math.Pow((x - newX), 2) + Math.Pow((y - newY), 2)) - hipotenusa / 2; //+ correction[i];

                avg_diameter += radialLenght[i];
                newX = x; newY = y;
            }

            diameter = avg_diameter / 12;

            List<double> diameters = new List<double>();

            for (int i = 0; i < 12; i++)
            {
                double diam = radialLenght[i] + radialLenght[i + 12];
                diameters.Add(diam);
            }

            maxDiameter = diameters.Max();
            minDiameter = diameters.Min();

            if (draw)
            {
                if (diameter90Deg)
                {
                    int maxIndex = diameters.IndexOf(maxDiameter);
                    int minIndex;
                    if (maxIndex >= 6)
                    {
                        minIndex = maxIndex - 6;
                    }
                    else
                    {
                        minIndex = maxIndex + 6;
                    }

                    using (Graphics g = Graphics.FromImage(image))
                    {
                        Pen pen1 = new Pen(Color.Green, 2);
                        Pen pen2 = new Pen(Color.Red, 2);

                        // Dibujar diámetro máximo
                        g.DrawLine(pen1, new Point(center.X, center.Y), listXY[maxIndex]);
                        g.DrawLine(pen1, new Point(center.X, center.Y), listXY[maxIndex + 12]);

                        // Dibujar diámetro minimo
                        g.DrawLine(pen2, new Point(center.X, center.Y), listXY[minIndex]);
                        g.DrawLine(pen2, new Point(center.X, center.Y), listXY[minIndex + 12]);
                    }

                    minDiameter = diameters[minIndex];
                }
                else
                {
                    int maxIndex = diameters.IndexOf(maxDiameter);
                    int minIndex = diameters.IndexOf(minDiameter);
                    

                    using (Graphics g = Graphics.FromImage(image))
                    {
                        Pen pen1 = new Pen(Color.Green, 2);
                        Pen pen2 = new Pen(Color.Red, 2);

                        // Dibujar diámetro máximo
                        g.DrawLine(pen1, new Point(center.X, center.Y), listXY[maxIndex]);
                        g.DrawLine(pen1, new Point(center.X, center.Y), listXY[maxIndex + 12]);

                        // Dibujar diámetro minimo
                        g.DrawLine(pen2, new Point(center.X, center.Y), listXY[minIndex]);
                        g.DrawLine(pen2, new Point(center.X, center.Y), listXY[minIndex + 12]);
                    }
                }

            }

            
            return (diameter, maxDiameter, minDiameter);
        }

        private double CalculateDiameterFromArea(int area)
        {
            const double pi = Math.PI;

            // Calcular el diámetro utilizando la fórmula d = sqrt(4 * Área / pi)
            double diameter = Math.Sqrt(4 * area / pi);

            return diameter;
        }

        private double CalculateCompactness(int area, double perimeter)
        {
            // Lógica para calcular la compacidad
            // Se asume que el área y el perímetro son mayores que cero para evitar divisiones por cero
            double compactness = (perimeter * perimeter) / (double)area;

            return compactness;
        }

        public bool IsTouchingEdges(VectorOfPoint vector)
        {
            foreach (Point a in vector.ToArray())
            {
                int x = a.X + UserROI.Left;
                int y = a.Y + UserROI.Top;
                if (x == UserROI.Left || x == UserROI.Right - 1 || y == UserROI.Top || y == UserROI.Bottom - 1)
                {
                    return true;
                }
            }

            return false;
        }

        private (VectorOfVectorOfPoint, List<Point>, List<double>, List<double>, List<bool>) FindContoursWithEdgesAndCenters(Mat image)
        {
            Mat grayImage = new Mat();
            CvInvoke.CvtColor(image, grayImage, ColorConversion.Bgr2Gray);

            // Encontrar contornos
            VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();
            VectorOfVectorOfPoint filteredContours = new VectorOfVectorOfPoint();
            Mat jerarquia = new Mat();

            List<Point> centroids = new List<Point>();
            List<double> areas = new List<double>();
            List<double> perimeters = new List<double>();
            List<bool> holePresent = new List<bool>();

            CvInvoke.FindContours(grayImage, contours, jerarquia, RetrType.Tree, ChainApproxMethod.ChainApproxSimple);


            // Coloreamos todos los pixeles de fondo
            Array array = jerarquia.GetData();
            //for (int i = 0; i < contours.Size; i++)
            //{
            //    // Si el contorno tiene un contorno padre
            //    int a = (int)array.GetValue(0, i, 3);
            //    int area = (int)CvInvoke.ContourArea(contours[i]);
            //    //MessageBox.Show(array.GetValue(0,i,3).ToString());
            //    if (a != -1 && area > 0 && area < minArea)
            //    {
            //        // Dibujar el contorno interno en verde
            //        CvInvoke.DrawContours(image, contours, i, new MCvScalar(0, 255, 0), -1);
            //    }
            //}

            for (int i = 0; i < contours.Size; i++)
            {
                double area = CvInvoke.ContourArea(contours[i]);
                if (area >= minArea && area <= maxArea)
                {
                    double perimeter = CvInvoke.ArcLength(contours[i], true);
                    int indicePrimerHijo = Convert.ToInt32(array.GetValue(0, i, 2));
                    if (indicePrimerHijo != -1)
                    {
                        holePresent.Add(true);
                        // El contorno tiene al menos un hijo
                        // Iterar sobre los hijos y hacer lo que necesites
                        int indiceHijoActual = indicePrimerHijo;
                        do
                        {
                            // Acceder al contorno hijo en el vector de contornos
                            //VectorOfPoint contornoHijo = contours[indiceHijoActual];
                            // Dibujar el contorno interno en verde
                            CvInvoke.DrawContours(image, contours, indiceHijoActual, new MCvScalar(0, 255, 0), -1);

                            //area -= CvInvoke.ContourArea(contornoHijo);
                            //perimeter += CvInvoke.ArcLength(contornoHijo, true);

                            // Obtener el índice del siguiente hijo del contorno padre actual
                            indiceHijoActual = Convert.ToInt32(array.GetValue(0, indiceHijoActual, 0));

                        } while (indiceHijoActual != -1); // Continuar mientras haya más hijos
                    }
                    else
                    {
                        holePresent.Add(false);
                    }

                    areas.Add(area);
                    filteredContours.Push(contours[i]);
                    perimeters.Add(perimeter);

                    var moments = CvInvoke.Moments(contours[i]);
                    if (moments.M00 != 0)
                    {
                        // Calcular centroides
                        float cx = (float)(moments.M10 / moments.M00);
                        float cy = (float)(moments.M01 / moments.M00);
                        centroids.Add(new Point((int)cx, (int)cy));
                    }
                }
            }

            return (filteredContours, centroids, areas, perimeters, holePresent);
        }

        private void SetPictureBoxPositionAndSize()
        {
            int width = 0;
            int height = 0;

            if (imageWidth == 640)
            {
                // Calcular el tamaño de la imagen
                width = UserROI.GetWidth();
                height = UserROI.GetHeight();
                // Ubicar el PictureBox en la posición del ROI
                pbROI.Location = new Point(UserROI.Left, UserROI.Top);
                pbROI.Size = new Size(width, height);
            }
            else
            {
                // Calcular el tamaño de la imagen
                width = UserROI.GetWidth()/2;
                height = UserROI.GetHeight()/2;
                // Ubicar el PictureBox en la posición del ROI
                pbROI.Location = new Point(UserROI.Left/2, UserROI.Top/2);
                pbROI.Size = new Size(width, height);
            }

            //originalBox.SendToBack();
            pbROI.Visible = true;
            pbROI.BringToFront();
        }

        private Mat BinarizeImage(Mat image)
        {
            try
            {
                if (autoThreshold)
                {
                    threshold = CalculateOtsuThreshold();
                    txtThreshold.Text = threshold.ToString();
                }
                //else
                //{
                //    threshold = int.Parse(txtThreshold.Text);
                //}
            }
            catch (FormatException)
            {

            }

            // Aplicar umbralización (binarización)
            Mat imagenBinarizada = new Mat();
            CvInvoke.Threshold(image, imagenBinarizada, threshold, 255, ThresholdType.Binary);
            //image.Dispose();

            // Guardar la imagen binarizada
            imagenBinarizada.Save(imagesPath + "imagen_binarizada.jpg");

            return imagenBinarizada;
        }

        private int CalculateOtsuThreshold()
        {
            long totalPixels = 0;
            for (int i = 0; i < Histogram.Length; i++)
            {
                totalPixels += Histogram[i];
            }

            double sum = 0;
            for (int i = 0; i < Histogram.Length; i++)
            {
                sum += i * Histogram[i];
            }

            double sumB = 0;
            long wB = 0;
            long wF = 0;

            double varMax = 0;
            int threshold = 0;

            for (int i = 0; i < Histogram.Length; i++)
            {
                wB += Histogram[i];
                if (wB == 0)
                    continue;

                wF = totalPixels - wB;
                if (wF == 0)
                    break;

                sumB += i * Histogram[i];

                double mB = sumB / wB;
                double mF = (sum - sumB) / wF;

                double varBetween = wB * wF * (mB - mF) * (mB - mF);

                if (varBetween > varMax)
                {
                    varMax = varBetween;
                    threshold = i;
                }
            }

            return threshold;
        }

        private Mat ExtractROI(Mat image)
        {
            // Extraer la región del ROI
            // Definir las coordenadas del ROI (rectángulo de interés)
            Rectangle roiRect = new Rectangle(UserROI.Left, UserROI.Top, UserROI.Right - UserROI.Left, UserROI.Bottom - UserROI.Top); // (x, y, ancho, alto)

            // Extraer el ROI de la imagen original
            Mat roiImage = new Mat(image, roiRect);
            image.Dispose();

            return roiImage;
        }

        private void txtImageSize_TextChanged(object sender, EventArgs e)
        {
            ResizeTextBoxToFitText(txtImageSize);
        }

        private void btnIncreaseRoiWidth_Click(object sender, EventArgs e)
        {
            if (File.Exists(imagesPath + "updatedROI.bmp"))
            {
                int roiWidth = (UserROI.Right - UserROI.Left) + deltaRoi;
                if (roiWidth > imageWidth-10) roiWidth = (imageWidth - 10);
                if (roiWidth % 2 == 0)
                {
                    UserROI.Left = imageWidth/2 - roiWidth / 2;
                    UserROI.Right = imageWidth / 2 + roiWidth / 2;
                }
                else
                {
                    UserROI.Left = imageWidth / 2 - (int)(roiWidth / 2) + 1;
                    UserROI.Right = imageWidth / 2 + (int)(roiWidth / 2);
                }

                settings.ROI = UserROI;
                //settings.ROI_Left = UserROI.Left;
                //settings.ROI_Right = UserROI.Right;
                txtRoiWidth.Text = roiWidth.ToString();
                UpdateROI();
            }
            else
            {
                MessageBox.Show("Please first take a frame");
            }

            settings.Save();
        }

        private void UpdateROI()
        {
            pbROI.Visible = false;

            Mat originalROIImage = CvInvoke.Imread(imagesPath + "updatedROI.bmp");

            DrawROI(originalROIImage);

            originalROIImage.Save(imagesPath + "roiDraw.bmp");

            pbMain.ImageLocation = imagesPath + "roiDraw.bmp";
            pbMain.LoadAsync();

            originalROIImage.Dispose();
        }

        private void btnIncreaseRoiHeight_Click(object sender, EventArgs e)
        {
            if (File.Exists(imagesPath + "updatedROI.bmp"))
            {
                int roiHeight = (UserROI.Bottom - UserROI.Top) + deltaRoi;
                if (roiHeight > imageHeight-10) roiHeight = (imageHeight - 10);
                if (roiHeight % 2 == 0)
                {
                    UserROI.Top = imageHeight / 2 - roiHeight / 2;
                    UserROI.Bottom = imageHeight / 2 + roiHeight / 2;
                }
                else
                {
                    UserROI.Top = imageHeight / 2 - (int)(roiHeight / 2) + 1;
                    UserROI.Bottom = imageHeight / 2 + (int)(roiHeight / 2);
                }

                settings.ROI = UserROI;

                //settings.ROI_Bottom = UserROI.Bottom;
                //settings.ROI_Top = UserROI.Top;
                txtRoiHeight.Text = roiHeight.ToString();
                UpdateROI();
            }
            else
            {
                MessageBox.Show("Please first take a frame");
            }

            settings.Save();

        }

        private void btnDecreaseRoiWidth_Click(object sender, EventArgs e)
        {
            if (File.Exists(imagesPath + "updatedROI.bmp"))
            {
                int roiWidth = (UserROI.Right - UserROI.Left) - deltaRoi;
                if (roiWidth < 10) roiWidth = (10);
                if (roiWidth % 2 == 0)
                {
                    UserROI.Left = imageWidth / 2 - roiWidth / 2;
                    UserROI.Right = imageWidth / 2 + roiWidth / 2;
                }
                else
                {
                    UserROI.Left = imageWidth / 2 - (int)(roiWidth / 2) + 1;
                    UserROI.Right = imageWidth / 2 + (int)(roiWidth / 2);
                }

                settings.ROI = UserROI;

                //settings.ROI_Left = UserROI.Left;
                //settings.ROI_Right = UserROI.Right;
                txtRoiWidth.Text = roiWidth.ToString();
                UpdateROI();
            }
            else
            {
                MessageBox.Show("Please first take a frame");
            }

            settings.Save();

        }

        private void btnDecreaseRoiHeight_Click(object sender, EventArgs e)
        {
            if (File.Exists(imagesPath + "updatedROI.bmp"))
            {
                int roiHeight = (UserROI.Bottom - UserROI.Top) - deltaRoi;
                if (roiHeight < 10) roiHeight = (10);
                if (roiHeight % 2 == 0)
                {
                    UserROI.Top = imageHeight / 2 - roiHeight / 2;
                    UserROI.Bottom = imageHeight / 2 + roiHeight / 2;
                }
                else
                {
                    UserROI.Top = imageHeight / 2 - (int)(roiHeight / 2) + 1;
                    UserROI.Bottom = imageHeight / 2 + (int)(roiHeight / 2);
                }

                settings.ROI = UserROI;

                //settings.ROI_Bottom = UserROI.Bottom;
                //settings.ROI_Top = UserROI.Top;
                txtRoiHeight.Text = roiHeight.ToString();
                UpdateROI();
            }
            else
            {
                MessageBox.Show("Please first take a frame");
            }

            settings.Save();

        }

        private void btnDrawRoi_Click(object sender, EventArgs e)
        {
            drawRoi = !drawRoi;

            if (drawRoi)
            {
                btnDrawRoi.BackColor = Color.LightGreen;
            }
            else
            {
                btnDrawRoi.BackColor = Color.Silver;
            }
        }

        private void btnAutoThreshold_Click(object sender, EventArgs e)
        {
            autoThreshold = true;

            btnAutoThreshold.BackColor = Color.LightGreen;
            btnManualThreshold.BackColor = Color.Silver;
        }

        private void btnManualThreshold_Click(object sender, EventArgs e)
        {
            autoThreshold = false;

            btnAutoThreshold.BackColor = Color.Silver;
            btnManualThreshold.BackColor = Color.LightGreen;
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            pbMain.ImageLocation = actualImagePath;
            pbMain.LoadAsync();
            pbROI.Visible = false;
        }

        private void rbtThermoCamera_CheckedChanged(object sender, EventArgs e)
        {
            if (rbtThermoCamera.Checked)
            {
                cameraConfig = "Thermo";
                rbtMonoCamera.Checked = false;
                deltaRoi = 6;
                
            }
            
            Console.WriteLine(cameraConfig);

        }
         
        private void ChangeROI(int width)
        {
            if (width == 640)
            {
                if (!(UserROI.GetWidth() <= 630 && UserROI.GetHeight() <= 470))
                {
                    UserROI.Left = UserROI.Left / 2;
                    UserROI.Right = UserROI.Right / 2;
                    UserROI.Top = UserROI.Top / 2;
                    UserROI.Bottom = UserROI.Bottom / 2;

                    settings.ROI = UserROI;

                    txtRoiWidth.Text = UserROI.GetWidth().ToString();
                    txtRoiHeight.Text = UserROI.GetHeight().ToString();
                }
            }
            else
            {
                if (UserROI.GetWidth() <= 630 && UserROI.GetHeight() <= 470)
                {
                    UserROI.Left = UserROI.Left * 2;
                    UserROI.Right = UserROI.Right * 2;
                    UserROI.Top = UserROI.Top * 2;
                    UserROI.Bottom = UserROI.Bottom * 2;

                    settings.ROI = UserROI;

                    txtRoiWidth.Text = UserROI.GetWidth().ToString();
                    txtRoiHeight.Text = UserROI.GetHeight().ToString();
                }
            }
        }

        private void rbtMonoCamera_CheckedChanged(object sender, EventArgs e)
        {
            if (rbtMonoCamera.Checked)
            {
                cameraConfig = "Mono";
                rbtThermoCamera.Checked = false;
                deltaRoi = 12;

            }
            
            Console.WriteLine(cameraConfig);
        }

        private void txtMinDiameter_Click(object sender, EventArgs e)
        {
            ShowInputKeyboard((TextBox)sender, 0);
            AdjustControlDiameter();
        }

        private void ShowInputKeyboard(TextBox textBox, int v)
        {
            using (var keyboardForm = new KeyBoard(textBox, v, false))
            {
                DialogResult r = keyboardForm.ShowDialog();

                if (r == DialogResult.OK)
                {
                    GetDataTxt();
                    textBox.DeselectAll();
                }
            }
        }

        private void GetDataTxt()
        {
            // Diameters
            double minD = 0;
            if (!(double.TryParse(txtMinDiameter.Text, out minD)))
            {
                MessageBox.Show("Use a valid number");
                txtMinDiameter.Text = (minDiameter * euFactor).ToString();
            }
            else
            {
                minDiameter = minD/euFactor;
            }

            double maxD = 0;
            if (!(double.TryParse(txtMaxDiameter.Text, out maxD)))
            {
                MessageBox.Show("Use a valid number");
                txtMaxDiameter.Text = (maxDiameter * euFactor).ToString(); ;
            }
            else
            {
                maxDiameter = maxD / euFactor;
            }

            // Threshold 
            if (!(int.TryParse(txtThreshold.Text, out threshold)))
            {
                MessageBox.Show("Use a valid number");
                txtThreshold.Text = threshold.ToString();
            }

            // EU Factor
            if (!(double.TryParse(txtEuFactor.Text, out euFactor)))
            {
                MessageBox.Show("Use a valid number");
                txtEuFactor.Text = euFactor.ToString();
            }
            else
            {
                settings.EUFactor = euFactor;
            }

            // Round Compacity
            if (!double.TryParse(txtMaxCompacity.Text, out maxCompactness))
            {
                Console.WriteLine("Shape Round Limit Invalid");
                txtMaxCompacity.Text = maxCompactness.ToString();
            }
            else
            {
                settings.maxCompacity = (float)maxCompactness;
            }
            // Hole compacity
            if (!double.TryParse(txtMaxCompacityHole.Text, out maxCompactnessHole))
            {
                Console.WriteLine("Shape Hole Limit Invalid");
                txtMaxCompacityHole.Text = maxCompactnessHole.ToString();
            }
            else
            {
                settings.maxCompacityHole = (float)maxCompactnessHole;
            }
            // Min objects
            if (!int.TryParse(txtMinBlobObjects.Text, out minBlobObjects))
            {
                Console.WriteLine("Number of Objects Invalid");
                txtMinBlobObjects.Text = minBlobObjects.ToString();
            }
            else
            {
                settings.minBlobObjects = minBlobObjects;
            }
            // Alpha
            if (!float.TryParse(txtAlpha.Text, out alpha))
            {
                Console.WriteLine("Alpha Value Invalid");
                txtAlpha.Text = alpha.ToString();
            }
            else
            {
                settings.alpha = alpha;
            }
            // Max diameter
            if (double.TryParse(txtMaxDiameter.Text, out maxDiameter))
            {
                maxDiameter = maxDiameter / euFactor;
                settings.maxDiameter = maxDiameter;
            }
            else
            {
                MessageBox.Show("Use a valid number", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            // Min Diameter
            if (double.TryParse(txtMinDiameter.Text, out minDiameter))
            {
                minDiameter = minDiameter / euFactor;
                settings.minDiameter = minDiameter;
            }
            else
            {
                MessageBox.Show("Use a valid number", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            // FH
            if (int.TryParse(txtFH.Text, out FH))
            {
                settings.FH = FH;
            }
            else
            {
                MessageBox.Show("Use a valid number", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            // FFH
            if (int.TryParse(txtFFH.Text, out FFH))
            {
                settings.FFH = FFH;
            }
            else
            {
                MessageBox.Show("Use a valid number", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            // Align
            if (float.TryParse(txtDiameterVariation.Text, out align))
            {
                settings.align = align;
            }
            else
            {
                MessageBox.Show("Use a valid number", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            settings.Save();

            //// Valid Frames Limir
            //if (int.TryParse(txtValidFramesLimit.Text, out validFramesLimit))
            //{
            //}
            //else
            //{
            //    MessageBox.Show("Use a valid number", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            //    txtValidFramesLimit.Text = validFramesLimit.ToString();
            //}

        }

        private void updateUnits(string unitsNew)
        {
            float fact = 0;
            if (units != unitsNew)
            {
                units = unitsNew;
                lblMaxDiameterUnits.Text = units;
                lblMinDiameterUnits.Text = units;

                txtAvgDiameterUnits.Text = units;
                txtAvgMaxDiameterUnits.Text = units;
                txtAvgMinDiameterUnits.Text = units;
                txtControlDiameterUnits.Text = units;
                txtEquivalentDiameterUnits.Text = units;

                switch (unitsNew)
                {
                    // inch/px
                    case "mm":
                        euFactor *= 25.4; //mm/inch
                        fact = 25.4f;
                        break;
                    // mm/px

                    // mm/px
                    case "inch":
                        euFactor *= 0.0393701; // inch/mm
                        fact = 0.0393701f;
                        break;
                        // inch/px
                }

                settings.Units = units;
                txtEuFactor.Text = Math.Round(euFactor, 3).ToString();
                settings.EUFactor = euFactor;

                // Actualizamos los datos de la tabla
                if (dataTable.Rows.Count > 0)
                {
                    dataTable.Clear();
                    SetDataTable();
                }

                if (unitsNew == "inch")
                {
                    double avgDiameter = 0;
                    if (Double.TryParse(lblAvgDiameter.Text, out avgDiameter)) ;
                    avgDiameter *= fact;
                    lblAvgDiameter.Text = Math.Round(avgDiameter, nUnitsInch).ToString();


                    double mxDiameter = 0;
                    if (Double.TryParse(lblMaxDiameter.Text, out mxDiameter)) ;
                    mxDiameter *= fact;
                    lblMaxDiameter.Text = Math.Round(mxDiameter, nUnitsInch).ToString();

                    double mnDiameter = 0;
                    if (Double.TryParse(lblMinDiameter.Text, out mnDiameter)) ;
                    mnDiameter *= fact;
                    lblMinDiameter.Text = Math.Round(mnDiameter, nUnitsInch).ToString();


                    double controlDiameter = 0;
                    if (Double.TryParse(dplControlDiameter.Text, out controlDiameter)) ;
                    controlDiameter *= fact;
                    dplControlDiameter.Text = Math.Round(controlDiameter, nUnitsInch).ToString();

                    double avgMinDiameter = 0;
                    if (Double.TryParse(txtMinDiameter.Text, out avgMinDiameter)) ;
                    avgMinDiameter *= fact;
                    txtMinDiameter.Text = Math.Round(avgMinDiameter, nUnitsInch).ToString();

                    double avgMaxDiameter = 0;
                    if (Double.TryParse(txtMaxDiameter.Text, out avgMaxDiameter)) ;
                    avgMaxDiameter *= fact;
                    txtMaxDiameter.Text = Math.Round(avgMaxDiameter, nUnitsInch).ToString();

                    double equivalentDiameter = 0;
                    if (Double.TryParse(lblSEQDiameter.Text, out equivalentDiameter)) ;
                    equivalentDiameter *= fact;
                    lblSEQDiameter.Text = Math.Round(equivalentDiameter, nUnitsInch).ToString();
                }
                else
                {
                    double avgDiameter = 0;
                    if (Double.TryParse(lblAvgDiameter.Text, out avgDiameter)) ;
                    avgDiameter *= fact;
                    lblAvgDiameter.Text = Math.Round(avgDiameter, nUnitsMm).ToString();

                    double mxDiameter = 0;
                    if (Double.TryParse(txtMaxDiameter.Text, out mxDiameter)) ;
                    mxDiameter *= fact;
                    txtMaxDiameter.Text = Math.Round(mxDiameter, nUnitsMm).ToString();

                    double mnDiameter = 0;
                    if (Double.TryParse(txtMinDiameter.Text, out mnDiameter)) ;
                    mnDiameter *= fact;
                    txtMinDiameter.Text = Math.Round(mnDiameter, nUnitsMm).ToString();

                    double controlDiameter = 0;
                    if (Double.TryParse(dplControlDiameter.Text, out controlDiameter)) ;
                    controlDiameter *= fact;
                    dplControlDiameter.Text = Math.Round(controlDiameter, nUnitsMm).ToString();

                    double avgMinDiameter = 0;
                    if (Double.TryParse(lblMinDiameter.Text, out avgMinDiameter)) ;
                    avgMinDiameter *= fact;
                    lblMinDiameter.Text = Math.Round(avgMinDiameter, nUnitsMm).ToString();

                    double avgMaxDiameter = 0;
                    if (Double.TryParse(lblMaxDiameter.Text, out avgMaxDiameter)) ;
                    avgMaxDiameter *= fact;
                    lblMaxDiameter.Text = Math.Round(avgMaxDiameter, nUnitsMm).ToString();

                    double equivalentDiameter = 0;
                    if (Double.TryParse(lblSEQDiameter.Text, out equivalentDiameter)) ;
                    equivalentDiameter *= fact;
                    lblSEQDiameter.Text = Math.Round(equivalentDiameter, nUnitsMm).ToString();
                }
            }
        }

        private void txtMaxDiameter_Click(object sender, EventArgs e)
        {
            ShowInputKeyboard((TextBox)sender, 0);
            AdjustControlDiameter();
        }

        private void AdjustControlDiameter()
        {
            controlDiameter = (maxDiameter + minDiameter) / 2;
            dplControlDiameter.Text = Math.Round(controlDiameter*euFactor,2).ToString();
            dplControlDiameter.BackColor = Color.LightGreen;
            controlDiameterOld = controlDiameter;
        }

        private void txtThreshold_Click(object sender, EventArgs e)
        {
            ShowInputKeyboard((TextBox)sender, 0);
        }

        private void tabControl2_Click(object sender, EventArgs e)
        {
            ShowInputKeyboard((TextBox)sender, 0);
        }

        private void txtEuFactor_Click(object sender, EventArgs e)
        {
            ShowInputKeyboard((TextBox)sender, 0);
            maxDiameter = double.Parse(txtMaxDiameter.Text) / euFactor;
            minDiameter = double.Parse(txtMinDiameter.Text) / euFactor;
        }

        private void cmbPolarity_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (cmbPolarity.SelectedItem)
            {
                case "White":
                    tortillaPolarity = 0;
                    break;
                case "Black":
                    tortillaPolarity = 1;
                    break;
            }
        }

        private void btnLinesFilter_Click(object sender, EventArgs e)
        {
            linesFilter = !linesFilter;
            if (linesFilter)
            {
                btnLinesFilter.BackColor = Color.LightGreen;
            }
            else
            {
                btnLinesFilter.BackColor = Color.Silver;
            }
        }

        private void btnDiameters90Deg_Click(object sender, EventArgs e)
        {
            diameter90Deg = !diameter90Deg;
            if (diameter90Deg)
            {
                btnDiameters90Deg.BackColor = Color.LightGreen;
            }
            else
            {
                btnDiameters90Deg.BackColor = Color.Silver;
            }
        }
    }
}
