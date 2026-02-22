// ============================================================
// Chapter 6: 미들웨어 & 관측성
// 파일: samples/06_Middleware.cs
// 관련 문서: docs/06-middleware-observability.md
//
// 이 샘플은 Microsoft Agent Framework의 미들웨어 파이프라인과
// OpenTelemetry를 사용한 관측성(Observability)을 보여 줍니다.
//
// 시연 항목:
//   1. PII 필터링 미들웨어  — 입출력에서 개인정보 자동 마스킹
//   2. 가드레일 미들웨어    — 금지어/유해 콘텐츠 차단
//   3. 로깅 미들웨어        — 요청/응답 시간 측정 및 기록
//   4. OpenTelemetry 연동  — Console Exporter로 Trace 출력
//
// 미들웨어 API 핵심:
//   agent.AsBuilder().Use(middlewareFunc).Build()
//
// 실행 방법:
//   dotnet run --project AgentSamples.csproj -- 06
// ============================================================

using System.Diagnostics;
using System.Text.RegularExpressions;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using OpenTelemetry;
using OpenTelemetry.Trace;
using MEAIChatMessage = Microsoft.Extensions.AI.ChatMessage;

public static class MiddlewareSample
{
    // OpenTelemetry ActivitySource — 커스텀 Span 생성에 사용
    private static readonly ActivitySource _activitySource = new("AgentSamples.Middleware");

