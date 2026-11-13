using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ReFound.Data;
using ReFound.Models;

namespace ReFound.Controllers
{
    public class FoundItemsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        public FoundItemsController(
            ApplicationDbContext context,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
        }

        // GET: FoundItems
        public async Task<IActionResult> Index()
        {
            return View(await _context.FoundItems.ToListAsync());
        }

        // SmartSEARCH API - Calls local fine-tuned Ollama model
        [HttpGet("/FoundItems/SmartSearch")]
        public async Task<IActionResult> SmartSearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Json(await _context.FoundItems.Select(i => i.Id).ToListAsync());
            }

            var items = await _context.FoundItems
                .Select(i => new { i.Id, i.ItemName, i.Category, i.Description, i.LocationFound })
                .ToListAsync();

            string endpoint = "http://localhost:11434/api/chat";

            var systemPrompt = @"You are a search query matching assistant for a university Lost & Found system.
Analyze the user's query and identify which item IDs from AVAILABLE ITEMS best match the intent, description, brand, color, category, or location.
Return ONLY a JSON array of matching integer IDs (e.g., [1, 4]). Return [] if nothing matches. Do not invent IDs.";

            var userPrompt = $@"USER QUERY: ""{query}""
AVAILABLE ITEMS: {JsonSerializer.Serialize(items)}";

            var requestPayload = new
            {
                model = "refound-model",
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                stream = false,
                format = new
                {
                    type = "array",
                    items = new { type = "integer" }
                },
                options = new
                {
                    temperature = 0
                }
            };

            var client = _httpClientFactory.CreateClient();
            var content = new StringContent(JsonSerializer.Serialize(requestPayload), Encoding.UTF8, "application/json");

            var response = await client.PostAsync(endpoint, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorDetails = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Ollama API Error ({response.StatusCode}): {errorDetails}");
                return StatusCode((int)response.StatusCode, errorDetails);
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonResponse);

            var rawResult = doc.RootElement
                .GetProperty("message")
                .GetProperty("content")
                .GetString()?.Trim() ?? "[]";

            rawResult = rawResult.Replace("```json", "").Replace("```", "").Trim();

            var matchingIds = JsonSerializer.Deserialize<List<int>>(rawResult) ?? new List<int>();
            return Json(matchingIds);
        }

        // GET: FoundItems/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var foundItem = await _context.FoundItems
                .FirstOrDefaultAsync(m => m.Id == id);
            if (foundItem == null)
            {
                return NotFound();
            }

            return View(foundItem);
        }

        // GET: FoundItems/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: FoundItems/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,ItemName,Description,Category,DateFound,LocationFound,Status")] FoundItem foundItem)
        {
            if (ModelState.IsValid)
            {
                _context.Add(foundItem);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(foundItem);
        }

        // GET: FoundItems/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var foundItem = await _context.FoundItems.FindAsync(id);
            if (foundItem == null)
            {
                return NotFound();
            }
            return View(foundItem);
        }

        // POST: FoundItems/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,ItemName,Description,Category,DateFound,LocationFound,Status")] FoundItem foundItem)
        {
            if (id != foundItem.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(foundItem);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!FoundItemExists(foundItem.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(foundItem);
        }

        // GET: FoundItems/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var foundItem = await _context.FoundItems
                .FirstOrDefaultAsync(m => m.Id == id);
            if (foundItem == null)
            {
                return NotFound();
            }

            return View(foundItem);
        }

        // POST: FoundItems/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var foundItem = await _context.FoundItems.FindAsync(id);
            if (foundItem != null)
            {
                _context.FoundItems.Remove(foundItem);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool FoundItemExists(int id)
        {
            return _context.FoundItems.Any(e => e.Id == id);
        }
    }
}