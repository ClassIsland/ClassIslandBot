using System.Text.Json;
using Octokit.GraphQL;
using Octokit.GraphQL.Model;
using OpenAI;
using OpenAI.Chat;

namespace ClassIslandBot.Services;

public class IssueLabelService
{
    public OpenAIClient OpenAiClient { get; }
    public IConfiguration Configuration { get; }
    public GithubOperationService GithubOperationService { get; }
    public ILogger<IssueLabelService> Logger { get; }

    public ChatClient ChatClient { get; }

    private const string Prompt = 
        """
        接下来我会输入一个 Issue 的内容，你需要根据输入的内容，从接下来我提供的标签中给这个 Issue 打上适合的标签。
        
        ## 要求
        - 输出是一个只包含由标签名组成的 JSON 字符串数组，不要包含其它内容，不要用代码块包裹。
        - 按原样输出标签名，不要做任何修改。
        - 选择最能精确描述输入 Issue 的 1 ~ 2 个标签，标签名应尽可能少，最多不得超过 4 个标签。如果没有适合的标签，请返回空数组。
        
        ## 标签
        
        你能且只能使用以下标签：
        
        - IPC
        - UI/UX
        - Web
        - WPF
        - 主界面
        - 主题
        - 主题色提取
        - 代码
        - 天气
        - 导入/导出
        - 应用设置
        - 托盘菜单
        - 换课
        - 提醒
        - 插件
        - 插件市场
        - 日志
        - 更新
        - 档案 - 时间表
        - 档案 - 课表
        - 档案 - 课表启用
        - 档案 - 课表群
        - 档案编辑器
        - 欢迎向导
        - 精确时间服务
        - 系统
        - 组件
        - 组件 - 倒计时日
        - 组件 - 课程表
        - 组件 - 轮播组件
        - 自动化
        - 行动
        - 规则集
        - 课程服务
        - 配置
        - 附加设置
        - 集控                          
        """;
    
    
    public IssueLabelService(OpenAIClient openAiClient, IConfiguration configuration, GithubOperationService githubOperationService, ILogger<IssueLabelService> logger)
    {
        OpenAiClient = openAiClient;
        Configuration = configuration;
        GithubOperationService = githubOperationService;
        Logger = logger;

        ChatClient = OpenAiClient.GetChatClient(Configuration["OpenAIChatModelName"]);
    }

    public async Task LabelIssueAsync(string body, ID issueId, ID repoId)
    {
        var response = await ChatClient.CompleteChatAsync(
            new SystemChatMessage(Prompt),
            new UserChatMessage(body)
        );
        Logger.LogInformation("Getting labels for issue {}", issueId);
        var labelsJson = string.Join("", response.Value.Content.Select(x => x.Text));
        Logger.LogInformation("Labels for issue {}: {}", issueId, labelsJson);
        var success = false;
        try
        {
            var labels = JsonSerializer.Deserialize<string[]>(labelsJson)!;
            foreach (var i in labels)
            {
                await GithubOperationService.AddLabelByNameAsync(issueId, "类别：" + i, repoId);
                success = true;
            }
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Unable to process result：{}", labelsJson);
        }
        if (!success)
        {
            await GithubOperationService.AddLabelByNameAsync(issueId, "需要人工分类", repoId);
        }
    }
}