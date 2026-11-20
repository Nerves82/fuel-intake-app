using FuelIntake.Core.Packets;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging.Abstractions;

namespace FuelIntake.UI.Printing;

/// <summary>Turns the chosen forms into one self-contained HTML document, ready to be printed to PDF.</summary>
public sealed class PacketRenderer(IServiceProvider services)
{
    public async Task<string> RenderAsync(PrintContext context, IReadOnlyList<FormId> forms, string title)
    {
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<PacketDocument>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(PacketDocument.P)] = context,
                [nameof(PacketDocument.Forms)] = forms,
                [nameof(PacketDocument.Title)] = title,
            }));
            return output.ToHtmlString();
        });
    }
}
