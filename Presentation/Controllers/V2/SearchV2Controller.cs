using EgyptOnline.Application.Services.Search;
using EgyptOnline.Dtos;
using EgyptOnline.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EgyptOnline.Presentation.Controllers.V2
{
    [ApiController]
    [Route("api/v{version:apiVersion}")]
    [ApiVersion("2.0")]
    [Authorize(Roles = Roles.User)]
    public class SearchV2Controller : ControllerBase
    {
        private readonly SearchService _searchService;
        private readonly ILogger<SearchV2Controller> _logger;

        public SearchV2Controller(SearchService searchService, ILogger<SearchV2Controller> logger)
        {
            _searchService = searchService;
            _logger = logger;
        }

        [HttpGet("workers")]
        public async Task<IActionResult> SearchWorkers([FromQuery] FilterSearchDto? filter)
        {
            try
            {
                var results = await _searchService.SearchWorkersV2Async(filter);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SearchWorkersV2 failed: {Message}", ex.Message);
                return StatusCode(500, new { message = "حدث خطأ داخلي في الخادم", errorCode = "INTERNAL_ERROR" });
            }
        }

        [HttpGet("companies")]
        public async Task<IActionResult> SearchCompanies([FromQuery] FilterSearchDto? filter)
        {
            try
            {
                var results = await _searchService.SearchCompaniesV2Async(filter);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SearchCompaniesV2 failed: {Message}", ex.Message);
                return StatusCode(500, new { message = "حدث خطأ داخلي في الخادم", errorCode = "INTERNAL_ERROR" });
            }
        }

        [HttpGet("contractors")]
        public async Task<IActionResult> SearchContractors([FromQuery] FilterSearchDto? filter)
        {
            try
            {
                var results = await _searchService.SearchContractorsV2Async(filter);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SearchContractorsV2 failed: {Message}", ex.Message);
                return StatusCode(500, new { message = "حدث خطأ داخلي في الخادم", errorCode = "INTERNAL_ERROR" });
            }
        }

        [HttpGet("marketplaces")]
        public async Task<IActionResult> SearchMarketPlaces([FromQuery] FilterSearchDto? filter)
        {
            try
            {
                var results = await _searchService.SearchMarketPlacesV2Async(filter);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SearchMarketPlacesV2 failed: {Message}", ex.Message);
                return StatusCode(500, new { message = "حدث خطأ داخلي في الخادم", errorCode = "INTERNAL_ERROR" });
            }
        }

        [HttpGet("engineers")]
        public async Task<IActionResult> SearchEngineers([FromQuery] FilterSearchDto? filter)
        {
            try
            {
                var results = await _searchService.SearchEngineersV2Async(filter);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SearchEngineersV2 failed: {Message}", ex.Message);
                return StatusCode(500, new { message = "حدث خطأ داخلي في الخادم", errorCode = "INTERNAL_ERROR" });
            }
        }

        [HttpGet("assistants")]
        public async Task<IActionResult> SearchAssistants([FromQuery] FilterSearchDto? filter)
        {
            try
            {
                var results = await _searchService.SearchAssistantsV2Async(filter);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SearchAssistantsV2 failed: {Message}", ex.Message);
                return StatusCode(500, new { message = "حدث خطأ داخلي في الخادم", errorCode = "INTERNAL_ERROR" });
            }
        }

        [HttpGet("sculptors")]
        public async Task<IActionResult> SearchSculptors([FromQuery] FilterSearchDto? filter)
        {
            try
            {
                var results = await _searchService.SearchSculptorsV2Async(filter);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SearchSculptorsV2 failed: {Message}", ex.Message);
                return StatusCode(500, new { message = "حدث خطأ داخلي في الخادم", errorCode = "INTERNAL_ERROR" });
            }
        }
    }
}
