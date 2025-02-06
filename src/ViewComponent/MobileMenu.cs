using Microsoft.AspNetCore.Mvc;

namespace docs.unilake.Pages.Shared.Components;

public class MobileMenuViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(MobileMenuViewModel model)
    {
        return View(model);
    }
}

public class MobileMenuViewModel
{
    public string Folder { get; set; }
}