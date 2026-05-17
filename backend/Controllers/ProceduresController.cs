using Microsoft.AspNetCore.Mvc;
using NexusBackend.Services;

namespace NexusBackend.Controllers;

[ApiController]
[Route("api/procedures")]
public class ProceduresController : ControllerBase
{
    private readonly DocumentIndexService _index;

    public ProceduresController(DocumentIndexService index)
    {
        _index = index;
    }

    [HttpGet]
    public IActionResult Get()
    {
        var items = _index.BuildIndex()
            .Where(IsProcedureCandidate)
            .Select(item => new
            {
                item.Path,
                item.Title,
                item.Type,
                item.Status,
                item.Tags,
                item.ModifiedAt,
                item.Size,
                item.Preview,
                category = Categorize(item),
                standardized = item.Path.Replace('\\', '/').StartsWith("06_Documents/Procedimentos-Padronizados/", StringComparison.OrdinalIgnoreCase),
                pendingReview = item.Status.Equals("revisar", StringComparison.OrdinalIgnoreCase)
            })
            .OrderByDescending(item => item.standardized)
            .ThenBy(item => item.category)
            .ThenBy(item => item.Title)
            .ToList();

        return Ok(new
        {
            total = items.Count,
            standardized = items.Count(item => item.standardized),
            pendingReview = items.Count(item => item.pendingReview),
            items
        });
    }

    private static bool IsProcedureCandidate(DocumentIndexItem item)
    {
        var path = item.Path.Replace('\\', '/');

        return item.Type.Equals("procedimento", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("Procedimentos-Padronizados", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("Base-de-Conhecimento", StringComparison.OrdinalIgnoreCase);
    }

    private static string Categorize(DocumentIndexItem item)
    {
        var text = $"{item.Path} {item.Title} {string.Join(" ", item.Tags)}".ToLowerInvariant();

        if (text.Contains("limpeza") || text.Contains("journal") || text.Contains("dados"))
            return "Limpeza";

        if (text.Contains("checklist"))
            return "Checklists";

        if (text.Contains("rede") || text.Contains("network") || text.Contains("firewall"))
            return "Rede";

        if (text.Contains("shift-bi") || text.Contains("qvd") || text.Contains("bi"))
            return "Shift BI";

        if (text.Contains("shift-lis") || text.Contains("lis"))
            return "Shift LIS";

        if (text.Contains("servidor") || text.Contains("linux") || text.Contains("windows"))
            return "Servidores";

        if (text.Contains("banco") || text.Contains("sql") || text.Contains("iris") || text.Contains("cache") || text.Contains("purg"))
            return "Banco de Dados";

        return "Todos";
    }
}
