// run node postinstall.js to update to latest version

using System.Text;
using Markdig;
using Markdig.Parsers;
using Markdig.Parsers.Inlines;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Markdig.Extensions.CustomContainers;
using ServiceStack.IO;
using ServiceStack.Text;

namespace docs.unilake;

public class MarkdigConfig
{
    public static MarkdigConfig Instance { get; private set; } = new();
    public Action<MarkdownPipelineBuilder>? ConfigurePipeline { get; set; }
    public Action<ContainerExtensions> ConfigureContainers { get; set; } = x => x.AddBuiltInContainers();

    public static void Set(MarkdigConfig config)
    {
        Instance = config;
    }
}

public class MarkdownFileBase
{
    public string Path { get; set; } = default!;
    public string? Slug { get; set; }
    public string? Layout { get; set; }
    public string? FileName { get; set; }
    public string? HtmlFileName { get; set; }

    /// <summary>
    /// Whether to hide this document in Production
    /// </summary>
    public bool Draft { get; set; }

    public string? Title { get; set; }
    public string? Summary { get; set; }
    public string? Image { get; set; }
    public string? Author { get; set; }
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Date document is published. Documents with future Dates are only shown in Development 
    /// </summary>
    public DateTime? Date { get; set; }

    public string? Content { get; set; }
    public string? Url { get; set; }

    /// <summary>
    /// The rendered HTML of the Markdown
    /// </summary>
    public string? Preview { get; set; }

    public string? HtmlPage { get; set; }
    public int? WordCount { get; set; }
    public int? LineCount { get; set; }
    public string? Group { get; set; }
    public int? Order { get; set; }
    public DocumentMap? DocumentMap { get; set; }

    public FeatureAvailabilityInfo? FeatureAvailabilityInfo { get; set; }

    /// <summary>
    /// Update Markdown File to latest version
    /// </summary>
    /// <param name="newDoc"></param>
    public virtual void Update(MarkdownFileBase newDoc)
    {
        Layout = newDoc.Layout;
        Title = newDoc.Title;
        Summary = newDoc.Summary;
        Draft = newDoc.Draft;
        Image = newDoc.Image;
        Author = newDoc.Author;
        Tags = newDoc.Tags;
        Content = newDoc.Content;
        Url = newDoc.Url;
        Preview = newDoc.Preview;
        HtmlPage = newDoc.HtmlPage;
        WordCount = newDoc.WordCount;
        LineCount = newDoc.LineCount;
        Group = newDoc.Group;
        Order = newDoc.Order;
        DocumentMap = newDoc.DocumentMap;

        if (newDoc.Date != null)
            Date = newDoc.Date;
    }
}

public interface IMarkdownPages
{
    string Id { get; }
    List<MarkdownFileBase> GetAll();
}

public abstract class MarkdownPagesBase<T>(ILogger log, IWebHostEnvironment env, IVirtualFiles fs) : IMarkdownPages
    where T : MarkdownFileBase
{
    public abstract string Id { get; }
    public IVirtualFiles VirtualFiles => fs;

    public virtual MarkdownPipeline CreatePipeline()
    {
        var builder = new MarkdownPipelineBuilder()
            .UseYamlFrontMatter()
            .UseAdvancedExtensions()
            .UseCustomParagraph()
            .UseAutoLinkHeadings()
            .UseHeadingsMap()
            .UseCustomContainers(MarkdigConfig.Instance.ConfigureContainers);
        MarkdigConfig.Instance.ConfigurePipeline?.Invoke(builder);

        var pipeline = builder.Build();
        return pipeline;
    }

    public virtual List<T> Fresh(List<T> docs)
    {
        if (docs.IsEmpty())
            return docs;
        foreach (var doc in docs)
        {
            Fresh(doc);
        }

        return docs;
    }

    public virtual T? Fresh(T? doc)
    {
        // Ignore reloading source .md if run in production or as AppTask
        if (doc == null || !env.IsDevelopment() || AppTasks.IsRunAsAppTask())
            return doc;
        var newDoc = Load(doc.Path);
        doc.Update(newDoc);
        return doc;
    }

    public virtual T CreateMarkdownFile(string content, TextWriter writer, MarkdownPipeline? pipeline = null)
    {
        pipeline ??= CreatePipeline();

        var renderer = new Markdig.Renderers.HtmlRenderer(writer);
        pipeline.Setup(renderer);

        var document = Markdown.Parse(content, pipeline);
        renderer.Render(document);

        var block = document
            .Descendants<Markdig.Extensions.Yaml.YamlFrontMatterBlock>()
            .FirstOrDefault();

        var doc = block?
                      .Lines // StringLineGroup[]
                      .Lines // StringLine[]
                      .Select(x => $"{x}\n")
                      .ToList()
                      .Select(x => x.Replace("---", string.Empty))
                      .Where(x => !string.IsNullOrWhiteSpace(x))
                      .Select(x => KeyValuePairs.Create(x.LeftPart(':').Trim(), x.RightPart(':').Trim()))
                      .ToObjectDictionary()
                      .ConvertTo<T>()
                  ?? typeof(T).CreateInstance<T>();

        var featureAvailability = block?.Lines.Lines
            .Select(x => x.ToString().Trim())
            .SkipWhile(x => !x.StartsWith("feature_availability"))
            .Skip(1)
            .Where(x => new[] { "self_hosted", "cloud_hosted", "notes" }.Any(x.StartsWith))
            .Select(x => KeyValuePairs.Create(x.LeftPart(':').Trim(), x.RightPart(':').Trim()))
            .ToObjectDictionary();

        if (featureAvailability is { Count: > 0 })
            doc.FeatureAvailabilityInfo = new FeatureAvailabilityInfo(featureAvailability);

        doc.Tags = doc.Tags.Map(x => x.Trim());
        doc.Content = content;
        doc.DocumentMap = document.GetData(nameof(DocumentMap)) as DocumentMap;

        return doc;
    }

    public virtual T? Load(string path, MarkdownPipeline? pipeline = null)
    {
        var file = fs.GetFile(path)
                   ?? throw new FileNotFoundException(path.LastRightPart('/'));
        var content = file.ReadAllText();

        var writer = new StringWriter();

        var doc = CreateMarkdownFile(content, writer, pipeline);
        doc.Title ??= file.Name;

        doc.Path = file.VirtualPath;
        doc.FileName = file.Name;
        doc.Slug = doc.FileName.WithoutExtension().GenerateSlug();
        doc.Content = content;
        doc.WordCount = WordCount(content);
        doc.LineCount = LineCount(content);
        writer.Flush();
        doc.Preview = writer.ToString();
        doc.Date ??= file.LastModified;

        return doc;
    }

    public virtual bool IsVisible(T doc) =>
        env.IsDevelopment() || !doc.Draft && (doc.Date == null || doc.Date.Value <= DateTime.UtcNow);

    public int WordsPerMin { get; set; } = 225;
    public char[] WordBoundaries { get; set; } = { ' ', '.', '?', '!', '(', ')', '[', ']' };
    public virtual int WordCount(string str) => str.Split(WordBoundaries, StringSplitOptions.RemoveEmptyEntries).Length;
    public virtual int LineCount(string str) => str.CountOccurrencesOf('\n');
    public virtual int MinutesToRead(int? words) => (int)Math.Ceiling((words ?? 1) / (double)WordsPerMin);

    public virtual List<MarkdownFileBase> GetAll() => new();

    public virtual string? StripFrontmatter(string? content)
    {
        if (content == null)
            return null;
        var startPos = content.IndexOf("---", StringComparison.CurrentCulture);
        if (startPos == -1)
            return content;
        var endPos = content.IndexOf("---", startPos + 3, StringComparison.Ordinal);
        if (endPos == -1)
            return content;
        return content.Substring(endPos + 3).Trim();
    }

    public virtual MarkdownFileBase ToMetaDoc(T x, Action<MarkdownFileBase>? fn = null)
    {
        var to = new MarkdownFileBase
        {
            Slug = x.Slug,
            Title = x.Title,
            Summary = x.Summary,
            Date = x.Date,
            Tags = x.Tags,
            Author = x.Author,
            Image = x.Image,
            WordCount = x.WordCount,
            LineCount = x.LineCount,
            Url = x.Url,
            Group = x.Group,
            Order = x.Order,
        };
        fn?.Invoke(to);
        return to;
    }

    /// <summary>
    /// Need to escape '{{' and '}}' template literals when using content inside a Vue template
    /// </summary>
    public virtual string? SanitizeVueTemplate(string? content)
    {
        if (content == null)
            return null;

        return content
            .Replace("{{", "{‎{")
            .Replace("}}", "}‎}");
    }
}

