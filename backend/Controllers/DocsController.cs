using Microsoft.AspNetCore.Mvc;
using NexusBackend.Services;

namespace NexusBackend.Controllers;

[ApiController]
[Route("api/docs")]
public class DocsController : ControllerBase
{
    private readonly ObsidianService _obsidian;
    private readonly OllamaService _ollama;
    private readonly ClaudeService _claude;
    private readonly OperationService _operation;

    public DocsController(ObsidianService obsidian, OllamaService ollama, ClaudeService claude, OperationService operation)
    {
        _obsidian = obsidian;
        _ollama = ollama;
        _claude = claude;
        _operation = operation;
    }

    private static bool ShouldUseClaude(string envVar)
    {
        return (Environment.GetEnvironmentVariable(envVar) ?? "false")
            .Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    [HttpGet("search")]
    public IActionResult Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { error = "Informe uma busca em ?q=" });

        var results = _obsidian.SearchMarkdownDetailed(q)
            .Take(10)
            .Select(result => new
            {
                path = result.Path,
                title = result.Title,
                folder = result.Folder,
                score = result.Score,
                snippet = result.Snippet,
                isPrioritySource = result.IsPrioritySource
            })
            .ToList();

        var best = results.FirstOrDefault()?.title ?? "nenhum";
        _operation.Log("busca_documental", q, best);

