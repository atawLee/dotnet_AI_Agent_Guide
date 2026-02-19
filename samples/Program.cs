// ============================================================
// AgentSamples — 진입점
// 파일: samples/Program.cs
//
// 사용법:
//   dotnet run -- 01        # 01_HelloAgent 실행
//   dotnet run -- 02        # 02_AddTools 실행
//   dotnet run -- 03        # 03_MultiTurn 실행
//   dotnet run -- 04        # 04_Memory 실행
//   dotnet run -- 05        # 05_RAG 실행
//   dotnet run -- 06        # 06_MultiAgent 실행
//   dotnet run -- 07        # 07_Middleware 실행
//   dotnet run              # 사용법 출력
// ============================================================

using Microsoft.Extensions.Configuration;

// ── 설정 빌드 (모든 샘플이 공유) ──────────────────────────────
IConfiguration config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.local.json", optional: true)   // 로컬 전용 (Git 제외)
    .AddEnvironmentVariables()                                // CI/CD 폴백
    .Build();

// ── 샘플 선택 ─────────────────────────────────────────────────
var sample = args.FirstOrDefault() ?? string.Empty;

switch (sample)
{
    case "01":
        await HelloAgent.RunAsync(config);
        break;
    case "02":
        await AddTools.RunAsync(config);
        break;
    // 이후 샘플은 작성 후 추가됩니다.
    case "03":
        await MultiTurn.RunAsync(config);
        break;
    // case "04": await Memory.RunAsync(config); break;
    // case "05": await RAG.RunAsync(config); break;
    // case "06": await MultiAgent.RunAsync(config); break;
    // case "07": await Middleware.RunAsync(config); break;
    default:
        Console.WriteLine("사용법: dotnet run -- <샘플 번호>");
        Console.WriteLine();
        Console.WriteLine("  01  Hello Agent          — 첫 번째 Agent (단일/스트리밍 응답)");
        Console.WriteLine("  02  Function Calling     — Tool 등록 (날씨/시각/계산기)");
        Console.WriteLine("  03  Multi-Turn           — 대화 컨텍스트 유지 (예정)");
        Console.WriteLine("  04  Memory               — 세션 메모리 (예정)");
        Console.WriteLine("  05  RAG                  — 검색 증강 생성 (예정)");
        Console.WriteLine("  06  Multi-Agent          — 에이전트 협업 (예정)");
        Console.WriteLine("  07  Middleware           — 관측성 & 로깅 (예정)");
        break;
}
