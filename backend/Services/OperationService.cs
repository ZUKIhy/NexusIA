using System.Text.Json;

namespace NexusBackend.Services;

public class OperationService
{
    private readonly ObsidianService _obsidian;
    private readonly ClaudeService _claude;
    private readonly OllamaService _ollama;

    public OperationService(ObsidianService obsidian, ClaudeService claude, OllamaService ollama)
    {
        _obsidian = obsidian;
        _claude = claude;
        _ollama = ollama;
        EnsureOperationFiles();
    }

    public bool IsOperationMode()
    {
        try
        {
            var content = _obsidian.ReadFile("07_Nexus/operation-mode.json");
            using var doc = JsonDocument.Parse(content);
            return doc.RootElement.TryGetProperty("enabled", out var enabled) && enabled.GetBoolean();
        }
        catch
        {
            return _obsidian.ReadFile("07_Nexus/operation-mode.md").Contains("Status: ativo", StringComparison.OrdinalIgnoreCase);
        }
    }

    public void SetOperationMode(bool enabled)
    {
        var jsonPath = _obsidian.GetFullPath("07_Nexus/operation-mode.json");
        var json = JsonSerializer.Serialize(new
        {
            enabled,
            updatedAt = DateTime.Now
        }, new JsonSerializerOptions { WriteIndented = true });

        File.WriteAllText(jsonPath, json);

        var markdown = $@"---
type: operation-mode
tags:
  - nexus
  - operacao
---

# Modo Operação

Status: {(enabled ? "ativo" : "inativo")}

Última atualização: {DateTime.Now:yyyy-MM-dd HH:mm:ss}

## Regras
- Responder com resumo, riscos, passo a passo, validação e próxima ação.
- Consultar o Obsidian antes de sugerir comandos.
- Nunca executar comandos destrutivos sem confirmação explícita.
- Registrar ações relevantes no log operacional.
";

        File.WriteAllText(_obsidian.GetFullPath("07_Nexus/operation-mode.md"), markdown);
        Log("modo_operacao", enabled ? "ativado" : "desativado", enabled ? "Modo operação ativado" : "Modo operação desativado");
    }

    public string StartSession(string context)
    {
        SetOperationMode(true);

        var session = $@"# Sessão Operacional Atual

## Status
ativo

## Início
{DateTime.Now:yyyy-MM-dd HH:mm:ss}

## Contexto
{(string.IsNullOrWhiteSpace(context) ? "-" : context)}

## Procedimentos consultados
-

## Ações realizadas
- Sessão iniciada.

## Pendências
-

## Próxima ação
- Levantar evidências iniciais e consultar procedimento relacionado.
";

        File.WriteAllText(_obsidian.GetFullPath("07_Nexus/current-session.md"), session);
        Log("atendimento_iniciado", context, "07_Nexus/current-session.md");
        return BuildOperationFrame($"Atendimento iniciado.\n\nContexto: {(string.IsNullOrWhiteSpace(context) ? "Não informado" : context)}", "Procure ou informe o procedimento relacionado ao atendimento.");
    }

    public void RegisterProcedureConsulted(string query, IReadOnlyList<MarkdownSearchResult> results)
    {
        var best = results.FirstOrDefault();
        var bestPath = best?.Path ?? "nenhum";
        AppendCurrentSession("Procedimentos consultados", $"- {DateTime.Now:HH:mm} | {query} | {bestPath}");
        Log("procedimento_consultado", query, bestPath);
    }

    public void RegisterAction(string action, string result)
    {
        AppendCurrentSession("Ações realizadas", $"- {DateTime.Now:HH:mm} | {action}: {result}");
        Log(action, "-", result);
    }

    public async Task<string> GenerateReport()
    {
        RegisterAction("relatorio_atendimento", "Relatório de atendimento gerado");
        var sessionContent = TrimForChat(_obsidian.ReadFile("07_Nexus/current-session.md"));
        var polished = await PolishWithAi(BuildReportPrompt(sessionContent));
        var body = string.IsNullOrWhiteSpace(polished)
            ? $"Relatório de atendimento gerado com base na sessão atual.\n\n{sessionContent}"
            : polished;
        return BuildOperationFrame(body, "Revisar o relatório, complementar evidências e registrar no chamado.");
    }

    public async Task<string> GenerateHandover()
    {
        RegisterAction("passagem_turno", "Passagem de turno criada");
        var sessionContent = TrimForChat(_obsidian.ReadFile("07_Nexus/current-session.md"));
        var polished = await PolishWithAi(BuildHandoverPrompt(sessionContent));
        var body = string.IsNullOrWhiteSpace(polished)
            ? $"Passagem de turno criada com base na sessão atual.\n\n{sessionContent}"
            : polished;
        return BuildOperationFrame(body, "Enviar para o responsável do próximo turno e anexar evidências.");
    }