public class MarkdownIncludes(ILogger<MarkdownIncludes> log, IWebHostEnvironment env, IVirtualFiles fs)
    : MarkdownPagesBase<MarkdownFileInfo>(log, env, fs)
{
    public override string Id => "includes";
    public List<MarkdownFileInfo> Pages { get; } = [];

    public void LoadFrom(string fromDirectory)
    {
        Pages.Clear();
        var files = VirtualFiles.GetDirectory(fromDirectory).GetAllFiles()
            .OrderBy(x => x.VirtualPath)
            .ToList();
        log.LogInformation("Found {Count} includes", files.Count);

        var pipeline = CreatePipeline();

        foreach (var file in files)
        {
            try
            {
                if (file.Extension == "md")
                {
                    var doc = Load(file.VirtualPath, pipeline);
                    if (doc == null)
                        continue;

                    var relativePath = file.VirtualPath[(fromDirectory.Length + 1)..];
                    if (relativePath.IndexOf('/') >= 0)
                    {
                        doc.Slug = relativePath.LastLeftPart('/') + '/' + doc.Slug;
                    }

                    Pages.Add(doc);
                }
            }
            catch (Exception e)
            {
                log.LogError(e, "Couldn't load {VirtualPath}: {Message}", file.VirtualPath, e.Message);
            }
        }
    }

    public override List<MarkdownFileBase> GetAll() =>
        Pages.Where(IsVisible).Map(doc => ToMetaDoc(doc, x => x.Url = $"/{x.Slug}"));
}

public struct HeadingInfo(int level, string id, string content)
{
    public int Level { get; } = level;
    public string Id { get; } = id;
    public string Content { get; } = content;
}

public class ParagraphRenderer : HtmlObjectRenderer<ParagraphBlock>
{
    protected override void Write(HtmlRenderer renderer, ParagraphBlock obj)
    {
        renderer.Write("<p class='text-xs leading-6 text-bodyText md:text-base lg:leading-7'");
        renderer.WriteAttributes(obj.GetAttributes());
        renderer.Write('>');
        renderer.WriteLeafInline(obj);
        renderer.Write("</p>");
    }
}

/// <summary>
/// An HTML renderer for a <see cref="HeadingBlock"/>.
/// </summary>
/// <seealso cref="HtmlObjectRenderer{TObject}" />
public class AutoLinkHeadingRenderer : HtmlObjectRenderer<HeadingBlock>
{
    private static readonly string[] HeadingTexts =
    [
        "h1",
        "h2",
        "h3",
        "h4",
        "h5",
        "h6"
    ];

    public event Action<HeadingBlock>? OnHeading;

