namespace WmsGridDownload
{
	using System.Drawing;

	public static class Settings
	{
		// Maximum number of parallel threads
		public static int DegreeOfParallelism = 8;

		// Directory must exist and be writable
		public static string OutputPath = @"c:\temp\wms\";

		// Extent of the WMS layer that should be captured
		// Note: lower boundaries inclusive, upper boundaries exclusive
		public static class Extent
		{
			public static double MaxX = 180;

			public static double MaxY = 83;

			public static double MinX = 19;

			public static double MinY = 40;
		}

		// Extent and pixel size of each tile that shall be downloaded
		public static class Grid
		{
			public static double GridSizeX = 1;

			public static double GridSizeY = 1;

			public static int Height = 3000;

			public static int Width = 3000;
		}

		public static class ImageColors
		{
			// Any color correction necessary (e.g. make black pixels white)
			public static bool CorrectionNeeded = true;

			public static Color FromColor = Color.Black;

			public static Color ToColor = Color.White;
		}

		public static class Wms
		{
			public static string BaseURL = "http://geoportal.roslesinforg.ru:8080/proxy/service?";

			public static ImageEnum ImageFormat = ImageEnum.Png;

			// Comma separated list of layer names
			public static string Layers = "shape_forestries";

			public static string ReferenceSystem = "EPSG:4326";

			public static string Styles = "";

			// True or False, only applies for ImageFormat Png
			public static string Transparency = "true";

			// 1.1.1 or 1.3.0
			public static string Version = "1.1.1";
		}
	}
}