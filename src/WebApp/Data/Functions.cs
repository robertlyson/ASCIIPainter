using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using ImageProcessor;
using ImageProcessor.Plugins.Effects;
using Newtonsoft.Json;
using PexelsNet;
using WebApp.Configuration;

namespace WebApp.Data
{    
    public static class Functions
    {
        private static Random _random;
        private static string[] _asciiChars = { "#", "#", "@", "%", "=", "+", "*", ":", "-", ".", " " };
        
        static Functions()
        {
            _random = new Random();
        }
        
        public static async Task<Uri> FindImage(HttpClient httpClient, PexelsClientDetails pexelsClientDetails, string query)
        {
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", pexelsClientDetails.ApiKey);
            var response = await httpClient.GetAsync("http://api.pexels.com/v1/search?query=" +
                                                     Uri.EscapeDataString(query) + "&per_page=" + 80 + "&page=" + 1);
            response.EnsureSuccessStatusCode();
            string message = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<Page>(message);
            
            var max = result.Photos.Count;
            var imageUrl = result.Photos.ElementAtOrDefault(_random.Next(0, max))?.Src.Medium;

            return new Uri(imageUrl);
        }

        public static async Task<Stream> DownloadImage(HttpClient httpClient, Uri imageUrl)
        {
            var response = await httpClient.GetAsync(imageUrl);
            if (response == null || response.StatusCode != HttpStatusCode.OK)
                throw new Exception($"{imageUrl} not found");
            
            return await response.Content.ReadAsStreamAsync();
        }

        public static Bitmap ToAscii(Stream stream)
        {
            using (var factory = new ImageFactory())
            {
                stream.Position = 0;
                factory.Load(stream);

                var processor = new Ascii
                {
                    DynamicParameter = new AsciiParameters
                        {CharacterCount = 5, FontSize = 3}
                };
                var handDrawingProcessor = new Drawing
                {
                    DynamicParameter = new DrawingParameters {FilterType = DrawingParameters.EdgeFilterType.Sharpen}
                };

                var processImage = processor.ProcessImage(factory);
                return new Bitmap(processImage);
            }
        }

        public static byte[] ToByte(Bitmap bitmap)
        {
            using(MemoryStream ms = new MemoryStream())
            {
                bitmap.Save(ms, bitmap.RawFormat);
                return ms.ToArray();
            }
        }
        
        //https://dotnetfiddle.net/neyqDF
        public static string ConvertImageToAsciiArt(Bitmap image, int asciiWidth)
        {
            image = GetReSizedImage(image, asciiWidth);

            return ConvertToAscii(image);
        }
        
        private static Bitmap GetReSizedImage(Bitmap inputBitmap, int asciiWidth)
        {
            int asciiHeight = 0;
            //Calculate the new Height of the image from its width
            asciiHeight = (int) Math.Ceiling((double) inputBitmap.Height*asciiWidth/inputBitmap.Width);

            //Create a new Bitmap and define its resolution
            Bitmap result = new Bitmap(asciiWidth, asciiHeight);
            Graphics g = Graphics.FromImage((Image) result);
            //The interpolation mode produces high quality images 
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(inputBitmap, 0, 0, asciiWidth, asciiHeight);
            g.Dispose();
            return result;
        }
        
        private static string ConvertToAscii(Bitmap image)
        {
            Boolean toggle = false;
            StringBuilder sb = new StringBuilder();

            for (int h = 0; h < image.Height; h++)
            {
                for (int w = 0; w < image.Width; w++)
                {
                    Color pixelColor = image.GetPixel(w, h);
                    //Average out the RGB components to find the Gray Color
                    int red = (pixelColor.R + pixelColor.G + pixelColor.B)/3;
                    int green = (pixelColor.R + pixelColor.G + pixelColor.B)/3;
                    int blue = (pixelColor.R + pixelColor.G + pixelColor.B)/3;
                    Color grayColor = Color.FromArgb(red, green, blue);

                    //Use the toggle flag to minimize height-wise stretch
                    if (!toggle)
                    {
                        int index = (grayColor.R*10)/255;
                        sb.Append(_asciiChars[index]);
                    }
                }

                if (!toggle)
                {
                    sb.Append(Environment.NewLine);
                    toggle = true;
                }
                else
                {
                    toggle = false;
                }
            }

            return sb.ToString();
        }	
    }
}