using System.Text;
using System.Text.Json;
using Diginsight.SmartDocs.Web.Shared.Navigation;

namespace Diginsight.SmartDocs.Web.Navigation;

public sealed class NavigationBootstrapMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, NavigationBootstrapState bootstrap)
    {
        if (!HttpMethods.IsGet(context.Request.Method))
        {
            await next(context);
            return;
        }

        Stream originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await next(context);

            buffer.Position = 0;
            if (context.Response.StatusCode == StatusCodes.Status200OK &&
                context.Response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true)
            {
                string html = await new StreamReader(buffer, Encoding.UTF8).ReadToEndAsync();
                string payload = JsonSerializer.Serialize(bootstrap);
                string script =
                    $"<script id=\"smartdocs-bootstrap\" type=\"application/json\">{payload}</script>";
                int bodyEnd = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
                html = bodyEnd >= 0 ? html.Insert(bodyEnd, script) : html + script;

                byte[] bytes = Encoding.UTF8.GetBytes(html);
                context.Response.ContentLength = bytes.Length;
                await originalBody.WriteAsync(bytes, context.RequestAborted);
                return;
            }

            context.Response.ContentLength = buffer.Length;
            await buffer.CopyToAsync(originalBody, context.RequestAborted);
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }
}
