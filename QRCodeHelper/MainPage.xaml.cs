using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.System;
using ZXing;
using ZXing.Common;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Storage.Pickers;
using Windows.Graphics.Imaging;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using ZXing.QrCode;
using System.Text;


// https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x804 上介绍了“空白页”项模板

namespace QRCodeHelper
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {

        private const ushort DEFAULT_SIZE = 320;

        private BarcodeWriter qrWriter = new BarcodeWriter()
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new QrCodeEncodingOptions
            {
                Height = DEFAULT_SIZE,
                Width = DEFAULT_SIZE,
                CharacterSet = "utf-8"
            }
        };
        private BarcodeReader qrReader = new BarcodeReader()
        {
            Options = new DecodingOptions
            {
                TryHarder = true,
                PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE },
                CharacterSet = "utf-8"
            }
        };
        private WriteableBitmap qrBitmap;

        public MainPage()
        {
            this.InitializeComponent();
            this.UpdateQRCodeImage();
        }

        private void QRTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateQRCodeImage();
        }

        private void UpdateQRCodeImage()
        {
            if (!String.IsNullOrEmpty(QRTextBox.Text))
            {
                qrBitmap = qrWriter.Write(QRTextBox.Text);
                QRCodeImage.Stretch = Stretch.None;
                QRCodeImage.Source = qrBitmap;
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {

            FileSavePicker picker = new FileSavePicker();
            picker.FileTypeChoices.Add("PNG File", new List<string>() { ".png" });
            picker.FileTypeChoices.Add("JPG File", new List<string>() { ".jpg", ".jpeg" });
            StorageFile savefile = await picker.PickSaveFileAsync();
            if (savefile == null)
            {
                return;
            }
            using (IRandomAccessStream stream = await savefile.OpenAsync(FileAccessMode.ReadWrite))
            using (Stream pixelStream = qrBitmap.PixelBuffer.AsStream())
            {
                Guid encoderId = BitmapEncoder.PngEncoderId;
                switch (savefile.FileType)
                {
                    case ".png":
                        // encoderId = BitmapEncoder.PngEncoderId;
                        break;
                    case ".jpg":
                    case ".jpeg":
                        encoderId = BitmapEncoder.JpegEncoderId;
                        break;
                }

                var encoder = await BitmapEncoder.CreateAsync(encoderId, stream);

                byte[] pixels = new byte[pixelStream.Length];
                await pixelStream.ReadAsync(pixels, 0, pixels.Length);
                encoder.SetPixelData(
                    BitmapPixelFormat.Bgra8, 
                    BitmapAlphaMode.Ignore,
                    (uint)qrBitmap.PixelWidth,
                    (uint)qrBitmap.PixelHeight,
                    96.0,
                    96.0,
                    pixels);
                await encoder.FlushAsync();
            }
        }

        private void SizeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!ushort.TryParse(SizeTextBox.Text, out ushort size))
            {
                size = DEFAULT_SIZE;
            }
            qrWriter.Options.Width = size;
            qrWriter.Options.Height = size;
            UpdateQRCodeImage();
        }

        private async void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker picker = new FileOpenPicker()
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.PicturesLibrary
            };
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".png");
            var file = await picker.PickSingleFileAsync();
            await LoadImageAndDecode(file);
        }

        private async Task LoadImageAndDecode(IStorageFile file)
        {
            if (file == null)
            {
                return;
            }
            using (var stream = await file.OpenReadAsync())
            {
                var bitmapImage = new BitmapImage();
                await bitmapImage.SetSourceAsync(stream);

                qrBitmap = new WriteableBitmap(bitmapImage.PixelWidth, bitmapImage.PixelHeight);
                stream.Seek(0);
                await qrBitmap.SetSourceAsync(stream);

                var result = qrReader.Decode(qrBitmap);

                QRCodeImage.Source = qrBitmap;
                QRCodeImage.Stretch = Stretch.Uniform;

                if (result != null)
                {
                    QRTextBox.TextChanged -= QRTextBox_TextChanged;
                    QRTextBox.Text = result.Text;
                    QRTextBox.TextChanged += QRTextBox_TextChanged;
                }
                else
                {
                    var dialog = new MessageDialog("Unable to detect QRCode.");
                    await dialog.ShowAsync();
                }
            }
        }

        private async void Grid_Drop(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                if (items[0] is IStorageFile file)
                {
                    await LoadImageAndDecode(file);
                }
            }
        }

        private void Grid_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.IsCaptionVisible = false;
            e.DragUIOverride.IsGlyphVisible = false;
        }
    }
}
