using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace docs.unilake;

public class Page : PageModel
{
    [FromRoute]
    public string Folder { get; set; }

    [FromRoute]
    public string Slug { get; set; }
}