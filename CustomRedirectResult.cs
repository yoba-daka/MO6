namespace MO6
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Extensions;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Primitives;
    using System;
    using System.Threading.Tasks;

    public class CustomRedirectResult : IActionResult
    {
        public string Url { get; private set; }
        public bool Permanent { get; }

        // Overloaded constructor: Accepts an optional URL and a permanence flag
        public CustomRedirectResult(string url = null, bool permanent = false)
        {
            Url = url; // URL will be set later if null
            Permanent = permanent;
        }

        public async Task ExecuteResultAsync(ActionContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (string.IsNullOrEmpty(Url))
            {
                // Fetch the current URL if no URL was provided
                Url = context.HttpContext.Request.GetDisplayUrl();
            }

            var response = context.HttpContext.Response;
            response.StatusCode = Permanent ? StatusCodes.Status301MovedPermanently : StatusCodes.Status302Found;

            // Ensure the URL is absolute
            var uri = new Uri(Url, UriKind.RelativeOrAbsolute);
            if (!uri.IsAbsoluteUri)
            {
                uri = new Uri(new Uri(context.HttpContext.Request.GetDisplayUrl()), uri);
            }

            // Encode the URL for the Location header
            var encodedUrl = Uri.EscapeUriString(uri.ToString());
            response.Headers.Location = encodedUrl;

            await Task.CompletedTask;
        }
    }
}
