// ============================================================
// Chapter 2: 첫 번째 Agent 만들기
// 파일: samples/01_HelloAgent.cs
// 관련 문서: docs/02-first-agent.md
//
// 필요한 설정 (appsettings.local.json 또는 환경 변수):
//   - AzureOpenAI:Endpoint
//   - AzureOpenAI:ApiKey
//   - AzureOpenAI:DeploymentName (기본값: gpt-4o-mini)
//
// 실행 방법:
//   dotnet run --project AgentSamples.csproj -- 01
// ============================================================

using Azure;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

public static class HelloAgent
{
    public static async Task RunAsync(IConfiguration config)
    {
        Console.WriteLine("=== 01: Hello Agent ===");
        Console.WriteLine();

        // ── 설정 로드 ────────────────────────────────────────────────
        // 우선순위: appsettings.local.json → 환경 변수
        var endpoint = config["AzureOpenAI:Endpoint"]
            ?? config["AZURE_OPENAI_ENDPOINT"]
            ?? throw new InvalidOperationException(
                "Azure OpenAI endpoint가 설정되지 않았습니다.\n" +
                "appsettings.local.json의 AzureOpenAI:Endpoint 또는\n" +
                "환경 변수 AZURE_OPENAI_ENDPOINT를 설정하세요.");

        var apiKey = config["AzureOpenAI:ApiKey"]
            ?? config["AZURE_OPENAI_API_KEY"]
            ?? throw new InvalidOperationException(
                "Azure OpenAI API 키가 설정되지 않았습니다.\n" +
                "appsettings.local.json의 AzureOpenAI:ApiKey 또는\n" +
                "환경 변수 AZURE_OPENAI_API_KEY를 설정하세요.");

        var deploymentName = config["AzureOpenAI:DeploymentName"]
            ?? config["AZURE_OPENAI_DEPLOYMENT_NAME"]
            ?? "gpt-4o-mini";

        Console.WriteLine($"Endpoint : {endpoint}");
        Console.WriteLine($"Deployment: {deploymentName}");
        Console.WriteLine();

        // ── Agent 초기화 ────────────────────────────────────────────
        // 1. AzureOpenAIClient: AzureKeyCredential로 인증 (API 키 사용)
        // 2. GetChatClient(): Chat Completions 엔드포인트 선택
        // 3. AsAIAgent(): Agent 래퍼로 변환 (instructions = 시스템 프롬프트)
        AIAgent agent = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey))
            .GetChatClient(deploymentName)
            .AsAIAgent(
                instructions: "당신은 친절한 AI 어시스턴트입니다. 항상 한국어로 간결하게 대답하세요.",
                name: "HelloAgent"
            );

        try
        {
            // ── 단일 응답: RunAsync() ───────────────────────────────
            // 반환 타입은 AgentResponse이며, .Text 로 응답 텍스트를 가져옵니다.
            Console.WriteLine("[단일 응답 — RunAsync()]");
            AgentResponse response = await agent.RunAsync("안녕하세요! 자기소개를 두 문장으로 해주세요.");
            Console.WriteLine(response.Text);
            Console.WriteLine();

            // ── 스트리밍 응답: RunStreamingAsync() ─────────────────
            // IAsyncEnumerable<AgentResponseUpdate>로 토큰 단위 즉시 출력
            // 각 update.Text 에 새로 추가된 토큰 조각이 담겨 있습니다.
            Console.WriteLine("[스트리밍 응답 — RunStreamingAsync()]");
            Console.Write("Agent: ");
            await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(
                "대한민국의 수도는 어디인가요? 한 문장으로 답하세요."))
            {
                Console.Write(update.Text); // 토큰이 생성되는 즉시 출력
            }
            Console.WriteLine();
        }
        catch (RequestFailedException ex) when (ex.Status == 401)
        {
            Console.Error.WriteLine($"[오류] 인증 실패: API 키를 확인하세요. ({ex.Message})");
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            Console.Error.WriteLine($"[오류] 배포를 찾을 수 없습니다: '{deploymentName}'. " +
                $"Azure Portal에서 배포 이름을 확인하세요. ({ex.Message})");
        }
        catch (RequestFailedException ex)
        {
            Console.Error.WriteLine($"[오류] Azure API 오류 [{ex.Status}]: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[오류] 예상치 못한 오류: {ex.Message}");
        }
    }
}