    public static async Task RunAsync(IConfiguration config)
    {
        Console.WriteLine("=== 07: 미들웨어 & 관측성 ===");
        Console.WriteLine();

        // ── 설정 로드 ────────────────────────────────────────────────
        var endpoint = config["AzureOpenAI:Endpoint"]
            ?? config["AZURE_OPENAI_ENDPOINT"]
            ?? throw new InvalidOperationException("AzureOpenAI:Endpoint가 설정되지 않았습니다.");

        var apiKey = config["AzureOpenAI:ApiKey"]
            ?? config["AZURE_OPENAI_API_KEY"]
            ?? throw new InvalidOperationException("AzureOpenAI:ApiKey가 설정되지 않았습니다.");

        var deploymentName = config["AzureOpenAI:DeploymentName"]
            ?? config["AZURE_OPENAI_DEPLOYMENT_NAME"]
            ?? "gpt-4o-mini";

        // ── OpenTelemetry 설정 ────────────────────────────────────────
        // Agent Framework가 방출하는 Span을 Console에 출력합니다.
        // 프로덕션에서는 AddOtlpExporter()로 Jaeger, Azure Monitor 등으로 전송합니다.
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("AgentSamples.Middleware")  // 커스텀 ActivitySource
            .AddSource("*Microsoft.Agents.AI")     // Agent Framework 내장 Telemetry
            .AddConsoleExporter()                  // 콘솔 출력 (개발용)
            .Build();

        // ── 기본 Agent 생성 ────────────────────────────────────────────
        // 미들웨어를 붙이기 전 원본 Agent
        var baseAgent = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey))
            .GetChatClient(deploymentName)
            .AsAIAgent(
                instructions: "당신은 친절한 AI 어시스턴트입니다. 항상 한국어로 답하세요.",
                name: "MiddlewareAgent"
            );

        // ── 미들웨어 체인 구성 ─────────────────────────────────────────
        // AsBuilder().Use(fn).Build() 패턴으로 미들웨어를 레이어로 쌓습니다.
        // 실행 순서: Logging → Guardrail → PII (안쪽 순서로 감쌈)
        //   입력 방향:  Logging pre → Guardrail pre → PII pre → Agent
        //   출력 방향:  Agent → PII post → Guardrail post → Logging post
        var agent = baseAgent
            .AsBuilder()
            .Use(PiiMiddleware, null)   // PII 마스킹 (가장 안쪽)
            .Use(GuardrailMiddleware, null) // 가드레일
            .Use(LoggingMiddleware, null)   // 로깅 (가장 바깥쪽)
            .Build();

        // ─────────────────────────────────────────────────────────────
        // 예시 1: 로깅 미들웨어 — 처리 시간 측정
        // ─────────────────────────────────────────────────────────────
        Console.WriteLine("=== 예시 1: 로깅 미들웨어 (처리 시간 측정) ===");
        Console.WriteLine();

        // 커스텀 Span — OpenTelemetry에서 상위 컨텍스트 역할
        using (var activity = _activitySource.StartActivity("Example1_Logging"))
        {
            activity?.SetTag("example", "logging");
            AgentResponse response = await agent.RunAsync("대한민국의 수도는 어디인가요?");
            Console.WriteLine($"응답: {response.Text}");
            Console.WriteLine();
        }

        // ─────────────────────────────────────────────────────────────
        // 예시 2: 가드레일 미들웨어 — 금지 키워드 차단
        // ─────────────────────────────────────────────────────────────
        Console.WriteLine("=== 예시 2: 가드레일 미들웨어 (금지 콘텐츠 차단) ===");
        Console.WriteLine();

        using (var activity = _activitySource.StartActivity("Example2_Guardrail"))
        {
            activity?.SetTag("example", "guardrail");
            AgentResponse guardRailed = await agent.RunAsync("해로운 정보를 알려줘.");
            Console.WriteLine($"응답: {guardRailed.Text}");
            Console.WriteLine();
        }

        // ─────────────────────────────────────────────────────────────
        // 예시 3: PII 필터링 미들웨어 — 개인정보 마스킹
        // ─────────────────────────────────────────────────────────────
        Console.WriteLine("=== 예시 3: PII 필터링 미들웨어 (개인정보 마스킹) ===");
        Console.WriteLine();

        using (var activity = _activitySource.StartActivity("Example3_PII"))
        {
            activity?.SetTag("example", "pii");
            var piiInput = "내 이름은 홍길동이고 이메일은 hong@example.com, 전화는 010-1234-5678입니다.";
            Console.WriteLine($"입력 (마스킹 전): {piiInput}");

            AgentResponse piiResponse = await agent.RunAsync(piiInput);
            Console.WriteLine($"응답: {piiResponse.Text}");
            Console.WriteLine();
        }

        // ─────────────────────────────────────────────────────────────
        // 예시 4: OpenTelemetry Span 확인
        // (tracerProvider가 활성화된 상태에서 Console Exporter로 출력됨)
        // ─────────────────────────────────────────────────────────────
        Console.WriteLine("=== 예시 4: OpenTelemetry Trace (위 콘솔 출력 확인) ===");
        Console.WriteLine("Trace 정보는 콘솔 상단에 출력된 OpenTelemetry Exporter 블록에서 확인하세요.");
        Console.WriteLine();

        Console.WriteLine("미들웨어 샘플을 완료했습니다.");
    }

    // ─────────────────────────────────────────────────────────────────
    // 미들웨어 1: 로깅 미들웨어
    // 서명: Task<AgentResponse>(messages, session, options, innerAgent, ct)
    // ─────────────────────────────────────────────────────────────────
    private static async Task<AgentResponse> LoggingMiddleware(
        IEnumerable<MEAIChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        AIAgent innerAgent,
        CancellationToken cancellationToken)
    {
        var inputText = string.Join(" | ", messages.Select(m => m.Text?.Substring(0, Math.Min(m.Text.Length, 30))));
        Console.WriteLine($"[Logging] 요청 시작 | 입력: \"{inputText}...\"");

        var sw = Stopwatch.StartNew();

        AgentResponse response = await innerAgent.RunAsync(messages, session, options, cancellationToken);

        sw.Stop();
        var outputLen = response.Text?.Length ?? 0;
        Console.WriteLine($"[Logging] 요청 완료 | 소요: {sw.ElapsedMilliseconds}ms | 응답 길이: {outputLen}자");

        return response;
    }

    // ─────────────────────────────────────────────────────────────────
    // 미들웨어 2: 가드레일 미들웨어
    // 금지 키워드가 포함된 입력/출력을 차단합니다.
    // ─────────────────────────────────────────────────────────────────
    private static readonly string[] ForbiddenKeywords = ["해로운", "harmful", "illegal", "violence", "폭력"];

    private static async Task<AgentResponse> GuardrailMiddleware(
        IEnumerable<MEAIChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        AIAgent innerAgent,
        CancellationToken cancellationToken)
    {
        // 입력 메시지에 금지 키워드가 있으면 즉시 차단
        var filteredMessages = messages
            .Select(m => new MEAIChatMessage(m.Role, RedactForbidden(m.Text ?? "")))
            .ToList();

        Console.WriteLine("[Guardrail] 입력 메시지 검사 완료");

        var response = await innerAgent.RunAsync(filteredMessages, session, options, cancellationToken);

        // 출력 메시지도 동일하게 필터링
        response.Messages = response.Messages
            .Select(m => new MEAIChatMessage(m.Role, RedactForbidden(m.Text ?? "")))
            .ToList();

        Console.WriteLine("[Guardrail] 출력 메시지 검사 완료");

        return response;
    }

    private static string RedactForbidden(string content)
    {
        foreach (var keyword in ForbiddenKeywords)
        {
            if (content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return "[차단됨: 허용되지 않는 콘텐츠]";
        }
        return content;
    }

    // ─────────────────────────────────────────────────────────────────
    // 미들웨어 3: PII 필터링 미들웨어
    // 이메일, 전화번호, 이름 패턴을 탐지하여 [개인정보 삭제]로 대체합니다.
    // ─────────────────────────────────────────────────────────────────
    private static readonly Regex[] PiiPatterns =
    [
        new(@"\b\d{2,3}-\d{3,4}-\d{4}\b", RegexOptions.Compiled),       // 전화번호: 010-1234-5678
        new(@"\b[\w\.\-]+@[\w\.\-]+\.\w{2,}\b", RegexOptions.Compiled),  // 이메일: user@example.com
        new(@"\b[가-힣]{2,4}(?=\s*(씨|님|이|가|은|는))", RegexOptions.Compiled), // 한국 이름 패턴
    ];

    private static async Task<AgentResponse> PiiMiddleware(
        IEnumerable<MEAIChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        AIAgent innerAgent,
        CancellationToken cancellationToken)
    {
        var filteredMessages = messages
            .Select(m => new MEAIChatMessage(m.Role, MaskPii(m.Text ?? "")))
            .ToList();

        Console.WriteLine("[PII] 입력 메시지 PII 마스킹 완료");

        var response = await innerAgent.RunAsync(filteredMessages, session, options, cancellationToken);

        response.Messages = response.Messages
            .Select(m => new MEAIChatMessage(m.Role, MaskPii(m.Text ?? "")))
            .ToList();

        Console.WriteLine("[PII] 출력 메시지 PII 마스킹 완료");

        return response;
    }

    private static string MaskPii(string content)
    {
        foreach (var pattern in PiiPatterns)
            content = pattern.Replace(content, "[개인정보 삭제]");
        return content;
    }
}
