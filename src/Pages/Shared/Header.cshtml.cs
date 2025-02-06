namespace docs.unilake.Pages.Shared;

public class Header
{
    public string HiddenClass(string page) => (page, "") switch
    {
        _ => "hidden"
    };
}