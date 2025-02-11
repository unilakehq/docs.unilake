using Microsoft.AspNetCore.Mvc.RazorPages;

namespace docs.unilake.Pages.Shared;

public class ArticlePage : PageModel
{
    public string Slug { get; set; } = string.Empty;
    public string Folder { get; set; } = string.Empty;
    public MarkdownFileInfo? Doc { get; set; }

    public ArticlePage Init(Microsoft.AspNetCore.Mvc.RazorPages.Page page, MarkdownPages markdown)
    {
        Console.WriteLine("Folder: {0}, Slug: {1}", Folder, Slug);
        var slug = Path.Combine("blog", Folder, Slug);
        Doc = markdown.GetBySlug(slug);
        if (Doc == null)
        {
            page.Response.Redirect("/404");
            return this;
        }

        page.ViewContext.ViewData["Title"] = Doc.Title;
        return this;
    }
}