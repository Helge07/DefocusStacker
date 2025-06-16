using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.IO;
using System.Collections.Generic;

namespace DFM
{
    public class ImageObj
    {
        public int SortIndex { get; set; }         // Sort criteron
        public int indexFlag { get; set; }

        // Image Meta-Information
        public string ImgName { get; set; }        // Image name, not the file name
        public string ImgDescr { get; set; }       // Text describing the image.
        public bool ImgSelected { get; set; }      // Bild wurde in der ImageList selektiert
        public Visibility CheckBoxOverlay { get; set; }

        // File system information
        public string FilePath { get; set; }       // Full path to the image, including filename.extension
        public string FileName { get; set; }       // Image file name including the extension:  p.e. "snowflake.jpg"
        public string FileExtension { get; set; }  // Image file name extension including '.': p.e. ".jpg"
        public long   FileSize { get; set; }       // Image file size in bytes: p.e.  3913517 

        // Image dimensions
        public int height { get; set; }
        public int width  { get; set; }  
        public int size   { get; set; }
        public int stride { get; set; }

        // Bitmap images
        public BitmapImage BMImage  { get; set; }                   // System.Windows.Media.Imaging.BitmapImage:  BitmapImage-Object volle Auflösung
        public ImageSource Miniatur { get; set; }                   // System.Windows.Media.Imaging.BitmapImage:  BitmapImage-Object Miniatur-Vorschaubild

        // Constructor
        public ImageObj(List<ImageObj> ImgList, int index, string file)
        {
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

            FileInfo fi = new FileInfo(file);       // BitmapImage has no FileSize-Information
            FileSize = fi.Length;

            // checkbox state
            ImgSelected = false;
            CheckBoxOverlay = Visibility.Visible;
        }
    }
}
