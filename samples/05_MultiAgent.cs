// ============================================================
// Chapter 5: 멀티 에이전트 협업
// 파일: samples/05_MultiAgent.cs
// 관련 문서: docs/05-multi-agent.md
//
// 패턴: 오케스트레이터(Orchestrator) + 전문 에이전트(Specialist)
//
//   사용자 질문
//       │
//       ▼
//   [OrchestratorAgent]  ← 의도 파악 후 적절한 전문 에이전트 호출
//       │
//       ├─▶ [WeatherAgent]    날씨 관련 질문 처리
//       ├─▶ [CalculatorAgent] 수학 계산 처리
//       └─▶ (직접 답변)       일반 질문 처리
//
// 구현 방식: AIFunctionFactory로 각 전문 에이전트를 Tool로 등록
// → 오케스트레이터가 필요 시 Tool(전문 에이전트)을 자동 호출
//
// 실행 방법:
//   dotnet run --project AgentSamples.csproj -- 05
// ============================================================

using Azure;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

public static class MultiAgentSample
{
    public static async Task RunAsync(IConfiguration config)
    {
        Console.WriteLine("=== 06: 멀티 에이전트 협업 (Orchestrator + Specialists) ===");
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

        var azureClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));

        // ── 전문 에이전트 생성 ───────────────────────────────────────

        // 날씨 전문 에이전트
        AIAgent weatherAgent = azureClient
            .GetChatClient(deploymentName)
            .AsAIAgent(
                instructions:
                    "당신은 날씨 전문 에이전트입니다. " +
                    "날씨 관련 질문에만 답변하며, 간결하게 답합니다. " +
                    "실제 날씨 데이터가 없으므로 예시 데이터로 답변합니다.",
                name: "WeatherAgent"
            );

        // 계산기 전문 에이전트
        AIAgent calculatorAgent = azureClient
            .GetChatClient(deploymentName)
            .AsAIAgent(
                instructions:
                    "당신은 수학 계산 전문 에이전트입니다. " +
                    "수식과 계산 문제를 정확하게 풀어 결과만 간결하게 답합니다.",
                name: "CalculatorAgent"
            );

        // ── 전문 에이전트를 Tool로 래핑 ─────────────────────────────
        // 오케스트레이터가 LLM Function Calling으로 이 Tool들을 호출합니다.

        var weatherTool = AIFunctionFactory.Create(
            async (string question) =>
            {
                Console.WriteLine($"  [WeatherAgent 호출] 질문: {question}");
                var response = await weatherAgent.RunAsync(question, session: null);
                return response.Text ?? string.Empty;
            },
            name: "ask_weather_agent",
            description: "날씨, 기온, 강수, 미세먼지 등 기상 관련 질문을 날씨 전문 에이전트에게 전달합니다.");

        var calculatorTool = AIFunctionFactory.Create(
            async (string expression) =>
            {
                Console.WriteLine($"  [CalculatorAgent 호출] 식: {expression}");
                var response = await calculatorAgent.RunAsync(expression, session: null);
                return response.Text ?? string.Empty;
            },
            name: "ask_calculator_agent",
            description: "수학 계산, 수식 풀이, 단위 변환 등 계산 관련 질문을 계산기 전문 에이전트에게 전달합니다.");

        // ── 오케스트레이터 에이전트 ──────────────────────────────────
        // 두 전문 에이전트 Tool을 장착하고 라우팅을 담당합니다.
        AIAgent orchestrator = azureClient
            .GetChatClient(deploymentName)
            .AsAIAgent(
                instructions:
                    "당신은 사용자 요청을 적절한 전문 에이전트에게 위임하는 오케스트레이터입니다. " +
                    "날씨 관련 질문 → ask_weather_agent, " +
                    "수학/계산 관련 질문 → ask_calculator_agent, " +
                    "그 외 일반 질문 → 직접 답변. " +
                    "항상 한국어로 최종 답변을 제공하세요.",
                name: "OrchestratorAgent",
                tools: [weatherTool, calculatorTool]
            );

        // ── 데모 질문 ────────────────────────────────────────────────
        var questions = new[]
        {
            "오늘 서울 날씨가 어때?",
            "123 곱하기 456은 얼마야?",
            "Microsoft Agent Framework가 뭐야?",
            "부산 내일 비 올 확률이랑, 27 더하기 38은?",  // 두 에이전트 동시 활용
        };

        Console.WriteLine("[오케스트레이터 라우팅 데모]");
        Console.WriteLine("(들여쓰기 라인은 전문 에이전트 호출 로그)");
        Console.WriteLine();

        foreach (var question in questions)
        {
            Console.WriteLine($"사용자: {question}");
            Console.Write("최종 답변: ");

            await foreach (var update in orchestrator.RunStreamingAsync(question, session: null))
                Console.Write(update.Text);

            Console.WriteLine("\n");
        }

        // ── 대화형 모드 ──────────────────────────────────────────────
        Console.WriteLine("[대화형 모드] ('quit' 입력 시 종료)");
        Console.WriteLine();

        while (true)
        {
            Console.Write("사용자: ");
            var input = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input)) continue;
            if (input.Equals("quit", StringComparison.OrdinalIgnoreCase)) break;

            Console.Write("최종 답변: ");
            try
            {
                await foreach (var update in orchestrator.RunStreamingAsync(input, session: null))
                    Console.Write(update.Text);
                Console.WriteLine("\n");
            }
            catch (RequestFailedException ex)
            {
                Console.Error.WriteLine($"\n[오류] Azure API 오류 [{ex.Status}]: {ex.Message}");
            }
        }

        Console.WriteLine("멀티 에이전트 샘플을 종료합니다.");
    }
}
