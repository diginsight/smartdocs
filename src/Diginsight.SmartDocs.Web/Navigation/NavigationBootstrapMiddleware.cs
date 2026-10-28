using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diginsight.SmartDocs.Web.Shared.Navigation;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Components.Endpoints;

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
            // The prerendered page is the reader's own, so it may carry their display name and the
            // sign-out form's antiforgery token; the client's sign-in menu reads both after
            // hydration, because this host doesn't emit the framework's persisted state.
            bootstrap.Reader = ToBootstrapReader(context);

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
                // The page and its bootstrap state are filtered for this request's reader, so neither
                // a shared cache nor the next person on the same browser may reuse them.
                context.Response.Headers.CacheControl = "private, no-store";
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

    private static BootstrapReader? ToBootstrapReader(HttpContext context)
    {
        ClaimsIdentity? identity = context.User.Identities.FirstOrDefault(static i => i.IsAuthenticated);
        if (identity is null)
        {
            return null;
        }

        string? name = identity.Name ?? identity.FindFirst("preferred_username")?.Value;
        var reader = new BootstrapReader { Name = string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim() };

        // Only a page renders the sign-out form, so only a page request issues a token and the
        // antiforgery cookie that pairs with it.
        if (context.GetEndpoint()?.Metadata.GetMetadata<ComponentTypeMetadata>() is not null)
        {
            AntiforgeryTokenSet tokens = context.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(context);
            reader.SignOutFormField = tokens.FormFieldName;
            reader.SignOutToken = tokens.RequestToken ?? string.Empty;
        }

        return reader;
    }
}
