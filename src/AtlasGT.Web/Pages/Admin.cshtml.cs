using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;

namespace AtlasGT.Web.Pages
{
    public class AdminModel : PageModel
    {
        private readonly IConfiguration _cfg;
        public AdminModel(IConfiguration cfg) => _cfg = cfg;
        public string ApiBaseUrl => _cfg["AtlasGt:ApiBaseUrl"] ?? "http://localhost:5000";
        public void OnGet() { }
    }
}
