// run node postinstall.js to update to latest version
using ServiceStack.IO;

namespace docs.unilake;

public record FolderMenu(string PageName, string? Icon);

public class MarkdownPages(ILogger<MarkdownPages> log, IWebHostEnvironment env, IVirtualFiles fs)
    : MarkdownPagesBase<MarkdownFileInfo>(log, env, fs)
{
    public override string Id => "pages";
    List<MarkdownFileInfo> Pages { get; set; } = new();
    public List<MarkdownFileInfo> GetVisiblePages(string? prefix=null, bool allDirectories=false) => prefix == null 
        ? Pages.Where(IsVisible).OrderBy(x => x.Order).ThenBy(x => x.Path).ToList()
        : Pages.Where(x => IsVisible(x) && x.Slug!.StartsWith(prefix.WithTrailingSlash()))
            .Where(x => allDirectories || (x.Slug.CountOccurrencesOf('/') == prefix.CountOccurrencesOf('/') + 1))
            .OrderBy(x => x.Order).ThenBy(x => x.Path).ToList();
    public MarkdownFileInfo? GetBySlug(string slug)
    {
        slug = slug.Trim('/');
        return Fresh(Pages.Where(IsVisible).FirstOrDefault(x => x.Slug == slug));
    }
    public Dictionary<string, List<MarkdownMenu>> Sidebars { get; set; } = new();
    public Dictionary<string, FolderMenu[]> FolderMenu { get; set; } = new();
    public void LoadFrom(string fromDirectory)
    {
        Sidebars.Clear();
        FolderMenu.Clear();
        Pages.Clear();
        var files = VirtualFiles.GetDirectory(fromDirectory).GetAllFiles()
            .OrderBy(x => x.VirtualPath)
            .ToList();
        log.LogInformation("Found {Count} pages", files.Count);

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
                        doc.Slug = doc.Slug.Split('/').Select(x => x.GenerateSlug()).Join("/");
                    }

                    Pages.Add(doc);
                }
                else if (file.Name == "menu.json")
                {
                    var virtualPath = file.VirtualPath.Substring(fromDirectory.Length);
                    var folder = virtualPath[..^"menu.json".Length].Trim('/');
                    var folderJson = file.ReadAllText();
                    var folderMenu = folderJson.FromJson<FolderMenu[]>();
                    FolderMenu[folder] = folderMenu;
                }
            }
            catch (Exception e)
            {
                log.LogError(e, "Couldn't load {VirtualPath}: {Message}", file.VirtualPath, e.Message);
            }
        }

        log.LogInformation("Loaded {Count} pages and folder order: {Sidebars}", Pages.Count, FolderMenu.Count);
    }
    public override List<MarkdownFileBase> GetAll() => Pages.Where(IsVisible).Map(doc => ToMetaDoc(doc, x => x.Url = $"/{x.Slug}"));
    public virtual List<MarkdownMenu> GetSidebar(string folder, MarkdownMenu? defaultMenu=null)
    {
        if (Sidebars.TryGetValue(folder, out var sidebar))
            return sidebar;

        var allPages = GetVisiblePages(folder, true);
        sidebar = new List<MarkdownMenu>();
        foreach (var page in allPages)
        {
            MarkdownMenu? menuItem = null;
            var lastItem = page.Path.Split('/').Last();
            var prevPath = page.Path.Split('/')[1];
            foreach (var pathfolder in page.Path.Split('/').Skip(2))
            {
                // new menu item
                var nMenuItem = new MarkdownMenu
                {
                    Text = pathfolder,
                    Icon = page.MenuIcon,
                    MenuPath = Path.Combine(prevPath, pathfolder)
                };
                prevPath = nMenuItem.MenuPath;

                // page
                if (pathfolder == lastItem)
                {
                    nMenuItem.Text = !string.IsNullOrWhiteSpace(page.SidebarLabel) ? page.SidebarLabel : page.Title;
                    nMenuItem.Link = page.Slug;
                    nMenuItem.Id = page.Title?.ToLower().Replace(' ', '_');

                    if(menuItem == null)
                        sidebar.Add(nMenuItem);
                    else if (menuItem.Children == null)
                    {
                        menuItem.Children =
                        [
                            nMenuItem
                        ];
                    }
                    else
                    {
                        menuItem.Children.Add(nMenuItem);
                        menuItem.Children = menuItem.Children.OrderBy(x => x.Text).ToList();
                    }

                    continue;
                }

                // folder
                if (menuItem != null)
                {
                    menuItem.Children ??=
                    [
                        nMenuItem
                    ];
                    var childMenuItem = menuItem.Children.FirstOrDefault(x => x.Text == pathfolder);
                    if (childMenuItem != null)
                    {
                        menuItem = childMenuItem;
                        continue;
                    }

                    menuItem.Children.Add(nMenuItem);
                    menuItem = nMenuItem;
                    continue;
                }

                var found = sidebar.FirstOrDefault(x => x.Text == pathfolder);
                if (found == null)
                {
                    sidebar.Add(nMenuItem);
                    sidebar = sidebar.OrderBy(x => x.Text).ToList();
                    menuItem = nMenuItem;
                }
                else
                    menuItem = found;
            }
        }

        sidebar = SetSideBarOrder(sidebar, folder);
        Sidebars.Add(folder, sidebar);
        return sidebar;
    }
    private List<MarkdownMenu> SetSideBarOrder(List<MarkdownMenu> menu, string menuPath = "")
    {
        foreach (var item in menu.Where(x => x.Children != null))
            item.Children = SetSideBarOrder(item.Children!, item.MenuPath);

        if (string.IsNullOrWhiteSpace(menuPath) || menu.Count <= 0 || !FolderMenu.TryGetValue(menuPath, out var folderMenu))
            // default order
            return menu.OrderBy(x=> x.Text).ToList();

        var orderIndex = folderMenu.ToList().Select((value, index) => new { value, index })
            .ToDictionary(item => item.value.PageName, item => item.index);
        return menu.OrderBy(x => orderIndex.TryGetValue(x.Text ?? "", out var value) ? value : 0).ToList();
    }
}
