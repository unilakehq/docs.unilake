using Microsoft.AspNetCore.Mvc.RazorPages;

namespace docs.unilake.Pages.Shared;

public class DocsPage : PageModel
{
    public string? Brand { get; set; }
    public string Slug { get; set; }
    public string Folder { get; set; }
    public MarkdownMenu? DefaultMenu { get; set; }
    public MarkdownFileInfo? Doc { get; set; }
    public Func<dynamic?, object>? Header { get; set; }
    public Func<dynamic?, object>? Footer { get; set; }
    public bool HideTitle { get; set; }
    public bool HideDocumentMap { get; set; }
    public Action<List<MarkdownMenu>>? SidebarFilter { get; set; }
    public List<MarkdownMenu> SidebarMenu { get; set; }

    public DocsPage Init(Microsoft.AspNetCore.Mvc.RazorPages.Page page, MarkdownPages markdown)
    {
        Console.WriteLine("Folder: {0}, Slug: {1}", Folder, Slug);
        if (string.IsNullOrEmpty(Slug))
            Slug = "index";
        if (string.IsNullOrEmpty(Folder))
            Folder = "getting_started";

        Doc = markdown.GetBySlug($"{Folder}/{Slug}");
        if (Doc == null)
        {
            page.Response.Redirect("/404");
            return this;
        }

        SidebarMenu = markdown.GetSidebar(Folder);
        if (!string.IsNullOrEmpty(Brand))
            page.ViewContext.ViewData["Brand"] = Brand;

        page.ViewContext.ViewData["Title"] = Doc.Title;
        return this;
    }

    public bool InPath(string path) => Doc?.Path.Contains(path) ?? true;
    public string ActiveItemClass(string title) => IsActiveItem(title) ? "text-brand bg-backgroundFaded" : "text-bodyText hover:bg-backgroundFaded hover:cursor-pointer";
    public bool IsActiveItem(string title) => Doc?.Title?.ToLower() == title.ToLower();
    public MarkdownMenu? NextPage => GetMenuLinks(SidebarMenu).Where(x => !string.IsNullOrWhiteSpace(x.Link)).SkipWhile(x => !IsActiveItem(x.Text ?? "")).Skip(1).FirstOrDefault();
    public MarkdownMenu? PrevPage => GetMenuLinks(SidebarMenu).Where(x => !string.IsNullOrWhiteSpace(x.Link)).TakeWhile(x => !IsActiveItem(x.Text ?? "")).LastOrDefault();

    private List<MarkdownMenu> GetMenuLinks(List<MarkdownMenu> menus)
    {
        List<MarkdownMenu> items = new List<MarkdownMenu>();
        foreach (var menu in menus)
        {
            if(!menu.Link.IsNullOrEmpty())
                items.Add(menu);
            if(menu.Children!= null)
                items.AddRange(GetMenuLinks(menu.Children));
        }

        return items;
    }
}