    protected override void Write(HtmlRenderer renderer, HeadingBlock obj)
    {
        int index = obj.Level - 1;
        string[] headings = HeadingTexts;
        string headingText = ((uint)index < (uint)headings.Length)
            ? headings[index]
            : $"h{obj.Level}";

        var att = obj.GetAttributes();
        var sizes = index == 1 ? ("2xl:text-[32px]", "text-[24px]") :
            index == 2 ? ("2xl:text-[26px]", "text-[18px]") : ("2xl:text-[24px]", "text-[16px]");
        string classNames = $"{sizes.Item1} {sizes.Item2} text-headLines leading-9 tracking-[-0.96px] font-bold";
        att.Classes ??= new List<string>();
        att.Classes.Add(classNames);
        obj.SetAttributes(att);

        if (renderer.EnableHtmlForBlock)
        {
            renderer.Write('<');
            renderer.Write(headingText);
            renderer.WriteAttributes(obj);
            renderer.Write('>');
        }

        renderer.WriteLeafInline(obj);

        // var attrs = obj.TryGetAttributes();
        // if (attrs?.Id != null && obj.Level <= 4)
        // {
        //     renderer.Write("<a class=\"header-anchor\" onclick=\"location.hash='#");
        //     renderer.Write(attrs.Id);
        //     renderer.Write("'\" aria-label=\"Permalink\">&ZeroWidthSpace;</a>");
        // }

        if (renderer.EnableHtmlForBlock)
        {
            renderer.Write("</");
            renderer.Write(headingText);
            renderer.WriteLine('>');
        }

        renderer.EnsureLine();
        OnHeading?.Invoke(obj);
    }
}

public class AutoLinkHeadingsExtension : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
        renderer.ObjectRenderers.Replace<HeadingRenderer>(new AutoLinkHeadingRenderer());
    }
}

public class ParagraphRendererExtension : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
        renderer.ObjectRenderers.Replace<Markdig.Renderers.Html.ParagraphRenderer>(new ParagraphRenderer());
    }
}

public class FilesCodeBlockRenderer(CodeBlockRenderer? underlyingRenderer = null) : HtmlObjectRenderer<CodeBlock>
{
    private readonly CodeBlockRenderer underlyingRenderer = underlyingRenderer ?? new CodeBlockRenderer();

    protected override void Write(HtmlRenderer renderer, CodeBlock obj)
    {
        if (obj is not FencedCodeBlock fencedCodeBlock || obj.Parser is not FencedCodeBlockParser parser)
        {
            underlyingRenderer.Write(renderer, obj);
            return;
        }

        var attributes = obj.TryGetAttributes() ?? new HtmlAttributes();
        var languageMoniker = fencedCodeBlock.Info?.Replace(parser.InfoPrefix!, string.Empty);
        if (string.IsNullOrEmpty(languageMoniker))
        {
            underlyingRenderer.Write(renderer, obj);
            return;
        }

        var txt = GetContent(obj);
        renderer
            .Write("<div")
            .WriteAttributes(attributes)
            .Write(">");

        var dir = ParseFileStructure(txt);
        RenderNode(renderer, dir);
        renderer.WriteLine("</div>");
    }

    private static string GetContent(LeafBlock obj)
    {
        var code = new StringBuilder();
        foreach (var line in obj.Lines.Lines)
        {
            var slice = line.Slice;
            if (slice.Text == null)
                continue;

            var lineText = slice.Text.Substring(slice.Start, slice.Length);
            code.AppendLine();
            code.Append(lineText);
        }

        return code.ToString();
    }

    public class Node
    {
        public List<string> Files { get; set; } = [];
        public Dictionary<string, Node> Directories { get; set; } = new();
    }

    public void RenderNode(HtmlRenderer html, Node model)
    {
        foreach (var (dirName, childNode) in model.Directories)
        {
            html.WriteLine("<div class=\"ml-6\">");
            html.WriteLine("  <div class=\"flex items-center text-base leading-8\">");
            html.WriteLine(
                "    <svg class=\"mr-1 text-slate-600 inline-block select-none align-text-bottom overflow-visible\" aria-hidden=\"true\" focusable=\"false\" role=\"img\" viewBox=\"0 0 12 12\" width=\"12\" height=\"12\" fill=\"currentColor\"><path d=\"M6 8.825c-.2 0-.4-.1-.5-.2l-3.3-3.3c-.3-.3-.3-.8 0-1.1.3-.3.8-.3 1.1 0l2.7 2.7 2.7-2.7c.3-.3.8-.3 1.1 0 .3.3.3.8 0 1.1l-3.2 3.2c-.2.2-.4.3-.6.3Z\"></path></svg>");
            html.WriteLine(
                "    <svg class=\"mr-1 text-sky-500\" aria-hidden=\"true\" focusable=\"false\" role=\"img\" viewBox=\"0 0 16 16\" width=\"16\" height=\"16\" fill=\"currentColor\"><path d=\"M.513 1.513A1.75 1.75 0 0 1 1.75 1h3.5c.55 0 1.07.26 1.4.7l.9 1.2a.25.25 0 0 0 .2.1H13a1 1 0 0 1 1 1v.5H2.75a.75.75 0 0 0 0 1.5h11.978a1 1 0 0 1 .994 1.117L15 13.25A1.75 1.75 0 0 1 13.25 15H1.75A1.75 1.75 0 0 1 0 13.25V2.75c0-.464.184-.91.513-1.237Z\"></path></svg>");
            html.WriteLine("    <span>" + dirName + "</span>");
            html.WriteLine("  </div>");
            RenderNode(html, childNode);
            html.WriteLine("</div>");
        }

        if (model.Files.Count > 0)
        {
            html.WriteLine("<div>");
            foreach (var file in model.Files)
            {
                html.WriteLine("<div class=\"ml-6 flex items-center text-base leading-8\">");
                html.WriteLine(
                    "  <svg class=\"mr-1 text-slate-600 inline-block select-none align-text-bottom overflow-visible\" aria-hidden=\"true\" focusable=\"false\" role=\"img\" viewBox=\"0 0 16 16\" width=\"16\" height=\"16\" fill=\"currentColor\"><path d=\"M2 1.75C2 .784 2.784 0 3.75 0h6.586c.464 0 .909.184 1.237.513l2.914 2.914c.329.328.513.773.513 1.237v9.586A1.75 1.75 0 0 1 13.25 16h-9.5A1.75 1.75 0 0 1 2 14.25Zm1.75-.25a.25.25 0 0 0-.25.25v12.5c0 .138.112.25.25.25h9.5a.25.25 0 0 0 .25-.25V6h-2.75A1.75 1.75 0 0 1 9 4.25V1.5Zm6.75.062V4.25c0 .138.112.25.25.25h2.688l-.011-.013-2.914-2.914-.013-.011Z\"></path></svg>");
                html.WriteLine("  <span>" + file + "</span>");
                html.WriteLine("</div>");
            }

            html.WriteLine("</div>");
        }
    }