        return Ok(new
        {
            query = q,
            total = results.Count,
            results
        });
    }

    [HttpGet("file")]
    public IActionResult File([FromQuery] string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return BadRequest(new { error = "Informe um arquivo em ?path=" });

        var content = _obsidian.ReadFile(path);

        if (string.IsNullOrEmpty(content))
            return NotFound(new { error = "Arquivo nao encontrado.", path });

        _operation.Log("arquivo_aberto", path, Path.GetFileName(path));

        return Ok(new
        {
            path,
            content
        });
    }

    [HttpPost("standardize")]
    public async Task<IActionResult> Standardize([FromBody] StandardizeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Path))
            return BadRequest(new { error = "Informe o path do arquivo." });

        var original = _obsidian.ReadFile(request.Path);

        if (string.IsNullOrWhiteSpace(original))
            return NotFound(new { error = "Arquivo nao encontrado ou vazio.", request.Path });

        var fileName = BuildSlugFromPath(request.Path);
        var targetPath = $"06_Documents/Procedimentos-Padronizados/Importados/{fileName}-padronizado.md";
        var fallbackContent = BuildFallbackProcedure(request.Path, original);
        var prompt = BuildStandardizePrompt(original, request.Path);

        string? aiContent = null;
        var engine = "fallback";

        if (ShouldUseClaude("CLAUDE_USE_FOR_STANDARDIZE"))
        {
            aiContent = await _claude.AskAsync(prompt, 4096);
            if (!string.IsNullOrWhiteSpace(aiContent)) engine = "claude";
        }

        if (string.IsNullOrWhiteSpace(aiContent))
        {
            aiContent = await _ollama.AskAsync(prompt);
            if (!string.IsNullOrWhiteSpace(aiContent)) engine = "ollama";
        }

        var content = string.IsNullOrWhiteSpace(aiContent) ? fallbackContent : aiContent.Trim();

        _obsidian.EnsureFile(targetPath, content);
        _operation.Log("procedimento_padronizado", request.Path, $"{targetPath} | motor: {engine}");

        return Ok(new
        {
            created = true,
            path = targetPath,
            engine,
            usedOllama = engine == "ollama"
        });
    }

    [HttpPost("checklist")]
    public async Task<IActionResult> GenerateChecklist([FromBody] StandardizeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Path))
            return BadRequest(new { error = "Informe o path do arquivo." });

        var original = _obsidian.ReadFile(request.Path);

        if (string.IsNullOrWhiteSpace(original))
            return NotFound(new { error = "Arquivo nao encontrado ou vazio.", request.Path });

        var fileName = BuildSlugFromPath(request.Path);
        var targetPath = $"06_Documents/Checklists/Gerados/{fileName}-checklist.md";
        var fallback = BuildFallbackChecklist(request.Path);
        var prompt = BuildChecklistPrompt(original);

        string? aiContent = null;
        var engine = "fallback";

        if (ShouldUseClaude("CLAUDE_USE_FOR_CHECKLIST"))
        {
            aiContent = await _claude.AskAsync(prompt, 2048);
            if (!string.IsNullOrWhiteSpace(aiContent)) engine = "claude";
        }

        if (string.IsNullOrWhiteSpace(aiContent))
        {
            aiContent = await _ollama.AskAsync(prompt);
            if (!string.IsNullOrWhiteSpace(aiContent)) engine = "ollama";
        }

        var content = string.IsNullOrWhiteSpace(aiContent) ? fallback : aiContent.Trim();

        _obsidian.EnsureFile(targetPath, content);
        _operation.Log("checklist_gerado", request.Path, $"{targetPath} | motor: {engine}");

        return Ok(new
        {
            created = true,
            path = targetPath,
            engine,
            usedOllama = engine == "ollama"
        });
    }

    private static string BuildSlugFromPath(string path)
    {
        return Path.GetFileNameWithoutExtension(path)
            .ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("_", "-");
    }

    private static string BuildFallbackProcedure(string sourcePath, string original)
    {
        return $@"---
type: procedimento
origem: {sourcePath}
status: revisar
tags:
  - procedimento
  - padronizado
---

# {Path.GetFileNameWithoutExtension(sourcePath)} - Padronizado

## Objetivo
Nao informado na documentacao original.

## Quando usar
Nao informado na documentacao original.

## Pre-requisitos
Nao informado na documentacao original.

## Riscos
Nao informado na documentacao original.

## Passo a passo
1. Revisar conteudo original antes de executar.

## Comandos uteis
```bash
# revisar comandos do conteudo original
```

## Cuidados
- Validar ambiente.
- Nao executar comandos destrutivos sem confirmacao.

## Validacao
Nao informado na documentacao original.

## Rollback
Nao informado na documentacao original.

## Evidencias
Nao informado na documentacao original.

## Conteudo original utilizado como base

{original}
";
    }

    private static string BuildFallbackChecklist(string sourcePath)
    {
        return $@"---
type: checklist
status: revisar
origem: {sourcePath}
tags:
  - checklist
  - gerado
---

# Checklist - {Path.GetFileNameWithoutExtension(sourcePath)}

## Antes de comecar
- [ ] Validar ambiente
- [ ] Confirmar acesso
- [ ] Confirmar backup quando aplicavel

## Execucao
- [ ] Revisar procedimento original
- [ ] Executar passos documentados

## Validacao
- [ ] Validar resultado
- [ ] Conferir logs

## Evidencias
- [ ] Registrar print/log/chamado

## Pos-execucao
- [ ] Atualizar chamado
- [ ] Registrar observacoes
";
    }

    private static string BuildStandardizePrompt(string originalContent, string sourcePath)
    {
        return $@"
Voce e Nexus, assistente tecnico de Gabriel.

Transforme a documentacao abaixo em um procedimento tecnico padronizado em Markdown.

Regras:
- Responda apenas com Markdown.
- Nao invente caminhos, senhas, usuarios, IPs ou comandos.
- Preserve comandos existentes.
- Se faltar informacao, escreva: 'Nao informado na documentacao original'.
- Inclua cuidados e validacao.
- Inclua rollback quando fizer sentido.
- Nao inclua credenciais.

Formato obrigatorio:

---
type: procedimento
origem: {sourcePath}
status: revisar
tags:
  - procedimento
  - padronizado
---

# Titulo do procedimento

## Objetivo

## Quando usar

## Pre-requisitos

## Riscos

## Passo a passo

## Comandos uteis

## Cuidados

## Validacao

## Rollback

## Evidencias

## Conteudo original utilizado como base

Documentacao original:

{originalContent}
";
    }

    private static string BuildChecklistPrompt(string originalContent)
    {
        return $@"
Voce e Nexus, assistente tecnico de Gabriel.

Gere um checklist operacional em Markdown com base na documentacao abaixo.

Regras:
- Responda apenas com Markdown.
- Use checkboxes '- [ ]'.
- Nao invente senhas, caminhos, IPs ou credenciais.
- Separe em: Antes de comecar, Execucao, Validacao, Evidencias, Pos-execucao.
- Inclua cuidados quando houver risco.

Documentacao:

{originalContent}
";
    }
}

public class StandardizeRequest
{
    public string Path { get; set; } = "";
}
