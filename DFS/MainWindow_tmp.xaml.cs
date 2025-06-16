// ==============================================================================================================================
//  DFS - DefocusStacker 
//  Version 1.0
// ---------------------
//  Author: Herbert Kopp
//  Last Modification:  04.08.2019
// ==============================================================================================================================

using Microsoft.Win32;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WPFCustomMessageBox;
using System.Windows.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DFS
{
    // Interaktionslogik für MainWindow.xaml
    public partial class MainWindow : System.Windows.Window
    {
        // =====  global objects  ===============================================================================================
        #region
        List<ImageObj> ImgList = new List<ImageObj>();  // Scrollable thumbnail list of all loaded Images
        List<int> DefocusList = new List<int>();        // ImgList indices of the images in the Defocus stack

        int ImgListIndex = -1;                          // points to the current object in the ImgList
        int FG_Index = 0;                               // Index of the Foreground image in the 'ImgList'
        int BG_Index = 0;                               // Index of the Background image in the 'ImgList'
        int ImgHeight = 0;                              // Height of the FG_image, BG_image
        int ImgWidth = 0;                               // Width of the FG_image, BG_image
        int StackFGIndex = -1;                          // index for the foreground image in the image stack

        double KernelSize;                              // kernel size for smoothing and morphological filters
        double BrushSize;                               // brush size for drawing the GCpolyline

        bool rB_GrayActive = false;                     // deactivates 'Click' events during MainWindow initializion
        bool BlobMaskReady = false;                     // prevents +/- selection before 'FindBlobs' was called
        bool GCinitialized = false;                     // prevents the modification of the GrabCut 'result' before first run of GrabCut

        bool FG_Brush_active = true;                    // true:  add foreground brush is active
        bool FinalImageReady = false;                   // indicates if a finalimage is ready in the ImageArea

        bool DeleteKeyPressed = false;                  // 'Delete' key was pressed: we can delete images
        bool EnterKeyPressed = false;                   // 'Enter' key was pressed:  used to continue the DFS algorithm
        bool EscapeKeyPressed = false;                  // 'ESC' key was pressed: aborts a 'delete images' processs
        bool PLUSkeyPressed = false;                    // '+' key was pressed
        bool MINUSkeyPressed = false;                   // '-' key was pressed

        bool SaveImageChecked = true;                   // controls wether the resulting image should be saved
        bool SaveMaskChecked = false;                   // controls wether the GC resp. CC mask should be saved
        //////WriteableBitmap Result_wbitmap = null;          // last result as writable Bitmap;
        //////WriteableBitmap Mask_wbitmap = null;            // last Mask as writable Bitmap;

        Mat FG_image = new Mat();                       // Foreground image
        Mat BG_image = new Mat();                       // Background image
        Mat GCresult = new Mat();                       // segmentation result of the grabCut algorithm
        Mat GCresultMask = new Mat();                   // unblurred mask from GrabCut
        Mat CCresultMask = new Mat();                   // unblurred mask from FindBlobs
        Mat MaskBlurred = new Mat();                    // blurred mask  
        Mat FG_override = new Mat();                    // forces pixels to be foreground (overrides GrabCut GCresult)
        Mat BG_override = new Mat();                    // forces Pixels to be background (in Grabcut GCresult)
        Mat finalImage = new Mat();                     // final image for display in 'ImageArea' or output to a file
        Mat FG_channel = new Mat();                     // CV_8UC1 image derived from FG_image

        BitmapImage FG_BrushBitmap;                     // Bitmaps for the brush icons
        BitmapImage BG_BrushBitmap;
        BitmapImage Inactive_BrushBitmap;

        Thread StackSelection_Thread;                   // Worker thread for the 'Defocus Stacking' algorithm

        ConnectedComponents cc;                         // decomposition of the foreground image into blobs
        OpenCvSharp.Rect GCrect;                        // Rectagle for the GrabCut algorithm 

        // pixel values of the GrabCut segmentation mask 'GCresult'
        const int GC_BGD = 0;                           // background pixel value
        const int GC_FGD = 1;                           // foreground pixel value
        const int GC_PR_BGD = 2;                        // probably foreground pixel value
        const int GC_PR_FGD = 3;                        // probably background pixel value

        System.Windows.Point GCpolylineStart_Rendered;  // starting point of the GCpolyLine relative to the rendered image
        System.Windows.Point GCpolylineStart_ImageArea; // starting point of the GCpolyLine relative to the ImageArea
        System.Windows.Point GCPolyline_PreviousPoint;  // previous mouse position during drawing the polyline

        // Foreground colors for buttons
        SolidColorBrush activeBrush = new SolidColorBrush(Colors.Chartreuse);
        SolidColorBrush whiteBrush = new SolidColorBrush(Colors.White);

        // the current algorithm
        enum Algorithm { NONE, GrabCut, ConnectedComponents } 
        Algorithm algorithm = Algorithm.NONE;

        // temporary algorithm  modes
        enum Mode { NONE, CC, GC, SelectDelete, SelectStack, InitGC, InitCC };
        Mode mode = Mode.NONE;

        // states during drawing the GCpolyline with the mouse
        enum DrawingMode { NONE, NOT_SET, IN_PROCESS, SET };      
        DrawingMode GCpolyLineState = DrawingMode.NONE;

        #endregion
        // =====  global objects  ===============================================================================================

        public MainWindow()
        {
            InitializeComponent();
        }

        // Initializations at startup
        public void MainWindowLoaded(object sender, RoutedEventArgs e)
        {
            // Prepare image loading from the file system
            string root = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string[] supportedExtensions = new[] { ".jpg", ".tiff" };
            var files = Directory.GetFiles(System.IO.Path.Combine(root, "demo_images"), "*.*").Where(s => supportedExtensions.Contains(System.IO.Path.GetExtension(s).ToLower()));

            // Build an initial ImgList:  Demo images are taken from "./images/"
            int index = 0;
            foreach (string file in files)
            {
                ImgList.Add(new ImageObj(ImgList, index, file));
                index++;
            }
            // Initialize the 'ScroolViewer' Control with the images from ImgList
            ImageList.ItemsSource = ImgList;

            // Initialize the Slider SlKernelSize for the Open/Close kernel
            slKernelSize.Minimum = 3;
            slKernelSize.Maximum = 50;
            slKernelSize.Value = 30;
            slKernelSize.ApplyTemplate();

            // Initialize the Slider slBrushSize for the Open/Close kernel
            slBrushSize.Minimum = 3;
            slBrushSize.Maximum = 30;
            slBrushSize.Value = 10;
            slBrushSize.ApplyTemplate();

            // hide busy indicator
            busyIndicator.Visibility = Visibility.Hidden;

            // rb_Gray is now set to checked: after MainWindow is loaded 
            rB_Gray.IsChecked = true;

            // Initialize the FG/BG brushes
            FG_BrushBitmap = new BitmapImage();
            FG_BrushBitmap = new BitmapImage();
            FG_BrushBitmap.BeginInit();
            FG_BrushBitmap.CacheOption = BitmapCacheOption.OnLoad;
            FG_BrushBitmap.DecodePixelWidth = 95;
            FG_BrushBitmap.UriSource = new Uri("data/Brush_addFG.jpg", UriKind.Relative);
            FG_BrushBitmap.EndInit();

            BG_BrushBitmap = new BitmapImage();
            BG_BrushBitmap = new BitmapImage();
            BG_BrushBitmap.BeginInit();
            BG_BrushBitmap.CacheOption = BitmapCacheOption.OnLoad;
            BG_BrushBitmap.DecodePixelWidth = 95;
            BG_BrushBitmap.UriSource = new Uri("data/Brush_removeFG.jpg", UriKind.Relative);
            BG_BrushBitmap.EndInit();

            Inactive_BrushBitmap = new BitmapImage();
            Inactive_BrushBitmap = new BitmapImage();
            Inactive_BrushBitmap.BeginInit();
            Inactive_BrushBitmap.CacheOption = BitmapCacheOption.OnLoad;
            Inactive_BrushBitmap.DecodePixelWidth = 95;
            Inactive_BrushBitmap.UriSource = new Uri("data/Brush_inactive.jpg", UriKind.Relative);
            Inactive_BrushBitmap.EndInit();

            FG_Brush.Source = FG_BrushBitmap;
            BG_Brush.Source = Inactive_BrushBitmap;

            KernelSizeTextBox.IsEnabled = false;
        }
        private void Reset_Globals()
        {
            StackFGIndex = -1;              
         
            DeleteKeyPressed = false;
            EnterKeyPressed = false; 
            EscapeKeyPressed = false; 
            MINUSkeyPressed = false;  
            PLUSkeyPressed = false;  

            rB_GrayActive = true;       
            BlobMaskReady = false;        
            GCinitialized = false;
            GCpolyLineState = DrawingMode.NONE;

            Result_wbitmap = null;                  //??????????????????
            Mask_wbitmap = null;                    //?????????????????????
        }

        // Handler for 'SizeChanged' event of the MainWindow: maintains the width/height proportion
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Update the legth of the 'ImgList'
            ScrollViewer.Width = (int)e.NewSize.Width - 160;
            BtnPanel.Height = this.ActualHeight - 60;
        }

        // Button handlers and methods for image file handling
        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            // prevent any other activities while saveing is in progress          ///????????????????????
            if (_busyIndicator.IsBusy == true) return;

            Reset_ButtonForeground();

            //LoadMultipleImages();
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Multiselect = true;
            openFileDialog.Filter = "JPEG Image (.jpg)|*.jpg|Tiff Image (.tif)|*.tif| All files (*.*)|*.*";
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            if (openFileDialog.ShowDialog() == true)
            {
                int index = ImgList.Count;
                foreach (string file in openFileDialog.FileNames)
                {
                    ImgList.Add(new ImageObj(ImgList, index, file));
                    index++;
                }
                // update the resource reference of the ImageList control
                ImageList.ItemsSource = null;
                ImageList.ItemsSource = ImgList;
            }

        }
        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            // prevent any other activities while saveing is in progress          ///????????????????????
            if (_busyIndicator.IsBusy == true) return;

            Dispatcher.Invoke(() => 
            {
                Reset_ButtonForeground();
                BtnDelete.Foreground = activeBrush;
                Reset_Hints();
            });

            // Deselect all images in the List and show the checkboxes
            foreach (ImageObj Img in ImgList)
            {
                Img.ImgSelected = false;                    // deselect all images
                Img.CheckBoxOverlay = Visibility.Hidden;    // show the checkboxes
            }
            Dispatcher.Invoke(() =>
            {
                ImageList.ItemsSource = null;
                ImageList.ItemsSource = ImgList;
            });

            // Set user hints
            Set_Hint(1, "  1. Select images from the Image List.", 230, true, true);
            Set_Hint(2, "  2. Delete them with the 'Delete' key or ...", 260, true, true);
            Set_Hint(3, "  ... use the 'Escape' key to abort.", 230, true, true);

            // Disable all buttons: only images can be selected
            DisableButtons();

            // Start the Select_Delete thread: 
            // It will wait until the 'Delete' key or the 'Escape' key pressed and then delete images or abort
            DeleteKeyPressed = false;                      // 'Select_Delete' remains blocked while 'DeleteKeyPressed' and 'EscapeKeyPressed' are false
            EscapeKeyPressed = false;                      // 
            mode = Mode.SelectDelete;                      // block the 'CheckBox_Checked' handler 

            Thread Select_Delete_Thread = new Thread(Select_Delete);
            Select_Delete_Thread.IsBackground = true;
            Select_Delete_Thread.Start();
        }
        private void Select_Delete()
        {
            // Wait until the 'DeleteKeyPressed' or 'EscapeKeyPressed' becomes 'true'
            while (!DeleteKeyPressed & !EscapeKeyPressed) { }

            // if the the 'DeleteKey' was pressed: delete the selected images
            if (!EscapeKeyPressed)
            {
                // Delete the selected images
                for (int i = 0; i < ImgList.Count; i++)
                {
                    if (ImgList[i].ImgSelected == true)
                    {
                        //Delete the image from the ImgList
                        Dispatcher.Invoke(() => { ImgList.Remove(ImgList[i]); });

                        // because the images in the list are shifted to the left
                        // the same index has to be tested again
                        i--;
                    }
                }
            }
            // For all images in the ImgList: deselect the images and hide the checkboxes
            foreach (ImageObj Img in ImgList)
            {
                Img.ImgSelected = false;
                Img.CheckBoxOverlay = Visibility.Visible;
            }
            // reset the DeleteMode (used in the 'CheckBox_Checked' handler)
            mode = Mode.NONE;

            // update the ImageList control
            Dispatcher.Invoke(() =>
            {
                ImageList.ItemsSource = null;
                ImageList.ItemsSource = ImgList;
                EnableButtons();
                Reset_Hints();
                Reset_ButtonForeground();
            });
        }

        // Button + Checkbox handlers and methods for saving full size result
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // prevent any other activities while saveing is in progress          ///????????????????????
            if (_busyIndicator.IsBusy == true) return;

            Reset_ButtonForeground();

            // return if no final image is ready
            if (!FinalImageReady)
            {
                MessageBoxResult result = CustomMessageBox.ShowOK(
                    "There exists no image that can be saved.",
                    "Hint:",
                    "OK",
                    MessageBoxImage.Asterisk);
                return;
            }

            if (!SaveImageChecked & !SaveMaskChecked)
            {
                MessageBoxResult result = CustomMessageBox.ShowOK(
                    "Check at least one of the checkboxes 'Save image' and 'Save mask'",
                    "Hint:",
                    "OK",
                    MessageBoxImage.Asterisk);
                return;
            }
            // FinalImageReady = false;  //???????????????????????

            // Start the background worker for saving the image
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += (o, ea) =>
            {
                SaveFullSizeImage();
            };

            // Setup and start the background worker for the busy indicator
            worker.RunWorkerCompleted += (o, ea) =>
            {
                _busyIndicator.IsBusy = false;
                busyIndicator.Visibility = Visibility.Hidden;
                Reset_Hints();
                //  MaskArea.Children.Clear();    // ???????????????
                //  ImageArea.Children.Clear();  // ??????????????????????????
            };
            busyIndicator.Visibility = Visibility.Visible;
            _busyIndicator.IsBusy = true;
            worker.RunWorkerAsync();
        }
        private void SaveImage_Checkbox_Checked(object sender, RoutedEventArgs e)
        {
            SaveImageChecked = true;
        }
        private void SaveImage_Checkbox_Unchecked(object sender, RoutedEventArgs e)
        {
            SaveImageChecked = false;
        }
        private void SaveMask_Checkbox_Checked(object sender, RoutedEventArgs e)
        {
            SaveMaskChecked = true;
        }
        private void SaveMask_Checkbox_Unchecked(object sender, RoutedEventArgs e)
        {
            SaveMaskChecked = false;
        }
        private void SaveFullSizeImage()
        {
            //show the "SaveMaskCheckboxStackPanel "
            Dispatcher.Invoke(() => { SaveMaskCheckboxStackPanel.Visibility = Visibility.Hidden; });  

            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "JPEG Image (.jpg)|*.jpg|Tiff Image (.tif)|*.tif| All files (*.*)|*.*";
            saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            if (saveFileDialog.ShowDialog() == true)
            {
                WriteableBitmap wbitmap=null;                 
                string filename;

                // save the image
                if (SaveImageChecked)
                {
                    // show hint "saving the resulting image ... " 
                    Dispatcher.Invoke(() => { Reset_Hints(); Set_Hint(1, "  Saving the final image ... ", 200, true, true); });

                    // if no final Result exists, compute it at full size and put it as a into Result_wbitmap
                    // if we have already a result, we will take it directly from Result_wbitmap
                    if (Result_wbitmap == null)
                    {
                        // compute the ´finalImage' at full size
                        ComputeFullSizeImage();
                        Result_wbitmap = ConvertMatToWriteableBitmap(finalImage);
                    }
                    wbitmap = Result_wbitmap;

                    filename = saveFileDialog.FileName;
                    string extension = filename.Substring(filename.Length - 3);

                    if (extension == "jpg")
                    {
                        var jpg_encoder = new JpegBitmapEncoder();
                        jpg_encoder.Frames.Add(BitmapFrame.Create(wbitmap));
                        using (FileStream stream = new FileStream(saveFileDialog.FileName, FileMode.Create))
                            jpg_encoder.Save(stream);
                    }
                    else
                    {
                        var tif_encoder = new TiffBitmapEncoder();
                        tif_encoder.Frames.Add(BitmapFrame.Create(wbitmap));
                        using (FileStream stream = new FileStream(saveFileDialog.FileName, FileMode.Create))
                        {
                            tif_encoder.Save(stream);
                        }
                    }
                }
                // save the mask
                if (SaveMaskChecked)
                {
                    //show hint "saving the mask ... "
                    Dispatcher.Invoke(() => { Reset_Hints(); Set_Hint(1, "  Saving the mask ... ", 200, true, true); });

                    // if no Mask exists, compute it at full size and put it as a writable Bitmap into Mask_wbitmap
                    // if we have already a mask, we will take it directly from Result_wbitmap
                    if (Mask_wbitmap == null)
                    {
                        // Compute the full size 'Mask'(blurred):
                        Mat Mask = new Mat();
                        int kernelsize = (int)KernelSize;

                        if (algorithm == Algorithm.GrabCut)
                            Mask = (GCresultMask | FG_override) & BG_override;
                        else if (algorithm == Algorithm.ConnectedComponents)
                            CCresultMask.CopyTo(Mask);

                        Morph_Dilate(Mask, Mask, kernelsize);
                        Cv2.GaussianBlur(Mask, Mask, new OpenCvSharp.Size(kernelsize, kernelsize), 0, 0, BorderTypes.Reflect);

                        OpenCvSharp.Size FullSize = new OpenCvSharp.Size(finalImage.Width, finalImage.Height);
                        Mask = Mask.Resize(FullSize, 1.0, 1.0, InterpolationFlags.Cubic);
                        Mask_wbitmap = ConvertMatToWriteableBitmap(Mask);
                    }
                    wbitmap = Mask_wbitmap;

                    filename = saveFileDialog.FileName;
                    filename = filename.Remove(filename.Length - 4) + "_mask.jpg";

                    var jpg_encoderMask = new JpegBitmapEncoder();
                    jpg_encoderMask.Frames.Add(BitmapFrame.Create(wbitmap));
                    using (FileStream stream = new FileStream(filename, FileMode.Create))
                        jpg_encoderMask.Save(stream);
                }

                // Stop the current Algorithm
                mode = Mode.NONE;
                algorithm = Algorithm.NONE;
            }
        }
        private void ComputeFullSizeImage()
        {
            int kernelsize = (int)KernelSize;

            // Get the Foreground image:  read full size image from the original file
            FG_image = new Mat(ImgList[FG_Index].FilePath, ImreadModes.Color);

            // Get the Background image: this is the 'finalImage' = result of the 'DefocusStack_Loop' (with reduced size)
            OpenCvSharp.Size FullSize = new OpenCvSharp.Size(FG_image.Width, FG_image.Height);
            BG_image = finalImage.Resize(FullSize, 1.0, 1.0, InterpolationFlags.Lanczos4);

            // Get the mask directly from the GC- resp. CC-algoritm (with reduced size)
            if (algorithm == Algorithm.GrabCut)
                MaskBlurred = (GCresultMask | FG_override) & BG_override;
            else if (algorithm == Algorithm.ConnectedComponents)
                CCresultMask.CopyTo(MaskBlurred);

            // dilate 'MaskBlurred'and blur it
            Morph_Dilate(MaskBlurred, MaskBlurred, kernelsize);
            Cv2.GaussianBlur(MaskBlurred, MaskBlurred, new OpenCvSharp.Size(kernelsize, kernelsize), 0, 0, BorderTypes.Reflect);
            MaskBlurred = MaskBlurred.Resize(FullSize, 1.0, 1.0, InterpolationFlags.Lanczos4);

            // prepare the Mat objects for combining FG_image and BG_image
            Mat A = new Mat();              // FG_Image        CV_8CU3   -->  CV_32FC3
            Mat B = new Mat();              // BG_Image        CV_8CU3   -->  CV_32FC3
            Mat C = new Mat();              // MaskBlurred     CV_8CU1   -->  CV_8CU3 --> CV_32FC3
            Mat D = new Mat();              // all elements    = 255

            Cv2.CvtColor(FG_image, A, ColorConversionCodes.BGRA2BGR);
            A.ConvertTo(A, MatType.CV_32FC3);       // FG_Image as CV_32FC3

            Cv2.CvtColor(BG_image, B, ColorConversionCodes.BGRA2BGR);
            B.ConvertTo(B, MatType.CV_32FC3);       // BG_Image --> CV_32FC3

            Cv2.CvtColor(MaskBlurred, C, ColorConversionCodes.GRAY2BGR);
            C.ConvertTo(C, MatType.CV_32FC3);       // FG_MaskBlurred  --> CV_32FC3

            D = new Mat(MaskBlurred.Height, MaskBlurred.Width, MatType.CV_32FC3, Scalar.All(255));

            // Combine foreground and background using the 'MaskBlurred'
            // finalImage = FG * alpha + BG * (255 - alpha)) / 255
            A = A.Mul(C);
            B = B.Mul(D - C);
            C = null;
            A = (A + B) / D;


            // prepare the final image as CV_8CU4 = BGRA
            A.ConvertTo(A, MatType.CV_8UC3);
            Cv2.CvtColor(A, finalImage, ColorConversionCodes.BGR2BGRA);
        }

        // Button handlers for the DFS algorithms
        private void BtnGrabCut_Click(object sender, RoutedEventArgs e)
        {
            // prevent any other activities while saveing is in progress          ///????????????????????
            if (_busyIndicator.IsBusy == true) return;

            algorithm = Algorithm.GrabCut;
            Initialize_GC_CC();
        }
        private void Btn_ConnectedComponents_Click(object sender, RoutedEventArgs e)
        {
            // prevent any other activities while saveing is in progress          ///????????????????????
            if (_busyIndicator.IsBusy == true) return;

            algorithm = Algorithm.ConnectedComponents;
            Initialize_GC_CC();
        }

        // Button activation / deactivation, Button layout
        private void EnableButtons()
        {
            Dispatcher.Invoke(() =>
            {
                BtnLoad.IsEnabled = true;
                BtnDelete.IsEnabled = true;
                BtnGrabCut.IsEnabled = true;
                Btn_ConnectedComponents.IsEnabled = true;
                BtnSave.IsEnabled = false;  //!!!!!!!!!!!!!!!!!
                BtnLoad.IsEnabled = true;
            });
        }
        private void DisableButtons()
        {
            Dispatcher.Invoke(() =>
            {
                BtnLoad.IsEnabled = false;
                BtnDelete.IsEnabled = false;
                BtnGrabCut.IsEnabled = false;
                Btn_ConnectedComponents.IsEnabled = false;
                BtnSave.IsEnabled = false;
                BtnLoad.IsEnabled = false;
            });
        }
        private void Reset_ButtonForeground()
        {
            SolidColorBrush whiteBrush = new SolidColorBrush();
            whiteBrush.Color = Colors.White;

            Dispatcher.Invoke(() =>
            {
                BtnLoad.Foreground = whiteBrush;
                BtnDelete.Foreground = whiteBrush;
                BtnGrabCut.Foreground = whiteBrush;
                Btn_ConnectedComponents.Foreground = whiteBrush;
                BtnSave.Foreground = whiteBrush;
            });
        }

        // Get the mouse position in coordinates of the rendered Image and of the original Image
        System.Windows.Point GetMousePosition_RenderedImage()
        {
            // coordinates of the last mouse click relative to the 'ImageArea'
            double x_ImageArea = Mouse.GetPosition(ImageArea).X;
            double y_ImageArea = Mouse.GetPosition(ImageArea).Y;

            // actual width, height of the ImageArea
            double ah_ImageArea = (int)ImageArea.ActualHeight;
            double aw_ImageArea = (int)ImageArea.ActualWidth;

            if (x_ImageArea > aw_ImageArea) x_ImageArea = aw_ImageArea;
            if (y_ImageArea > ah_ImageArea) y_ImageArea = ah_ImageArea;

            // Width and height of the rendered image
            double w_renderedImage = ImageArea.Children[0].RenderSize.Width;
            double h_renderedImage = ImageArea.Children[0].RenderSize.Height;

            // mouseposition relative to the rendered image
            double x_RenderedImage = x_ImageArea - (aw_ImageArea - w_renderedImage) / 2;
            double y_RenderedImage = y_ImageArea - (ah_ImageArea - h_renderedImage) / 2;

            return new System.Windows.Point(x_RenderedImage, y_RenderedImage);
        }      
        System.Windows.Point GetMousePosition_OriginalImage()
        {
            System.Windows.Point p_RenderedImage = GetMousePosition_RenderedImage();
            double w_renderedImage = ImageArea.Children[0].RenderSize.Width;
            double h_renderedImage = ImageArea.Children[0].RenderSize.Height;

            double x_Image = p_RenderedImage.X * (ImgWidth / w_renderedImage);
            double y_Image = p_RenderedImage.Y * (ImgHeight / h_renderedImage);

            return new System.Windows.Point(x_Image, y_Image);
        }       

        // Start 'FindBlobs()' when a channel radio button (Gray, R, G, B, H, S, V) is checked 
        private void RB_Checked(object sender, RoutedEventArgs e)
        {
            FindBlobs();
        }
        private void RB_Gray_Checked(object sender, RoutedEventArgs e)
        {
            // rb_Gray is checked by default,
            // but before Window initialization we do nothing!!
            if (rB_GrayActive == true) FindBlobs();
        }

        // Key down handler for control keys 'Delete', 'Escape', 'Enter'
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete) DeleteKeyPressed = true;
            if (e.Key == Key.Escape) EscapeKeyPressed = true;
            if (e.Key == Key.Enter) EnterKeyPressed = true;
        }

        // Slider Handlers for 'SlKernelSize' and 'SlBrushSize'
        private void SlKernelSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (slKernelSize.Value < 3)
            {
                slKernelSize.Value = 3;
                KernelSize = 3;
            }
            else if (slKernelSize.Value % 2 == 0)
                KernelSize = (double)slKernelSize.Value + 1;
            else
                KernelSize = (double)slKernelSize.Value;
        }
        private void SlBrushSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            BrushSize = (double)slBrushSize.Value;
        }
        private void SlDragCompleted(object sender, RoutedEventArgs e)
        {
            if (algorithm == Algorithm.GrabCut) GrabCut();
            if (algorithm == Algorithm.ConnectedComponents) FindBlobs();
        }
        private void thumb_MouseEnter(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && e.MouseDevice.Captured == null)
            {
                // the left button is pressed on mouse enter but the mouse isn't captured, so the thumb 
                // must have been moved under the mouse in response to a click on the track.
                // Generate a MouseLeftButtonDown event.
                MouseButtonEventArgs args = new MouseButtonEventArgs(e.MouseDevice, e.Timestamp, MouseButton.Left);
                args.RoutedEvent = MouseLeftButtonDownEvent;
                (sender as Thumb).RaiseEvent(args);
            }
        }

        // Display the BitmapImage with index 'ImgListIndex' from the 'ImgList' in the grid 'ImageArea'
        public void Display_ImgList_Image()
        {
            // Get Image from the ImgList and display it
            Dispatcher.Invoke(() =>
            {
                // get the BitmapImage to be displayed
                BitmapSource Display_Image = ImgList[ImgListIndex].BMImage;

                // Convert Bitmap into Bgra32-Format: 3 channels: B, G, R and ALPHA channel
                if (Display_Image.Format != PixelFormats.Bgra32)
                    Display_Image = new FormatConvertedBitmap(Display_Image, PixelFormats.Bgra32, null, 0);

                Mat dst = new Mat();
                dst = Display_Image.ToMat();
                DisplayMat(dst, ImageArea);
                //  FinalImageReady = false;   //??????????????
            });
        }

        // Display a Mat image(8UC4) in a Grid
        void DisplayMat(Mat mat, Grid grd)
        {
            WriteableBitmap wbitmap = ConvertMatToWriteableBitmap(mat);

            // Create an Image to display the bitmap.
            System.Windows.Controls.Image myImage = new System.Windows.Controls.Image();
            myImage.Stretch = Stretch.Uniform;
            myImage.Margin = new Thickness(0);
            myImage.Source = wbitmap;

            Dispatcher.Invoke(() =>
            {
                grd.Children.Clear();
                grd.Children.Add(myImage);
            });
        }

        // Convert Mat (8UC4) to WriteableBitmap
        public WriteableBitmap ConvertMatToWriteableBitmap(Mat mat)
        {
            byte[] pixels1d = new byte[mat.Height * mat.Width * 4];

            int index = 0;
            for (int y = 0; y < mat.Height; y++)
            {
                for (int x = 0; x < mat.Width; x++)
                {
                    Vec4b px = mat.Get<Vec4b>(y, x);
                    pixels1d[index] = px.Item0;
                    pixels1d[index + 1] = px.Item1;
                    pixels1d[index + 2] = px.Item2;
                    pixels1d[index + 3] = px.Item3;
                    index = index + 4;
                }
            }
            Int32Rect rect = new Int32Rect(0, 0, mat.Width, mat.Height);
            int stride = 4 * mat.Width;
            WriteableBitmap wbitmap = new WriteableBitmap(mat.Width, mat.Height, 96, 96, PixelFormats.Bgra32, null);
            wbitmap.WritePixels(rect, pixels1d, stride, 0);

            return wbitmap;
        }

        // Morphological filters
        void Morph_Open(Mat src, Mat dst, int kernelsize)
        {
            Mat kernel = Mat.Ones(kernelsize, kernelsize, MatType.CV_8UC1);
            Cv2.Dilate(src, src, kernel);
            Cv2.Erode(src, dst, kernel);
        }
        void Morph_Close(Mat src, Mat dst, int kernelsize)
        {
            Mat kernel = Mat.Ones(kernelsize, kernelsize, MatType.CV_8UC1);
            Cv2.Erode(src, src, kernel);
            Cv2.Dilate(src, dst, kernel);
        }   
        void Morph_Dilate(Mat src, Mat dst, int kernelsize)
        {
            Mat kernel = Mat.Ones(kernelsize, kernelsize, MatType.CV_8UC1);
            Cv2.Dilate(src, dst, kernel);
        }
        void Morph_Erode(Mat src, Mat dst, int kernelsize)
        {
            Mat kernel = Mat.Ones(kernelsize, kernelsize, MatType.CV_8UC1);
            Cv2.Erode(src, dst, kernel);
        }

        // CheckBox-Handler: handles checkbox selection in the ImageList
        // 'Delete' mode:       nothing to do
        // 'SelectStack' mode:  build the 'DefocusList'
        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            // if we are selecting images to be deleted: nothing to do
            if (mode == Mode.SelectDelete) return;

            // if we are selecting images for the image stack:
            if (mode == Mode.SelectStack)
            {
                // if this is the first selected item add it to the 'DefocusList'
                if (StackFGIndex == -1)
                {
                    for (int i = 0; i < ImgList.Count; i++)
                        if (ImgList[i].ImgSelected == true)
                            StackFGIndex = i;
                    DefocusList.Clear();
                    DefocusList.Add(StackFGIndex);
                    ImgListIndex = StackFGIndex;
                    Display_ImgList_Image();
                }
                // for the next selected items we must find their ImgListIndex and add it to the 'DefocusList'
                // we look for selected items which are not yet in the 'DefocusList'
                else
                {
                    for (int i = 0; i < ImgList.Count; i++)
                    {
                        bool newItem = true;
                        if (ImgList[i].ImgSelected == true)
                        {
                            for (int j = 0; j < DefocusList.Count; j++)
                                if ((ImgList[DefocusList[j]].ImgSelected == true) & (i == DefocusList[j])) newItem = false;

                            if (newItem == true)
                            {
                                DefocusList.Add(i);
                                ImgListIndex = i;
                                Display_ImgList_Image();
                                break;
                            }
                        }
                    }
                }
            }
        }
        private void ChkBox_Unchecked(object sender, RoutedEventArgs e)
        {
            for (int j = 0; j < DefocusList.Count; j++)
                if ((ImgList[DefocusList[j]].ImgSelected == false))
                    DefocusList.RemoveAt(j);
        }

        // Mouse handlers for blob selection and polyline drawing
        private void ImageArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // The handler is activated by a mouse click within the area of the rendered image
            // but not by clicks within the ImageArea outside of the rendered image

            // We are not yet ready for drawing the GCstroke polyline
            if (GCpolyLineState == DrawingMode.NONE) return;

            // if GCpolyline doesn't yet exist  but we are ready for drawing it: 
            if ((GCpolyLineState == DrawingMode.NOT_SET) & (algorithm==Algorithm.GrabCut))
            {
                GCpolyLineState = DrawingMode.IN_PROCESS;

                GCpolyline.Visibility = Visibility.Visible;

                // Get the mouse position in pixel coordinates of the rendered image 
                System.Windows.Point p = GetMousePosition_RenderedImage();

                // The rectangle may not use the outmost 5 pixels of the rendered image 
                double w = ImageArea.Children[0].RenderSize.Width;
                double h = ImageArea.Children[0].RenderSize.Height;

                // Define colors for the GrabCut brush tool
                // default and PLUSkey --> blue, MINUSkey --> red
                SolidColorBrush redBrush = new SolidColorBrush();
                redBrush.Color = Colors.Tomato;
                SolidColorBrush LightGreenBrush = new SolidColorBrush();
                LightGreenBrush.Color = Colors.LightGreen;
                GCpolyline.Stroke = LightGreenBrush;
                if (FG_Brush_active)
                    GCpolyline.Stroke = LightGreenBrush;
                else
                    GCpolyline.Stroke = redBrush;

                // save the starting point of the GCpolyLine relative to the rendered image
                GCpolylineStart_Rendered.X = p.X;
                GCpolylineStart_Rendered.Y = p.Y;

                // We position the GCpolyLine using Canvas.SetLeft/Canvas.SetTop
                // This requires coordinates relative to the ImageArea 
                GCpolylineStart_ImageArea.X = p.X + (ImageArea.ActualWidth - ImageArea.Children[0].RenderSize.Width) / 2;
                GCpolylineStart_ImageArea.Y = p.Y + (ImageArea.ActualHeight - ImageArea.Children[0].RenderSize.Height) / 2;
                Canvas.SetLeft(GCpolyline, GCpolylineStart_ImageArea.X);
                Canvas.SetTop(GCpolyline, GCpolylineStart_ImageArea.Y);


                // We define the first 2 points of the polyline
                System.Windows.Point Point0 = new System.Windows.Point(0, 0);
                System.Windows.Point Point1 = new System.Windows.Point(0, 0);
                PointCollection myPointCollection = new PointCollection();
                myPointCollection.Add(Point0);
                myPointCollection.Add(Point1);
                System.Windows.Shapes.Polyline myPolyline = new System.Windows.Shapes.Polyline();
                myPolyline.Points = myPointCollection;
                GCpolyline.Points = myPolyline.Points;

                GCpolyline.Visibility = Visibility.Visible;
                GCpolyline.StrokeThickness = BrushSize;
            }

        }
        private void ImageArea_MouseMove(object sender, MouseEventArgs e)
        {
            // if we are drawing the GCStroke polyline by moving the mouse: update it
            if (GCpolyLineState == DrawingMode.IN_PROCESS)
            {
                // Get the mouse position in pixel coordinates of the rendered image 
                System.Windows.Point p = GetMousePosition_RenderedImage();

                // cordinates of the next polyline point relative to the first point
                System.Windows.Point nextPoint = new System.Windows.Point(p.X - GCpolylineStart_Rendered.X, p.Y - GCpolylineStart_Rendered.Y);

                // add a new polyline point if the distance is > 4
                if ((GCPolyline_PreviousPoint.X - p.X) * (GCPolyline_PreviousPoint.X - p.X)
                     + (GCPolyline_PreviousPoint.Y - p.Y) * (GCPolyline_PreviousPoint.Y - p.Y) > 4 * 4)
                    GCpolyline.Points.Add(nextPoint);
            }
        }
        private void ImageArea_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if ((mode == Mode.CC) & (algorithm == Algorithm.ConnectedComponents))
            {
                if (Keyboard.IsKeyDown(Key.OemPlus)) PLUSkeyPressed = true;
                if (Keyboard.IsKeyDown(Key.OemMinus)) MINUSkeyPressed = true;
                SelectDeselectBlobs(); 
            }
            // on 'MouseLeftButtonUp' we stop drawing the polyline
            if (mode == Mode.GC & GCpolyLineState == DrawingMode.IN_PROCESS)
            {
                GCpolyLineState = DrawingMode.SET;
                GCpolyline.Visibility = Visibility.Hidden;
                Modyfy_GC_Mask();
            }
        }
        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // If we are drawing a polyline 'MouseLeftButtonUp' in the MainWindow area the line is finished 
            if (mode == Mode.GC & GCpolyLineState == DrawingMode.IN_PROCESS)
            {
                GCpolyLineState = DrawingMode.SET;
                GCpolyline.Visibility = Visibility.Hidden;
                Modyfy_GC_Mask();
            }
        }

        // Handlers for changing the brush type
        private void FG_Brush_MouseUp(object sender, MouseButtonEventArgs e)
        {
            FG_Brush_active = true;
            FG_Brush.Source = FG_BrushBitmap;
            BG_Brush.Source = Inactive_BrushBitmap;
        }
        private void BG_Brush_MouseUp(object sender, MouseButtonEventArgs e)
        {
            FG_Brush_active = false;
            FG_Brush.Source = Inactive_BrushBitmap;
            BG_Brush.Source = BG_BrushBitmap;
        }

        // Set, Reset user hints
        private void Reset_Hints()
        {
            SolidColorBrush BGBrush = new SolidColorBrush();
            SolidColorBrush FGBrush = new SolidColorBrush();

            Dispatcher.Invoke(() =>
            {

                BGBrush.Color = Colors.LightCyan;
                FGBrush.Color = Colors.White;

                Hint1.Background = BGBrush;
                Hint1.Foreground = FGBrush;
                Hint1.Text = "";
                Hint1.Width = 280;
                Hint1.Visibility = Visibility.Hidden;
                Hint2.Background = BGBrush;
                Hint2.Foreground = FGBrush;
                Hint2.Text = "";
                Hint2.Width = 280;
                Hint2.Visibility = Visibility.Hidden;
                Hint3.Background = BGBrush;
                Hint3.Foreground = FGBrush;
                Hint3.Text = "";
                Hint3.Width = 280;
                Hint3.Visibility = Visibility.Hidden;
                Hint4.Background = BGBrush;
                Hint4.Foreground = FGBrush;
                Hint4.Text = "";
                Hint4.Width = 280;
                Hint4.Visibility = Visibility.Hidden;
                Hint5.Background = BGBrush;
                Hint5.Foreground = FGBrush;
                Hint5.Text = "5";
                Hint5.Width = 280;
                Hint5.Visibility = Visibility.Hidden;
            });
        }
        private void Set_Hint(int TextBoxNumber, String hintText, int width, bool Highlighted, bool visible)
        {
            SolidColorBrush BGBrush = new SolidColorBrush();
            SolidColorBrush FGBrush = new SolidColorBrush();

            Visibility visibility = new Visibility();
            Dispatcher.Invoke(() =>
            {
                if (visible)
                    visibility = Visibility.Visible;
                else
                    visibility = Visibility.Hidden;

                if (Highlighted)
                {
                    BGBrush.Color = Colors.LightSeaGreen;
                    FGBrush.Color = Colors.White;
                }
                else
                {
                    BGBrush.Color = Colors.White;
                    FGBrush.Color = Colors.Gray;
                }

                if (TextBoxNumber == 1)
                {
                    Hint1.Background = BGBrush;
                    Hint1.Foreground = FGBrush;
                    Hint1.Text = hintText;
                    Hint1.Width = width;
                    Hint1.Visibility = visibility;
                }
                else if (TextBoxNumber == 2)
                {
                    Hint2.Background = BGBrush;
                    Hint2.Foreground = FGBrush;
                    Hint2.Text = hintText;
                    Hint2.Width = width;
                    Hint2.Visibility = visibility;
                }
                else if (TextBoxNumber == 3)
                {
                    Hint3.Background = BGBrush;
                    Hint3.Foreground = FGBrush;
                    Hint3.Text = hintText;
                    Hint3.Width = width;
                    Hint3.Visibility = visibility;
                }
                else if (TextBoxNumber == 4)
                {
                    Hint4.Background = BGBrush;
                    Hint4.Foreground = FGBrush;
                    Hint4.Text = hintText;
                    Hint4.Width = width;
                    Hint1.Width = width;
                    Hint4.Visibility = visibility;
                }
                else if (TextBoxNumber == 5)
                {
                    Hint5.Background = BGBrush;
                    Hint5.Foreground = FGBrush;
                    Hint5.Text = hintText;
                    Hint5.Width = width;
                    Hint5.Visibility = visibility;
                }
            });
        }

        // common methods for 'GrabCut' and 'Connected Components'
        // ========================================================================================
        private void Initialize_GC_CC()             // common initializations for GC and CC 
        {
            // If there exists a 'StackSelection_Thread' from a previous Click on 'BtnGrabCut': kill it
            if (StackSelection_Thread != null) StackSelection_Thread.Abort();

            Reset_Globals();
            Reset_Hints();
            Reset_ButtonForeground();

            // Setup the controls for the CC algorithm
            Dispatcher.Invoke(() =>
            {
                KernelSizeStackPanel.Visibility = Visibility.Hidden;
                BrushSizeStackPanel.Visibility = Visibility.Hidden;
                ColorChannnelsStackPanel.Visibility = Visibility.Hidden;
                SaveMaskCheckboxStackPanel.Visibility = Visibility.Hidden;
            });

            Dispatcher.Invoke(() =>
            {
                ImageArea.Children.Clear();         //??????????????????
                MaskArea.Children.Clear();
            });

            if (algorithm == Algorithm.GrabCut) Dispatcher.Invoke(() => { BtnGrabCut.Foreground = activeBrush; });
            else if (algorithm == Algorithm.ConnectedComponents) Dispatcher.Invoke(() => { Btn_ConnectedComponents.Foreground = activeBrush; });

            Dispatcher.Invoke(() =>
            {
                Set_Hint(1, "  1. Select the foreground image as stack top.", 280, true, true);
                Set_Hint(2, "  2. Select more images increasingly defocused.", 280, true, true);
                Set_Hint(3, "  3. Press 'ENTER' to continue or 'ESCAPE' to abort.", 300, true, true);
            });
            PrepareStackSelection();

            // start the StackSelection_Thread
            StackSelection_Thread = new Thread(ProcessImageStack);
            StackSelection_Thread.SetApartmentState(ApartmentState.STA);
            StackSelection_Thread.IsBackground = true;
            StackSelection_Thread.Start();
        }
        private void PrepareStackSelection()        // Prepare selection of the stack images
        {
            // Deselect all images in the List and show the checkboxes
            foreach (ImageObj Img in ImgList)
            {
                Img.ImgSelected = false;                    // deselect all images
                Img.CheckBoxOverlay = Visibility.Hidden;    // show the checkboxes
                Img.ImgDescr = "";                          // remove the Foreground/Background label
            }
            Dispatcher.Invoke(() =>
            {
                ImageList.ItemsSource = null;
                ImageList.ItemsSource = ImgList;
            });

            // Prepare the 'StackSelection_Thread': it will be stopped by pressing the 'ENTER'  or the 'ESCAPE' key
            EnterKeyPressed = false;
            EscapeKeyPressed = false;
            mode = Mode.SelectStack;
        }
        private void ProcessImageStack()            // Check the images on the Stack, if OK call GrabCut and GC_Loop
        {
            // wait until the 'ENTER' or the 'ESCAPE' key is pressed
            Dispatcher.Invoke(() => { DisableButtons(); });
            while (!EnterKeyPressed & !EscapeKeyPressed) { Thread.Sleep(100); }

            // When we arrive here, 'CheckBox_Checked' handler has built the 'DefocusList'

            // For all images in the ImgList: deselect the images and hide the checkboxes
            foreach (ImageObj Img in ImgList)
            {
                Img.ImgSelected = false;
                Img.CheckBoxOverlay = Visibility.Visible;
            }
            // reset the mode (used in the 'CheckBox_Checked' handler)
            mode = Mode.NONE;

            // update the ImageList control
            Dispatcher.Invoke(() =>
            {
                ImageList.ItemsSource = null;
                ImageList.ItemsSource = ImgList;
                EnableButtons();
            });

            //  if only 0 or 1 images are selected: return
            if ((DefocusList.Count <= 1) & (!EscapeKeyPressed))
            {
                while (MessageBoxResult.OK != CustomMessageBox.ShowOK(
                    "At least 2 images must be selected !! \n",
                    "Hint:",
                    "OK",
                    MessageBoxImage.Asterisk)) { };
            }

            // if the function was canceled or <=1 images selected, reset all
            if (EscapeKeyPressed | DefocusList.Count <= 1)
            {
                StackFGIndex = -1;
                Dispatcher.Invoke(() =>
                {
                    Reset_Globals();
                    Reset_Hints();
                    Reset_ButtonForeground();
                    MaskArea.Children.Clear();
                    ImageArea.Children.Clear();
                });
                return;
            }
            // show hint "working ... " while GrabCut is working
            this.Dispatcher.Invoke(() =>
            {
                Reset_Hints();
                Set_Hint(1, "  Working ... ", 200, true, true);
            });
            Thread.Sleep(100);

            // compute the Foreground mask with 'GrabCut' resp. 'ConnectedComponents'
            if (algorithm == Algorithm.GrabCut)
            {
                Dispatcher.Invoke(() =>
                {
                    GrabCut();
                    Reset_Hints();
                    Set_Hint(1, "  1. Use the 'Kernel size' slider or the brush to optimize the result.", 400, true, true);
                    Set_Hint(2, "  2. Press the 'ENTER' key to continue.", 250, true, true);
                });
            }
            else if (algorithm == Algorithm.ConnectedComponents)
            {
                Dispatcher.Invoke(() =>
                {
                    FindBlobs();
                    Reset_Hints();
                    Set_Hint(1, "  1. Use the 'Kernel size' or select/desect Blobs.", 400, true, true);
                    Set_Hint(2, "  2. Press the 'ENTER' key to continue.", 250, true, true);
                });
            }

            EnterKeyPressed = false;
            while (!EnterKeyPressed) { Thread.Sleep(100); }
            GCpolyLineState = DrawingMode.NONE;
            DefocusStack_Loop();
        }
        private bool PrepareSegmentation()          // Common checks and setup for 'Connected Components' and for 'GrabCut'
        {
            Dispatcher.Invoke(() => { Reset_Hints(); });

            // the original images in the 'DefocusList' must be of equal size!
            for (int j = 1; j < DefocusList.Count; j++)
            {
                if ((ImgList[DefocusList[j]].OriginalHeight != ImgList[DefocusList[0]].OriginalHeight)
                     | (ImgList[DefocusList[j]].OriginalWidth != ImgList[DefocusList[0]].OriginalWidth))
                {
                    MessageBoxResult result = CustomMessageBox.ShowOK(
                                                "Foreground and background images are not of the same size.",
                                                "Hint:",
                                                "OK",
                                                MessageBoxImage.Asterisk);
                    return false;
                }
            }
            // height and width of FG and BG images
            ImgHeight = ImgList[FG_Index].height;
            ImgWidth = ImgList[FG_Index].width;

            // Setup global Mat objects 'FG_image' and 'BG_image'
            FG_image = new Mat(ImgHeight, ImgWidth, MatType.CV_8UC4);     // foregroud image
            BG_image = new Mat(ImgHeight, ImgWidth, MatType.CV_8UC4);     // background image

            // Convert 'FG_image' and 'BG_image' BMI images to Mat CV_8UC4 images
            Dispatcher.Invoke(() =>
            {
                ImgList[FG_Index].BMImage.ToMat(FG_image);
                ImgList[BG_Index].BMImage.ToMat(BG_image);
            });
            return true;
        }

        // Connected Components
        // ========================================================================================
        void FindBlobs()                            // Start the CC algorithm
        {
            // initial call of ConnectedComponents: some checks and initializations are necessary
            if (mode == Mode.NONE)
            {
                FG_Index = DefocusList[0];
                BG_Index = DefocusList[DefocusList.Count - 1];

                // reset the GrabCut data structures
                GC_Reset();

                // Setup the controls for the CC algorithm
                KernelSizeStackPanel.Visibility = Visibility.Visible;
                BrushSizeStackPanel.Visibility = Visibility.Hidden;
                ColorChannnelsStackPanel.Visibility = Visibility.Visible;

                // common tests and setups for 'ConnectedComponents' and 'GrabCut'
                if (!PrepareSegmentation()) return;

                // Now we are ready to perform GrabCut
                mode = Mode.CC;
            }

            // Setup global Mat objects
            FG_channel = new Mat(ImgHeight, ImgWidth, MatType.CV_8UC1);     // 1 channel image: gray image, R, G, B, H, S, V 
            CCresultMask = new Mat(ImgHeight, ImgWidth, MatType.CV_8UC1);           // Blob mask
            MaskBlurred = new Mat(ImgHeight, ImgWidth, MatType.CV_8UC1);    // Blob mask after dilatation and Gauss filtering

            // local Mat objects
            Mat FG_bin = new Mat(ImgHeight, ImgWidth, MatType.CV_16UC1);
            Mat FG_blur = new Mat(ImgHeight, ImgWidth, MatType.CV_16UC1);

            // transform the 'FG_image' into a 1 channel Mat image --> 'FG_channel'
            if (rB_Gray.IsChecked == true)      // Gray channel is the  default channel
                FG_channel = FG_image.CvtColor(ColorConversionCodes.BGRA2GRAY);
            else
            {
                Mat rgb_hsv;
                Mat[] RGB_HSV;

                if ((rB_R.IsChecked | rB_G.IsChecked | rB_B.IsChecked) == true)
                {
                    rgb_hsv = FG_image.CvtColor(ColorConversionCodes.BGRA2RGB);
                    Cv2.Split(rgb_hsv, out RGB_HSV);
                    if (rB_R.IsChecked == true) FG_channel = RGB_HSV[0];   // RGB, red channel
                    else if (rB_G.IsChecked == true) FG_channel = RGB_HSV[1];   // RGB, green channel
                    else if (rB_B.IsChecked == true) FG_channel = RGB_HSV[2];   // RGB, blue channel
                }
                else
                {
                    rgb_hsv = FG_image.CvtColor(ColorConversionCodes.BGR2HSV);
                    Cv2.Split(rgb_hsv, out RGB_HSV);
                    if (rB_H.IsChecked == true) FG_channel = RGB_HSV[0];   // HSV, Hue channel
                    else if (rB_S.IsChecked == true) FG_channel = RGB_HSV[1];   // HSV, Saturatin channel
                    else if (rB_V.IsChecked == true) FG_channel = RGB_HSV[2];   // HSV, value channel
                }
            }

            // Binarize the 'FG_channel' --> FG_bin
            Cv2.Threshold(FG_channel, FG_bin, 0, 255, ThresholdTypes.Otsu);

            // get the list of connected components from 'FG_bin':  --> cc
            cc = Cv2.ConnectedComponentsEx(FG_bin, PixelConnectivity.Connectivity4);

            // test if foreground and background are separated:  then we have > 1 blobs
            if (cc.LabelCount <= 1)
            {
                MessageBoxResult result = CustomMessageBox.ShowOK(
                "Foreground and background cannot be separated.",
                "Hint:",
                "OK",
                MessageBoxImage.Asterisk);
                return;
            }
            // Find the largest blob as candidate for the background:  ---> BG_blob
            ConnectedComponents.Blob BG_blob = new ConnectedComponents.Blob();
            int maxArea = 0;
            foreach (var blob in cc.Blobs)
                if (blob.Area > maxArea)
                {
                    maxArea = blob.Area;
                    BG_blob = blob;
                }

            // Initialize 'Mask':  = for BG-pixels, 255 for all other pixels
            for (int row = 0; row < ImgHeight; row++)
            {
                for (int col = 0; col < ImgWidth; col++)
                {
                    if ((int)cc.Labels.GetValue(row, col) == BG_blob.Label)
                        CCresultMask.Set(row, col, 0);
                    else
                        CCresultMask.Set(row, col, 255);
                }
            }
            // Now the blob mask ist ready: blob selection via +/- and mouse click is allowed
            BlobMaskReady = true;

            // Initialize 'MaskBlurred': dilate and blur 'Mask' with 'kernelsize'
            CCresultMask.CopyTo(MaskBlurred);
            int kernelsize = (int)KernelSize;
            Morph_Dilate(MaskBlurred, MaskBlurred, kernelsize);
            Cv2.GaussianBlur(MaskBlurred, MaskBlurred, new OpenCvSharp.Size(kernelsize, kernelsize), 0, 0, BorderTypes.Reflect);

            //Combine the channels again
            if (cc != null) Combine_FG_BG();

            //Display finalImage in the 'ImageArea'
            DisplayMat(finalImage, ImageArea);
            FinalImageReady = true;
            BtnSave.IsEnabled = true;

            // Display the Mask for combining the channels in the 'MaskArea'
            Mat MaskBlurred_BGRA = new Mat();
            Cv2.CvtColor(MaskBlurred, MaskBlurred_BGRA, ColorConversionCodes.BGR2BGRA);
            DisplayMat(MaskBlurred_BGRA, MaskArea);

            Set_Hint(1, "  1. Change 'kernelsize' or 'color channels' or select/deselect blobs with +/- & click.", 450, true, true);
            Set_Hint(2, "  2. Press the 'ENTER' key to continue.", 250, true, true);
        }
        void SelectDeselectBlobs()                  // Optimizhe CC result be selecting/deselecting Blobs      
        {
            // if 'Mask' is not yet initialized:  return
            if (BlobMaskReady == false) return;

            // Get the (x,y)positon of the last mouseclick in pixel coordinates of the Image Matrix
            System.Windows.Point p = GetMousePosition_OriginalImage();
            int x = (int)p.X;
            int y = (int)p.Y;

            // width, height of the original image
            int h = ImgHeight;
            int w = ImgWidth;

            // show mouse click position as square markers
            #region 
            //// Display the click position as a black sqauare
            //Mat MousePositionMarker = FG_image;
            //for (int row = 0; row < h; row++)
            //    for (int col = 0; col < w; col++)
            //        if (Math.Abs(y - row) < 10 && Math.Abs(x - col) < 10)
            //            MousePositionMarker.Set<byte>(row, col, 255);
            //DisplayTestImage("Click positions", MousePositionMarker);
            #endregion

            int targetBlob = 0;
            double distance = 0;
            distance = cc.Blobs[0].Width * cc.Blobs[0].Width + cc.Blobs[0].Height * cc.Blobs[0].Height;  // greater than any distance to blobs
            foreach (var blob in cc.Blobs)
            {
                if (blob.Rect.Contains(x, y))
                {
                    if ((x - blob.Centroid.X) * (x - blob.Centroid.X) + (y - blob.Centroid.Y) * (y - blob.Centroid.Y) < distance)
                    {
                        distance = (x - blob.Centroid.X) * (x - blob.Centroid.X) + (y - blob.Centroid.Y) * (y - blob.Centroid.Y);
                        targetBlob = blob.Label;
                    }
                }
            }
            // update the mask 
            for (int row = 0; row < ImgHeight; row++)
            {
                for (int col = 0; col < ImgWidth; col++)
                    if ((int)cc.Labels.GetValue(row, col) == targetBlob)
                    {
                        if (PLUSkeyPressed == true)
                            CCresultMask[row, row + 1, col, col + 1].Set<int>(0, 255);
                        else if (MINUSkeyPressed == true)
                            CCresultMask[row, row + 1, col, col + 1].Set<int>(0, 0);
                    }
            }

            MINUSkeyPressed = false;
            PLUSkeyPressed = false;

            CCresultMask.CopyTo(MaskBlurred);
            int kernelsize = (int)KernelSize;
            Morph_Dilate(MaskBlurred, MaskBlurred, kernelsize);
            Cv2.GaussianBlur(MaskBlurred, MaskBlurred, new OpenCvSharp.Size(kernelsize, kernelsize), 0, 0, BorderTypes.Reflect);

            //Combine foreground an background images
            Combine_FG_BG();

            //Display finalImage in the 'ImageArea'
            DisplayMat(finalImage, ImageArea);
            FinalImageReady = true;
            BtnSave.IsEnabled = true;

            // Display the Mask for combining the channels in the 'MaskArea'
            Mat MaskBlurred_BGRA = new Mat();
            Cv2.CvtColor(MaskBlurred, MaskBlurred_BGRA, ColorConversionCodes.BGR2BGRA);
            DisplayMat(MaskBlurred_BGRA, MaskArea);

            // Display Blobs
            //Mat BlobImage = new Mat();
            //cc.RenderBlobs(BlobImage);              // returns 3 Planes !!!
            //foreach (var blob in cc.Blobs.Skip(1))
            //    BlobImage.Rectangle(blob.Rect, Scalar.Red, 2);
            //Cv2.CvtColor(BlobImage, BlobImage, ColorConversionCodes.BGR2BGRA);
            //DisplayMat(BlobImage, MaskArea);

        }

        // GrabCut
        // ========================================================================================
        private void GrabCut()                      // compute the Foreground mask using the GrabCut algorithm     
        {
            // initial call of GrabCut: some checks and initializations are necessary
            if (mode == Mode.NONE)
            {
                FG_Index = DefocusList[0];
                BG_Index = DefocusList[DefocusList.Count - 1];

                // reset the GrabCut data structures
                GC_Reset();

                // common tests and setups for 'ConnectedComponents' and 'GrabCut'
                if (!PrepareSegmentation()) return;

                // Now we are ready to perform GrabCut
                mode = Mode.GC;
            }

            // if KernelSize was changed or a polyline drawn, we arrive directly here because of mode = Mode.GC:

            // convert 'FG_image' (CV_8UC4) into 'image' (CV_8UC3)
            OpenCvSharp.Size size = new OpenCvSharp.Size(FG_image.Width, FG_image.Height);
            Mat FG_image_8UC3 = new Mat(size, MatType.CV_8UC3);
            Cv2.CvtColor(FG_image, FG_image_8UC3, ColorConversionCodes.BGRA2BGR);

            Mat bgModel = new Mat();    // GC BG model (internally used)
            Mat fgModel = new Mat();    // GC FG model (internally used)

            // Preset all pixels of the 'source' mask to 'GC_PR_FGD'
            // i.e. the last 2 bits of each source pixel are '1'
            var source = new Mat(FG_image_8UC3.Size(), MatType.CV_8U, new Scalar(GC_PR_FGD));

            // if 'GrabCutMask' is not yet initialized: start GrabCut in rectangle mode  
            if (!GCinitialized)
            {
                // initialize the GCrectangle:
                GCrect = new OpenCvSharp.Rect(10, 10, FG_image.Cols - 20, FG_image.Rows - 20);

                Cv2.GrabCut(FG_image_8UC3,      // input image
                    GCresult,                   // segmentation result
                    GCrect,                     // rectangle containing foreground 
                    bgModel, fgModel,           // BG- and FG-models
                    1,                          // number of iterations
                    GrabCutModes.InitWithRect); // use rectangle
                GCinitialized = true;

                // setup the override masks for foreground ans Background
                FG_override = new Mat(size, MatType.CV_8UC1, Scalar.All(0));
                BG_override = new Mat(size, MatType.CV_8UC1, Scalar.All(255));
            }

            // The  next GrabCut iterations are performed in mask mode
            Cv2.GrabCut(FG_image_8UC3,      // input image
                GCresult,                   // segmentation result
                GCrect,                     // rectangle containing foreground 
                bgModel, fgModel,           // models
                5,                          // number of iterations
                GrabCutModes.InitWithMask); // use rectangle

            // compare 'GCresult'  with  'source':  
            // Set GCresultMask pixels=255 for all pixels with GCresult = GC_FGd or GC_PR_FGd 
            GCresultMask = new Mat();
            Cv2.Compare(GCresult, source, GCresultMask, CmpTypes.EQ);

            // Initialize 'MaskBlurred': dilate and blur 'Mask' with 'kernelsize'
            // GCresultMask = (GCresultMask | FG_override);
            GCresultMask = (GCresultMask | FG_override) & BG_override;

            GCresultMask.CopyTo(MaskBlurred);
            int kernelsize = (int)KernelSize;
            Morph_Dilate(MaskBlurred, MaskBlurred, kernelsize);
            Cv2.GaussianBlur(MaskBlurred, MaskBlurred, new OpenCvSharp.Size(kernelsize, kernelsize), 0, 0, BorderTypes.Reflect);

            //Combine foreground an background images
            Combine_FG_BG();

            //Display finalImage in the 'ImageArea'
            DisplayMat(finalImage, ImageArea);

            // Display the Mask for combining the channels in the 'MaskArea'
            Mat MaskBlurred_BGRA = new Mat();

            Cv2.CvtColor(GCresultMask, MaskBlurred_BGRA, ColorConversionCodes.BGR2BGRA);
            DisplayMat(MaskBlurred_BGRA, MaskArea);  

            Set_Hint(1, "  1. Use the 'Kernel size' slider or the brush to optimize the result.", 400, true, true);
            Set_Hint(2, "  2. Press the 'ENTER' key to continue.", 250, true, true);

            // Allow drawing a polyline for optimizing the GrabCut mask
            GCpolyLineState = DrawingMode.NOT_SET;
        }
        private void GC_Reset()                     // Reset all parameters for GC
        {
            Dispatcher.Invoke(() =>
            {
                // Disable the 'Save' button
                BtnSave.IsEnabled = false;

                // initialize the FG/BG brushes
                FG_Brush_active = true;
                FG_Brush.Source = FG_BrushBitmap;
                BG_Brush.Source = Inactive_BrushBitmap;

                // initialize the polyline color
                SolidColorBrush LightGreenBrush = new SolidColorBrush();
                LightGreenBrush.Color = Colors.LightGreen;
                GCpolyline.Stroke = LightGreenBrush;

                // Setup the controls for the CC algorithm
                KernelSizeStackPanel.Visibility = Visibility.Visible;
                BrushSizeStackPanel.Visibility = Visibility.Visible;
                ColorChannnelsStackPanel.Visibility = Visibility.Hidden;

                // Reset Polyline parameters
                GCpolyLineState = DrawingMode.NONE;
                GCpolyline.Visibility = Visibility.Hidden;
                GCinitialized = false;
            });
        }
        private void Combine_FG_BG()                // Merge 'FG_Image' and 'BG_image' to the 'finalImage' and display the result     
        {
            Mat A = new Mat();              // FG_Image        CV_8CU3   -->  CV_32FC3
            Mat B = new Mat();              // BG_Image        CV_8CU3   -->  CV_32FC3
            Mat C = new Mat();              // MaskBlurred     CV_8CU1   -->  CV_8CU3 --> CV_32FC3
            Mat D = new Mat();              // all elements = 255

            // clear the 'MaskArea'
            if (mode != Mode.GC)
                Dispatcher.Invoke(() => { MaskArea.Children.Clear(); });

            // prepare the Mat objects
            Cv2.CvtColor(FG_image, A, ColorConversionCodes.BGRA2BGR);
            A.ConvertTo(A, MatType.CV_32FC3);       // FG_Image as CV_32FC3

            Cv2.CvtColor(BG_image, B, ColorConversionCodes.BGRA2BGR);
            B.ConvertTo(B, MatType.CV_32FC3);       // BG_Image --> CV_32FC3

            Cv2.CvtColor(MaskBlurred, C, ColorConversionCodes.GRAY2BGR);
            C.ConvertTo(C, MatType.CV_32FC3);       // FG_MaskBlurred  --> CV_32FC3

            D = new Mat(ImgHeight, ImgWidth, MatType.CV_32FC3, Scalar.All(255));

            // Combine foreground and background image using the 'MaskBlurred'
            // finalImage = FG * alpha + BG * (255 - alpha)) / 255
            A = A.Mul(C);
            B = B.Mul(D - C);
            A = (A + B) / D;

            // prepare the final image as CV_8CU4 = BGRA
            A.ConvertTo(A, MatType.CV_8UC3);
            Cv2.CvtColor(A, finalImage, ColorConversionCodes.BGR2BGRA);
        }
        private void Modyfy_GC_Mask()               // Modify the GrabCut mask when a polyline is terminated and call GrabCut  
        {
            // if 'Mask' is not yet initialized:  return
            if ((GCpolyLineState != DrawingMode.SET)) return;

            // Get the Polyline from GCstroke: coordinates are relative to te starting point
            List<System.Windows.Point> GCpointlist = GCpolyline.Points.ToList();

            // use the pointlist to modify 'GCresult', FG_override and BG_override
            GCpointlist.ForEach(delegate (System.Windows.Point p)
            {
                // transform the 'p'-coordinates into the image coodinates
                p.X = ((p.X + GCpolylineStart_Rendered.X)) * ImgWidth / ImageArea.Children[0].RenderSize.Width;
                p.Y = ((p.Y + GCpolylineStart_Rendered.Y)) * ImgHeight / ImageArea.Children[0].RenderSize.Height;
                int bsize = (int)BrushSize;

                if (!FG_Brush_active) // now we use the BG brush
                {
                    // Modify the GCresult mask and the override masks for FG and BG
                    Cv2.Circle(GCresult,    (int)p.X, (int)p.Y, bsize, GC_PR_BGD, bsize);
                    Cv2.Circle(FG_override, (int)p.X, (int)p.Y, bsize/2, 0, bsize/2);
                    Cv2.Circle(BG_override, (int)p.X, (int)p.Y, bsize/2, 0, bsize/2);
                }
                else if (FG_Brush_active)   // now we use the FG brush
                {
                    // Modify the GCresult mask and the override masks for FG and BG
                    Cv2.Circle(GCresult, (int)p.X, (int)p.Y, bsize, GC_PR_BGD, bsize);
                    Cv2.Circle(FG_override, (int)p.X, (int)p.Y, bsize / 2, 255, bsize / 2);
                    Cv2.Circle(BG_override, (int)p.X, (int)p.Y, bsize / 2, 255, bsize / 2);
                }
            });

            // if we are performing GC
            if (mode == Mode.GC) 
            {
                GCinitialized = true;

                // Setup the controls for the GC algorithm
                KernelSizeStackPanel.Visibility = Visibility.Visible;
                BrushSizeStackPanel.Visibility = Visibility.Visible;
                ColorChannnelsStackPanel.Visibility = Visibility.Hidden;

                GrabCut();
            }
        }
        private void DefocusStack_Loop()            // loop over the images of the GCS stack in phase 2               
        {
            // hide the BrushSize- and Kernelsize-Sliders and the ColorChannels
            Dispatcher.Invoke(() => 
            {
                BrushSizeStackPanel.Visibility = Visibility.Hidden;
                KernelSizeStackPanel.Visibility = Visibility.Hidden;
                ColorChannnelsStackPanel.Visibility = Visibility.Hidden;
            });
            // put the Foreground image into 'finalImage'
            Dispatcher.Invoke(() => { finalImage = ImgList[DefocusList[0]].BMImage.ToMat(); });

            #region 
            // Test: mean and deviation of the stack images
            // --------------------------------------------
            //for (int i = 0; i < DefocusList.Count - 1; i++)
            //{
            //    StdDeviation(i, MaskBlurred, out double mean, out double dev);
            //    Console.WriteLine("i = " + i.ToString() + "     Mean = " + mean.ToString() + "    Standard deviation = " + dev.ToString());
            //}
            #endregion

            int kernelsize = (int)KernelSize;

            // get the (not yet blurred) mask for  'GrabCut' resp. 'FindBlobs'
            Mat MaskBlurred_BGRA = new Mat();
            if (algorithm == Algorithm.GrabCut)
                MaskBlurred = (GCresultMask | FG_override) & BG_override;
            else if (algorithm == Algorithm.ConnectedComponents)
                CCresultMask.CopyTo(MaskBlurred);

            // DFS loop Combine the stack images successively
            int DefocusStackIndex = 1;
            while (DefocusStackIndex < DefocusList.Count)
            {
                // put the next foreground and background images into 'FG_image' and into 'BG_image'
                FG_image = finalImage;              // was set by the preceeding 'Combine_FG_BG' call
                Dispatcher.Invoke(() => { BG_image = ImgList[DefocusList[DefocusStackIndex++]].BMImage.ToMat(); });

                // dilate 'MaskBlurred'  by 'kernelsize' for each loop iteration by 10 pixels and blur it
                Morph_Dilate(MaskBlurred, MaskBlurred, kernelsize);
                Cv2.GaussianBlur(MaskBlurred, MaskBlurred, new OpenCvSharp.Size(kernelsize, kernelsize), 0, 0, BorderTypes.Reflect);

                //Combine foreground an background images, result comes to 'finalImage'
                Combine_FG_BG();

                Dispatcher.Invoke(() =>
                {
                    //Display finalImage in the 'ImageArea'
                    DisplayMat(finalImage, ImageArea);

                    //Display the Mask for combining the channels in the 'MaskArea'
                    Cv2.CvtColor(MaskBlurred, MaskBlurred_BGRA, ColorConversionCodes.BGR2BGRA);
                    DisplayMat(MaskBlurred_BGRA, MaskArea);
                });
            }

            // BG_image = 'finalImage' resulting from the focus stack loop
            BG_image = finalImage;

            // FG_image = the original foreground image
            Dispatcher.Invoke(() => { FG_image = ImgList[DefocusList[0]].BMImage.ToMat(); });

            //Combine the original FG_image and the background image from the DFS loop:

            // get the original mask
            if (algorithm == Algorithm.GrabCut)
                MaskBlurred = (GCresultMask | FG_override) & BG_override;
            else if (algorithm == Algorithm.ConnectedComponents)
                CCresultMask.CopyTo(MaskBlurred);

            // Initialize 'MaskBlurred': blur 'Mask' with 'kernelsize'
            Morph_Dilate(MaskBlurred, MaskBlurred, kernelsize);
            Cv2.GaussianBlur(MaskBlurred, MaskBlurred, new OpenCvSharp.Size(kernelsize, kernelsize), 0, 0, BorderTypes.Reflect);
            Combine_FG_BG();

            Dispatcher.Invoke(() =>
            {
                //Display finalImage in the 'ImageArea'
                DisplayMat(finalImage, ImageArea);
                FinalImageReady = true;
                BtnSave.IsEnabled = true;

                //Display the Mask for combining the channels in the 'MaskArea'
                Cv2.CvtColor(MaskBlurred, MaskBlurred_BGRA, ColorConversionCodes.BGR2BGRA);
                DisplayMat(MaskBlurred_BGRA, MaskArea);
            });
            Dispatcher.Invoke(() => {
                Reset_Hints();
                Set_Hint(1, "  1. Save the result.", 200, true, true);
                SaveMaskCheckboxStackPanel.Visibility = Visibility.Visible;
                SaveImage_Checkbox.IsChecked = true;
                SaveMask_Checkbox.IsChecked = false;
            });
        }

        void DisplayTestImage(string title, Mat img)
        {
            Mat img_display = new Mat();
            Cv2.Resize(img, img_display, new OpenCvSharp.Size(600.0, 400.0), 1.0, 1.0, OpenCvSharp.InterpolationFlags.Lanczos4);
            Cv2.ImShow(title, img_display);
            MessageBox.Show("weiter");
        }
    }
}