// ============================================================
// Chapter 4: 멀티턴 대화 — AgentSession으로 컨텍스트 유지
// 파일: samples/03_MultiTurn.cs
// 관련 문서: docs/04-memory-session.md
//
// AgentSession을 한 번 생성해 재사용하면 프레임워크가
// 대화 히스토리를 자동으로 누적·관리합니다.
// 직접 List<ChatMessage>를 관리할 필요가 없습니다.
//
// 실행 방법:
//   dotnet run --project AgentSamples.csproj -- 03
// ============================================================

using Azure;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

public static class MultiTurn
{
    public static async Task RunAsync(IConfiguration config)
    {
        Console.WriteLine("=== 03: Multi-Turn Conversation (AgentSession) ===");
        Console.WriteLine("('quit' 입력 시 종료)");
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

        // ── Agent 초기화 ─────────────────────────────────────────────
        AIAgent agent = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey))
            .GetChatClient(deploymentName)
            .AsAIAgent(
                instructions: "당신은 친절한 AI 어시스턴트입니다. " +
                              "항상 한국어로 대답하고, 이전 대화 내용을 기억하세요.",
                name: "MultiTurnAgent"
            );

        // ── AgentSession 생성 ────────────────────────────────────────
        // session을 한 번 만들어 모든 턴에 재사용합니다.
        // 프레임워크가 session 내부에 대화 히스토리를 자동으로 누적합니다.
        // session = null로 RunAsync를 호출하면 매번 새 세션 → 상태 비저장.
        AgentSession session = await agent.CreateSessionAsync();

        // ── 데모: 이전 대화 기억 확인 ────────────────────────────────
        var demoExchanges = new[]
        {
            "제 이름은 김민준이고 서울에 삽니다.",
            "제가 어디에 산다고 했나요?",
            "제 이름의 성씨(Family name)는 무엇인가요?"
        };

        Console.WriteLine("[데모 모드: 사전 설정 질문 3개 실행]");
        Console.WriteLine();

        foreach (var question in demoExchanges)
        {
            Console.WriteLine($"사용자: {question}");
            Console.Write("Agent: ");

            // 같은 session을 계속 전달 → 히스토리 자동 유지
            await foreach (AgentResponseUpdate update in
                agent.RunStreamingAsync(question, session))
            {
                Console.Write(update.Text);
            }

            Console.WriteLine();
            Console.WriteLine();
        }

        // ── 대화형 모드 ──────────────────────────────────────────────
        Console.WriteLine("[대화형 모드: 직접 입력하세요]");
        Console.WriteLine();

        while (true)
        {
            Console.Write("사용자: ");
            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(input)) continue;
            if (input.Equals("quit", StringComparison.OrdinalIgnoreCase)) break;

            Console.Write("Agent: ");

            try
            {
                await foreach (AgentResponseUpdate update in
                    agent.RunStreamingAsync(input, session))
                {
                    Console.Write(update.Text);
                }
                Console.WriteLine();
                Console.WriteLine();
            }
            catch (RequestFailedException ex)
            {
                Console.Error.WriteLine($"\n[오류] Azure API 오류 [{ex.Status}]: {ex.Message}");
            }
        }

        Console.WriteLine("대화를 종료합니다.");
    }
}
