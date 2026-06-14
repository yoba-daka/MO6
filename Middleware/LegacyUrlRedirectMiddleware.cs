using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Threading.Tasks;
using Examine;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Web.Common;
using Umbraco.Cms.Core.Web;
using System.Net;

public class LegacyUrlRedirectMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IExamineManager _examineManager;
    private readonly IUmbracoContextFactory _umbracoContextFactory;

    public LegacyUrlRedirectMiddleware(RequestDelegate next, IExamineManager examineManager, IUmbracoContextFactory umbracoContextFactory)
    {
        _next = next;
        _examineManager = examineManager;
        _umbracoContextFactory = umbracoContextFactory;
    }
    public static string EncodeUrlSegments(string relativeUrl)
    {
        var uri = new Uri(relativeUrl, UriKind.RelativeOrAbsolute);

        if (uri.IsAbsoluteUri)
        {
            return WebUtility.UrlEncode(relativeUrl);
        }

        var segments = relativeUrl.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segments.Length; i++)
        {
            segments[i] = Uri.EscapeDataString(segments[i]);
        }

        return string.Join("/", segments);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        //הפנייה קבלה
        if (context.Request.Path.Equals("/kabala", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Redirect("/", permanent: true);
            return;
        }

        var categoryId = context.Request.Query["categoryId"].ToString();
        var itemId = context.Request.Query["itemId"].ToString();

        if (!string.IsNullOrEmpty(categoryId) && !string.IsNullOrEmpty(itemId))
        {
            var oldUrlParam = $"categoryId={categoryId}&itemId={itemId}";

            IPublishedContent? matchingContent = null;
            if (_examineManager.TryGetIndex("ExternalIndex", out IIndex? index))
            {
                var results = index
                                .Searcher
                                .CreateQuery("content")
                                .NodeTypeAlias("article")
                                .And()
                                .Field("oldUrlParams", oldUrlParam)
                                .Execute();

                var contentIdString = results.Select(x => x.Id).FirstOrDefault();
                if (!string.IsNullOrEmpty(contentIdString) && int.TryParse(contentIdString, out int contentId))
                {
                    using (var umbContext = _umbracoContextFactory.EnsureUmbracoContext())
                    {
                        var umbracoHelper = umbContext.UmbracoContext.Content;
                        matchingContent = umbracoHelper.GetById(contentId);
                    }
                }
            }

            if (matchingContent != null)
            {
                string redirectUrl;
                using (var umbContext = _umbracoContextFactory.EnsureUmbracoContext())
                {
                    redirectUrl = EncodeUrlSegments(matchingContent.Url());
                }
                context.Response.Redirect(redirectUrl, permanent: true);
                return;
            }
        }

        await _next(context);
    }
}