    public static Node ParseFileStructure(string ascii, int indent = 2)
    {
        var lines = ascii.Trim().Split('\n').Where(x => x.Trim().Length > 0);
        var root = new Node();
        var stack = new Stack<Node>();
        stack.Push(root);

        foreach (var line in lines)
        {
            var depth = line.TakeWhile(char.IsWhiteSpace).Count() / indent;
            var name = line.Trim();
            var isDir = name.StartsWith('/');

            while (stack.Count > depth + 1)
                stack.Pop();

            var parent = stack.Peek();
            if (isDir)
            {
                var dirName = name.Substring(1);
                var dirContents = new Node();
                parent.Directories[dirName] = dirContents;
                stack.Push(dirContents);
            }
            else
            {
                parent.Files.Add(name);
            }
        }

        return root;
    }
}

public class CopyContainerRenderer : HtmlObjectRenderer<CustomContainer>
{
    public string Class { get; set; } = "";
    public string BoxClass { get; set; } = "bg-gray-700";
    public string IconClass { get; set; } = "";
    public string TextClass { get; set; } = "text-lg text-white";

    protected override void Write(HtmlRenderer renderer, CustomContainer obj)
    {
        renderer.EnsureLine();
        if (renderer.EnableHtmlForBlock)
        {
            renderer.Write(@$"<div class=""{Class} flex cursor-pointer mb-3"" onclick=""copy(this)"">
                <div class=""flex-grow {BoxClass}"">
                    <div class=""pl-4 py-1 pb-1.5 align-middle {TextClass}"">");
        }

        // We don't escape a CustomContainer
        renderer.WriteChildren(obj);
        if (renderer.EnableHtmlForBlock)
        {
            renderer.WriteLine(@$"</div>
                    </div>
                <div class=""flex"">
                    <div class=""{IconClass} text-white p-1.5 pb-0"">
                        <svg class=""copied w-6 h-6"" fill=""none"" stroke=""currentColor"" viewBox=""0 0 24 24"" xmlns=""http://www.w3.org/2000/svg""><path stroke-linecap=""round"" stroke-linejoin=""round"" stroke-width=""2"" d=""M5 13l4 4L19 7""></path></svg>
                        <svg class=""nocopy w-6 h-6"" title=""copy"" fill='none' stroke='white' viewBox='0 0 24 24' xmlns='http://www.w3.org/2000/svg'>
                            <path stroke-linecap='round' stroke-linejoin='round' stroke-width='1' d='M8 7v8a2 2 0 002 2h6M8 7V5a2 2 0 012-2h4.586a1 1 0 01.707.293l4.414 4.414a1 1 0 01.293.707V15a2 2 0 01-2 2h-2M8 7H6a2 2 0 00-2 2v10a2 2 0 002 2h8a2 2 0 002-2v-2'></path>
                        </svg>
                    </div>
                </div>
            </div>");
        }
    }
}

public class CustomInfoRenderer : HtmlObjectRenderer<CustomContainer>
{
    public string Title { get; set; } = "TIP";
    public string Class { get; set; } = "tip";

    private static Dictionary<string, (string, string, string)> _settings = new()
    {
        {
            "info", ("text-[#619DFF]", "bg-[#619DFF]", string.Empty)
        },
        {
            "tip", ("text-[#A4CD80]", "bg-[#A4CD80]", """
                                                      <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 16 16"
                                                          fill="none">
                                                      <path fill-rule="evenodd" clip-rule="evenodd"
                                                        d="M8 1.83337C7.35812 1.83337 6.74664 1.95823 6.18761 2.18452C5.93165 2.28813 5.64015 2.16463 5.53654 1.90866C5.43292 1.65269 5.55643 1.3612 5.8124 1.25758C6.48861 0.983859 7.22735 0.833374 8 0.833374C11.2217 0.833374 13.8333 3.44505 13.8333 6.66671C13.8333 8.89963 12.1998 10.581 11.1933 11.4137C10.7637 11.7692 10.5 12.2581 10.5 12.7512C10.5 14.0853 9.41856 15.1667 8.08453 15.1667H7.92513C6.58577 15.1667 5.5 14.0809 5.5 12.7416C5.5 12.2509 5.24113 11.765 4.81768 11.4107C3.81536 10.5721 2.16667 8.87316 2.16667 6.66671C2.16667 5.28769 2.64582 4.01921 3.4463 3.02064C3.61902 2.80518 3.9337 2.77053 4.14916 2.94325C4.36462 3.11597 4.39927 3.43065 4.22655 3.64611C3.56315 4.47367 3.16667 5.5232 3.16667 6.66671C3.16667 8.39121 4.48638 9.82971 5.45937 10.6438C5.75262 10.8891 6.00735 11.193 6.19138 11.5381C6.20098 11.5421 6.21051 11.5464 6.21996 11.551C6.22075 11.5514 6.22154 11.5518 6.22233 11.5522L6.21996 11.551C6.2213 11.5516 6.22525 11.5535 6.23166 11.5563C6.24449 11.5619 6.26745 11.5715 6.30057 11.584C6.36677 11.6088 6.47366 11.6447 6.62127 11.6816C6.91617 11.7554 7.37546 11.8334 8 11.8334C8.27615 11.8334 8.5 12.0572 8.5 12.3334C8.5 12.6095 8.27615 12.8334 8 12.8334C7.36436 12.8334 6.8639 12.7621 6.49924 12.6803C6.49975 12.7007 6.5 12.7211 6.5 12.7416C6.5 13.5287 7.13806 14.1667 7.92513 14.1667H8.08453C8.86627 14.1667 9.5 13.533 9.5 12.7512C9.5 11.8987 9.94861 11.1457 10.5558 10.6433C11.5246 9.84173 12.8333 8.4198 12.8333 6.66671C12.8333 3.99733 10.6694 1.83337 8 1.83337ZM6.22327 11.5527C6.22323 11.5526 6.22331 11.5527 6.22327 11.5527V11.5527Z"
                                                        fill="#6B9E32"/>
                                                      </svg>
                                                      """)
        },
        {
            "warning", ("text-[#F79009]", "bg-[#F79009]", """
                                                          <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 16 16"
                                                               fill="none">
                                                              <g clip-path="url(#clip0_536_1825)">
                                                                  <path fill-rule="evenodd" clip-rule="evenodd"
                                                                        d="M9.1705 2.15142C8.42921 1.81671 7.57983 1.81671 6.83854 2.15142C6.46645 2.31944 6.09047 2.67757 5.59928 3.39251C5.1101 4.10453 4.55267 5.0989 3.78741 6.46545L3.5534 6.88331C2.81764 8.19718 2.28317 9.15262 1.94451 9.91295C1.60498 10.6753 1.49766 11.168 1.54329 11.5669C1.63397 12.3596 2.05462 13.0772 2.70191 13.5436C3.02768 13.7784 3.51001 13.9255 4.34103 14.0017C5.16989 14.0777 6.26466 14.0782 7.77052 14.0782H8.23853C9.74439 14.0782 10.8392 14.0777 11.668 14.0017C12.499 13.9255 12.9814 13.7784 13.3071 13.5436C13.9544 13.0772 14.3751 12.3596 14.4658 11.5669C14.5114 11.168 14.4041 10.6753 14.0645 9.91295C13.7259 9.15262 13.1914 8.19718 12.4556 6.8833L12.2216 6.46544C11.4564 5.0989 10.8989 4.10452 10.4098 3.39251C9.91858 2.67757 9.5426 2.31944 9.1705 2.15142ZM6.42702 1.24002C7.42994 0.787179 8.5791 0.787179 9.58202 1.24002C10.2103 1.52369 10.7147 2.07035 11.234 2.82624C11.7522 3.58056 12.3316 4.61521 13.0807 5.95288L13.3414 6.41843C14.0612 7.70365 14.6185 8.69887 14.978 9.50608C15.3389 10.3163 15.5359 11.0106 15.4593 11.6806C15.3366 12.753 14.7675 13.7239 13.8917 14.355C13.3447 14.7491 12.6426 14.9165 11.7594 14.9975C10.8794 15.0782 9.73877 15.0782 8.26577 15.0782H7.74328C6.27028 15.0782 5.12965 15.0782 4.24969 14.9975C3.36647 14.9165 2.66437 14.7491 2.11731 14.355C1.24157 13.7239 0.672448 12.753 0.549773 11.6806C0.47314 11.0106 0.670165 10.3163 1.03103 9.50608C1.39056 8.69888 1.94788 7.70367 2.66759 6.41846L2.92829 5.95294C3.6774 4.61524 4.2568 3.58058 4.77506 2.82624C5.29438 2.07035 5.79879 1.52369 6.42702 1.24002ZM8.00452 5.41156C8.28066 5.41156 8.50452 5.63542 8.50452 5.91156V8.57823C8.50452 8.85437 8.28066 9.07823 8.00452 9.07823C7.72838 9.07823 7.50452 8.85437 7.50452 8.57823V5.91156C7.50452 5.63542 7.72838 5.41156 8.00452 5.41156ZM7.17119 10.9116C7.17119 10.4513 7.54428 10.0782 8.00452 10.0782C8.46476 10.0782 8.83786 10.4513 8.83786 10.9116C8.83786 11.3718 8.46476 11.7449 8.00452 11.7449C7.54428 11.7449 7.17119 11.3718 7.17119 10.9116Z"
                                                                        fill="#F79009"/>
                                                              </g>
                                                              <defs>
                                                                  <clipPath id="clip0_536_1825">
                                                                      <rect width="16" height="16" fill="white"/>
                                                                  </clipPath>
                                                              </defs>
                                                          </svg>
                                                          """)
        },
        {
            "danger", ("text-[#FF7D87]", "bg-[#FF7D87]", """
                                                              <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 16 16" fill="none">
                                                                  <g clip-path="url(#clip0_536_1272)">
                                                                      <path fill-rule="evenodd" clip-rule="evenodd" d="M7.99998 1.83398C4.59422 1.83398 1.83331 4.5949 1.83331 8.00065C1.83331 11.4064 4.59422 14.1673 7.99998 14.1673C11.4057 14.1673 14.1666 11.4064 14.1666 8.00065C14.1666 4.5949 11.4057 1.83398 7.99998 1.83398ZM0.833313 8.00065C0.833313 4.04261 4.04194 0.833984 7.99998 0.833984C11.958 0.833984 15.1666 4.04261 15.1666 8.00065C15.1666 11.9587 11.958 15.1673 7.99998 15.1673C4.04194 15.1673 0.833313 11.9587 0.833313 8.00065ZM5.64643 5.6471C5.84169 5.45184 6.15827 5.45184 6.35353 5.6471L7.99998 7.29354L9.64643 5.6471C9.84169 5.45184 10.1583 5.45184 10.3535 5.6471C10.5488 5.84236 10.5488 6.15894 10.3535 6.35421L8.70709 8.00065L10.3535 9.6471C10.5488 9.84236 10.5488 10.1589 10.3535 10.3542C10.1583 10.5495 9.84169 10.5495 9.64643 10.3542L7.99998 8.70776L6.35353 10.3542C6.15827 10.5495 5.84169 10.5495 5.64643 10.3542C5.45116 10.1589 5.45116 9.84236 5.64643 9.6471L7.29287 8.00065L5.64643 6.35421C5.45116 6.15894 5.45116 5.84236 5.64643 5.6471Z" fill="#EA0044"></path>
                                                                  </g>
                                                                  <defs>
                                                                      <clipPath id="clip0_536_1272">
                                                                          <rect width="16" height="16" fill="white"></rect>
                                                                      </clipPath>
                                                                  </defs>
                                                              </svg>
                                                         """)
        },
        { "quote", ("text-bodyText", "bg-brand", string.Empty) }
    };

    protected override void Write(HtmlRenderer renderer, CustomContainer obj)
    {
        renderer.EnsureLine();
        if (renderer.EnableHtmlForBlock)
        {
            var title = obj.Arguments ?? obj.Info;
            if (string.IsNullOrEmpty(title))
                title = Title;

            var settings = _settings[Class];
            renderer.Write($"""
                            <div class="flex items-start gap-4 py-2 pr-6 mt-4 rounded-lg ">
                                 <div class="w-1 2xl:mr-11 mr-5 shrink-0 self-stretch {settings.Item2}"></div>
                                 <div class="w-full text-base leading-7 text-bodyText">
                                     <div class="flex items-center gap-2 mb-3">
                                     {settings.Item3}
                                         <span class="{settings.Item1}">{title}</span>
                                     </div>
                            """);
        }

        // We don't escape a CustomContainer
        renderer.WriteChildren(obj);
        if (renderer.EnableHtmlForBlock)
        {
            renderer.WriteLine("</div>");
            renderer.WriteLine("</div>");
        }
    }
}

/// <summary>
/// Render HTML-encoded inline contents inside a &gt;pre class="pre"/&lt;
/// </summary>
public class PreContainerRenderer : HtmlObjectRenderer<CustomContainer>
{
    public string Class { get; set; } = "pre";

    protected override void Write(HtmlRenderer renderer, CustomContainer obj)
    {
        renderer.EnsureLine();
        if (renderer.EnableHtmlForBlock)
        {
            var attrs = obj.TryGetAttributes();
            if (attrs != null && attrs.Classes.IsEmpty())
            {
                attrs.Classes ??= new();
                attrs.Classes.Add(Class);
            }

            renderer.Write("<pre").WriteAttributes(obj).Write('>');
            renderer.WriteLine();
        }

        if (obj.FirstOrDefault() is LeafBlock leafBlock)
        {
            // There has to be an official API to resolve the original text from a renderer?
            string? FindOriginalText(ContainerBlock? block)
            {
                if (block != null)
                {
                    if (block.FirstOrDefault(x => x is LeafBlock { Lines.Count: > 0 }) is LeafBlock first)
                        return first.Lines.Lines[0].Slice.Text;
                    return FindOriginalText(block.Parent);
                }

                return null;
            }

            var originalSource = leafBlock.Lines.Count > 0
                ? leafBlock.Lines.Lines[0].Slice.Text
                : FindOriginalText(obj.Parent);
            if (originalSource == null)
            {
                HostContext.Resolve<ILogger<PreContainerRenderer>>().LogError("Could not find original Text");
                renderer.WriteLine($"Could not find original Text");
            }
            else
            {
                renderer.WriteEscape(originalSource.AsSpan().Slice(leafBlock.Span.Start, leafBlock.Span.Length));
            }
        }
        else
        {
            renderer.WriteChildren(obj);
        }

        if (renderer.EnableHtmlForBlock)
        {
            renderer.WriteLine("</pre>");
        }
    }
}

public class IncludeContainerInlineRenderer : HtmlObjectRenderer<CustomContainerInline>
{
    protected override void Write(HtmlRenderer renderer, CustomContainerInline obj)
    {
        var include = obj.FirstChild is LiteralInline literalInline
            ? literalInline.Content.AsSpan().RightPart(' ').ToString()
            : null;
        if (string.IsNullOrEmpty(include))
            return;

        renderer.Write("<div").WriteAttributes(obj).Write('>');
        MarkdownFileBase? doc = null;
        if (include.EndsWith(".md"))
        {
            var includes = HostContext.TryResolve<MarkdownIncludes>();
            var pages = HostContext.TryResolve<MarkdownPages>();
            // default relative path to _includes/
            include = include[0] != '/'
                ? "_includes/" + include
                : include.TrimStart('/');

            doc = includes?.Pages.FirstOrDefault(x => x.Path == include);
            if (doc == null && pages != null)
            {
                var prefix = include.LeftPart('/');
                var slug = include.LeftPart('.');
                var allIncludes = pages.GetVisiblePages(prefix, allDirectories: true);
                doc = allIncludes.FirstOrDefault(x => x.Slug == slug);
            }
        }

        if (doc?.Preview != null)
        {
            renderer.WriteLine(doc.Preview!);
        }
        else
        {
            var log = HostContext.Resolve<ILogger<IncludeContainerInlineRenderer>>();
            log.LogError("Could not find: {Include}", include);
            renderer.WriteLine($"Could not find: {include}");
        }

        renderer.Write("</div>");
    }
}

public class YouTubeContainerInlineRenderer : HtmlObjectRenderer<CustomContainerInline>
{
    public string? ContainerClass { get; set; } = "flex justify-center";
    public string? Class { get; set; } = "w-full mx-4 my-4";

    protected override void Write(HtmlRenderer renderer, CustomContainerInline obj)
    {
        var videoId = obj.FirstChild is LiteralInline literalInline
            ? literalInline.Content.AsSpan().RightPart(' ').ToString()
            : null;
        if (string.IsNullOrEmpty(videoId))
            return;

        if (ContainerClass != null) renderer.WriteLine($"<div class=\"{ContainerClass}\">");
        renderer.WriteLine(
            $"<lite-youtube class=\"{Class}\" width=\"560\" height=\"315\" videoid=\"{videoId}\" style=\"background-image:url('https://img.youtube.com/vi/{videoId}/maxresdefault.jpg')\"></lite-youtube>");
        if (ContainerClass != null) renderer.WriteLine("</div>");
    }
}

public class YouTubeContainerRenderer : HtmlObjectRenderer<CustomContainer>
{
    public string? ContainerClass { get; set; } // = "flex justify-center";
    public string? Class { get; set; } = "w-full mx-4 my-4";

    protected override void Write(HtmlRenderer renderer, CustomContainer obj)
    {
        renderer.EnsureLine();

        var videoId = (obj.Arguments ?? "").TrimEnd(':');
        if (string.IsNullOrEmpty(videoId))
        {
            renderer.WriteLine("<!-- youtube: Missing YouTube Video Id -->");
            return;
        }

        var title = ((obj.Count > 0 ? obj[0] as ParagraphBlock : null)?.Inline?.FirstChild as LiteralInline)?.Content
            .ToString() ?? "";
        if (ContainerClass != null) renderer.WriteLine($"<div class=\"{ContainerClass}\">");
        renderer.WriteLine(
            $"<lite-youtube class=\"{Class}\" width=\"560\" height=\"315\" videoid=\"{videoId}\" playlabel=\"{title}\" style=\"background-image:url('https://img.youtube.com/vi/{videoId}/maxresdefault.jpg')\"></lite-youtube>");
        if (ContainerClass != null) renderer.WriteLine("</div>");
    }
}

public class CustomCodeBlockRenderers(ContainerExtensions extensions, CodeBlockRenderer? underlyingRenderer = null)
    : HtmlObjectRenderer<CodeBlock>
{
    protected override void Write(HtmlRenderer renderer, CodeBlock obj)
    {
        var useRenderer = obj is FencedCodeBlock { Info: not null } f &&
                          extensions.CodeBlocks.TryGetValue(f.Info, out var customRenderer)
            ? customRenderer(underlyingRenderer)
            : underlyingRenderer ?? new CodeBlockRenderer();
        useRenderer.Write(renderer, obj);
    }
}

public class CustomContainerRenderers(ContainerExtensions extensions) : HtmlObjectRenderer<CustomContainer>
{
    protected override void Write(HtmlRenderer renderer, CustomContainer obj)
    {
        var useRenderer = obj.Info != null && extensions.BlockContainers.TryGetValue(obj.Info, out var customRenderer)
            ? customRenderer
            : new HtmlCustomContainerRenderer();
        useRenderer.Write(renderer, obj);
    }
}

public class CustomContainerInlineRenderers(ContainerExtensions extensions) : HtmlObjectRenderer<CustomContainerInline>
{
    protected override void Write(HtmlRenderer renderer, CustomContainerInline obj)
    {
        var firstWord = obj.FirstChild is LiteralInline literalInline
            ? literalInline.Content.AsSpan().LeftPart(' ').ToString()
            : null;
        var useRenderer =
            firstWord != null && extensions.InlineContainers.TryGetValue(firstWord, out var customRenderer)
                ? customRenderer
                : new HtmlCustomContainerInlineRenderer();
        useRenderer.Write(renderer, obj);
    }
}

public class ContainerExtensions : IMarkdownExtension
{
    public Dictionary<string, Func<CodeBlockRenderer?, HtmlObjectRenderer<CodeBlock>>> CodeBlocks { get; set; } = new();
    public Dictionary<string, HtmlObjectRenderer<CustomContainer>> BlockContainers { get; set; } = new();
    public Dictionary<string, HtmlObjectRenderer<CustomContainerInline>> InlineContainers { get; set; } = new();

    public void AddCodeBlock(string name, Func<CodeBlockRenderer?, HtmlObjectRenderer<CodeBlock>> fenceCodeBlock) =>
        CodeBlocks[name] = fenceCodeBlock;

    public void AddBlockContainer(string name, HtmlObjectRenderer<CustomContainer> container) =>
        BlockContainers[name] = container;

    public void AddInlineContainer(string name, HtmlObjectRenderer<CustomContainerInline> container) =>
        InlineContainers[name] = container;

    public void AddBuiltInContainers(string[]? exclude = null)
    {
        CodeBlocks = new()
        {
            ["files"] = origRenderer => new FilesCodeBlockRenderer(origRenderer)
        };
        BlockContainers = new()
        {
            ["copy"] = new CopyContainerRenderer
            {
                Class = "not-prose copy cp",
                IconClass = "bg-sky-500",
            },
            ["sh"] = new CopyContainerRenderer
            {
                Class = "not-prose sh-copy cp",
                BoxClass = "bg-gray-800",
                IconClass = "bg-green-600",
                TextClass = "whitespace-pre text-base text-gray-100",
            },
            ["tip"] = new CustomInfoRenderer(),
            ["info"] = new CustomInfoRenderer
            {
                Class = "info",
                Title = "INFO",
            },
            ["warning"] = new CustomInfoRenderer
            {
                Class = "warning",
                Title = "WARNING",
            },
            ["danger"] = new CustomInfoRenderer
            {
                Class = "danger",
                Title = "DANGER",
            },
            ["pre"] = new PreContainerRenderer(),
            ["youtube"] = new YouTubeContainerRenderer(),
        };
        InlineContainers = new()
        {
            ["include"] = new IncludeContainerInlineRenderer(),
            ["youtube"] = new YouTubeContainerInlineRenderer(),
        };

        if (exclude != null)
        {
            foreach (var name in exclude)
            {
                BlockContainers.TryRemove(name, out _);
                InlineContainers.TryRemove(name, out _);
            }
        }
    }

    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        if (!pipeline.BlockParsers.Contains<CustomContainerParser>())
        {
            // Insert the parser before any other parsers
            pipeline.BlockParsers.Insert(0, new CustomContainerParser());
        }

        // Plug the inline parser for CustomContainerInline
        var inlineParser = pipeline.InlineParsers.Find<EmphasisInlineParser>();
        if (inlineParser != null && !inlineParser.HasEmphasisChar(':'))
        {
            inlineParser.EmphasisDescriptors.Add(new EmphasisDescriptor(':', 2, 2, true));
            inlineParser.TryCreateEmphasisInlineList.Add((emphasisChar, delimiterCount) =>
            {
                if (delimiterCount >= 2 && emphasisChar == ':')
                {
                    return new CustomContainerInline();
                }

                return null;
            });
        }
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
        if (renderer is HtmlRenderer htmlRenderer)
        {
            var originalCodeBlockRenderer = htmlRenderer.ObjectRenderers.FindExact<CodeBlockRenderer>();
            if (originalCodeBlockRenderer != null)
            {
                htmlRenderer.ObjectRenderers.Remove(originalCodeBlockRenderer);
            }

            htmlRenderer.ObjectRenderers.AddIfNotAlready(
                new CustomCodeBlockRenderers(this, originalCodeBlockRenderer));

            if (!htmlRenderer.ObjectRenderers.Contains<CustomContainerRenderers>())
            {
                // Must be inserted before CodeBlockRenderer
                htmlRenderer.ObjectRenderers.Insert(0, new CustomContainerRenderers(this));
            }

            htmlRenderer.ObjectRenderers.TryRemove<HtmlCustomContainerInlineRenderer>();
            // Must be inserted before EmphasisRenderer
            htmlRenderer.ObjectRenderers.Insert(0, new CustomContainerInlineRenderers(this));
        }
    }
}

public class HeadingsMapExtension : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        var headingBlockParser = pipeline.BlockParsers.Find<HeadingBlockParser>();
        if (headingBlockParser != null)
        {
            // Install a hook on the HeadingBlockParser when a HeadingBlock is actually processed
            // headingBlockParser.Closed -= HeadingBlockParser_Closed;
            // headingBlockParser.Closed += HeadingBlockParser_Closed;
        }
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
        if (renderer.ObjectRenderers.TryFind<AutoLinkHeadingRenderer>(out var customHeader))
        {
            customHeader.OnHeading += OnHeading;
        }
    }

    private void OnHeading(HeadingBlock headingBlock)
    {
        if (headingBlock.Parent is not MarkdownDocument document)
            return;

        if (document.GetData(nameof(DocumentMap)) is not DocumentMap docMap)
        {
            docMap = new();
            document.SetData(nameof(DocumentMap), docMap);
        }

        var text = headingBlock.Inline?.FirstChild is LiteralInline literalInline
            ? literalInline.ToString()
            : null;
        var attrs = headingBlock.TryGetAttributes();

        if (!string.IsNullOrEmpty(text) && attrs?.Id != null)
        {
            if (headingBlock.Level == 2)
            {
                docMap.Headings.Add(new MarkdownMenu
                {
                    Text = text,
                    Link = $"#{attrs.Id}",
                    Id = attrs.Id
                });
            }
            else if (headingBlock.Level == 3)
            {
                var lastHeading = docMap.Headings.LastOrDefault();
                if (lastHeading != null)
                {
                    lastHeading.Children ??= new();
                    lastHeading.Children.Add(new MarkdownMenu
                    {
                        Text = text,
                        Link = $"#{attrs.Id}",
                        Id = attrs.Id
                    });
                }
            }
        }
    }
}

