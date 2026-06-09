using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthCore.API.Controllers;

[ApiController]
[Route("api/tasks")]
[Authorize]
public class TasksController : ControllerBase
{
    [HttpGet]
    public IActionResult GetTasks()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        var tasks = new[]
        {
            new { Id = Guid.NewGuid(), Title = "Learn Clean Architecture", IsCompleted = true,  UserId = userId },
            new { Id = Guid.NewGuid(), Title = "Implement JWT Auth",        IsCompleted = true,  UserId = userId },
            new { Id = Guid.NewGuid(), Title = "Write integration tests",   IsCompleted = false, UserId = userId },
        };

        return Ok(tasks);
    }
}
