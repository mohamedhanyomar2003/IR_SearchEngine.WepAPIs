using IR_SearchEngine.Core.DTOs;
using IR_SearchEngine.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace IR_SearchEngine.WepAPIs.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentsController : ControllerBase
    {
        private readonly IIndexingService _indexingService;

        public DocumentsController(IIndexingService indexingService)
        {
            _indexingService = indexingService;
        }

        // Endpoint: POST api/Documents
        [HttpPost]
        public IActionResult UploadDocument([FromBody] DocumentUploadDto dto)
        {
         
            if (string.IsNullOrWhiteSpace(dto.Content))
            {
                return BadRequest("Content cannot be empty.");
            }

           
            if (dto.Id == 0)
            {
               
                var allDocs = _indexingService.GetAllDocuments();

                int newId = allDocs.Count > 0 ? allDocs.Keys.Max() + 1 : 1;

              
                dto.Id = newId;
            }
            else
            {
              
                if (_indexingService.GetAllDocuments().ContainsKey(dto.Id))
                {
                    return BadRequest($"Error: Document ID {dto.Id} is already taken. Please send '0' to auto-generate.");
                }
            }

            _indexingService.IndexDocument(dto.Id, dto.Content);

            return Ok(new
            {
                message = "Document indexed successfully!",
                generatedId = dto.Id
            });
        }

    }
}