    private async Task<string?> PolishWithAi(string prompt)
    {
        var useClaude = (Environment.GetEnvironmentVariable("CLAUDE_USE_FOR_REPORTS") ?? "false")
            .Equals("true", StringComparison.OrdinalIgnoreCase);

        string? answer = null;

        if (useClaude)
            answer = await _claude.AskAsync(prompt, 4096);

        if (string.IsNullOrWhiteSpace(answer))
            answer = await _ollama.AskAsync(prompt);

        return answer;
    }

    private static string BuildReportPrompt(string sessionContent)
    {
        return $@"
Voce e Nexus, assistente tecnico de Gabriel.

Gere um relatorio de atendimento profissional em Markdown com base no conteudo da sessao atual abaixo.

Regras:
- Responda apenas com Markdown.
- Nao invente dados que nao estao na sessao.
- Mantenha sigilo: nao exponha credenciais.
- Estruture em: Resumo, Procedimentos consultados, Acoes realizadas, Pendencias, Proxima acao.

Sessao atual:

{sessionContent}
";
    }

    private static string BuildHandoverPrompt(string sessionContent)
    {
        return $@"
Voce e Nexus, assistente tecnico de Gabriel.

Gere uma passagem de turno clara em Markdown com base na sessao atual abaixo.

Regras:
- Responda apenas com Markdown.
- Foque no que o proximo turno precisa saber para continuar.
- Nao invente dados que nao estao na sessao.
- Estruture em: Contexto, O que ja foi feito, Pendencias, Riscos/Atencao, Proxima acao recomendada.

Sessao atual:

{sessionContent}
";
    }

    public void Log(string action, string query, string result)
    {
        var entry = $"""

        ## {DateTime.Now:yyyy-MM-dd HH:mm:ss}
        Ação: {action}
        Consulta: {query}
        Resultado principal: {result}

        """;

        _obsidian.AppendToFile("07_Nexus/operation-log.md", entry);
    }

    public string BuildOperationFrame(string answer, string nextAction = "Validar documentação relacionada antes de executar.")
    {
        return $"""
        Resumo
        {answer}

        Riscos
        - Validar ambiente antes de executar qualquer ação.
        - Não executar comandos destrutivos sem confirmação.
        - Confirmar backup quando houver alteração ou remoção de dados.

        Passo a passo
        1. Confirmar o procedimento correto no Obsidian.
        2. Validar pré-requisitos e permissões.
        3. Executar somente após aprovação.

        Validação
        - Conferir logs, serviço, evidências e resultado esperado.

        Próxima ação
        {nextAction}
        """;
    }

    private void EnsureOperationFiles()
    {
        EnsureOperationLog();
        _obsidian.EnsureFile("07_Nexus/operation-mode.json", "{\"enabled\":false}");
        _obsidian.EnsureFile("07_Nexus/operation-mode.md", InitialOperationMode());
        _obsidian.EnsureFile("07_Nexus/current-session.md", InitialCurrentSession());
    }

    private void EnsureOperationLog()
    {
        const string path = "07_Nexus/operation-log.md";
        var header = InitialOperationLog();

        _obsidian.EnsureFile(path, header);

        var content = _obsidian.ReadFile(path);
        if (!content.Contains("type: operation-log", StringComparison.OrdinalIgnoreCase))
        {
            File.WriteAllText(_obsidian.GetFullPath(path), header + "\n" + content);
        }
    }

    private void AppendCurrentSession(string section, string line)
    {
        var path = _obsidian.GetFullPath("07_Nexus/current-session.md");
        var content = File.Exists(path) ? File.ReadAllText(path) : InitialCurrentSession();
        var marker = $"## {section}";
        var index = content.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

        if (index < 0)
        {
            File.WriteAllText(path, content.TrimEnd() + $"\n\n{marker}\n{line}\n");
            return;
        }

        var nextSection = content.IndexOf("\n## ", index + marker.Length, StringComparison.OrdinalIgnoreCase);
        var insertAt = nextSection < 0 ? content.Length : nextSection;
        var updated = content.Insert(insertAt, $"\n{line}");
        File.WriteAllText(path, updated);
    }

    private static string InitialOperationLog()
    {
        return """
        ---
        type: operation-log
        tags:
          - nexus
          - log
          - operacao
        ---

        # Operation Log

        Registro de ações importantes executadas pelo Nexus.

        ## Eventos

        """;
    }

    private static string InitialOperationMode()
    {
        return $@"---
type: operation-mode
tags:
  - nexus
  - operacao
---

# Modo Operação

Status: inativo

Última atualização: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
";
    }

    private static string InitialCurrentSession()
    {
        return $@"# Sessão Operacional Atual

## Status
inativo

## Início
-

## Contexto
-

## Procedimentos consultados
-

## Ações realizadas
-

## Pendências
-

## Próxima ação
-
";
    }

    private static string TrimForChat(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return "Nenhum registro operacional encontrado.";

        return content.Length <= 2200 ? content : content[^2200..];
    }
}
