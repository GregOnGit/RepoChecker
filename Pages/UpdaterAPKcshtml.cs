using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace RepoChecker.Pages
{
    public class UpdaterAPKModel : PageModel
    {
        private readonly ILogger<PrivacyModel> _logger;

        public UpdaterAPKModel(ILogger<PrivacyModel> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
            // Nothing for the moment
        }

        public void OnPost()
        {
            
        }
    }
}