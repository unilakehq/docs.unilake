using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ServiceStack.Text.Controller;

namespace docs.unilake;

public class Page : PageModel
{
    private string _folder = string.Empty;
    private string _slug = string.Empty;

    [FromRoute]
    public string PathInfo { get; set; } = "//";

    public string Folder
    {
        get => string.IsNullOrWhiteSpace(_folder)? PathInfo.Split('/')[0] : _folder;
        set => _folder = value;
    }

    public string Slug
    {
        get => string.IsNullOrWhiteSpace(_slug) ? PathInfo.Split('/')[1..].Join("/") : _slug;
        set => _slug = value;
    }
}