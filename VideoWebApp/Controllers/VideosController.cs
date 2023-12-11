using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using VideoWebApp.Data;
using VideoWebApp.Interface;
using VideoWebApp.Services;

namespace VideoWebApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VideosController : ControllerBase
    {
        private readonly AzureService? _azureService; 
        private readonly ApplicationDbContext _context;
        
        public VideosController(ApplicationDbContext context, IAzureService azureService)
        {
            _context = context;
            _azureService = azureService;
        }

        [HttpGet]
        
    }

    
}