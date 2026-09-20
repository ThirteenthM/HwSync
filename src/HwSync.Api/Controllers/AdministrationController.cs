using HwSync.Api.Authentication;
using HwSync.Api.Contracts.Administration;
using HwSync.Api.Handlers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HwSync.Api.Controllers
{
    /// <summary>
    /// Защищённый API просмотра состояния сервера без операций изменения.
    /// </summary>
    [ApiController]
    [Authorize(Policy = AdministrationAuthenticationHandler.ReadPolicy)]
    [Route("api/v1/admin")]
    public sealed class AdministrationController : ControllerBase
    {
        private readonly AdministrationHandler _handler;

        /// <summary>
        /// Принимает обработчик административных запросов.
        /// </summary>
        public AdministrationController(AdministrationHandler handler)
        {
            _handler = handler;
        }

        /// <summary>
        /// Показывает разрешённые настройки сервера.
        /// </summary>
        [HttpGet("settings")]
        public ServerSettingsDto GetSettings() => _handler.GetSettings(User.Identity!.Name!);

        /// <summary>
        /// Показывает папки, известные серверной базе.
        /// </summary>
        [HttpGet("folders")]
        public IReadOnlyList<ServerFolderDto> GetFolders() => _handler.GetFolders();

        /// <summary>
        /// Показывает страницу истории выбранной папки.
        /// </summary>
        [HttpGet("folders/{folderId}/deletions")]
        public ActionResult<DeletionPageDto> GetDeletions(string folderId, long after = 0, int limit = 100)
        {
            if (after < 0 || limit is < 1 or > 500)
            {
                return BadRequest("after должен быть неотрицательным, limit — от 1 до 500.");
            }

            DeletionPageDto? page = _handler.GetDeletions(folderId, after, limit);
            return page is null ? NotFound() : Ok(page);
        }
    }
}
