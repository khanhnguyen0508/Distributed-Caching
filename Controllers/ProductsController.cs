using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

using WebApplication1.Data;
using WebApplication1.Hubs;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly IDistributedCache _cache;
        private readonly AppDbContext _db;
        private readonly IHubContext<ProductHub> _hub;

        public ProductsController(IDistributedCache cache, AppDbContext db, IHubContext<ProductHub> hub)
        {
            _cache = cache;
            _db = db;
            _hub = hub;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var cacheKey = "productList";
            var cached = await _cache.GetStringAsync(cacheKey);

            if (cached != null)
            {
                var data = JsonSerializer.Deserialize<List<Product>>(cached);
                return Ok(new { source = "cache", data });
            }

            var dbData = await _db.Products.ToListAsync();
            var json = JsonSerializer.Serialize(dbData);
            await _cache.SetStringAsync(cacheKey, json);

            return Ok(new { source = "db", data = dbData });
        }

        [HttpPost]
        public async Task<IActionResult> Create(Product p)
        {
            _db.Products.Add(p);
            await _db.SaveChangesAsync();

            await RefreshCacheAndNotify();
            return Ok(p);
        }

        private async Task RefreshCacheAndNotify()
        {
            var data = await _db.Products.ToListAsync();
            var json = JsonSerializer.Serialize(data);
            await _cache.SetStringAsync("productList", json);
            await _hub.Clients.All.SendAsync("ProductsUpdated", data);
        }
    }
}
