// ============================================================
// Chapter 3: Function Calling — Tool로 능력 확장
// 파일: samples/02_AddTools.cs
// 관련 문서: docs/03-function-calling.md
//
// 이 샘플은 AIFunctionFactory.Create()로 로컬 C# 함수를
// Agent의 Tool로 등록하는 방법을 보여 줍니다.
//
// 등록 Tool 목록:
//   1. GetCurrentWeather  — 도시 날씨 조회 (mock)
//   2. GetCurrentTime     — 현재 시각 반환
//   3. Calculate          — 사칙연산 계산기
//
// 실행 방법:
//   dotnet run --project AgentSamples.csproj -- 02
// ============================================================

using System.ComponentModel;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

public static class AddTools
{
    public static async Task RunAsync(IConfiguration config)
    {
        Console.WriteLine("=== 02: Function Calling (Tools) ===");
        Console.WriteLine();

        // ── 설정 로드 ────────────────────────────────────────────────
        var endpoint = config["AzureOpenAI:Endpoint"]
            ?? config["AZURE_OPENAI_ENDPOINT"]
            ?? throw new InvalidOperationException(
                "AzureOpenAI:Endpoint가 설정되지 않았습니다.");

        var apiKey = config["AzureOpenAI:ApiKey"]
            ?? config["AZURE_OPENAI_API_KEY"]
            ?? throw new InvalidOperationException(
                "AzureOpenAI:ApiKey가 설정되지 않았습니다.");

        var deploymentName = config["AzureOpenAI:DeploymentName"]
            ?? config["AZURE_OPENAI_DEPLOYMENT_NAME"]
            ?? "gpt-4o-mini";

        // ── Tool 정의 ────────────────────────────────────────────────
        // AIFunctionFactory.Create()는 대리자(delegate)를 AITool로 변환합니다.
        // [Description] 어트리뷰트는 LLM이 도구를 선택할 때 참조하는 설명입니다.

        // Tool 1: 날씨 조회 (mock — 실제 외부 API 호출로 교체 가능)
        AITool weatherTool = AIFunctionFactory.Create(
            ([Description("날씨를 조회할 도시 이름 (예: 서울, 부산)")] string city) =>
            {
                // 실제 서비스에서는 OpenWeatherMap 등 외부 API를 호출합니다.
                var mockData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["서울"]  = "맑음, 22°C",
                    ["부산"]  = "흐림, 19°C",
                    ["제주"]  = "비, 17°C",
                    ["대구"]  = "맑음, 25°C",
                    ["인천"]  = "안개, 18°C",
                };

                return mockData.TryGetValue(city, out var weather)
                    ? $"{city}의 현재 날씨: {weather}"
                    : $"{city}의 날씨 정보를 찾을 수 없습니다.";
            },
            name: "GetCurrentWeather",
            description: "지정한 도시의 현재 날씨와 기온을 반환합니다."
        );

        // Tool 2: 현재 시각
        AITool timeTool = AIFunctionFactory.Create(
            ([Description("시각을 조회할 타임존 (예: Asia/Seoul). 생략하면 로컬 시간 반환")] string? timezone = null) =>
            {
                // 간단하게 로컬 시간만 사용 (타임존 변환은 TimeZoneInfo로 확장 가능)
                var now = DateTime.Now;
                return $"현재 시각: {now:yyyy-MM-dd HH:mm:ss} (로컬 시간)";
            },
            name: "GetCurrentTime",
            description: "현재 날짜와 시각을 반환합니다."
        );

        // Tool 3: 사칙연산 계산기
        AITool calculatorTool = AIFunctionFactory.Create(
            (
                [Description("첫 번째 피연산자")] double a,
                [Description("연산자: +, -, *, /")] string op,
                [Description("두 번째 피연산자")] double b
            ) =>
            {
                return op switch
                {
                    "+" => $"{a} + {b} = {a + b}",
                    "-" => $"{a} - {b} = {a - b}",
                    "*" => $"{a} × {b} = {a * b}",
                    "/" => b == 0
                        ? "오류: 0으로 나눌 수 없습니다."
                        : $"{a} ÷ {b} = {a / b}",
                    _ => $"알 수 없는 연산자: {op}"
                };
            },
            name: "Calculate",
            description: "두 수의 사칙연산(+, -, *, /)을 수행합니다."
        );

        // ── Agent 초기화 (Tool 등록) ──────────────────────────────────
        // AsAIAgent()의 세 번째 위치 인수(tools)에 Tool 목록을 전달합니다.
        AIAgent agent = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey))
            .GetChatClient(deploymentName)
            .AsAIAgent(
                instructions: "당신은 유능한 AI 어시스턴트입니다. " +
                              "날씨, 시간, 계산이 필요하면 반드시 제공된 도구를 사용하고, 항상 한국어로 답하세요.",
                name: "ToolAgent",
                tools: [weatherTool, timeTool, calculatorTool]
            );

        // ── 테스트 질문 ──────────────────────────────────────────────
        var questions = new[]
        {
            "서울과 부산의 날씨를 알려주세요.",
            "지금 몇 시인가요?",
            "1234 곱하기 5678은 얼마인가요?",
            "제주도 날씨를 확인하고, 현재 시각도 알려주세요."  // 멀티 Tool 호출
        };

        try
        {
            foreach (var question in questions)
            {
                Console.WriteLine($"Q: {question}");
                Console.Write("A: ");

                // 스트리밍으로 출력 — Tool 호출/결과 처리는 프레임워크가 자동 수행
                await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(question))
                {
                    Console.Write(update.Text);
                }

                Console.WriteLine();
                Console.WriteLine(new string('-', 60));
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 401)
        {
            Console.Error.WriteLine($"[오류] 인증 실패: API 키를 확인하세요. ({ex.Message})");
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            Console.Error.WriteLine($"[오류] 배포 '{deploymentName}'을 찾을 수 없습니다. ({ex.Message})");
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
