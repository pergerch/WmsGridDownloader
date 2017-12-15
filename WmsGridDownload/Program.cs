namespace WmsGridDownload
{
	using System;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.Drawing;
	using System.Drawing.Imaging;
	using System.IO;
	using System.Linq;
	using System.Net;
	using System.Net.Http;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using System.Web;

	public class Program
	{
		private static long tilesDownloaded = 0;

		private static long tilesSkipped = 0;

		private static long total;

		private static double Progress
		{
			get
			{
				if (total == 0)
					return 0;
				return (double)(tilesDownloaded + tilesSkipped) / total * 100;
			}
		}

		public static void Main(string[] args)
		{
			if (!Directory.Exists(Settings.OutputPath))
			{
				throw new DirectoryNotFoundException("Output directory not found.");
			}

			List<double> cols = new List<double>();
			for (double i = Settings.Extent.MinX; i < Settings.Extent.MaxX; i += Settings.Grid.GridSizeX)
			{
				cols.Add(i);
			}

			List<double> rows = new List<double>();
			for (double i = Settings.Extent.MinY; i < Settings.Extent.MaxY; i += Settings.Grid.GridSizeY)
			{
				rows.Add(i);
			}

			total = cols.Count * rows.Count;

			Console.WriteLine("Loading images...");
			Parallel.ForEach(cols, new ParallelOptions { MaxDegreeOfParallelism = Settings.DegreeOfParallelism }, x =>
			{
				foreach (double y in rows)
				{
					string filename = Path.Combine(Settings.OutputPath, $"img_{x}_{y}");

					string imgfileName = filename;
					switch (Settings.Wms.ImageFormat)
					{
						case ImageEnum.Jpg:
							imgfileName += ".jpg";
							break;
						case ImageEnum.Png:
							imgfileName += ".png";
							break;
					}

					if (!File.Exists(imgfileName))
					{
						bool result = Download(x, y, imgfileName);

						if (result)
						{
							string worldfileName = filename;
							switch (Settings.Wms.ImageFormat)
							{
								case ImageEnum.Jpg:
									worldfileName += ".jgw";
									break;
								case ImageEnum.Png:
									worldfileName += ".pgw";
									break;
							}

							WriteWorldfile(x, y, worldfileName);
							Interlocked.Increment(ref tilesDownloaded);
						}
						else
						{
							Interlocked.Increment(ref tilesSkipped);
						}
					}

					DisplayProgress();
				}
			});

			Console.WriteLine();
			Console.WriteLine("Download finished. Press a key to continue...");
			Console.ReadKey();
		}

		private static Bitmap ChangeColor(Bitmap scrBitmap)
		{
			Color actualColor = Settings.ImageColors.FromColor;
			Color newColor = Settings.ImageColors.ToColor;

			Bitmap newBitmap = new Bitmap(scrBitmap.Width, scrBitmap.Height);
			for (int i = 0; i < scrBitmap.Width; i++)
			{
				for (int j = 0; j < scrBitmap.Height; j++)
				{
					Color pixel = scrBitmap.GetPixel(i, j);

					if (actualColor.R == pixel.R && actualColor.G == pixel.G && actualColor.B == pixel.B)
					{
						newBitmap.SetPixel(i, j, newColor);
					}
					else
					{
						newBitmap.SetPixel(i, j, pixel);
					}
				}
			}

			return newBitmap;
		}

		private static void DisplayProgress()
		{
			Console.Write(
				$"\r{tilesDownloaded} downloaded, {tilesSkipped} skipped, {total} total. Progress: {Progress:0.00} %");
		}

		private static bool Download(double x, double y, string filename)
		{
			HttpClient client = new HttpClient();
			string url = GetWmsUrl(x, y);

			HttpResponseMessage response = client.GetAsync(url).Result;

			if (response.StatusCode != HttpStatusCode.InternalServerError &&
				response.Content.Headers.ContentType.MediaType != "application/vnd.ogc.se_xml")
			{
				Stream result = response.Content.ReadAsStreamAsync().Result;
				Image image = Image.FromStream(result);

				if (Settings.ImageColors.CorrectionNeeded)
				{
					image = ChangeColor((Bitmap)image);
				}

				switch (Settings.Wms.ImageFormat)
				{
					case ImageEnum.Jpg:
						image.Save(filename, ImageFormat.Jpeg);
						break;
					case ImageEnum.Png:
						image.Save(filename, ImageFormat.Png);
						break;
				}

				return true;
			}

			return false;
		}

		private static string GetWmsUrl(double x, double y)
		{
			NameValueCollection parameters = new NameValueCollection
			{
				{ "service", "WMS" },
				{ "request", "GetMap" },
				{ "layers", Settings.Wms.Layers },
				{ "styles", Settings.Wms.Styles },
				{ "height", Settings.Grid.Height.ToString() },
				{ "width", Settings.Grid.Width.ToString() }
			};

			switch (Settings.Wms.ImageFormat)
			{
				case ImageEnum.Jpg:
					parameters.Add("format", "image/jpeg");
					break;
				case ImageEnum.Png:
					parameters.Add("format", "image/png");
					parameters.Add("transparent", Settings.Wms.Transparency);
					break;
				default:
					throw new ArgumentException("Image Format invalid.");
			}

			switch (Settings.Wms.Version)
			{
				case "1.1.1":
					parameters.Add("version", "1.1.1");
					parameters.Add("srs", Settings.Wms.ReferenceSystem);
					parameters.Add("bbox", $"{x},{y},{x + Settings.Grid.GridSizeX},{y + Settings.Grid.GridSizeY}");
					break;
				case "1.3.0":
					parameters.Add("version", "1.3.0");
					parameters.Add("crs", Settings.Wms.ReferenceSystem);
					parameters.Add("bbox", $"{y},{x},{y + Settings.Grid.GridSizeY},{x + Settings.Grid.GridSizeX}");
					break;
				default:
					throw new ArgumentException("WMS Version invalid.");
			}

			string baseUrl = Settings.Wms.BaseURL;
			string queryString = string.Join("&",
				(from key in parameters.AllKeys
					from value in parameters.GetValues(key)
					select string.Format("{0}={1}", HttpUtility.UrlEncode(key), HttpUtility.UrlEncode(value))).ToArray());

			return baseUrl + queryString;
		}

		private static void WriteWorldfile(double x, double y, string filename)
		{
			string[] lines =
			{
				(Settings.Grid.GridSizeX / Settings.Grid.Width).ToString(), "0", "0",
				(-1.0 * Settings.Grid.GridSizeY / Settings.Grid.Height).ToString(),
				(x + Settings.Grid.GridSizeX / 2.0 / Settings.Grid.Width).ToString(),
				(y + Settings.Grid.GridSizeY - Settings.Grid.GridSizeY / 2 / Settings.Grid.Height).ToString()
			};

			if (File.Exists(filename))
			{
				File.Delete(filename);
			}

			File.WriteAllLines(filename, lines, Encoding.ASCII);
		}
	}
}