public static class MarkdigExtensions
{
    /// <summary>
    /// Uses the auto-identifier extension.
    /// </summary>
    public static MarkdownPipelineBuilder UseAutoLinkHeadings(this MarkdownPipelineBuilder pipeline)
    {
        pipeline.Extensions.AddIfNotAlready(new AutoLinkHeadingsExtension());
        return pipeline;
    }

    public static MarkdownPipelineBuilder UseCustomParagraph(this MarkdownPipelineBuilder pipeline)
    {
        pipeline.Extensions.AddIfNotAlready(new ParagraphRendererExtension());
        return pipeline;
    }

    public static MarkdownPipelineBuilder UseHeadingsMap(this MarkdownPipelineBuilder pipeline)
    {
        pipeline.Extensions.AddIfNotAlready(new HeadingsMapExtension());
        return pipeline;
    }

    public static MarkdownPipelineBuilder UseCustomContainers(this MarkdownPipelineBuilder pipeline,
        Action<ContainerExtensions>? configure = null)
    {
        var ext = new ContainerExtensions();
        configure?.Invoke(ext);
        pipeline.Extensions.AddIfNotAlready(ext);
        return pipeline;
    }
}

public class DocumentMap
{
    public List<MarkdownMenu> Headings { get; } = new();
}

public class MarkdownMenu
{
    public string? Icon { get; set; }
    public string? Text { get; set; }
    public string? Link { get; set; }
    public string? Id { get; set; }
    public string MenuPath { get; set; }
    public List<MarkdownMenu>? Children { get; set; }
}