using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using MyProject12.Services;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text.Encodings.Web;
using Umbraco.Cms.Core.PropertyEditors.ValueConverters;
using Umbraco.Extensions;

namespace MosheSharon
{
    public static class OptimizedCropUrlExtensions
    {
        public static string GetOptimizedCropUrl(
            this IUrlHelper urlHelper,
            ImageCropperValue image,
            string cropAlias,
            int quality = OptimizedImageUrl.DefaultQuality)
        {
            if (image == null)
            {
                return string.Empty;
            }

            var cropUrl = HtmlContentToString(urlHelper.GetCropUrl(image, cropAlias));
            return OptimizedImageUrl.ApplyWebpDefaults(cropUrl, quality);
        }

        public static string GetOptimizedImageUrl(
            this IUrlHelper urlHelper,
            string imageUrl,
            int quality = OptimizedImageUrl.DefaultQuality)
        {
            return OptimizedImageUrl.ApplyWebpDefaults(imageUrl, quality);
        }

        private static string HtmlContentToString(IHtmlContent htmlContent)
        {
            if (htmlContent == null)
            {
                return string.Empty;
            }

            using var writer = new StringWriter(CultureInfo.InvariantCulture);
            htmlContent.WriteTo(writer, HtmlEncoder.Default);
            return WebUtility.HtmlDecode(writer.ToString());
        }
    }
}
