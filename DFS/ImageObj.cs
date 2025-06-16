using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.IO;
using System.Collections.Generic;

namespace DFS
{
    public class ImageObj
    {
        public int SortIndex { get; set; }              // Sort criteron
        public int indexFlag { get; set; }

        // Image Meta-Information
        public string ImgName { get; set; }             // Image name, not the file name
        public string ImgDescr { get; set; }            // Text describing the image.
        public bool ImgSelected { get; set; }           // Bild wurde in der ImageList selektiert
        public Visibility CheckBoxOverlay { get; set; }

        // File system information
        public string FilePath { get; set; }            // Full path to the image, including filename.extension
        public string FileName { get; set; }            // Image file name including the extension:  p.e. "snowflake.jpg"
        public string FileExtension { get; set; }       // Image file name extension including '.': p.e. ".jpg"
        public long   FileSize { get; set; }            // Image file size in bytes: p.e.  3913517 

        // Image dimensions of the loaded BMImage
        public int height { get; set; }                 // height of the BMImage
        public int width  { get; set; }                 // width  of the BMImage = DecodePixelWidth = 600       
        public int size   { get; set; }                 // size  of the BMImage 
        public int stride { get; set; }                 // stride  of the BMImage 

        // Image dimensions of the original image in the file
        public int OriginalHeight { get; set; }         // width  of the original image in the file
        public int OriginalWidth { get; set; }          // width  of the original image in the file
        public int OriginalSize { get; set; }           // width  of the original image in the file
        public int OriginalStride { get; set; }         // width  of the original image in the file


        // Bitmap images
        public BitmapImage BMImage  { get; set; }       // System.Windows.Media.Imaging.BitmapImage:  BitmapImage-Object volle Auflösung
        public ImageSource Miniatur { get; set; }       // System.Windows.Media.Imaging.BitmapImage:  BitmapImage-Object Miniatur-Vorschaubild

        // Constructor
        public ImageObj(List<ImageObj> ImgList, int index, string file)
        {
            BitmapImage BMItmp;  // used to get the height and width of the image

            SortIndex = index;
            indexFlag = 0;

            FilePath = file;
            FileName = Path.GetFileName(file);
            FileExtension = Path.GetExtension(file);

            BitmapImage BMminiatur = new BitmapImage();
            BMminiatur = new BitmapImage();
            BMminiatur.BeginInit();
            BMminiatur.CacheOption = BitmapCacheOption.OnLoad;
            BMminiatur.DecodePixelWidth = 95;
            BMminiatur.UriSource = new Uri(file, UriKind.Absolute);
            BMminiatur.EndInit();

            Miniatur = BMminiatur;

            BMImage = new BitmapImage();
            BMImage.BeginInit();
            BMImage.CacheOption = BitmapCacheOption.OnLoad;
            BMImage.DecodePixelWidth = 600;
            BMImage.UriSource = new Uri(file, UriKind.Absolute);
            BMImage.EndInit();
            width  = BMImage.PixelWidth;
            height = BMImage.PixelHeight;
            stride = width * 4;
            size = height * stride;

            BMItmp = new BitmapImage();
            BMItmp.BeginInit();
            BMItmp.CacheOption = BitmapCacheOption.None;
            BMItmp.UriSource = new Uri(file, UriKind.Absolute);
            BMItmp.EndInit();
            OriginalWidth  = BMItmp.PixelWidth;
            OriginalHeight = BMItmp.PixelHeight;
            OriginalStride = OriginalWidth * 4;
            OriginalSize   = OriginalHeight * OriginalStride;

            FileInfo fi = new FileInfo(file);           // BitmapImage has no FileSize-Information
            FileSize = fi.Length;

            // checkbox state
            ImgSelected = false;
            CheckBoxOverlay = Visibility.Visible;
        }
    }
}
